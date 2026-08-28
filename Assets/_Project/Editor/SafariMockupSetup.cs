using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tiny Safari eventini tek tıkla kurar — Menü: TinyFixers > Mockup > Safari Event.
/// MainMenu sahnesine: event ikonu + "katıl" popup + tam-ekran safari harita (7 pitstop anchor,
/// avatar dairesi, sayaç, devam/durum) kurar; SafariConfig asset'ini üretir ve controller'ı bağlar.
/// Placeholder art — kalıcı görseller sonra.
///
/// Ayrıca "TinyFixers/Mockup/Safari Booster Injector (01_Game)" ile Game sahnesine booster enjektörü ekler.
/// </summary>
public static class SafariMockupSetup
{
    private const string ResDir     = "Assets/_Project/Resources/Events";
    private const string ConfigPath = ResDir + "/SafariConfig.asset";
    private const string BGPath     = "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/SafariBGV1.png";
    private const string PopupPath  = "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/SafriPopupBG.png";
    private static readonly string[] HelmetPaths =
    {
        "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/HB1.png",
        "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/HG1.png",
        "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/HR1.png",
        "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/HY1.png"
    };
    private const string SystemName = "SafariEventSystem";

    [MenuItem("TinyFixers/Mockup/Safari Event")]
    public static void Setup()
    {
        MockupUI.EnsureFolder(ResDir);
        var config = EnsureConfig();
        EnsureSpriteImport(BGPath);
        EnsureSpriteImport(PopupPath);
        EnsureHelmetImports();
        var bg = MockupUI.LoadSprite(BGPath);
        var theme = MockupUI.EnsureTheme();

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Safari Setup", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }
        var root = canvas.rootCanvas.transform;

        // Tekrar çalıştırılabilir: eski sistemi ve RightEventPanel altındaki eski ikonu temizle.
        var old = root.Find(SystemName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var oldIcon = FindChildByName(root, "SafariEventIcon");
        if (oldIcon != null) Object.DestroyImmediate(oldIcon.gameObject);

        var system = MockupUI.NewRect(SystemName, root);
        MockupUI.Stretch(system);

        var mapScreen  = BuildMapScreen(system, theme, bg, config, out var mapRoot);
        var joinPopup  = BuildJoinPopup(system, theme, out var popupRoot);
        var iconParent = FindChildByName(root, "RightEventPanel") ?? system;
        var eventBtn   = BuildEventIcon(iconParent, theme);

        // Controller + wiring
        var ctrlGO = MockupUI.NewRect("SafariController", system);
        var ctrl = ctrlGO.gameObject.AddComponent<SafariEventController>();
        MockupUI.SetRef(ctrl, "config", config);
        MockupUI.SetRef(ctrl, "eventButton", eventBtn);
        MockupUI.SetRef(ctrl, "joinPopup", joinPopup);
        MockupUI.SetRef(ctrl, "mapScreen", mapScreen);

        MockupUI.SetRef(eventBtn, "controller", ctrl);
        if (iconParent == system)
            AddHomeOnlyElement(eventBtn.gameObject);

        // Overlay'ler başta kapalı; controller runtime'da açar.
        mapRoot.SetActive(false);
        popupRoot.SetActive(false);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Safari Setup",
            "Tiny Safari kuruldu (ikon + popup + harita). Sahneyi kaydet (Cmd+S).", "Tamam");
    }

    // ── Map screen ───────────────────────────────────────────────

    private static SafariMapScreen BuildMapScreen(Transform parent, UITheme theme, Sprite bg,
                                                  SafariConfig config, out GameObject mapRoot)
    {
        var rootRt = MockupUI.NewRect("SafariMapScreen", parent);
        MockupUI.Stretch(rootRt);
        mapRoot = rootRt.gameObject;
        var map = mapRoot.AddComponent<SafariMapScreen>();

        // Letterbox boşluğunu dolduran + tıkı bloklayan tam-ekran arkalık (gökyüzü tonu).
        var backdrop = MockupUI.NewImage("Backdrop", rootRt, new Color(0.42f, 0.66f, 0.88f, 1f));
        MockupUI.Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;

        // Arka plan — ekranı oran bozmadan doldurur; güvenli kenarlar gerekirse kırpılır. Yol öğeleri BG'nin
        // çocuğu olur ki görselle birlikte hizalanıp ölçeklensin.
        var bgImg = MockupUI.NewImage("BG", rootRt, Color.white);
        bgImg.rectTransform.anchorMin = bgImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        bgImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        if (bg != null) bgImg.sprite = bg;
        bgImg.preserveAspect = false;   // oranı fitter yönetir
        bgImg.raycastTarget = true;
        // EnvelopeParent: ekranı DOLDURUR, oranı BOZMAZ, taşan (genişletilmiş güvenli) kenarları kırpar.
        var fitter = bgImg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = (bg != null && bg.rect.height > 0.01f) ? bg.rect.width / bg.rect.height : 0.5f;
        var board = bgImg.rectTransform;   // yol öğeleri buna bağlanır

        // Başlangıç + pitstop anchor'ları — BG'ye göre normalize (görselle hizalı, birlikte ölçeklenir)
        var start = MakeAnchor("StartAnchor", board, new Vector2(0.10f, 0.02f));
        var frac = new[]
        {
            new Vector2(0.22f, 0.06f), new Vector2(0.62f, 0.20f), new Vector2(0.28f, 0.33f),
            new Vector2(0.66f, 0.44f), new Vector2(0.30f, 0.55f), new Vector2(0.62f, 0.63f),
            new Vector2(0.34f, 0.74f)
        };
        int n = Mathf.Min(config.pitstopCount, frac.Length);
        var anchors = new Object[n];
        for (int i = 0; i < n; i++)
            anchors[i] = MakeAnchor($"Pitstop{i + 1}", board, frac[i]);

        var cliff = MakeAnchor("CliffPoint", board, new Vector2(0.90f, 0.30f));

        // Oyuncu marker'ı (placeholder daire, runtime'da gizlenir)
        var marker = MockupUI.NewImage("PlayerMarker", board, new Color(1f, 0.85f, 0.2f, 1f));
        marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        marker.rectTransform.sizeDelta = new Vector2(72, 72);
        marker.raycastTarget = false;

        // Sol-üst avatar dairesi (yığın container)
        var circle = MockupUI.NewImage("AvatarCircle", rootRt, new Color(0f, 0f, 0f, 0.28f));
        circle.rectTransform.anchorMin = circle.rectTransform.anchorMax = new Vector2(0f, 1f);
        circle.rectTransform.pivot = new Vector2(0f, 1f);
        circle.rectTransform.anchoredPosition = new Vector2(28, -28);
        circle.rectTransform.sizeDelta = new Vector2(230, 230);
        circle.raycastTarget = false;
        var stack = circle.gameObject.AddComponent<SafariAvatarStackView>();
        MockupUI.SetRef(stack, "container", circle.rectTransform);
        MockupUI.SetRefArray(stack, "helmetSprites", LoadHelmetSprites());

        var counter = MockupUI.NewText("Counter", rootRt, "1 / 100", 40,
            Color.white, TextAlignmentOptions.Center, theme.headingFont);
        counter.rectTransform.anchorMin = counter.rectTransform.anchorMax = new Vector2(0f, 1f);
        counter.rectTransform.pivot = new Vector2(0f, 1f);
        counter.rectTransform.anchoredPosition = new Vector2(28, -268);
        counter.rectTransform.sizeDelta = new Vector2(280, 60);

        // Durum yazısı + devam butonu (alt)
        var status = MockupUI.NewText("Status", rootRt, "", 34,
            Color.white, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.AnchorBottom(status.rectTransform, 60, 220);

        var continueRoot = MockupUI.NewRect("ContinueRoot", rootRt);
        MockupUI.AnchorBottom(continueRoot, 120, 70);
        var continueBtn = MockupUI.GlossyButton(continueRoot, "Assets/_Project/Art/UI/Buttons/GreenButton.png",
            theme.ctaGreen, "Devam", 40, theme.headingFont, out var continueLabel);
        continueBtn.image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        continueBtn.image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        continueBtn.image.rectTransform.sizeDelta = new Vector2(360, 110);
        continueBtn.image.rectTransform.anchoredPosition = Vector2.zero;

        // Kapat (sağ üst)
        var closeBtn = MockupUI.GlossyButton(rootRt, "Assets/_Project/Art/UI/Buttons/RedButton.png",
            new Color(0.8f, 0.3f, 0.25f), "X", 36, theme.headingFont, out _);
        closeBtn.image.rectTransform.anchorMin = closeBtn.image.rectTransform.anchorMax = new Vector2(1f, 1f);
        closeBtn.image.rectTransform.pivot = new Vector2(1f, 1f);
        closeBtn.image.rectTransform.anchoredPosition = new Vector2(-24, -24);
        closeBtn.image.rectTransform.sizeDelta = new Vector2(90, 90);

        // Wiring
        MockupUI.SetRef(map, "root", mapRoot);
        MockupUI.SetRef(map, "avatarStack", stack);
        MockupUI.SetRef(map, "counterText", counter);
        MockupUI.SetRef(map, "startAnchor", start);
        MockupUI.SetRefArray(map, "pitstopAnchors", anchors);
        MockupUI.SetRef(map, "playerMarker", marker.rectTransform);
        MockupUI.SetRef(map, "cliffPoint", cliff);
        MockupUI.SetRef(map, "continueRoot", continueRoot.gameObject);
        MockupUI.SetRef(map, "continueButton", continueBtn);
        MockupUI.SetRef(map, "continueLabel", continueLabel);
        MockupUI.SetRef(map, "statusText", status);
        MockupUI.SetRef(map, "closeButton", closeBtn);

        return map;
    }

    private static RectTransform MakeAnchor(string name, Transform parent, Vector2 frac)
    {
        var rt = MockupUI.NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = frac;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(8, 8);
        return rt;
    }

    // ── Join popup ───────────────────────────────────────────────

    private static SafariJoinPopupController BuildJoinPopup(Transform parent, UITheme theme, out GameObject popupRoot)
    {
        var rootRt = MockupUI.NewRect("SafariJoinPopup", parent);
        MockupUI.Stretch(rootRt);
        popupRoot = rootRt.gameObject;
        var popup = popupRoot.AddComponent<SafariJoinPopupController>();

        // Karartma
        var dim = MockupUI.NewImage("Dim", rootRt, new Color(0, 0, 0, 0.65f));
        MockupUI.Stretch(dim.rectTransform);

        // Panel: PreLevel popup gibi tek ana görsel + üzerine opsiyonel overlay image.
        var panel = MockupUI.NewImage("PopupPanel", rootRt, theme.panelSurface);
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.sizeDelta = new Vector2(760, 620);
        var popupSprite = MockupUI.LoadSprite(PopupPath);
        if (popupSprite != null) panel.sprite = popupSprite;

        var overlay = MockupUI.NewImage("OverlayImage", panel.rectTransform, Color.white);
        overlay.rectTransform.anchorMin = overlay.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        overlay.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        overlay.rectTransform.anchoredPosition = new Vector2(0, 40);
        overlay.rectTransform.sizeDelta = new Vector2(420, 260);
        overlay.gameObject.SetActive(false);

        var joinBtn = MockupUI.GlossyButton(panel.rectTransform, "Assets/_Project/Art/UI/Buttons/GreenButton.png",
            theme.ctaGreen, "Devam", 40, theme.headingFont, out _);
        joinBtn.gameObject.name = "ContinueButton";
        joinBtn.image.rectTransform.anchorMin = joinBtn.image.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        joinBtn.image.rectTransform.pivot = new Vector2(0.5f, 0f);
        joinBtn.image.rectTransform.anchoredPosition = new Vector2(0, 130);
        joinBtn.image.rectTransform.sizeDelta = new Vector2(400, 110);

        var declineBtn = MockupUI.GlossyButton(rootRt, "Assets/_Project/Art/UI/Buttons/RedButton.png",
            new Color(0.6f, 0.55f, 0.5f), "Vazgeç", 32, theme.bodyFont, out _);
        declineBtn.gameObject.name = "CancelButton";
        declineBtn.image.rectTransform.anchorMin = declineBtn.image.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        declineBtn.image.rectTransform.pivot = new Vector2(0.5f, 0f);
        declineBtn.image.rectTransform.anchoredPosition = new Vector2(0, 70);
        declineBtn.image.rectTransform.sizeDelta = new Vector2(320, 80);

        MockupUI.SetRef(popup, "root", popupRoot);
        MockupUI.SetRef(popup, "popupRoot", panel.rectTransform);
        MockupUI.SetRef(popup, "popupBackgroundImage", panel);
        MockupUI.SetRef(popup, "overlayImage", overlay);
        MockupUI.SetRef(popup, "continueButton", joinBtn);
        MockupUI.SetRef(popup, "continueButtonImage", joinBtn.image);
        MockupUI.SetRef(popup, "cancelButton", declineBtn);
        MockupUI.SetRef(popup, "cancelButtonImage", declineBtn.image);
        return popup;
    }

    // ── Event icon ───────────────────────────────────────────────

    private static SafariEventButton BuildEventIcon(Transform parent, UITheme theme)
    {
        var icon = MockupUI.NewImage("SafariEventIcon", parent, Color.white);
        var bg = MockupUI.LoadSprite(BGPath);
        if (bg != null) icon.sprite = bg;
        icon.preserveAspect = true;
        var rt = icon.rectTransform;
        if (parent != null && parent.name == "RightEventPanel")
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(24, 120);
        }
        rt.sizeDelta = new Vector2(150, 150);

        var btn = icon.gameObject.AddComponent<Button>();
        btn.targetGraphic = icon;
        icon.gameObject.AddComponent<EventIconAnimator>();

        var label = MockupUI.NewText("Label", rt, "SAFARI", 22,
            Color.white, TextAlignmentOptions.Bottom, theme.headingFont);
        MockupUI.AnchorBottom(label.rectTransform, 34, 4);

        var comp = icon.gameObject.AddComponent<SafariEventButton>();
        MockupUI.SetRef(comp, "button", btn);
        MockupUI.SetRef(comp, "labelText", label);
        MockupUI.SetRef(comp, "visibilityRoot", icon.gameObject);
        return comp;
    }

    private static void AddHomeOnlyElement(GameObject element)
    {
        if (element == null) return;

        var tabs = Resources.FindObjectsOfTypeAll<BottomTabController>();
        if (tabs == null || tabs.Length == 0) return;

        BottomTabController target = null;
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null && tabs[i].gameObject.scene.IsValid())
            {
                target = tabs[i];
                break;
            }
        }
        if (target == null) return;

        var so = new SerializedObject(target);
        var array = so.FindProperty("homeOnlyElements");
        if (array == null || !array.isArray) return;

        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == element)
                return;
        }

        array.InsertArrayElementAtIndex(array.arraySize);
        array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = element;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;

        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == childName)
                return all[i];
        }

        return null;
    }

    // ── Assets ───────────────────────────────────────────────────

    private static SafariConfig EnsureConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<SafariConfig>(ConfigPath);
        if (config != null) return config;

        MockupUI.EnsureFolder(ResDir);
        config = ScriptableObject.CreateInstance<SafariConfig>();
        AssetDatabase.CreateAsset(config, ConfigPath);
        AssetDatabase.SaveAssets();
        return config;
    }

    private static void EnsureSpriteImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureHelmetImports()
    {
        for (int i = 0; i < HelmetPaths.Length; i++)
            EnsureSpriteImport(HelmetPaths[i]);
    }

    private static Sprite[] LoadHelmetSprites()
    {
        var sprites = new Sprite[HelmetPaths.Length];
        for (int i = 0; i < HelmetPaths.Length; i++)
            sprites[i] = MockupUI.LoadSprite(HelmetPaths[i]);
        return sprites;
    }
}
