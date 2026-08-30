using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yükseliş (Rising) eventini tek tıkla kurar — Menü: TinyFixers > Mockup > Rising Event.
/// Safari backend'ini (SafariConfig/State/Schedule/Controller) yeniden kullanır; sunum olarak
/// dikey asansör harita ekranını (RisingMapScreen) + üst HUD'u (RisingTopHud) kurar:
///   - Kule fonu (RisingBG) + 7 kat anchor'ı (alttan üste).
///   - Scissor kaldıraç (ScissorLiftView, Resources/MiniLift parçaları).
///   - Kalabalık yığını (SafariAvatarStackView + helmet çerçeveler).
///   - TopHUD: mor başlık bandı + bej panel + iki mavi kutu (Seviye / Oyuncu).
///
/// Placeholder renkler kullanılır; kalıcı sprite'lar Inspector'dan takılır (mor bant, bej panel,
/// mavi kutu, Seviye/Oyuncu ikonları). Tekrar çalıştırılabilir.
/// </summary>
public static class RisingMockupSetup
{
    private const string ResDir     = "Assets/_Project/Resources/Events";
    private const string ConfigPath = ResDir + "/SafariConfig.asset";
    // KATMANLI yapı (çok-çözünürlük): arka plan + kule + tophud ayrı katmanlar.
    private const string PopupPath  = "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/SafriPopupBG.png";
    private const string GoldPath   = "Assets/_Project/Art/UI/GoldMoney.png";
    private const string IntroGoldPilePath = "Assets/_Project/Art/UI/MarketUI/OnlyGolds.png";
    private const string IconPath   = "Assets/_Project/Art/UI/MainScreenEvents/SafariEventBTNV2.png";
    // Üst şeridin ekran oranı (üstte tophud, altında kule alanı).
    private const float TopHudFrac  = 0.80f;

    // Rising lift parçaları + kutu ikonları (kullanıcı Resources/RisingEvent altına yükledi).
    private const string LiftDir      = "Assets/_Project/Resources/RisingEvent/";
    private const string IntroFlagPath = LiftDir + "RisingIntroFlag.png";
    private const string BasePath     = LiftDir + "RLBA1.png";   // alt tabla
    private const string PlatformPath = LiftDir + "RLU1.png";    // üst tabla
    private const string ArmFrontPath = LiftDir + "RLFL1.png";   // ön makas
    private const string ArmBackPath  = LiftDir + "RLBL1.png";   // arka makas
    private const string BoltPath     = LiftDir + "RLB1.png";    // pim
    private const string LevelIconPath   = LiftDir + "Target.png";   // Seviye ikonu
    private const string PlayersIconPath = LiftDir + "Members.png";  // Oyuncu ikonu
    // Katman görselleri (kullanıcı Resources/RisingEvent altına verdi).
    private const string BackgroundPath  = LiftDir + "RiseBGV2.jpg";       // Katman 1: arka plan (spot ışık)
    private const string TowerPath       = LiftDir + "RisingMainLift.png"; // Katman 2: kule (7 kat + ray)
    private const string TopHudFramePath = LiftDir + "RLTophud.png";       // Katman 3: tophud çerçevesi
    private const string RestLiftPath    = LiftDir + "RisingLiftT2.png";   // Kaldıraç rest (ilk duruş) görseli
    private static readonly string[] HelmetPaths =
    {
        "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/HB1.png",
        "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/HG1.png",
        "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/HR1.png",
        "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/HY1.png"
    };
    private const string BotHelmetPath = "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/HB1.png";
    private const string PlayerHelmetPath = "Assets/_Project/Art/UI/MainScreenEvents/TinySafari/HR1.png";
    private const string SystemName = "RisingEventSystem";
    private const string LegacySystemName = "SafariEventSystem";

    private static readonly Color Yellow = new Color(1f, 0.82f, 0.2f, 1f);

    [MenuItem("TinyFixers/Mockup/Rising Event")]
    public static void Setup()
    {
        MockupUI.EnsureFolder(ResDir);
        var config = EnsureConfig();
        EnsureSpriteImport(PopupPath);
        EnsureSpriteImport(IconPath);
        EnsureSpriteImport(IntroGoldPilePath);
        EnsureSpriteImport(IntroFlagPath);
        foreach (var p in new[] { BasePath, PlatformPath, ArmFrontPath, ArmBackPath, BoltPath,
                                  LevelIconPath, PlayersIconPath, BackgroundPath, TowerPath, TopHudFramePath,
                                  RestLiftPath })
            EnsureSpriteImport(p);
        EnsureHelmetImports();
        var tower = MockupUI.LoadSprite(TowerPath);
        var theme = MockupUI.EnsureTheme();

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Rising Setup", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }
        var root = canvas.rootCanvas.transform;

        // Tekrar çalıştırılabilir: eski Rising'i ve aynı eventin eski Safari sunumunu temizle.
        // Kullanıcının sahnede tasarladığı SafariJoinPopup korunur; Rising controller onu kullanır.
        PreserveExistingJoinPopup(root);
        DestroyChildByName(root, SystemName);
        DestroyChildByName(root, LegacySystemName);
        DestroyChildByName(root, "RisingEventIcon");
        DestroyChildByName(root, "SafariEventIcon");

        var system = MockupUI.NewRect(SystemName, root);
        MockupUI.Stretch(system);

        var mapScreen  = BuildMapScreen(system, theme, tower, config, out var mapRoot);
        var introOverlay = BuildIntroOverlay(system, theme, mapScreen, config, out var introRoot);
        var joinPopup  = FindExistingJoinPopup(root, system, out var popupRoot)
                         ?? BuildJoinPopup(system, theme, out popupRoot);
        var iconParent = FindChildByName(root, "RightEventPanel") ?? system;
        var eventBtn   = BuildEventIcon(iconParent, theme, MockupUI.LoadSprite(IconPath));

        var ctrlGO = MockupUI.NewRect("RisingController", system);
        var ctrl = ctrlGO.gameObject.AddComponent<SafariEventController>();
        MockupUI.SetRef(ctrl, "config", config);
        MockupUI.SetRef(ctrl, "eventButton", eventBtn);
        MockupUI.SetRef(ctrl, "joinPopup", joinPopup);
        MockupUI.SetRef(ctrl, "mapScreen", mapScreen);
        MockupUI.SetRef(ctrl, "risingIntroOverlay", introOverlay);

        MockupUI.SetRef(eventBtn, "controller", ctrl);
        if (iconParent == system)
            AddHomeOnlyElement(eventBtn.gameObject);

        mapRoot.SetActive(false);
        introRoot.SetActive(false);
        popupRoot.SetActive(false);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Rising Setup",
            "Yükseliş kuruldu (ikon + popup + asansör harita + TopHUD).\n" +
            "Kalıcı sprite'ları Inspector'dan tak: mor bant, bej panel, mavi kutu, Seviye/Oyuncu ikonları.\n" +
            "Sahneyi kaydet (Cmd+S).", "Tamam");
    }

    // ── Map screen ───────────────────────────────────────────────

    private static RisingMapScreen BuildMapScreen(Transform parent, UITheme theme, Sprite tower,
                                                  SafariConfig config, out GameObject mapRoot)
    {
        var rootRt = MockupUI.NewRect("RisingMapScreen", parent);
        MockupUI.Stretch(rootRt);
        mapRoot = rootRt.gameObject;
        var map = mapRoot.AddComponent<RisingMapScreen>();

        // KATMAN 1 — Arka plan (RiseBGV2 spot ışık): tam-ekran stretch (crop serbest), tıkı bloklar.
        var backdrop = MockupUI.NewImage("Background", rootRt, Color.white);
        MockupUI.Stretch(backdrop.rectTransform);
        var bgSprite = MockupUI.LoadSprite(BackgroundPath);
        if (bgSprite != null) backdrop.sprite = bgSprite;
        backdrop.preserveAspect = false;   // geniş çizildi → tam ekranı doldur (oran koruma yok)
        backdrop.raycastTarget = true;

        // KATMAN 2 — Kule: SOL bölgede üstten sabitlenir. Bottom alanı yükseltir; üst hizası değişmez.
        // Sağ boşluk kaldıraca kalır. Kat/kalabalık öğeleri bu görselin çocuğu.
        var towerArea = MockupUI.NewRect("TowerArea", rootRt);
        towerArea.anchorMin = new Vector2(0.0f, 0.0f);
        towerArea.anchorMax = new Vector2(0.62f, TopHudFrac);   // SOL bölge → kule sola yaslı; sağ boşluk kaldıraca
        towerArea.offsetMin = new Vector2(0f, 300f);   // Bottom = 300; 1.kat sağdaki lift üst tablasına yaklaşır
        towerArea.offsetMax = Vector2.zero;

        var towerImg = MockupUI.NewImage("Tower", towerArea, Color.white);
        towerImg.rectTransform.anchorMin = towerImg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        towerImg.rectTransform.pivot = new Vector2(0.5f, 1f);
        towerImg.rectTransform.anchoredPosition = Vector2.zero;
        if (tower != null) towerImg.sprite = tower;
        towerImg.preserveAspect = false;
        towerImg.raycastTarget = false;
        var fitter = towerImg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (tower != null && tower.rect.height > 0.01f) ? tower.rect.width / tower.rect.height : 0.36f;
        var board = towerImg.rectTransform;   // kat/kalabalık öğeleri buna bağlanır

        // Kat anchor'ları — RisingMainLift cam kat merkezleri (alttan üste: index0 = 1.kat). Inspector'dan tunelanabilir.
        int n = Mathf.Min(config.pitstopCount, 7);
        var anchors = new Object[n];
        var floorNumbers = new Object[n];
        float yBottom = 0.10f, yTop = 0.88f;
        float numberEdgeX = 0.30f;   // kule kenarındaki numara sütunu (x) — Inspector'dan tunelanır
        for (int i = 0; i < n; i++)
        {
            float f = n <= 1 ? yBottom : Mathf.Lerp(yBottom, yTop, i / (float)(n - 1));
            anchors[i] = MakeAnchor($"Floor{i + 1}", board, new Vector2(0.55f, f));

            // Kule kenarı kat numarası (1..N). Renk runtime'da RisingMapScreen'den: geçilen sarı / kalan beyaz.
            var num = MockupUI.NewText($"FloorNum{i + 1}", board, (i + 1).ToString(), 40f,
                Color.white, TextAlignmentOptions.Center, theme.headingFont);
            num.fontStyle = FontStyles.Bold;
            num.raycastTarget = false;
            var numRt = num.rectTransform;
            numRt.anchorMin = numRt.anchorMax = new Vector2(numberEdgeX, f);
            numRt.pivot = new Vector2(0.5f, 0.5f);
            numRt.anchoredPosition = Vector2.zero;
            numRt.sizeDelta = new Vector2(70f, 70f);
            floorNumbers[i] = num;
        }

        // Kaldıraç (scissor lift) — kulenin SAĞINDA (ekran-uzayı, tower'dan bağımsız), 1.kat hizası.
        // Aşağıdan yukarı uzar (pivot alt). CanvasGroup şart (Alpha erişimi).
        var liftRt = MockupUI.NewRect("ScissorLift", rootRt);
        liftRt.anchorMin = liftRt.anchorMax = new Vector2(0.86f, 0.03f);
        liftRt.pivot = new Vector2(0.5f, 0f);
        liftRt.anchoredPosition = new Vector2(-50f, 0f);
        liftRt.sizeDelta = new Vector2(330f, 300f);
        liftRt.gameObject.AddComponent<CanvasGroup>();
        var liftView = liftRt.gameObject.AddComponent<ScissorLiftView>();
        // Rising temalı lift parçaları (override; boşsa MiniLift default).
        MockupUI.SetRef(liftView, "baseSpriteOverride",     MockupUI.LoadSprite(BasePath));
        MockupUI.SetRef(liftView, "platformSpriteOverride", MockupUI.LoadSprite(PlatformPath));
        MockupUI.SetRef(liftView, "armFrontSpriteOverride", MockupUI.LoadSprite(ArmFrontPath));
        MockupUI.SetRef(liftView, "armBackSpriteOverride",  MockupUI.LoadSprite(ArmBackPath));
        MockupUI.SetRef(liftView, "boltSpriteOverride",     MockupUI.LoadSprite(BoltPath));
        SetBool(liftView, "preserveRootTransform", true);
        SetBool(liftView, "armsInFrontOfBase", true);
        SetBool(liftView, "simpleCrossMode", true);
        SetBool(liftView, "progressiveStageReveal", true);
        SetInt(liftView, "stageCountOverride", Mathf.Max(1, config.pitstopCount - 1));
        SetFloat(liftView, "backArmAlpha", 0.82f);
        SetFloat(liftView, "collapsedStageAlpha", 0.22f);
        SetFloat(liftView, "armLayerOffsetY", 8f);
        SetFloat(liftView, "baseMountYReferencePx", 75f);

        // Rest (ilk duruş) lift görseli — RisingLiftT2, sağ köşe.
        var restLiftImg = MockupUI.NewImage("RestLift", rootRt, Color.white);
        var restRt = restLiftImg.rectTransform;
        restRt.anchorMin = restRt.anchorMax = new Vector2(0.86f, 0.03f);
        restRt.pivot = new Vector2(0.5f, 0f);
        restRt.anchoredPosition = new Vector2(-50f, 0f);   // biraz daha sola (kullanıcı değeri)
        var restSprite = MockupUI.LoadSprite(RestLiftPath);
        if (restSprite != null) restLiftImg.sprite = restSprite;
        restLiftImg.preserveAspect = false;
        restLiftImg.raycastTarget = false;
        restRt.sizeDelta = new Vector2(330f, 300f);   // ~1.5x (kullanıcı değeri)

        var liftAnchor = MockupUI.NewRect("LiftAnchor", rootRt);
        liftAnchor.anchorMin = liftAnchor.anchorMax = restRt.anchorMin;
        liftAnchor.pivot = new Vector2(0.5f, 0.5f);
        liftAnchor.anchoredPosition = new Vector2(-25f, 300f);
        liftAnchor.sizeDelta = new Vector2(8, 8);

        // Kalabalık container'ı (runtime'da konumlanır). Root altında ve RestLift'ten sonra:
        // önce lift çizilir, oyuncular onun üstünde görünür.
        var crowdRt = MockupUI.NewRect("Crowd", rootRt);
        crowdRt.anchorMin = crowdRt.anchorMax = new Vector2(0.5f, 0.5f);
        crowdRt.pivot = new Vector2(0.5f, 0.5f);
        crowdRt.sizeDelta = new Vector2(10, 10);
        var stack = crowdRt.gameObject.AddComponent<SafariAvatarStackView>();
        MockupUI.SetRef(stack, "container", crowdRt);
        MockupUI.SetRefArray(stack, "helmetSprites", LoadHelmetSprites());
        MockupUI.SetRef(stack, "playerHelmetSprite", MockupUI.LoadSprite(PlayerHelmetPath));
        MockupUI.SetRefArray(stack, "botHelmetSprites", new Object[] { MockupUI.LoadSprite(BotHelmetPath) });

        // KATMAN 3 — TopHUD: üst şerit (ayrı katman, top-anchored). RLTophud çerçevesi + dinamik metin/ikon/değer.
        var topHud = BuildTopHud(rootRt, theme, MockupUI.LoadSprite(TopHudFramePath),
            MockupUI.LoadSprite(LevelIconPath), MockupUI.LoadSprite(PlayersIconPath));

        // Durum yazısı + devam prompt (alt)
        var status = MockupUI.NewText("Status", rootRt, "", 34,
            Color.white, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.AnchorBottom(status.rectTransform, 50, 150);

        // "Devam etmek için dokunun" — EN ALTTA, tek satır.
        var continueRoot = MockupUI.NewRect("ContinueRoot", rootRt);
        MockupUI.AnchorBottom(continueRoot, 110, 24);
        var continueBtn = MockupUI.GlossyButton(continueRoot, "Assets/_Project/Art/UI/Buttons/GreenButton.png",
            theme.ctaGreen, "Devam", 34, theme.headingFont, out var continueLabel);
        continueBtn.image.rectTransform.anchorMin = continueBtn.image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        continueBtn.image.rectTransform.sizeDelta = new Vector2(680, 96);
        continueBtn.image.rectTransform.anchoredPosition = Vector2.zero;
        if (continueLabel != null)
        {
            continueLabel.enableWordWrapping = false;               // tek satıra sığsın
            continueLabel.overflowMode = TextOverflowModes.Overflow;
            continueLabel.fontSize = 34;
        }

        var closeBtn = MockupUI.GlossyButton(rootRt, "Assets/_Project/Art/UI/Buttons/RedButton.png",
            new Color(0.8f, 0.3f, 0.25f), "X", 36, theme.headingFont, out _);
        closeBtn.image.rectTransform.anchorMin = closeBtn.image.rectTransform.anchorMax = new Vector2(1f, 1f);
        closeBtn.image.rectTransform.pivot = new Vector2(1f, 1f);
        closeBtn.image.rectTransform.anchoredPosition = new Vector2(-24, -24);
        closeBtn.image.rectTransform.sizeDelta = new Vector2(90, 90);

        // Wiring
        MockupUI.SetRef(map, "root", mapRoot);
        MockupUI.SetRef(map, "topHud", topHud);
        SetFloat(map, "liftTileSize", 287f);
        SetInt(map, "maxVisibleCrowdAvatars", 8);
        MockupUI.SetRefArray(map, "floorAnchors", anchors);
        MockupUI.SetRefArray(map, "floorNumberLabels", floorNumbers);
        MockupUI.SetRef(map, "lift", liftView);
        MockupUI.SetRef(map, "restLift", restLiftImg.gameObject);
        MockupUI.SetRef(map, "liftAnchor", liftAnchor);
        MockupUI.SetRef(map, "crowdStack", stack);
        MockupUI.SetRef(map, "continueRoot", continueRoot.gameObject);
        MockupUI.SetRef(map, "continueButton", continueBtn);
        MockupUI.SetRef(map, "continueLabel", continueLabel);
        MockupUI.SetRef(map, "statusText", status);
        MockupUI.SetRef(map, "closeButton", closeBtn);
        var gold = MockupUI.LoadSprite(GoldPath);
        if (gold != null) MockupUI.SetRef(map, "finalGoldMoneySprite", gold);

        return map;
    }

    // ── Intro overlay ─────────────────────────────────────────────────

    private static RisingIntroOverlay BuildIntroOverlay(Transform parent, UITheme theme, RisingMapScreen mapScreen,
                                                        SafariConfig config, out GameObject introRoot)
    {
        var rootRt = MockupUI.NewRect("RisingIntroOverlay", parent);
        MockupUI.Stretch(rootRt);
        introRoot = rootRt.gameObject;
        var intro = introRoot.AddComponent<RisingIntroOverlay>();

        var bg = MockupUI.NewImage("Background", rootRt, Color.black);
        MockupUI.Stretch(bg.rectTransform);
        bg.raycastTarget = true;

        var goldPile = MockupUI.NewImage("GoldPile", rootRt, Color.white);
        goldPile.rectTransform.anchorMin = goldPile.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        goldPile.rectTransform.pivot = new Vector2(0.5f, 1f);
        goldPile.rectTransform.anchoredPosition = new Vector2(-100f, -300f);
        goldPile.rectTransform.sizeDelta = new Vector2(700f, 379f);
        var goldPileSprite = MockupUI.LoadSprite(IntroGoldPilePath) ?? MockupUI.LoadSprite(GoldPath);
        if (goldPileSprite != null) goldPile.sprite = goldPileSprite;
        goldPile.preserveAspect = true;
        goldPile.raycastTarget = false;

        var flag = MockupUI.NewImage("Flag", rootRt, new Color(0.02f, 0.18f, 0.62f, 1f));
        flag.rectTransform.anchorMin = flag.rectTransform.anchorMax = new Vector2(0.82f, 1f);
        flag.rectTransform.pivot = new Vector2(0.5f, 1f);
        flag.rectTransform.anchoredPosition = new Vector2(0f, -300f);
        flag.rectTransform.sizeDelta = new Vector2(300f, 250f);
        var flagSprite = MockupUI.LoadSprite(IntroFlagPath);
        if (flagSprite != null) flag.sprite = flagSprite;
        flag.preserveAspect = true;
        flag.raycastTarget = false;

        var flagLabel = MockupUI.NewText("PlaceholderLabel", flag.rectTransform,
            "Büyük\nÖdül!", 31f, Yellow, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.Stretch(flagLabel.rectTransform);
        flagLabel.raycastTarget = false;
        flagLabel.gameObject.SetActive(flagSprite == null);

        var title = MockupUI.NewText("Title", rootRt, "Yükseliş", 82f,
            new Color(1f, 0.93f, 0.62f, 1f), TextAlignmentOptions.Center, theme.headingFont);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -700f);
        title.rectTransform.sizeDelta = new Vector2(760f, 110f);
        title.enableWordWrapping = false;

        var lift = MockupUI.NewImage("RisingLiftT2", rootRt, Color.white);
        lift.rectTransform.anchorMin = lift.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        lift.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        lift.rectTransform.anchoredPosition = new Vector2(0f, -250f);
        lift.rectTransform.sizeDelta = new Vector2(430f, 390f);
        var liftSprite = MockupUI.LoadSprite(RestLiftPath);
        if (liftSprite != null) lift.sprite = liftSprite;
        lift.preserveAspect = false;
        lift.raycastTarget = false;

        var crowdAnchor = MockupUI.NewRect("CrowdAnchor", rootRt);
        crowdAnchor.anchorMin = crowdAnchor.anchorMax = new Vector2(0.5f, 0.5f);
        crowdAnchor.pivot = new Vector2(0.5f, 0.5f);
        crowdAnchor.anchoredPosition = new Vector2(0f, -112f);
        crowdAnchor.sizeDelta = new Vector2(8f, 8f);

        var crowdRt = MockupUI.NewRect("Crowd", rootRt);
        crowdRt.anchorMin = crowdRt.anchorMax = new Vector2(0.5f, 0.5f);
        crowdRt.pivot = new Vector2(0.5f, 0.5f);
        crowdRt.anchoredPosition = crowdAnchor.anchoredPosition;
        crowdRt.sizeDelta = new Vector2(10f, 10f);
        var stack = crowdRt.gameObject.AddComponent<SafariAvatarStackView>();
        MockupUI.SetRef(stack, "container", crowdRt);
        MockupUI.SetRefArray(stack, "helmetSprites", LoadHelmetSprites());
        MockupUI.SetRef(stack, "playerHelmetSprite", MockupUI.LoadSprite(PlayerHelmetPath));
        MockupUI.SetRefArray(stack, "botHelmetSprites", new Object[] { MockupUI.LoadSprite(BotHelmetPath) });

        var counter = MockupUI.NewText("Counter", rootRt, $"0/{Mathf.Max(1, config.participantVisualCount)}", 78f,
            new Color(1f, 0.9f, 0.28f, 1f), TextAlignmentOptions.Center, theme.headingFont);
        counter.rectTransform.anchorMin = counter.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        counter.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        counter.rectTransform.anchoredPosition = new Vector2(0f, -600f);
        counter.rectTransform.sizeDelta = new Vector2(520f, 100f);
        counter.enableWordWrapping = false;

        var tap = MockupUI.NewText("TapText", rootRt, "Devam Etmek İçin Dokun", 46f,
            new Color(1f, 0.93f, 0.62f, 1f), TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.AnchorBottom(tap.rectTransform, 90f, 70f);
        tap.enableWordWrapping = false;

        MockupUI.SetRef(intro, "root", introRoot);
        MockupUI.SetRef(intro, "backgroundImage", bg);
        MockupUI.SetRef(intro, "mapScreen", mapScreen);
        MockupUI.SetRef(intro, "goldPileImage", goldPile);
        MockupUI.SetRef(intro, "flagImage", flag);
        MockupUI.SetRef(intro, "liftImage", lift);
        MockupUI.SetRef(intro, "titleText", title);
        MockupUI.SetRef(intro, "counterText", counter);
        MockupUI.SetRef(intro, "tapText", tap);
        MockupUI.SetRef(intro, "crowdStack", stack);
        MockupUI.SetRef(intro, "crowdAnchor", crowdAnchor);
        SetInt(intro, "maxVisibleCrowdAvatars", 8);
        SetFloat(intro, "crowdAvatarSize", 132f);
        SetFloat(intro, "crowdSpread", 68f);
        SetFloat(intro, "transferTargetAvatarSize", 112f);
        SetFloat(intro, "countDuration", 2.4f);
        SetFloat(intro, "transferDuration", 0.9f);
        SetFloat(intro, "transferHop", 130f);

        return intro;
    }

    // ── TopHUD ───────────────────────────────────────────────────

    private static RisingTopHud BuildTopHud(Transform parent, UITheme theme, Sprite frameSprite,
                                            Sprite levelIconSprite, Sprite playersIconSprite)
    {
        // Üst şerit — top-anchored (ekranın üst %(1-TopHudFrac)'i).
        var hudRt = MockupUI.NewRect("RisingTopHud", parent);
        hudRt.anchorMin = new Vector2(0f, TopHudFrac);
        hudRt.anchorMax = new Vector2(1f, 1f);
        hudRt.offsetMin = Vector2.zero;
        hudRt.offsetMax = new Vector2(0f, -75f);   // Top = 75
        var hud = hudRt.gameObject.AddComponent<RisingTopHud>();

        // RLTophud çerçevesi — oranını KORUYARAK şeride oturur (FitInParent). Dinamik metin/ikon'lar
        // bu FRAME'in çocuğu → çerçeveyle birebir hizalı kalır (Inspector'dan kesirli koordinat tunelanır).
        var frame = MockupUI.NewImage("Frame", hudRt, frameSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f));
        MockupUI.Stretch(frame.rectTransform);
        if (frameSprite != null) frame.sprite = frameSprite;
        frame.raycastTarget = false;
        var frameFitter = frame.gameObject.AddComponent<AspectRatioFitter>();
        frameFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        frameFitter.aspectRatio = (frameSprite != null && frameSprite.rect.height > 0.01f)
            ? frameSprite.rect.width / frameSprite.rect.height : 3.05f;
        var fr = frame.rectTransform;

        // Mor bant başlık
        var titleText = MakeHudText("Title", fr, theme.headingFont, Yellow, 40,
            new Vector2(0.5f, 0.82f), new Vector2(360f, 60f), "Yükseliş");

        // Sol kutu — Seviye (posX/posY: kullanıcı Unity tuning'i, mevcut frac anchor'a göre; font 40).
        var levelTitle = MakeHudText("LevelTitle", fr, theme.headingFont, Yellow, 40,
            new Vector2(0.30f, 0.62f), new Vector2(160f, 40f), "Seviye");
        levelTitle.rectTransform.anchoredPosition = new Vector2(-25f, -60f);
        var levelIcon  = MakeHudIcon("LevelIcon", fr, new Vector2(0.25f, 0.30f), 50f, levelIconSprite);
        levelIcon.rectTransform.anchoredPosition = new Vector2(0f, -20f);   // satırı Value ile hizala (simetri)
        var levelValue = MakeHudText("LevelValue", fr, theme.bodyFont, Color.white, 40,
            new Vector2(0.34f, 0.30f), new Vector2(120f, 40f), "0/7");
        levelValue.rectTransform.anchoredPosition = new Vector2(0f, -20f);
        levelValue.alignment = TextAlignmentOptions.Left;

        // Sağ kutu — Oyuncu (kullanıcı değerleri).
        var playersTitle = MakeHudText("PlayersTitle", fr, theme.headingFont, Yellow, 40,
            new Vector2(0.70f, 0.62f), new Vector2(160f, 40f), "Oyuncu");
        playersTitle.rectTransform.anchoredPosition = new Vector2(50f, -60f);
        var playersIcon  = MakeHudIcon("PlayersIcon", fr, new Vector2(0.64f, 0.30f), 50f, playersIconSprite);
        playersIcon.rectTransform.anchoredPosition = new Vector2(50f, -20f);
        var playersValue = MakeHudText("PlayersValue", fr, theme.bodyFont, Color.white, 40,
            new Vector2(0.73f, 0.30f), new Vector2(120f, 40f), "100");
        playersValue.rectTransform.anchoredPosition = new Vector2(70f, -20f);
        playersValue.alignment = TextAlignmentOptions.Left;

        MockupUI.SetRef(hud, "titleText", titleText);
        MockupUI.SetRef(hud, "levelTitleText", levelTitle);
        MockupUI.SetRef(hud, "levelIcon", levelIcon);
        MockupUI.SetRef(hud, "levelValueText", levelValue);
        MockupUI.SetRef(hud, "playersTitleText", playersTitle);
        MockupUI.SetRef(hud, "playersIcon", playersIcon);
        MockupUI.SetRef(hud, "playersValueText", playersValue);
        return hud;
    }

    // Kesirli koordinata (board oranı) yerleşen dinamik metin.
    private static TMP_Text MakeHudText(string name, Transform parent, TMP_FontAsset font, Color color,
                                        float size, Vector2 frac, Vector2 boxSize, string text)
    {
        var t = MockupUI.NewText(name, parent, text, size, color, TextAlignmentOptions.Center, font);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = frac;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = boxSize;
        return t;
    }

    // Kesirli koordinata yerleşen dinamik ikon (sprite verilirse görünür).
    private static Image MakeHudIcon(string name, Transform parent, Vector2 frac, float size, Sprite sprite)
    {
        var icon = MockupUI.NewImage(name, parent, Color.white);
        var rt = icon.rectTransform;
        rt.anchorMin = rt.anchorMax = frac;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.preserveAspect = true;
        return icon;
    }

    // ── Join popup (Safari popup controller'ını yeniden kullan) ───

    private static SafariJoinPopupController FindExistingJoinPopup(Transform root, Transform newSystem, out GameObject popupRoot)
    {
        popupRoot = null;
        var all = Resources.FindObjectsOfTypeAll<SafariJoinPopupController>();
        for (int i = 0; i < all.Length; i++)
        {
            var popup = all[i];
            if (popup == null || !popup.gameObject.scene.IsValid())
                continue;
            if (newSystem != null && popup.transform.IsChildOf(newSystem))
                continue;
            if (popup.name != "SafariJoinPopup")
                continue;

            popupRoot = popup.gameObject;
            return popup;
        }

        var named = FindChildByName(root, "SafariJoinPopup");
        if (named != null && (newSystem == null || !named.IsChildOf(newSystem)))
        {
            var popup = named.GetComponent<SafariJoinPopupController>();
            if (popup != null)
            {
                popupRoot = named.gameObject;
                return popup;
            }
        }

        return null;
    }

    private static SafariJoinPopupController BuildJoinPopup(Transform parent, UITheme theme, out GameObject popupRoot)
    {
        var rootRt = MockupUI.NewRect("RisingJoinPopup", parent);
        MockupUI.Stretch(rootRt);
        popupRoot = rootRt.gameObject;
        var popup = popupRoot.AddComponent<SafariJoinPopupController>();

        var dim = MockupUI.NewImage("Dim", rootRt, new Color(0, 0, 0, 0.65f));
        MockupUI.Stretch(dim.rectTransform);

        var panel = MockupUI.NewImage("PopupPanel", rootRt, theme.panelSurface);
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.sizeDelta = new Vector2(760, 620);
        var popupSprite = MockupUI.LoadSprite(PopupPath);
        if (popupSprite != null) panel.sprite = popupSprite;

        var overlay = MockupUI.NewImage("OverlayImage", panel.rectTransform, Color.white);
        overlay.rectTransform.anchorMin = overlay.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
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

    private static SafariEventButton BuildEventIcon(Transform parent, UITheme theme, Sprite bg)
    {
        var icon = MockupUI.NewImage("RisingEventIcon", parent, Color.white);
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

        var label = MockupUI.NewText("Label", rt, "YÜKSELİŞ", 20,
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
            if (tabs[i] != null && tabs[i].gameObject.scene.IsValid()) { target = tabs[i]; break; }
        if (target == null) return;

        var so = new SerializedObject(target);
        var array = so.FindProperty("homeOnlyElements");
        if (array == null || !array.isArray) return;
        for (int i = 0; i < array.arraySize; i++)
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == element) return;

        array.InsertArrayElementAtIndex(array.arraySize);
        array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = element;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Yardımcılar ──────────────────────────────────────────────

    private static RectTransform MakeAnchor(string name, Transform parent, Vector2 frac)
    {
        var rt = MockupUI.NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = frac;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(8, 8);
        return rt;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == childName) return all[i];
        return null;
    }

    private static void PreserveExistingJoinPopup(Transform root)
    {
        var popup = FindChildByName(root, "SafariJoinPopup");
        if (popup == null) return;
        if (popup.parent != root)
            popup.SetParent(root, false);
        popup.SetAsLastSibling();
        popup.gameObject.SetActive(false);
    }

    private static void DestroyChildByName(Transform root, string childName)
    {
        var child = FindChildByName(root, childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }

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

    private static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[RisingMockupSetup] '{field}' alanı {target} üzerinde yok."); return; }
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[RisingMockupSetup] '{field}' alanı {target} üzerinde yok."); return; }
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[RisingMockupSetup] '{field}' alanı {target} üzerinde yok."); return; }
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
