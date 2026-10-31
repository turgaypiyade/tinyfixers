using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// TinyFixers Pop'un kalıcı TMP font asset'ini ve materyal preset'lerini üretir/günceller.
/// Menü: TinyFixers ▸ Fonts ▸ TinyFixers Pop ▸ Build Font Asset + Presets
///
/// - Kaynak: Assets/_Project/Fonts/TinyFixersPop/TinyFixersPop-Regular.ttf (Docs/Typography/TinyFixersPop/build_font.py üretir).
/// - Statik atlas: TTF'teki TÜM karakterler (Türkçe dahil) önceden pişer; çalışma anında FontEngine işi yok.
/// - Padding geniş (SDF gradient alanı): kalın outline, underlay gölgesi, softness ve glow kesilmeden çizilir.
/// - Font eksik karakterde BakBak One'a düşer (fallback).
/// - Preset'ler: yazı kuralı (TextSurfaceStyle) — beyaz yüz, outline/gölge = zeminin koyu tonu — her zemin için
///   TinyFixersPop_On{Surface}.mat; ad "TinyFixersPop_" ile başladığı için TMP Material Preset listesinde görünür.
/// - Popup'lara uygular: ortak popup kaplaması (CommonPopupSkin: başlık + yeşil/mavi buton, geçişler beyaz) ve
///   PreLevel popup başlığı + devam butonu.
/// Yeniden çalıştırmak güvenli: mevcut asset/materyaller yerinde güncellenir (GUID ve sahne referansları korunur).
/// </summary>
public static class TinyFixersPopFontBuilder
{
    private const string FontDir = "Assets/_Project/Fonts/TinyFixersPop/";
    private const string SourcePath = FontDir + "TinyFixersPop-Regular.ttf";
    private const string AssetPath = FontDir + "TinyFixersPop SDF.asset";
    private const string MaterialsDir = "Assets/_Project/Fonts/Materials/";
    private const string FallbackPath = "Assets/_Project/Fonts/BakBakOne/BakbakOne-Regular SDF.asset";

    // 90 pt örnekleme + 13 px padding (~%14): başlıklardaki kalın outline + alt gölge sığar.
    private const int SamplingPointSize = 90;
    private const int AtlasPadding = 13;
    private const int AtlasSize = 2048;

    private const string SkinPath = "Assets/_Project/Resources/CommonPopupSkin.asset";
    private const string PreLevelPrefabPath = "Assets/_Project/Prefabs/UI/PreLevelSpecialPopup.prefab";
    // PreLevel deneme aracı açık kalırsa Play'de kalıcı ayarın üstüne kendi materyalini yazar.
    private const string TrialEnabledKey = "TinyFixers.PreLevelGroboldTrial.ArcEvaluationEnabled";
    private const string GameNumberFontPath = "Assets/_Project/Resources/GameNumberFont.asset";
    private const string GameNumbersMenu = "TinyFixers/Fonts/Game Numbers/Use TinyFixers Pop (01_Game: moves, goals, jokers)";

    [MenuItem("TinyFixers/Fonts/TinyFixers Pop/Build Font Asset + Presets")]
    public static void Build()
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        if (source == null)
        {
            Debug.LogError("[TinyFixersPop] TTF yok: " + SourcePath + " — önce build_font.py çalıştır.");
            return;
        }

        var characters = CollectCharacters(source);
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        bool created = fontAsset == null;
        if (created)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            fontAsset.name = "TinyFixersPop SDF";
            AssetDatabase.CreateAsset(fontAsset, AssetPath);
            fontAsset.atlasTexture.name = "TinyFixersPop SDF Atlas";
            fontAsset.material.name = "TinyFixersPop SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }
        else
        {
            // Yerinde yenile: TTF değişmiş olabilir (yeni sürüm). GUID aynı kalır.
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.ClearFontAssetData(true);
        }

        // Tam SDF shader (CreateFontAsset mobil shader verir; orada kontur/glow anahtara bağlı, bevel yok):
        // outline, underlay, softness, glow, bevel hepsi her materyalde çalışsın.
        var fullShader = Shader.Find("TextMeshPro/Distance Field");
        if (fullShader != null) fontAsset.material.shader = fullShader;
        fontAsset.material.EnableKeyword(ShaderUtilities.Keyword_Outline);

        fontAsset.TryAddCharacters(characters, out string missing, includeFontFeatures: true);
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;   // çalışma anında atlas büyümez
        fontAsset.isMultiAtlasTexturesEnabled = true;
        AttachExtraAtlases(fontAsset);
        AddFallback(fontAsset);
        EditorUtility.SetDirty(fontAsset);

        var presets = new Dictionary<TextSurface, Material>();
        foreach (TextSurface surface in System.Enum.GetValues(typeof(TextSurface)))
            presets[surface] = BuildSurfacePreset(fontAsset, surface);
        int presetCount = presets.Count;

        AssetDatabase.SaveAssets();
        ApplyToCommonPopups(fontAsset, presets);
        ApplyToPreLevel(fontAsset, presets);
        EnsureGameNumberFont(fontAsset);
        SessionState.SetBool(TrialEnabledKey, false);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int atlasCount = fontAsset.atlasTextures?.Length ?? 0;
        string missingNote = string.IsNullOrEmpty(missing) ? "yok" : missing;
        Debug.Log($"[TinyFixersPop] {(created ? "Oluşturuldu" : "Güncellendi")}: {AssetPath} — " +
                  $"{fontAsset.characterTable.Count}/{characters.Length} karakter, {atlasCount} atlas, " +
                  $"{presetCount} preset. Eksik: {missingNote}");
        Selection.activeObject = fontAsset;
    }

    // TTF'in eşlediği tüm karakterler (cmap): font neyi destekliyorsa atlasta da o olsun.
    private static string CollectCharacters(Font source)
    {
        var sb = new System.Text.StringBuilder();
        FontEngine.InitializeFontEngine();
        if (FontEngine.LoadFontFace(source, SamplingPointSize) != FontEngineError.Success)
            return "";
        for (uint code = 0x20; code <= 0xFFFF; code++)
        {
            if (code >= 0xD800 && code <= 0xDFFF) continue;
            if (FontEngine.TryGetGlyphIndex(code, out uint index) && index != 0)
                sb.Append((char)code);
        }
        return sb.ToString();
    }

    // Çoklu atlas açıldıysa ek dokular da asset'in içinde saklanmalı (yoksa sahne yeniden açılınca kaybolur).
    private static void AttachExtraAtlases(TMP_FontAsset fontAsset)
    {
        var textures = fontAsset.atlasTextures;
        if (textures == null) return;
        for (int i = 1; i < textures.Length; i++)
        {
            var tex = textures[i];
            if (tex == null || AssetDatabase.IsSubAsset(tex)) continue;
            tex.name = $"TinyFixersPop SDF Atlas {i}";
            AssetDatabase.AddObjectToAsset(tex, fontAsset);
        }
    }

    private static void AddFallback(TMP_FontAsset fontAsset)
    {
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
        if (fallback == null) return;
        fontAsset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
        if (!fontAsset.fallbackFontAssetTable.Contains(fallback))
            fontAsset.fallbackFontAssetTable.Add(fallback);
    }

    // Kural: beyaz yüz; outline / alt gölge = zeminin koyu tonu (TextSurfaceStyle). Pop'un kendi atlası + shader'ı.
    private static Material BuildSurfacePreset(TMP_FontAsset fontAsset, TextSurface surface)
    {
        string path = MaterialsDir + TextSurfaceStyle.MaterialName(surface) + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = material == null;
        if (isNew) material = new Material(fontAsset.material);
        else material.CopyPropertiesFromMaterial(fontAsset.material);
        material.shader = fontAsset.material.shader;

        Color zemin = TextSurfaceStyle.Base(surface);
        material.SetColor(ShaderUtilities.ID_FaceColor, TextSurfaceStyle.Face);
        material.SetColor(ShaderUtilities.ID_OutlineColor, TextSurfaceStyle.Outline(zemin));
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, TextSurfaceStyle.OutlineWidth);
        material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, TextSurfaceStyle.Underlay(zemin));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, TextSurfaceStyle.UnderlayOffsetY);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, TextSurfaceStyle.UnderlayDilate);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
        ShaderUtilities.UpdateShaderRatios(material);   // outline/underlay oranları bu atlasa göre

        if (isNew) AssetDatabase.CreateAsset(material, path);
        else EditorUtility.SetDirty(material);
        return material;
    }

    // Ortak popup kaplaması (RuntimeChoicePopup, takım, bildirim izni, kayıt… hepsi CommonPopupView'dan):
    // başlık bordo bant, butonlar yeşil/mavi. Sarıya kayan yüz geçişleri beyaz.
    private static void ApplyToCommonPopups(TMP_FontAsset font, Dictionary<TextSurface, Material> presets)
    {
        var skin = AssetDatabase.LoadAssetAtPath<CommonPopupSkin>(SkinPath);
        if (skin == null) { Debug.LogWarning("[TinyFixersPop] CommonPopupSkin yok: " + SkinPath); return; }
        skin.titleFont = font;
        skin.buttonFont = font;
        skin.titleMaterial = presets[TextSurface.Maroon];
        skin.greenButtonMaterial = presets[TextSurface.Green];
        skin.blueButtonMaterial = presets[TextSurface.Blue];
        skin.titleGradientTop = skin.titleGradientBottom = TextSurfaceStyle.Face;
        skin.buttonGradientTop = skin.buttonGradientBottom = TextSurfaceStyle.Face;
        EditorUtility.SetDirty(skin);
    }

    // PreLevel popup: bordo bant başlığı + yeşil devam butonu (prefab; sahnedeki örnek fontu override etmiyor).
    private static void ApplyToPreLevel(TMP_FontAsset font, Dictionary<TextSurface, Material> presets)
    {
        var root = PrefabUtility.LoadPrefabContents(PreLevelPrefabPath);
        try
        {
            var popup = root.GetComponentInChildren<PreLevelSpecialPopupController>(true);
            if (popup == null) { Debug.LogWarning("[TinyFixersPop] PreLevel controller bulunamadı."); return; }
            var so = new SerializedObject(popup);
            Style(so.FindProperty("titleText").objectReferenceValue as TMP_Text, font, presets[TextSurface.Maroon]);
            Style(so.FindProperty("continueText").objectReferenceValue as TMP_Text, font, presets[TextSurface.Green]);
            PrefabUtility.SaveAsPrefabAsset(root, PreLevelPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Style(TMP_Text text, TMP_FontAsset font, Material material)
    {
        if (text == null) return;
        text.font = font;
        text.fontSharedMaterial = material;
        text.fontStyle &= ~FontStyles.Bold;          // font zaten kalın; sahte bold aralığı açar
        text.enableVertexGradient = false;           // kural: düz beyaz yüz
        text.color = TextSurfaceStyle.Face;
        text.extraPadding = true;                    // kalın outline + gölge kesilmesin
        EditorUtility.SetDirty(text);
    }

    // Oyun içi sayılar (hamle/hedef/joker) — geri alınabilir: sahneye dokunmaz, çalışma anında geçer.
    // İlk kurulumda açık; sonraki çalıştırmalarda kullanıcının aç/kapa seçimi korunur.
    private static void EnsureGameNumberFont(TMP_FontAsset font)
    {
        var cfg = AssetDatabase.LoadAssetAtPath<GameNumberFont>(GameNumberFontPath);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<GameNumberFont>();
            cfg.useTinyFixersPop = true;
            AssetDatabase.CreateAsset(cfg, GameNumberFontPath);
        }
        cfg.font = font;
        EditorUtility.SetDirty(cfg);
    }

    [MenuItem(GameNumbersMenu)]
    private static void ToggleGameNumbers()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<GameNumberFont>(GameNumberFontPath);
        if (cfg == null)
        {
            Debug.LogWarning("[TinyFixersPop] Önce 'Build Font Asset + Presets' çalıştır (GameNumberFont.asset yok).");
            return;
        }
        cfg.useTinyFixersPop = !cfg.useTinyFixersPop;
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log("[TinyFixersPop] Oyun içi sayılar: " + (cfg.useTinyFixersPop ? "TinyFixers Pop" : "orijinal fontlar")
                  + " (Play'i yeniden başlat).");
    }

    [MenuItem(GameNumbersMenu, true)]
    private static bool ToggleGameNumbersValidate()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<GameNumberFont>(GameNumberFontPath);
        Menu.SetChecked(GameNumbersMenu, cfg != null && cfg.useTinyFixersPop);
        return cfg != null && !EditorApplication.isPlaying;
    }

    [MenuItem("TinyFixers/Fonts/TinyFixers Pop/Build Font Asset + Presets", true)]
    private static bool BuildValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
