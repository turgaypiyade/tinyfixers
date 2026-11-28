using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// One download worker, immutable files, verified atomic disk cache. Prefetch only downloads;
/// Unity assets are loaded before entering the level/menu, never installed into a running board.
/// Activated by the editor publishing workflow, not merely by adding this component.
/// </summary>
public sealed partial class ProgressiveContent : MonoBehaviour
{
    private static ProgressiveContent instance;
    private static bool attempted;
    private ProgressiveContentManifest manifest;
    private readonly Dictionary<string, ProgressiveContentBundle> definitions = new();
    private readonly Dictionary<string, AssetBundle> loadedBundles = new();
    private readonly Dictionary<string, Object> loadedAssets = new();
    private readonly HashSet<string> verifiedFiles = new();
    private readonly HashSet<string> readyPacks = new();
    private readonly Queue<ProgressiveContentPack> foreground = new();
    private readonly HashSet<string> queued = new();
    private readonly Dictionary<string, string> failures = new();
    private UnityWebRequest request;
    private string cacheDirectory;
    private string partialFile;
    private bool paused;
    private bool foregroundActive;
    private float nextPrefetch;
    private int lastPrefetchLevel = -1;
    private readonly HashSet<int> gatedOwners = new();
    public bool DownloadingForView => foregroundActive && request != null && !request.isDone;
    public float Progress { get; private set; }
    public long DownloadBytes { get; private set; }
    public string LastError { get; private set; }
    public static bool Enabled => Get() != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; attempted = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { Get(); }

    private static ProgressiveContent Get()
    {
        if (instance != null) return instance;
#if UNITY_EDITOR
        return null; // Editor/LevelEditor always resolve the source assets directly.
#else
        if (attempted || !Application.isPlaying || RuntimeSimulationSession.IsActive) return null;
        attempted = true;
        var text = Resources.Load<TextAsset>("ProgressiveContentManifest");
        if (text == null) return null; // The existing local game remains untouched until activation.
        ProgressiveContentManifest data;
        try { data = JsonUtility.FromJson<ProgressiveContentManifest>(text.text); }
        catch (Exception) { Debug.LogError("[Content] Invalid content manifest."); return null; }
        if (!Validate(data)) { Debug.LogError("[Content] Incompatible content manifest."); return null; }
        var go = new GameObject("ProgressiveContent");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ProgressiveContent>();
        instance.manifest = data;
        instance.cacheDirectory = Path.Combine(Application.persistentDataPath, "content-v1", data.platform);
        foreach (var b in data.bundles) instance.definitions.Add(b.id, b);
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
        FirebaseCloudSaveService.OnRestored += instance.OnCloudRestored;
        instance.PruneObsoleteFiles();
        instance.StartCoroutine(instance.Worker());
        return instance;
#endif
    }

    public static string PlatformName => Application.platform == RuntimePlatform.IPhonePlayer ? "iOS"
        : Application.platform == RuntimePlatform.Android ? "Android" : "Editor";

    private static bool Validate(ProgressiveContentManifest data)
    {
        if (data == null || (data.schema != 2 && data.schema != 3) || data.bundledLevels != 50 || data.packLevels != 50
            || data.packs == null || data.cities == null || data.bundles == null || data.unityVersion != Application.unityVersion
            || data.appVersion != Application.version || !Uri.TryCreate(data.baseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        if (data.platform != PlatformName || data.prefetchLevels < 0) return false;
        data.events ??= Array.Empty<ProgressiveContentPack>();
        var ids = new HashSet<string>();
        foreach (var b in data.bundles)
            if (b == null || string.IsNullOrEmpty(b.id) || !ids.Add(b.id) || b.bytes <= 0
                || b.sha256 == null || b.sha256.Length != 64 || b.file != b.sha256 + ".bundle"
                || !IsHex(b.sha256)) return false;
        int previous = 50;
        var guids = new Dictionary<string, string>();
        foreach (var p in data.packs)
        {
            if (p == null || !string.IsNullOrEmpty(p.cityId) || p.firstLevel != previous + 1 || p.lastLevel < p.firstLevel
                || p.lastLevel - p.firstLevel >= 50 || p.bundles == null || p.assets == null) return false;
            foreach (var id in p.bundles) if (!ids.Contains(id)) return false;
            foreach (var a in p.assets)
            {
                if (a == null || string.IsNullOrEmpty(a.guid) || string.IsNullOrEmpty(a.path)
                    || Array.IndexOf(p.bundles, a.bundle) < 0) return false;
                string address = a.bundle + ":" + a.path;
                if (guids.TryGetValue(a.guid, out var existing) && existing != address) return false;
                guids[a.guid] = address;
            }
            previous = p.lastLevel;
        }
        var cityIds = new HashSet<string>();
        var cityBundles = new HashSet<string>();
        foreach (var city in data.cities)
        {
            if (city == null || string.IsNullOrEmpty(city.cityId) || !cityIds.Add(city.cityId)
                || city.firstLevel != 0 || city.lastLevel != 0 || city.bundles == null
                || city.bundles.Length != 1 || !ids.Contains(city.bundles[0])
                || !cityBundles.Add(city.bundles[0]) || city.assets == null || city.assets.Length != 2) return false;
            var addresses = new HashSet<string>();
            foreach (var asset in city.assets)
            {
                if (asset == null || string.IsNullOrEmpty(asset.guid) || guids.ContainsKey(asset.guid)
                    || string.IsNullOrEmpty(asset.path) || asset.bundle != city.bundles[0]
                    || (asset.resourcePath != "WorldTour/City_" + city.cityId
                        && asset.resourcePath != "WorldTour/City_" + city.cityId + "_Site")
                    || !addresses.Add(asset.resourcePath)) return false;
                guids[asset.guid] = asset.bundle + ":" + asset.path;
            }
        }
        // City bundles are exclusive: safe to unload textures after the last city view closes.
        foreach (var pack in data.packs)
            foreach (string id in pack.bundles) if (cityBundles.Contains(id)) return false;
        var eventIds = new HashSet<string>();
        var nonEventBundles = new HashSet<string>(cityBundles);
        foreach (var pack in data.packs) foreach (string id in pack.bundles) nonEventBundles.Add(id);
        foreach (var pack in data.events)
        {
            bool mission = pack?.eventId?.StartsWith("mission-", StringComparison.Ordinal) == true;
            string expectedUnlock = mission ? "on-demand"
                : pack != null && pack.eventId == EventContentPolicy.Gift ? "unlockAfterLevel"
                : pack != null && pack.eventId == EventContentPolicy.Claw ? "unlockLevel" : "minLevelGate";
            if (pack == null || !eventIds.Add(pack.eventId ?? "") || string.IsNullOrEmpty(pack.configGuid)
                || pack.unlockField != expectedUnlock
                || !mission && Array.IndexOf(new[] { EventContentPolicy.Safari, EventContentPolicy.Bridge, EventContentPolicy.Harvest,
                    EventContentPolicy.Gift, EventContentPolicy.Gear, EventContentPolicy.Claw, EventContentPolicy.MissionMap }, pack.eventId) < 0
                || !string.IsNullOrEmpty(pack.cityId) || pack.firstLevel != 0 || pack.lastLevel != 0
                || pack.bundles == null || pack.bundles.Length == 0 || pack.assets == null || pack.assets.Length == 0) return false;
            foreach (string id in pack.bundles)
                if (!ids.Contains(id) || nonEventBundles.Contains(id) || (id != "event-" + pack.eventId && id != "event-shared")) return false;
            foreach (var asset in pack.assets)
            {
                if (asset == null || string.IsNullOrEmpty(asset.guid) || string.IsNullOrEmpty(asset.sourceGuid)
                    || string.IsNullOrEmpty(asset.path) || string.IsNullOrEmpty(asset.subAssetName)
                    || (asset.assetType != "Sprite" && asset.assetType != "Material" && asset.assetType != "Texture2D")
                    || Array.IndexOf(pack.bundles, asset.bundle) < 0) return false;
                string address = asset.bundle + ":" + asset.path + ":" + asset.assetType + ":" + asset.subAssetName;
                if (guids.TryGetValue(asset.guid, out var existing) && existing != address) return false;
                guids[asset.guid] = address;
            }
        }
        return true;
    }

    private static bool IsHex(string value)
    {
        foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
        return true;
    }

    public static T Resolve<T>(string guid) where T : Object
    {
        if (string.IsNullOrEmpty(guid)) return null;
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
#else
        var self = Get();
        return self != null && self.loadedAssets.TryGetValue(guid, out var value) ? value as T : null;
#endif
    }

    public static bool IsReady(int level)
    {
        var self = Get();
        if (self == null || level <= self.manifest.bundledLevels) return true;
        var pack = self.manifest.FindPack(level);
        // Levels beyond the authored catalog are handled by MasterLeague, not a nonexistent pack.
        return pack == null || self.readyPacks.Contains(pack.Key);
    }

    /// <summary>Ensures both the played level and the current progression's theme (league can differ).</summary>
    public static IEnumerator RequireCurrent()
    {
        yield return Require(CurrentLevel.Global);
        if (CurrentLevel.Played != CurrentLevel.Global) yield return Require(CurrentLevel.Played);
    }

    public static IEnumerator Require(int level)
    {
        var self = Get();
        if (self == null || IsReady(level)) yield break;
        var pack = self.manifest.FindPack(level);
        if (pack != null) yield return self.RequirePack(pack);
    }

    private bool PackReady(ProgressiveContentPack pack, string cityAsset)
        => readyPacks.Contains(pack.Key) && (cityAsset == null || loadedAssets.ContainsKey(cityAsset));

    private IEnumerator RequirePack(ProgressiveContentPack pack, string cityAsset = null, Func<bool> cancelled = null, Action cancel = null)
    {
        if (PackReady(pack, cityAsset)) yield break;
        var overlay = ContentDownloadOverlay.Open(this, cancel);
        try
        {
            while (!PackReady(pack, cityAsset) && !(cancelled?.Invoke() ?? false))
            {
                failures.Remove(pack.Key);
                if (cityAsset != null) requestedCityAssets[pack.Key] = cityAsset;
                if (queued.Add(pack.Key)) foreground.Enqueue(pack);
                while (!PackReady(pack, cityAsset) && !failures.ContainsKey(pack.Key)
                    && !(cancelled?.Invoke() ?? false)) yield return null;
                if (PackReady(pack, cityAsset) || (cancelled?.Invoke() ?? false)) break;
                int retryVersion = overlay.RetryVersion;
                overlay.ShowFailure();
                while (overlay != null && overlay.RetryVersion == retryVersion
                    && !(cancelled?.Invoke() ?? false)) yield return null;
                if (overlay != null) overlay.ShowProgress();
            }
        }
        finally { if (overlay != null) overlay.Release(cancel); }
    }

    public static void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        var self = Get();
        if (self == null || (IsReady(CurrentLevel.Global) && IsReady(CurrentLevel.Played)))
            SceneManager.LoadScene(sceneName, mode);
        else if (!self.sceneTransitionPending)
            self.StartCoroutine(self.LoadSceneRoutine(sceneName, mode));
    }
    private bool sceneTransitionPending;
    private IEnumerator LoadSceneRoutine(string sceneName, LoadSceneMode mode)
    {
        sceneTransitionPending = true;
        yield return RequireCurrent();
        sceneTransitionPending = false;
        SceneManager.LoadScene(sceneName, mode);
    }

    private IEnumerator Worker()
    {
        var poll = new WaitForSecondsRealtime(1f);
        while (true)
        {
            if (releaseUnusedRequested)
            {
                releaseUnusedRequested = false;
                yield return ReleaseUnusedPacks();
            }
            ReleaseUnusedCities();
            ReleaseUnusedEvents();
            if (paused) { yield return poll; continue; }
            if (foreground.Count > 0)
            {
                var pack = foreground.Dequeue();
                if (!string.IsNullOrEmpty(pack.cityId) && !cityUsers.ContainsKey(pack.cityId))
                { queued.Remove(pack.Key); continue; }
                foregroundActive = true;
                yield return DownloadPack(pack);
                if (LastError == null && !CityRequestCancelled(pack) && !EventRequestCancelled(pack)) yield return LoadPack(pack);
                if (LastError != null) failures[pack.Key] = LastError;
                queued.Remove(pack.Key);
                foregroundActive = false;
                continue;
            }
            int level = CurrentLevel.Global;
            if ((level != lastPrefetchLevel || Time.realtimeSinceStartup >= nextPrefetch)
                && FirebaseCloudSaveService.RestoreResolved)
            {
                lastPrefetchLevel = level;
                nextPrefetch = Time.realtimeSinceStartup + 60f;
                // Reinstall/late cloud restore: start with the restored level, never download
                // all intervening packs. Level changes wake prefetch within this one-second poll.
                var currentPack = manifest.FindPack(level);
                if (currentPack != null) yield return DownloadPack(currentPack);
                var nextPack = manifest.FindPack(level + manifest.prefetchLevels + 1);
                if (nextPack != null && nextPack != currentPack && (currentPack == null || LastError == null))
                    yield return DownloadPack(nextPack);
                if (foreground.Count == 0) yield return PrefetchEvents();
                if (foreground.Count == 0 && cityCatalog != null && ChapterProgress.HasLibrary)
                {
                    int slot = Mathf.Clamp(ChapterProgress.Current - 1, 0, cityCatalog.Count - 1);
                    var city = CityForSlot(slot);
                    if (city != null) yield return DownloadPack(city);
                    if (foreground.Count == 0 && slot + 1 < cityCatalog.Count
                        && (long)level + manifest.prefetchLevels + 1 >= ChapterProgress.FirstLevelOfChapter(slot + 2))
                    {
                        var nextCity = CityForSlot(slot + 1);
                        if (nextCity != null) yield return DownloadPack(nextCity);
                    }
                }
            }
            yield return poll;
        }
    }

    private IEnumerator DownloadPack(ProgressiveContentPack pack)
    {
        LastError = null;
        Progress = 0;
        DownloadBytes = 0;
        foreach (var id in pack.bundles) DownloadBytes += definitions[id].bytes;
        long done = 0;
        foreach (var id in pack.bundles)
        {
            if ((!foregroundActive && foreground.Count > 0) || CityRequestCancelled(pack) || EventRequestCancelled(pack)) yield break;
            var b = definitions[id];
            string path = Path.Combine(cacheDirectory, b.file);
            if (!verifiedFiles.Contains(b.file))
            {
                var validation = Task.Run(() => Verify(path, b));
                while (!validation.IsCompleted) yield return null;
                if (validation.Result) verifiedFiles.Add(b.file);
            }
            if (!verifiedFiles.Contains(b.file))
            {
                bool success = false;
                for (int attempt = 0; attempt < 3 && !success; attempt++)
                {
                    while (paused || Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        if (!foregroundActive) { LastError = "offline"; yield break; }
                        LastError = "offline";
                        yield break; // Foreground UI offers retry instead of an endless spinner.
                    }
                    partialFile = path + ".part";
                    var op = BeginDownload(b);
                    if (op == null) { LastError = "download_or_storage"; yield break; }
                    ulong lastBytes = 0;
                    float lastProgressAt = Time.realtimeSinceStartup;
                    while (!op.isDone)
                    {
                        CleanupCitiesWhileDownloading();
                        if (request.downloadedBytes != lastBytes)
                        {
                            lastBytes = request.downloadedBytes;
                            lastProgressAt = Time.realtimeSinceStartup;
                        }
                        if (Time.realtimeSinceStartup - lastProgressAt > 30f || request.downloadedBytes > (ulong)b.bytes) request.Abort();
                        Progress = DownloadBytes > 0 ? Mathf.Clamp01((done + (float)request.downloadedBytes) / DownloadBytes) : 0;
                        if (paused || (!foregroundActive && foreground.Count > 0) || CityRequestCancelled(pack) || EventRequestCancelled(pack)) request.Abort();
                        yield return null;
                    }
                    bool downloaded = request.result == UnityWebRequest.Result.Success;
                    request.Dispose(); request = null;
                    if (downloaded)
                    {
                        string downloadedPath = partialFile;
                        var validation = Task.Run(() => Verify(downloadedPath, b));
                        while (!validation.IsCompleted) yield return null;
                        success = validation.Result;
                        if (success)
                        {
                            try
                            {
                                if (File.Exists(path)) File.Delete(path);
                                File.Move(partialFile, path);
                                verifiedFiles.Add(b.file);
                            }
                            catch (IOException) { success = false; }
                            catch (UnauthorizedAccessException) { success = false; }
                        }
                    }
                    DeletePartial();
                    if ((!foregroundActive && foreground.Count > 0) || CityRequestCancelled(pack) || EventRequestCancelled(pack)) yield break;
                    if (!success && attempt < 2) yield return new WaitForSecondsRealtime(1 << attempt);
                }
                if (!success) { LastError = "download_or_storage"; yield break; }
            }
            done += b.bytes;
            Progress = DownloadBytes > 0 ? (float)done / DownloadBytes : 1;
        }
    }

    private UnityWebRequestAsyncOperation BeginDownload(ProgressiveContentBundle bundle)
    {
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            request = new UnityWebRequest(manifest.baseUrl.TrimEnd('/') + "/" + bundle.file, UnityWebRequest.kHttpVerbGET);
            request.downloadHandler = new DownloadHandlerFile(partialFile) { removeFileOnAbort = true };
            request.timeout = 600; // Slow but progressing mobile downloads are allowed; stalls abort after 30s.
            return request.SendWebRequest();
        }
        catch (Exception)
        {
            request?.Dispose(); request = null;
            DeletePartial();
            return null;
        }
    }

    private static bool Verify(string path, ProgressiveContentBundle b)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length != b.bytes) return false;
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            return string.Equals(hash, b.sha256, StringComparison.Ordinal);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private IEnumerator LoadPack(ProgressiveContentPack pack)
    {
        foreach (var id in pack.bundles)
        {
            if (loadedBundles.ContainsKey(id)) continue;
            var b = definitions[id];
            var op = AssetBundle.LoadFromFileAsync(Path.Combine(cacheDirectory, b.file), b.crc);
            yield return op;
            if (op.assetBundle == null)
            {
                verifiedFiles.Remove(b.file);
                try { File.Delete(Path.Combine(cacheDirectory, b.file)); } catch (IOException) { }
                LastError = "bundle_incompatible";
                yield break;
            }
            loadedBundles.Add(id, op.assetBundle);
        }
        foreach (var asset in pack.assets)
        {
            bool city = !string.IsNullOrEmpty(pack.cityId);
            if (city && (!requestedCityAssets.TryGetValue(pack.Key, out var wanted) || wanted != asset.guid)) continue;
            if (loadedAssets.ContainsKey(asset.guid)) continue;
            if (!string.IsNullOrEmpty(pack.eventId))
            {
                yield return LoadEventAsset(pack, asset);
                if (LastError != null) yield break;
                continue;
            }
            var op = city ? loadedBundles[asset.bundle].LoadAssetAsync<Sprite>(asset.path)
                : loadedBundles[asset.bundle].LoadAssetAsync<Object>(asset.path);
            yield return op;
            if (op.asset == null) { LastError = "asset_missing"; yield break; }
            loadedAssets.Add(asset.guid, op.asset);
        }
        readyPacks.Add(pack.Key);
    }

    // Callback gates run on the persistent service: inactive prelevel popups cannot run coroutines.
    public static bool Gate(MonoBehaviour owner, Action onReady)
    {
        var self = Get();
        if (self == null || (IsReady(CurrentLevel.Global) && IsReady(CurrentLevel.Played))) return false;
        int id = owner.GetInstanceID();
        if (self.gatedOwners.Add(id)) self.StartCoroutine(self.GateRoutine(owner, id, onReady));
        return true;
    }
    private IEnumerator GateRoutine(MonoBehaviour owner, int id, Action onReady)
    {
        yield return RequireCurrent();
        gatedOwners.Remove(id);
        if (owner != null) onReady();
    }

    private bool releaseUnusedRequested;
    private void OnCloudRestored() { nextPrefetch = 0; }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) releaseUnusedRequested = true;
    }
    private IEnumerator ReleaseUnusedPacks()
    {
        // Previous scene consumers have torn down. Unload(false) preserves any outstanding references;
        // Unity's next scene cleanup reclaims assets after the last consumer releases them.
        yield return null;
        var keep = new HashSet<string>();
        var keepAssets = new HashSet<string>();
        int global = CurrentLevel.Global, played = CurrentLevel.Played;
        foreach (var pack in manifest.packs)
        {
            bool active = (global >= pack.firstLevel && global <= pack.lastLevel)
                || (played >= pack.firstLevel && played <= pack.lastLevel)
                || queued.Contains(pack.Key);
            if (active)
            {
                foreach (var id in pack.bundles) keep.Add(id);
                foreach (var asset in pack.assets) keepAssets.Add(asset.guid);
                continue;
            }
            readyPacks.Remove(pack.Key);
        }
        foreach (var city in manifest.cities)
        {
            foreach (var id in city.bundles) keep.Add(id);
            foreach (var asset in city.assets) keepAssets.Add(asset.guid);
        }
        foreach (var pack in manifest.events)
        {
            foreach (var id in pack.bundles) keep.Add(id);
            foreach (var asset in pack.assets) keepAssets.Add(asset.guid);
        }
        var releaseAssets = new List<string>();
        foreach (var item in loadedAssets) if (!keepAssets.Contains(item.Key)) releaseAssets.Add(item.Key);
        foreach (var guid in releaseAssets) loadedAssets.Remove(guid);
        var release = new List<string>();
        foreach (var item in loadedBundles)
            if (!keep.Contains(item.Key)) release.Add(item.Key);
        foreach (var id in release)
        {
            loadedBundles[id].Unload(false);
            loadedBundles.Remove(id);
        }
    }

    private void PruneObsoleteFiles()
    {
        var keep = new HashSet<string>();
        foreach (var b in manifest.bundles) keep.Add(b.file);
        // Only our dedicated cache directory, never player saves. Valid packs remain available offline.
        try
        {
            foreach (string path in Directory.EnumerateFiles(cacheDirectory))
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith(".part", StringComparison.Ordinal)
                    || (name.EndsWith(".bundle", StringComparison.Ordinal) && !keep.Contains(name)))
                    File.Delete(path);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void OnApplicationPause(bool value)
    {
        paused = value;
        if (!value) nextPrefetch = 0;
        if (value) request?.Abort();
    }
    private void DeletePartial()
    {
        if (partialFile == null) return;
        try { if (File.Exists(partialFile)) File.Delete(partialFile); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        partialFile = null;
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        FirebaseCloudSaveService.OnRestored -= OnCloudRestored;
        StopAllCoroutines();
        request?.Abort(); request?.Dispose(); request = null;
        DeletePartial();
        foreach (var bundle in loadedBundles.Values) if (bundle != null) bundle.Unload(false);
        loadedBundles.Clear(); loadedAssets.Clear();
        if (instance == this) instance = null;
    }
}
