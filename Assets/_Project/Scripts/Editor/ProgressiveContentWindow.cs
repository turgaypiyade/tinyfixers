#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

/// <summary>Builds filtered content bundles, uploads/verifies them on explicit operator action,
/// activates the verified manifest, and can produce a local player build. Never records an app release.</summary>
public sealed class ProgressiveContentWindow : EditorWindow
{
    internal const string CatalogPath = "Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset";
    internal const string LibraryPath = "Assets/_Project/Resources/ChapterThemeLibrary.asset";
    internal const string RuntimeManifestPath = "Assets/_Project/Resources/ProgressiveContentManifest.json";
    private string baseUrl = "https://content.wondigames.com";
    private string status;
    private Vector2 scroll;
    private Process runningProcess;
    private string processOutput = "";
    private double nextPoll;
    private Action afterContentPublish;
    internal static string Output => Path.GetFullPath("Exports/ProgressiveContent/" + EditorUserBuildSettings.activeBuildTarget);

    [Serializable] public sealed class SourceStamp { public string path; public string dependencyHash; }
    [Serializable] internal sealed class LocalLevelSource { public string sourceGuid; public string generatedPath; }
    [Serializable] internal sealed class BuildPlan
    {
        public string manifestSha256;
        public string mappingHash;
        public SourceStamp[] sources;
        public ProgressiveCityBuild.Stamp[] citySources;
        public ProgressiveEventBuild.Stamp[] eventSources;
        public string[] duplicatedLocalDependencies;
        public string[] sharedEventAssets;
        public string[] eventLocalDependencies;
        public LocalLevelSource[] localLevels;
    }

    [MenuItem("TinyFixers/Yayın/Parçalı İçerik (R2)")]
    private static void Open() => GetWindow<ProgressiveContentWindow>("Parçalı İçerik");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("İlk 50 seviye için yalnız kullanılan engel tanımları yerelde tutulur. Sonraki seviyeler filtrelenmiş 50'lik kopyalara bölünür; 15 seviye önceden indirilir. Event ve şehir paketleri ayrı kalır. Kaynak LevelV1 asset'leri değiştirilmez; yalnız üretilen kopyalar kullanılır. Bu araç release baseline kaydetmez.", MessageType.Info);
        baseUrl = EditorGUILayout.TextField("R2 özel alan adı", baseUrl);
        EditorGUILayout.LabelField("Hedef", EditorUserBuildSettings.activeBuildTarget.ToString());
        using (new EditorGUI.DisabledScope(runningProcess != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            if (GUILayout.Button("1. Bu platformun içerik paketlerini BUILD ET")) Run(BuildContent);
            if (GUILayout.Button("2. Paketleri R2'ye yükle ve doğrula")) PublishAndVerify(null);
            if (GUILayout.Button("3. Doğrulanmış yayını bu platform için ETKİNLEŞTİR")) Run(Activate);
            if (GUILayout.Button("Yerel içerik moduna geri dön")) Run(RestoreLocal);
            if (GUILayout.Button("Tüm akış: paketle → R2 → doğrula → etkinleştir → player build"))
                Run(BuildEverything);
        }
        EditorGUILayout.HelpBox("Tüm akış R2'ye içerik paketlerini yükler, özel alan adından doğrular, doğrulanmış manifesti etkinleştirir ve player build alır. " +
            "Yükleme için yalnız geliştirici makinesindeki ~/.wondi-cloudflare.env kullanılır. " +
            "Otomatik player build, seçili Android/iOS hedefi için Exports/PlayerBuild altına yeni çıktı üretir. Android player build Windows Editor'de alınır. " +
            "Bu filtre, ObstacleLibrary'ye bağlı level görsellerini ayırır; doğrudan string ile Resources.Load yapılan diğer klasörler ayrıca taşınmalıdır. " +
            "Kaynak paylaşmadan önce Yerel içerik moduna geri dön; üretilen filtreli kopyalar yeniden oluşturulur.", MessageType.None);
        if (runningProcess != null) EditorGUILayout.LabelField("R2 işlemi sürüyor…");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(status ?? "Hazır", GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception e) { status = e.Message; Debug.LogException(e); }
    }

    private void BuildContent()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        if (target != BuildTarget.Android && target != BuildTarget.iOS)
            throw new InvalidOperationException("Önce Android veya iOS platformunu seç.");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host.EndsWith(".r2.dev")
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("Üretim için HTTPS özel alan adı gerekli; r2.dev kullanma.");
        var currentCatalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (File.Exists(RuntimeManifestPath) || ProgressiveCityBuild.HasMovedSources || ProgressiveEventBuild.Active
            || (currentCatalog != null && currentCatalog.entries.Any(e => e != null && !string.IsNullOrEmpty(e.remoteGuid))))
            RestoreLocal();
        AssetDatabase.SaveAssets();
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        var library = AssetDatabase.LoadAssetAtPath<ChapterThemeLibrary>(LibraryPath);
        if (catalog == null || library == null) throw new InvalidOperationException("Üretim kataloğu/tema kütüphanesi eksik.");
        var generatedLevels = ProgressiveLevelPackSources.Prepare(catalog);
        var groups = new SortedDictionary<int, List<string>>();
        var levels = new HashSet<int>();
        foreach (var entry in catalog.entries)
        {
            if (entry == null || !levels.Add(entry.level) || entry.levelData == null)
                throw new InvalidOperationException("Katalogda eksik/tekrarlı level var.");
            if (entry.level <= 50) continue;
            int first = ((entry.level - 1) / 50) * 50 + 1;
            string sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.levelData));
            if (!generatedLevels.sourceGuidToGeneratedPath.TryGetValue(sourceGuid, out string generatedPath))
                throw new BuildFailedException("50'lik level paketi için filtrelenmiş kopya üretilemedi: " + entry.level);
            Add(groups, first, generatedPath);
        }
        for (int level = 1; level <= catalog.MaximumGlobalLevel; level++)
            if (!levels.Contains(level)) throw new InvalidOperationException("Katalog sıralı olmalı; eksik level: " + level);
        for (int chapter = 3; chapter <= (catalog.MaximumGlobalLevel - 1) / library.levelsPerChapter + 1; chapter++)
        {
            var theme = library.GetThemeForChapter(chapter);
            if (theme == null || theme.chapterIndex != chapter) continue;
            int firstLevel = (chapter - 1) * library.levelsPerChapter + 1;
            if (firstLevel <= 50) continue;
            Add(groups, ((firstLevel - 1) / 50) * 50 + 1, AssetDatabase.GetAssetPath(theme));
        }
        if (library.levelsPerChapter != 25) throw new InvalidOperationException("Bu paket düzeni 25 seviyelik bölüm temaları için hazırlanmıştır.");
        if (groups.Count == 0) throw new InvalidOperationException("50 sonrası içerik yok.");

        var owners = new Dictionary<string, HashSet<int>>();
        foreach (var group in groups)
            foreach (var dependency in AssetDatabase.GetDependencies(group.Value.ToArray(), true))
            {
                if (!Bundleable(dependency)) continue;
                if (!owners.TryGetValue(dependency, out var set)) owners[dependency] = set = new HashSet<int>();
                set.Add(group.Key);
            }
        var roots = new HashSet<string>(groups.Values.SelectMany(x => x));
        var shared = owners.Where(x => x.Value.Count > 1 && !roots.Contains(x.Key)).Select(x => x.Key).OrderBy(x => x).ToArray();
        var builds = new List<AssetBundleBuild>();
        foreach (var set in shared.GroupBy(path => string.Join("-", owners[path].OrderBy(first => first))))
            builds.Add(new AssetBundleBuild { assetBundleName = "shared-" + set.Key, assetNames = set.ToArray() });
        foreach (var group in groups)
            builds.Add(new AssetBundleBuild { assetBundleName = "levels-" + group.Key, assetNames = group.Value.OrderBy(x => x).ToArray() });

        var cities = ProgressiveCityBuild.Collect();
        ProgressiveCityBuild.AssertExclusive(cities);
        foreach (var city in cities)
        {
            var cityPaths = city.assets.Select(a => AssetDatabase.GUIDToAssetPath(a.guid)).ToArray();
            if (cityPaths.Any(p => owners.ContainsKey(p)))
                throw new BuildFailedException("Şehir görseli level paketine de bağlı; bağımlılıkları ayır.");
            builds.Add(new AssetBundleBuild { assetBundleName = city.bundles[0], assetNames = cityPaths });
        }

        var events = ProgressiveEventBuild.Collect(builds);
        if (events.Length != ProgressiveEventBuild.Ids.Length) throw new BuildFailedException("All five event art inventories must be present before building.");
        var missionMap = ProgressiveMissionBuild.Collect(builds);
        events = events.Concat(missionMap).ToArray();
        var eventPaths = events.SelectMany(p => p.assets).Select(a => AssetDatabase.GUIDToAssetPath(a.sourceGuid)).ToHashSet();
        if (eventPaths.Any(owners.ContainsKey)) throw new BuildFailedException("Event/mission artwork is also used by level content; separate its local consumer first.");

        string raw = Path.Combine(Output, "raw"), publish = Path.Combine(Output, "publish");
        Directory.CreateDirectory(raw); Directory.CreateDirectory(publish);
        var result = BuildPipeline.BuildAssetBundles(raw, builds.ToArray(),
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target);
        if (result == null) throw new InvalidOperationException("İçerik build'i başarısız.");
        var manifest = new ProgressiveContentManifest {
            platform = target.ToString(), unityVersion = Application.unityVersion, appVersion = Application.version,
            baseUrl = baseUrl.TrimEnd('/') + "/" + target
        };
        var bundles = new List<ProgressiveContentBundle>();
        foreach (string id in result.GetAllAssetBundles().OrderBy(x => x))
        {
            string source = Path.Combine(raw, id), hash = Sha256(source);
            if (!BuildPipeline.GetCRCForAssetBundle(source, out uint crc)) throw new InvalidOperationException("CRC alınamadı: " + id);
            string file = hash + ".bundle";
            File.Copy(source, Path.Combine(publish, file), true);
            bundles.Add(new ProgressiveContentBundle { id = id, file = file, sha256 = hash, bytes = new FileInfo(source).Length, crc = crc });
        }
        manifest.bundles = bundles.ToArray();
        foreach (var city in cities)
            if (result.GetAllDependencies(city.bundles[0]).Length != 0)
                throw new BuildFailedException("Şehir paketi bağımsız olmalı: " + city.cityId);
        manifest.cities = cities;
        foreach (var pack in events)
        {
            pack.bundles = pack.bundles.SelectMany(id => result.GetAllDependencies(id).Concat(new[] { id })).Distinct().ToArray();
            if (pack.bundles.Any(id => id != "event-" + pack.eventId && id != "event-shared"))
                throw new BuildFailedException("Unexpected event dependency: " + pack.eventId);
        }
        manifest.events = events;
        manifest.packs = groups.Select(g => new ProgressiveContentPack {
            firstLevel = g.Key, lastLevel = Math.Min(g.Key + 49, catalog.MaximumGlobalLevel),
            bundles = result.GetAllDependencies("levels-" + g.Key).Concat(new[] { "levels-" + g.Key }).Distinct().ToArray(),
            assets = g.Value.Select(path => new ProgressiveContentAsset {
                guid = generatedLevels.generatedPathToSourceGuid.TryGetValue(path, out string sourceGuid)
                    ? sourceGuid : AssetDatabase.AssetPathToGUID(path),
                path = path.ToLowerInvariant(), bundle = "levels-" + g.Key
            }).ToArray()
        }).ToArray();
        // A partial final pack (e.g. level 251) can reuse the last authored chapter theme.
        // Store that reference in the requesting pack, with the original owner's bundle dependencies.
        var assetOwners = manifest.packs.SelectMany(p => p.assets).ToDictionary(a => a.guid);
        foreach (var pack in manifest.packs)
        {
            var requiredAssets = pack.assets.ToList();
            var requiredBundles = pack.bundles.ToList();
            for (int level = pack.firstLevel; level <= pack.lastLevel; level += 25)
            {
                var theme = library.GetThemeForChapter(library.GetChapterForLevel(level));
                if (theme == null) continue;
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(theme));
                if (!assetOwners.TryGetValue(guid, out var reference) || requiredAssets.Any(a => a.guid == guid)) continue;
                requiredAssets.Add(reference);
                requiredBundles.AddRange(result.GetAllDependencies(reference.bundle));
                requiredBundles.Add(reference.bundle);
            }
            pack.assets = requiredAssets.ToArray();
            pack.bundles = requiredBundles.Distinct().ToArray();
        }
        string manifestPath = Path.Combine(Output, "manifest.json");
        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        var localRoots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
            .Concat(AssetDatabase.GetAllAssetPaths().Where(p => p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p))).ToArray();
        var localDeps = new HashSet<string>(AssetDatabase.GetDependencies(localRoots, true));
        var plan = new BuildPlan {
            manifestSha256 = Sha256(manifestPath),
            mappingHash = MappingHash(),
            citySources = ProgressiveCityBuild.Stamps(cities),
            eventSources = ProgressiveEventBuild.Stamps(events),
            sharedEventAssets = events.SelectMany(p => p.assets).Where(a => a.bundle == "event-shared").Select(a => a.sourceGuid).Distinct().Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray(),
            eventLocalDependencies = AssetDatabase.GetDependencies(eventPaths.ToArray(), true).Where(p => !eventPaths.Contains(p) && localDeps.Contains(p) && Bundleable(p)).OrderBy(p => p).ToArray(),
            localLevels = generatedLevels.sourceGuidToLocalGeneratedPath.Select(p => new LocalLevelSource { sourceGuid = p.Key, generatedPath = p.Value }).ToArray(),
            sources = roots.Concat(generatedLevels.generatedPathToSourcePath.Values).Concat(generatedLevels.generatedLocalPaths).Concat(generatedLevels.localSourcePaths).Distinct().OrderBy(x => x)
                .Select(p => new SourceStamp { path = p, dependencyHash = AssetDatabase.GetAssetDependencyHash(p).ToString() }).ToArray(),
            duplicatedLocalDependencies = owners.Keys.Where(p => !roots.Contains(p) && localDeps.Contains(p)).OrderBy(x => x).ToArray()
        };
        File.WriteAllText(Path.Combine(Output, "plan.json"), JsonUtility.ToJson(plan, true));
        status = $"Filtrelenmiş içerik hazır: ilk 50 yerel kopya + {manifest.packs.Length} seviye paketi + {manifest.cities.Length} şehir + {ProgressiveEventBuild.Ids.Length} event + {missionMap.Length} görev paketi; toplam {bundles.Sum(b => b.bytes) / (1024d * 1024d):0.0} MiB.\n" +
            "Henüz yüklenmedi/etkinleştirilmedi. Çıktı: " + Output;
    }

    private static bool Bundleable(string path) => path.StartsWith("Assets/", StringComparison.Ordinal)
        && !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        && !path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) && !AssetDatabase.IsValidFolder(path);
    private static void Add(SortedDictionary<int, List<string>> groups, int first, string path)
    {
        if (!groups.TryGetValue(first, out var list)) groups[first] = list = new List<string>();
        if (!list.Contains(path)) list.Add(path);
    }
    internal static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static string MappingHash()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        var library = AssetDatabase.LoadAssetAtPath<ChapterThemeLibrary>(LibraryPath);
        var text = new System.Text.StringBuilder();
        foreach (var entry in catalog.entries)
            text.Append(entry.chapter).Append('|').Append(entry.level).Append('|').Append(entry.levelKey).Append('|')
                .Append(!string.IsNullOrEmpty(entry.remoteGuid) ? entry.remoteGuid
                    : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.levelData))).Append('\n');
        text.Append("chapterSize=").Append(library.levelsPerChapter).Append('\n');
        for (int chapter = 1; chapter <= (catalog.MaximumGlobalLevel - 1) / library.levelsPerChapter + 1; chapter++)
            text.Append(chapter).Append('|').Append(AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(library.GetThemeForChapter(chapter)))).Append('\n');
        foreach (var city in ProgressiveCityBuild.Collect())
        {
            text.Append("city=").Append(city.cityId).Append('\n');
            foreach (var asset in city.assets) text.Append(asset.resourcePath).Append('|').Append(asset.guid).Append('\n');
        }
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
    }

    internal static ProgressiveContentManifest ValidatePrepared(bool receipt)
    {
        string path = Path.Combine(Output, "manifest.json");
        var manifest = JsonUtility.FromJson<ProgressiveContentManifest>(File.ReadAllText(path));
        var plan = JsonUtility.FromJson<BuildPlan>(File.ReadAllText(Path.Combine(Output, "plan.json")));
        if ((manifest.schema != 2 && manifest.schema != 3) || Sha256(path) != plan.manifestSha256 || manifest.platform != EditorUserBuildSettings.activeBuildTarget.ToString()
            || manifest.appVersion != Application.version || manifest.unityVersion != Application.unityVersion)
            throw new BuildFailedException("İçerik platform/sürüm bilgisi değişmiş; içerik paketlerini yeniden hazırla.");
        if (plan.mappingHash != MappingHash())
            throw new BuildFailedException("Katalog/tema eşlemesi değişmiş; içerik paketlerini yeniden hazırla.");
        foreach (var source in plan.sources)
            if (AssetDatabase.GetAssetDependencyHash(source.path).ToString() != source.dependencyHash)
                throw new BuildFailedException("İçerik build'inden sonra asset değişmiş: " + source.path);
        ProgressiveCityBuild.ValidateStamps(plan.citySources, manifest.cities);
        if (manifest.schema >= 3) ProgressiveEventBuild.ValidateStamps(plan.eventSources);
        if (receipt && (!File.Exists(Path.Combine(Output, "verified.sha256"))
            || File.ReadAllText(Path.Combine(Output, "verified.sha256")).Trim() != plan.manifestSha256))
            throw new BuildFailedException("Önce Tools/r2_content.py verify ile tüm dosyaları özel alan adından doğrula.");
        return manifest;
    }

    private void Activate()
    {
        var manifest = ValidatePrepared(true);
        var plan = JsonUtility.FromJson<BuildPlan>(File.ReadAllText(Path.Combine(Output, "plan.json")));
        var localSources = (plan.localLevels ?? Array.Empty<LocalLevelSource>()).ToDictionary(x => x.sourceGuid, x => x.generatedPath);
        ProgressiveCityBuild.AssertExclusive(manifest.cities);
        var remote = new HashSet<string>(manifest.packs.SelectMany(p => p.assets).Select(a => a.guid));
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        var library = AssetDatabase.LoadAssetAtPath<ChapterThemeLibrary>(LibraryPath);
        var catalogBackup = EditorJsonUtility.ToJson(catalog);
        var libraryBackup = EditorJsonUtility.ToJson(library);
        var moves = new List<(string from, string to)>();
        byte[] manifestBackup = File.Exists(RuntimeManifestPath) ? File.ReadAllBytes(RuntimeManifestPath) : null;
        try
        {
            // Asset moves and the runtime manifest form one transaction; Unity Undo cannot cover it.
            ProgressiveCityBuild.Move(manifest.cities, true, moves);
            ProgressiveEventBuild.Activate(manifest);
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                int level = entry.FindPropertyRelative("level").intValue;
                var local = entry.FindPropertyRelative("bundledLevelData");
                var data = catalog.entries[i].levelData;
                string guid = !string.IsNullOrEmpty(catalog.entries[i].remoteGuid)
                    ? catalog.entries[i].remoteGuid
                    : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
                if (level <= 50)
                {
                    if (!localSources.TryGetValue(guid, out string localPath))
                        throw new InvalidOperationException("İlk 50 seviye için filtrelenmiş yerel kopya yok: " + level);
                    var filtered = AssetDatabase.LoadAssetAtPath<LevelData>(localPath);
                    if (filtered == null) throw new InvalidOperationException("Filtrelenmiş yerel level dosyası eksik: " + localPath);
                    local.objectReferenceValue = filtered;
                    entry.FindPropertyRelative("remoteGuid").stringValue = guid; // Local copy wins; guid preserves the authored source for restore.
                    entry.FindPropertyRelative("remoteLevelKind").intValue = (int)filtered.levelKind;
                    continue;
                }
                if (!remote.Contains(guid)) throw new InvalidOperationException("Level pakette yok: " + catalog.entries[i].level);
                entry.FindPropertyRelative("remoteGuid").stringValue = guid;
                entry.FindPropertyRelative("remoteLevelKind").intValue = (int)data.levelKind;
                local.objectReferenceValue = null;
            }
            serialized.ApplyModifiedProperties();
            var themes = new SerializedObject(library);
            var localThemes = themes.FindProperty("themes");
            var remoteThemes = themes.FindProperty("remoteThemes");
            remoteThemes.ClearArray();
            // Reconstruct from the package, not from stripped local slots: activation is repeatable
            // when switching Android/iOS or publishing a new revision.
            foreach (var asset in manifest.packs.SelectMany(p => p.assets).GroupBy(a => a.guid).Select(g => g.First()))
            {
                var theme = AssetDatabase.LoadAssetAtPath<ChapterTheme>(AssetDatabase.GUIDToAssetPath(asset.guid));
                if (theme == null) continue;
                int n = remoteThemes.arraySize;
                remoteThemes.arraySize = n + 1;
                var entry = remoteThemes.GetArrayElementAtIndex(n);
                entry.FindPropertyRelative("chapter").intValue = theme.chapterIndex;
                entry.FindPropertyRelative("guid").stringValue = asset.guid;
                for (int i = 0; i < localThemes.arraySize; i++)
                    if (localThemes.GetArrayElementAtIndex(i).objectReferenceValue == theme)
                        localThemes.GetArrayElementAtIndex(i).objectReferenceValue = null;
            }
            themes.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            File.Copy(Path.Combine(Output, "manifest.json"), RuntimeManifestPath, true);
            AssetDatabase.ImportAsset(RuntimeManifestPath);
            ValidatePrepared(true); // Moving a source must not silently change its importer or content.
            ProgressiveCityBuild.ValidateInstalled(manifest);
            ProgressiveEventBuild.ValidateInstalled(manifest);
            status = "Bu platform için etkin. LevelEditor kaynak level asset'lerini GUID üzerinden açar. Release kaydı oluşturulmadı.\n" +
                "UYARI: Bu mod yalnız bu makinede build içindir. Commit etmeden önce 'Yerel içerik moduna geri dön' butonuna bas (git kontrolü de engeller).";
        }
        catch
        {
            EditorJsonUtility.FromJsonOverwrite(catalogBackup, catalog);
            EditorJsonUtility.FromJsonOverwrite(libraryBackup, library);
            EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            if (manifestBackup != null) { File.WriteAllBytes(RuntimeManifestPath, manifestBackup); AssetDatabase.ImportAsset(RuntimeManifestPath); }
            else if (File.Exists(RuntimeManifestPath)) AssetDatabase.DeleteAsset(RuntimeManifestPath);
            ProgressiveCityBuild.Rollback(moves);
            ProgressiveEventBuild.Restore();
            if (manifestBackup != null)
            {
                var previous = JsonUtility.FromJson<ProgressiveContentManifest>(System.Text.Encoding.UTF8.GetString(manifestBackup));
                if (previous.events != null && previous.events.Length > 0) ProgressiveEventBuild.Activate(previous);
            }
            throw;
        }
    }

    private void RestoreLocal()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        var library = AssetDatabase.LoadAssetAtPath<ChapterThemeLibrary>(LibraryPath);
        var cities = ProgressiveCityBuild.Collect();
        var catalogBackup = EditorJsonUtility.ToJson(catalog);
        var libraryBackup = EditorJsonUtility.ToJson(library);
        byte[] manifestBackup = File.Exists(RuntimeManifestPath) ? File.ReadAllBytes(RuntimeManifestPath) : null;
        var moves = new List<(string from, string to)>();
        try
        {
            ProgressiveEventBuild.Restore();
            ProgressiveCityBuild.Move(cities, false, moves);
            foreach (var entry in catalog.entries)
                if (!string.IsNullOrEmpty(entry.remoteGuid))
                {
                    string sourcePath = AssetDatabase.GUIDToAssetPath(entry.remoteGuid);
                    var data = AssetDatabase.LoadAssetAtPath<LevelData>(sourcePath);
                    if (data == null) throw new InvalidOperationException("Level kaynağı bulunamadı: " + entry.level);
                    entry.levelData = data;
                }
            var so = new SerializedObject(library);
            var local = so.FindProperty("themes"); var remote = so.FindProperty("remoteThemes");
            for (int i = 0; i < remote.arraySize; i++)
            {
                var item = remote.GetArrayElementAtIndex(i);
                var theme = AssetDatabase.LoadAssetAtPath<ChapterTheme>(AssetDatabase.GUIDToAssetPath(item.FindPropertyRelative("guid").stringValue));
                if (theme == null) throw new InvalidOperationException("Tema kaynağı bulunamadı.");
                int slot = theme.chapterIndex - 1;
                if (local.arraySize <= slot) local.arraySize = slot + 1;
                local.GetArrayElementAtIndex(slot).objectReferenceValue = theme;
            }
            remote.ClearArray(); so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            if (File.Exists(RuntimeManifestPath) && !AssetDatabase.DeleteAsset(RuntimeManifestPath))
                throw new InvalidOperationException("Runtime manifest kaldırılamadı.");
            status = "Yerel mod geri yüklendi. Uzak paketler silinmedi.";
        }
        catch
        {
            EditorJsonUtility.FromJsonOverwrite(catalogBackup, catalog);
            EditorJsonUtility.FromJsonOverwrite(libraryBackup, library);
            EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            if (manifestBackup != null) { File.WriteAllBytes(RuntimeManifestPath, manifestBackup); AssetDatabase.ImportAsset(RuntimeManifestPath); }
            ProgressiveCityBuild.Rollback(moves);
            ProgressiveEventBuild.Restore();
            if (manifestBackup != null)
            {
                var previous = JsonUtility.FromJson<ProgressiveContentManifest>(System.Text.Encoding.UTF8.GetString(manifestBackup));
                if (previous.events != null && previous.events.Length > 0) ProgressiveEventBuild.Activate(previous);
            }
            throw;
        }
    }

    private void PublishAndVerify(Action onSuccess)
    {
        afterContentPublish = onSuccess;
        RunR2("publish", () => RunR2("verify", () => {
            try
            {
                ValidatePrepared(true);
                if (afterContentPublish != null) Activate();
                afterContentPublish?.Invoke();
                afterContentPublish = null;
            }
            catch (Exception e) { status = e.Message; Debug.LogException(e); }
        }));
    }

    private void RunR2(string command, Action onSuccess)
    {
        if (runningProcess != null) return;
        bool windows = Application.platform == RuntimePlatform.WindowsEditor;
        string python = windows ? "py" : new[] { "/opt/homebrew/bin/python3", "/usr/local/bin/python3", "/usr/bin/python3" }
            .FirstOrDefault(File.Exists) ?? "python3";
        string script = Path.GetFullPath("Tools/r2_content.py");
        var arguments = new[] { script, command, "--directory", Output };
        if (windows) arguments = new[] { "-3" }.Concat(arguments).ToArray();
        if (command == "publish") arguments = arguments.Concat(new[] { "--apply" }).ToArray();
        var start = new ProcessStartInfo {
            FileName = python, Arguments = string.Join(" ", arguments.Select(Quote)),
            WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        processOutput = "";
        try
        {
            runningProcess = Process.Start(start);
            runningProcess.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) processOutput += e.Data + "\n"; };
            runningProcess.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) processOutput += e.Data + "\n"; };
            runningProcess.BeginOutputReadLine(); runningProcess.BeginErrorReadLine();
            status = "R2 " + command + " çalışıyor…";
            EditorApplication.update -= PollR2;
            EditorApplication.update += PollR2;
            pendingR2Success = onSuccess;
        }
        catch (Exception e)
        {
            status = "R2 aracı başlatılamadı: " + e.Message;
            runningProcess?.Dispose(); runningProcess = null;
        }
    }

    private Action pendingR2Success;
    private void PollR2()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || runningProcess == null) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.2;
        if (!runningProcess.HasExited) return;
        int exitCode = runningProcess.ExitCode;
        runningProcess.Dispose(); runningProcess = null;
        EditorApplication.update -= PollR2;
        status = processOutput.Trim();
        if (exitCode == 0) pendingR2Success?.Invoke();
        else pendingR2Success = null;
        Repaint();
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private void BuildPlayer()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        if (target != BuildTarget.iOS && target != BuildTarget.Android)
            throw new BuildFailedException("Player build için iOS veya Android hedefi seç.");
        if (target == BuildTarget.Android && Application.platform != RuntimePlatform.WindowsEditor)
            throw new BuildFailedException("Android player build Windows makinede alınmalı.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new BuildFailedException("Player build'den önce açık sahnelerdeki değişiklikleri kaydet.");

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string suffix = Guid.NewGuid().ToString("N");
        bool androidBundle = target == BuildTarget.Android && EditorUserBuildSettings.buildAppBundle;
        string output = target == BuildTarget.iOS
            ? Path.Combine("Exports/PlayerBuild", "iOS-" + stamp + "-" + suffix)
            : Path.Combine("Exports/PlayerBuild", "Android-" + stamp + "-" + suffix + (androidBundle ? ".aab" : ".apk"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var options = new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = output,
            target = target,
            targetGroup = target == BuildTarget.iOS ? BuildTargetGroup.iOS : BuildTargetGroup.Android,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Player build " + report.summary.result + ". Unity Console/Editor.log'daki ilk gerçek hata satırını incele.");
        if (target == BuildTarget.iOS)
        {
            long dataBytes = DirectoryBytes(Path.Combine(output, "Data"));
            status = $"İçerik R2'ye yüklendi ve doğrulandı. Yayın bu platform için etkinleştirildi. iOS Xcode projesi tamamlandı: {output}. Unity Data: {dataBytes / (1024d * 1024d):0.0} MiB. Bu Xcode proje boyutu veya telefondaki kurulu uygulama boyutu değildir.";
        }
        else
        {
            status = $"İçerik R2'ye yüklendi ve doğrulandı. Yayın bu platform için etkinleştirildi. Player build tamamlandı: {output} ({report.summary.totalSize / (1024d * 1024d):0.0} MiB).";
        }
    }

    private static long DirectoryBytes(string directory)
    {
        if (!Directory.Exists(directory)) return 0;
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
    }

    private void BuildEverything()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        if (target != BuildTarget.iOS && target != BuildTarget.Android)
            throw new BuildFailedException("Player build için iOS veya Android hedefi seç.");
        if (target == BuildTarget.Android && Application.platform != RuntimePlatform.WindowsEditor)
            throw new BuildFailedException("Android player build Windows Editor'de alınmalı.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new BuildFailedException("Tüm akıştan önce açık sahnelerdeki değişiklikleri kaydet.");
        BuildContent();
        // Etkin (uzak) mod yalnız build süresince yaşar: player build bitince — başarılı ya da değil — repo
        // yerel moda döner. Böylece git'e asla makineye özel üretilmiş kopyalara/platform manifestine
        // bağlı katalog girmez; Mac (iOS) ve Windows (Android) birbirinin manifestini ezmez.
        PublishAndVerify(() =>
        {
            try { BuildPlayer(); }
            finally
            {
                string built = status;
                try { RestoreLocal(); status = built + "\n\nRepo yerel moda döndü; commit için güvenli."; }
                catch (Exception e) { status = built + "\n\nYEREL MODA DÖNÜLEMEDİ: " + e.Message + " — commit etmeden önce 'Yerel içerik moduna geri dön' butonuna bas."; Debug.LogException(e); }
            }
        });
    }

    private void OnDisable()
    {
        EditorApplication.update -= PollR2;
        // R2 CLI runs independently; closing the window must not interrupt an upload.
        runningProcess?.Dispose();
        runningProcess = null;
    }

}

public sealed class ProgressiveContentBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProgressiveContentWindow.CatalogPath);
        bool stripped = catalog != null && catalog.entries.Any(e => !string.IsNullOrEmpty(e.remoteGuid));
        if (!File.Exists(ProgressiveContentWindow.RuntimeManifestPath))
        {
            if (stripped || ProgressiveCityBuild.HasMovedSources || ProgressiveEventBuild.Active) throw new BuildFailedException("İndirilebilir level referansları var ama manifest yok. Yerel moda dön veya yayını etkinleştir.");
            return;
        }
        var manifest = ProgressiveContentWindow.ValidatePrepared(true);
        ProgressiveCityBuild.ValidateInstalled(manifest);
        ProgressiveEventBuild.ValidateInstalled(manifest);
        if (ProgressiveContentWindow.Sha256(ProgressiveContentWindow.RuntimeManifestPath)
            != ProgressiveContentWindow.Sha256(Path.Combine(ProgressiveContentWindow.Output, "manifest.json")))
            throw new BuildFailedException("Aktif manifest hazırlanan içerikle aynı değil; platform için yeniden etkinleştir.");
    }
}
#endif
