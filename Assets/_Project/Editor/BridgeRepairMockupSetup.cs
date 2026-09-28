using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridge Repair eventini tek tıkla kurar — Menü: TinyFixers ▸ Mockup ▸ Bridge Repair Event (MainMenu açıkken).
///  - Config (Resources/Events/BridgeRepairConfig.asset) + 5 karakterin kare listesi.
///  - Yarış ekranı: gökyüzü dolgusu + arka plan Board (arka plan pikseli uzayı) + 5 köprü anchor'ı (sol/sağ
///    çıkıntıların ön-üst kenarı, BridgeRepairBG'den ölçüldü) + podyum slotları + TopHUD tabelası + butonlar.
///  - LeftEventPanel'e ikon (geri sayım + ilerleme rozeti).
/// Tekrar çalıştırılabilir; anchor'lar Inspector'dan sürüklenerek ince ayarlanır (yeniden kurulum sıfırlar).
/// </summary>
public static class BridgeRepairMockupSetup
{
    private const string ArtDir     = "Assets/_Project/Art/UI/MainScreenEvents/BridgeRepair/";
    private const string CharDir    = ArtDir + "Characters/";
    private const string ConfigPath = "Assets/_Project/Resources/Events/BridgeRepairConfig.asset";
    private const string BgPath     = ArtDir + "BridgeRepairBG.png";
    private const string LeftPath   = ArtDir + "BridgeLeftV3.png";   // taşsız (taşlar arka planda), ortak tuvalden kesik
    private const string RightPath  = ArtDir + "BridgeRightV3.png";
    private const string SegPath    = ArtDir + "BridgeSegmentV3.png";
    private const string TitlePath  = ArtDir + "BridgeTitleBG.png";
    private const string LogoPath   = ArtDir + "BridgeEventLogo.png";        // LeftEventPanel ikonu
    private const string PopupImgPath = ArtDir + "BridgeRepairPopupImg.png"; // katılım popup görseli
    private const string PlayBtnPath = "Assets/_Project/Art/UI/ScreenImages/Chapter001/NewUI/GreenButonwoStroke.png";
    private const string ClosePath  = "Assets/_Project/Art/UI/BtnClose.png";
    private const string CheckPath  = "Assets/_Project/Art/Icons/CheckMarkIcon.png";
    private const string CoinPath   = "Assets/_Project/Art/UI/GoldMoney.png";
    private const string ChestPath  = "Assets/_Project/Resources/UI/RewardChestClosed.png";
    private const string RibbonPath = "Assets/_Project/Art/UI/RibbonV2.png";
    private const string GoldGlowMatPath = "Assets/_Project/Fonts/Materials/Inter_ExtraBold_GoldOutlineGlow.mat";
    private const string RewardStylePath = "Assets/_Project/Resources/RewardTextStyle.asset";
    private const string SystemName = "BridgeRepairEventSystem";
    private const string IconName   = "BridgeRepairEventIcon";

    private static readonly string[] CharacterIds = { "Bear", "Pig", "Rabbit", "Ram", "Monkey" };

    // BridgeRepairBG (941x1672) üzerinde ölçülen çıkıntı ön-üst kenar ortaları (sol-üst köşeden piksel).
    private static readonly Vector2[] LeftLedges =
        { new(110, 396), new(110, 602), new(110, 810), new(115, 1016), new(115, 1231) };
    private static readonly Vector2[] RightLedges =
        { new(832, 397), new(828, 604), new(836, 812), new(838, 1017), new(838, 1232) };
    // Podyum sandıklarının üstü (1., 2., 3.).
    private static readonly Vector2[] PodiumSpots = { new(475, 1318), new(245, 1380), new(705, 1380) };
    private static readonly Color Sky = new Color(11 / 255f, 137 / 255f, 246 / 255f, 1f);

    [MenuItem("TinyFixers/Mockup/Bridge Repair Event")]
    public static void Setup()
    {
        foreach (var p in new[] { BgPath, LeftPath, RightPath, SegPath, TitlePath, LogoPath, PopupImgPath, RibbonPath, PlayBtnPath, ClosePath, CheckPath, CoinPath, ChestPath })
            EnsureSpriteImport(p);
        foreach (var id in CharacterIds)
            foreach (var suffix in new[] { "Walk_0", "Walk_1", "Bend_0", "Bend_1", "Bend_2" })
                EnsureSpriteImport($"{CharDir}{id}_{suffix}.png");

        var config = EnsureConfig();
        var theme = MockupUI.EnsureTheme();
        var rewardStyle = AssetDatabase.LoadAssetAtPath<RewardTextStyle>(RewardStylePath);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Bridge Repair Setup", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }
        var root = canvas.rootCanvas.transform;

        DestroyChildByName(root, SystemName);
        DestroyChildByName(root, IconName);

        var system = MockupUI.NewRect(SystemName, root);
        MockupUI.Stretch(system);

        var map = BuildMapScreen(system, theme, rewardStyle, out var mapRoot);
        var leftPanel = FindChildByName(root, "LeftEventPanel");
        var icon = BuildEventIcon(leftPanel != null ? leftPanel : system, theme);

        var ctrlGo = MockupUI.NewRect("BridgeRepairController", system);
        var ctrl = ctrlGo.gameObject.AddComponent<BridgeRepairController>();
        MockupUI.SetRef(ctrl, "config", config);
        MockupUI.SetRef(ctrl, "eventButton", icon);
        MockupUI.SetRef(ctrl, "mapScreen", map);
        MockupUI.SetRef(ctrl, "overlayParent", system);
        MockupUI.SetRef(ctrl, "joinPopup", BuildJoinPopupFromSafari(root, system));
        MockupUI.SetRef(ctrl, "continueButton", MockupUI.LoadSprite(PlayBtnPath));
        MockupUI.SetRef(ctrl, "checkMark", MockupUI.LoadSprite(CheckPath));
        MockupUI.SetRef(ctrl, "coin", MockupUI.LoadSprite(CoinPath));
        MockupUI.SetRef(ctrl, "chest", MockupUI.LoadSprite(ChestPath));
        MockupUI.SetRef(ctrl, "victoryRibbon", MockupUI.LoadSprite(RibbonPath));
        MockupUI.SetRef(ctrl, "victoryLabelMaterial", AssetDatabase.LoadAssetAtPath<Material>(GoldGlowMatPath));
        MockupUI.SetRef(icon, "controller", ctrl);

        mapRoot.SetActive(false);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Bridge Repair Setup",
            "Bridge Repair kuruldu (LeftEventPanel ikonu + yarış ekranı + config).\n" +
            "Test: BridgeRepairConfig > debugForceAvailable (yalnız editör) veya TinyFixers ▸ Debug ▸ Bridge Repair.\n" +
            "Köprü anchor'ları: BridgeRepairMapScreen/Board/Anchors altında sürüklenebilir.\n" +
            "Sahneyi kaydet (Cmd+S).", "Tamam");
    }

    // ── Yarış ekranı ─────────────────────────────────────────────

    private static BridgeRepairMapScreen BuildMapScreen(Transform parent, UITheme theme, RewardTextStyle style,
        out GameObject mapRoot)
    {
        var rootRt = MockupUI.NewRect("BridgeRepairMapScreen", parent);
        MockupUI.Stretch(rootRt);
        mapRoot = rootRt.gameObject;
        var map = mapRoot.AddComponent<BridgeRepairMapScreen>();

        var sky = MockupUI.NewImage("SkyFill", rootRt, Sky);
        MockupUI.Stretch(sky.rectTransform);
        sky.raycastTarget = true;   // arkadaki ana menüye tık geçmesin

        // Board: boyut = arka plan pikseli; ekrana BridgeRepairMapScreen.FitBoard ölçekler (alt hizalı).
        var bgSprite = MockupUI.LoadSprite(BgPath);
        Vector2 bgSize = bgSprite != null ? bgSprite.rect.size : new Vector2(941, 1672);
        var boardImg = MockupUI.NewImage("Board", rootRt, Color.white);
        boardImg.sprite = bgSprite;
        boardImg.raycastTarget = false;
        var board = boardImg.rectTransform;
        board.anchorMin = board.anchorMax = new Vector2(0.5f, 0f);
        board.pivot = new Vector2(0.5f, 0f);
        board.anchoredPosition = Vector2.zero;
        board.sizeDelta = bgSize;

        var anchorsRoot = MockupUI.NewRect("Anchors", board);
        MockupUI.Stretch(anchorsRoot);
        var lefts = new Object[LeftLedges.Length];
        var rights = new Object[RightLedges.Length];
        for (int i = 0; i < LeftLedges.Length; i++)
        {
            lefts[i] = MakePixelAnchor($"LeftLedge{i}", anchorsRoot, LeftLedges[i], bgSize);
            rights[i] = MakePixelAnchor($"RightLedge{i}", anchorsRoot, RightLedges[i], bgSize);
        }
        var podium = new Object[PodiumSpots.Length];
        for (int i = 0; i < PodiumSpots.Length; i++)
            podium[i] = MakePixelAnchor($"PodiumSlot{i + 1}", anchorsRoot, PodiumSpots[i], bgSize);

        var lanes = MockupUI.NewRect("Lanes", board);
        MockupUI.Stretch(lanes);
        var tags = MockupUI.NewRect("Tags", board);
        MockupUI.Stretch(tags);
        var podiumLayer = MockupUI.NewRect("Podium", board);
        MockupUI.Stretch(podiumLayer);

        // TopHUD: tabela (BridgeTitleBG) + başlık + alt yazı; altında süre.
        var hud = MockupUI.NewRect("TopHud", rootRt);
        MockupUI.AnchorTop(hud, 430, 40);
        var titleSprite = MockupUI.LoadSprite(TitlePath);
        var frame = MockupUI.NewImage("Frame", hud, Color.white);
        frame.sprite = titleSprite;
        frame.preserveAspect = true;
        frame.raycastTarget = false;
        var frt = frame.rectTransform;
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 1f);
        frt.pivot = new Vector2(0.5f, 1f);
        frt.anchoredPosition = Vector2.zero;
        frt.sizeDelta = new Vector2(836, 836 * 110f / 266f);   // %10 büyük (kullanıcı)

        var title = HudText("Title", frt, "BRIDGE REPAIR", 62, new Vector2(560, 92), new Vector2(0, 36), style, theme);   // kenar detayı görünsün
        title.gameObject.AddComponent<TMPArcText>().arcDegrees = 15f;   // yukarı kavis (kubbe)
        var subtitle = HudText("Subtitle", frt, "15 seviye kazan, ödülleri kap!", 32, new Vector2(560, 52),
            new Vector2(0, -42), style, theme);
        subtitle.color = new Color(1f, 0.92f, 0.62f);

        var timerPill = MockupUI.NewImage("TimerPill", hud, new Color(0.05f, 0.12f, 0.25f, 0.8f));
        timerPill.sprite = null;
        timerPill.raycastTarget = false;
        var tprt = timerPill.rectTransform;
        tprt.anchorMin = tprt.anchorMax = new Vector2(0.5f, 1f);
        tprt.pivot = new Vector2(0.5f, 1f);
        tprt.anchoredPosition = new Vector2(0, -frt.sizeDelta.y - 6);
        tprt.sizeDelta = new Vector2(300, 64);
        var timer = HudText("Timer", tprt, "23:59:59", 40, new Vector2(280, 60), Vector2.zero, style, theme);

        // Alt yazılar açık renkli podyumun üstüne düşüyor → koyu mavi hap zemin + düz (bold olmayan) beyaz yazı.
        var status = BottomLabel("Status", rootRt, "", 36, new Vector2(560, 64), 250, theme);

        // Buton yok: tüm ekran dokunma alanı (SkyFill; Board raycast almaz → dokunuş buraya düşer).
        var tapArea = sky.gameObject.AddComponent<Button>();
        tapArea.transition = Selectable.Transition.None;
        tapArea.targetGraphic = sky;
        // Yanıp sönen yazı: zeminsiz (kullanıcı) — okunurluk için beyaz yazı + koyu mavi kontur.
        var cont = BottomLabel("ContinueText", rootRt, "Devam etmek için dokun", 46, new Vector2(760, 80), 150, theme);
        var contPill = cont.transform.parent.GetComponent<Image>();
        contPill.color = new Color(0f, 0f, 0f, 0f);
        cont.color = new Color32(18, 45, 120, 255);   // lacivert (kullanıcı)
        cont.outlineColor = Color.white;
        cont.outlineWidth = 0.18f;

        var close = MockupUI.NewImage("CloseButton", rootRt, Color.white);
        close.sprite = MockupUI.LoadSprite(ClosePath);
        close.preserveAspect = true;
        var crt = close.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-30, -60);
        crt.sizeDelta = new Vector2(110, 110);
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = close;

        MockupUI.SetRef(map, "root", mapRoot);
        MockupUI.SetRef(map, "viewport", rootRt);
        MockupUI.SetRef(map, "board", board);
        MockupUI.SetRef(map, "boardImage", boardImg);
        MockupUI.SetRef(map, "lanesRoot", lanes);
        MockupUI.SetRef(map, "tagsLayer", tags);
        MockupUI.SetRef(map, "podiumLayer", podiumLayer);
        MockupUI.SetRefArray(map, "leftAnchors", lefts);
        MockupUI.SetRefArray(map, "rightAnchors", rights);
        MockupUI.SetRefArray(map, "podiumSlots", podium);
        MockupUI.SetRef(map, "bridgeLeftSprite", MockupUI.LoadSprite(LeftPath));
        MockupUI.SetRef(map, "bridgeRightSprite", MockupUI.LoadSprite(RightPath));
        MockupUI.SetRef(map, "bridgeSegmentSprite", MockupUI.LoadSprite(SegPath));
        MockupUI.SetRef(map, "titleText", title);
        MockupUI.SetRef(map, "subtitleText", subtitle);
        MockupUI.SetRef(map, "timerText", timer);
        MockupUI.SetRef(map, "statusText", status);
        MockupUI.SetRef(map, "tapArea", tapArea);
        MockupUI.SetRef(map, "continueText", cont);
        MockupUI.SetRef(map, "continueRoot", cont.transform.parent.gameObject);
        MockupUI.SetRef(map, "closeButton", closeBtn);
        return map;
    }

    // ── Katılım popup'ı: Safari popup'ının kopyası ───────────────

    // Sahnedeki SafariJoinPopup'ı kopyalar (panel/başlık/buton/X görünümü birebir), Safari bileşenini söker,
    // ortadaki OverlayImage'a BridgeRepairPopupImg koyar; Body/PrizeHeadline gizlenir.
    private static BridgeRepairJoinPopup BuildJoinPopupFromSafari(Transform canvasRoot, Transform system)
    {
        var safari = FindChildByName(canvasRoot, "SafariJoinPopup");
        if (safari == null)
        {
            Debug.LogError("[BridgeRepairSetup] Sahnede SafariJoinPopup yok — Bridge popup'ı kopyalanamadı.");
            return null;
        }

        var go = Object.Instantiate(safari.gameObject, system, false);
        go.name = "BridgeRepairJoinPopup";
        var safariCtrl = go.GetComponent<SafariJoinPopupController>();
        if (safariCtrl != null) Object.DestroyImmediate(safariCtrl);
        var popup = go.AddComponent<BridgeRepairJoinPopup>();

        var panel = FindChildByName(go.transform, "PopupPanel") as RectTransform;
        var title = FindChildByName(go.transform, "Title")?.GetComponent<TMP_Text>();
        var overlay = FindChildByName(go.transform, "OverlayImage")?.GetComponent<Image>();
        var cont = FindChildByName(go.transform, "ContinueButton")?.GetComponent<Button>();
        var contLabel = cont != null ? cont.GetComponentInChildren<TMP_Text>(true) : null;
        var cancel = FindChildByName(go.transform, "CancelButton")?.GetComponent<Button>();
        foreach (var hideName in new[] { "Body", "PrizeHeadline" })
        {
            var t = FindChildByName(go.transform, hideName);
            if (t != null) t.gameObject.SetActive(false);
        }

        if (title != null)
        {
            title.text = "BRIDGE REPAIR";
            // "SAFARİ"den uzun → ekrana sığsın diye küçült + kutuya otomatik sığdır.
            title.enableAutoSizing = true;
            title.fontSizeMax = title.fontSize * 0.72f;
            title.fontSizeMin = title.fontSizeMax * 0.5f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
        }
        if (overlay != null)
        {
            overlay.sprite = MockupUI.LoadSprite(PopupImgPath);
            overlay.preserveAspect = true;
            overlay.gameObject.SetActive(true);
        }
        if (contLabel != null) contLabel.text = "KATIL";

        MockupUI.SetRef(popup, "root", go);
        MockupUI.SetRef(popup, "popupRoot", panel);
        MockupUI.SetRef(popup, "titleText", title);
        MockupUI.SetRef(popup, "overlayImage", overlay);
        MockupUI.SetRef(popup, "continueButton", cont);
        MockupUI.SetRef(popup, "continueLabel", contLabel);
        MockupUI.SetRef(popup, "cancelButton", cancel);
        go.SetActive(false);
        return popup;
    }

    private static TMP_Text BottomLabel(string name, RectTransform parent, string value, float size, Vector2 box,
        float y, UITheme theme)
    {
        var pill = MockupUI.NewImage(name + "Pill", parent, new Color(0.04f, 0.16f, 0.42f, 0.88f));
        pill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        pill.type = Image.Type.Sliced;
        pill.pixelsPerUnitMultiplier = 0.35f;   // daha yuvarlak köşe
        pill.raycastTarget = false;
        var prt = pill.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(0, y);
        prt.sizeDelta = box;

        var text = MockupUI.NewText(name, prt, value, size, Color.white, TextAlignmentOptions.Center,
            theme.bodyFont != null ? theme.bodyFont : theme.headingFont);
        text.fontStyle = FontStyles.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMax = size;
        text.fontSizeMin = size * 0.55f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        MockupUI.Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(16, 4);
        text.rectTransform.offsetMax = new Vector2(-16, -4);
        return text;
    }

    private static TMP_Text HudText(string name, RectTransform parent, string value, float size, Vector2 box,
        Vector2 pos, RewardTextStyle style, UITheme theme)
    {
        var text = MockupUI.NewText(name, parent, value, size, Color.white, TextAlignmentOptions.Center,
            style != null && style.font != null ? style.font : theme.headingFont);
        if (style != null && style.material != null) text.fontSharedMaterial = style.material;
        text.enableAutoSizing = true;
        text.fontSizeMax = size;
        text.fontSizeMin = size * 0.55f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        return text;
    }

    // Sol-üst köşe piksel koordinatını Board içinde (0,1) anchor'lı noktaya çevirir → sürüklenebilir.
    private static RectTransform MakePixelAnchor(string name, RectTransform parent, Vector2 px, Vector2 bgSize)
    {
        var rt = MockupUI.NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(16, 16);
        rt.anchoredPosition = new Vector2(px.x, -px.y);
        return rt;
    }

    // ── İkon (LeftEventPanel) ────────────────────────────────────

    private static BridgeRepairEventButton BuildEventIcon(Transform parent, UITheme theme)
    {
        var icon = MockupUI.NewImage(IconName, parent, Color.white);
        icon.sprite = MockupUI.LoadSprite(LogoPath);
        icon.preserveAspect = true;
        var rt = icon.rectTransform;
        rt.sizeDelta = new Vector2(150, 150);
        if (parent == null || parent.name != "LeftEventPanel")
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(24, -80);
        }

        var btn = icon.gameObject.AddComponent<Button>();
        btn.targetGraphic = icon;
        icon.gameObject.AddComponent<EventIconAnimator>();

        // Geri sayım logonun alttaki lacivert kutusuna yazılır (logo 512x505; kutu ≈ x %17-83, y %7-22).
        var label = MockupUI.NewText("Label", rt, "KÖPRÜ", 22, Color.white, TextAlignmentOptions.Center, theme.headingFont);
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12;
        label.fontSizeMax = 22;
        label.raycastTarget = false;
        MockupUI.AnchorBox(label.rectTransform, new Vector2(0.18f, 0.075f), new Vector2(0.82f, 0.215f));

        var badge = MockupUI.NewImage("ProgressBadge", rt, new Color(0.85f, 0.2f, 0.15f, 1f));
        badge.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        badge.type = Image.Type.Sliced;
        badge.raycastTarget = false;
        var brt = badge.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(1f, 1f);
        brt.anchoredPosition = new Vector2(6, 6);
        brt.sizeDelta = new Vector2(76, 40);
        var progress = MockupUI.NewText("Text", brt, "0/15", 24, Color.white, TextAlignmentOptions.Center, theme.headingFont);
        progress.fontStyle = FontStyles.Bold;
        MockupUI.Stretch(progress.rectTransform);

        var comp = icon.gameObject.AddComponent<BridgeRepairEventButton>();
        MockupUI.SetRef(comp, "button", btn);
        MockupUI.SetRef(comp, "labelText", label);
        MockupUI.SetRef(comp, "progressText", progress);
        MockupUI.SetRef(comp, "progressBadge", badge.gameObject);
        MockupUI.SetRef(comp, "visibilityRoot", icon.gameObject);
        icon.gameObject.SetActive(false);   // controller görünürlüğü yönetir
        return comp;
    }

    // ── Config ───────────────────────────────────────────────────

    private static BridgeRepairConfig EnsureConfig()
    {
        MockupUI.EnsureFolder("Assets/_Project/Resources/Events");
        var config = AssetDatabase.LoadAssetAtPath<BridgeRepairConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<BridgeRepairConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        // Karakter kareleri her kurulumda yenilenir (yeni kare eklenirse otomatik gelir).
        var list = new List<BridgeCharacterDef>();
        foreach (var id in CharacterIds)
        {
            list.Add(new BridgeCharacterDef
            {
                id = id.ToLowerInvariant(),
                walk = LoadFrames(id, "Walk", 2),
                bend = LoadFrames(id, "Bend", 3),
            });
        }
        config.characters = list;
        EditorUtility.SetDirty(config);
        return config;
    }

    private static Sprite[] LoadFrames(string id, string kind, int count)
    {
        var frames = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            var s = MockupUI.LoadSprite($"{CharDir}{id}_{kind}_{i}.png");
            if (s != null) frames.Add(s);
            else Debug.LogWarning($"[BridgeRepairSetup] Kare yok: {CharDir}{id}_{kind}_{i}.png");
        }
        return frames.ToArray();
    }

    // ── Yardımcılar ──────────────────────────────────────────────

    private static void EnsureSpriteImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
        if (dirty) importer.SaveAndReimport();
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }

    private static void DestroyChildByName(Transform root, string childName)
    {
        var child = FindChildByName(root, childName);
        while (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
            child = FindChildByName(root, childName);
        }
    }
}
