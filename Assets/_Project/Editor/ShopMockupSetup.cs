using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mağaza ekranını TEK TIKLA kurar — kullanıcı Unity'de hiçbir şey sürüklemez.
/// Menü: TinyFixers > Mockup > Setup Shop Screen.
///
/// Yaptıkları:
///  1) UITheme asset'i (yoksa) oluşturur — palet kod default'larından gelir.
///  2) ShopCatalog asset'i mock veriyle oluşturur (tüm fiyat türlerini test eder).
///  3) Chip / Card / SectionHeader prefab'larını düz-renk + TMP ile üretir.
///  4) Açık MainMenu sahnesinde Canvas altına ShopPanel kurar, ShopScreenController'ı
///     bağlar ve BottomTabController'daki "Market" sekmesinin panel alanına atar.
///
/// Hepsi Unity'nin kendi serileştirmesiyle yapılır — elle YAML/GUID riski yok.
/// </summary>
public static class ShopMockupSetup
{
    private const string SettingsDir = "Assets/_Project/Settings";
    private const string PrefabDir   = "Assets/_Project/Prefabs/UI/Shop";
    private const string ThemePath   = SettingsDir + "/UITheme.asset";
    private const string CatalogPath = SettingsDir + "/ShopCatalog.asset";
    private const string ChipPath    = PrefabDir + "/ShopRewardChip.prefab";
    private const string CardPath    = PrefabDir + "/ShopOfferCard.prefab";
    private const string HeaderPath  = PrefabDir + "/ShopSectionHeader.prefab";

    [MenuItem("TinyFixers/Mockup/Setup Shop Screen")]
    public static void Setup()
    {
        EnsureFolder(SettingsDir);
        EnsureFolder(PrefabDir);

        var theme   = EnsureTheme();
        var catalog = EnsureCatalog();
        PatchMockAvailability(catalog);   // mevcut katalogda da günlük/tek-seferlik teklifleri ayarla

        // Prefab'lar (chip → card → header sırası; card chip'e referans verir).
        BuildChipPrefab(theme);
        var chipAsset = AssetDatabase.LoadAssetAtPath<ShopRewardChip>(ChipPath);
        BuildCardPrefab(theme, chipAsset);
        BuildHeaderPrefab(theme);
        AssetDatabase.SaveAssets();

        var cardAsset   = AssetDatabase.LoadAssetAtPath<ShopOfferCard>(CardPath);
        var headerAsset = AssetDatabase.LoadAssetAtPath<ShopSectionHeader>(HeaderPath);

        var tabController = Object.FindFirstObjectByType<BottomTabController>(FindObjectsInactive.Include);
        if (tabController == null)
        {
            EditorUtility.DisplayDialog("Shop Setup",
                "Sahnede BottomTabController bulunamadı. MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        // Idempotent: önceki çalıştırmadan kalan paneli sil (kopya olmasın).
        var existing = tabController.transform.parent.Find("ShopPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var panel = BuildShopPanel(tabController.transform, theme, catalog, cardAsset, headerAsset);
        AssignMarketTabPanel(tabController, panel);

        EditorSceneManager.MarkSceneDirty(tabController.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Shop Setup",
            "Mağaza kuruldu ve Market sekmesine bağlandı.\nSahneyi kaydetmeyi unutma (Cmd+S).", "Tamam");
    }

    // ===================================================================
    //  Asset'ler
    // ===================================================================

    private static UITheme EnsureTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme != null) return theme;

        theme = ScriptableObject.CreateInstance<UITheme>();
        // Font default'u — varsa TMP default'unu kullan.
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

        catalog = ScriptableObject.CreateInstance<ShopCatalog>();
        catalog.sections = new List<ShopSection>
        {
            new ShopSection
            {
                title = "Özel Teklifler",
                bandStyle = ShopSection.BandStyle.Special,
                offers = new List<ShopOffer>
                {
                    new ShopOffer
                    {
                        id = "special_2000", displayName = "Özel Teklif", heroAmount = 2000,
                        priceType = ShopOffer.PriceType.RealMoney, priceLabel = "99,99 TL",
                        contents = new List<ShopReward>
                        {
                            new ShopReward { kind = ShopReward.Kind.Booster, booster = BoardController.BoosterMode.Single, amount = 1 },
                            new ShopReward { kind = ShopReward.Kind.Booster, booster = BoardController.BoosterMode.Row,    amount = 1 },
                            new ShopReward { kind = ShopReward.Kind.Booster, booster = BoardController.BoosterMode.Column, amount = 1 },
                            new ShopReward { kind = ShopReward.Kind.InfiniteLifeTimed, durationHours = 1 },
                        }
                    }
                }
            },
            new ShopSection
            {
                title = "Mega Fırsatlar",
                offers = new List<ShopOffer>
                {
                    new ShopOffer
                    {
                        id = "mega_5000", displayName = "Prestij Kupası", heroAmount = 5000,
                        priceType = ShopOffer.PriceType.RealMoney, priceLabel = "449,99 TL",
                        contents = new List<ShopReward>
                        {
                            new ShopReward { kind = ShopReward.Kind.Booster, booster = BoardController.BoosterMode.Single,  amount = 5 },
                            new ShopReward { kind = ShopReward.Kind.Booster, booster = BoardController.BoosterMode.Shuffle, amount = 5 },
                        }
                    }
                }
            },
            new ShopSection
            {
                title = "Coin Paketleri",
                offers = new List<ShopOffer>
                {
                    new ShopOffer
                    {
                        id = "boost_pack_coins", displayName = "Booster Paketi",
                        priceType = ShopOffer.PriceType.Coins, priceAmount = 300,
                        contents = new List<ShopReward>
                        {
                            new ShopReward { kind = ShopReward.Kind.Booster, booster = BoardController.BoosterMode.Single, amount = 3 },
                        }
                    },
                    new ShopOffer
                    {
                        id = "coins_for_stars", displayName = "Yıldızla Coin", heroAmount = 500,
                        priceType = ShopOffer.PriceType.Stars, priceAmount = 50,
                        contents = new List<ShopReward>()
                    },
                    new ShopOffer
                    {
                        id = "free_gift", displayName = "Günlük Hediye",
                        priceType = ShopOffer.PriceType.Free,
                        availability = ShopOffer.Availability.OncePerDay, cooldownHours = 24,
                        contents = new List<ShopReward>
                        {
                            new ShopReward { kind = ShopReward.Kind.Coins, amount = 100 },
                            new ShopReward { kind = ShopReward.Kind.Life,  amount = 1 },
                        }
                    },
                    new ShopOffer
                    {
                        id = "starter_once", displayName = "Başlangıç Paketi", heroAmount = 1000,
                        priceType = ShopOffer.PriceType.Coins, priceAmount = 100,
                        availability = ShopOffer.Availability.OnceEver,
                        contents = new List<ShopReward>
                        {
                            new ShopReward { kind = ShopReward.Kind.Booster, booster = BoardController.BoosterMode.Single, amount = 10 },
                        }
                    }
                }
            }
        };
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    /// <summary>Mevcut (önceden üretilmiş) katalogda da bilinen mock tekliflerin uygunluğunu ayarlar.</summary>
    private static void PatchMockAvailability(ShopCatalog catalog)
    {
        if (catalog?.sections == null) return;
        bool changed = false;
        foreach (var section in catalog.sections)
        {
            if (section?.offers == null) continue;
            foreach (var offer in section.offers)
            {
                if (offer == null) continue;
                if (offer.id == "free_gift" && offer.availability != ShopOffer.Availability.OncePerDay)
                {
                    offer.availability = ShopOffer.Availability.OncePerDay;
                    offer.cooldownHours = 24; changed = true;
                }
                if (offer.id == "starter_once" && offer.availability != ShopOffer.Availability.OnceEver)
                {
                    offer.availability = ShopOffer.Availability.OnceEver; changed = true;
                }
            }
        }
        if (changed) { EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); }
    }

    // ===================================================================
    //  Prefab'lar
    // ===================================================================

    private static void BuildChipPrefab(UITheme theme)
    {
        var root = NewRect("ShopRewardChip", null);
        root.sizeDelta = new Vector2(72, 88);
        var chip = root.gameObject.AddComponent<ShopRewardChip>();

        var icon = NewImage("Icon", root, Color.white);
        AnchorBox(icon.rectTransform, new Vector2(0, 0.28f), Vector2.one);
        icon.preserveAspect = true;

        var label = NewText("Label", root, "x1", 26, theme.textOnCream, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(label.rectTransform, Vector2.zero, new Vector2(1, 0.30f));

        SetRef(chip, "icon", icon);
        SetRef(chip, "label", label);

        SaveAndCleanup(root.gameObject, ChipPath);
    }

    private static void BuildCardPrefab(UITheme theme, ShopRewardChip chipPrefab)
    {
        var root = NewRect("ShopOfferCard", null);
        root.sizeDelta = new Vector2(880, 260);
        var bg = root.gameObject.AddComponent<Image>();
        bg.color = theme.creamSurface;
        AddLayoutElement(root.gameObject, preferredHeight: 260);

        var card = root.gameObject.AddComponent<ShopOfferCard>();

        var vlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.spacing = 10;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        // --- Body: hero + chip grid
        var body = NewRect("Body", root);
        var bodyH = body.gameObject.AddComponent<HorizontalLayoutGroup>();
        bodyH.spacing = 16; bodyH.childControlWidth = bodyH.childControlHeight = true;
        bodyH.childForceExpandWidth = false; bodyH.childForceExpandHeight = true;
        AddLayoutElement(body.gameObject, preferredHeight: 150);

        var heroGroup = NewRect("Hero", body);
        AddLayoutElement(heroGroup.gameObject, preferredWidth: 180);
        var heroV = heroGroup.gameObject.AddComponent<VerticalLayoutGroup>();
        heroV.childControlWidth = heroV.childControlHeight = true;
        heroV.childForceExpandWidth = true; heroV.childForceExpandHeight = false;
        heroV.childAlignment = TextAnchor.MiddleCenter; heroV.spacing = 4;

        var heroIcon = NewImage("HeroIcon", heroGroup, Color.white);
        heroIcon.preserveAspect = true;
        AddLayoutElement(heroIcon.gameObject, preferredWidth: 120, preferredHeight: 110);
        var heroAmount = NewText("HeroAmount", heroGroup, "0", 34, theme.textOnCream, TextAlignmentOptions.Center, theme.headingFont);
        AddLayoutElement(heroAmount.gameObject, preferredHeight: 36);

        var chipContainer = NewRect("Chips", body);
        var grid = chipContainer.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(72, 88); grid.spacing = new Vector2(8, 8);
        grid.childAlignment = TextAnchor.MiddleCenter;
        var chipLE = AddLayoutElement(chipContainer.gameObject); chipLE.flexibleWidth = 1;

        // --- Price row: name + buy button
        var priceRow = NewRect("PriceRow", root);
        var rowH = priceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowH.spacing = 12; rowH.childControlWidth = rowH.childControlHeight = true;
        rowH.childForceExpandWidth = false; rowH.childForceExpandHeight = true;
        rowH.childAlignment = TextAnchor.MiddleCenter;
        AddLayoutElement(priceRow.gameObject, preferredHeight: 70);

        var nameText = NewText("Name", priceRow, "Teklif", 30, theme.textOnCream, TextAlignmentOptions.Left, theme.headingFont);
        var nameLE = AddLayoutElement(nameText.gameObject); nameLE.flexibleWidth = 1;

        var priceBtnImg = NewImage("PriceButton", priceRow, theme.priceGreen);
        var priceBtn = priceBtnImg.gameObject.AddComponent<Button>();
        priceBtn.targetGraphic = priceBtnImg;
        AddLayoutElement(priceBtnImg.gameObject, preferredWidth: 280, preferredHeight: 70);
        var priceText = NewText("PriceText", priceBtnImg.rectTransform, "0", 30, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(priceText.rectTransform, Vector2.zero, Vector2.one);

        SetRef(card, "cardBackground", bg);
        SetRef(card, "heroIcon", heroIcon);
        SetRef(card, "heroAmountText", heroAmount);
        SetRef(card, "chipContainer", chipContainer);
        SetRef(card, "chipPrefab", chipPrefab);
        SetRef(card, "nameText", nameText);
        SetRef(card, "priceButton", priceBtn);
        SetRef(card, "priceButtonBackground", priceBtnImg);
        SetRef(card, "priceText", priceText);

        SaveAndCleanup(root.gameObject, CardPath);
    }

    private static void BuildHeaderPrefab(UITheme theme)
    {
        var root = NewRect("ShopSectionHeader", null);
        root.sizeDelta = new Vector2(880, 72);
        var band = root.gameObject.AddComponent<Image>();
        band.color = theme.headerBand;
        AddLayoutElement(root.gameObject, preferredHeight: 72);

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
                                             ShopOfferCard cardPrefab, ShopSectionHeader headerPrefab)
    {
        // Paneli alt çubuğun hemen ÖNÜNE (aynı parent, sibling index = çubuk index'i) koy:
        // böylece panel diğer içeriği (world map/HUD) kapatır ama alt çubuk üstte/tıklanabilir kalır.
        var parent = bottomBar.parent;
        var panel = NewRect("ShopPanel", parent);
        panel.SetSiblingIndex(bottomBar.GetSiblingIndex());
        Stretch(panel);
        var pbg = panel.gameObject.AddComponent<Image>();
        pbg.color = theme.screenBackground;
        var ctrl = panel.gameObject.AddComponent<ShopScreenController>();

        // Top bar
        var top = NewRect("TopBar", panel);
        top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1);
        top.anchoredPosition = Vector2.zero; top.sizeDelta = new Vector2(0, 150);
        var topBg = top.gameObject.AddComponent<Image>(); topBg.color = theme.headerBand;
        var title = NewText("Title", top, "Mağaza", 48, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(title.rectTransform, Vector2.zero, Vector2.one);
        var coin = NewText("CoinBalance", top, "0", 36, theme.accentAmber, TextAlignmentOptions.Left, theme.headingFont);
        coin.rectTransform.anchorMin = new Vector2(0, 0.5f); coin.rectTransform.anchorMax = new Vector2(0, 0.5f);
        coin.rectTransform.pivot = new Vector2(0, 0.5f);
        coin.rectTransform.anchoredPosition = new Vector2(40, 0); coin.rectTransform.sizeDelta = new Vector2(220, 60);

        // Scroll view (top bar altı, alt çubuk üstü)
        var scroll = NewRect("ScrollView", panel);
        scroll.anchorMin = Vector2.zero; scroll.anchorMax = Vector2.one;
        scroll.offsetMin = new Vector2(0, 200); scroll.offsetMax = new Vector2(0, -160);
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;

        var viewport = NewRect("Viewport", scroll);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, 0);
        var cv = content.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.padding = new RectOffset(24, 24, 24, 24); cv.spacing = 20;
        cv.childControlWidth = cv.childControlHeight = true;
        cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
        cv.childAlignment = TextAnchor.UpperCenter;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewport; sr.content = content;

        // Satın alma toast'ı (alt ortada, başta kapalı)
        var toast = NewRect("PurchaseToast", panel);
        toast.anchorMin = new Vector2(0.5f, 0); toast.anchorMax = new Vector2(0.5f, 0); toast.pivot = new Vector2(0.5f, 0);
        toast.anchoredPosition = new Vector2(0, 220); toast.sizeDelta = new Vector2(560, 90);
        var toastBg = toast.gameObject.AddComponent<Image>(); toastBg.color = theme.ctaGreen;
        var toastText = NewText("Text", toast, "Satın alındı!", 32, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        AnchorBox(toastText.rectTransform, Vector2.zero, Vector2.one);
        toast.gameObject.SetActive(false);

        SetRef(ctrl, "catalog", catalog);
        SetRef(ctrl, "theme", theme);
        SetRef(ctrl, "contentContainer", content);
        SetRef(ctrl, "sectionHeaderPrefab", headerPrefab);
        SetRef(ctrl, "offerCardPrefab", cardPrefab);
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
