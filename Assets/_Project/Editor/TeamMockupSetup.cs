using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takım ekranını tek tıkla kurar — Menü: TinyFixers > Mockup > Setup Team.
/// Sohbet + can isteği satır prefab'ları, üst bilgi kartı, hediye barı, akış, alt butonlar.
/// "Teams" sekmesine bağlar.
/// </summary>
public static class TeamMockupSetup
{
    private const string PrefabDir   = "Assets/_Project/Prefabs/UI/Team";
    private const string ChatPath    = PrefabDir + "/TeamChatRow.prefab";
    private const string RequestPath = PrefabDir + "/TeamLifeRequestRow.prefab";

    [MenuItem("TinyFixers/Mockup/Setup Team")]
    public static void Setup()
    {
        MockupUI.EnsureFolder(PrefabDir);
        var theme = MockupUI.EnsureTheme();

        BuildChatRowPrefab(theme);
        BuildRequestRowPrefab(theme);
        var chatAsset    = AssetDatabase.LoadAssetAtPath<TeamChatRow>(ChatPath);
        var requestAsset = AssetDatabase.LoadAssetAtPath<TeamLifeRequestRow>(RequestPath);

        var tab = MockupUI.FindTabController();
        if (tab == null)
        {
            EditorUtility.DisplayDialog("Team Setup", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var panel = BuildPanel(tab.transform, theme, chatAsset, requestAsset);
        MockupUI.AssignTabPanel(tab, "Teams", panel);

        EditorSceneManager.MarkSceneDirty(tab.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Team Setup",
            "Takım ekranı kuruldu ve Teams sekmesine bağlandı. Sahneyi kaydet (Cmd+S).", "Tamam");
    }

    private static void BuildChatRowPrefab(UITheme theme)
    {
        var root = MockupUI.NewRect("TeamChatRow", null);
        root.sizeDelta = new Vector2(820, 180);          // 150 avatarın sığması için yükseltildi
        MockupUI.LayoutElem(root.gameObject, preferredHeight: 180);
        var h = MockupUI.HLayout(root.gameObject, 12);
        h.padding = new RectOffset(8, 8, 8, 8);
        var row = root.gameObject.AddComponent<TeamChatRow>();

        // Avatar = ProfileScreen AvatarCircle kopyası, 150x150
        var avatar = MockupUI.BuildAvatarCircle("AvatarCircle", root, 150f, out var avatarRoot);
        MockupUI.LayoutElem(avatarRoot.gameObject, preferredWidth: 150, preferredHeight: 150);

        var bubble = MockupUI.NewRect("Bubble", root);
        var bubbleImg = bubble.gameObject.AddComponent<Image>();
        MockupUI.Card(bubbleImg, theme, theme.creamSurface);   // yuvarlak köşeli sohbet balonu
        var bubbleLE = MockupUI.LayoutElem(bubble.gameObject); bubbleLE.flexibleWidth = 1;
        var bv = MockupUI.VLayout(bubble.gameObject, 12, 2); bv.childAlignment = TextAnchor.UpperLeft;

        var topRow = MockupUI.NewRect("TopRow", bubble);
        MockupUI.LayoutElem(topRow.gameObject, preferredHeight: 30);
        MockupUI.HLayout(topRow.gameObject, 8);
        var sender = MockupUI.NewText("Sender", topRow, "Oyuncu", 36, Color.black, TextAlignmentOptions.Left, theme.headingFont);
        sender.fontStyle = FontStyles.Bold;   // isim: siyah + bold + 10 punto büyük (26→36)
        var senderLE = MockupUI.LayoutElem(sender.gameObject); senderLE.flexibleWidth = 1;
        var time = MockupUI.NewText("Time", topRow, "3g", 20, theme.textSub, TextAlignmentOptions.Right, theme.bodyFont);
        MockupUI.LayoutElem(time.gameObject, preferredWidth: 120);

        var message = MockupUI.NewText("Message", bubble, "mesaj", 24, theme.textOnCream, TextAlignmentOptions.Left, theme.bodyFont);
        MockupUI.LayoutElem(message.gameObject, preferredHeight: 30);

        MockupUI.SetRef(row, "bubble", bubbleImg);
        MockupUI.SetRef(row, "avatar", avatar);
        MockupUI.SetRef(row, "senderText", sender);
        MockupUI.SetRef(row, "messageText", message);
        MockupUI.SetRef(row, "timeText", time);

        MockupUI.SaveAndLoadPrefab<TeamChatRow>(root.gameObject, ChatPath);
    }

    private static void BuildRequestRowPrefab(UITheme theme)
    {
        var root = MockupUI.NewRect("TeamLifeRequestRow", null);
        root.sizeDelta = new Vector2(820, 150);
        var panelImg = root.gameObject.AddComponent<Image>();
        MockupUI.Card(panelImg, theme, theme.creamSurface);
        MockupUI.LayoutElem(root.gameObject, preferredHeight: 150);
        var v = MockupUI.VLayout(root.gameObject, 12, 8);
        var row = root.gameObject.AddComponent<TeamLifeRequestRow>();

        // Üst satır: avatar + isim + "Can İsteği!"
        var top = MockupUI.NewRect("Top", root);
        MockupUI.LayoutElem(top.gameObject, preferredHeight: 90);   // avatar circle'a yer
        MockupUI.HLayout(top.gameObject, 10);
        // Mini AvatarCircle (bu ikincil kartta 150 orantısız kaçtığı için 90; yapı/imaj aynı)
        var avatar = MockupUI.BuildAvatarCircle("AvatarCircle", top, 90f, out var avatarRoot);
        MockupUI.LayoutElem(avatarRoot.gameObject, preferredWidth: 90, preferredHeight: 90);
        var name = MockupUI.NewText("Name", top, "Oyuncu", 36, Color.black, TextAlignmentOptions.Left, theme.headingFont);
        name.fontStyle = FontStyles.Bold;   // isim: siyah + bold + 10 punto büyük (26→36)
        var nameLE = MockupUI.LayoutElem(name.gameObject); nameLE.flexibleWidth = 1;
        var tag = MockupUI.NewText("Tag", top, "Can İsteği!", 24, theme.lifeRed, TextAlignmentOptions.Right, theme.headingFont);
        MockupUI.LayoutElem(tag.gameObject, preferredWidth: 180);

        // Alt satır: ilerleme + Yardım
        var bottom = MockupUI.NewRect("Bottom", root);
        MockupUI.LayoutElem(bottom.gameObject, preferredHeight: 56);
        MockupUI.HLayout(bottom.gameObject, 12);

        var progress = MockupUI.NewRect("Progress", bottom);
        var progressBg = progress.gameObject.AddComponent<Image>();
        MockupUI.Card(progressBg, theme, new Color(0, 0, 0, 0.15f));
        var progLE = MockupUI.LayoutElem(progress.gameObject); progLE.flexibleWidth = 1; progLE.preferredHeight = 48;
        var fill = MockupUI.NewImage("Fill", progress, theme.ctaGreen);
        MockupUI.AnchorBox(fill.rectTransform, Vector2.zero, Vector2.one);
        if (theme.progressFill != null) fill.sprite = theme.progressFill;   // yuvarlak dolgu
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left; fill.fillAmount = 0.4f;
        var progText = MockupUI.NewText("ProgressText", progress, "2/5", 24, theme.textOnCream, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.AnchorBox(progText.rectTransform, Vector2.zero, Vector2.one);

        // Yardım butonu: glossy hazır yeşil buton (yoksa düz ctaGreen'e düşer).
        var helpBtn = MockupUI.GlossyButton(bottom, MockupBeautifyTool.GreenBtnPath, theme.ctaGreen,
            "Yardım", 26, theme.headingFont, out var helpText);
        var helpImg = (Image)helpBtn.targetGraphic;
        MockupUI.LayoutElem(helpImg.gameObject, preferredWidth: 180, preferredHeight: 48);

        MockupUI.SetRef(row, "panel", panelImg);
        MockupUI.SetRef(row, "avatar", avatar);
        MockupUI.SetRef(row, "nameText", name);
        MockupUI.SetRef(row, "tagText", tag);
        MockupUI.SetRef(row, "progressFill", fill);
        MockupUI.SetRef(row, "progressText", progText);
        MockupUI.SetRef(row, "helpButton", helpBtn);
        MockupUI.SetRef(row, "helpButtonText", helpText);

        MockupUI.SaveAndLoadPrefab<TeamLifeRequestRow>(root.gameObject, RequestPath);
    }

    private static GameObject BuildPanel(Transform bottomBar, UITheme theme, TeamChatRow chatPrefab, TeamLifeRequestRow requestPrefab)
    {
        var panel = MockupUI.BuildScreenPanel(bottomBar, "TeamPanel", theme, "Takım", out var body);
        var ctrl = panel.AddComponent<TeamScreenController>();

        // Üst bilgi kartı
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

        // Hediye barı + sayaç
        var giftBar = MockupUI.NewRect("GiftBar", body);
        MockupUI.AnchorTop(giftBar, height: 36, y: 160);
        var giftBg = giftBar.gameObject.AddComponent<Image>();
        MockupUI.Card(giftBg, theme, new Color(0, 0, 0, 0.15f));
        var giftFill = MockupUI.NewImage("GiftFill", giftBar, theme.accentAmber);
        MockupUI.AnchorBox(giftFill.rectTransform, Vector2.zero, Vector2.one);
        if (theme.progressFill != null) giftFill.sprite = theme.progressFill;
        giftFill.type = Image.Type.Filled; giftFill.fillMethod = Image.FillMethod.Horizontal;
        giftFill.fillOrigin = (int)Image.OriginHorizontal.Left; giftFill.fillAmount = 0.35f;
        var timer = MockupUI.NewText("Timer", giftBar, "2g 20s", 22, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.AnchorBox(timer.rectTransform, Vector2.zero, Vector2.one);

        // Görev banner'ı
        var mission = MockupUI.NewRect("Mission", body);
        MockupUI.AnchorTop(mission, height: 60, y: 204);
        var missionImg = mission.gameObject.AddComponent<Image>();
        MockupUI.Card(missionImg, theme, theme.infoBlue);
        var missionText = MockupUI.NewText("MissionText", mission, "kazanmak için bir göreve BAŞLA", 24, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.AnchorBox(missionText.rectTransform, Vector2.zero, Vector2.one);

        // Akış (scroll) — banner altı, alt butonların üstü
        var feedArea = MockupUI.NewRect("FeedArea", body);
        MockupUI.AnchorFill(feedArea, topOffset: 280, bottomOffset: 110);
        var content = MockupUI.BuildVerticalScroll(feedArea);

        // Alt butonlar
        var buttonsRow = MockupUI.NewRect("Buttons", body);
        MockupUI.AnchorBottom(buttonsRow, height: 90, y: 0);
        var bh = MockupUI.HLayout(buttonsRow.gameObject, 16); bh.padding = new RectOffset(16, 16, 8, 8);
        bh.childForceExpandWidth = true;
        var reqBtn = MockupUI.GlossyButton(buttonsRow, MockupBeautifyTool.GreenBtnPath, theme.ctaGreen,
            "Can İste", 28, theme.headingFont, out _);
        var msgBtn = MockupUI.GlossyButton(buttonsRow, MockupBeautifyTool.BlueBtnPath, theme.infoBlue,
            "Mesaj", 28, theme.headingFont, out _);

        MockupUI.SetRef(ctrl, "theme", theme);
        MockupUI.SetRef(ctrl, "emblemImage", emblem);
        MockupUI.SetRef(ctrl, "defaultEmblem", MockupUI.LoadSprite("Assets/_Project/Art/UI/ProfileUI/TeamIcon.png"));
        MockupUI.SetRefArray(ctrl, "avatarPool", MockupUI.AvatarPool());
        MockupUI.SetRef(ctrl, "teamNameText", teamName);
        MockupUI.SetRef(ctrl, "memberCountText", memberCount);
        MockupUI.SetRef(ctrl, "giftFill", giftFill);
        MockupUI.SetRef(ctrl, "timerText", timer);
        MockupUI.SetRef(ctrl, "missionText", missionText);
        MockupUI.SetRef(ctrl, "contentContainer", content);
        MockupUI.SetRef(ctrl, "chatRowPrefab", chatPrefab);
        MockupUI.SetRef(ctrl, "lifeRequestRowPrefab", requestPrefab);
        MockupUI.SetRef(ctrl, "requestLifeButton", reqBtn);
        MockupUI.SetRef(ctrl, "messageButton", msgBtn);

        return panel;
    }

}
