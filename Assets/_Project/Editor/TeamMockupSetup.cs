using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takım ekranını tek tıkla kurar — Menü: TinyFixers > Mockup > Setup Team.
/// SADE sohbet: gelen mesaj solda, benimki sağda; üst bilgi (amblem+isim+üye);
/// altta "Can İste"/"Mesaj"; Mesaj'a basınca bottom bar üstünde tek satır input.
/// (Event/hediye/görev YOK.) "Teams" sekmesine bağlar.
/// </summary>
public static class TeamMockupSetup
{
    private const string PrefabDir = "Assets/_Project/Prefabs/UI/Team";
    private const string ChatPath  = PrefabDir + "/TeamChatRow.prefab";
    private const string BrowserRowPath = PrefabDir + "/TeamBrowserRow.prefab";

    /// <summary>
    /// SADECE takımsız-durum ekranını (Ara/Oluştur) SAHNEDEKİ MEVCUT TeamPanel'e ekler —
    /// paneli yeniden KURMAZ, elle basılmış sprite/görsellere DOKUNMAZ.
    /// Mevcut sohbet içeriği "InTeamRoot" altına taşınır (görünümü değişmez).
    /// </summary>
    [MenuItem("TinyFixers/Mockup/Ekle - Takim Ara-Olustur (Team'i BOZMAZ)")]
    public static void AddTeamBrowserOnly()
    {
        var ctrl = Object.FindFirstObjectByType<TeamScreenController>(FindObjectsInactive.Include);
        if (ctrl == null)
        {
            EditorUtility.DisplayDialog("Takım Ara/Oluştur",
                "Sahnede TeamPanel bulunamadı. MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var theme = MockupUI.EnsureTheme();

        var body = ctrl.transform.Find("Body") as RectTransform;
        if (body == null)
        {
            EditorUtility.DisplayDialog("Takım Ara/Oluştur",
                "Panelde Body bulunamadı — panel eski kurulumdan farklı görünüyor.", "Tamam");
            return;
        }

        // Tarayıcı satırı prefab'ı: YOKSA üret; varsa aynen kullan.
        var browserRowAsset = AssetDatabase.LoadAssetAtPath<TeamBrowserRow>(BrowserRowPath);
        if (browserRowAsset == null)
        {
            MockupUI.EnsureFolder(PrefabDir);
            BuildBrowserRowPrefab(theme);
            browserRowAsset = AssetDatabase.LoadAssetAtPath<TeamBrowserRow>(BrowserRowPath);
        }

        // Mevcut sohbet içeriğini InTeamRoot altına grupla (bir kez; görsel yerleşim
        // değişmez — InTeamRoot Body ile birebir aynı alana yayılır).
        var inTeam = body.Find("InTeamRoot") as RectTransform;
        if (inTeam == null)
        {
            inTeam = MockupUI.NewRect("InTeamRoot", body);
            MockupUI.Stretch(inTeam);

            var toMove = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in body)
                if (child != inTeam && child.name != "NoTeamRoot")
                    toMove.Add(child);
            foreach (var child in toMove)
                child.SetParent(inTeam, false);   // lokal anchor'lar aynı alanda geçerli kalır
        }

        // Idempotent: önceki çalıştırmanın NoTeamRoot'unu temizle.
        var oldRoot = body.Find("NoTeamRoot");
        if (oldRoot != null) Object.DestroyImmediate(oldRoot.gameObject);

        var defaultEmblem = MockupUI.LoadSprite("Assets/_Project/Art/UI/ProfileUI/TeamIcon.png");
        var emblemPool = defaultEmblem != null ? new[] { defaultEmblem } : new Sprite[0];
        var browser = BuildNoTeamRoot(body, theme, browserRowAsset, emblemPool);

        MockupUI.SetRef(ctrl, "inTeamRoot", inTeam.gameObject);
        MockupUI.SetRef(ctrl, "browser", browser);
        MockupUI.SetRefArray(ctrl, "emblemPool", emblemPool);

        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Takım Ara/Oluştur",
            "Ara/Oluştur ekranı MEVCUT TeamPanel'e eklendi; sohbet içeriğine ve görsellere dokunulmadı.\nSahneyi kaydet (Cmd+S).", "Tamam");
    }

    [MenuItem("TinyFixers/Mockup/Setup Team")]
    public static void Setup()
    {
        MockupUI.EnsureFolder(PrefabDir);
        var theme = MockupUI.EnsureTheme();

        BuildChatRowPrefab(theme);
        var chatAsset = AssetDatabase.LoadAssetAtPath<TeamChatRow>(ChatPath);

        BuildBrowserRowPrefab(theme);
        var browserRowAsset = AssetDatabase.LoadAssetAtPath<TeamBrowserRow>(BrowserRowPath);

        var tab = MockupUI.FindTabController();
        if (tab == null)
        {
            EditorUtility.DisplayDialog("Team Setup", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var panel = BuildPanel(tab.transform, theme, chatAsset, browserRowAsset);
        MockupUI.AssignTabPanel(tab, "Teams", panel);

        EditorSceneManager.MarkSceneDirty(tab.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Team Setup",
            "Takım kuruldu: takımlıyken sohbet, takımsızken Ara/Oluştur.\nAmblem sprite'larını TeamScreenController + TeamBrowserController'daki emblemPool'a bağla, Cmd+S.", "Tamam");
    }

    // ── Sohbet satırı: avatar + baloncuk (sol/sağ runtime'da) ──────────
    private static void BuildChatRowPrefab(UITheme theme)
    {
        var root = MockupUI.NewRect("TeamChatRow", null);
        root.sizeDelta = new Vector2(900, 150);
        MockupUI.LayoutElem(root.gameObject, preferredHeight: 150);
        var h = MockupUI.HLayout(root.gameObject, 12);       // childForceExpandWidth zaten false
        h.padding = new RectOffset(8, 8, 8, 8);
        h.childAlignment = TextAnchor.UpperLeft;
        var row = root.gameObject.AddComponent<TeamChatRow>();

        // Avatar = ProfileScreen AvatarCircle kopyası (messenger boyu 110)
        var avatar = MockupUI.BuildAvatarCircle("AvatarCircle", root, 110f, out var avatarRoot);
        MockupUI.LayoutElem(avatarRoot.gameObject, preferredWidth: 110, preferredHeight: 110);

        // Baloncuk: SABİT genişlik kapak (flexible YOK) → sola/sağa yaslanabilsin
        var bubble = MockupUI.NewRect("Bubble", root);
        var bubbleImg = bubble.gameObject.AddComponent<Image>();
        MockupUI.Card(bubbleImg, theme, theme.creamSurface);
        var bubbleLE = MockupUI.LayoutElem(bubble.gameObject);
        bubbleLE.preferredWidth = 660; bubbleLE.flexibleWidth = 0;
        var bv = MockupUI.VLayout(bubble.gameObject, 12, 2); bv.childAlignment = TextAnchor.UpperLeft;

        var topRow = MockupUI.NewRect("TopRow", bubble);
        MockupUI.LayoutElem(topRow.gameObject, preferredHeight: 34);
        MockupUI.HLayout(topRow.gameObject, 8);
        var sender = MockupUI.NewText("Sender", topRow, "Oyuncu", 30, theme.headerBand, TextAlignmentOptions.Left, theme.headingFont);
        sender.fontStyle = FontStyles.Bold;
        var senderLE = MockupUI.LayoutElem(sender.gameObject); senderLE.flexibleWidth = 1;
        var time = MockupUI.NewText("Time", topRow, "3g", 20, theme.textSub, TextAlignmentOptions.Right, theme.bodyFont);
        MockupUI.LayoutElem(time.gameObject, preferredWidth: 120);

        var message = MockupUI.NewText("Message", bubble, "mesaj", 26, theme.textOnCream, TextAlignmentOptions.Left, theme.bodyFont);
        message.textWrappingMode = TextWrappingModes.Normal;
        MockupUI.LayoutElem(message.gameObject, preferredHeight: 40);

        MockupUI.SetRef(row, "layout", h);
        MockupUI.SetRef(row, "bubble", bubbleImg);
        MockupUI.SetRef(row, "avatar", avatar);
        MockupUI.SetRef(row, "senderText", sender);
        MockupUI.SetRef(row, "messageText", message);
        MockupUI.SetRef(row, "timeText", time);

        MockupUI.SaveAndLoadPrefab<TeamChatRow>(root.gameObject, ChatPath);
    }

    // ── Takım tarayıcı satırı: amblem + isim + Kapasite + "Takım Bilgisi" ──
    private static void BuildBrowserRowPrefab(UITheme theme)
    {
        const float rowH = 150f;

        var root = MockupUI.NewRect("TeamBrowserRow", null);
        root.sizeDelta = new Vector2(880, rowH);
        var bg = root.gameObject.AddComponent<Image>();
        MockupUI.Card(bg, theme, theme.creamSurface);
        MockupUI.LayoutElem(root.gameObject, preferredHeight: rowH);
        var row = root.gameObject.AddComponent<TeamBrowserRow>();

        var emblem = MockupUI.NewImage("Emblem", root, Color.white);
        emblem.preserveAspect = true;
        PlaceAt(emblem.rectTransform, new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(108, 108), pivotX: 0);

        var name = MockupUI.NewText("Name", root, "Takım", 36, Color.black, TextAlignmentOptions.Left, theme.headingFont);
        name.fontStyle = FontStyles.Bold;
        PlaceAt(name.rectTransform, new Vector2(0, 0.5f), new Vector2(152, 0), new Vector2(300, 44), pivotX: 0);

        var capLabel = MockupUI.NewText("CapacityLabel", root, "Kapasite", 20,
            new Color(0.72f, 0.58f, 0.45f), TextAlignmentOptions.Center, theme.headingFont);
        PlaceAt(capLabel.rectTransform, new Vector2(1, 0.5f), new Vector2(-268, 34), new Vector2(130, 26), pivotX: 1);
        var capChip = MockupUI.NewSlicedImage("CapacityChip", root, theme.cardBackground, new Color(0.92f, 0.86f, 0.74f));
        PlaceAt(capChip.rectTransform, new Vector2(1, 0.5f), new Vector2(-272, -12), new Vector2(120, 44), pivotX: 1);
        var capText = MockupUI.NewText("CapacityText", capChip.transform, "41/50", 24, theme.textOnCream, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.Stretch(capText.rectTransform);

        var infoBtn = MockupUI.GlossyButton(root, MockupBeautifyTool.BlueBtnPath, theme.accentAmber,
            "Takım Bilgisi", 24, theme.headingFont, out _);
        var ibrt = ((Image)infoBtn.targetGraphic).rectTransform;
        PlaceAt(ibrt, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(240, 92), pivotX: 1);

        MockupUI.SetRef(row, "background", bg);
        MockupUI.SetRef(row, "emblem", emblem);
        MockupUI.SetRef(row, "nameText", name);
        MockupUI.SetRef(row, "capacityText", capText);
        MockupUI.SetRef(row, "infoButton", infoBtn);

        MockupUI.SaveAndLoadPrefab<TeamBrowserRow>(root.gameObject, BrowserRowPath);
    }

    private static void PlaceAt(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size,
                                float pivotX = 0.5f, float pivotY = 0.5f)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(pivotX, pivotY);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // ── Panel ──────────────────────────────────────────────────────────
    private static GameObject BuildPanel(Transform bottomBar, UITheme theme, TeamChatRow chatPrefab, TeamBrowserRow browserRowPrefab)
    {
        var panel = MockupUI.BuildScreenPanel(bottomBar, "TeamPanel", theme, "Takım", out var outerBody);
        var ctrl = panel.AddComponent<TeamScreenController>();

        // Takım İÇİ görünümün kökü — takımsızken kapatılır (yerine NoTeamRoot).
        var body = MockupUI.NewRect("InTeamRoot", outerBody);
        MockupUI.Stretch(body);

        // Üst bilgi kartı: amblem + isim + üye sayısı
        var header = MockupUI.NewRect("Header", body);
        MockupUI.AnchorTop(header, height: 150, y: 0);
        var headerImg = header.gameObject.AddComponent<Image>();
        MockupUI.Card(headerImg, theme, theme.panelSurface);
        var emblem = MockupUI.NewImage("Emblem", header, Color.white);
        emblem.preserveAspect = true;
        emblem.rectTransform.anchorMin = new Vector2(0, 0.5f); emblem.rectTransform.anchorMax = new Vector2(0, 0.5f);
        emblem.rectTransform.pivot = new Vector2(0, 0.5f);
        emblem.rectTransform.anchoredPosition = new Vector2(24, 0); emblem.rectTransform.sizeDelta = new Vector2(110, 110);
        var teamName = MockupUI.NewText("TeamName", header, "Takım", 38, theme.textLight, TextAlignmentOptions.Left, theme.headingFont);
        teamName.rectTransform.anchorMin = new Vector2(0, 0.5f); teamName.rectTransform.anchorMax = new Vector2(1, 0.5f);
        teamName.rectTransform.pivot = new Vector2(0, 0.5f);
        teamName.rectTransform.anchoredPosition = new Vector2(160, 16); teamName.rectTransform.sizeDelta = new Vector2(-200, 60);
        var memberCount = MockupUI.NewText("MemberCount", header, "40/50", 24, theme.textSub, TextAlignmentOptions.Left, theme.bodyFont);
        memberCount.rectTransform.anchorMin = new Vector2(0, 0.5f); memberCount.rectTransform.anchorMax = new Vector2(0, 0.5f);
        memberCount.rectTransform.pivot = new Vector2(0, 0.5f);
        memberCount.rectTransform.anchoredPosition = new Vector2(162, -30); memberCount.rectTransform.sizeDelta = new Vector2(220, 36);

        // Sohbet akışı — header altı, alt butonların üstü
        var feedArea = MockupUI.NewRect("FeedArea", body);
        MockupUI.AnchorFill(feedArea, topOffset: 170, bottomOffset: 110);
        var content = MockupUI.BuildVerticalScroll(feedArea);
        var scroll = feedArea.GetComponentInChildren<ScrollRect>();

        // Mesaj input satırı (başta KAPALI) — alt butonların hemen üstü
        var inputRoot = MockupUI.NewRect("MessageInput", body);
        MockupUI.AnchorBottom(inputRoot, height: 84, y: 96);
        var inputBgFull = inputRoot.gameObject.AddComponent<Image>();
        MockupUI.Card(inputBgFull, theme, new Color(0, 0, 0, 0.25f));   // yarı saydam şerit
        var inH = MockupUI.HLayout(inputRoot.gameObject, 10); inH.padding = new RectOffset(16, 16, 6, 6);
        inH.childForceExpandWidth = false;
        var field = BuildInputField(inputRoot, theme);
        var fieldLE = MockupUI.LayoutElem(field.gameObject); fieldLE.flexibleWidth = 1; fieldLE.preferredHeight = 68;
        var postBtn = MockupUI.GlossyButton(inputRoot, MockupBeautifyTool.GreenBtnPath, theme.ctaGreen,
            "Gönder", 26, theme.headingFont, out _);
        MockupUI.LayoutElem(((Image)postBtn.targetGraphic).gameObject, preferredWidth: 170, preferredHeight: 68);
        inputRoot.gameObject.SetActive(false);

        // Alt butonlar: Can İste / Mesaj
        var buttonsRow = MockupUI.NewRect("Buttons", body);
        MockupUI.AnchorBottom(buttonsRow, height: 90, y: 0);
        var bh = MockupUI.HLayout(buttonsRow.gameObject, 16); bh.padding = new RectOffset(16, 16, 8, 8);
        bh.childForceExpandWidth = true;
        var reqBtn = MockupUI.GlossyButton(buttonsRow, MockupBeautifyTool.GreenBtnPath, theme.ctaGreen,
            "Can İste", 28, theme.headingFont, out _);
        var msgBtn = MockupUI.GlossyButton(buttonsRow, MockupBeautifyTool.BlueBtnPath, theme.infoBlue,
            "Mesaj", 28, theme.headingFont, out _);

        // ── Takımsız görünüm: Ara/Oluştur tarayıcısı ─────────────────
        var defaultEmblem = MockupUI.LoadSprite("Assets/_Project/Art/UI/ProfileUI/TeamIcon.png");
        var emblemPool = defaultEmblem != null ? new[] { defaultEmblem } : new Sprite[0];
        var browser = BuildNoTeamRoot(outerBody, theme, browserRowPrefab, emblemPool);

        // Wiring
        MockupUI.SetRef(ctrl, "theme", theme);
        MockupUI.SetRef(ctrl, "emblemImage", emblem);
        MockupUI.SetRef(ctrl, "defaultEmblem", defaultEmblem);
        MockupUI.SetRefArray(ctrl, "avatarPool", MockupUI.AvatarPool());
        MockupUI.SetRefArray(ctrl, "emblemPool", emblemPool);
        MockupUI.SetRef(ctrl, "teamNameText", teamName);
        MockupUI.SetRef(ctrl, "memberCountText", memberCount);
        MockupUI.SetRef(ctrl, "contentContainer", content);
        MockupUI.SetRef(ctrl, "chatRowPrefab", chatPrefab);
        MockupUI.SetRef(ctrl, "scrollRect", scroll);
        MockupUI.SetRef(ctrl, "requestLifeButton", reqBtn);
        MockupUI.SetRef(ctrl, "messageButton", msgBtn);
        MockupUI.SetRef(ctrl, "messageInputRoot", inputRoot.gameObject);
        MockupUI.SetRef(ctrl, "messageInput", field);
        MockupUI.SetRef(ctrl, "messagePostButton", postBtn);
        MockupUI.SetRef(ctrl, "inTeamRoot", body.gameObject);
        MockupUI.SetRef(ctrl, "browser", browser);

        return panel;
    }

    // Takımsız durum ekranı (referans RM): üstte Ara/Oluştur sekmeleri;
    // Ara = arama satırı + takım listesi (+Takım Bilgisi popup'ı), Oluştur = form.
    private static TeamBrowserController BuildNoTeamRoot(RectTransform outerBody, UITheme theme,
        TeamBrowserRow rowPrefab, Sprite[] emblemPool)
    {
        var root = MockupUI.NewRect("NoTeamRoot", outerBody);
        MockupUI.Stretch(root);
        var browser = root.gameObject.AddComponent<TeamBrowserController>();

        // ── Sekmeler: Ara / Oluştur ──────────────────────────────────
        var tabsBar = MockupUI.NewRect("Tabs", root);
        MockupUI.AnchorTop(tabsBar, height: 84, y: 0);
        var searchTabBg = MockupUI.NewSlicedImage("Tab_Ara", tabsBar, theme.cardBackground, theme.accentAmber);
        MockupUI.AnchorBox(searchTabBg.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.49f, 1f));
        var searchTabBtn = searchTabBg.gameObject.AddComponent<Button>();
        searchTabBtn.targetGraphic = searchTabBg;
        var searchTabLbl = MockupUI.NewText("Label", searchTabBg.transform, "Ara", 30, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.Stretch(searchTabLbl.rectTransform);
        var createTabBg = MockupUI.NewSlicedImage("Tab_Olustur", tabsBar, theme.cardBackground, theme.screenBackground);
        MockupUI.AnchorBox(createTabBg.rectTransform, new Vector2(0.51f, 0f), new Vector2(0.96f, 1f));
        var createTabBtn = createTabBg.gameObject.AddComponent<Button>();
        createTabBtn.targetGraphic = createTabBg;
        var createTabLbl = MockupUI.NewText("Label", createTabBg.transform, "Oluştur", 30, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.Stretch(createTabLbl.rectTransform);

        // ── ARA kökü ─────────────────────────────────────────────────
        var searchRoot = MockupUI.NewRect("SearchRoot", root);
        MockupUI.AnchorFill(searchRoot, topOffset: 96, bottomOffset: 0);

        var searchRow = MockupUI.NewRect("SearchRow", searchRoot);
        MockupUI.AnchorTop(searchRow, height: 92, y: 0);
        searchRow.offsetMin = new Vector2(20, searchRow.offsetMin.y);
        searchRow.offsetMax = new Vector2(-20, searchRow.offsetMax.y);
        var searchInput = MockupUI.BuildInputField("SearchInput", searchRow, theme, "Takım adını yaz...", 28);
        var sirt = ((Image)searchInput.targetGraphic).rectTransform;
        sirt.anchorMin = new Vector2(0, 0); sirt.anchorMax = new Vector2(1, 1);
        sirt.offsetMin = Vector2.zero; sirt.offsetMax = new Vector2(-330, 0);
        var clearBtn = MockupUI.GlossyButton(searchRow, MockupBeautifyTool.BlueBtnPath, theme.screenBackground,
            "✕", 30, theme.headingFont, out _);
        var clrt = ((Image)clearBtn.targetGraphic).rectTransform;
        PlaceAt(clrt, new Vector2(1, 0.5f), new Vector2(-236, 0), new Vector2(84, 84), pivotX: 1);
        var searchBtn = MockupUI.GlossyButton(searchRow, MockupBeautifyTool.GreenBtnPath, theme.ctaGreen,
            "Ara", 30, theme.headingFont, out _);
        var srt = ((Image)searchBtn.targetGraphic).rectTransform;
        PlaceAt(srt, new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(220, 92), pivotX: 1);

        var resultArea = MockupUI.NewRect("ResultArea", searchRoot);
        MockupUI.AnchorFill(resultArea, topOffset: 108, bottomOffset: 0);
        var resultContent = MockupUI.BuildVerticalScroll(resultArea);

        // ── OLUŞTUR kökü (form) ──────────────────────────────────────
        var createRoot = MockupUI.NewRect("CreateRoot", root);
        MockupUI.AnchorFill(createRoot, topOffset: 96, bottomOffset: 0);
        var formCard = MockupUI.NewImage("FormCard", createRoot, Color.white);
        MockupUI.Card(formCard, theme, theme.panelSurface);
        MockupUI.Stretch(formCard.rectTransform);
        formCard.rectTransform.offsetMin = new Vector2(20, 20);
        formCard.rectTransform.offsetMax = new Vector2(-20, -6);
        var form = formCard.rectTransform;

        TMP_Text FormLabel(string text, float y, float height = 44)
        {
            var lbl = MockupUI.NewText("Lbl_" + text, form, text, 32, theme.textLight, TextAlignmentOptions.Left, theme.headingFont);
            lbl.fontStyle = FontStyles.Bold;
            PlaceAt(lbl.rectTransform, new Vector2(0, 1), new Vector2(34, -y), new Vector2(300, height), pivotX: 0, pivotY: 1);
            return lbl;
        }

        // Takım Adı
        FormLabel("Takım Adı:", 34);
        var nameInput = MockupUI.BuildInputField("NameInput", form, theme, "Takım adını yaz...", 28);
        var nirt = ((Image)nameInput.targetGraphic).rectTransform;
        PlaceAt(nirt, new Vector2(1, 1), new Vector2(-34, -24), new Vector2(430, 84), pivotX: 1, pivotY: 1);

        // Takım Amblemi
        FormLabel("Takım Amblemi:", 158);
        var emblemImg = MockupUI.NewImage("EmblemPreview", form, Color.white);
        emblemImg.preserveAspect = true;
        PlaceAt(emblemImg.rectTransform, new Vector2(1, 1), new Vector2(-300, -140), new Vector2(110, 110), pivotX: 1, pivotY: 1);
        var browseBtn = MockupUI.GlossyButton(form, MockupBeautifyTool.BlueBtnPath, theme.accentAmber,
            "Gözat", 28, theme.headingFont, out _);
        var brt = ((Image)browseBtn.targetGraphic).rectTransform;
        PlaceAt(brt, new Vector2(1, 1), new Vector2(-34, -150), new Vector2(230, 90), pivotX: 1, pivotY: 1);

        // Açıklama
        FormLabel("Açıklama:", 300);
        var descInput = MockupUI.BuildInputField("DescInput", form, theme, "Takım açıklamasını yaz...", 26, multiline: true);
        var dirt = ((Image)descInput.targetGraphic).rectTransform;
        PlaceAt(dirt, new Vector2(1, 1), new Vector2(-34, -290), new Vector2(430, 200), pivotX: 1, pivotY: 1);

        // Gereken Bölüm (◀ N ▶)
        FormLabel("Gereken Bölüm:", 540);
        var minusBtn = MockupUI.GlossyButton(form, MockupBeautifyTool.GreenBtnPath, theme.ctaGreen,
            "◀", 30, theme.headingFont, out _);
        var mrt = ((Image)minusBtn.targetGraphic).rectTransform;
        PlaceAt(mrt, new Vector2(1, 1), new Vector2(-380, -530), new Vector2(84, 84), pivotX: 1, pivotY: 1);
        var chapterChip = MockupUI.NewSlicedImage("ChapterChip", form, theme.cardBackground, theme.screenBackground);
        PlaceAt(chapterChip.rectTransform, new Vector2(1, 1), new Vector2(-134, -530), new Vector2(230, 84), pivotX: 1, pivotY: 1);
        var chapterText = MockupUI.NewText("ChapterValue", chapterChip.transform, "0", 32, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.Stretch(chapterText.rectTransform);
        var plusBtn = MockupUI.GlossyButton(form, MockupBeautifyTool.GreenBtnPath, theme.ctaGreen,
            "▶", 30, theme.headingFont, out _);
        var prt = ((Image)plusBtn.targetGraphic).rectTransform;
        PlaceAt(prt, new Vector2(1, 1), new Vector2(-34, -530), new Vector2(84, 84), pivotX: 1, pivotY: 1);

        // Geri bildirim + Oluştur
        var feedback = MockupUI.NewText("Feedback", form, "", 26, theme.accentAmber, TextAlignmentOptions.Center, theme.bodyFont);
        PlaceAt(feedback.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(600, 40), pivotY: 0);
        var createBtn = MockupUI.GlossyButton(form, MockupBeautifyTool.GreenBtnPath, theme.ctaGreen,
            "Oluştur  100", 34, theme.headingFont, out var createLabel);
        var crt = ((Image)createBtn.targetGraphic).rectTransform;
        PlaceAt(crt, new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(460, 110), pivotY: 0);

        createRoot.gameObject.SetActive(false);

        // ── Takım Bilgisi popup ──────────────────────────────────────
        var popupRoot = MockupUI.NewRect("TeamInfoPopup", root);
        MockupUI.Stretch(popupRoot);
        var scrim = popupRoot.gameObject.AddComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.6f);

        var card = MockupUI.NewImage("Card", popupRoot, Color.white);
        MockupUI.Card(card, theme, theme.panelSurface);
        PlaceAt(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(760, 720));

        var infoEmblem = MockupUI.NewImage("Emblem", card.transform, Color.white);
        infoEmblem.preserveAspect = true;
        PlaceAt(infoEmblem.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(130, 130), pivotY: 1);
        var infoName = MockupUI.NewText("Name", card.transform, "Takım", 40, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        infoName.fontStyle = FontStyles.Bold;
        MockupUI.AnchorTop(infoName.rectTransform, height: 52, y: 174);
        var infoCap = MockupUI.NewText("Capacity", card.transform, "41/50", 30, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.AnchorTop(infoCap.rectTransform, height: 40, y: 232);
        var infoMin = MockupUI.NewText("MinChapter", card.transform, "Gereken Bölüm: 0", 26, theme.accentAmber, TextAlignmentOptions.Center, theme.bodyFont);
        MockupUI.AnchorTop(infoMin.rectTransform, height: 36, y: 278);
        var infoDesc = MockupUI.NewText("Desc", card.transform, "Açıklama", 26, theme.textLight, TextAlignmentOptions.Center, theme.bodyFont);
        infoDesc.textWrappingMode = TextWrappingModes.Normal;
        MockupUI.AnchorTop(infoDesc.rectTransform, height: 150, y: 324);
        infoDesc.rectTransform.offsetMin = new Vector2(40, infoDesc.rectTransform.offsetMin.y);
        infoDesc.rectTransform.offsetMax = new Vector2(-40, infoDesc.rectTransform.offsetMax.y);

        var joinBtn = MockupUI.GlossyButton(card.transform, MockupBeautifyTool.GreenBtnPath, theme.ctaGreen,
            "Katıl", 34, theme.headingFont, out var joinLabel);
        var jrt = ((Image)joinBtn.targetGraphic).rectTransform;
        PlaceAt(jrt, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(380, 108), pivotY: 0);
        var infoCloseBtn = MockupUI.GlossyButton(card.transform, MockupBeautifyTool.BlueBtnPath, theme.accentAmber,
            "✕", 40, theme.headingFont, out _);
        var icrt = ((Image)infoCloseBtn.targetGraphic).rectTransform;
        PlaceAt(icrt, new Vector2(1, 1), new Vector2(-14, -14), new Vector2(84, 84), pivotX: 1, pivotY: 1);

        popupRoot.gameObject.SetActive(false);
        root.gameObject.SetActive(false);   // TeamScreenController takım durumuna göre açar

        // ── Wiring ───────────────────────────────────────────────────
        MockupUI.SetRef(browser, "theme", theme);
        MockupUI.SetRef(browser, "searchTabButton", searchTabBtn);
        MockupUI.SetRef(browser, "searchTabBg", searchTabBg);
        MockupUI.SetRef(browser, "createTabButton", createTabBtn);
        MockupUI.SetRef(browser, "createTabBg", createTabBg);
        MockupUI.SetRef(browser, "searchRoot", searchRoot.gameObject);
        MockupUI.SetRef(browser, "createRoot", createRoot.gameObject);
        MockupUI.SetRef(browser, "searchInput", searchInput);
        MockupUI.SetRef(browser, "searchButton", searchBtn);
        MockupUI.SetRef(browser, "searchClearButton", clearBtn);
        MockupUI.SetRef(browser, "resultContainer", resultContent);
        MockupUI.SetRef(browser, "rowPrefab", rowPrefab);
        MockupUI.SetRef(browser, "infoPopupRoot", popupRoot.gameObject);
        MockupUI.SetRef(browser, "infoEmblem", infoEmblem);
        MockupUI.SetRef(browser, "infoNameText", infoName);
        MockupUI.SetRef(browser, "infoCapacityText", infoCap);
        MockupUI.SetRef(browser, "infoDescText", infoDesc);
        MockupUI.SetRef(browser, "infoMinChapterText", infoMin);
        MockupUI.SetRef(browser, "infoJoinButton", joinBtn);
        MockupUI.SetRef(browser, "infoJoinLabel", joinLabel);
        MockupUI.SetRef(browser, "infoCloseButton", infoCloseBtn);
        MockupUI.SetRef(browser, "createNameInput", nameInput);
        MockupUI.SetRef(browser, "createEmblemImage", emblemImg);
        MockupUI.SetRef(browser, "browseEmblemButton", browseBtn);
        MockupUI.SetRef(browser, "createDescInput", descInput);
        MockupUI.SetRef(browser, "minChapterText", chapterText);
        MockupUI.SetRef(browser, "minChapterMinusButton", minusBtn);
        MockupUI.SetRef(browser, "minChapterPlusButton", plusBtn);
        MockupUI.SetRef(browser, "createButton", createBtn);
        MockupUI.SetRef(browser, "createButtonLabel", createLabel);
        MockupUI.SetRef(browser, "createFeedbackText", feedback);
        MockupUI.SetRefArray(browser, "emblemPool", emblemPool);

        return browser;
    }

    // Basit tek-satır TMP_InputField (arka plan + text area + placeholder + text).
    private static TMP_InputField BuildInputField(Transform parent, UITheme theme)
    {
        var bg = MockupUI.NewImage("InputField", parent, Color.white);
        MockupUI.Card(bg, theme, theme.creamSurface);
        var field = bg.gameObject.AddComponent<TMP_InputField>();

        var area = MockupUI.NewRect("TextArea", bg.transform);
        MockupUI.Stretch(area);
        area.offsetMin = new Vector2(18, 6); area.offsetMax = new Vector2(-18, -6);
        area.gameObject.AddComponent<RectMask2D>();

        var placeholder = MockupUI.NewText("Placeholder", area, "Mesaj yaz...", 26,
            new Color(0.42f, 0.34f, 0.26f, 0.6f), TextAlignmentOptions.Left, theme.bodyFont);
        MockupUI.Stretch(placeholder.rectTransform);
        var text = MockupUI.NewText("Text", area, "", 26, theme.textOnCream, TextAlignmentOptions.Left, theme.bodyFont);
        MockupUI.Stretch(text.rectTransform);

        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.targetGraphic = bg;
        field.pointSize = 26;
        return field;
    }
}
