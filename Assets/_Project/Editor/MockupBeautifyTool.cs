using UnityEditor;
using UnityEngine;

/// <summary>
/// Alt-menü ekranlarını "düz renkli mockup" görünümünden çıkaran tek-tık araç.
/// Menü: TinyFixers > Mockup > 0) Beautify Theme (Sprites)
///
/// Yaptıkları:
///  1. Beyaz, yuvarlak köşeli 9-slice sprite üretir (RoundedRect / RoundedRectSoft) —
///     tint ile her renge girer; kart, buton, bant, progress dolgusu hepsinde kullanılır.
///  2. GreenButton/BlueButton'a 9-slice border yazar (glossy CTA butonları için).
///  3. UITheme.asset'in boş sprite slotlarını projedeki GERÇEK art ile doldurur:
///     coin=GoldMoney, star=Star, heart=TopHUD/heart, kart/buton/bant=RoundedRect.
///
/// Bunu çalıştırdıktan sonra ekran mockup menülerini (Shop/Leaderboard/Team...) yeniden
/// çalıştır: kurulumlar theme'den beslendiği için hepsi yuvarlak köşeli + ikonlu kurulur.
/// </summary>
public static class MockupBeautifyTool
{
    private const string GeneratedDir   = "Assets/_Project/Art/UI/Generated";
    private const string RoundedPath    = GeneratedDir + "/RoundedRect.png";
    private const string RoundedSoftPath = GeneratedDir + "/RoundedRectSoft.png";

    public const string CoinPath   = "Assets/_Project/Art/UI/GoldMoney.png";
    public const string StarPath   = "Assets/_Project/Art/UI/Star.png";
    public const string HeartPath  = "Assets/_Project/Art/UI/TopHUD/heart.png";
    public const string GreenBtnPath = "Assets/_Project/Art/UI/SettingsImg/GreenButton.png";
    public const string BlueBtnPath  = "Assets/_Project/Art/UI/SettingsImg/BlueButton.png";

    [MenuItem("TinyFixers/Mockup/0) Beautify Theme (Sprites)")]
    public static void Run()
    {
        MockupUI.EnsureFolder(GeneratedDir);

        // 1) Yuvarlak köşeli beyaz sprite'lar (tint için beyaz şart).
        GenerateRoundedRect(RoundedPath, size: 128, radius: 36, softEdge: 2f);
        GenerateRoundedRect(RoundedSoftPath, size: 128, radius: 60, softEdge: 2f); // hap/pill görünüm

        ImportAsSlicedSprite(RoundedPath, border: 40);
        ImportAsSlicedSprite(RoundedSoftPath, border: 62);

        // 2) Hazır glossy butonlara border (köşeler ~40px eğimli görünüyor).
        ImportAsSlicedSprite(GreenBtnPath, border: 48);
        ImportAsSlicedSprite(BlueBtnPath, border: 48);

        // 3) Theme'i doldur.
        var theme = MockupUI.EnsureTheme();
        var rounded     = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
        var roundedSoft = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSoftPath);

        theme.panelBackground         = rounded;
        theme.cardBackground          = rounded;
        theme.buttonBackground        = roundedSoft;
        theme.sectionHeaderBackground = rounded;
        theme.progressFill            = roundedSoft;
        theme.coinIcon  = AssetDatabase.LoadAssetAtPath<Sprite>(CoinPath);
        theme.starIcon  = AssetDatabase.LoadAssetAtPath<Sprite>(StarPath);
        theme.heartIcon = AssetDatabase.LoadAssetAtPath<Sprite>(HeartPath);

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Beautify Theme",
            "Sprite'lar üretildi ve UITheme dolduruldu.\n\nŞimdi ekran mockup'larını yeniden çalıştır:\n" +
            "TinyFixers > Mockup > Setup Shop / Leaderboard / Team", "Tamam");
    }

    // ── Rounded-rect PNG üretimi ────────────────────────────────────────

    private static void GenerateRoundedRect(string path, int size, float radius, float softEdge)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        float boxHalf = half - 1f; // 1px güvenlik payı

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance to rounded box (merkezli).
                float dx = Mathf.Abs(x + 0.5f - half) - (boxHalf - radius);
                float dy = Mathf.Abs(y + 0.5f - half) - (boxHalf - radius);
                float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
                float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                float dist = outside + inside - radius;

                float a = Mathf.Clamp01(0.5f - dist / Mathf.Max(0.0001f, softEdge));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void ImportAsSlicedSprite(string path, int border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[Beautify] Importer bulunamadı: {path}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
    }
}
