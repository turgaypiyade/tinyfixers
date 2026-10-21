using System.Collections.Generic;
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
///   4) Başlıklar + Butonlar — Baloo 2 başlık / Nunito buton + bordo/yeşil/mavi zemin preset'leri (PreLevel,
///      fail/success, CommonPopupSkin).
///   5) Nunito Denemesi — Mağaza prefab'larında Inter→Nunito, BakbakOne→Baloo 2 (punto harf boyuna göre telafi,
///      ortak preset'lerin yeni font kopyaları). Beğenilirse aynı çekirdek tüm oyuna uygulanır. (PARK: beğenilmedi)
///   6) Font Tutarlılığı — fontsuz (LiberationSans) / Anta yazılar → oyun fontu, Inter 24→28 Bold, uyumsuz
///      materyal, ana menüde faux-bold kaldırma denemesi.
///   7) Stil Onarımı — popup başlığı tasarım değerlerine, eski yumuşak gölgeler nete, Inter ExtraBold varsayılan
///      materyali (en yaygın) nete. Inspector'dan kayarsa yeniden çalıştırmak yeterli.
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
    private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string PreLevelPrefabPath = "Assets/_Project/Prefabs/UI/PreLevelSpecialPopup.prefab";

    // Zemin stilleri: yazının konturu/gölgesi DURDUĞU ZEMİNİN koyu tonu (referans kuralı).
    private static readonly Color MaroonOutline = new Color(0.30f, 0.04f, 0.08f, 1f);   // bordo şerit
    private const float TitleOnMaroonWidth = 0.16f;
    private static readonly Color ButtonFace = Color.white;
    private static readonly Color GreenOutline = new Color(0.07f, 0.30f, 0.06f, 1f);    // yeşil buton
    private const float OnGreenButtonWidth = 0.14f;
    private static readonly Color BlueOutline = new Color(0.05f, 0.16f, 0.42f, 1f);     // mavi buton

    // Başlık = Baloo 2 ExtraBold, buton = Nunito Black (kullanıcı seçimi 2026-09-29; BakbakOne/Inter "aynı,
    // karaktersiz" bulundu). Inter yalnız metin/etiket/sayı. Zeminler: başlıklar bordo şeritte, devam butonları
    // yeşil (GreenButonEfso). Font'ların varsayılan materyaline dokunulmaz — zemin stili ayrı preset.
    // Kaynaklar: Google Fonts OFL (lisans Fonts/<ad>/OFL.txt), değişken font'tan sabit kalınlık üretildi.
    private const string TitleTtfPath = "Assets/_Project/Fonts/Baloo2/Baloo2-ExtraBold.ttf";
    private const string TitleAssetPath = "Assets/_Project/Fonts/Baloo2/Baloo2-ExtraBold SDF.asset";
    private const string ButtonTtfPath = "Assets/_Project/Fonts/Nunito/Nunito-Black.ttf";
    private const string ButtonAssetPath = "Assets/_Project/Fonts/Nunito/Nunito-Black SDF.asset";
    private const int UiFontPadding = 9;   // mevcut font'larla aynı (kontur/gölge değerleri buna göre ayarlı)

    [MenuItem("TinyFixers/Fonts/4) Başlıklar + Butonlar (Baloo 2 başlık, Nunito buton)")]
    public static void RunTitlesAndButtons()
    {
        var titleFont = GetOrCreateFontAsset(TitleTtfPath, TitleAssetPath, UiFontPadding);
        var buttonFont = GetOrCreateFontAsset(ButtonTtfPath, ButtonAssetPath, UiFontPadding);
        if (titleFont == null || buttonFont == null) return;

        // Level öncesi popup (prefab; ana menü + oyun içi "Tekrar Dene" aynı prefab).
        int count = 0;
        var root = PrefabUtility.LoadPrefabContents(PreLevelPrefabPath);
        if (root != null)
        {
            foreach (var popup in root.GetComponentsInChildren<PreLevelSpecialPopupController>(true))
            {
                count += AssignPopupTitle(popup, "titleText", titleFont);
                count += AssignContextStyle(popup, "continueText", "OnGreenButton", ButtonFace, GreenOutline, OnGreenButtonWidth, buttonFont);
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
                count += AssignPopupTitle(levelEnd, "failTitleText", titleFont);
                count += AssignPopupTitle(levelEnd, "successTitleText", titleFont);
                count += AssignContextStyle(levelEnd, "failContinueText", "OnGreenButton", ButtonFace, GreenOutline, OnGreenButtonWidth, buttonFont);
                // Fiyatlı buton ("Devam Et 🪙 900") uzun → daha küçük üst sınır (kullanıcı).
                var failCont = new SerializedObject(levelEnd).FindProperty("failContinueText")?.objectReferenceValue as TMP_Text;
                if (failCont != null)
                {
                    failCont.fontSizeMax = PriceButtonLabelSize;
                    failCont.fontSize = PriceButtonLabelSize;
                    EditorUtility.SetDirty(failCont);
                }
                count += AssignContextStyle(levelEnd, "successContinueText", "OnGreenButton", ButtonFace, GreenOutline, OnGreenButtonWidth, buttonFont);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Event katılım popup'ları (MainMenu): aynı bordo zemin (EvenSelectorPopup) + yeşil buton.
            var menu = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            foreach (var safari in Object.FindObjectsByType<SafariJoinPopupController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                count += AssignPopupTitle(safari, "titleText", titleFont);
                var button = new SerializedObject(safari).FindProperty("continueButton")?.objectReferenceValue as Component;
                if (button != null)
                    count += StyleContextText(button.GetComponentInChildren<TMP_Text>(true), "OnGreenButton",
                        ButtonFace, GreenOutline, OnGreenButtonWidth, buttonFont);
            }
            foreach (var bridge in Object.FindObjectsByType<BridgeRepairJoinPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                count += AssignPopupTitle(bridge, "titleText", titleFont);
                count += AssignContextStyle(bridge, "continueLabel", "OnGreenButton", ButtonFace, GreenOutline, OnGreenButtonWidth, buttonFont);
            }
            // Görevler paneli: başlık ("GÖREVLER") + yeşil "Devam" — sahnede panelRoot altında (alan değil).
            foreach (var missions in Object.FindObjectsByType<RegionUnlockListPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var panelRoot = new SerializedObject(missions).FindProperty("panelRoot")?.objectReferenceValue as GameObject;
                if (panelRoot == null) continue;
                foreach (var t in panelRoot.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.name == "TitleText")
                        count += StylePopupTitle(t, titleFont);
                    else if (t.GetComponentInParent<UnityEngine.UI.Button>(true) is { name: "ContinueButton" })
                        count += StyleContextText(t, "OnGreenButton", ButtonFace, GreenOutline, OnGreenButtonWidth, buttonFont);
                }
            }
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);

            if (!string.IsNullOrEmpty(previous) && previous != EditorSceneManager.GetActiveScene().path)
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }

        AssignCommonPopupSkin(titleFont, buttonFont);

        AssetDatabase.SaveAssets();
        if (GameDiagnostics.EditorTools)
            Debug.Log($"[CrispText] Başlık + buton: {count} yazı (Baloo 2 başlık / Nunito buton) + zemin stili; " + "ortak popup skin'i bağlandı (başlık/yeşil/mavi buton).");
    }

    /// fontOverride verilirse önce yazının fontu değişir; preset o font'un materyalinden üretilir
    /// (materyal atlası font'la birebir olmalı, yoksa harfler bozulur).
    private static int AssignContextStyle(Object owner, string field, string suffix, Color face, Color outline, float width,
        TMP_FontAsset fontOverride = null)
    {
        var text = new SerializedObject(owner).FindProperty(field)?.objectReferenceValue as TMP_Text;
        return StyleContextText(text, suffix, face, outline, width, fontOverride);
    }

    private static int StyleContextText(TMP_Text text, string suffix, Color face, Color outline, float width,
        TMP_FontAsset fontOverride = null)
    {
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
            // Gerçek harf şekline göre dikey ortala (Baloo 2'nin dev alt boşluğu Middle'da yazıyı yukarı iter).
            text.verticalAlignment = VerticalAlignmentOptions.Geometry;
        }
        if (text.font == null) return 0;

        text.fontSharedMaterial = GetOrCreateContextPreset(text.font, suffix, face, outline, width);
        text.fontStyle &= ~FontStyles.Bold;   // font zaten kalın: faux-bold harf aralığını açar
        EditorUtility.SetDirty(text);

        // Yeşil ana butonlar: şeker kabartmalı yazı (beyaz → krem-sarı yüz) + parlama şeridi + nefes alma
        // (ButtonShine; görsel çalışma anında kurulur).
        if (suffix == "OnGreenButton")
        {
            // Tüm popup'ların alt butonu AYNI boyut (kullanıcı): hedef ButtonLabelSize, sığmazsa otomatik küçülür
            // (uzun "Tekrar Dene" / "Devam Et 900" gibi).
            text.enableAutoSizing = true;
            text.fontSizeMax = ButtonLabelSize;
            text.fontSize = ButtonLabelSize;
            text.fontSizeMin = ButtonLabelMinSize;
            // Buton yazısı TEK satır: kaydırma açıkken autosize küçültmek yerine satırı bölüyordu
            // ("Devam Et" / "🪙 900" iki satır). Kapalıyken sığmayan yazı tek satırda küçülür.
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Yazı kutusu çoğu butonda görselin TAMAMI → kenarlara dayanıyordu. Yatayda iç boşluk bırak.
            float side = Mathf.Max(0f, text.rectTransform.rect.width) * ButtonLabelSideMargin;
            text.margin = new Vector4(side, 0f, side, 0f);
            ApplyCandyBevel(text.fontSharedMaterial);
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(ButtonGradientTop, ButtonGradientTop,
                ButtonGradientBottom, ButtonGradientBottom);

            var button = text.GetComponentInParent<UnityEngine.UI.Button>(true);
            if (button != null && !button.TryGetComponent(out ButtonShine _))
            {
                button.gameObject.AddComponent<ButtonShine>();
                EditorUtility.SetDirty(button.gameObject);
            }
        }
        return 1;
    }

    // ── Popup başlığı (joker başlığı stili) ──────────────────────────────────
    // Krem → açık altın yüz (vertex gradient; materyal yüzü beyaz) + ince bordo kontur + arkada ALTIN halka
    // (underlay, aşağı kayık → altta kalın). TMP'de tek underlay var: koyu gölge yerine altın halka.
    private static readonly Color PopupTitleGradientTop = Color.white;
    private static readonly Color PopupTitleGradientBottom = new Color(1f, 0.97f, 0.78f, 1f);    // çok açık altın (parlak)
    private static readonly Color PopupTitleGold = new Color(1f, 0.74f, 0.12f, 1f);
    private const float PopupTitleGoldDilate = 0.5f;                                     // padding 9 font'lar için
    private static readonly Vector2 PopupTitleGoldOffset = new Vector2(0f, -0.3f);
    // Kontur: kabartmasız, bir tık kalın ve koyu → beyaz yüz ile altın halka arasında NET koyu çizgi
    // (2026-10-20: 0.16 bordo + kabartma, ışığı konturun üstüne de düşürüp çizgiyi bulanık gösteriyordu).
    private static readonly Color PopupTitleOutline = new Color(0.20f, 0.02f, 0.05f, 1f);   // koyu bordo
    private const float PopupTitleOutlineWidth = 0.22f;
    // Kenar yumuşatma bandını daraltır (shader: scale *= _Sharpness + 1): beyaz yüz → koyu kontur geçişindeki
    // gri piksel bandı incelir. Başlıklar büyük punto → tırtıklanma riski yok.
    private const float PopupTitleSharpness = 0.5f;
    private static readonly int SharpnessId = Shader.PropertyToID("_Sharpness");

    private static int AssignPopupTitle(Object owner, string field, TMP_FontAsset font)
        => StylePopupTitle(new SerializedObject(owner).FindProperty(field)?.objectReferenceValue as TMP_Text, font);

    private static int StylePopupTitle(TMP_Text text, TMP_FontAsset font)
    {
        if (StyleContextText(text, "TitleOnMaroon", Color.white, MaroonOutline, TitleOnMaroonWidth, font) == 0)
            return 0;
        text.fontSharedMaterial = GetOrCreatePopupTitlePreset(text.font);
        text.color = Color.white;
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(PopupTitleGradientTop, PopupTitleGradientTop,
            PopupTitleGradientBottom, PopupTitleGradientBottom);
        EditorUtility.SetDirty(text);
        return 1;
    }

    private static Material GetOrCreatePopupTitlePreset(TMP_FontAsset font)
    {
        // Font'un kendi (mobil SDF) shader'ından kopyalanır → kabartma yok; ışık konturu bulanıklaştırmaz.
        var mat = GetOrCreateContextPreset(font, "TitleOnMaroon", Color.white, PopupTitleOutline, PopupTitleOutlineWidth);
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, PopupTitleGold);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, PopupTitleGoldDilate);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, PopupTitleGoldOffset.x);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, PopupTitleGoldOffset.y);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        mat.DisableKeyword("BEVEL_ON");   // parlaklık yüzdeki beyaz → açık altın geçişten; kabartma konturu bozuyordu
        if (mat.HasProperty(SharpnessId)) mat.SetFloat(SharpnessId, PopupTitleSharpness);
        ShaderUtilities.UpdateShaderRatios(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ── Buton yazısı: "şeker" kabartma ───────────────────────────────────────
    // Mobil SDF shader kabartma yapamaz → tam "TextMeshPro/Distance Field" (aynı ada sahip özellikler korunur).
    // Hafif yuvarlak kabartma + sol-üstten ışık + beyaz parlama; yüz beyaz → krem-sarı (vertex gradient).
    private static readonly Color ButtonGradientTop = Color.white;
    public const float ButtonLabelSize = 74f;      // Nunito Black punto (alt/ana butonlar, hepsi aynı)
    private const float ButtonLabelMinSize = 36f;
    private const float ButtonLabelSideMargin = 0.08f;   // buton genişliğinin sağ/sol iç boşluğu
    private const float PriceButtonLabelSize = 60f;      // "Devam Et 🪙 900" gibi fiyatlı uzun buton
    private static readonly Color ButtonGradientBottom = new Color(1f, 0.99f, 0.9f, 1f);        // çok açık krem (parlak)

    private static void ApplyCandyBevel(Material mat)
    {
        if (mat == null) return;
        var full = Shader.Find("TextMeshPro/Distance Field");
        if (full == null) { if (GameDiagnostics.EditorTools)
            Debug.LogWarning("[CrispText] TMP 'Distance Field' shader bulunamadı; kabartma atlandı."); return; }
        if (mat.shader != full) mat.shader = full;

        mat.EnableKeyword("BEVEL_ON");
        mat.SetFloat("_Bevel", 0.3f);
        mat.SetFloat("_BevelOffset", 0f);
        mat.SetFloat("_BevelWidth", 0f);
        mat.SetFloat("_BevelClamp", 0f);
        mat.SetFloat("_BevelRoundness", 0.6f);
        mat.SetFloat("_LightAngle", 2.3f);            // sol-üstten ışık
        mat.SetColor("_SpecularColor", Color.white);
        mat.SetFloat("_SpecularPower", 4f);
        mat.SetFloat("_Reflectivity", 20f);
        mat.SetFloat("_Diffuse", 0f);                 // ışık almayan yüz KARARMAZ (yazı parlak kalsın)
        mat.SetFloat("_Ambient", 1f);
        ShaderUtilities.UpdateShaderRatios(mat);
        EditorUtility.SetDirty(mat);
    }

    private static float CapHeightRatio(TMP_FontAsset from, TMP_FontAsset to)
    {
        if (from == null || to == null) return 1f;
        float a = from.faceInfo.capLine / Mathf.Max(1f, from.faceInfo.pointSize);
        float b = to.faceInfo.capLine / Mathf.Max(1f, to.faceInfo.pointSize);
        return (a > 0f && b > 0f) ? a / b : 1f;
    }

    /// Kodla kurulan ortak popup'lar (RuntimeChoicePopup, kayıt, müzik, takım, market…) CommonPopupSkin'den
    /// geçer: başlık/buton fontu + zemin stillerini skin'e bağla (başlık bordo, yeşil/mavi buton). Gövde
    /// metni skin.font'ta kalır.
    private static void AssignCommonPopupSkin(TMP_FontAsset titleFont, TMP_FontAsset buttonFont)
    {
        var skin = AssetDatabase.LoadAssetAtPath<CommonPopupSkin>("Assets/_Project/Resources/CommonPopupSkin.asset");
        if (skin == null) return;
        var so = new SerializedObject(skin);
        so.FindProperty("titleFont").objectReferenceValue = titleFont;
        so.FindProperty("buttonFont").objectReferenceValue = buttonFont;
        so.FindProperty("titleMaterial").objectReferenceValue = GetOrCreatePopupTitlePreset(titleFont);
        so.FindProperty("greenButtonMaterial").objectReferenceValue =
            GetOrCreateContextPreset(buttonFont, "OnGreenButton", ButtonFace, GreenOutline, OnGreenButtonWidth);
        so.FindProperty("blueButtonMaterial").objectReferenceValue =
            GetOrCreateContextPreset(buttonFont, "OnBlueButton", ButtonFace, BlueOutline, OnGreenButtonWidth);
        so.FindProperty("buttonFontSize").floatValue = ButtonLabelSize;
        so.FindProperty("titleGradientTop").colorValue = PopupTitleGradientTop;
        so.FindProperty("titleGradientBottom").colorValue = PopupTitleGradientBottom;
        so.FindProperty("buttonGradientTop").colorValue = ButtonGradientTop;
        so.FindProperty("buttonGradientBottom").colorValue = ButtonGradientBottom;
        ApplyCandyBevel((Material)so.FindProperty("greenButtonMaterial").objectReferenceValue);
        ApplyCandyBevel((Material)so.FindProperty("blueButtonMaterial").objectReferenceValue);
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
        if (GameDiagnostics.EditorTools)
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
        if (GameDiagnostics.EditorTools)
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
        if (GameDiagnostics.EditorTools)
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
        => GetOrCreateFontAsset(DynaPuffTtfPath, DynaPuffAssetPath, TitlePadding);

    // ── 7) Stil onarımı (kontur/gölge denetimi, kullanıcı onayı 2026-10-20) ─────────────────────────────
    //   a) Popup başlığı (Baloo2 TitleOnMaroon): Inspector'da kaymıştı (kontur ~siyah 0.24, koyu zeytin altın) →
    //      tasarım değerleri (bordo kontur + parlak altın halka + kabartma) GetOrCreatePopupTitlePreset ile.
    //   b) Eski yumuşak/yarı saydam gölgeli preset'ler → net stil (ApplyHardShadow; kontur rengi/kalınlığı aynı).
    //   c) Inter ExtraBold VARSAYILAN materyali (~265 yazı): kontur yumuşaklığı + yüz şişirme sıfır, kontur bir
    //      tık ince, gölge yukarı-yumuşak yerine aşağı-sağa keskin. Ödül parıltısı (GoldOutlineGlow), net
    //      preset'ler ve konturu olmayan varsayılanlar (Inter Bold, Baloo 2 — açık kart üstü koyu yazı) dokunulmaz.
    private const float InterDefaultOutlineWidth = 0.18f;   // eski 0.25 + yumuşak kenar; net halde bir tık ince

    private static readonly string[] SoftShadowPresetPaths =
    {
        MaterialsDir + "/BakBakOne_Cartoon.mat",
        MaterialsDir + "/Inter_Cartoon.mat",
        MaterialsDir + "/Inter_28pt_Bold_LossAmount.mat",
    };

    [MenuItem("TinyFixers/Fonts/7) Stil Onarımı (popup başlığı, yumuşak gölgeler, Inter varsayılanı)")]
    public static void RunStyleRepair()
    {
        int count = 0;

        var titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleAssetPath);
        if (titleFont != null) { GetOrCreatePopupTitlePreset(titleFont); count++; }

        foreach (string path in SoftShadowPresetPaths)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            ApplyHardShadow(mat);
            count++;
        }
        var bakbak = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/BakBakOne/BakbakOne-Regular SDF.asset");
        if (bakbak != null && bakbak.material != null) { ApplyHardShadow(bakbak.material); count++; }

        var interExtraBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterExtraBoldAssetPath);
        if (interExtraBold != null && interExtraBold.material != null)
        {
            var mat = interExtraBold.material;
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, InterDefaultOutlineWidth);
            ApplyHardShadow(mat);   // kontur yumuşaklığı 0, yüz şişirme 0, gölge = kontur tonu, aşağı-sağ, keskin
            EditorUtility.SetDirty(interExtraBold);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CrispText] Stil onarımı: {count} materyal (popup başlığı tasarım değerleri, yumuşak gölgeler → net, " +
                  "Inter ExtraBold varsayılanı net).");
    }

    // ── 6) Font tutarlılığı (uyumsuz font analizi, kullanıcı onayı 2026-10-20) ──────────────────────────
    //   a) Fontu atanmamış (TMP varsayılanı LiberationSans) ve robot dönemi Anta yazıları → rolüne göre oyun
    //      fontu: başlık = Baloo 2, buton = Nunito Black, diğer = Inter ExtraBold (fontun varsayılan stili).
    //   b) Inter 24pt Bold → Inter 28pt Bold (görünüm aynı, bir atlas eksik; preset'in yeni font kopyası).
    //   c) Font ile atlası uyuşmayan materyal → fontun kendi materyali (TMP çalışırken zaten bunu yapıyordu).
    //   d) DENEME, yalnız MainMenu: kalın fonta ayrıca verilmiş Bold stili (faux-bold: şişirme + açık aralık) kalkar.
    // Popup başlık/buton stilleri ve DynaPuff oyun anları dokunulmaz. İç içe prefab yazısı kendi asset'inde düzelir.

    private const string LiberationSansAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string AntaAssetName = "Anta-Regular SDF";
    private const string InterExtraBoldAssetPath = "Assets/_Project/Fonts/Inter/Static/Inter_28pt-ExtraBold SDF.asset";
    private const string Inter24BoldAssetPath = "Assets/_Project/Fonts/Inter/Static/Inter_24pt-Bold SDF.asset";
    private const string Inter28BoldAssetPath = "Assets/_Project/Fonts/Inter/Static/Inter_28pt-Bold SDF.asset";

    [MenuItem("TinyFixers/Fonts/6) Font Tutarlılığı (fontsuz/Anta yazılar, Inter 24→28, ana menü faux-bold denemesi)")]
    public static void RunFontConsistency()
    {
        var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationSansAssetPath)
                         ?? TMP_Settings.defaultFontAsset;
        TMP_FontAsset anta = null;
        foreach (string guid in AssetDatabase.FindAssets(AntaAssetName + " t:TMP_FontAsset"))
            anta = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid)) ?? anta;
        var interExtraBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterExtraBoldAssetPath);
        var inter24Bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Inter24BoldAssetPath);
        var inter28Bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Inter28BoldAssetPath);
        var titleFont = GetOrCreateFontAsset(TitleTtfPath, TitleAssetPath, UiFontPadding);
        var buttonFont = GetOrCreateFontAsset(ButtonTtfPath, ButtonAssetPath, UiFontPadding);
        if (interExtraBold == null || inter28Bold == null || titleFont == null || buttonFont == null)
        {
            Debug.LogWarning("[CrispText] Font tutarlılığı: gerekli font asset'leri bulunamadı; işlem yapılmadı.");
            return;
        }

        var boldFonts = new HashSet<TMP_FontAsset> { interExtraBold, inter28Bold, titleFont, buttonFont };
        foreach (string p in new[] { "Assets/_Project/Fonts/BakBakOne/BakbakOne-Regular SDF.asset", Inter24BoldAssetPath })
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            if (f != null) boldFonts.Add(f);
        }

        var stats = new int[5];   // 0 fontsuz/Anta, 1 Inter24→28, 2 materyal, 3 faux-bold, 4 iç içe atlanan
        void Fix(TMP_Text text, bool fauxBoldTrial)
        {
            bool changed = false;
            if (text.font == null || text.font == liberation || (anta != null && text.font == anta))
            {
                var to = IsTitleText(text) ? titleFont : IsButtonText(text) ? buttonFont : interExtraBold;
                float ratio = text.font != null ? CapHeightRatio(text.font, to) : 1f;
                text.font = to;
                text.fontSize *= ratio;
                text.fontSizeMax *= ratio;
                text.fontSizeMin *= ratio;
                text.fontSharedMaterial = to.material;
                text.fontStyle &= ~FontStyles.Bold;   // yeni fontlar zaten kalın
                if (to == titleFont) text.verticalAlignment = VerticalAlignmentOptions.Geometry;
                stats[0]++; changed = true;
            }
            else if (inter24Bold != null && text.font == inter24Bold)
            {
                var old = text.fontSharedMaterial;
                text.font = inter28Bold;
                text.fontSharedMaterial = MapPresetToFont(old, inter24Bold, inter28Bold);
                stats[1]++; changed = true;
            }

            var mat = text.fontSharedMaterial;
            var atlas = text.font != null ? text.font.material.GetTexture(ShaderUtilities.ID_MainTex) : null;
            if (text.font != null && (mat == null || mat.GetTexture(ShaderUtilities.ID_MainTex) != atlas))
            {
                text.fontSharedMaterial = text.font.material;
                stats[2]++; changed = true;
            }

            if (fauxBoldTrial && (text.fontStyle & FontStyles.Bold) != 0 && boldFonts.Contains(text.font))
            {
                text.fontStyle &= ~FontStyles.Bold;
                stats[3]++; changed = true;
            }
            if (changed) EditorUtility.SetDirty(text);
        }

        // Prefab'lar (Prefabs + Resources): iç içe prefab yazısı atlanır, kendi asset'inde düzelir.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<TMP_Text>(true) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int before = stats[0] + stats[1] + stats[2];
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(text)) { stats[4]++; continue; }
                    Fix(text, false);
                }
                if (stats[0] + stats[1] + stats[2] != before) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Sahneler (test sahneleri hariç). Faux-bold kaldırma yalnız MainMenu'de (deneme).
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = EditorSceneManager.GetActiveScene().path;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).Contains("Test")) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool trial = path == MainMenuScenePath;
            foreach (var go in scene.GetRootGameObjects())
            foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(text)) { stats[4]++; continue; }
                Fix(text, trial);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);
        Debug.Log($"[CrispText] Font tutarlılığı: fontsuz/Anta→oyun fontu {stats[0]}, Inter 24→28 Bold {stats[1]}, " +
                  $"uyumsuz materyal {stats[2]}, ana menü faux-bold kaldırıldı {stats[3]} (iç içe prefab'tan atlanan {stats[4]}).");
    }

    // Yazının ya da doğrudan ebeveyninin adında "title" geçiyorsa başlık (buton içindekiler hariç).
    private static bool IsTitleText(TMP_Text text)
    {
        if (IsButtonText(text)) return false;
        var parent = text.transform.parent;
        return text.name.ToLowerInvariant().Contains("title")
               || (parent != null && parent.name.ToLowerInvariant().Contains("title"));
    }

    private static bool IsButtonText(TMP_Text text) => text.GetComponentInParent<UnityEngine.UI.Button>(true) != null;

    // ── 5) Nunito geçişi (kullanıcı seçimi 2026-10-20: web sitesindeki Baloo 2 + Nunito) ─────────────────

    // Gövde/etiket: Inter → Nunito (aynı kalınlık sınıfı); ekran başlığı: BakbakOne → Baloo 2.
    // Popup başlık/buton (Baloo 2 / Nunito Black) ve oyun anları (DynaPuff) zaten ayrı; dokunulmaz.
    private const string NunitoExtraBoldTtf = "Assets/_Project/Fonts/Nunito/Nunito-ExtraBold.ttf";
    private const string NunitoExtraBoldAsset = "Assets/_Project/Fonts/Nunito/Nunito-ExtraBold SDF.asset";
    private const string NunitoBoldTtf = "Assets/_Project/Fonts/Nunito/Nunito-Bold.ttf";
    private const string NunitoBoldAsset = "Assets/_Project/Fonts/Nunito/Nunito-Bold SDF.asset";

    private static readonly string[] ShopPrefabPaths =
    {
        "Assets/_Project/Prefabs/UI/Shop/ShopScreenPanel.prefab",
        "Assets/_Project/Prefabs/UI/Shop/ShopOfferCard.prefab",
        "Assets/_Project/Prefabs/UI/Shop/ShopSectionHeader.prefab",
        "Assets/_Project/Prefabs/UI/Shop/ShopRewardIcon.prefab",
        "Assets/_Project/Prefabs/UI/Shop/ShopRewardGroupBox.prefab",
        "Assets/_Project/Prefabs/UI/Shop/ShopCoinRowCard.prefab",
    };

    [MenuItem("TinyFixers/Fonts/5) Nunito Denemesi — Mağaza (Inter→Nunito, BakBak→Baloo 2)")]
    public static void RunNunitoShopTrial() => SwapFontsInPrefabs(ShopPrefabPaths, "Mağaza");

    private static void SwapFontsInPrefabs(string[] prefabPaths, string label)
    {
        var map = BuildNunitoFontMap();
        if (map == null) return;

        int swapped = 0, nestedSkipped = 0;
        foreach (string path in prefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    // İç içe prefab'ın yazısı kendi asset'inde değişir; dış prefab'a override yazma.
                    if (PrefabUtility.IsPartOfPrefabInstance(text)) { nestedSkipped++; continue; }
                    if (SwapToNunitoFamily(text, map)) swapped++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[CrispText] Nunito geçişi ({label}): {swapped} yazı değişti; iç içe prefab'tan atlanan {nestedSkipped}.");
    }

    private static Dictionary<TMP_FontAsset, TMP_FontAsset> BuildNunitoFontMap()
    {
        var nunitoExtraBold = GetOrCreateFontAsset(NunitoExtraBoldTtf, NunitoExtraBoldAsset, UiFontPadding);
        var nunitoBold = GetOrCreateFontAsset(NunitoBoldTtf, NunitoBoldAsset, UiFontPadding);
        var baloo = GetOrCreateFontAsset(TitleTtfPath, TitleAssetPath, UiFontPadding);
        if (nunitoExtraBold == null || nunitoBold == null || baloo == null)
        {
            Debug.LogWarning("[CrispText] Nunito/Baloo 2 font asset'i oluşturulamadı; işlem yapılmadı.");
            return null;
        }

        var map = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        void Add(string fromPath, TMP_FontAsset to)
        {
            var from = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fromPath);
            if (from == null) return;
            map[from] = to;
            // Emoji/ikon gibi yedek fontlar (ör. 🪙) yeni fontta da bulunsun.
            if ((to.fallbackFontAssetTable == null || to.fallbackFontAssetTable.Count == 0)
                && from.fallbackFontAssetTable != null && from.fallbackFontAssetTable.Count > 0)
            {
                to.fallbackFontAssetTable = new List<TMP_FontAsset>(from.fallbackFontAssetTable);
                EditorUtility.SetDirty(to);
            }
        }
        Add("Assets/_Project/Fonts/Inter/Static/Inter_28pt-ExtraBold SDF.asset", nunitoExtraBold);
        Add("Assets/_Project/Fonts/Inter/Static/Inter_28pt-Bold SDF.asset", nunitoBold);
        Add("Assets/_Project/Fonts/Inter/Static/Inter_24pt-Bold SDF.asset", nunitoBold);
        Add("Assets/_Project/Fonts/BakBakOne/BakbakOne-Regular SDF.asset", baloo);
        return map;
    }

    private static bool SwapToNunitoFamily(TMP_Text text, Dictionary<TMP_FontAsset, TMP_FontAsset> map)
    {
        if (text.font == null || !map.TryGetValue(text.font, out var to)) return false;
        var from = text.font;
        var oldMaterial = text.fontSharedMaterial;

        float ratio = CapHeightRatio(from, to);   // görünen harf boyu aynı kalsın
        text.font = to;
        text.fontSize *= ratio;
        text.fontSizeMax *= ratio;
        text.fontSizeMin *= ratio;
        if (to.name.StartsWith("Baloo2"))
            text.verticalAlignment = VerticalAlignmentOptions.Geometry;   // Baloo 2'nin dev alt boşluğu
        text.fontSharedMaterial = MapPresetToFont(oldMaterial, from, to);
        EditorUtility.SetDirty(text);
        return true;
    }

    /// Eski fontun varsayılan materyali → yeni fontun varsayılanı. Ortak preset (kontur/gölge/bevel) →
    /// "{yeniFont}_from_{preset}.mat" kopyası: stil değerleri preset'ten, atlas + _GradientScale yeni fonttan.
    private static Material MapPresetToFont(Material oldMaterial, TMP_FontAsset from, TMP_FontAsset to)
    {
        if (oldMaterial == null || oldMaterial == from.material) return to.material;

        string path = $"{MaterialsDir}/{to.name.Replace(" SDF", "")}_from_{oldMaterial.name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool created = mat == null;
        if (created) mat = new Material(oldMaterial);
        else
        {
            mat.shader = oldMaterial.shader;
            mat.CopyPropertiesFromMaterial(oldMaterial);
        }
        mat.shaderKeywords = oldMaterial.shaderKeywords;

        var fontMat = to.material;
        mat.SetTexture(ShaderUtilities.ID_MainTex, fontMat.GetTexture(ShaderUtilities.ID_MainTex));
        foreach (int id in new[] { ShaderUtilities.ID_GradientScale, ShaderUtilities.ID_TextureWidth,
                     ShaderUtilities.ID_TextureHeight, ShaderUtilities.ID_WeightNormal, ShaderUtilities.ID_WeightBold })
            if (fontMat.HasProperty(id) && mat.HasProperty(id)) mat.SetFloat(id, fontMat.GetFloat(id));
        ShaderUtilities.UpdateShaderRatios(mat);

        if (created) AssetDatabase.CreateAsset(mat, path);
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    /// TTF'ten dinamik TMP font asset'i (90pt, çoklu atlas); varsa olanı döner. Atlas + materyal alt-varlık
    /// (TMP Font Asset Creator ile aynı düzen). Türkçe dahil sık karakterler hemen üretilir.
    private static TMP_FontAsset GetOrCreateFontAsset(string ttfPath, string assetPath, int padding)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        var source = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (source == null)
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning($"[CrispText] Font dosyası yok: {ttfPath}");
            return null;
        }

        string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\', '/'), Path.GetFileName(dir));

        var font = TMP_FontAsset.CreateFontAsset(source, TitleSamplingSize, padding, GlyphRenderMode.SDFAA,
            1024, 1024, AtlasPopulationMode.Dynamic, true);
        font.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(font, assetPath);

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
