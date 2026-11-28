using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class ProgressiveContent
{
    private WonderCatalog cityCatalog;
    private readonly Dictionary<string, int> cityUsers = new();
    private readonly Dictionary<string, string> requestedCityAssets = new();

    public static void RegisterCities(WonderCatalog catalog)
    {
        var self = Get();
        if (self == null || catalog == null || catalog.Count == 0 || self.cityCatalog == catalog) return;
        self.cityCatalog = catalog;
        self.nextPrefetch = 0;
    }

    private ProgressiveContentPack CityForSlot(int slot)
    {
        var wonder = cityCatalog.Get(slot); // Uses the restored, country-specific WonderOrder.
        return wonder != null ? manifest.FindCity(wonder.wonderId) : null;
    }

    public static bool HasRemoteCity(string cityId) => Get()?.manifest.FindCity(cityId) != null;

    public static Sprite LocalCitySprite(string resourcePath)
    {
#if UNITY_EDITOR
        // Source GUIDs survive activation moves; editor previews never need a network request.
        var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/RemoteContent/" + resourcePath + ".png");
        if (sprite != null) return sprite;
#endif
        return Resources.Load<Sprite>(resourcePath);
    }

    /// <summary>The view owns one city lease until OnDestroy. Only the requested panorama is decoded.</summary>
    public static void OpenCity(MonoBehaviour owner, string cityId, bool completed, Action<Sprite> onReady)
    {
        var self = Get();
        var pack = self?.manifest.FindCity(cityId);
        if (pack == null) { onReady(null); return; }
        self.cityUsers.TryGetValue(cityId, out int users);
        self.cityUsers[cityId] = users + 1;
        self.StartCoroutine(self.OpenCityRoutine(owner, pack, completed, onReady));
    }

    private IEnumerator OpenCityRoutine(MonoBehaviour owner, ProgressiveContentPack pack, bool completed, Action<Sprite> onReady)
    {
        bool cancelled = false;
        string address = "WorldTour/City_" + pack.cityId + (completed ? "" : "_Site");
        var asset = Array.Find(pack.assets, a => a.resourcePath == address);
        if (asset == null) { if (owner != null) onReady(null); yield break; }
        yield return RequirePack(pack, asset.guid, () => owner == null || cancelled, () => cancelled = true);
        if (owner != null) onReady(cancelled ? null : Resolve<Sprite>(asset.guid));
    }

    public static void ReleaseCity(string cityId)
    {
        // Do not create a new service during scene/application teardown.
        var self = instance;
        if (self == null || string.IsNullOrEmpty(cityId) || !self.cityUsers.TryGetValue(cityId, out int users)) return;
        if (users <= 1) self.cityUsers.Remove(cityId); else self.cityUsers[cityId] = users - 1;
    }

    private bool CityRequestCancelled(ProgressiveContentPack pack)
        => foregroundActive && !string.IsNullOrEmpty(pack.cityId) && !cityUsers.ContainsKey(pack.cityId);

    private float nextCityCleanup;
    private void CleanupCitiesWhileDownloading()
    {
        if (Time.realtimeSinceStartup < nextCityCleanup) return;
        nextCityCleanup = Time.realtimeSinceStartup + 1f;
        ReleaseUnusedCities(); // Network I/O does not load Unity objects; no async asset load is in flight here.
    }

    private void ReleaseUnusedCities()
    {
        // Called only between worker operations: never unload under an async bundle/sprite load.
        foreach (var city in manifest.cities)
        {
            if (cityUsers.ContainsKey(city.cityId) || queued.Contains(city.Key)) continue;
            foreach (var asset in city.assets) loadedAssets.Remove(asset.guid);
            foreach (var id in city.bundles)
                if (loadedBundles.TryGetValue(id, out var bundle))
                {
                    bundle.Unload(true); // Validated exclusive city bundle; the view has been destroyed.
                    loadedBundles.Remove(id);
                }
            readyPacks.Remove(city.Key);
            requestedCityAssets.Remove(city.Key);
        }
    }
}
