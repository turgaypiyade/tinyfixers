using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Data-driven köprü: mevcut test sahnesini WonderDefinition'a BAKE eder, ve
/// bir definition'dan sahneyi (WonderScene ile) yeniden KURAR. Böylece elle
/// ayarlanan feel veri olur, üretimde definition değiştirerek harika değişir.
/// Menü: TinyFixers > Wonders > Bake / Build From Definition.
/// </summary>
public static class WonderDefinitionTool
{
    const string DefDir = "Assets/_Project/Settings/Wonders";
    const string SoftCircle = "Assets/_Project/Art/Icons/PulseCoreEffectsIcon/soft_circle.png";
    const string ScenePath = "Assets/_Project/Scenes/WonderDefinitionTest.unity";

    // ---- BAKE: sahne → WonderDefinition -------------------------------
    [MenuItem("TinyFixers/Wonders/Bake Scene → WonderDefinition")]
    public static void Bake()
    {
        var view = Object.FindFirstObjectByType<WonderRevealView>();
        if (view == null)
        {
            EditorUtility.DisplayDialog("Bake", "Sahnede WonderRevealView yok.", "Tamam");
            return;
        }

        var def = ScriptableObject.CreateInstance<WonderDefinition>();
        def.wonderId = string.IsNullOrEmpty(view.wonderId) ? "wonder" : view.wonderId;
        def.displayName = def.wonderId;
        def.backgroundSprite = view.GetComponent<Image>()?.sprite;
        def.totalStages = view.totalStages;
        def.welderFrames = view.welderFrames;
        def.welderFps = view.welderFps;
        if (view.welderRobot != null)
        {
            def.welderSize = view.welderRobot.sizeDelta;
            def.welderHome = view.welderRobot.anchoredPosition;
        }

        var chars = new List<WonderCharacter>();
        if (view.ambientAgents != null)
            foreach (var a in view.ambientAgents)
            {
                if (a == null) continue;
                chars.Add(new WonderCharacter
                {
                    name = a.gameObject.name,
                    facingMode = a.facingMode,
                    frontFrames = a.frontFrames,
                    backFrames = a.backFrames,
                    walkFrames = a.walkFrames,
                    walkFps = a.walkFps,
                    mirrorBySide = a.mirrorBySide,
                    speed = a.speed,
                    bobAmplitude = a.bobAmplitude,
                    bobFrequency = a.bobFrequency,
                    visualSize = a.visual != null ? a.visual.sizeDelta.x : 200f,
                    pingPong = a.pingPong,
                    pauseAtPoint = a.pauseAtPoint,
                    path = ExtractPath(a),
                });
            }
        def.characters = chars.ToArray();

        Directory.CreateDirectory(DefDir);
        var path = AssetDatabase.GenerateUniqueAssetPath($"{DefDir}/Wonder_{def.wonderId}.asset");
        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = def;
        EditorGUIUtility.PingObject(def);
        EditorUtility.DisplayDialog("Bake",
            $"Yazıldı:\n{path}\n\n{def.characters.Length} karakter, {def.totalStages} kademe.\n" +
            "Artık 'Build From Definition' ile bundan sahne kurabilirsin.", "Tamam");
    }

    static Vector2[] ExtractPath(WonderAmbientAgent a)
    {
        if (a.pathPoints != null && a.pathPoints.Length >= 2)
            return (Vector2[])a.pathPoints.Clone();
        if (a.waypoints != null && a.waypoints.Length >= 2)
        {
            var p = new Vector2[a.waypoints.Length];
            for (int i = 0; i < a.waypoints.Length; i++)
                p[i] = a.waypoints[i] != null ? a.waypoints[i].anchoredPosition : Vector2.zero;
            return p;
        }
        return new Vector2[0];
    }

    // ---- BUILD: WonderDefinition → sahne ------------------------------
    [MenuItem("TinyFixers/Wonders/Build From Definition (Selected)")]
    public static void BuildFromDefinition()
    {
        var def = Selection.activeObject as WonderDefinition;
        if (def == null)
        {
            EditorUtility.DisplayDialog("Build", "Önce bir WonderDefinition asset'i seç (Project'te).", "Tamam");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Canvas
        var canvasGo = new GameObject("WonderScene_Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // WonderScene (tam ekran)
        var wsGo = new GameObject("WonderScene", typeof(RectTransform), typeof(WonderScene));
        var wsRt = (RectTransform)wsGo.transform;
        wsRt.SetParent(canvasGo.transform, false);
        wsRt.anchorMin = Vector2.zero; wsRt.anchorMax = Vector2.one;
        wsRt.offsetMin = Vector2.zero; wsRt.offsetMax = Vector2.zero;

        var ws = wsGo.GetComponent<WonderScene>();
        ws.revealShader = Shader.Find("UI/WonderReveal");
        ws.weldLightSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SoftCircle);
        ws.definition = def;
        ws.buildOnStart = false;                 // edit-mode'da kuruyoruz, Play'de tekrar kurma
        ws.charactersWalkImmediately = true;     // verify için hemen yürüsün

        var view = ws.Build(def);                // ŞİMDİ kur (edit mode)

        // Test paneli (yıldız harca / slider / sıfırla)
        if (view != null) WonderRevealSetup.BuildTestPanel(canvasGo.transform, view);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeObject = wsGo;
        EditorUtility.DisplayDialog("Build",
            $"'{def.wonderId}' definition'ından sahne kuruldu → 'WonderDefinitionTest'.\n" +
            "Play'e bas: 'Yıldız Harca' ile kaynak, %100'de karakterler yürür.\n" +
            "Bu tamamen VERİDEN kuruldu — elle bağlama yok.", "Tamam");
    }
}
