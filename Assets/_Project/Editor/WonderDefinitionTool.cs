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

        def.characters = CharactersFromScene(view);

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

    // Sahnedeki ambient agent'lardan WonderCharacter[] üretir (path dahil).
    static WonderCharacter[] CharactersFromScene(WonderRevealView view)
    {
        var chars = new List<WonderCharacter>();
        if (view != null && view.ambientAgents != null)
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
        return chars.ToArray();
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

    // ---- EDIT: seçili wonder'ın YOLLARINI kendi imajıyla düzenle ------
    const string EditScenePath = "Assets/_Project/Scenes/WonderPathEdit.unity";
    const string EditTargetKey = "wonder_path_edit_target_guid";

    [MenuItem("TinyFixers/Wonders/Edit Wonder Paths (Selected)")]
    public static void EditWonderPaths()
    {
        var def = Selection.activeObject as WonderDefinition;
        if (def == null)
        {
            EditorUtility.DisplayDialog("Edit Paths", "Önce bir WonderDefinition asset'i seç (Project'te).", "Tamam");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var canvasGo = new GameObject("WonderPathEdit_Canvas",
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

        // Arka plan = wonder'ın TAM imajı (yollar imaja göre çizilsin diye reveal=1)
        var bgGo = new GameObject("WonderBackground", typeof(Image), typeof(AspectRatioFitter), typeof(WonderRevealView));
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.SetParent(canvasGo.transform, false);
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = def.backgroundSprite;
        bgImg.preserveAspect = false;
        var fitter = bgGo.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        if (def.backgroundSprite != null)
            fitter.aspectRatio = (float)def.backgroundSprite.texture.width / def.backgroundSprite.texture.height;
        var view = bgGo.GetComponent<WonderRevealView>();
        view.wonderId = def.wonderId;
        view.totalStages = def.TaskCount;
        view.previewReveal = 1f;   // düzenlerken imaj tam görünsün

        // Mevcut karakterleri DÜZENLENEBİLİR (magenta waypoint) olarak doğur
        var agents = new List<WonderAmbientAgent>();
        if (def.characters != null)
            for (int i = 0; i < def.characters.Length; i++)
                agents.Add(SpawnEditableCharacter(bgRt, def.characters[i], i));
        view.ambientAgents = agents.ToArray();

        EditorPrefs.SetString(EditTargetKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(def)));

        Directory.CreateDirectory(Path.GetDirectoryName(EditScenePath));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, EditScenePath);
        Selection.activeObject = bgGo;
        EditorUtility.DisplayDialog("Edit Paths",
            $"'{def.wonderId}' düzenleme sahnesi açıldı ({agents.Count} karakter).\n\n" +
            "• Magenta noktaları imaj üzerinde sürükle → yolları çiz\n" +
            "• Yeni karakter: Add Robot / Add Drone menüleri\n" +
            "• Bitince: 'Bake Paths → Selected Wonder' → bu wonder'a yazılır", "Tamam");
    }

    [MenuItem("TinyFixers/Wonders/Bake Paths → Selected Wonder")]
    public static void BakePathsToWonder()
    {
        WonderDefinition def = null;
        var guid = EditorPrefs.GetString(EditTargetKey, "");
        if (!string.IsNullOrEmpty(guid))
            def = AssetDatabase.LoadAssetAtPath<WonderDefinition>(AssetDatabase.GUIDToAssetPath(guid));
        if (def == null) def = Selection.activeObject as WonderDefinition;
        if (def == null)
        {
            EditorUtility.DisplayDialog("Bake Paths", "Hedef wonder yok. 'Edit Wonder Paths' ile aç ya da bir WonderDefinition seç.", "Tamam");
            return;
        }
        var view = Object.FindFirstObjectByType<WonderRevealView>();
        if (view == null) { EditorUtility.DisplayDialog("Bake Paths", "Sahnede WonderRevealView yok.", "Tamam"); return; }

        def.characters = CharactersFromScene(view);   // sadece karakterler+yollar; bg/tasks/chest korunur
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        Selection.activeObject = def;
        EditorGUIUtility.PingObject(def);
        EditorUtility.DisplayDialog("Bake Paths",
            $"'{def.wonderId}' → {def.characters.Length} karakter/yol yazıldı.\n" +
            "Arka plan, görevler ve sandık korundu.", "Tamam");
    }

    static WonderAmbientAgent SpawnEditableCharacter(RectTransform parent, WonderCharacter c, int index)
    {
        var pts = (c.path != null && c.path.Length >= 2)
            ? c.path
            : new[] { new Vector2(-200, -400), new Vector2(200, -400) };

        char[] letters = { 'A', 'B', 'C', 'D', 'E', 'F' };
        var wps = new RectTransform[pts.Length];
        string nm = string.IsNullOrEmpty(c.name) ? "Character" : c.name;
        for (int i = 0; i < pts.Length; i++)
            wps[i] = MagentaRect($"{nm}_{index}_WP_{letters[Mathf.Min(i, letters.Length - 1)]}{i}", parent, pts[i]);

        var go = new GameObject($"{nm}_{index}", typeof(RectTransform), typeof(WonderAmbientAgent));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pts[0];

        var visualGo = new GameObject("Visual", typeof(Image));
        var vRt = (RectTransform)visualGo.transform;
        vRt.SetParent(rt, false);
        vRt.anchorMin = vRt.anchorMax = vRt.pivot = new Vector2(0.5f, 0.5f);
        vRt.sizeDelta = new Vector2(c.visualSize, c.visualSize);
        var vImg = visualGo.GetComponent<Image>();
        vImg.preserveAspect = true;
        vImg.raycastTarget = false;
        vImg.sprite = FirstFrameOf(c);

        var agent = go.GetComponent<WonderAmbientAgent>();
        agent.waypoints = wps;
        agent.pathPoints = null;            // düzenlerken magenta waypoint'ler otorite
        agent.visual = vRt;
        agent.visualImage = vImg;
        agent.facingMode = c.facingMode;
        agent.frontFrames = c.frontFrames;
        agent.backFrames = c.backFrames;
        agent.walkFrames = c.walkFrames;
        agent.walkFps = c.walkFps;
        agent.mirrorBySide = c.mirrorBySide;
        agent.speed = c.speed;
        agent.bobAmplitude = c.bobAmplitude;
        agent.bobFrequency = c.bobFrequency;
        agent.pingPong = c.pingPong;
        agent.pauseAtPoint = c.pauseAtPoint;
        agent.startWalking = false;          // düzenleme modu: yürümesin, noktalar dursun
        return agent;
    }

    static Sprite FirstFrameOf(WonderCharacter c)
    {
        if (c.frontFrames != null && c.frontFrames.Length > 0) return c.frontFrames[0];
        if (c.walkFrames != null && c.walkFrames.Length > 0) return c.walkFrames[0];
        if (c.backFrames != null && c.backFrames.Length > 0) return c.backFrames[0];
        return null;
    }

    static RectTransform MagentaRect(string name, Transform parent, Vector2 pos)
    {
        var go = new GameObject(name, typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(40, 40);
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 0.15f, 0.8f, 0.85f);
        img.raycastTarget = false;
        return rt;
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
