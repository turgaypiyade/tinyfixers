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

    [MenuItem("TinyFixers/Mockup/Setup Team")]
    public static void Setup()
    {
        MockupUI.EnsureFolder(PrefabDir);
        var theme = MockupUI.EnsureTheme();

        BuildChatRowPrefab(theme);
        var chatAsset = AssetDatabase.LoadAssetAtPath<TeamChatRow>(ChatPath);

        var tab = MockupUI.FindTabController();
        if (tab == null)
        {
            EditorUtility.DisplayDialog("Team Setup", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var panel = BuildPanel(tab.transform, theme, chatAsset);
        MockupUI.AssignTabPanel(tab, "Teams", panel);

        EditorSceneManager.MarkSceneDirty(tab.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Team Setup",
            "Takım (sade sohbet) kuruldu ve Teams sekmesine bağlandı. Sprite'ları elle bağla, Cmd+S.", "Tamam");
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

    // ── Panel ──────────────────────────────────────────────────────────
    private static GameObject BuildPanel(Transform bottomBar, UITheme theme, TeamChatRow chatPrefab)
    {
        var panel = MockupUI.BuildScreenPanel(bottomBar, "TeamPanel", theme, "Takım", out var body);
        var ctrl = panel.AddComponent<TeamScreenController>();

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

        // Wiring
        MockupUI.SetRef(ctrl, "theme", theme);
        MockupUI.SetRef(ctrl, "emblemImage", emblem);
        MockupUI.SetRef(ctrl, "defaultEmblem", MockupUI.LoadSprite("Assets/_Project/Art/UI/ProfileUI/TeamIcon.png"));
        MockupUI.SetRefArray(ctrl, "avatarPool", MockupUI.AvatarPool());
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

        return panel;
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
