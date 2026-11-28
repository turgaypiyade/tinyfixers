using System;
using UnityEngine;

/// <summary>Public, immutable metadata only. Never contains publishing credentials.</summary>
[Serializable]
public sealed class ProgressiveContentManifest
{
    public int schema = 3;
    public string platform;
    public string unityVersion;
    public string appVersion;
    public string baseUrl;
    public int bundledLevels = 50;
    public int packLevels = 50;
    public int prefetchLevels = 15;
    public ProgressiveContentPack[] packs = Array.Empty<ProgressiveContentPack>();
    public ProgressiveContentPack[] cities = Array.Empty<ProgressiveContentPack>();
    public ProgressiveContentPack[] events = Array.Empty<ProgressiveContentPack>();
    public ProgressiveContentPack FindEvent(string id) => Array.Find(events, p => p.eventId == id);

    public ProgressiveContentBundle[] bundles = Array.Empty<ProgressiveContentBundle>();

    public ProgressiveContentPack FindCity(string cityId)
    {
        foreach (var city in cities) if (city.cityId == cityId) return city;
        return null;
    }

    public ProgressiveContentPack FindPack(int level)
    {
        foreach (var pack in packs)
            if (level >= pack.firstLevel && level <= pack.lastLevel) return pack;
        return null;
    }
}

[Serializable]
public sealed class ProgressiveContentPack
{
    public string eventId;
    public string configGuid;
    public string unlockField;
    public string cityId;
    [NonSerialized] private string key;
    public string Key => key ??= !string.IsNullOrEmpty(eventId) ? "event-" + eventId : string.IsNullOrEmpty(cityId) ? "levels-" + firstLevel : "city-" + cityId;
    public int firstLevel, lastLevel;
    public string[] bundles = Array.Empty<string>();
    public ProgressiveContentAsset[] assets = Array.Empty<ProgressiveContentAsset>();
}

[Serializable]
public sealed class ProgressiveContentBundle
{
    public string id;
    public string file;
    public string sha256;
    public uint crc;
    public long bytes;
}

[Serializable]
public sealed class ProgressiveContentAsset
{
    public string sourceGuid; // Event source identity; guid is the unique subasset key.
    public string subAssetName;
    public string assetType;
    public string resourcePath; // City sprite address; independent of its source file location.
    public string guid;
    public string path;
    public string bundle;
}
