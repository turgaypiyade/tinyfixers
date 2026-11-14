using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Uygulama boyutu: _Project altındaki dokulara iOS + Android için açık ASTC ayarı yazar.
//  - Board'da küçük görünen ama keskin kalması gereken görseller (taş, special, engel, booster, ızgara) → ASTC 4x4.
//  - Geri kalan UI/arka plan/karakter görselleri → ASTC 6x6 (~%55 daha küçük, telefonda fark edilmez).
//  - Sıkıştırmasız (None ya da varsayılan formatı RGBA32 vb. sabitlenmiş) dokular da sıkıştırılır.
// Elle iPhone/Android override'ı açılmış dokulara DOKUNULMAZ (bilinçli ayar sayılır).
// Önce "Rapor" ile tahmini kazancı gör, sonra "Uygula". Geri almak: .meta değişikliklerini git ile geri al.
public static class MobileTextureCompressionTool
{
    private const string Root = "Assets/_Project";

    private static readonly string[] SharpFolders =
    {
        "Assets/_Project/Art/Icons/Gems/",
        "Assets/_Project/Art/Icons/Specials/",
        "Assets/_Project/Art/Icons/Boosters/",
        "Assets/_Project/Art/UI/Obstacles/",
        "Assets/_Project/Art/UI/GridBorders/",
    };

    private static readonly string[] Platforms = { "iPhone", "Android" };

    // Taşlar 1100–1250 px kaynak, ekranda ~110–300 px çizilir → mobilde en fazla 512 px (kullanıcı 2026-11-14: Play
    // indirme boyutu 431 MB, ucuz telefonlarda RAM sınırda; "sıkıştır, fark etmez"). Önceden bilerek sıkıştırmasızdı.
    private const string GemFolder = "Assets/_Project/Art/Icons/Gems/";
    private const int GemMaxSize = 512;

    // Tahtada görünen engel/special/joker görselleri: kaynak 1000–2000 px, ekranda ~110–450 px → mobilde en fazla 768
    // (kullanıcı 2026-11-14, Build Report: Obstacles 61 MB/265 dosya, Specials 18 MB). Önceden override edilmiş
    // (bu araçla ASTC yapılmış) dokularda da yalnız boyut sınırı uygulanır; format korunur.
    private const int BoardArtMaxSize = 768;
    private static readonly string[] BoardArtFolders =
    {
        "Assets/_Project/Art/UI/Obstacles/",
        "Assets/_Project/Art/Icons/Specials/",
        "Assets/_Project/Art/Icons/Boosters/",
        "Assets/_Project/Resources/SealSafe/",
    };

    private static int MobileMaxSize(string path, int importerMax)
    {
        if (path.StartsWith(GemFolder)) return Mathf.Min(importerMax, GemMaxSize);
        if (BoardArtFolders.Any(f => path.StartsWith(f))) return Mathf.Min(importerMax, BoardArtMaxSize);
        return importerMax;
    }

    [MenuItem("TinyFixers/Build/Mobil Doku Sıkıştırma — Rapor")]
    private static void Report() => Run(apply: false);

    [MenuItem("TinyFixers/Build/Mobil Doku Sıkıştırma — Uygula")]
    private static void Apply()
    {
        if (!EditorUtility.DisplayDialog("Mobil Doku Sıkıştırma",
                "_Project altındaki dokulara iOS/Android ASTC ayarı yazılacak ve yeniden içe aktarılacak. " +
                "Elle override edilmiş dokulara dokunulmaz. Devam?", "Uygula", "Vazgeç"))
            return;
        Run(apply: true);
    }

    private static void Run(bool apply)
    {
        var plans = new List<(TextureImporter importer, TextureImporterFormat format, long before, long after)>();
        var capPlans = new List<(TextureImporter importer, int cap, long before, long after)>();
        int skippedManual = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
            if (importer.textureType != TextureImporterType.Sprite && importer.textureType != TextureImporterType.Default)
                continue;
            if (Platforms.Any(p => importer.GetPlatformTextureSettings(p).overridden))
            {
                // Override'lı dokuda yalnız boyut sınırı (tahta görselleri); format/kalite elle ayardır, korunur.
                int cap = MobileMaxSize(path, int.MaxValue);
                int currentMax = Platforms.Max(p => importer.GetPlatformTextureSettings(p).maxTextureSize);
                if (cap < currentMax)
                {
                    importer.GetSourceTextureWidthAndHeight(out int sw, out int sh);
                    int block = path.StartsWith(GemFolder) || SharpFolders.Any(f => path.StartsWith(f)) ? 4 : 6;
                    long b = Scaled(sw, sh, currentMax, block), a = Scaled(sw, sh, cap, block);
                    if (a < b) capPlans.Add((importer, cap, b, a));
                }
                else skippedManual++;
                continue;
            }

            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            int max = MobileMaxSize(path, importer.maxTextureSize);
            float scale = Mathf.Min(1f, (float)max / Mathf.Max(1, Mathf.Max(w, h)));
            int tw = Mathf.Max(1, Mathf.RoundToInt(w * scale));
            int th = Mathf.Max(1, Mathf.RoundToInt(h * scale));

            bool sharp = SharpFolders.Any(f => path.StartsWith(f));
            var format = sharp ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            // Varsayılan formatı elle sabitlenmiş (ör. RGBA32) doku "Compression" alanına bakılmadan sıkıştırmasızdır.
            bool forcedFormat = importer.GetDefaultPlatformTextureSettings().format != TextureImporterFormat.Automatic;
            long before = forcedFormat
                ? (long)tw * th * 4
                : EstimateCurrent(importer.textureCompression, tw, th);
            long after = AstcBytes(tw, th, sharp ? 4 : 6);
            if (after >= before) continue;
            plans.Add((importer, format, before, after));
        }

        long totalBefore = plans.Sum(p => p.before) + capPlans.Sum(p => p.before);
        long totalAfter = plans.Sum(p => p.after) + capPlans.Sum(p => p.after);
        var sb = new StringBuilder();
        sb.AppendLine($"[TextureCompression] {(apply ? "UYGULANDI" : "RAPOR")}: {plans.Count} doku sıkıştırma + {capPlans.Count} doku boyut sınırı, " +
                      $"~{Mb(totalBefore)} → ~{Mb(totalAfter)} (kazanç ~{Mb(totalBefore - totalAfter)}). " +
                      $"Elle override edilmiş, atlanan: {skippedManual}.");
        foreach (var p in plans.OrderByDescending(p => p.before - p.after).Take(25))
            sb.AppendLine($"  -{Mb(p.before - p.after),8}  {p.format,-9} {p.importer.assetPath}");
        foreach (var p in capPlans.OrderByDescending(p => p.before - p.after).Take(25))
            sb.AppendLine($"  -{Mb(p.before - p.after),8}  max {p.cap,-5} {p.importer.assetPath}");
        // Elle çalıştırılan araç: rapor her zaman görünsün (teşhis bayrağına bağlı değil).
        Debug.Log(sb.ToString());
        if (!apply || plans.Count + capPlans.Count == 0)
        {
            EditorUtility.DisplayDialog("Mobil Doku Sıkıştırma — Rapor",
                $"{plans.Count} doku sıkıştırılabilir, {capPlans.Count} dokuya boyut sınırı.\n~{Mb(totalBefore)} → ~{Mb(totalAfter)} " +
                $"(kazanç ~{Mb(totalBefore - totalAfter)}).\nElle ayarlanmış, atlanan: {skippedManual}.\n\n" +
                "Ayrıntılı liste Console'da.", "Tamam");
            return;
        }

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < plans.Count; i++)
            {
                var (importer, format, _, _) = plans[i];
                if (EditorUtility.DisplayCancelableProgressBar("Mobil Doku Sıkıştırma", importer.assetPath, (float)i / plans.Count))
                    break;
                foreach (string platform in Platforms)
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    settings.overridden = true;
                    settings.format = format;
                    settings.maxTextureSize = MobileMaxSize(importer.assetPath, importer.maxTextureSize);
                    importer.SetPlatformTextureSettings(settings);
                }
                importer.SaveAndReimport();
            }
            for (int i = 0; i < capPlans.Count; i++)
            {
                var (importer, cap, _, _) = capPlans[i];
                if (EditorUtility.DisplayCancelableProgressBar("Mobil Doku Boyut Sınırı", importer.assetPath, (float)i / capPlans.Count))
                    break;
                foreach (string platform in Platforms)
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    if (!settings.overridden) continue;
                    settings.maxTextureSize = Mathf.Min(settings.maxTextureSize, cap);
                    importer.SetPlatformTextureSettings(settings);
                }
                importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }
    }

    // Mobilde mevcut kalite ayarının karşılığı (yaklaşık): None=RGBA32, High=ASTC4x4, Normal=ASTC6x6, Low=ASTC8x8.
    private static long EstimateCurrent(TextureImporterCompression compression, int w, int h) => compression switch
    {
        TextureImporterCompression.Uncompressed => (long)w * h * 4,
        TextureImporterCompression.CompressedHQ => AstcBytes(w, h, 4),
        TextureImporterCompression.CompressedLQ => AstcBytes(w, h, 8),
        _ => AstcBytes(w, h, 6),
    };

    private static long Scaled(int w, int h, int max, int block)
    {
        float s = Mathf.Min(1f, (float)max / Mathf.Max(1, Mathf.Max(w, h)));
        return AstcBytes(Mathf.Max(1, Mathf.RoundToInt(w * s)), Mathf.Max(1, Mathf.RoundToInt(h * s)), block);
    }

    private static long AstcBytes(int w, int h, int block) =>
        (long)((w + block - 1) / block) * ((h + block - 1) / block) * 16;

    private static string Mb(long bytes) => $"{bytes / (1024f * 1024f):0.0} MB";
}
