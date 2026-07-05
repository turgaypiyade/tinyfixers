using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liderlik Panosu ekranını tek tıkla kurar — Menü: TinyFixers > Mockup > Setup Leaderboard.
/// Royal Match düzeni: sekmeler + seçili sekmeyle KAYNAŞAN toggle bandı + zengin satır
/// anatomisi (madalya/rozet, çerçeveli avatar, Bölüm N, banner art, Kapasite/Puan bloğu).
/// Tüm görseller LeaderboardSkin.asset üzerinden değiştirilir (sprite'ları kullanıcı basar).
/// </summary>
public static class LeaderboardMockupSetup
{
    private const string PrefabDir = "Assets/_Project/Prefabs/UI/Leaderboard";
    private const string RowPath   = PrefabDir + "/LeaderboardRow.prefab";
    private const string SkinPath  = "Assets/_Project/Settings/LeaderboardSkin.asset";

    private static readonly LeaderboardTab[] Tabs =
        { LeaderboardTab.Weekly, LeaderboardTab.Friends, LeaderboardTab.Players, LeaderboardTab.Team };
    private static readonly string[] TabLabels = { "Haftalık", "Arkadaşlar", "Oyuncular", "Takım" };

    [MenuItem("TinyFixers/Mockup/Setup Leaderboard")]
    public static void Setup()
    {
        MockupUI.EnsureFolder(PrefabDir);
        var theme = MockupUI.EnsureTheme();
        var skin  = EnsureSkin();

        BuildRowPrefab(theme, skin);
        var rowAsset = AssetDatabase.LoadAssetAtPath<LeaderboardRow>(RowPath);

        var tab = MockupUI.FindTabController();
        if (tab == null)
        {
            EditorUtility.DisplayDialog("Leaderboard Setup", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var panel = BuildPanel(tab.transform, theme, skin, rowAsset);
        MockupUI.AssignTabPanel(tab, "Ranks", panel);

        EditorSceneManager.MarkSceneDirty(tab.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Leaderboard Setup",
            "Liderlik Panosu (RM düzeni) kuruldu.\nSprite'ları Settings/LeaderboardSkin.asset üzerinden bağla.\nSahneyi kaydet (Cmd+S).", "Tamam");
    }

    private static LeaderboardSkin EnsureSkin()
    {
        var skin = AssetDatabase.LoadAssetAtPath<LeaderboardSkin>(SkinPath);
        if (skin != null) return skin;

        MockupUI.EnsureFolder("Assets/_Project/Settings");
        skin = ScriptableObject.CreateInstance<LeaderboardSkin>();
        AssetDatabase.CreateAsset(skin, SkinPath);
        return skin;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Satır prefab'ı — RM anatomisi (manuel anchor'lar, layout group yok)
    // ─────────────────────────────────────────────────────────────────

    private static void BuildRowPrefab(UITheme theme, LeaderboardSkin skin)
    {
        float rowH = skin.rowHeight;

        var root = MockupUI.NewRect("LeaderboardRow", null);
        root.sizeDelta = new Vector2(880, rowH);
        var bg = root.gameObject.AddComponent<Image>();
        MockupUI.Card(bg, theme, theme.creamSurface);
        MockupUI.LayoutElem(root.gameObject, preferredHeight: rowH);
        var row = root.gameObject.AddComponent<LeaderboardRow>();

        // Rütbe rozeti (sol, dikey ortalı)
        var badge = MockupUI.NewImage("RankBadge", root, Color.clear);
        Place(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(skin.rankBadgeX, 0), new Vector2(skin.rankBadgeSize, skin.rankBadgeSize));
        var rank = MockupUI.NewText("Rank", badge.transform, "1", 34, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.Stretch(rank.rectTransform);

        // Avatar = ProfileScreen'deki AvatarCircle kopyası (çerçeve + daire mask), 150x150
        var avatar = MockupUI.BuildAvatarCircle("AvatarCircle", root, 150f, out var avatarRoot);
        Place(avatarRoot, new Vector2(0, 0.5f), new Vector2(skin.avatarX, 0), new Vector2(150f, 150f));

        // Bilgi bloğu: Bölüm N (üst), isim (orta), alt-isim (alt)
        float infoX = skin.infoX;
        var chapter = MockupUI.NewText("Chapter", root, "Bölüm 4401", 24, theme.headerBand, TextAlignmentOptions.Left, theme.headingFont);
        Place(chapter.rectTransform, new Vector2(0, 1), new Vector2(infoX, -10), new Vector2(240, 30), pivotY: 1);
        // İsim: SİYAH + BOLD + 10 punto büyük (30 → 40)
        var name = MockupUI.NewText("Name", root, "Oyuncu", 40, Color.black, TextAlignmentOptions.Left, theme.headingFont);
        name.fontStyle = FontStyles.Bold;
        Place(name.rectTransform, new Vector2(0, 0.5f), new Vector2(infoX, -4), new Vector2(300, 46));
        var sub = MockupUI.NewText("Subtitle", root, "alt", 20, theme.textSub, TextAlignmentOptions.Left, theme.bodyFont);
        Place(sub.rectTransform, new Vector2(0, 0), new Vector2(infoX, 8), new Vector2(300, 26), pivotY: 0);

        // Banner art (orta-sağ dekor; sprite yoksa gizli)
        var banner = MockupUI.NewImage("Banner", root, Color.white);
        banner.enabled = false;
        var brt = banner.rectTransform;
        brt.anchorMin = new Vector2(0.52f, 0.08f); brt.anchorMax = new Vector2(0.78f, 0.92f);
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

        // Sağ blok: Kapasite (takım) + Puan
        var capacityRoot = MockupUI.NewRect("CapacityRoot", root);
        Place(capacityRoot, new Vector2(1, 0.5f), new Vector2(-208, 0), new Vector2(130, 96), pivotX: 1);
        var capLabel = MockupUI.NewText("CapacityLabel", capacityRoot, "Kapasite", 20, new Color(0.72f, 0.58f, 0.45f), TextAlignmentOptions.Center, theme.headingFont);
        Place(capLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(130, 26), pivotY: 1);
        var capChip = MockupUI.NewSlicedImage("CapacityChip", capacityRoot, theme.cardBackground, new Color(0.92f, 0.86f, 0.74f));
        Place(capChip.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(120, 44), pivotY: 0);
        var capText = MockupUI.NewText("CapacityText", capChip.transform, "49/50", 24, theme.textOnCream, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.Stretch(capText.rectTransform);

        // Hediye kutusu (yalnız Haftalık top-3'te görünür; Bind yönetir)
        var gift = MockupUI.NewImage("GiftIcon", root, Color.white);
        gift.enabled = false;
        gift.raycastTarget = false;
        Place(gift.rectTransform, new Vector2(1, 0.5f), new Vector2(-210, 0), new Vector2(skin.giftIconSize, skin.giftIconSize), pivotX: 1);

        // Puan bloğu (etiket + sayı) İKİSİ DE sağ-MERKEZE ankrajlı → satır yüksekliği
        // değişse de (büyük top-3 kartı vs küçük satır) birlikte, sabit blok olarak durur.
        // Eskiden etiket üst-kenara, sayı alt-kenara bağlıydı → yükseklik değişince kayıyordu.
        float pad = skin.scoreRightPad;
        var scoreLabel = MockupUI.NewText("ScoreLabel", root, "Puan", 22, theme.headerBand, TextAlignmentOptions.Right, theme.headingFont);
        Place(scoreLabel.rectTransform, new Vector2(1, 0.5f), new Vector2(-pad, 24), new Vector2(150, 28), pivotX: 1, pivotY: 0.5f);
        var score = MockupUI.NewText("Score", root, "241103", 30, theme.headerBand, TextAlignmentOptions.Right, theme.headingFont);
        Place(score.rectTransform, new Vector2(1, 0.5f), new Vector2(-pad, -16), new Vector2(170, 36), pivotX: 1, pivotY: 0.5f);

        // Bağla
        MockupUI.SetRef(row, "rowBackground", bg);
        MockupUI.SetRef(row, "rankBadge", badge);
        MockupUI.SetRef(row, "rankText", rank);
        // avatarFrame bilerek BAĞLANMAZ (null) → Bind çerçeveye dokunmaz, ProfileAvatarBG durur.
        MockupUI.SetRef(row, "avatar", avatar);
        MockupUI.SetRef(row, "chapterText", chapter);
        MockupUI.SetRef(row, "nameText", name);
        MockupUI.SetRef(row, "subtitleText", sub);
        MockupUI.SetRef(row, "bannerImage", banner);
        MockupUI.SetRef(row, "giftIcon", gift);
        MockupUI.SetRef(row, "capacityRoot", capacityRoot.gameObject);
        MockupUI.SetRef(row, "capacityLabel", capLabel);
        MockupUI.SetRef(row, "capacityChip", capChip);
        MockupUI.SetRef(row, "capacityText", capText);
        MockupUI.SetRef(row, "scoreLabel", scoreLabel);
        MockupUI.SetRef(row, "scoreText", score);

        MockupUI.SaveAndLoadPrefab<LeaderboardRow>(root.gameObject, RowPath);
    }

    // Kısa yerleşim yardımcısı: tek anchor noktası + pozisyon + boyut.
    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size,
                              float pivotX = -1, float pivotY = -1)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(pivotX >= 0 ? pivotX : (anchor.x == 1 ? 1 : anchor.x == 0 ? 0 : 0.5f),
                               pivotY >= 0 ? pivotY : 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Panel — sekmeler + bağlı bant + toggle + liste + pinli self
    // ─────────────────────────────────────────────────────────────────

    private static GameObject BuildPanel(Transform bottomBar, UITheme theme, LeaderboardSkin skin, LeaderboardRow rowPrefab)
    {
        var panel = MockupUI.BuildScreenPanel(bottomBar, "LeaderboardPanel", theme, "Liderlik Panosu", out var body);
        var ctrl = panel.AddComponent<LeaderboardScreenController>();

        // Başlığı çentik/safe-area altına it (skin.titleTopOffset).
        var topBar = panel.transform.Find("TopBar") as RectTransform;
        if (topBar != null)
            topBar.anchoredPosition = new Vector2(0, -skin.titleTopOffset);

        // Zaman çipi (sol üst)
        var timeChip = MockupUI.NewSlicedImage("TimeChip", body, theme.cardBackground, theme.goldTrim);
        Place(timeChip.rectTransform, new Vector2(0, 1), skin.timerChipPos, skin.timerChipSize, pivotX: 0, pivotY: 1);
        var time = MockupUI.NewText("TimeLabel", timeChip.transform, "2g 20s", 24, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.Stretch(time.rectTransform);

        // RM z-sırası: ÖNCE sekmeler (arkada), SONRA bant (üstlerine biner) —
        // sekme dipleri bandın ALTINA girer, bandın üst çizgisi pasif sekmelerin
        // üzerinden geçer. Kaynaşmayı yalnız AKTİF sekmedeki yama yapar.
        var tabsBar = MockupUI.NewRect("Tabs", body);
        TopAnchor(tabsBar, height: skin.tabsHeight, y: skin.tabsTopY);

        var buttons = new Button[Tabs.Length];
        var tabBgs  = new Image[Tabs.Length];
        var tabRects = new RectTransform[Tabs.Length];
        float tabW = 1f / Tabs.Length;
        float halfGap = skin.tabGap * 0.5f;
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tabRect = MockupUI.NewRect("Tab_" + Tabs[i], tabsBar);
            tabRect.anchorMin = new Vector2(i * tabW, 0);
            tabRect.anchorMax = new Vector2((i + 1) * tabW, 1);
            // Px bazlı boşluklar: dış kenarlara side-margin, sekme aralarına gap.
            tabRect.offsetMin = new Vector2(i == 0 ? skin.tabsSideMargin : halfGap, 0);
            tabRect.offsetMax = new Vector2(i == Tabs.Length - 1 ? -skin.tabsSideMargin : -halfGap, 0);

            var tabBg = tabRect.gameObject.AddComponent<Image>();
            MockupUI.Card(tabBg, theme, theme.screenBackground);
            var btn = tabRect.gameObject.AddComponent<Button>();
            btn.targetGraphic = tabBg;

            // Yazı ÜSTE dayalı: sekmenin alt kısmı bandın altına girdiği için
            // ortalama yazıyı bandın arkasında bırakır — üstten hizala.
            var lbl = MockupUI.NewText("Label", tabRect, TabLabels[i], 27, theme.textLight, TextAlignmentOptions.Top, theme.headingFont);
            MockupUI.AnchorBox(lbl.rectTransform, Vector2.zero, Vector2.one);
            lbl.rectTransform.offsetMin = new Vector2(0, 14);
            lbl.rectTransform.offsetMax = new Vector2(0, -skin.tabLabelTopPadding);

            buttons[i] = btn; tabBgs[i] = tabBg; tabRects[i] = tabRect;
        }

        // Bant — sekmelerden SONRA yaratılır → üstlerine çizilir (sekme dipleri altında kalır).
        var band = MockupUI.NewImage("ConnectedBand", body, theme.panelSurface);
        band.raycastTarget = false;   // altında kalan sekme tıklamalarını yutmasın
        TopAnchor(band.rectTransform, height: skin.bandHeight, y: skin.bandTopY);
        // Yanlardan ekran DIŞINA taşır → sprite'ın kenar bevel'leri görünmez, bant tam kaplar.
        band.rectTransform.sizeDelta = new Vector2(skin.bandSideOverflow * 2f, skin.bandHeight);

        // Alt-toggle hapları (bandın içinde, ortalanmış iki hap)
        var toggleButtons = new Button[2];
        var toggleBgs = new Image[2];
        var toggleLbls = new TMP_Text[2];
        for (int i = 0; i < 2; i++)
        {
            var pill = MockupUI.NewSlicedImage("Toggle_" + i, band.transform, theme.buttonBackground,
                i == 0 ? theme.accentAmber : theme.screenBackground);
            Place(pill.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(i == 0 ? -skin.togglePillSpread : skin.togglePillSpread, -4f), skin.togglePillSize);
            var pillBtn = pill.gameObject.AddComponent<Button>();
            pillBtn.targetGraphic = pill;
            var pillLbl = MockupUI.NewText("Label", pill.transform, i == 0 ? "Dünya" : "Türkiye", 26,
                theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
            MockupUI.Stretch(pillLbl.rectTransform);

            toggleButtons[i] = pillBtn; toggleBgs[i] = pill; toggleLbls[i] = pillLbl;
        }

        // Dikiş yaması — bandın ÇOCUĞU (bandın üstüne çizilir); controller aktif
        // sekmenin X aralığına hizalayıp bandın üst kenarına ortalar (başta kapalı).
        var seam = MockupUI.NewImage("SeamCover", band.transform, theme.panelSurface);
        seam.raycastTarget = false;
        seam.gameObject.SetActive(false);

        // Pinli kendi satırın (ALTTA — RM'deki gibi) + üstünde liste
        var selfGO = (GameObject)PrefabUtility.InstantiatePrefab(rowPrefab.gameObject, body);
        var selfRT = (RectTransform)selfGO.transform;
        MockupUI.AnchorBottom(selfRT, height: skin.rowHeight, y: 4);
        var selfRow = selfGO.GetComponent<LeaderboardRow>();

        var listArea = MockupUI.NewRect("ListArea", body);
        listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one;
        // Liste alt kenarı, pinli self row'un ÜSTÜNDE bitmeli — yoksa kayan satırlar
        // self row bandına girer. En az (rowHeight + pay) kadar boşluk bırak.
        float listBottom = Mathf.Max(skin.listBottomOffset, skin.rowHeight + 16f);
        listArea.offsetMin = new Vector2(0, listBottom);
        listArea.offsetMax = new Vector2(0, -skin.listTopOffset);
        var content = MockupUI.BuildVerticalScroll(listArea);

        // Controller bağlama
        MockupUI.SetRef(ctrl, "theme", theme);
        MockupUI.SetRef(ctrl, "skin", skin);
        MockupUI.SetRef(ctrl, "contentContainer", content);
        MockupUI.SetRef(ctrl, "rowPrefab", rowPrefab);
        MockupUI.SetRef(ctrl, "selfRow", selfRow);
        MockupUI.SetRef(ctrl, "timeLabelText", time);
        MockupUI.SetRef(ctrl, "timeChip", timeChip);
        MockupUI.SetRef(ctrl, "connectedBand", band);
        MockupUI.SetRef(ctrl, "seamCover", seam);

        // Ekran zemini + üretilen başlık bandı (skin.screenBackground atanınca bant gizlenir).
        MockupUI.SetRef(ctrl, "panelBackground", panel.GetComponent<Image>());
        var topBarTr = panel.transform.Find("TopBar");
        if (topBarTr != null)
            MockupUI.SetRef(ctrl, "titleBand", topBarTr.GetComponent<Image>());
        MockupUI.SetRefArray(ctrl, "toggleButtons", toggleButtons);
        MockupUI.SetRefArray(ctrl, "toggleBackgrounds", toggleBgs);
        MockupUI.SetRefArray(ctrl, "toggleLabels", toggleLbls);
        MockupUI.SetRefArray(ctrl, "avatarPool", MockupUI.AvatarPool());
        AssignTabButtons(ctrl, buttons, tabBgs, tabRects);

        return panel;
    }

    private static void AssignTabButtons(LeaderboardScreenController ctrl, Button[] buttons, Image[] bgs, RectTransform[] rects)
    {
        var so = new SerializedObject(ctrl);
        var arr = so.FindProperty("tabButtons");
        arr.arraySize = Tabs.Length;
        for (int i = 0; i < Tabs.Length; i++)
        {
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("tab").enumValueIndex = (int)Tabs[i];
            el.FindPropertyRelative("button").objectReferenceValue = buttons[i];
            el.FindPropertyRelative("background").objectReferenceValue = bgs[i];
            el.FindPropertyRelative("rect").objectReferenceValue = rects[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void TopAnchor(RectTransform rt, float height, float y)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -y); rt.sizeDelta = new Vector2(0, height);
    }
}
