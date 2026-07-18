using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Level KATALOĞUNU (sıra ataması) Excel-uyumlu CSV'ye aktarır ve geri okur.
/// Level İÇERİĞİNE dokunmaz — yalnızca "1., 2., 3. levele hangi asset konmuş"
/// bilgisi düzenlenir: chapter, level, levelKey, levelData (asset adı).
///
/// Export: TinyFixers > Levels > Export Catalog CSV — seçili LevelCatalog
/// (seçili değilse LevelCatalogPro). "info" kolonu salt-okunur özettir (tanımak için).
/// Import: TinyFixers > Levels > Import Catalog CSV — katalog girişleri CSV'deki
/// satır SIRASIYLA yeniden kurulur: satır sil = giriş kalkar, satır ekle/yer değiştir
/// = katalog öyle olur. levelData kolonundaki asset adı projede aranıp bağlanır;
/// bulunamayan ad varsa import TÜMÜYLE iptal edilir (katalog yarım kalmaz).
/// </summary>
public static class LevelCatalogCsvTool
{
    private const char Sep = ';';
    private const string DefaultCatalogPath = "Assets/_Project/Settings/LevelCatalogPro.asset";

    // ── Export ───────────────────────────────────────────────────────────────

    [MenuItem("TinyFixers/Levels/Export Catalog CSV")]
    public static void Export()
    {
        var catalog = ResolveCatalog();
        if (catalog == null) return;

        string path = EditorUtility.SaveFilePanel(
            "Katalog CSV kaydet", Directory.GetParent(Application.dataPath).FullName,
            catalog.name + ".csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        var sb = new StringBuilder();
        sb.AppendLine("sep=;");
        sb.AppendLine(string.Join(Sep.ToString(), "chapter", "level", "levelKey", "levelData", "info"));

        int rows = 0;
        foreach (var e in catalog.entries)
        {
            if (e == null) continue;
            string assetName = e.levelData != null ? e.levelData.name : "";
            sb.AppendLine(string.Join(Sep.ToString(),
                e.chapter.ToString(), e.level.ToString(), e.levelKey ?? "", assetName, Info(e.levelData)));
            rows++;
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        Debug.Log($"[LevelCatalogCsv] Export: {rows} giriş → {path}");
        EditorUtility.RevealInFinder(path);
    }

    // Salt-okunur tanıma özeti: tür, hamle, hedefler. Import'ta YOK SAYILIR.
    private static string Info(LevelData d)
    {
        if (d == null) return "";
        var goals = new List<string>();
        if (d.goals != null)
            foreach (var g in d.goals)
            {
                if (g == null) continue;
                string t = g.targetType switch
                {
                    LevelGoalTargetType.Tile        => g.tileType.ToString(),
                    LevelGoalTargetType.Obstacle    => g.obstacleId.ToString(),
                    LevelGoalTargetType.Collectible => g.collectibleId.ToString(),
                    _ => "?"
                };
                goals.Add($"{t} x{g.amount}");
            }
        return $"{d.levelKind} | {d.moves} hamle | {string.Join(", ", goals)}";
    }

    // ── Import ───────────────────────────────────────────────────────────────

    [MenuItem("TinyFixers/Levels/Import Catalog CSV")]
    public static void Import() => Import(autoRenumber: false);

    /// Araya level sokma / sıra değiştirme için: chapter-level-levelKey kolonları
    /// YOK SAYILIR, girişler satır sırasına göre baştan numaralanır
    /// (chapter=1, level=1..N, levelKey=LevelCL_001..). Excel'de sadece satır ekle/taşı yeter.
    [MenuItem("TinyFixers/Levels/Import Catalog CSV (Yeniden Numaralandır)")]
    public static void ImportRenumbered() => Import(autoRenumber: true);

    private static void Import(bool autoRenumber)
    {
        var catalog = ResolveCatalog();
        if (catalog == null) return;

        string path = EditorUtility.OpenFilePanel(
            "Katalog CSV seç", Directory.GetParent(Application.dataPath).FullName, "csv");
        if (string.IsNullOrEmpty(path)) return;

        var lines = File.ReadAllLines(path);
        int start = 0;
        if (start < lines.Length && lines[start].TrimStart().StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
            start++;
        if (start >= lines.Length) { Fail("CSV boş."); return; }

        var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var head = lines[start].Split(Sep);
        for (int i = 0; i < head.Length; i++) col[head[i].Trim()] = i;
        var requiredCols = autoRenumber ? new[] { "levelData" } : new[] { "chapter", "level", "levelKey", "levelData" };
        foreach (var required in requiredCols)
            if (!col.ContainsKey(required)) { Fail($"CSV başlığında '{required}' kolonu yok."); return; }
        start++;

        // Asset adı → LevelData (tüm proje). Aynı ada sahip birden çok asset varsa uyar.
        var byName = new Dictionary<string, LevelData>(StringComparer.OrdinalIgnoreCase);
        var duplicate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var guid in AssetDatabase.FindAssets("t:LevelData"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var d = AssetDatabase.LoadAssetAtPath<LevelData>(p);
            if (d == null) continue;
            if (byName.ContainsKey(d.name)) duplicate.Add(d.name);
            else byName[d.name] = d;
        }

        var newEntries = new List<LevelCatalog.LevelEntry>();
        var errors = new List<string>();

        for (int li = start; li < lines.Length; li++)
        {
            var line = lines[li];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = line.Split(Sep);

            string Get(string key) =>
                col.TryGetValue(key, out int idx) && idx < cells.Length ? cells[idx].Trim() : "";

            string assetName = Get("levelData");
            if (string.IsNullOrEmpty(assetName))
            {
                errors.Add($"satır {li + 1}: levelData boş.");
                continue;
            }
            if (duplicate.Contains(assetName))
            {
                errors.Add($"satır {li + 1}: '{assetName}' adında birden çok LevelData var — ad benzersiz olmalı.");
                continue;
            }
            if (!byName.TryGetValue(assetName, out var data))
            {
                errors.Add($"satır {li + 1}: '{assetName}' adında LevelData bulunamadı.");
                continue;
            }

            int chapter, level;
            string levelKey;

            if (autoRenumber)
            {
                // Satır sırası = yeni sıra: numaralar ve key'ler baştan üretilir.
                chapter = 1;
                level = newEntries.Count + 1;
                levelKey = $"LevelCL_{level:000}";
            }
            else
            {
                if (!int.TryParse(Get("chapter"), out chapter) || chapter < 1)
                {
                    errors.Add($"satır {li + 1}: chapter geçersiz.");
                    continue;
                }
                if (!int.TryParse(Get("level"), out level) || level < 1)
                {
                    errors.Add($"satır {li + 1}: level geçersiz.");
                    continue;
                }
                levelKey = Get("levelKey");
            }

            newEntries.Add(new LevelCatalog.LevelEntry
            {
                chapter = chapter,
                level = level,
                levelKey = levelKey,
                levelData = data
            });
        }

        if (errors.Count > 0)
        {
            foreach (var err in errors) Debug.LogError("[LevelCatalogCsv] " + err);
            Fail($"Import İPTAL — {errors.Count} hata var (Console'a bak). Katalog değiştirilmedi.");
            return;
        }

        Undo.RecordObject(catalog, "Import Level Catalog CSV");
        catalog.entries.Clear();
        catalog.entries.AddRange(newEntries);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Katalog Import",
            $"{catalog.name}: {newEntries.Count} giriş CSV'deki sırayla yazıldı.", "Tamam");
        Debug.Log($"[LevelCatalogCsv] Import: {newEntries.Count} giriş → {catalog.name}");
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static LevelCatalog ResolveCatalog()
    {
        var selected = Selection.activeObject as LevelCatalog;
        if (selected != null) return selected;

        var fallback = AssetDatabase.LoadAssetAtPath<LevelCatalog>(DefaultCatalogPath);
        if (fallback == null)
            Fail($"LevelCatalog seçili değil ve {DefaultCatalogPath} bulunamadı.\n" +
                 "Project panelinde bir LevelCatalog asset'i seçip tekrar dene.");
        return fallback;
    }

    private static void Fail(string msg) =>
        EditorUtility.DisplayDialog("Level Catalog CSV", msg, "Tamam");
}
