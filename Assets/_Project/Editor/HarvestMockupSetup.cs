using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bostan Hasadı kazı ekranını tek tıkla kurar — Menü: TinyFixers ▸ Mockup ▸ Harvest Event (MainMenu açıkken).
///  - Config (Resources/Events/HarvestConfig.asset): 8 ürün, 4 kat (yerleşim + ödül), görseller, yatak köşeleri.
///    Var olan config'in kat/ödül ayarları KORUNUR; yalnız görsel referanslar ve eksik alanlar doldurulur.
///  - Ekran: gök dolgusu + Board (arka plan pikseli uzayı) + kare/ürün katmanları + tezgâh noktaları + ayı +
///    Bridge'in üst tabelası (başlık + kat) + alt tabelada kürek sayacı / Oyna + kapatma.
///  - LeftEventPanel'e Harvest logosu (şimdilik takvimsiz: ekranı açar).
/// Tekrar çalıştırılabilir. Tezgâh noktaları Board/StandSlots altında sürüklenerek ince ayarlanır.
/// </summary>
public static class HarvestMockupSetup
{
    private const string ArtDir     = "Assets/_Project/Art/UI/MainScreenEvents/Harvest/";
    private const string ConfigPath = "Assets/_Project/Resources/Events/HarvestConfig.asset";
    private const string BgPath     = ArtDir + "Harvest_SceneBG.jpg";
    private const string TitlePath  = "Assets/_Project/Art/UI/MainScreenEvents/BridgeRepair/BridgeTitleBG.png";
    private const string PlayBtnPath = "Assets/_Project/Art/UI/ScreenImages/Chapter001/NewUI/GreenButonwoStroke.png";
    private const string ClosePath  = "Assets/_Project/Art/UI/BtnClose.png";
    private const string RewardStylePath = "Assets/_Project/Resources/RewardTextStyle.asset";
    private const string SystemName = "HarvestEventSystem";
    private const string IconName   = "HarvestEventIcon";

    // Harvest_SceneBG (1152x2064) üzerinde ölçüldü (sol-üst köşeden piksel).
    private static readonly Vector2 BgRef = new(1152, 2064);
    private static readonly Vector2[] StandSpots = { new(800, 612), new(866, 632), new(932, 652), new(1000, 680) };
    private static readonly Vector2 BearFeet = new(235, 1530);
    private static readonly Color Sky = new Color(0.33f, 0.66f, 0.96f, 1f);

    private static readonly (string id, string file, Vector2Int size)[] Crops =
    {
        ("carrot", "Harvest_Carrot.png", new Vector2Int(1, 2)),
        ("potato", "Harvest_Potato.png", new Vector2Int(1, 1)),
        ("radish", "Harvest_Radish.png", new Vector2Int(1, 1)),
        ("corn", "Harvest_Corn.png", new Vector2Int(1, 3)),
        ("watermelon", "Harvest_Watermelon.png", new Vector2Int(2, 1)),
        ("pumpkin", "Harvest_Pumpkin.png", new Vector2Int(2, 2)),
        ("eggplant", "Harvest_Eggplant.png", new Vector2Int(2, 1)),
        ("strawberry", "Harvest_Strawberry.png", new Vector2Int(1, 1)),
    };

    [MenuItem("TinyFixers/Mockup/Harvest Event")]
    public static void Setup()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir.TrimEnd('/') }))
            EnsureSpriteImport(AssetDatabase.GUIDToAssetPath(guid));
        foreach (var p in new[] { TitlePath, PlayBtnPath, ClosePath }) EnsureSpriteImport(p);

        var config = EnsureConfig();
        var theme = MockupUI.EnsureTheme();
        var style = AssetDatabase.LoadAssetAtPath<RewardTextStyle>(RewardStylePath);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Harvest Setup", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }
        var root = canvas.rootCanvas.transform;
        DestroyChildByName(root, SystemName);
        DestroyChildByName(root, IconName);
        DestroyChildByName(root, IconName + "Controller");

        var system = MockupUI.NewRect(SystemName, root);
        MockupUI.Stretch(system);
        var screen = BuildScreen(system, config, theme, style, out var screenRoot);
        HarvestTitleStyle.ApplyLastChoice(screen);   // iki satırlı tabela başlığı (Baloo 2 / DynaPuff)

        var leftPanel = FindChildByName(root, "LeftEventPanel");
        BuildEventIcon(leftPanel != null ? leftPanel : system, screen);

        screenRoot.SetActive(false);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Harvest Setup",
            "Bostan Hasadı kuruldu (kazı ekranı + config + LeftEventPanel ikonu).\n" +
            "Test (Play): HarvestConfig > debugForceAvailable aç (yalnız editör), TinyFixers ▸ Debug ▸ Harvest ▸ +10 Kürek,\n" +
            "sonra sol paneldeki ikona bas.\n" +
            "Tezgâh noktaları: HarvestScreen/Board/StandSlots altında sürüklenebilir.\n" +
            "Sahneyi kaydet (Cmd+S).", "Tamam");
    }

    // ── Ekran ────────────────────────────────────────────────────

    private static HarvestScreen BuildScreen(Transform parent, HarvestConfig cfg, UITheme theme, RewardTextStyle style,
        out GameObject screenRoot)
    {
        var rootRt = MockupUI.NewRect("HarvestScreen", parent);
        MockupUI.Stretch(rootRt);
        screenRoot = rootRt.gameObject;
        var screen = screenRoot.AddComponent<HarvestScreen>();

        // Tam ekran dolgu: arkadaki ana menüye tık geçmez + dokunuşlar HarvestScreen'e (IPointerClickHandler) çıkar.
        var sky = MockupUI.NewImage("SkyFill", rootRt, Sky);
        MockupUI.Stretch(sky.rectTransform);
        sky.raycastTarget = true;

        var bgSprite = MockupUI.LoadSprite(BgPath);
        Vector2 bgSize = bgSprite != null ? bgSprite.rect.size : BgRef;
        Vector2 k = new(bgSize.x / BgRef.x, bgSize.y / BgRef.y);
        var boardImg = MockupUI.NewImage("Board", rootRt, Color.white);
        boardImg.sprite = bgSprite;
        boardImg.raycastTarget = false;
        var board = boardImg.rectTransform;
        board.anchorMin = board.anchorMax = new Vector2(0.5f, 0f);
        board.pivot = new Vector2(0.5f, 0f);
        board.anchoredPosition = Vector2.zero;
        board.sizeDelta = bgSize;

        var tiles = MockupUI.NewRect("Tiles", board);
        MockupUI.Stretch(tiles);
        var crops = MockupUI.NewRect("Crops", board);
        MockupUI.Stretch(crops);

        var slotsRoot = MockupUI.NewRect("StandSlots", board);
        MockupUI.Stretch(slotsRoot);
        var slots = new Object[StandSpots.Length];
        for (int i = 0; i < StandSpots.Length; i++)
            slots[i] = PixelAnchor($"Slot{i}", slotsRoot, Vector2.Scale(StandSpots[i], k));

        // Ayı: yatağın önündeki toprak alanda, ayakları yere basar.
        var bear = MockupUI.NewImage("Bear", board, Color.white);
        bear.sprite = cfg.bearIdle;
        bear.preserveAspect = true;
        bear.raycastTarget = false;
        var brt = bear.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0.5f, 0.06f);
        brt.sizeDelta = new Vector2(420, 420);
        var feet = Vector2.Scale(BearFeet, k);
        brt.anchoredPosition = new Vector2(feet.x, -feet.y);

        // Alt tabela (arka planda çizili): kürek ikonu + sayaç; kürek yoksa ipucu + Oyna.
        var trowel = MockupUI.NewImage("TrowelIcon", board, Color.white);
        trowel.sprite = cfg.trowelIcon;
        trowel.preserveAspect = true;
        trowel.raycastTarget = false;
        PlaceInBoard(trowel.rectTransform, Vector2.Scale(new Vector2(250, 1855), k), new Vector2(150, 150));
        var trowelText = HudText("TrowelCount", board, "x0", 78, new Vector2(240, 110), style, theme);
        PlaceInBoard(trowelText.rectTransform, Vector2.Scale(new Vector2(420, 1855), k), new Vector2(240, 110));

        var play = MockupUI.GlossyButton(board, PlayBtnPath, new Color(0.3f, 0.75f, 0.2f), "OYNA", 54,
            style != null && style.font != null ? style.font : theme.headingFont, out var playLabel);
        if (style != null && style.material != null) playLabel.fontSharedMaterial = style.material;
        PlaceInBoard((RectTransform)play.transform, Vector2.Scale(new Vector2(820, 1855), k), new Vector2(330, 130));

        var hint = HudText("Hint", board, "Seviye geç, kürek kazan!", 46, new Vector2(900, 80), style, theme);
        PlaceInBoard(hint.rectTransform, Vector2.Scale(new Vector2(576, 1690), k), new Vector2(900, 80));

        // Üst tabela: Bridge'in başlık çerçevesi + başlık + kat göstergesi.
        var hud = MockupUI.NewRect("TopHud", rootRt);
        MockupUI.AnchorTop(hud, 520, 30);
        var frame = MockupUI.NewImage("Frame", hud, Color.white);
        frame.sprite = MockupUI.LoadSprite(TitlePath);
        frame.preserveAspect = true;
        frame.raycastTarget = false;
        var frt = frame.rectTransform;
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 1f);
        frt.pivot = new Vector2(0.5f, 1f);
        frt.anchoredPosition = Vector2.zero;
        frt.sizeDelta = new Vector2(1000, 1000 * 110f / 266f);   // Bridge'den büyük (kullanıcı)
        // "GARDEN HARVEST" uzun → kutu çerçevenin kenar süslerine taşmasın; autosize küçültür.
        // Kavis (TMPArcText) harfleri autosize'dan SONRA büker → uzun başlık yanlardan taşmasın diye boyut düşük.
        var title = HudText("Title", frt, "BOSTAN HASADI", 58, new Vector2(680, 110), style, theme);
        title.rectTransform.anchoredPosition = new Vector2(0, 44);
        title.gameObject.AddComponent<TMPArcText>().arcDegrees = 15f;
        var floor = HudText("Floor", frt, "Hasat 1/4", 42, new Vector2(600, 60), style, theme);
        floor.rectTransform.anchoredPosition = new Vector2(0, -50);
        floor.color = new Color(1f, 0.92f, 0.62f);

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

        // "?" — Nasıl oynanır (sol üst, kapatma düğmesinin simetriği).
        var help = MockupUI.NewImage("HelpButton", rootRt, new Color(0.18f, 0.55f, 0.22f, 1f));
        help.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var hrt = help.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f);
        hrt.pivot = new Vector2(0f, 1f);
        hrt.anchoredPosition = new Vector2(30, -60);
        hrt.sizeDelta = new Vector2(100, 100);
        var helpBtn = help.gameObject.AddComponent<Button>();
        helpBtn.targetGraphic = help;
        var q = HudText("Label", hrt, "?", 64, new Vector2(100, 100), style, theme);
        q.rectTransform.anchoredPosition = Vector2.zero;
        var outline = help.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.92f, 0.62f, 1f);
        outline.effectDistance = new Vector2(4, -4);

        MockupUI.SetRef(screen, "root", screenRoot);
        MockupUI.SetRef(screen, "viewport", rootRt);
        MockupUI.SetRef(screen, "board", board);
        MockupUI.SetRef(screen, "tilesLayer", tiles);
        MockupUI.SetRef(screen, "cropsLayer", crops);
        MockupUI.SetRefArray(screen, "standSlots", slots);
        MockupUI.SetRef(screen, "bear", bear);
        MockupUI.SetRef(screen, "topHud", hud);
        MockupUI.SetRef(screen, "titleText", title);
        MockupUI.SetRef(screen, "floorText", floor);
        MockupUI.SetRef(screen, "trowelText", trowelText);
        MockupUI.SetRef(screen, "hintText", hint);
        MockupUI.SetRef(screen, "playButton", play);
        MockupUI.SetRef(screen, "closeButton", closeBtn);
        MockupUI.SetRef(screen, "helpButton", helpBtn);
        return screen;
    }

    // ── İkon (LeftEventPanel) ────────────────────────────────────

    private static void BuildEventIcon(Transform parent, HarvestScreen screen)
    {
        var icon = MockupUI.NewImage(IconName, parent, Color.white);
        icon.sprite = MockupUI.LoadSprite(ArtDir + "Harvest_Logo.png");
        icon.preserveAspect = true;
        var rt = icon.rectTransform;
        rt.sizeDelta = new Vector2(150, 150);
        if (parent == null || parent.name != "LeftEventPanel")
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(24, 100);
        }
        var btn = icon.gameObject.AddComponent<Button>();
        btn.targetGraphic = icon;
        icon.gameObject.AddComponent<EventIconAnimator>();

        // Geri sayım logonun alttaki teal bandına (logo 512x512; band ≈ x %12-88, y %8-24).
        var theme = MockupUI.EnsureTheme();
        var label = MockupUI.NewText("Label", rt, "HASAT", 22, Color.white, TextAlignmentOptions.Center, theme.headingFont);
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12;
        label.fontSizeMax = 22;
        label.raycastTarget = false;
        MockupUI.AnchorBox(label.rectTransform, new Vector2(0.14f, 0.09f), new Vector2(0.86f, 0.23f));

        // Rozet: kullanılabilir kürek sayısı (kürek varsa).
        var badge = MockupUI.NewImage("TrowelBadge", rt, new Color(0.85f, 0.2f, 0.15f, 1f));
        badge.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        badge.type = Image.Type.Sliced;
        badge.raycastTarget = false;
        var brt = badge.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(1f, 1f);
        brt.anchoredPosition = new Vector2(6, 6);
        brt.sizeDelta = new Vector2(56, 44);
        var count = MockupUI.NewText("Text", brt, "0", 26, Color.white, TextAlignmentOptions.Center, theme.headingFont);
        count.fontStyle = FontStyles.Bold;
        MockupUI.Stretch(count.rectTransform);

        // Bileşen ikonun DIŞINDA bir taşıyıcıda: ikon gizliyken de takvimi izleyip ikonu açabilsin.
        var host = MockupUI.NewRect(IconName + "Controller", parent);
        host.sizeDelta = Vector2.zero;
        var le = host.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
        var comp = host.gameObject.AddComponent<HarvestEventButton>();
        MockupUI.SetRef(comp, "screen", screen);
        MockupUI.SetRef(comp, "button", btn);
        MockupUI.SetRef(comp, "labelText", label);
        MockupUI.SetRef(comp, "badgeText", count);
        MockupUI.SetRef(comp, "badge", badge.gameObject);
        MockupUI.SetRef(comp, "visibilityRoot", icon.gameObject);
        icon.gameObject.SetActive(false);   // takvim/kapı açınca bileşen gösterir
    }

    // ── Config ───────────────────────────────────────────────────

    private static HarvestConfig EnsureConfig()
    {
        MockupUI.EnsureFolder("Assets/_Project/Resources/Events");
        var config = AssetDatabase.LoadAssetAtPath<HarvestConfig>(ConfigPath);
        bool fresh = config == null;
        if (fresh)
        {
            config = ScriptableObject.CreateInstance<HarvestConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        // Görsel referanslar her kurulumda yenilenir.
        var crops = new List<HarvestCropDef>();
        foreach (var (id, file, size) in Crops)
            crops.Add(new HarvestCropDef { id = id, sprite = MockupUI.LoadSprite(ArtDir + file), size = size });
        config.crops = crops.ToArray();
        config.sceneBackground = MockupUI.LoadSprite(BgPath);
        config.soilTiles = new[]
        {
            MockupUI.LoadSprite(ArtDir + "Harvest_IsoSoil_A.png"),
            MockupUI.LoadSprite(ArtDir + "Harvest_IsoSoil_B.png"),
            MockupUI.LoadSprite(ArtDir + "Harvest_IsoSoil_C.png"),
        };
        config.dugTile = MockupUI.LoadSprite(ArtDir + "Harvest_IsoSoil_Dug.png");
        config.hintGlow = MockupUI.LoadSprite("Assets/_Project/Art/Icons/PulseCoreEffectsIcon/soft_circle.png");
        config.bearIdle = MockupUI.LoadSprite(ArtDir + "Harvest_Bear_Idle.png");
        config.bearDig = MockupUI.LoadSprite(ArtDir + "Harvest_Bear_Dig.png");
        config.bearCheer = MockupUI.LoadSprite(ArtDir + "Harvest_Bear_Cheer.png");
        config.trowelIcon = MockupUI.LoadSprite(ArtDir + "Harvest_Trowel.png");
        if (config.wonderCatalog == null)
            config.wonderCatalog = AssetDatabase.LoadAssetAtPath<WonderCatalog>("Assets/_Project/Settings/Wonders/WonderCatalog.asset");

        // Katlar yalnız ilk kurulumda (sonra Inspector'dan ayarlanır, yeniden kurulum ezmez).
        if (fresh || config.floors == null || config.floors.Length == 0)
            config.floors = DefaultFloors();

        EditorUtility.SetDirty(config);
        return config;
    }

    // Ürün indeksleri: 0 havuç(1x2) 1 patates 2 turp 3 mısır(1x3) 4 karpuz(2x1) 5 kabak(2x2) 6 patlıcan(2x1) 7 çilek.
    private static HarvestFloorDef[] DefaultFloors() => new[]
    {
        Floor(new[] { P(0, 1, 1), P(1, 3, 3), P(2, 0, 4) },
            R(DailySlotRewardType.Coins, 100)),
        Floor(new[] { P(4, 2, 0), P(7, 0, 2), P(0, 4, 2) },
            R(DailySlotRewardType.Booster_Hammer, 1), R(DailySlotRewardType.Coins, 150)),
        Floor(new[] { P(5, 1, 1), P(3, 4, 1), P(1, 0, 4), P(6, 2, 4) },
            R(DailySlotRewardType.Joker_Line, 2), R(DailySlotRewardType.Coins, 200)),
        Floor(new[] { P(5, 3, 3), P(3, 0, 0), P(4, 2, 0), P(6, 1, 4), P(2, 2, 2) },
            R(DailySlotRewardType.Coins, 500), R(DailySlotRewardType.Booster_Hammer, 2), R(DailySlotRewardType.Joker_PulseCore, 1)),
    };

    private static HarvestFloorDef Floor(HarvestPlacement[] p, params DailySlotReward[] r) =>
        new() { placements = p, rewards = r };

    private static HarvestPlacement P(int crop, int u, int v) => new() { crop = crop, origin = new Vector2Int(u, v) };

    private static DailySlotReward R(DailySlotRewardType type, int amount) => new() { type = type, amount = amount };

    // ── Yardımcılar ──────────────────────────────────────────────

    private static void PlaceInBoard(RectTransform rt, Vector2 px, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(px.x, -px.y);
    }

    private static RectTransform PixelAnchor(string name, RectTransform parent, Vector2 px)
    {
        var rt = MockupUI.NewRect(name, parent);
        PlaceInBoard(rt, px, new Vector2(16, 16));
        return rt;
    }

    private static TMP_Text HudText(string name, RectTransform parent, string value, float size, Vector2 box,
        RewardTextStyle style, UITheme theme)
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
        return text;
    }

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
