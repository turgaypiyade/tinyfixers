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
        int skippedManual = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
            if (importer.textureType != TextureImporterType.Sprite && importer.textureType != TextureImporterType.Default)
                continue;
            if (Platforms.Any(p => importer.GetPlatformTextureSettings(p).overridden))
            {
                skippedManual++;
                continue;
            }

            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            int max = importer.maxTextureSize;
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

        long totalBefore = plans.Sum(p => p.before);
        long totalAfter = plans.Sum(p => p.after);
        var sb = new StringBuilder();
        sb.AppendLine($"[TextureCompression] {(apply ? "UYGULANDI" : "RAPOR")}: {plans.Count} doku, " +
                      $"~{Mb(totalBefore)} → ~{Mb(totalAfter)} (kazanç ~{Mb(totalBefore - totalAfter)}). " +
                      $"Elle override edilmiş, atlanan: {skippedManual}.");
        foreach (var p in plans.OrderByDescending(p => p.before - p.after).Take(25))
            sb.AppendLine($"  -{Mb(p.before - p.after),8}  {p.format,-9} {p.importer.assetPath}");
        Debug.Log(sb.ToString());

        if (!apply || plans.Count == 0) return;

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
                    settings.maxTextureSize = importer.maxTextureSize;
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

    private static long AstcBytes(int w, int h, int block) =>
        (long)((w + block - 1) / block) * ((h + block - 1) / block) * 16;

    private static string Mb(long bytes) => $"{bytes / (1024f * 1024f):0.0} MB";
}
