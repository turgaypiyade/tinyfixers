using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// "Net yazı" stili (referans: Royal Match popup yazıları): İNCE kontur (zeminin koyu tonu) + aynı renkte,
/// keskin, OPAK, sağa-aşağı kayık gölge. Bulanıklığın kökü yumuşak + yarı saydam gölgeydi.
///
/// Menü: TinyFixers ▸ Fonts ▸
///   1) Joker Denemesi — YALNIZ joker sayılarına "OnRed" stili (01_Game joker çubuğu + level öncesi slotlar).
///   2) Tüm Yazılara Sert Gölge — ortak preset'lerin gölgesini keskinleştirir (joker beğenilince).
///   3) Oyun Anı Başlık Stili — DynaPuff Bold SDF (yeni) + DynaPuff_RoyalTitle; joker başlığı, övgü, boss dalga.
///   4) Başlıklar + Devam Butonları — BakbakOne + bordo başlık / yeşil buton preset'leri (PreLevel + fail/success).
///
/// DİKKAT: Font asset padding'ine (→ materyal _GradientScale) DOKUNMA. TMP'de kontur/gölge kalınlığı
/// padding'e oranlıdır; padding'i 9→18 yapmak o fontu kullanan TÜM yazıların konturunu ~2 katına çıkardı
/// (2026-09-28, geri alındı).
///
/// Materyal değerleri API ile set edilir ve ShaderUtilities.UpdateShaderRatios çağrılır — YAML'dan elle
/// değiştirmek gölgenin mesh payını (ScaleRatio) güncellemediği için gölge kırpılırdı.
/// </summary>
public static class CrispTextSetup
{
    private const string MaterialsDir = "Assets/_Project/Fonts/Materials";

    // Ortak preset'ler → sert gölge (kontur rengi/kalınlığı korunur).
    private static readonly string[] PresetPaths =
    {
        MaterialsDir + "/BakBakOne_Cartoon.mat",
        MaterialsDir + "/BakBakOne_PreLevelTitle.mat",
        MaterialsDir + "/Inter_Cartoon.mat",
        MaterialsDir + "/Inter_28pt_Bold_LossAmount.mat",
        MaterialsDir + "/Inter_ExtraBold_GoldOutlineGlow.mat",
    };

    // Gölge: sağa biraz, aşağı daha çok (referans). TMP birimi; padding 9 font'larda ~1 birim ≈ 9 örnek piksel.
    public static readonly Vector2 ShadowOffset = new Vector2(0.25f, -0.55f);
    private const float ShadowDarken = 0.75f;   // gölge = kontur renginin bu oranı (biraz daha koyu ton)

    // Joker sayısı: kırmızı daire üstünde beyaz, İNCE koyu bordo kontur.
    private static readonly Color OnRedOutline = new Color(0.36f, 0.03f, 0.07f, 1f);
    private const float OnRedOutlineWidth = 0.12f;

    private const string GameScenePath = "Assets/_Project/Scenes/01_Game.unity";
    private const string PreLevelPrefabPath = "Assets/_Project/Prefabs/UI/PreLevelSpecialPopup.prefab";

    // Zemin stilleri: yazının konturu/gölgesi DURDUĞU ZEMİNİN koyu tonu (referans kuralı).
    private static readonly Color CreamFace = new Color(1f, 0.97f, 0.86f, 1f);
    private static readonly Color MaroonOutline = new Color(0.30f, 0.04f, 0.08f, 1f);   // bordo şerit
    private const float TitleOnMaroonWidth = 0.16f;
    private static readonly Color ButtonFace = new Color(1f, 1f, 0.94f, 1f);
    private static readonly Color GreenOutline = new Color(0.07f, 0.30f, 0.06f, 1f);    // yeşil buton
    private const float OnGreenButtonWidth = 0.14f;
    private static readonly Color BlueOutline = new Color(0.05f, 0.16f, 0.42f, 1f);     // mavi buton

    // Başlık + devam butonu = GÖSTERİM fontu (BakbakOne); Inter yalnız metin/etiket/sayı için (düz, teknik,
    // büyük boyutta karaktersiz). Zeminler: başlıklar bordo şeritte, devam butonları yeşil (GreenButonEfso).
    // Eskiden bu yazılar font'un VARSAYILAN materyalindeydi (siyah kalın kontur + yumuşak gölge, Inter'de
    // YUKARI kayık). Varsayılan materyale dokunulmaz (onu kullanan diğer yazılar etkilenmesin) — ayrı preset.
    private const string DisplayFontPath = "Assets/_Project/Fonts/BakBakOne/BakbakOne-Regular SDF.asset";

    [MenuItem("TinyFixers/Fonts/4) Başlıklar + Devam Butonları (BakbakOne, level öncesi + fail/success)")]
    public static void RunTitlesAndButtons()
    {
        var display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
        if (display == null) { Debug.LogWarning($"[CrispText] Font yok: {DisplayFontPath}"); return; }

        // Level öncesi popup (prefab; ana menü + oyun içi "Tekrar Dene" aynı prefab).
        int count = 0;
        var root = PrefabUtility.LoadPrefabContents(PreLevelPrefabPath);
        if (root != null)
        {
            foreach (var popup in root.GetComponentsInChildren<PreLevelSpecialPopupController>(true))
            {
                count += AssignContextStyle(popup, "titleText", "TitleOnMaroon", CreamFace, MaroonOutline, TitleOnMaroonWidth, display);
                count += AssignContextStyle(popup, "continueText", "OnGreenButton", ButtonFace, GreenOutline, OnGreenButtonWidth, display);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PreLevelPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Fail / success popup'ları (01_Game).
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            var previous = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            foreach (var levelEnd in Object.FindObjectsByType<LevelEndSimplePopupController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                count += AssignContextStyle(levelEnd, "failTitleText", "TitleOnMaroon", CreamFace, MaroonOutline, TitleOnMaroonWidth, display);
                count += AssignContextStyle(levelEnd, "successTitleText", "TitleOnMaroon", CreamFace, MaroonOutline, TitleOnMaroonWidth, display);
                count += AssignContextStyle(levelEnd, "failContinueText", "OnGreenButton", ButtonFace, GreenOutline, OnGreenButtonWidth, display);
                count += AssignContextStyle(levelEnd, "successContinueText", "OnGreenButton", ButtonFace, GreenOutline, OnGreenButtonWidth, display);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previous) && previous != GameScenePath)
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }

        AssignCommonPopupSkin(display);

        AssetDatabase.SaveAssets();
        Debug.Log($"[CrispText] Başlık + devam butonu: {count} yazı BakbakOne + zemin stili (başlık bordo, buton yeşil); " +
                  "ortak popup skin'i bağlandı (başlık/yeşil/mavi buton).");
    }

    /// fontOverride verilirse önce yazının fontu değişir; preset o font'un materyalinden üretilir
    /// (materyal atlası font'la birebir olmalı, yoksa harfler bozulur).
    private static int AssignContextStyle(Object owner, string field, string suffix, Color face, Color outline, float width,
        TMP_FontAsset fontOverride = null)
    {
        var so = new SerializedObject(owner);
        var text = so.FindProperty(field)?.objectReferenceValue as TMP_Text;
        if (text == null) return 0;
        if (fontOverride != null && text.font != fontOverride)
        {
            // Aynı punto değerinde fontların büyük harf boyu farklı (Inter cap 66 / BakbakOne 58 @90) →
            // görünen yazı küçülmesin diye punto'yu büyük harf yüksekliği oranında telafi et.
            float ratio = CapHeightRatio(text.font, fontOverride);
            text.font = fontOverride;
            text.fontSize *= ratio;
            text.fontSizeMax *= ratio;
            text.fontSizeMin *= ratio;
        }
        if (text.font == null) return 0;

        text.fontSharedMaterial = GetOrCreateContextPreset(text.font, suffix, face, outline, width);
        text.fontStyle &= ~FontStyles.Bold;   // font zaten kalın: faux-bold harf aralığını açar
        EditorUtility.SetDirty(text);
        return 1;
    }

    private static float CapHeightRatio(TMP_FontAsset from, TMP_FontAsset to)
    {
        if (from == null || to == null) return 1f;
        float a = from.faceInfo.capLine / Mathf.Max(1f, from.faceInfo.pointSize);
        float b = to.faceInfo.capLine / Mathf.Max(1f, to.faceInfo.pointSize);
        return (a > 0f && b > 0f) ? a / b : 1f;
    }

    /// Kodla kurulan ortak popup'lar (RuntimeChoicePopup, kayıt, müzik, takım, market…) CommonPopupSkin'den
    /// geçer: font zaten BakbakOne; zemin stillerini skin'e bağla (başlık bordo, yeşil/mavi buton).
    private static void AssignCommonPopupSkin(TMP_FontAsset display)
    {
        var skin = AssetDatabase.LoadAssetAtPath<CommonPopupSkin>("Assets/_Project/Resources/CommonPopupSkin.asset");
        if (skin == null || skin.font == null) return;
        var so = new SerializedObject(skin);
        so.FindProperty("titleMaterial").objectReferenceValue =
            GetOrCreateContextPreset(skin.font, "TitleOnMaroon", CreamFace, MaroonOutline, TitleOnMaroonWidth);
        so.FindProperty("greenButtonMaterial").objectReferenceValue =
            GetOrCreateContextPreset(skin.font, "OnGreenButton", ButtonFace, GreenOutline, OnGreenButtonWidth);
        so.FindProperty("blueButtonMaterial").objectReferenceValue =
            GetOrCreateContextPreset(skin.font, "OnBlueButton", ButtonFace, BlueOutline, OnGreenButtonWidth);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skin);
    }

    /// Font'un materyalinden (atlas + _GradientScale birebir) "{font}_{suffix}.mat" preset'i: yüz rengi +
    /// ince kontur + aynı tonun koyusunda keskin sağa-aşağı gölge. Varsa yerinde günceller.
    private static Material GetOrCreateContextPreset(TMP_FontAsset font, string suffix, Color face, Color outline, float width)
    {
        string path = $"{MaterialsDir}/{font.name.Replace(" SDF", "")}_{suffix}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(font.material);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = font.material.shader;
            mat.CopyPropertiesFromMaterial(font.material);
        }

        mat.SetColor(ShaderUtilities.ID_FaceColor, face);
        mat.EnableKeyword("OUTLINE_ON");
        mat.SetColor(ShaderUtilities.ID_OutlineColor, outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        ApplyHardShadow(mat);
        return mat;
    }

    [MenuItem("TinyFixers/Fonts/1) Joker Denemesi (sadece joker sayıları)")]
    public static void RunJokerTrial()
    {
        if (!EditorUtility.DisplayDialog("Joker Denemesi",
                "YALNIZ joker sayılarına yeni stil (beyaz + ince bordo kontur + sağa-aşağı keskin gölge) " +
                "atanacak. Diğer yazılara dokunulmaz. 01_Game sahnesi açılıp kaydedilecek. Devam?",
                "Devam", "İptal"))
            return;

        int assigned = AssignJokerCountStyle();
        AssetDatabase.SaveAssets();
        Debug.Log($"[CrispText] Joker denemesi: {assigned} joker sayısına OnRed atandı.");
    }

    [MenuItem("TinyFixers/Fonts/2) Tüm Yazılara Sert Gölge (joker beğenilince)")]
    public static void RunHardShadowAll()
    {
        if (!EditorUtility.DisplayDialog("Tüm Yazılara Sert Gölge",
                "Ortak yazı stilleri (BakBakOne_Cartoon, PreLevelTitle, Inter_Cartoon, LossAmount, GoldOutlineGlow) " +
                "keskin + opak + sağa-aşağı gölgeye çevrilecek. Bu stilleri kullanan TÜM yazılar değişir. Devam?",
                "Devam", "İptal"))
            return;

        foreach (var path in PresetPaths)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) ApplyHardShadow(mat);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[CrispText] Ortak yazı stilleri sert gölgede.");
    }

    // ── 3) Joker başlığı ("Wonder Çekici!") — referans: Royal Match "Kraliyet Çekici!" ──────────
    // Yuvarlak kalın font (DynaPuff Bold, Türkçe tam) + krem→sarı yüz (kodda vertex gradient) + lacivert iç
    // kontur + altta kalınlaşan altın dış kontur (underlay, aşağı kayık). Yeni ayrı font asset'i: başka
    // hiçbir yazı bu font'u kullanmaz → mevcut yazılar etkilenmez.
    private const string DynaPuffTtfPath = "Assets/TextMesh Pro/Fonts/DynaPuff/DynaPuff-Bold.ttf";
    private const string DynaPuffDir = "Assets/_Project/Fonts/DynaPuff";
    private const string DynaPuffAssetPath = DynaPuffDir + "/DynaPuff-Bold SDF.asset";
    private const string RoyalTitleMatPath = MaterialsDir + "/DynaPuff_RoyalTitle.mat";

    // Padding 12 @90pt: altın dış kontur (kalın underlay) için pay; yalnız bu yeni font'u etkiler.
    private const int TitleSamplingSize = 90;
    private const int TitlePadding = 12;

    private static readonly Color TitleOutline = new Color(0.11f, 0.13f, 0.38f, 1f);   // lacivert
    private const float TitleOutlineWidth = 0.2f;
    private static readonly Color TitleGoldRing = new Color(1f, 0.74f, 0.12f, 1f);     // altın
    private const float TitleGoldDilate = 0.6f;
    private static readonly Vector2 TitleGoldOffset = new Vector2(0f, -0.35f);        // altta kalın

    // Kullanıcı kuralı: DynaPuff YALNIZ kısa süreli "oyun anı" yazılarında — joker odak başlığı, övgü
    // (Harika!/Süper!) popup'ı, boss dalga afişi. Menü/buton/açıklama yazılarında KULLANMA.
    [MenuItem("TinyFixers/Fonts/3) Oyun Anı Başlık Stili (joker başlığı + övgü + boss dalga)")]
    public static void RunMomentTitleStyle()
    {
        var font = GetOrCreateDynaPuffBold();
        if (font == null) return;
        var mat = GetOrCreateRoyalTitleMaterial(font);

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        int joker = AssignFontFields<JokerFocusOverlayController>("titleFont", "titleMaterial", font, mat);
        int boss = AssignFontFields<BossDuelController>("bannerFont", "bannerMaterial", font, mat);
        // Övgü popup'ı kendi 3 katmanlı materyalini font'un materyalinden kurar → yalnız font.
        int praise = AssignFontFields<MoveClearPraisePopupController>("preferredFont", null, font, null);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(previous) && previous != GameScenePath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        Debug.Log($"[CrispText] Oyun anı başlık stili: joker={joker}, boss dalga={boss}, övgü={praise}.");
    }

    private static int AssignFontFields<T>(string fontField, string materialField, TMP_FontAsset font, Material mat)
        where T : Object
    {
        int count = 0;
        foreach (var target in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(target);
            var fontProp = so.FindProperty(fontField);
            if (fontProp == null) continue;
            fontProp.objectReferenceValue = font;
            if (materialField != null)
            {
                var matProp = so.FindProperty(materialField);
                if (matProp != null) matProp.objectReferenceValue = mat;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }
        return count;
    }

    private static TMP_FontAsset GetOrCreateDynaPuffBold()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DynaPuffAssetPath);
        if (existing != null) return existing;

        var source = AssetDatabase.LoadAssetAtPath<Font>(DynaPuffTtfPath);
        if (source == null)
        {
            Debug.LogWarning($"[CrispText] Font dosyası yok: {DynaPuffTtfPath}");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(DynaPuffDir))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(DynaPuffDir).Replace('\\', '/'), Path.GetFileName(DynaPuffDir));

        var font = TMP_FontAsset.CreateFontAsset(source, TitleSamplingSize, TitlePadding, GlyphRenderMode.SDFAA,
            1024, 1024, AtlasPopulationMode.Dynamic, true);
        font.name = "DynaPuff-Bold SDF";
        AssetDatabase.CreateAsset(font, DynaPuffAssetPath);

        // Atlas + materyal font asset'inin alt-varlıkları (TMP Font Asset Creator ile aynı düzen).
        font.atlasTexture.name = font.name + " Atlas";
        AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
        font.material.name = font.name + " Material";
        AssetDatabase.AddObjectToAsset(font.material, font);

        font.TryAddCharacters(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            "ÇçĞğİıÖöŞşÜü .,:;!?%+-x/()'\"&", out _);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }

    private static Material GetOrCreateRoyalTitleMaterial(TMP_FontAsset font)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RoyalTitleMatPath);
        if (mat == null)
        {
            mat = new Material(font.material);
            AssetDatabase.CreateAsset(mat, RoyalTitleMatPath);
        }
        else
        {
            mat.shader = font.material.shader;
            mat.CopyPropertiesFromMaterial(font.material);
        }

        mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);   // yüz rengi text'in vertex gradient'inden
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
        mat.EnableKeyword("OUTLINE_ON");
        mat.SetColor(ShaderUtilities.ID_OutlineColor, TitleOutline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, TitleOutlineWidth);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);

        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, TitleGoldRing);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, TitleGoldDilate);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, TitleGoldOffset.x);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, TitleGoldOffset.y);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);

        ShaderUtilities.UpdateShaderRatios(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ── Sert gölge ───────────────────────────────────────────────────────────

    /// Kontur yumuşaklığı 0; gölge opak, keskin, sağa-aşağı, konturla aynı kalınlıkta, kontur renginin koyu tonu.
    public static void ApplyHardShadow(Material mat)
    {
        float outlineWidth = mat.GetFloat(ShaderUtilities.ID_OutlineWidth);
        Color outline = mat.GetColor(ShaderUtilities.ID_OutlineColor);

        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);

        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, Darker(outline));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, ShadowOffset.x);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, ShadowOffset.y);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, outlineWidth);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);

        ShaderUtilities.UpdateShaderRatios(mat);
        EditorUtility.SetDirty(mat);
    }

    private static Color Darker(Color c) => new Color(c.r * ShadowDarken, c.g * ShadowDarken, c.b * ShadowDarken, 1f);

    // ── Joker sayıları ───────────────────────────────────────────────────────

    private static int AssignJokerCountStyle()
    {
        int count = 0;

        // Level öncesi popup (prefab).
        var root = PrefabUtility.LoadPrefabContents(PreLevelPrefabPath);
        if (root != null)
        {
            foreach (var slot in root.GetComponentsInChildren<PreLevelSpecialSlotView>(true))
                count += AssignOnRed(slot, "countText");
            PrefabUtility.SaveAsPrefabAsset(root, PreLevelPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Oyun içi joker slotları (01_Game sahnesi).
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return count;
        var previous = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        foreach (var slot in Object.FindObjectsByType<BoosterSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            count += AssignOnRed(slot, "countText");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(previous) && previous != GameScenePath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

        return count;
    }

    private static int AssignOnRed(Object owner, string field)
        => AssignContextStyle(owner, field, "OnRed", Color.white, OnRedOutline, OnRedOutlineWidth);
}
