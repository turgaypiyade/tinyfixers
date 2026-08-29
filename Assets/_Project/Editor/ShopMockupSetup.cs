using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mağaza ekranını TEK TIKLA kurar — MarketUI sprite'larını otomatik bağlar.
/// Menü: TinyFixers > Mockup > Setup Shop Screen.
///
/// Yapı (referansa göre):
///  • Titlebar  = GeneralTophud (sol/sağ -6, yükseklik 210).
///  • Bundle kartı = MegaAwards1 (900x463). SOL'da doğrudan altın ikonu + miktar (kutu YOK);
///    sağında 3 kutu (MATGrup5/3/1, dinamik/elle değiştirilebilir); mor bantta isim + BuyButton (300x94).
///  • CoinRow    = OnlyGolds; coin ikonu + miktar + BuyButton (300x94).
///  • ScrollView 215'ten başlar; içerik genişliği 900.
///
/// Kutular kartta elle konumlandırılır (bu generator başlangıç düzenini kurar); kod içeriği doldurur.
/// </summary>
public static class ShopMockupSetup
{
    private const string SettingsDir = "Assets/_Project/Settings";
    private const string PrefabDir   = "Assets/_Project/Prefabs/UI/Shop";
    private const string ArtDir      = "Assets/_Project/Art/UI/MarketUI";
    private const string BoosterDir  = "Assets/_Project/Art/Icons/Boosters";
    private const string TopHudPath  = "Assets/_Project/Art/UI/RanksTeamUI/GeneralTophud.png";
    private const string HeartPath   = "Assets/_Project/Art/UI/HeartInfinite.png";

    private const string ThemePath      = SettingsDir + "/UITheme.asset";
    private const string CatalogPath    = SettingsDir + "/ShopCatalog.asset";
    private const string IconPath       = PrefabDir + "/ShopRewardIcon.prefab";
    private const string BundleCardPath = PrefabDir + "/ShopOfferCard.prefab";
    private const string CoinRowPath    = PrefabDir + "/ShopCoinRowCard.prefab";
    private const string HeaderPath     = PrefabDir + "/ShopSectionHeader.prefab";

    // MegaAwards1 (900x463) iç bölgeleri — normalize (y=0 alt, y=1 üst)
    private const float CreamTop = 0.90f, CreamBot = 0.40f;   // krem alan
    private const float BandY    = 111f;                       // mor bant merkez (px, alttan)

    [MenuItem("TinyFixers/Mockup/Setup Shop Screen")]
    public static void Setup()
    {
        EnsureFolder(SettingsDir);
        EnsureFolder(PrefabDir);

        var theme   = EnsureTheme();
        var catalog = EnsureCatalog();

        var iconAsset = BuildIconPrefab();
        BuildBundleCardPrefab(theme, iconAsset);
        BuildCoinRowPrefab(theme);
        BuildHeaderPrefab(theme);
        AssetDatabase.SaveAssets();

        var bundleAsset = AssetDatabase.LoadAssetAtPath<ShopOfferCard>(BundleCardPath);
        var coinAsset   = AssetDatabase.LoadAssetAtPath<ShopCoinRowCard>(CoinRowPath);
        var headerAsset = AssetDatabase.LoadAssetAtPath<ShopSectionHeader>(HeaderPath);

        var tabController = Object.FindFirstObjectByType<BottomTabController>(FindObjectsInactive.Include);
        if (tabController == null)
        {
            EditorUtility.DisplayDialog("Shop Setup",
                "Sahnede BottomTabController bulunamadı. MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var existing = tabController.transform.parent.Find("ShopPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var panel = BuildShopPanel(tabController.transform, theme, catalog, bundleAsset, coinAsset, headerAsset);
        AssignMarketTabPanel(tabController, panel);

        EditorSceneManager.MarkSceneDirty(tabController.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Shop Setup",
            "Mağaza kuruldu. Kutu konum/ölçülerini editör'de ince ayar yap; paket adları/miktar/grant " +
            "ShopCatalog.asset'ten düzenlenir.\n\nSahneyi kaydet (Cmd+S).", "Tamam");
    }

    // ===================================================================
    //  Sprite yükleyiciler
    // ===================================================================

    private static Sprite Art(string file)    => AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/" + file + ".png");
    private static Sprite Booster(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(BoosterDir + "/" + file + ".png");
    private static Sprite TopHud()             => AssetDatabase.LoadAssetAtPath<Sprite>(TopHudPath);
    private static Sprite Heart()              => AssetDatabase.LoadAssetAtPath<Sprite>(HeartPath);

    /// <summary>GoldSheets.png içindeki alt-sprite'ı adıyla getirir (GoldSheets_0..5).</summary>
    private static Sprite Gold(string subName)
    {
        foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(ArtDir + "/GoldSheets.png"))
            if (o is Sprite s && s.name == subName) return s;
        Debug.LogWarning($"[ShopMockupSetup] GoldSheets alt-sprite bulunamadı: {subName}");
        return null;
    }

    // ===================================================================
    //  Asset'ler
    // ===================================================================

    private static UITheme EnsureTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme != null) return theme;

        theme = ScriptableObject.CreateInstance<UITheme>();
        var font = TMP_Settings.defaultFontAsset;
        theme.headingFont = font;
        theme.bodyFont    = font;
        AssetDatabase.CreateAsset(theme, ThemePath);
        return theme;
    }

    private static ShopCatalog EnsureCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
        if (catalog != null) return catalog;

        var boosters = new List<Sprite>
        {
            Booster("HammerBooster"), Booster("CannonBooster"), Booster("VerticalBooster"),
            Booster("ShuffleBooster"), Booster("DrillJoker"),
        };

        catalog = ScriptableObject.CreateInstance<ShopCatalog>();
        catalog.sections = new List<ShopSection>
        {
            new ShopSection
            {
                title = "Özel Teklifler",
                bandStyle = ShopSection.BandStyle.Special,
                offers = new List<ShopOffer>
                {
                    Bundle("bundle_grand_safe", "Muhteşem Kasa", "3999.99 TL", best: false,
                           coinIcon: Gold("GoldSheets_0"), coins: 50000, boosters: boosters,
                           boosterCount: 10, timedValue: 72, infiniteHours: 12),
                    Bundle("bundle_legendary", "Efsanevi Hazine", "4999.99 TL", best: true,
                           coinIcon: Gold("GoldSheets_1"), coins: 65000, boosters: boosters,
                           boosterCount: 13, timedValue: 100, infiniteHours: 18),
                }
            },
            new ShopSection
            {
                title = "Altınlar",
                bandStyle = ShopSection.BandStyle.Header,
                offers = new List<ShopOffer>
                {
                    CoinRow("coins_1000", "99.99 TL",  1000, Gold("GoldSheets_3")),
                    CoinRow("coins_5000", "399.99 TL", 5000, Gold("GoldSheets_5")),
                }
            }
        };
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    /// <summary>Bundle: groups[0]=altın hero (kutu YOK), groups[1..3]=kutular (MATGrup5/3/1).</summary>
    private static ShopOffer Bundle(string id, string name, string price, bool best, Sprite coinIcon,
                                    int coins, List<Sprite> boosters, int boosterCount, int timedValue, int infiniteHours)
    {
        return new ShopOffer
        {
            id = id, displayName = name, cardStyle = ShopOffer.CardStyle.Bundle, showBestBadge = best,
            priceType = ShopOffer.PriceType.RealMoney, priceLabel = price,
            groups = new List<ShopRewardGroup>
            {
                new ShopRewardGroup   // [0] altın hero — background YOK (doğrudan sol ikon)
                {
                    icons = coinIcon != null ? new List<Sprite> { coinIcon } : new List<Sprite>(),
                    labelMode = ShopRewardGroup.LabelMode.Currency, labelValue = coins,
                    grants = new List<ShopReward> { new ShopReward { kind = ShopReward.Kind.Coins, amount = coins } },
                },
                new ShopRewardGroup   // [1] booster kutusu (MATGrup5)
                {
                    background = Art("MATGrup5"),
                    icons = new List<Sprite>(boosters),
                    labelMode = ShopRewardGroup.LabelMode.Count, labelValue = boosterCount,
                    grants = new List<ShopReward>
                    {
                        new ShopReward { kind = ShopReward.Kind.Booster, booster = BoardController.BoosterMode.Single, amount = boosterCount },
                    },
                },
                new ShopRewardGroup   // [2] süreli kutu (MATGrup3, saat ikonlu)
                {
                    background = Art("MATGrup3"),
                    labelMode = ShopRewardGroup.LabelMode.Duration, labelValue = timedValue, showTimerIcon = true,
                    grants = new List<ShopReward>
                    {
                        new ShopReward { kind = ShopReward.Kind.Booster, booster = BoardController.BoosterMode.Shuffle, amount = 2 },
                    },
                },
                new ShopRewardGroup   // [3] sonsuz can (MATGrup1)
                {
                    background = Art("MATGrup1"),
                    icons = Heart() != null ? new List<Sprite> { Heart() } : new List<Sprite>(),
                    labelMode = ShopRewardGroup.LabelMode.Duration, labelValue = infiniteHours,
                    grants = new List<ShopReward> { new ShopReward { kind = ShopReward.Kind.InfiniteLifeTimed, durationHours = infiniteHours } },
                },
            }
        };
    }

    private static ShopOffer CoinRow(string id, string price, int coins, Sprite coinIcon)
    {
        return new ShopOffer
        {
            id = id, displayName = coins.ToString("N0"), cardStyle = ShopOffer.CardStyle.CoinRow,
            priceType = ShopOffer.PriceType.RealMoney, priceLabel = price,
            groups = new List<ShopRewardGroup>
            {
                new ShopRewardGroup
                {
                    icons = coinIcon != null ? new List<Sprite> { coinIcon } : new List<Sprite>(),
                    labelMode = ShopRewardGroup.LabelMode.Currency, labelValue = coins,
                    grants = new List<ShopReward> { new ShopReward { kind = ShopReward.Kind.Coins, amount = coins } },
                }
            }
        };
    }

    // ===================================================================
    //  Prefab'lar
    // ===================================================================

    private static Image BuildIconPrefab()
    {
        var root = NewRect("ShopRewardIcon", null);
        root.sizeDelta = new Vector2(48, 48);
        var img = root.gameObject.AddComponent<Image>();
        img.color = Color.white;
        img.preserveAspect = true;
        AddLayoutElement(root.gameObject, preferredWidth: 48, preferredHeight: 48);
        var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, IconPath);
        Object.DestroyImmediate(root.gameObject);
        return saved.GetComponent<Image>();   // null-timing sorununu önler
    }

    private static void BuildBundleCardPrefab(UITheme theme, Image iconPrefab)
    {
        var root = NewRect("ShopOfferCard", null);
        root.sizeDelta = new Vector2(900, 463);
        var bg = root.gameObject.AddComponent<Image>();
        SetSprite(bg, Art("MegaAwards1"), theme.creamSurface);
        AddLayoutElement(root.gameObject, preferredHeight: 463);
        var card = root.gameObject.AddComponent<ShopOfferCard>();

        // --- Hero (altın, sol — kutu değil) ---
        var heroIcon = NewImage("HeroIcon", root, Color.white);
        heroIcon.preserveAspect = true;
        AnchorBox(heroIcon.rectTransform, new Vector2(0.05f, 0.50f), new Vector2(0.21f, CreamTop));
        var heroAmount = NewText("HeroAmount", root, "0", 34, theme.textOnCream, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(heroAmount.rectTransform, new Vector2(0.03f, CreamBot), new Vector2(0.23f, 0.50f));

        // --- 3 kutu (altının sağı) ---
        var boxes = new ShopRewardGroupBox[3];
        float[] xs = { 0.24f, 0.53f, 0.75f, 0.96f };  // 3 slot sınırı
        for (int i = 0; i < 3; i++)
            boxes[i] = BuildGroupBox($"Box{i}", root, new Vector2(xs[i] + 0.005f, CreamBot),
                                     new Vector2(xs[i + 1] - 0.005f, CreamTop), theme, iconPrefab);

        // --- Mor bant: isim + BuyButton ---
        var nameText = NewText("Name", root, "Teklif", 34, theme.textLight, TextAlignmentOptions.Left, theme.headingFont);
        var nameRT = nameText.rectTransform;
        nameRT.anchorMin = nameRT.anchorMax = new Vector2(0, 0); nameRT.pivot = new Vector2(0, 0.5f);
        nameRT.anchoredPosition = new Vector2(55, BandY); nameRT.sizeDelta = new Vector2(440, 64);

        var priceBtnImg = NewImage("PriceButton", root, Color.white);
        SetSprite(priceBtnImg, Art("BuyButton"), theme.priceGreen);
        var btnRT = priceBtnImg.rectTransform;
        btnRT.anchorMin = btnRT.anchorMax = new Vector2(1, 0); btnRT.pivot = new Vector2(1, 0.5f);
        btnRT.anchoredPosition = new Vector2(-45, BandY); btnRT.sizeDelta = new Vector2(300, 94);
        var priceBtn = priceBtnImg.gameObject.AddComponent<Button>();
        priceBtn.targetGraphic = priceBtnImg;
        var priceText = NewText("PriceText", btnRT, "0", 30, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(priceText.rectTransform, Vector2.zero, Vector2.one);

        // --- "En İyi Fırsat" kurdelesi (sol üst köşe) ---
        var badge = NewImage("BestBadge", root, theme.specialBand);
        var badgeRT = badge.rectTransform;
        badgeRT.anchorMin = badgeRT.anchorMax = new Vector2(0, 1); badgeRT.pivot = new Vector2(0.5f, 0.5f);
        badgeRT.anchoredPosition = new Vector2(80, -46); badgeRT.sizeDelta = new Vector2(220, 50);
        badgeRT.localRotation = Quaternion.Euler(0, 0, 20);
        var badgeText = NewText("Text", badgeRT, "En İyi Fırsat", 22, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(badgeText.rectTransform, Vector2.zero, Vector2.one);
        badge.gameObject.SetActive(false);

        SetRef(card, "cardBackground", bg);
        SetRef(card, "heroIcon", heroIcon);
        SetRef(card, "heroAmountText", heroAmount);
        SetArrayRef(card, "boxes", boxes);
        SetRef(card, "bestBadge", badge.gameObject);
        SetRef(card, "nameText", nameText);
        SetRef(card, "priceButton", priceBtn);
        SetRef(card, "priceButtonBackground", priceBtnImg);
        SetRef(card, "priceText", priceText);

        SaveAndCleanup(root.gameObject, BundleCardPath);
    }

    /// <summary>Tek kutu: arka plan (runtime'da MATGrup) + ikon grid'i (üst) + etiket satırı (alt şerit).</summary>
    private static ShopRewardGroupBox BuildGroupBox(string name, Transform parent, Vector2 min, Vector2 max,
                                                    UITheme theme, Image iconPrefab)
    {
        var root = NewRect(name, parent);
        AnchorBox(root, min, max);
        var bg = root.gameObject.AddComponent<Image>();
        bg.color = theme.creamSurface;   // gerçek MATGrup runtime'da group.background'tan gelir
        var box = root.gameObject.AddComponent<ShopRewardGroupBox>();

        var icons = NewRect("Icons", root);
        AnchorBox(icons, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.95f));
        var grid = icons.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(46, 46); grid.spacing = new Vector2(4, 4);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.MiddleCenter;

        var labelRow = NewRect("LabelRow", root);
        AnchorBox(labelRow, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.29f));
        var rowH = labelRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowH.spacing = 3; rowH.childControlWidth = rowH.childControlHeight = true;
        rowH.childForceExpandWidth = false; rowH.childForceExpandHeight = false;
        rowH.childAlignment = TextAnchor.MiddleCenter;

        var timer = NewImage("TimerIcon", labelRow, Color.white);
        timer.preserveAspect = true;
        AddLayoutElement(timer.gameObject, preferredWidth: 24, preferredHeight: 24);
        timer.gameObject.SetActive(false);

        var label = NewText("Label", labelRow, "x1", 26, theme.textOnCream, TextAlignmentOptions.Center, theme.headingFont);
        AddLayoutElement(label.gameObject, preferredHeight: 30);

        SetRef(box, "panelBackground", bg);
        SetRef(box, "iconContainer", icons);
        SetRef(box, "iconPrefab", iconPrefab);
        SetRef(box, "timerIcon", timer.gameObject);
        SetRef(box, "labelText", label);
        return box;
    }

    private static void BuildCoinRowPrefab(UITheme theme)
    {
        var root = NewRect("ShopCoinRowCard", null);
        root.sizeDelta = new Vector2(900, 150);
        var bg = root.gameObject.AddComponent<Image>();
        SetSprite(bg, Art("OnlyGolds"), theme.creamSurface);
        AddLayoutElement(root.gameObject, preferredHeight: 150);
        var card = root.gameObject.AddComponent<ShopCoinRowCard>();

        var coinIcon = NewImage("CoinIcon", root, Color.white);
        coinIcon.preserveAspect = true;
        AnchorBox(coinIcon.rectTransform, new Vector2(0.03f, 0.12f), new Vector2(0.19f, 0.88f));

        var amountText = NewText("Amount", root, "0", 44, theme.textOnCream, TextAlignmentOptions.Left, theme.headingFont);
        AnchorBox(amountText.rectTransform, new Vector2(0.22f, 0.10f), new Vector2(0.58f, 0.90f));

        var priceBtnImg = NewImage("PriceButton", root, Color.white);
        SetSprite(priceBtnImg, Art("BuyButton"), theme.priceGreen);
        var btnRT = priceBtnImg.rectTransform;
        btnRT.anchorMin = btnRT.anchorMax = new Vector2(1, 0.5f); btnRT.pivot = new Vector2(1, 0.5f);
        btnRT.anchoredPosition = new Vector2(-30, 0); btnRT.sizeDelta = new Vector2(300, 94);
        var priceBtn = priceBtnImg.gameObject.AddComponent<Button>();
        priceBtn.targetGraphic = priceBtnImg;
        var priceText = NewText("PriceText", btnRT, "0", 30, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(priceText.rectTransform, Vector2.zero, Vector2.one);

        SetRef(card, "cardBackground", bg);
        SetRef(card, "coinIcon", coinIcon);
        SetRef(card, "amountText", amountText);
        SetRef(card, "priceButton", priceBtn);
        SetRef(card, "priceButtonBackground", priceBtnImg);
        SetRef(card, "priceText", priceText);

        SaveAndCleanup(root.gameObject, CoinRowPath);
    }

    private static void BuildHeaderPrefab(UITheme theme)
    {
        var root = NewRect("ShopSectionHeader", null);
        root.sizeDelta = new Vector2(900, 64);
        var band = root.gameObject.AddComponent<Image>();
        band.color = theme.headerBand;
        AddLayoutElement(root.gameObject, preferredHeight: 64);

        var header = root.gameObject.AddComponent<ShopSectionHeader>();
        var title = NewText("Title", root, "Bölüm", 34, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(title.rectTransform, Vector2.zero, Vector2.one);

        SetRef(header, "band", band);
        SetRef(header, "title", title);

        SaveAndCleanup(root.gameObject, HeaderPath);
    }

    // ===================================================================
    //  Panel (sahnede)
    // ===================================================================

    private static GameObject BuildShopPanel(Transform bottomBar, UITheme theme, ShopCatalog catalog,
                                             ShopOfferCard bundlePrefab, ShopCoinRowCard coinRowPrefab,
                                             ShopSectionHeader headerPrefab)
    {
        var parent = bottomBar.parent;
        var panel = NewRect("ShopPanel", parent);
        panel.SetSiblingIndex(bottomBar.GetSiblingIndex());
        Stretch(panel);
        var pbg = panel.gameObject.AddComponent<Image>();
        SetSprite(pbg, Art("MarketBG"), theme.screenBackground);
        var ctrl = panel.gameObject.AddComponent<ShopScreenController>();

        // Titlebar = GeneralTophud (sol/sağ -6, yükseklik 210)
        var top = NewRect("TopBar", panel);
        top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1);
        top.offsetMin = new Vector2(-6, -210); top.offsetMax = new Vector2(6, 0);
        var topBg = top.gameObject.AddComponent<Image>();
        SetSprite(topBg, TopHud(), theme.headerBand);
        var title = NewText("Title", top, "Mağaza", 48, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(title.rectTransform, new Vector2(0, 0), new Vector2(1, 0.72f));
        var coin = NewText("CoinBalance", top, "0", 36, theme.accentAmber, TextAlignmentOptions.Left, theme.headingFont);
        coin.rectTransform.anchorMin = coin.rectTransform.anchorMax = new Vector2(0, 0);
        coin.rectTransform.pivot = new Vector2(0, 0.5f);
        coin.rectTransform.anchoredPosition = new Vector2(40, 45); coin.rectTransform.sizeDelta = new Vector2(220, 60);

        // ScrollView (215'ten başlar)
        var scroll = NewRect("ScrollView", panel);
        scroll.anchorMin = Vector2.zero; scroll.anchorMax = Vector2.one;
        scroll.offsetMin = new Vector2(0, 40); scroll.offsetMax = new Vector2(0, -215);
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;

        var viewport = NewRect("Viewport", scroll);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        // İçerik genişliği sabit 900
        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0.5f, 1); content.anchorMax = new Vector2(0.5f, 1); content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(900, 0);
        var cv = content.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.padding = new RectOffset(0, 0, 0, 0); cv.spacing = 20;
        cv.childControlWidth = cv.childControlHeight = true;
        cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
        cv.childAlignment = TextAnchor.UpperCenter;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewport; sr.content = content;

        var toast = NewRect("PurchaseToast", panel);
        toast.anchorMin = new Vector2(0.5f, 0); toast.anchorMax = new Vector2(0.5f, 0); toast.pivot = new Vector2(0.5f, 0);
        toast.anchoredPosition = new Vector2(0, 260); toast.sizeDelta = new Vector2(560, 90);
        var toastBg = toast.gameObject.AddComponent<Image>(); toastBg.color = theme.ctaGreen;
        var toastText = NewText("Text", toast, "Satın alındı!", 32, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(toastText.rectTransform, Vector2.zero, Vector2.one);
        toast.gameObject.SetActive(false);

        SetRef(ctrl, "catalog", catalog);
        SetRef(ctrl, "theme", theme);
        SetRef(ctrl, "contentContainer", content);
        SetRef(ctrl, "sectionHeaderPrefab", headerPrefab);
        SetRef(ctrl, "bundleCardPrefab", bundlePrefab);
        SetRef(ctrl, "coinRowPrefab", coinRowPrefab);
        SetRef(ctrl, "coinBalanceText", coin);
        SetRef(ctrl, "purchaseToast", toast.gameObject);
        SetRef(ctrl, "purchaseToastText", toastText);

        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    private static void AssignMarketTabPanel(BottomTabController controller, GameObject panel)
    {
        var so = new SerializedObject(controller);
        var tabs = so.FindProperty("tabs");
        if (tabs == null) return;
        for (int i = 0; i < tabs.arraySize; i++)
        {
            var el = tabs.GetArrayElementAtIndex(i);
            var name = el.FindPropertyRelative("name");
            if (name != null && name.stringValue == "Market")
            {
                el.FindPropertyRelative("panel").objectReferenceValue = panel;
                break;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ===================================================================
    //  UGUI yardımcıları
    // ===================================================================

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        return rt;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size,
                                           Color color, TextAlignmentOptions align, TMP_FontAsset font)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        if (font != null) t.font = font;
        return t;
    }

    /// <summary>Image'a sprite bas (varsa beyaz tint); yoksa düz renk fallback.</summary>
    private static void SetSprite(Image img, Sprite sprite, Color fallback)
    {
        if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Simple; img.color = Color.white; }
        else                { img.sprite = null; img.color = fallback; }
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void AnchorBox(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static LayoutElement AddLayoutElement(GameObject go, float preferredWidth = -1, float preferredHeight = -1)
    {
        var le = go.AddComponent<LayoutElement>();
        if (preferredWidth  >= 0) le.preferredWidth  = preferredWidth;
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        return le;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[ShopMockupSetup] '{field}' alanı {target} üzerinde bulunamadı."); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArrayRef(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[ShopMockupSetup] '{field}' dizisi {target} üzerinde bulunamadı."); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SaveAndCleanup(GameObject temp, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf   = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
