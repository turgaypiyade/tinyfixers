using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dünya harikası "kaynak/inşa ile açılma" prototipini tek tıkla kurar.
/// Menü: TinyFixers > Wonders > Setup Reveal Test.
/// Tam ekran Canvas + harika imajı (UI/WonderReveal materyali) + WonderRevealView +
/// test paneli (yıldız harca / sıfırla / ham slider). Aktif sahneye ekler.
/// </summary>
public static class WonderRevealSetup
{
    // Tam ekran (enhance edilmiş) arka plan — alttan üstten dolar.
    const string BgPath = "Assets/_Project/Art/UI/Missions/ND_M001/1/MIS1.png";
    const string ScenePath = "Assets/_Project/Scenes/WonderRevealTest.unity";
    const string WelderDir = "Assets/_Project/Art/UI/RoboCharacters/WDImgs/";

    [MenuItem("TinyFixers/Wonders/Setup Reveal Test")]
    public static void Setup()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("Wonder Reveal",
                "Sprite bulunamadı:\n" + BgPath + "\nUnity'nin imajı import ettiğinden emin ol.", "Tamam");
            return;
        }

        var shader = Shader.Find("UI/WonderReveal");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("Wonder Reveal",
                "UI/WonderReveal shader bulunamadı. Derleme hatasını kontrol et.", "Tamam");
            return;
        }

        // Menü akışı karışmasın diye BAĞIMSIZ boş bir sahneye kur.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // --- Canvas (tam ekran) ----------------------------------------
        var canvasGo = new GameObject("WonderRevealTest_Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            // Proje yeni Input System kullanıyor → eski StandaloneInputModule patlar.
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // --- Arka plan imajı (TAM EKRAN cover: alttan üstten dolar) ----
        // AspectRatioFitter/EnvelopeParent → her telefonda ekranı kaplar,
        // taşan kenar kırpılır, imaj BOZULMAZ.
        var imgGo = new GameObject("WonderBackground",
            typeof(Image), typeof(AspectRatioFitter), typeof(WonderRevealView));
        var imgRt = (RectTransform)imgGo.transform;
        imgRt.SetParent(canvasGo.transform, false);
        imgRt.anchorMin = imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        imgRt.pivot = new Vector2(0.5f, 0.5f);
        var img = imgGo.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = false; // fitter zaten oranı korur
        img.material = new Material(shader) { name = "WonderReveal_mis1" };

        var fitter = imgGo.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = (float)sprite.texture.width / sprite.texture.height;

        var view = imgGo.GetComponent<WonderRevealView>();
        view.wonderId = "mis1";
        view.totalStages = 5;
        view.previewReveal = 0f; // başlangıçta hologram göster

        // --- Kaynakçı robot (MW_1..4 frame animasyonu) + kaynak arkı ---
        var welderFrames = LoadWelderFrames();
        var robotSprite = welderFrames.Length > 0 ? welderFrames[0] : null;
        var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Art/Icons/PulseCoreEffectsIcon/soft_circle.png");

        // Konteyner (boş Rect) — pozisyonu view sürer.
        var welderGo = new GameObject("WelderRobot", typeof(RectTransform));
        var welderRt = (RectTransform)welderGo.transform;
        welderRt.SetParent(imgRt, false);
        welderRt.sizeDelta = new Vector2(240, 240);
        welderRt.anchorMin = welderRt.anchorMax = new Vector2(0.5f, 0.5f);
        view.welderRobot = welderRt;
        view.welderFrames = welderFrames;
        view.welderFps = 10f;

        // [0] Robot — TAM OPAK, altta (kaynak ışığı önünde parlayacak)
        var robotGo = new GameObject("RobotSprite", typeof(Image));
        var robotRt = (RectTransform)robotGo.transform;
        robotRt.SetParent(welderRt, false);
        Stretch(robotRt);
        var welderImg = robotGo.GetComponent<Image>();
        welderImg.sprite = robotSprite;
        welderImg.preserveAspect = true;
        welderImg.color = robotSprite != null ? Color.white : new Color(0.4f, 1.7f, 2.2f, 0.9f);
        welderImg.raycastTarget = false;

        // [1] Kaynak arkı ışığı — torç ucunda, ÖNDE, titreşir (radyal yumuşak daire)
        if (glowSprite != null)
        {
            var lightGo = new GameObject("WeldLight", typeof(Image));
            var lightRt = (RectTransform)lightGo.transform;
            lightRt.SetParent(welderRt, false);
            lightRt.sizeDelta = new Vector2(150, 150);
            lightRt.anchoredPosition = new Vector2(18, -78); // torç ucu (aşağı)
            var lightImg = lightGo.GetComponent<Image>();
            lightImg.sprite = glowSprite;
            lightImg.color = view.weldLightColor;
            lightImg.raycastTarget = false;
            view.weldLight = lightImg;
            lightGo.SetActive(false); // yalnız kaynak sürerken görünür
        }
        view.welderImage = welderImg;

        // --- Test paneli (alt) -----------------------------------------
        var panel = BuildTestPanel(canvasGo.transform, view);

        Selection.activeObject = imgGo;

        // Sahneyi kaydet ve açık bırak — Play bunu oynatır, menü akışı karışmaz.
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Wonder Reveal",
            "Kuruldu → 'WonderRevealTest' sahnesi AÇIK.\nDoğrudan Play'e bas:\n• 'Yıldız Harca (+1)' → bir kademe kaynakla açılır\n• Slider → ham önizleme\n• 'Sıfırla' → baştan\n\nBaşlangıç: tamamı mavi hologram.", "Tamam");
    }

    enum AgentStyle { VerticalWalk, HorizontalWalk, Drone }

    [MenuItem("TinyFixers/Wonders/Add Robot (Vertical Path)")]
    public static void AddVertical() => AddAgent(AgentStyle.VerticalWalk);

    [MenuItem("TinyFixers/Wonders/Add Robot (Horizontal Path)")]
    public static void AddHorizontal() => AddAgent(AgentStyle.HorizontalWalk);

    [MenuItem("TinyFixers/Wonders/Add Drone (Sky Path)")]
    public static void AddDrone() => AddAgent(AgentStyle.Drone);

    static void AddAgent(AgentStyle style)
    {
        var view = Object.FindFirstObjectByType<WonderRevealView>();
        if (view == null)
        {
            EditorUtility.DisplayDialog("Ambient", "Sahnede WonderRevealView yok. Önce 'Setup Reveal Test' çalıştır.", "Tamam");
            return;
        }
        var parentRt = (RectTransform)view.transform;
        var placeholder = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Art/UI/RoboCharacters/LoadPatchbot.png");
        int index = (view.ambientAgents?.Length ?? 0) + 1;

        // --- Preset (karakter tipine göre yol + ayar) ------------------
        string prefix; Vector2[] pathPts; bool mirror; float bobAmp, bobFreq, spd; int visSize; string frameHint;
        WonderAmbientAgent.FacingMode fm;
        switch (style)
        {
            case AgentStyle.HorizontalWalk:
                prefix = "AmbientRobotH";
                pathPts = new[] { new Vector2(-440, -650), new Vector2(-150, -650), new Vector2(150, -650), new Vector2(440, -650) };
                mirror = false; bobAmp = 8f; bobFreq = 6f; spd = 95f; visSize = 200;
                fm = WonderAmbientAgent.FacingMode.DirectionalFrontBack;
                frameHint = "• Front Frames → İLERİ giderken (soldan sağa) kareler\n• Back Frames → DÖNÜŞTE (sağdan sola) kareler";
                break;
            case AgentStyle.Drone:
                prefix = "AmbientDrone";
                pathPts = new[] { new Vector2(-380, 520), new Vector2(-40, 720), new Vector2(300, 560), new Vector2(430, 780) };
                mirror = true; bobAmp = 20f; bobFreq = 3f; spd = 135f; visSize = 150;
                fm = WonderAmbientAgent.FacingMode.SideMirror;
                frameHint = "• Walk Frames → dron/pervane kareleri (yoksa tek sprite)\n• Gökte süzülür (büyük yumuşak bob)";
                break;
            default: // VerticalWalk
                prefix = "AmbientRobot";
                pathPts = new[] { new Vector2(-160, -700), new Vector2(140, -520), new Vector2(-40, -300), new Vector2(180, -150) };
                mirror = false; bobAmp = 8f; bobFreq = 6f; spd = 90f; visSize = 200;
                fm = WonderAmbientAgent.FacingMode.DirectionalFrontBack;
                frameHint = "• Front Frames → İLERİ giderken (A→B→…) kareler — BİZE DÖNÜK\n• Back Frames → DÖNÜŞTE (…→A) kareler — ARKASI DÖNÜK";
                break;
        }

        char[] letters = { 'A', 'B', 'C', 'D' };
        var wps = new RectTransform[pathPts.Length];
        for (int i = 0; i < pathPts.Length; i++)
            wps[i] = MockRect($"{prefix}_{index}_WP_{letters[i]}", parentRt, pathPts[i]);

        var agentGo = new GameObject($"{prefix}_{index}", typeof(RectTransform), typeof(WonderAmbientAgent));
        var agentRt = (RectTransform)agentGo.transform;
        agentRt.SetParent(parentRt, false);
        agentRt.anchorMin = agentRt.anchorMax = new Vector2(0.5f, 0.5f);
        agentRt.anchoredPosition = wps[0].anchoredPosition;

        var visualGo = new GameObject("Visual", typeof(Image));
        var visualRt = (RectTransform)visualGo.transform;
        visualRt.SetParent(agentRt, false);
        visualRt.sizeDelta = new Vector2(visSize, visSize);
        var visualImg = visualGo.GetComponent<Image>();
        visualImg.sprite = placeholder;
        visualImg.preserveAspect = true;
        visualImg.raycastTarget = false;

        var agent = agentGo.GetComponent<WonderAmbientAgent>();
        agent.waypoints = wps;
        agent.visual = visualRt;
        agent.visualImage = visualImg;
        agent.facingMode = fm;
        agent.mirrorBySide = mirror;
        agent.bobAmplitude = bobAmp;
        agent.bobFrequency = bobFreq;
        agent.speed = spd;
        agent.walkFps = 5f;
        agent.startWalking = true; // test için hemen; prod'da false + reveal-gate

        var list = new System.Collections.Generic.List<WonderAmbientAgent>(
            view.ambientAgents ?? new WonderAmbientAgent[0]);
        list.Add(agent);
        view.ambientAgents = list.ToArray();

        Selection.activeObject = agentGo;
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        EditorUtility.DisplayDialog("Ambient",
            $"{prefix}_{index} eklendi (placeholder Patchbot).\n\n" +
            frameHint + "\n\n" +
            $"• {prefix}_{index}_WP_A→D (4 magenta nokta) yolu çizer; sahnede sürükle\n" +
            "• Play'de magenta noktalar gizlenir", "Tamam");
    }

    static RectTransform MockRect(string name, Transform parent, Vector2 pos)
    {
        // Görünür işaret (Image) — Scene'de kolay tutulur; Play'de agent gizler.
        var go = new GameObject(name, typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(40, 40);
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 0.15f, 0.8f, 0.85f); // parlak magenta
        img.raycastTarget = false;
        return rt;
    }

    internal static WonderRevealTester BuildTestPanel(Transform parent, WonderRevealView view)
    {
        var panelGo = new GameObject("TestPanel", typeof(RectTransform), typeof(WonderRevealTester));
        var rt = (RectTransform)panelGo.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0, 260);
        rt.anchoredPosition = new Vector2(0, 40);

        var tester = panelGo.GetComponent<WonderRevealTester>();
        tester.view = view;

        tester.advanceButton = MakeButton(rt, "Yıldız Harca (+1)", new Vector2(-260, 170), new Vector2(360, 90));
        tester.resetButton = MakeButton(rt, "Sıfırla", new Vector2(260, 170), new Vector2(300, 90));
        tester.revealSlider = MakeSlider(rt, new Vector2(0, 60), new Vector2(820, 40));
        return tester;
    }

    static Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Btn_" + label, typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.12f, 0.5f, 0.85f, 0.95f);

        var txtGo = new GameObject("Label", typeof(TextMeshProUGUI));
        var trt = (RectTransform)txtGo.transform;
        trt.SetParent(rt, false);
        Stretch(trt);
        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 34;
        tmp.color = Color.white;
        return go.GetComponent<Button>();
    }

    static Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("RevealSlider", typeof(Slider));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var bgGo = new GameObject("Background", typeof(Image));
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.SetParent(rt, false);
        Stretch(bgRt);
        bgGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        var faRt = (RectTransform)fillArea.transform;
        faRt.SetParent(rt, false);
        Stretch(faRt);
        var fillGo = new GameObject("Fill", typeof(Image));
        var fillRt = (RectTransform)fillGo.transform;
        fillRt.SetParent(faRt, false);
        Stretch(fillRt);
        fillGo.GetComponent<Image>().color = new Color(0.4f, 1.4f, 2.0f, 0.9f);

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fillRt;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        return slider;
    }

    // MW_1, MW_2, ... sırayla yükler; ilk bulunamayanda durur.
    static Sprite[] LoadWelderFrames()
    {
        var list = new System.Collections.Generic.List<Sprite>();
        for (int i = 1; i <= 32; i++)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{WelderDir}MW_{i}.png");
            if (s == null) break;
            list.Add(s);
        }
        return list.ToArray();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
