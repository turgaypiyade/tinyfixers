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
    const string PisaPath = "Assets/_Project/Art/UI/Wonders/Wonder_PisaTower.png";
    const string ScenePath = "Assets/_Project/Scenes/WonderRevealTest.unity";

    [MenuItem("TinyFixers/Wonders/Setup Reveal Test")]
    public static void Setup()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PisaPath);
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("Wonder Reveal",
                "Sprite bulunamadı:\n" + PisaPath + "\nUnity'nin imajı import ettiğinden emin ol.", "Tamam");
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

        // --- Harika imajı (tam ekran, aspect korunur) ------------------
        var imgGo = new GameObject("Wonder_Pisa", typeof(Image), typeof(WonderRevealView));
        var imgRt = (RectTransform)imgGo.transform;
        imgRt.SetParent(canvasGo.transform, false);
        Stretch(imgRt);
        var img = imgGo.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.material = new Material(shader) { name = "WonderReveal_pisa" };

        var view = imgGo.GetComponent<WonderRevealView>();
        view.wonderId = "pisa";
        view.totalStages = 5;
        view.previewReveal = 0f; // başlangıçta hologram göster

        // --- Kaynakçı robot (gerçek sprite) + kaynak parıltısı ---------
        var robotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Art/UI/RoboCharacters/LoadWrenchBot.png");
        var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Art/Icons/FX/beam_glow_soft_white.png");

        var welderGo = new GameObject("WelderRobot", typeof(Image));
        var welderRt = (RectTransform)welderGo.transform;
        welderRt.SetParent(imgRt, false);
        welderRt.sizeDelta = new Vector2(240, 240);
        welderRt.anchorMin = welderRt.anchorMax = new Vector2(0.5f, 0.5f);
        var welderImg = welderGo.GetComponent<Image>();
        welderImg.sprite = robotSprite;
        welderImg.preserveAspect = true;
        welderImg.color = robotSprite != null ? Color.white : new Color(0.4f, 1.7f, 2.2f, 0.9f);
        welderImg.raycastTarget = false;
        view.welderRobot = welderRt;

        // Robotun ucundaki yumuşak kaynak parıltısı (additive hissi için parlak tint)
        if (glowSprite != null)
        {
            var glowGo = new GameObject("WeldGlow", typeof(Image));
            var glowRt = (RectTransform)glowGo.transform;
            glowRt.SetParent(welderRt, false);
            glowRt.sizeDelta = new Vector2(320, 320);
            glowRt.anchoredPosition = new Vector2(0, -60);
            glowRt.SetSiblingIndex(0); // robotun arkasında
            var glowImg = glowGo.GetComponent<Image>();
            glowImg.sprite = glowSprite;
            glowImg.color = new Color(0.5f, 1.9f, 2.4f, 0.9f);
            glowImg.raycastTarget = false;
        }

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

    static WonderRevealTester BuildTestPanel(Transform parent, WonderRevealView view)
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

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
