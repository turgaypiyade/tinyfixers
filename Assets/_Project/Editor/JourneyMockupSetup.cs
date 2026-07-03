using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yolculuk ekranını tek tıkla kurar — Menü: TinyFixers > Mockup > Setup Journey.
/// Bölüm kartı prefab'ı + katalog (mock bölümler) + aktif/önizleme kartları. "Journey" sekmesine bağlar.
/// </summary>
public static class JourneyMockupSetup
{
    private const string SettingsDir = "Assets/_Project/Settings";
    private const string PrefabDir   = "Assets/_Project/Prefabs/UI/Journey";
    private const string CatalogPath = SettingsDir + "/JourneyCatalog.asset";
    private const string CardPath    = PrefabDir + "/JourneyChapterCard.prefab";

    [MenuItem("TinyFixers/Mockup/Setup Journey")]
    public static void Setup()
    {
        MockupUI.EnsureFolder(SettingsDir);
        MockupUI.EnsureFolder(PrefabDir);
        var theme = MockupUI.EnsureTheme();
        var catalog = EnsureCatalog();

        BuildCardPrefab(theme);
        var cardAsset = AssetDatabase.LoadAssetAtPath<JourneyChapterCard>(CardPath);

        var tab = MockupUI.FindTabController();
        if (tab == null)
        {
            EditorUtility.DisplayDialog("Journey Setup", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var panel = BuildPanel(tab.transform, theme, catalog, cardAsset);
        MockupUI.AssignTabPanel(tab, "Journey", panel);

        EditorSceneManager.MarkSceneDirty(tab.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Journey Setup",
            "Yolculuk ekranı kuruldu ve Journey sekmesine bağlandı. Sahneyi kaydet (Cmd+S).", "Tamam");
    }

    private static JourneyCatalog EnsureCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<JourneyCatalog>(CatalogPath);
        if (catalog != null) return catalog;

        catalog = ScriptableObject.CreateInstance<JourneyCatalog>();
        catalog.chapters = new List<JourneyChapter>
        {
            new JourneyChapter { title = "Fluffy Flight", chapterNumber = 201, revealProgress = 1f },
            new JourneyChapter { title = "Tutan C'mon",   chapterNumber = 202, revealProgress = 0.4f },
        };
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    private static void BuildCardPrefab(UITheme theme)
    {
        var root = MockupUI.NewRect("JourneyChapterCard", null);
        root.sizeDelta = new Vector2(820, 560);
        var frame = root.gameObject.AddComponent<Image>();
        frame.color = theme.panelSurface;
        MockupUI.LayoutElem(root.gameObject, preferredHeight: 560);
        var v = MockupUI.VLayout(root.gameObject, 16, 10);
        var card = root.gameObject.AddComponent<JourneyChapterCard>();

        var title = MockupUI.NewText("Title", root, "Bölüm", 36, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.LayoutElem(title.gameObject, preferredHeight: 60);

        // Resim alanı (placeholder + resim + karartma + İzle)
        var imageArea = MockupUI.NewRect("ImageArea", root);
        imageArea.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.12f, 0.25f);
        var areaLE = MockupUI.LayoutElem(imageArea.gameObject); areaLE.flexibleHeight = 1; areaLE.preferredHeight = 380;

        var image = MockupUI.NewImage("Image", imageArea, Color.white);
        MockupUI.AnchorBox(image.rectTransform, Vector2.zero, Vector2.one);
        image.preserveAspect = true;

        var dim = MockupUI.NewImage("Dim", imageArea, new Color(0, 0, 0, 0f));
        MockupUI.AnchorBox(dim.rectTransform, Vector2.zero, Vector2.one);
        dim.enabled = false;

        var watchImg = MockupUI.NewImage("WatchButton", imageArea, theme.ctaGreen);
        watchImg.rectTransform.anchorMin = new Vector2(0.5f, 0.5f); watchImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        watchImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        watchImg.rectTransform.anchoredPosition = Vector2.zero; watchImg.rectTransform.sizeDelta = new Vector2(260, 84);
        var watchBtn = watchImg.gameObject.AddComponent<Button>();
        watchBtn.targetGraphic = watchImg;
        var watchText = MockupUI.NewText("WatchText", watchImg.rectTransform, "İzle", 32, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.AnchorBox(watchText.rectTransform, Vector2.zero, Vector2.one);

        var chapter = MockupUI.NewText("Chapter", root, "Bölüm 1", 28, theme.textLight, TextAlignmentOptions.Center, theme.headingFont);
        MockupUI.LayoutElem(chapter.gameObject, preferredHeight: 50);

        MockupUI.SetRef(card, "frame", frame);
        MockupUI.SetRef(card, "image", image);
        MockupUI.SetRef(card, "titleText", title);
        MockupUI.SetRef(card, "chapterText", chapter);
        MockupUI.SetRef(card, "watchButton", watchBtn);
        MockupUI.SetRef(card, "watchButtonText", watchText);
        MockupUI.SetRef(card, "dimOverlay", dim);

        MockupUI.SaveAndLoadPrefab<JourneyChapterCard>(root.gameObject, CardPath);
    }

    private static GameObject BuildPanel(Transform bottomBar, UITheme theme, JourneyCatalog catalog, JourneyChapterCard cardPrefab)
    {
        var panel = MockupUI.BuildScreenPanel(bottomBar, "JourneyPanel", theme, "Yolculuk", out var body);
        var ctrl = panel.AddComponent<JourneyScreenController>();

        var content = MockupUI.BuildVerticalScroll(body);

        var currentGO = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab.gameObject, content);
        var nextGO    = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab.gameObject, content);
        var currentCard = currentGO.GetComponent<JourneyChapterCard>();
        var nextCard    = nextGO.GetComponent<JourneyChapterCard>();

        MockupUI.SetRef(ctrl, "catalog", catalog);
        MockupUI.SetRef(ctrl, "theme", theme);
        MockupUI.SetRef(ctrl, "currentCard", currentCard);
        MockupUI.SetRef(ctrl, "nextCard", nextCard);

        return panel;
    }
}
