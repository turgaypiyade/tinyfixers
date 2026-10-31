using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Arka plan simülasyon build'i (TinyFixersSim) level verisini kendi içine gömer; her level değişikliğinde yeniden
/// build gerekiyordu (kullanıcı, 2026-10-28: "her seferinde asset değişince build alıyorum"). Bu araç aktif
/// kataloğun levellarını Library/SimLevels/L0004.json biçiminde dışa yazar; sim build oyunu kurarken bu dosya
/// varsa runtime kopyanın üzerine yazar (RuntimeSimulationSession.ApplyLevelOverride). ProLevels altındaki bir
/// asset kaydedilince otomatik çalışır; yeni build gerekmez.
/// Nesne referansları (ObstacleLibrary, müzik, görseller) JSON'dan çıkarılır: build'deki değerler korunur.
/// </summary>
public sealed class SimLevelExporter : AssetPostprocessor
{
    public const string CatalogPath = "Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset";
    private const string WatchFolder = "Assets/_Project/Settings/ProLevels/";
    public static string OutputDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "SimLevels"));

    private static readonly Regex ObjectRef = new Regex("\"[A-Za-z0-9_]+\":\\{\"instanceID\":-?\\d+\\}", RegexOptions.Compiled);

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
            if (path.StartsWith(WatchFolder) && path.EndsWith(".asset")) { ExportAll(); return; }
    }

    [MenuItem("TinyFixers/Sim/Export Levels For Sim")]
    public static void ExportAll()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null || catalog.entries == null) return;
        Directory.CreateDirectory(OutputDir);
        int written = 0;
        foreach (var entry in catalog.entries)
        {
            string file = Path.Combine(OutputDir, $"L{entry.level:0000}.json");
            if (entry.levelData == null)
            {
                if (File.Exists(file)) File.Delete(file);
                continue;
            }
            File.WriteAllText(file, StripObjectReferences(JsonUtility.ToJson(entry.levelData)));
            written++;
        }
        Debug.Log($"[SimLevelExporter] {written} level → {OutputDir}");
    }

    // {"instanceID":N} editör dışında anlamsız: alanı JSON'dan çıkar → FromJsonOverwrite build'deki değeri korur.
    private static string StripObjectReferences(string json)
    {
        json = ObjectRef.Replace(json, "");
        // Art arda silinen alanlar ",,," bırakır; tek geçiş yetmez → değişmeyene dek temizle.
        string prev;
        do
        {
            prev = json;
            json = Regex.Replace(json, ",\\s*,", ",");
            json = Regex.Replace(json, "\\{\\s*,", "{");
            json = Regex.Replace(json, ",\\s*\\}", "}");
            json = Regex.Replace(json, "\\[\\s*,", "[");
            json = Regex.Replace(json, ",\\s*\\]", "]");
        } while (json != prev);
        return json;
    }
}
