using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Yazının oturduğu zemin (başlık bandı, buton rengi…). Zemine göre outline/gölge rengi türetilir.</summary>
public enum TextSurface { Maroon, Green, Blue, Red, Purple, Orange }

/// <summary>
/// Oyun yazı kuralı (kullanıcı kararı 2026-10-27): yazı yüzü BEYAZ; outline ve alt gölge (underlay),
/// yazının oturduğu zeminin daha koyu tonu. Sarı/altın yüz geçişi kullanılmaz.
/// Materyal preset'leri (Fonts/Materials/TinyFixersPop_On{Surface}.mat) bu kuraldan üretilir —
/// TinyFixers ▸ Fonts ▸ TinyFixers Pop ▸ Build Font Asset + Presets.
/// </summary>
public static class TextSurfaceStyle
{
    public const string MaterialPrefix = "TinyFixersPop_On";
    public static readonly Color Face = Color.white;

    // Zemin oranları: PreLevel bordo başlığında beğenilen kontur/gölge (bordo × 0.40 / × 0.24) referans.
    private const float OutlineFactor = 0.40f;
    private const float UnderlayFactor = 0.24f;

    // Kalınlıklar: PreLevel başlığında onaylanan görünüm.
    public const float OutlineWidth = 0.20f;
    public const float UnderlayOffsetY = -0.55f;
    public const float UnderlayDilate = 0.18f;

    /// <summary>Zeminin temel rengi (sprite'ların ortalama tonu).</summary>
    public static Color Base(TextSurface surface) => surface switch
    {
        TextSurface.Maroon => new Color(0.48f, 0.06f, 0.16f),
        TextSurface.Green  => new Color(0.27f, 0.66f, 0.07f),
        TextSurface.Blue   => new Color(0.05f, 0.45f, 0.80f),
        TextSurface.Red    => new Color(0.80f, 0.14f, 0.14f),
        TextSurface.Purple => new Color(0.46f, 0.20f, 0.72f),
        TextSurface.Orange => new Color(0.95f, 0.50f, 0.10f),
        _ => Color.gray,
    };

    public static Color Outline(Color surface) => Darken(surface, OutlineFactor);
    public static Color Underlay(Color surface) => Darken(surface, UnderlayFactor);

    public static string MaterialName(TextSurface surface) => MaterialPrefix + surface;

    // Ton korunur, yalnız parlaklık düşer (zeminin "koyusu").
    private static Color Darken(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, 1f);

    // ── Çalışma anı: koddan kurulan yazılar (buton etiketleri, event başlıkları) ─────────────────
    // Zemin rengi başına TEK paylaşılan materyal (yazı başına kopya yok).
    private static readonly Dictionary<Color32, Material> runtimeMaterials = new();

    public static void Apply(TMP_Text text, TextSurface surface) => Apply(text, Base(surface));

    /// <summary>Yazıyı kurala geçirir: TinyFixers Pop, beyaz yüz, kontur/gölge = verilen zeminin koyusu.
    /// Font değişirse punto büyük harf yüksekliği oranında korunur. Pop yüklü değilse dokunmaz.</summary>
    public static void Apply(TMP_Text text, Color surface)
    {
        if (text == null) return;
        var font = CommonPopupSkin.Shared.titleFont;
        if (font == null || font.material == null) return;
        if (text.font != font)
        {
            float ratio = CapHeight(text.font) > 0f && CapHeight(font) > 0f ? CapHeight(text.font) / CapHeight(font) : 1f;
            text.font = font;
            text.fontSize *= ratio;
            text.fontSizeMax *= ratio;
            text.fontSizeMin *= ratio;
        }
        text.fontSharedMaterial = MaterialFor(font, surface);
        text.fontStyle &= ~FontStyles.Bold;   // Pop zaten kalın
        text.enableVertexGradient = false;
        text.color = Face;
        text.extraPadding = true;
        if (text.TryGetComponent(out TmpOutline legacy)) Object.Destroy(legacy);   // eski koddan kontur ezmesin
    }

    /// Verilen zemin için paylaşılan TinyFixers Pop materyali (geçici stil değişimi yapan çağıranlar için).
    internal static Material RuntimeMaterial(TMP_FontAsset font, Color surface) => MaterialFor(font, surface);

    private static Material MaterialFor(TMP_FontAsset font, Color surface)
    {
        Color32 key = surface;
        if (runtimeMaterials.TryGetValue(key, out var mat) && mat != null) return mat;
        mat = new Material(font.material) { name = "TinyFixersPop_OnRuntime_" + ColorUtility.ToHtmlStringRGB(surface) };
        mat.SetColor(ShaderUtilities.ID_FaceColor, Face);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Outline(surface));
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, Underlay(surface));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, UnderlayOffsetY);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, UnderlayDilate);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        ShaderUtilities.UpdateShaderRatios(mat);
        runtimeMaterials[key] = mat;
        return mat;
    }

    private static float CapHeight(TMP_FontAsset font)
        => font != null && font.faceInfo.pointSize > 0 ? font.faceInfo.capLine / font.faceInfo.pointSize : 0f;
}
