using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liderlik Panosu ekranını tek tıkla kurar — Menü: TinyFixers > Mockup > Setup Leaderboard.
/// Satır prefab'ı + sekme çubuğu + scroll list üretir, "Ranks" sekmesine bağlar.
/// </summary>
public static class LeaderboardMockupSetup
{
    private const string PrefabDir = "Assets/_Project/Prefabs/UI/Leaderboard";
    private const string RowPath   = PrefabDir + "/LeaderboardRow.prefab";

    private static readonly LeaderboardTab[] Tabs =
        { LeaderboardTab.Weekly, LeaderboardTab.Friends, LeaderboardTab.Players, LeaderboardTab.Team };
    private static readonly string[] TabLabels = { "Haftalık", "Arkadaşlar", "Oyuncular", "Takım" };

    [MenuItem("TinyFixers/Mockup/Setup Leaderboard")]
    public static void Setup()
    {
        MockupUI.EnsureFolder(PrefabDir);
        var theme = MockupUI.EnsureTheme();

        BuildRowPrefab(theme);
        var rowAsset = AssetDatabase.LoadAssetAtPath<LeaderboardRow>(RowPath);

        var tab = MockupUI.FindTabController();
        if (tab == null)
        {
            EditorUtility.DisplayDialog("Leaderboard Setup", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var panel = BuildPanel(tab.transform, theme, rowAsset);
        MockupUI.AssignTabPanel(tab, "Ranks", panel);

        EditorSceneManager.MarkSceneDirty(tab.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Leaderboard Setup",
            "Liderlik Panosu kuruldu ve Ranks sekmesine bağlandı. Sahneyi kaydet (Cmd+S).", "Tamam");
    }

    private static void BuildRowPrefab(UITheme theme)
    {
        var root = MockupUI.NewRect("LeaderboardRow", null);
        root.sizeDelta = new Vector2(880, 110);
        var bg = root.gameObject.AddComponent<Image>();
        MockupUI.Card(bg, theme, theme.panelSurface);   // yuvarlak köşeli kart
        MockupUI.LayoutElem(root.gameObject, preferredHeight: 110);
        var h = MockupUI.HLayout(root.gameObject, 12);
        h.padding = new RectOffset(16, 16, 8, 8);
        var row = root.gameObject.AddComponent<LeaderboardRow>();

        var rank = MockupUI.NewText("Rank", root, "1", 38, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.LayoutElem(rank.gameObject, preferredWidth: 104);   // 3-4 hane sığsın (örn "115", "1000")

        var avatar = MockupUI.NewImage("Avatar", root, Color.white);
        avatar.preserveAspect = true;   // robot avatarları ezilmesin
        MockupUI.LayoutElem(avatar.gameObject, preferredWidth: 84, preferredHeight: 84);

        var info = MockupUI.NewRect("Info", root);
        var infoLE = MockupUI.LayoutElem(info.gameObject); infoLE.flexibleWidth = 1;
        var iv = MockupUI.VLayout(info.gameObject, 0, 2); iv.childAlignment = TextAnchor.MiddleLeft;
        var name = MockupUI.NewText("Name", info, "Oyuncu", 30, theme.textLight, TextAlignmentOptions.Left, theme.headingFont);
        var sub  = MockupUI.NewText("Subtitle", info, "Bölüm", 22, theme.textSub, TextAlignmentOptions.Left, theme.bodyFont);

        var score = MockupUI.NewText("Score", root, "0", 34, theme.accentAmber, TextAlignmentOptions.Right, theme.headingFont);
        MockupUI.LayoutElem(score.gameObject, preferredWidth: 140);

        MockupUI.SetRef(row, "rowBackground", bg);
        MockupUI.SetRef(row, "rankText", rank);
        MockupUI.SetRef(row, "avatar", avatar);
        MockupUI.SetRef(row, "nameText", name);
        MockupUI.SetRef(row, "subtitleText", sub);
        MockupUI.SetRef(row, "scoreText", score);

        MockupUI.SaveAndLoadPrefab<LeaderboardRow>(root.gameObject, RowPath);
    }

    private static GameObject BuildPanel(Transform bottomBar, UITheme theme, LeaderboardRow rowPrefab)
    {
        var panel = MockupUI.BuildScreenPanel(bottomBar, "LeaderboardPanel", theme, "Liderlik Panosu", out var body);
        var ctrl = panel.AddComponent<LeaderboardScreenController>();

        // Kalan süre etiketi (üstte)
        var time = MockupUI.NewText("TimeLabel", body, "2g 20s", 26, theme.accentAmber, TextAlignmentOptions.Center, theme.headingFont);
        TopAnchor(time.rectTransform, height: 44, y: 0);

        // Sekme çubuğu
        var tabsBar = MockupUI.NewRect("Tabs", body);
        TopAnchor(tabsBar, height: 84, y: 52);
        var tabsH = MockupUI.HLayout(tabsBar.gameObject, 8);
        tabsH.childForceExpandWidth = true;

        var buttons = new Button[Tabs.Length];
        var highlights = new Image[Tabs.Length];
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tabRect = MockupUI.NewRect("Tab_" + Tabs[i], tabsBar);
            var tabBg = tabRect.gameObject.AddComponent<Image>();
            MockupUI.Card(tabBg, theme, theme.panelSurface);
            var btn = tabRect.gameObject.AddComponent<Button>();
            btn.targetGraphic = tabBg;

            var hl = MockupUI.NewSlicedImage("Highlight", tabRect, theme.cardBackground, theme.goldTrim);
            MockupUI.Stretch(hl.rectTransform);
            hl.enabled = false;

            var lbl = MockupUI.NewText("Label", tabRect, TabLabels[i], 26, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
            MockupUI.AnchorBox(lbl.rectTransform, Vector2.zero, Vector2.one);

            buttons[i] = btn; highlights[i] = hl;
        }

        // Pinlenmiş kendi satırın (sekme çubuğunun altında, sabit; scroll'dan bağımsız)
        var selfGO = (GameObject)PrefabUtility.InstantiatePrefab(rowPrefab.gameObject, body);
        var selfRT = (RectTransform)selfGO.transform;
        TopAnchor(selfRT, height: 110, y: 145);
        var selfRow = selfGO.GetComponent<LeaderboardRow>();

        // Liste alanı (pinlenmiş satırın altı)
        var listArea = MockupUI.NewRect("ListArea", body);
        listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one;
        listArea.offsetMin = Vector2.zero; listArea.offsetMax = new Vector2(0, -265);
        var content = MockupUI.BuildVerticalScroll(listArea);

        // Controller bağlama
        MockupUI.SetRef(ctrl, "theme", theme);
        MockupUI.SetRef(ctrl, "contentContainer", content);
        MockupUI.SetRef(ctrl, "rowPrefab", rowPrefab);
        MockupUI.SetRef(ctrl, "selfRow", selfRow);
        MockupUI.SetRef(ctrl, "timeLabelText", time);
        MockupUI.SetRefArray(ctrl, "avatarPool", MockupUI.AvatarPool());   // robot avatarlar
        AssignTabButtons(ctrl, buttons, highlights);

        return panel;
    }

    private static void AssignTabButtons(LeaderboardScreenController ctrl, Button[] buttons, Image[] highlights)
    {
        var so = new SerializedObject(ctrl);
        var arr = so.FindProperty("tabButtons");
        arr.arraySize = Tabs.Length;
        for (int i = 0; i < Tabs.Length; i++)
        {
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("tab").enumValueIndex = (int)Tabs[i];
            el.FindPropertyRelative("button").objectReferenceValue = buttons[i];
            el.FindPropertyRelative("highlight").objectReferenceValue = highlights[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void TopAnchor(RectTransform rt, float height, float y)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -y); rt.sizeDelta = new Vector2(0, height);
    }
}
