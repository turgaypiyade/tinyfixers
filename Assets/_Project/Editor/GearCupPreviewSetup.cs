using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Installs the live Gear Cup screen into MainMenu. Never starts Play/builds.</summary>
public static class GearCupPreviewSetup
{
    private const string Art = "Assets/_Project/Art/UI/Missions/GearCup/";
    private const string ConfigPath = "Assets/_Project/Resources/Events/GearCupConfig.asset";
    private const string RootName = "GearCupPreviewScreen";
    private const string IconName = "GearCupPreviewIcon";
    private static readonly Color Navy = new Color32(12, 34, 73, 255);
    private static readonly Color Cyan = new Color32(22, 219, 244, 255);
    private static readonly Color[] RankColors =
    {
        new Color32(255, 200, 65, 255), new Color32(204, 224, 245, 255),
        new Color32(233, 155, 104, 255), new Color32(194, 204, 216, 255)
    };
    private static readonly string[] RabbitNames =
    {
        "Cheerful Bunny in Sporty Streetwear.png", "Half-Blinking Teal Jacket Bunny.png",
        "Happy Bunny Blink Render.png", "Cheerful Rabbit with Raised Hand.png",
        "Cheerful Bunny in Teal Bomber Jacket.png", "Cheerful Bunny Mascot Reaching Out.png",
        "Joyful Rabbit Reaching Skyward.png"
    };
    private static readonly string[] PigNames =
    {
        "Cheerful Pig in Varsity Jacket.png", "Cheerful Pig Mascot Ready to Celebrate.png",
        "Cheerful Pig in Varsity Jacket2.png", "Celebrating Cartoon Pig Mascot.png"
    };

    [MenuItem("TinyFixers/Gear Cup/1 - Ekranı Kur")]
    public static void Setup()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.name != "MainMenu")
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning("[GearCup] Play kapalıyken Assets/_Project/Scenes/MainMenu.unity sahnesini açıp tekrar çalıştır.");
            return;
        }
        Canvas canvas = null;
        foreach (var candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene != scene || candidate.rootCanvas != candidate) continue;
            if (canvas == null || candidate.name == "Canvas") canvas = candidate;
        }
        if (canvas == null) { if (GameDiagnostics.EditorTools)
            Debug.LogError("[GearCup] MainMenu Canvas bulunamadı."); return; }

        var cfg = EnsureConfig();
        if (cfg.background == null || cfg.rabbitFrames[0] == null || cfg.gear == null)
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogError("[GearCup] Gerekli görseller bulunamadı. Missions/GearCup klasörünü kontrol et.");
            return;
        }
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Gear Cup setup");
        Remove(canvas.transform, RootName);
        Remove(canvas.transform, IconName);
        var root = MockupUI.NewRect(RootName, canvas.transform);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Gear Cup preview");
        MockupUI.Stretch(root);
        root.gameObject.tag = "Untagged";
        root.gameObject.SetActive(false);
        var screenCanvas = root.gameObject.AddComponent<Canvas>();
        screenCanvas.overrideSorting = true;
        screenCanvas.sortingOrder = 220;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var screen = root.gameObject.AddComponent<GearCupPreviewScreen>();

        var bg = ArtImage("Background", root, cfg.background);
        MockupUI.Stretch(bg.rectTransform);
        bg.preserveAspect = false;
        bg.raycastTarget = true;
        var cover = bg.gameObject.AddComponent<AspectRatioFitter>();
        cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        cover.aspectRatio = cfg.background.rect.width / cfg.background.rect.height;

        var safe = MockupUI.NewRect("SafeArea", root);
        MockupUI.Stretch(safe);
        var design = MockupUI.NewRect("Design1080x1920", safe);
        design.sizeDelta = new Vector2(1080, 1920);
        var view = new GearCupPreviewScreen.View { safeArea = safe, design = design };
        view.title = Text("Title", design, "GEAR CUP", 84, RankColors[0], cfg.headingFont);
        Box(view.title.rectTransform, 540, 30, 760, 60);
        var timerPanel = Panel("TimerPanel", design, cfg.darkPanel, Color.white);
        Box(timerPanel.rectTransform, 540, 90, 560, 54);
        view.timer = Text("Countdown", timerPanel.transform, "", 34, Color.white, cfg.bodyFont);
        MockupUI.Stretch(view.timer.rectTransform);
        view.info = IconButton("Info", design, cfg.infoIcon, null);
        Box(view.info.image.rectTransform, 95, 37, 74, 74);
        view.close = IconButton("Close", design, cfg.closeIcon, null);
        Box(view.close.image.rectTransform, 985, 37, 74, 74);

        BuildHero(design, cfg);
        view.rewards = BuildRewards(design, cfg, screen);
        view.teams = BuildTeams(design, cfg, screen);
        var footer = Panel("ContributionPanel", design, cfg.darkPanel, Color.white);
        Box(footer.rectTransform, 540, 1810, 980, 132);
        view.contributionLabel = Text("Label", footer.transform, "", 31, new Color32(200, 230, 255, 255), cfg.bodyFont);
        Local(view.contributionLabel.rectTransform, -155, 0, 570, 80);
        var divider = MockupUI.NewImage("Divider", footer.transform, new Color32(153, 189, 222, 160));
        Local(divider.rectTransform, 170, 0, 2, 62);
        divider.raycastTarget = false;
        var contributionGear = ArtImage("Gear", footer.transform, cfg.gear);
        Local(contributionGear.rectTransform, 240, 0, 90, 90);
        view.contribution = Text("Value", footer.transform, "240", 63, Color.white, cfg.headingFont);
        Local(view.contribution.rectTransform, 365, 0, 180, 90);
        view.previewLabel = Text("PreviewNotice", design, "", 30, new Color32(190, 217, 245, 255), cfg.bodyFont);
        Box(view.previewLabel.rectTransform, 540, 1898, 1000, 44);
        view.previewLabel.raycastTarget = true;
        var continueButton = view.previewLabel.gameObject.AddComponent<Button>();
        continueButton.targetGraphic = view.previewLabel;
        continueButton.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(continueButton.onClick, screen.ContinuePlaying);
        screen.Configure(cfg, view);
        BuildLauncher(canvas.transform, cfg, screen);
        EditorUtility.SetDirty(screen);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root.gameObject;
        if (GameDiagnostics.EditorTools)
            Debug.Log("[GearCup] Gear Cup kuruldu. Sahneyi kaydet, Play'e bas ve Gear Cup ikonuna dokun. " + "TinyFixers/Gear Cup/2 - Önizlemeyi Aç (Play) de kullanılabilir. Gear Cup canlı etkinliği hazır.");
    }

    [MenuItem("TinyFixers/Gear Cup/2 - Önizlemeyi Aç (Play)")]
    public static void OpenPreview()
    {
        var screen = Object.FindFirstObjectByType<GearCupPreviewScreen>(FindObjectsInactive.Include);
        if (screen != null && Application.isPlaying) screen.Open();
        else {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning("[GearCup] Önce MainMenu'de ekranı kur ve Play'e bas.");
        }
    }

    private static void BuildHero(RectTransform design, GearCupPreviewConfig cfg)
    {
        var hero = MockupUI.NewRect("Hero", design);
        Box(hero, 540, 380, 940, 530);
        // Isolate animated images from the static leaderboard/text canvas rebuilds.
        hero.gameObject.AddComponent<Canvas>();
        var platform = ArtImage("Platform", hero, cfg.platform);
        Local(platform.rectTransform, 0, -180, 780, 292.5f);
        var ring = ArtImage("PlatformRing", hero, cfg.platformRing);
        Local(ring.rectTransform, 0, -168, 780, 292.5f);
        var back = ArtImage("GearBack", hero, cfg.gear);
        Local(back.rectTransform, 0, 60, 174, 174);
        var breath = MockupUI.NewRect("RabbitBreathing", hero);
        breath.pivot = new Vector2(0.5f, 0f);
        breath.anchoredPosition = new Vector2(-140, -150);
        breath.sizeDelta = new Vector2(280, 420);
        var rabbit = ArtImage("Rabbit", breath, cfg.rabbitFrames[0]);
        rabbit.rectTransform.anchorMin = rabbit.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rabbit.rectTransform.pivot = new Vector2(0.5f, 0f);
        rabbit.rectTransform.sizeDelta = new Vector2(280, 420);
        rabbit.rectTransform.anchoredPosition = new Vector2(0, -28f / 1536f * 420f);
        var pigBreath = MockupUI.NewRect("PigBreathing", hero);
        pigBreath.pivot = new Vector2(0.5f, 0f);
        pigBreath.anchoredPosition = new Vector2(165, -150);
        pigBreath.sizeDelta = new Vector2(280, 420);
        var pig = ArtImage("Pig", pigBreath, cfg.pigFrames[0]);
        pig.rectTransform.anchorMin = pig.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        pig.rectTransform.pivot = new Vector2(0.5f, 0f);
        pig.rectTransform.sizeDelta = new Vector2(280, 420);
        pig.rectTransform.anchoredPosition = new Vector2(0, -39f / 1536f * 420f);
        var front = ArtImage("GearFront", hero, cfg.gear);
        Local(front.rectTransform, 0, 60, 174, 174);
        var animator = hero.gameObject.AddComponent<GearCupHeroAnimator>();
        animator.Configure(cfg, rabbit, breath, back, front, ring, pig, pigBreath);
    }

    private static GearCupPreviewScreen.RewardCard[] BuildRewards(RectTransform design,
        GearCupPreviewConfig cfg, GearCupPreviewScreen screen)
    {
        var views = new GearCupPreviewScreen.RewardCard[3];
        UnityAction[] actions = { screen.ShowFirstReward, screen.ShowSecondReward, screen.ShowThirdReward };
        const float cardScale = 347f / 335f;
        // The visible source has an 8-unit transparent margin on each side.
        // Scale the complete cards uniformly and keep their outside edges aligned to the 1029-wide rows.
        const float cardStep = (980f * 1.05f - (335f - 16f) * cardScale) * 0.5f;
        for (int i = 0; i < 3; i++)
        {
            bool framedPanel = cfg.rewardCardGold != null;
            var card = Panel("Reward" + (i + 1), design, framedPanel ? cfg.rewardCardGold : cfg.rowBackground, Color.white);
            card.material = i == 1 ? cfg.rewardCardSilverMaterial : i == 2 ? cfg.rewardCardBronzeMaterial : null;
            // Include the source's transparent padding: painted outer edges align with the team rows.
            Box(card.rectTransform, 540 + (i - 1) * cardStep, 845, 335, framedPanel ? 300 : 250);
            card.rectTransform.localScale = Vector3.one * cardScale;
            if (framedPanel)
            {
                // Preserve the painted corners and padding independently of texture downsampling.
                var canvas = design.GetComponentInParent<Canvas>();
                float referencePpu = canvas != null ? canvas.rootCanvas.referencePixelsPerUnit : 100f;
                card.pixelsPerUnitMultiplier = Mathf.Max(0.01f,
                    card.sprite.border.x * referencePpu / (card.sprite.pixelsPerUnit * 44f));
            }
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            card.raycastTarget = true;
            UnityEventTools.AddPersistentListener(button.onClick, actions[i]);
            var badge = ArtImage("RankHeader", card.transform, cfg.rewardHeader);
            badge.color = RankColors[i];
            Local(badge.rectTransform, 0, 114, 220, 110);
            var rank = Text("Rank", card.transform, (i + 1).ToString(), 47, Navy, cfg.headingFont);
            Local(rank.rectTransform, 0, 117, 160, 64);
            var coins = ArtImage("Coins", card.transform, TileIconLibrary.Shared != null ? TileIconLibrary.Shared.coin : null);
            Local(coins.rectTransform, -70, 12, 115, 115);
            var item = ArtImage("Item", card.transform, null);
            Local(item.rectTransform, 65, 12, 118, 118);
            var coinsText = Text("CoinsAmount", card.transform, "", 34, Navy, cfg.bodyFont);
            Local(coinsText.rectTransform, -70, -82, 140, 45);
            var amount = Text("ItemAmount", card.transform, "", 34, Navy, cfg.bodyFont);
            Local(amount.rectTransform, 65, -82, 110, 45);
            var inspect = ArtImage("Inspect", card.transform, cfg.inspectIcon);
            Local(inspect.rectTransform, 135, -72, 40, 40);
            views[i] = new GearCupPreviewScreen.RewardCard { rank = rank, coins = coinsText, amount = amount, item = item };
        }
        return views;
    }

    private static GearCupPreviewScreen.TeamRow[] BuildTeams(RectTransform design,
        GearCupPreviewConfig cfg, GearCupPreviewScreen screen)
    {
        var rows = new GearCupPreviewScreen.TeamRow[5];
        UnityAction[] actions = { screen.ShowTeamOne, screen.ShowTeamTwo, screen.ShowTeamThree, screen.ShowTeamFour, screen.ShowTeamFive };
        for (int i = 0; i < 5; i++)
        {
            var root = MockupUI.NewRect("TeamRow" + (i + 1), design);
            Box(root, 540, 1055 + i * 151, 980, 118);
            root.localScale = Vector3.one * 1.05f;
            var outline = Panel("OwnTeamOutline", root, cfg.rowBackground, Cyan);
            outline.material = cfg.teamRowWhiteMaterial;
            Local(outline.rectTransform, 0, 0, 994, 132);
            var body = Panel("Body", root, cfg.rowBackground, Color.white);
            body.material = cfg.teamRowWhiteMaterial;
            MockupUI.Stretch(body.rectTransform);
            body.raycastTarget = true;
            var button = body.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            UnityEventTools.AddPersistentListener(button.onClick, actions[i]);
            var badge = ArtImage("RankShield", root, cfg.rankShield);
            badge.color = RankColors[Mathf.Min(i, 3)];
            Local(badge.rectTransform, -420, 0, 110, 110);
            var rank = Text("Rank", root, (i + 1).ToString(), 43, Navy, cfg.headingFont);
            Local(rank.rectTransform, -420, 0, 80, 60);
            var emblem = ArtImage("TeamEmblem", root, null);
            Local(emblem.rectTransform, -305, 0, 96, 96);
            var name = Text("TeamName", root, "", 39, Navy, cfg.bodyFont);
            Local(name.rectTransform, -60, 0, 350, 62);
            name.alignment = TextAlignmentOptions.Left;
            var own = Text("YourTeam", root, "", 22, new Color32(0, 119, 163, 255), cfg.bodyFont);
            Local(own.rectTransform, -60, -30, 350, 35);
            own.alignment = TextAlignmentOptions.Left;
            var gear = ArtImage("Gear", root, cfg.gear);
            Local(gear.rectTransform, 225, 0, 70, 70);
            var score = Text("Score", root, "", 45, Navy, cfg.headingFont);
            Local(score.rectTransform, 345, 0, 160, 68);
            score.alignment = TextAlignmentOptions.Right;
            var chevron = Text("Chevron", root, "›", 64, new Color32(106, 129, 151, 255), cfg.bodyFont);
            Local(chevron.rectTransform, 455, 0, 40, 80);
            rows[i] = new GearCupPreviewScreen.TeamRow
            {
                rank = rank, name = name, score = score, ownLabel = own,
                emblem = emblem, ownOutline = outline.gameObject
            };
        }
        return rows;
    }

    private static void BuildLauncher(Transform canvas, GearCupPreviewConfig cfg, GearCupPreviewScreen screen)
    {
        var parent = Find(canvas, "LeftEventPanel") ?? canvas;
        var launcher = ArtImage(IconName, parent, cfg.eventBadge != null ? cfg.eventBadge : cfg.gear);
        launcher.gameObject.tag = "Untagged";
        Undo.RegisterCreatedObjectUndo(launcher.gameObject, "Gear Cup launcher");
        launcher.rectTransform.sizeDelta = new Vector2(150, 150);
        // Match the authored Bridge/Harvest menu icon scale, including its countdown ribbon.
        launcher.rectTransform.localScale = Vector3.one * 1.15f;
        launcher.rectTransform.anchorMin = launcher.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        if (parent == canvas) Box(launcher.rectTransform, 110, 800, 150, 150);
        launcher.raycastTarget = true;
        var button = launcher.gameObject.AddComponent<Button>();
        button.targetGraphic = launcher;
        launcher.gameObject.AddComponent<EventIconAnimator>();
        UnityEventTools.AddPersistentListener(button.onClick, screen.Open);
        var label = Text("Countdown", launcher.transform, "", 22, Color.white, cfg.bodyFont);
        MockupUI.AnchorBox(label.rectTransform, new Vector2(0.16f, 0.13f), new Vector2(0.84f, 0.27f));
        var timer = launcher.gameObject.AddComponent<GearCupPreviewEventButton>();
        timer.Configure(screen, label);
        screen.ConfigureLauncher(launcher.rectTransform);
    }

    private static GearCupPreviewConfig EnsureConfig()
    {
        MockupUI.EnsureFolder("Assets/_Project/Resources/Events");
        var cfg = AssetDatabase.LoadAssetAtPath<GearCupPreviewConfig>(ConfigPath);
        bool fresh = cfg == null;
        if (fresh)
        {
            cfg = ScriptableObject.CreateInstance<GearCupPreviewConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
        }
        cfg.background = LoadArt("Futuristic Blue Arena Stage.png", 2048);
        cfg.platform = LoadArt("Futuristic Gold-Accented Sci-Fi Podium.png", 1024);
        cfg.platformRing = LoadArt("Glowing Cyan Elliptical Portal Ring.png", 1024);
        cfg.gear = LoadArt("Glossy Golden Gear Icon.png", 512);
        cfg.gainLabelMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_GearCupGainBlue.mat");
        cfg.eventBadge = LoadArt("Gear Trophy Stadium Badge.png", 512);
        cfg.rankShield = LoadArt("Glossy Silver Shield Badge.png", 512);
        cfg.rewardHeader = LoadArt("Glossy Silver Shield UI Badge.png", 512);
        cfg.rewardCardGold = LoadArt("Glossy Golden Game Card Panel.png", 1024);
        // Shared serialized materials; no runtime material/texture copies are needed.
        cfg.rewardCardSilverMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_GearCupRewardSilver.mat");
        cfg.rewardCardBronzeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_GearCupRewardBronze.mat");
        cfg.teamRowWhiteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_GearCupTeamRowWhite.mat");
        cfg.rabbitFrames = new Sprite[RabbitNames.Length];
        for (int i = 0; i < RabbitNames.Length; i++) cfg.rabbitFrames[i] = LoadArt(RabbitNames[i], 1024);
        cfg.pigFrames = new Sprite[PigNames.Length];
        for (int i = 0; i < PigNames.Length; i++) cfg.pigFrames[i] = LoadArt(PigNames[i], 1024);
        cfg.rowBackground = MockupUI.LoadSprite("Assets/_Project/Art/UI/RanksTeamUI/RowImage.png");
        cfg.darkPanel = MockupUI.LoadSprite("Assets/_Project/Art/UI/BackGroundImgs/LevelLoadingImg/LoadingHint_BottomPanel_BlueGold.png");
        cfg.infoIcon = MockupUI.LoadSprite("Assets/_Project/Resources/UIIcons/Icon_Info.png");
        cfg.closeIcon = MockupUI.LoadSprite("Assets/_Project/Art/UI/btnclose.png");
        cfg.inspectIcon = MockupUI.LoadSprite("Assets/_Project/Resources/UIIcons/Icon_Search.png");
        cfg.buttonBackground = MockupUI.LoadSprite("Assets/_Project/Resources/UIButtons/Btn_Blue.png");
        cfg.closeBackground = null; // btnclose already contains its complete button artwork.
        cfg.joinPopupBackground = MockupUI.LoadSprite("Assets/_Project/Art/UI/Missions/MissionPopup.png");
        cfg.joinPopupMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_SaveProgressPurple.mat");
        cfg.headingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Baloo2/Baloo2-ExtraBold SDF.asset");
        cfg.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Inter/Static/Inter_28pt-ExtraBold SDF.asset");
        cfg.teamEmblems = new Sprite[5];
        for (int i = 0; i < 5; i++)
            cfg.teamEmblems[i] = MockupUI.LoadSprite($"Assets/_Project/Resources/TeamEmblems/TeamEmblem_{i + 1:00}.png");
        if (fresh)
        {
            cfg.rewards = new[]
            {
                new GearCupPreviewConfig.RewardSample { coins = 1000, item = DailySlotRewardType.Joker_PulseCore, itemAmount = 2 },
                new GearCupPreviewConfig.RewardSample { coins = 500, item = DailySlotRewardType.Joker_LineH, itemAmount = 1 },
                new GearCupPreviewConfig.RewardSample { coins = 250, item = DailySlotRewardType.Booster_Hammer, itemAmount = 1 }
            };
        }
        EditorUtility.SetDirty(cfg);
        return cfg;
    }

    private static Sprite LoadArt(string name, int limit)
    {
        string path = Art + name;
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.maxTextureSize != limit || importer.mipmapEnabled || importer.isReadable ||
                !importer.alphaIsTransparency || settings.spriteMeshType != SpriteMeshType.FullRect ||
                importer.textureCompression != TextureImporterCompression.Compressed;
            if (changed)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = limit;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
        }
        return MockupUI.LoadSprite(path);
    }

    private static Image ArtImage(string name, Transform parent, Sprite sprite)
    {
        var image = MockupUI.NewImage(name, parent, Color.white);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static Image Panel(string name, Transform parent, Sprite sprite, Color color)
    {
        var image = MockupUI.NewSlicedImage(name, parent, sprite, color);
        image.raycastTarget = false;
        return image;
    }

    private static Button IconButton(string name, Transform parent, Sprite icon, Sprite background)
    {
        var image = background != null ? Panel(name, parent, background, Color.white) : ArtImage(name, parent, icon);
        image.raycastTarget = true;
        if (background != null)
        {
            var symbol = ArtImage("Symbol", image.transform, icon);
            MockupUI.Stretch(symbol.rectTransform);
            symbol.rectTransform.offsetMin = new Vector2(18, 18);
            symbol.rectTransform.offsetMax = new Vector2(-18, -18);
        }
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, TMP_FontAsset font)
    {
        var text = MockupUI.NewText(name, parent, value, size, color, TextAlignmentOptions.Center, font);
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.65f;
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void Box(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x - 540, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void Local(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private static Transform Find(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == name) return child;
            var found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void Remove(Transform parent, string name)
    {
        var old = Find(parent, name);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
    }
}
