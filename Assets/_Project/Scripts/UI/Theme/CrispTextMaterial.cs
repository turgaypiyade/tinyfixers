using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Net (buğusuz) TMP materyali. Bazı font asset'lerinin varsayılan materyalinde yumuşak kontur +
/// yumuşak alt gölge var (Inter ExtraBold: OutlineSoftness 0.27, UnderlaySoftness 0.49) → açık
/// zeminde yazı "buğulu" görünür. Ortak materyali değiştirmek tüm oyunu etkileyeceği için font başına
/// bir kez temiz kopya üretilir ve paylaşılır (satır başına materyal kopyası YOK).
/// </summary>
public static class CrispTextMaterial
{
    private static readonly Dictionary<TMP_FontAsset, Material> cache = new();

    public static Material Get(TMP_FontAsset font)
    {
        if (font == null || font.material == null) return null;
        if (cache.TryGetValue(font, out var mat) && mat != null) return mat;

        mat = new Material(font.material) { name = font.material.name + " (Crisp)" };
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.08f);   // hafif dolgun, keskin kenar
        mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        mat.DisableKeyword(ShaderUtilities.Keyword_Glow);
        cache[font] = mat;
        return mat;
    }

    /// <summary>Yazıyı net materyale geçirir; sahte kalın (faux bold) kapatılır — kalın font asset'inde
    /// harf aralığını açıp kenarı yumuşatır.</summary>
    public static void Apply(TMP_Text text)
    {
        if (text == null) return;
        var mat = Get(text.font);
        if (mat != null) text.fontSharedMaterial = mat;
        text.fontStyle &= ~FontStyles.Bold;
    }
}
