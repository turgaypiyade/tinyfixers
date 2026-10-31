using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Oyun içi sayı yazılarının (hamle, hedef, joker adedi) fontu — GERİ ALINABİLİR deneme.
/// Sahne/prefab'taki font atamaları DEĞİŞMEZ; açıkken yazı çalışma anında Pop fontuna geçer ve
/// mevcut görünümü (yüz/kontur/gölge renk ve kalınlıkları) Pop atlasına kopyalanarak korunur.
/// Geri almak: TinyFixers ▸ Fonts ▸ Game Numbers ▸ Use TinyFixers Pop (işareti kaldır) — ya da
/// Resources/GameNumberFont.asset içinde "useTinyFixersPop" kapat / dosyayı sil.
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/UI/Game Number Font")]
public sealed class GameNumberFont : ScriptableObject
{
    [Tooltip("Kapalı = sahnedeki orijinal fontlar (tam geri alma).")]
    public bool useTinyFixersPop = true;
    public TMP_FontAsset font;
    [Tooltip("Punto çarpanı: Pop'un rakamları eski fonttan iriyse < 1 (aynı kutuya sığsın).")]
    [Range(0.5f, 1.5f)] public float sizeScale = 0.9f;

    private static GameNumberFont cached;
    private static bool loaded;
    // Orijinal materyal → aynı görünümün Pop atlası kopyası (yazı başına değil, materyal başına bir kez).
    private static readonly Dictionary<Material, Material> materials = new();

    private static GameNumberFont Config
    {
        get
        {
            if (!loaded) { cached = Resources.Load<GameNumberFont>("GameNumberFont"); loaded = true; }
            return cached;
        }
    }

    public static bool Active => Config != null && Config.useTinyFixersPop && Config.font != null;

    /// <summary>Punto: açıkken sizeScale ile çarpılmış değer (puntoyu her stilde baştan kuran yazılar için).</summary>
    public static float Size(float size) => Active ? size * Config.sizeScale : size;

    /// <summary>Yazıyı Pop'a geçirir (kapalıysa hiçbir şey yapmaz). Tekrar çağrılması güvenli.
    /// scaleSize: puntoyu sahnedeki değerden bir kez ölçekle (puntoyu kendisi kuran yazılar false + Size()).</summary>
    public static void Apply(TMP_Text text, bool scaleSize = true)
    {
        if (text == null || !Active) return;
        var cfg = Config;
        if (text.font == cfg.font) return;

        var original = text.fontSharedMaterial;
        text.font = cfg.font;
        text.fontSharedMaterial = PopCopyOf(original, cfg.font);
        text.fontStyle &= ~FontStyles.Bold;   // Pop zaten kalın; sahte bold aralığı açar
        if (scaleSize)
        {
            text.fontSize *= cfg.sizeScale;
            text.fontSizeMax *= cfg.sizeScale;
            text.fontSizeMin *= cfg.sizeScale;
        }
        text.extraPadding = true;              // kalın kontur kesilmesin
    }

    private static Material PopCopyOf(Material original, TMP_FontAsset font)
    {
        if (original == null) return font.material;
        if (materials.TryGetValue(original, out var copy) && copy != null) return copy;

        // Orijinalin shader'ı + anahtarları: kontur/gölge birebir aynı yoldan çizilsin (mobil shader'da
        // kontur OUTLINE_ON'a bağlı; Pop'un varsayılan materyali farklı shader'la gelebilir).
        copy = new Material(font.material) { name = original.name + " (TinyFixers Pop)", shader = original.shader };
        copy.shaderKeywords = original.shaderKeywords;
        copy.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
        copy.SetFloat(ShaderUtilities.ID_TextureWidth, font.atlasWidth);
        copy.SetFloat(ShaderUtilities.ID_TextureHeight, font.atlasHeight);
        copy.SetFloat(ShaderUtilities.ID_GradientScale, font.atlasPadding + 1);
        var shader = original.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string prop = shader.GetPropertyName(i);
            if (IsAtlasSpecific(prop) || !copy.HasProperty(prop)) continue;
            switch (shader.GetPropertyType(i))
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color: copy.SetColor(prop, original.GetColor(prop)); break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector: copy.SetVector(prop, original.GetVector(prop)); break;
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range: copy.SetFloat(prop, original.GetFloat(prop)); break;
            }
        }
        ShaderUtilities.UpdateShaderRatios(copy);   // kontur/gölge oranları Pop atlasına göre
        materials[original] = copy;
        return copy;
    }

    private static bool IsAtlasSpecific(string prop) =>
        prop == "_MainTex" || prop == "_TextureWidth" || prop == "_TextureHeight" || prop == "_GradientScale"
        || prop == "_ScaleRatioA" || prop == "_ScaleRatioB" || prop == "_ScaleRatioC"
        || prop == "_WeightNormal" || prop == "_WeightBold";
}
