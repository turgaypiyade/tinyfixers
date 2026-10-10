using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bostan Hasadı tabela başlığı (kullanıcı referansı "Garden Event"): iki satır — üstte büyük, harf başına
/// açık sarı → turuncu gradyan; altta krem. İkisi de kalın koyu kahve kontur + sert kahve gölge (tabelanın
/// ahşabından koyu ton). Font denemesi için iki menü: Baloo 2 ExtraBold / DynaPuff Bold.
/// Gradyan vertex rengi (materyalde değil) → iki satır aynı materyali paylaşır.
///
/// Menü: TinyFixers ▸ Mockup ▸ Harvest Başlık ▸ … (MainMenu sahnesi açıkken). Sahneyi kaydet.
/// </summary>
public static class HarvestTitleStyle
{
    private const string MaterialsDir = "Assets/_Project/Fonts/Materials";
    private const string BalooPath = "Assets/_Project/Fonts/Baloo2/Baloo2-ExtraBold SDF.asset";
    private const string DynaPuffPath = "Assets/_Project/Fonts/DynaPuff/DynaPuff-Bold SDF.asset";
    private const string PrefKey = "HarvestTitleStyle.Font";

    private static readonly Color GradTop = new(1f, 0.96f, 0.45f, 1f);      // açık limon sarısı
    private static readonly Color GradBottom = new(1f, 0.68f, 0.08f, 1f);   // turuncu-altın
    private static readonly Color Cream = new(1f, 0.95f, 0.84f, 1f);
    private static readonly Color Outline = new(0.33f, 0.16f, 0.05f, 1f);   // koyu kahve (ahşabın koyusu)
    private static readonly Color Shadow = new(0.20f, 0.09f, 0.03f, 1f);
    private static readonly Vector2 ShadowOffset = new(0.15f, -0.7f);       // sağa az, aşağı çok (sert)

    [MenuItem("TinyFixers/Mockup/Harvest Başlık/Baloo 2 ExtraBold")]
    private static void UseBaloo() => Run(BalooPath, "Baloo2-ExtraBold_HarvestSign", 0.30f);

    [MenuItem("TinyFixers/Mockup/Harvest Başlık/DynaPuff Bold")]
    private static void UseDynaPuff() => Run(DynaPuffPath, "DynaPuff_HarvestSign", 0.24f);   // padding 12 → oransal daha kalın

    /// Mockup kurulumu sonunda: son seçilen font (varsayılan Baloo 2) + hasat/süre hapları.
    public static void ApplyLastChoice(HarvestScreen screen)
    {
        bool dyna = EditorPrefs.GetString(PrefKey, "baloo") == "dyna";
        if (Apply(screen, dyna ? DynaPuffPath : BalooPath,
                dyna ? "DynaPuff_HarvestSign" : "Baloo2-ExtraBold_HarvestSign", dyna ? 0.24f : 0.30f))
            BuildInfoRow(screen);
    }

    // ── Tabela altı: hasat sayısı + kalan süre hapları (referans). Tabela/başlığa dokunmaz. ──

    private const string RoundedPath = "Assets/_Project/Art/UI/Generated/RoundedRect.png";   // 9-slice, kenar 40px
    private const string TimerIconPath = "Assets/_Project/Art/UI/MarketUI/Timerimage.png";
    private const string RoundIconPath = "Assets/_Project/Art/UI/MainScreenEvents/Harvest/Harvest_Carrot.png";
    private static readonly Color PillRim = new(1f, 0.93f, 0.78f, 1f);       // krem kenar
    private static readonly Color PillField = new(0.36f, 0.19f, 0.08f, 1f);  // koyu kahve iç
    private static readonly Color SocketRim = new(0.80f, 0.56f, 0.30f, 1f);
    private static readonly Color SocketFill = new(1f, 0.97f, 0.88f, 1f);
    private static readonly Vector2 PillSize = new(340f, 88f);
    private const float PillGap = 44f;

    [MenuItem("TinyFixers/Mockup/Harvest Başlık/Hasat + Süre Hapları")]
    private static void RunInfoRow()
    {
        var screen = Object.FindFirstObjectByType<HarvestScreen>(FindObjectsInactive.Include);
        if (screen == null)
        {
            EditorUtility.DisplayDialog("Harvest HUD", "MainMenu sahnesini aç (HarvestScreen bulunamadı).", "Tamam");
            return;
        }
        if (!BuildInfoRow(screen)) return;
        EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
        Debug.Log("[Harvest] Hasat + süre hapları kuruldu. Sahneyi kaydet (Cmd+S).");
    }

    private static bool BuildInfoRow(HarvestScreen screen)
    {
        var so = new SerializedObject(screen);
        var title = so.FindProperty("titleText").objectReferenceValue as TextMeshProUGUI;
        var floor = so.FindProperty("floorText").objectReferenceValue as TextMeshProUGUI;
        if (title == null || floor == null) { Debug.LogWarning("[Harvest] titleText/floorText atanmamış."); return false; }

        var frame = (RectTransform)title.rectTransform.parent;
        var old = frame.Find("InfoRow");
        if (old != null)
        {
            floor.rectTransform.SetParent(frame, false);   // yeniden kurulumda hasat yazısı kaybolmasın
            Object.DestroyImmediate(old.gameObject);
        }

        var row = MockupUI.NewRect("InfoRow", frame);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(PillSize.x * 2f + PillGap, PillSize.y + 30f);
        row.anchoredPosition = new Vector2(0f, -frame.rect.height * 0.5f - 50f);

        float x = (PillSize.x + PillGap) * 0.5f;
        var roundText = Pill("HarvestPill", row, -x, MockupUI.LoadSprite(RoundIconPath));
        var timerText = Pill("TimerPill", row, x, MockupUI.LoadSprite(TimerIconPath));

        // Hasat sayısı: mevcut floorText hapın içine taşınır (referanslar aynı kalır).
        floor.rectTransform.SetParent(roundText, false);
        PillText(floor, title);
        floor.text = "Hasat 1/4";

        var timer = MockupUI.NewText("Timer", timerText, "4g 12s", 46f, Color.white, TextAlignmentOptions.Center, title.font);
        PillText(timer, title);

        so.FindProperty("timerText").objectReferenceValue = timer;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    /// Krem kenarlı, koyu kahve içli hap; solda ikon yuvası. Dönen: yazı alanı.
    private static RectTransform Pill(string name, RectTransform parent, float x, Sprite icon)
    {
        var rounded = MockupUI.LoadSprite(RoundedPath);
        var rim = MockupUI.NewImage(name, parent, PillRim);
        rim.sprite = rounded;
        rim.type = Image.Type.Sliced;
        rim.pixelsPerUnitMultiplier = 40f / (PillSize.y * 0.5f);   // köşe = yüksekliğin yarısı → tam kapsül
        rim.raycastTarget = false;
        var rt = rim.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = PillSize;
        rt.anchoredPosition = new Vector2(x, 0f);

        const float inset = 9f, socket = 112f;
        var field = MockupUI.NewImage("Field", rt, PillField);
        field.sprite = rounded;
        field.type = Image.Type.Sliced;
        field.pixelsPerUnitMultiplier = 40f / (PillSize.y * 0.5f - inset);
        field.raycastTarget = false;
        var frt = field.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(socket * 0.5f, inset);
        frt.offsetMax = new Vector2(-inset, -inset);

        // İkon yuvası: hapın sol ucunu taşan yuvarlak (referanstaki gibi haptan biraz büyük).
        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var ring = MockupUI.NewImage("Socket", rt, SocketRim);
        ring.sprite = knob;
        ring.raycastTarget = false;
        var srt = ring.rectTransform;
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0f, 0.5f);
        srt.sizeDelta = new Vector2(socket, socket);
        srt.anchoredPosition = new Vector2(-socket * 0.22f, 0f);
        var fill = MockupUI.NewImage("Fill", srt, SocketFill);
        fill.sprite = knob;
        fill.raycastTarget = false;
        var fl = fill.rectTransform;
        fl.anchorMin = Vector2.zero;
        fl.anchorMax = Vector2.one;
        fl.offsetMin = new Vector2(7f, 7f);
        fl.offsetMax = new Vector2(-7f, -7f);
        var img = MockupUI.NewImage("Icon", srt, Color.white);
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var irt = img.rectTransform;
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(socket * 0.72f, socket * 0.72f);
        irt.anchoredPosition = Vector2.zero;

        return frt;
    }

    private static void PillText(TMP_Text t, TextMeshProUGUI title)
    {
        t.font = title.font;
        t.fontSharedMaterial = title.fontSharedMaterial;   // tabelayla aynı kahve kontur
        t.fontStyle = FontStyles.Normal;
        t.enableVertexGradient = false;
        t.color = Color.white;
        t.enableAutoSizing = true;
        t.fontSizeMax = 46f;
        t.fontSizeMin = 26f;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(10f, 0f);
        rt.offsetMax = new Vector2(-10f, 0f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        EditorUtility.SetDirty(t);
    }

    private static void Run(string fontPath, string matName, float outlineWidth)
    {
        var screen = Object.FindFirstObjectByType<HarvestScreen>(FindObjectsInactive.Include);
        if (screen == null)
        {
            EditorUtility.DisplayDialog("Harvest Başlık", "MainMenu sahnesini aç (HarvestScreen bulunamadı).", "Tamam");
            return;
        }
        EditorPrefs.SetString(PrefKey, fontPath == DynaPuffPath ? "dyna" : "baloo");
        if (!Apply(screen, fontPath, matName, outlineWidth)) return;
        EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Harvest] Tabela başlığı: {matName}. Sahneyi kaydet (Cmd+S).");
    }

    private static bool Apply(HarvestScreen screen, string fontPath, string matName, float outlineWidth)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if (font == null) { Debug.LogWarning($"[Harvest] Font yok: {fontPath}"); return false; }
        var mat = GetOrCreateMaterial(font, $"{MaterialsDir}/{matName}.mat", outlineWidth);

        var so = new SerializedObject(screen);
        var title = so.FindProperty("titleText").objectReferenceValue as TextMeshProUGUI;
        var floor = so.FindProperty("floorText").objectReferenceValue as TextMeshProUGUI;
        if (title == null) { Debug.LogWarning("[Harvest] titleText atanmamış."); return false; }

        // Referansta tabela yazısı düz; kavis kaldırılır.
        if (title.TryGetComponent(out TMPArcText arc)) Object.DestroyImmediate(arc);

        var parent = (RectTransform)title.rectTransform.parent;
        var subtitle = parent.Find("Subtitle")?.GetComponent<TextMeshProUGUI>();
        if (subtitle == null)
        {
            subtitle = Object.Instantiate(title, parent);
            subtitle.name = "Subtitle";
        }

        Style(title, font, mat, 118f, new Vector2(720f, 140f), new Vector2(0f, 52f));
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(GradTop, GradTop, GradBottom, GradBottom);   // harf başına
        title.text = "Bostan";

        Style(subtitle, font, mat, 84f, new Vector2(620f, 110f), new Vector2(0f, -48f));
        subtitle.enableVertexGradient = false;
        subtitle.color = Cream;
        subtitle.text = "Hasadı";

        // "Hasat 1/4" tabelanın altına (hap görselleri gelene kadar geçici yer).
        if (floor != null && floor.rectTransform.parent == parent)
            floor.rectTransform.anchoredPosition = new Vector2(0f, -parent.rect.height * 0.5f - 40f);

        so.FindProperty("subtitleText").objectReferenceValue = subtitle;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(title);
        EditorUtility.SetDirty(subtitle);
        return true;
    }

    private static void Style(TextMeshProUGUI t, TMP_FontAsset font, Material mat, float size, Vector2 box, Vector2 pos)
    {
        t.font = font;
        t.fontSharedMaterial = mat;
        t.fontStyle = FontStyles.Normal;   // kalın asset'e faux-bold harf aralığını açar
        t.characterSpacing = 0f;
        t.color = Color.white;
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.55f;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    // Değerler API ile + UpdateShaderRatios (YAML'dan elle değişince gölge mesh payı güncellenmez, kırpılır).
    private static Material GetOrCreateMaterial(TMP_FontAsset font, string path, float outlineWidth)
    {
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

        mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);   // yüz rengi vertex'ten (gradyan / krem)
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.05f);
        mat.EnableKeyword("OUTLINE_ON");
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);

        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, Shadow);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, outlineWidth * 2f);   // gölge kontur kadar kalın
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, ShadowOffset.x);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, ShadowOffset.y);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);

        ShaderUtilities.UpdateShaderRatios(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
