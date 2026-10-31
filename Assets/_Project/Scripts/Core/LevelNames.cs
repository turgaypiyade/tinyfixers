using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level isimleri — tek kaynak Resources/LevelNames.json (name = TR, nameEn = EN).
/// Anahtar katalogdaki global `level` alanıdır (asset numarası değil). Dil cihaz diline göre
/// (GameLocalization.CurrentLanguage); karşılığı olmayan dilde boş döner — başka dilde isim gösterilmez.
/// </summary>
public static class LevelNames
{
    private const string ResourcePath = "LevelNames";

    [Serializable] private sealed class Entry { public int level; public string name; public string nameEn; }
    [Serializable] private sealed class File { public Entry[] entries; }

    private static Dictionary<int, Entry> byLevel;

    /// <summary>Seviyenin geçerli dildeki ismi; yoksa boş string.</summary>
    public static string Get(int level)
    {
        EnsureLoaded();
        if (!byLevel.TryGetValue(level, out Entry entry)) return string.Empty;
        string name = GameLocalization.CurrentLanguage == "tr" ? entry.name : entry.nameEn;
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
    }

    private static void EnsureLoaded()
    {
        if (byLevel != null) return;
        byLevel = new Dictionary<int, Entry>();
        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null) return;
        File file = null;
        try { file = JsonUtility.FromJson<File>(asset.text); }
        catch (ArgumentException) { }
        if (file?.entries == null) return;
        foreach (var entry in file.entries)
            if (entry != null && entry.level > 0) byLevel[entry.level] = entry;
        Resources.UnloadAsset(asset);
    }
}
