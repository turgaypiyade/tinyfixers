using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Alt-menü mockup kurulum script'lerinin paylaştığı UGUI yardımcıları (Editor-only).
/// Panel iskeleti, scroll, layout, serialized-field bağlama, prefab kaydetme.
/// Her ekran kendi *MockupSetup'ında bunu kullanır — kopya kod olmasın.
/// </summary>
public static class MockupUI
{
    // ---- Temel oluşturucular ------------------------------------------

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        return rt;
    }

    public static Image NewImage(string name, Transform parent, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    /// <summary>Sprite'lı image; sprite varsa Sliced (9-slice), yoksa düz renk.</summary>
    public static Image NewSlicedImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var img = NewImage(name, parent, color);
        UITheme.ApplySurface(img, sprite, color);
        return img;
    }

    /// <summary>Tema kart yüzeyi uygular (rounded sprite + tint). Beautify çalışmadıysa düz renk kalır.</summary>
    public static void Card(Image img, UITheme theme, Color color)
        => UITheme.ApplySurface(img, theme != null ? theme.cardBackground : null, color);

    // ---- Sprite yükleme (editor-only) ----------------------------------

    public static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    // ProfileScreen'deki AvatarCircle'ın birebir kopyası: ProfileAvatarBG çerçeve +
    // soft_circle mask (showMaskGraphic:0) + iç AvatarImage. Dönen Image = iç avatar;
    // runtime sprite'ı buna yazılır. root = dış çember (konumlandırma/LayoutElement için).
    public static Image BuildAvatarCircle(string name, Transform parent, float size, out RectTransform root)
    {
        var frameSprite = LoadSprite("Assets/_Project/Art/UI/ProfileUI/ProfileAvatarBG.png");
        var maskSprite  = LoadSprite("Assets/_Project/Art/Icons/PulseCoreEffectsIcon/soft_circle.png");

        var frame = NewImage(name, parent, Color.white);
        root = frame.rectTransform;
        root.sizeDelta = new Vector2(size, size);
        frame.sprite = frameSprite;
        frame.preserveAspect = true;
        frame.raycastTarget = false;

        // Daire kırpma katmanı
        var maskRt = NewRect("Mask", root);
        Stretch(maskRt);
        var maskImg = maskRt.gameObject.AddComponent<Image>();
        maskImg.sprite = maskSprite;
        maskImg.raycastTarget = false;
        var mask = maskRt.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // İç avatar (kırpılır). Profildeki oran: 300/350.
        var avatar = NewImage("AvatarImage", maskRt, Color.white);
        var ar = avatar.rectTransform;
        ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f);
        ar.pivot = new Vector2(0.5f, 0.5f);
        ar.anchoredPosition = Vector2.zero;
        ar.sizeDelta = new Vector2(size, size) * (300f / 350f);
        avatar.preserveAspect = true;
        avatar.raycastTarget = false;
        return avatar;
    }

    /// <summary>Mock avatar havuzu: TopHUD robot yüzleri + yükleme robotları. Bulunanları döner.</summary>
    public static Sprite[] AvatarPool()
    {
        string[] paths =
        {
            "Assets/_Project/Art/UI/TopHUD/Robot_happy.png",
            "Assets/_Project/Art/UI/TopHUD/Robot_excited.png",
            "Assets/_Project/Art/UI/TopHUD/Robot_idle.png",
            "Assets/_Project/Art/UI/TopHUD/Robot_sad.png",
            "Assets/_Project/Art/UI/RoboCharacters/LoadWrenchBot.png",
            "Assets/_Project/Art/UI/RoboCharacters/LoadBolt.png",
            "Assets/_Project/Art/UI/RoboCharacters/LoadMediBot.png",
            "Assets/_Project/Art/UI/RoboCharacters/LoadPatchbot.png",
        };
        var list = new System.Collections.Generic.List<Sprite>();
        foreach (var p in paths)
        {
            var s = LoadSprite(p);
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }

    /// <summary>Glossy hazır buton (GreenButton/BlueButton png). Sprite yoksa düz renge düşer.</summary>
    public static Button GlossyButton(Transform parent, string spritePath, Color fallback, string label,
                                      float fontSize, TMP_FontAsset font, out TextMeshProUGUI text)
    {
        var sprite = LoadSprite(spritePath);
        var img = NewImage("Btn_" + label, parent, Color.white);
        if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
        else img.color = fallback;

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        text = NewText("Label", img.rectTransform, label, fontSize, Color.white, TextAlignmentOptions.Center, font);
        AnchorBox(text.rectTransform, Vector2.zero, Vector2.one);
        return btn;
    }

    public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size,
                                          Color color, TextAlignmentOptions align, TMP_FontAsset font)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        if (font != null) t.font = font;
        return t;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    public static void AnchorBox(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    /// <summary>Üstten yapışık, tam genişlik, sabit yükseklik; y = üstten px boşluk.</summary>
    public static void AnchorTop(RectTransform rt, float height, float y)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -y); rt.sizeDelta = new Vector2(0, height);
    }

    /// <summary>Alttan yapışık, tam genişlik, sabit yükseklik; y = alttan px boşluk.</summary>
    public static void AnchorBottom(RectTransform rt, float height, float y)
    {
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(0, height);
    }

    /// <summary>Tam genişlik dikey doldur; top/bottom px boşluk bırakır.</summary>
    public static void AnchorFill(RectTransform rt, float topOffset, float bottomOffset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0, bottomOffset); rt.offsetMax = new Vector2(0, -topOffset);
    }

    public static LayoutElement LayoutElem(GameObject go, float preferredWidth = -1, float preferredHeight = -1)
    {
        var le = go.AddComponent<LayoutElement>();
        if (preferredWidth  >= 0) le.preferredWidth  = preferredWidth;
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        return le;
    }

    public static VerticalLayoutGroup VLayout(GameObject go, int pad, float spacing, bool expandH = false)
    {
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(pad, pad, pad, pad);
        v.spacing = spacing;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = expandH;
        v.childAlignment = TextAnchor.UpperCenter;
        return v;
    }

    public static HorizontalLayoutGroup HLayout(GameObject go, float spacing)
    {
        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = true;
        h.childAlignment = TextAnchor.MiddleCenter;
        return h;
    }

    // ---- Panel iskeleti -----------------------------------------------

    /// <summary>
    /// Alt çubuğun hemen önüne tam-ekran bir panel + üst başlık bandı kurar.
    /// body = başlık altı / alt çubuk üstü kalan alan. Panel başlangıçta kapalı.
    /// </summary>
    public static GameObject BuildScreenPanel(Transform bottomBar, string name, UITheme theme,
                                              string title, out RectTransform body)
    {
        var parent = bottomBar.parent;
        // Idempotent: önceki çalıştırmadan kalan aynı isimli paneli sil (kopya olmasın).
        var old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var panel = NewRect(name, parent);
        panel.SetSiblingIndex(bottomBar.GetSiblingIndex());
        Stretch(panel);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = theme.screenBackground;

        var top = NewRect("TopBar", panel);
        top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1);
        top.anchoredPosition = Vector2.zero; top.sizeDelta = new Vector2(0, 150);
        var topImg = top.gameObject.AddComponent<Image>();
        UITheme.ApplySurface(topImg, theme.sectionHeaderBackground, theme.headerBand);
        var titleText = NewText("Title", top, title, 48, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(titleText.rectTransform, Vector2.zero, Vector2.one);

        // Alt çubuğu tam temizle: body'nin altı, çubuğun gerçek yüksekliğinin üstünde başlasın.
        float barHeight = bottomBar is RectTransform brt ? brt.rect.height : 200f;
        body = NewRect("Body", panel);
        body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(0, barHeight + 20f); body.offsetMax = new Vector2(0, -160);

        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    /// <summary>parent içine dikey scroll list kurar; doldurulacak Content RectTransform döner.</summary>
    public static RectTransform BuildVerticalScroll(RectTransform parent)
    {
        var scroll = NewRect("ScrollView", parent);
        Stretch(scroll);
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;

        var viewport = NewRect("Viewport", scroll);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var cvl = VLayout(content.gameObject, 24, 16);
        cvl.padding.bottom = 40;    // son satır rahat görünsün (body zaten alt çubuğu temizliyor)
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewport; sr.content = content;
        return content;
    }

    // ---- Bağlama & kaydetme -------------------------------------------

    public static BottomTabController FindTabController()
        => Object.FindFirstObjectByType<BottomTabController>(FindObjectsInactive.Include);

    public static void AssignTabPanel(BottomTabController controller, string tabName, GameObject panel)
    {
        var so = new SerializedObject(controller);
        var tabs = so.FindProperty("tabs");
        if (tabs == null) return;
        for (int i = 0; i < tabs.arraySize; i++)
        {
            var el = tabs.GetArrayElementAtIndex(i);
            var n = el.FindPropertyRelative("name");
            if (n != null && n.stringValue == tabName)
            {
                el.FindPropertyRelative("panel").objectReferenceValue = panel;
                break;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Serialized Object[] alanına (örn. Sprite[] avatarPool) dizi bağlar.</summary>
    public static void SetRefArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[MockupUI] '{field}' alanı {target} üzerinde yok."); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[MockupUI] '{field}' alanı {target} üzerinde yok."); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static T SaveAndLoadPrefab<T>(GameObject temp, string path) where T : Object
    {
        PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    public const string ThemePath = "Assets/_Project/Settings/UITheme.asset";

    /// <summary>Paylaşılan UITheme asset'ini yükler; yoksa default paletle oluşturur.</summary>
    public static UITheme EnsureTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme != null) return theme;

        EnsureFolder("Assets/_Project/Settings");
        theme = ScriptableObject.CreateInstance<UITheme>();
        theme.headingFont = TMP_Settings.defaultFontAsset;
        theme.bodyFont    = TMP_Settings.defaultFontAsset;
        AssetDatabase.CreateAsset(theme, ThemePath);
        return theme;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf   = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
