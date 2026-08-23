using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wonder mission sistemini mevcut ana menüye tek tıkla bağlar:
///  1) WonderCatalog asset'i oluşturur/günceller (Settings/Wonders'taki tüm Wonder_*.asset)
///  2) Açık sahnedeki RegionUnlockListPanel'e wonderCatalog + wonderOverlay bağlar (wonder modu)
///  3) Menüyü karartan tam ekran WonderRevealOverlay'i sahneye kurar
/// Region mantığı bozulmaz; panel wonderCatalog atanınca wonder moduna geçer.
/// Menü: TinyFixers > Wonders > Setup Wonder Missions (Main Menu).
/// </summary>
public static class WonderMenuSetup
{
    const string WonderDir = "Assets/_Project/Settings/Wonders";
    const string CatalogPath = WonderDir + "/WonderCatalog.asset";
    const string SoftCircle = "Assets/_Project/Art/Icons/PulseCoreEffectsIcon/soft_circle.png";
    const string DefaultBg = "Assets/_Project/Art/UI/Missions/ND_M001/1/MISDefault.png";

    [MenuItem("TinyFixers/Wonders/Setup Wonder Missions (Main Menu)")]
    public static void Setup()
    {
        var catalog = EnsureCatalog();
        if (catalog.Count == 0)
        {
            EditorUtility.DisplayDialog("Wonder Missions",
                "Katalog boş. Önce 'Bake Scene → WonderDefinition' ile en az bir harika oluştur.", "Tamam");
            return;
        }

        var panel = Object.FindFirstObjectByType<RegionUnlockListPanel>(FindObjectsInactive.Include);
        if (panel == null)
        {
            EditorUtility.DisplayDialog("Wonder Missions",
                "Açık sahnede RegionUnlockListPanel yok. MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var canvas = panel.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Wonder Missions", "Panel bir Canvas altında değil.", "Tamam");
            return;
        }

        var overlay = BuildOverlay(canvas.transform);

        // Panel'in gizli alanlarını bağla (wonder moduna geçer)
        var so = new SerializedObject(panel);
        so.FindProperty("wonderCatalog").objectReferenceValue = catalog;
        so.FindProperty("wonderOverlay").objectReferenceValue = overlay;
        so.ApplyModifiedProperties();

        // Progress bar'ı da wonder moduna al
        foreach (var pb in Object.FindObjectsByType<RegionProgressBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var pso = new SerializedObject(pb);
            var prop = pso.FindProperty("wonderCatalog");
            if (prop != null) { prop.objectReferenceValue = catalog; pso.ApplyModifiedProperties(); }
        }

        // Journey tab'ı da wonder moduna al (açılanlar tam, açılmamışlar hologram)
        foreach (var jc in Object.FindObjectsByType<JourneyScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var jso = new SerializedObject(jc);
            var prop = jso.FindProperty("wonderCatalog");
            if (prop != null) { prop.objectReferenceValue = catalog; jso.ApplyModifiedProperties(); }
        }

        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Selection.activeObject = panel;
        EditorUtility.DisplayDialog("Wonder Missions",
            $"Bağlandı!\n• Katalog: {catalog.Count} harika\n• Panel wonder moduna geçti\n• Overlay kuruldu\n\n" +
            "Sahneyi kaydet (Cmd+S), Play → Mission butonuna bas → görevleri gör, yıldız harca.", "Tamam");
    }

    [MenuItem("TinyFixers/Wonders/Setup Wonder Background (Main Menu)")]
    public static void SetupBackground()
    {
        var catalog = EnsureCatalog();
        var panel = Object.FindFirstObjectByType<RegionUnlockListPanel>(FindObjectsInactive.Include);
        var canvas = panel != null ? panel.GetComponentInParent<Canvas>()
                                   : Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Wonder Background", "Sahnede Canvas yok. MainMenu'yü aç.", "Tamam");
            return;
        }

        var existing = Object.FindFirstObjectByType<WonderBackgroundView>(FindObjectsInactive.Include);
        GameObject rootGo;
        if (existing != null)
        {
            rootGo = existing.gameObject;
            ((RectTransform)rootGo.transform).SetAsFirstSibling(); // arkada kalsın
        }
        else
        {
            // Arka plan kökü — canvas'ın EN ALTINDA (UI'ın arkasında çizilir)
            rootGo = new GameObject("WonderBackground",
                typeof(RectTransform), typeof(WonderBackgroundView)) { layer = canvas.gameObject.layer };
            var newRt = (RectTransform)rootGo.transform;
            newRt.SetParent(canvas.transform, false);
            Stretch(newRt);
            newRt.SetAsFirstSibling();
        }

        // HOME dışı sekmelerde (Journey/Profile...) gizlensin → tab controller homeOnlyElements'e ekle.
        RegisterHomeOnly(rootGo);

        if (existing != null)
        {
            EditorSceneManager.MarkSceneDirty(rootGo.scene);
            Selection.activeObject = rootGo;
            EditorUtility.DisplayDialog("Wonder Background",
                "Mevcut WonderBackground güncellendi: arkaya alındı + HOME-only yapıldı " +
                "(artık Journey/Profile'ı ezmez). Cmd+S.", "Tamam");
            return;
        }

        var rootRt = (RectTransform)rootGo.transform;

        // İçerik kabı (slide bunu oynatır)
        var content = new GameObject("Content", typeof(RectTransform)) { layer = canvas.gameObject.layer };
        var contentRt = (RectTransform)content.transform;
        contentRt.SetParent(rootRt, false);
        Stretch(contentRt);

        var view = rootGo.GetComponent<WonderBackgroundView>();
        var so = new SerializedObject(view);
        so.FindProperty("catalog").objectReferenceValue = catalog;
        so.FindProperty("revealShader").objectReferenceValue = Shader.Find("UI/WonderReveal");
        so.FindProperty("weldLightSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SoftCircle);
        so.FindProperty("content").objectReferenceValue = contentRt;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(rootGo.scene);
        Selection.activeObject = rootGo;
        EditorUtility.DisplayDialog("Wonder Background",
            "Kuruldu (canvas'ın en altında = UI arkasında).\n" +
            "• completedCount=0 → default arka plan\n" +
            "• harika bitince → sağdan slide-in\n\n" +
            "Mevcut menü arka plan imajın varsa onu kapat/kaldır ki bu görünsün. Cmd+S.", "Tamam");
    }

    [MenuItem("TinyFixers/Wonders/Hide Legacy Region Background")]
    public static void HideLegacyBackground()
    {
        var hidden = new List<string>();
        UnityEngine.SceneManagement.Scene scene = default;

        void Disable(GameObject go, string tag)
        {
            if (go == null || !go.activeSelf) return;
            Undo.RecordObject(go, "Hide Legacy BG");
            go.SetActive(false);
            hidden.Add(tag);
            scene = go.scene;
        }

        // 1) WorkshopController'ları durdur (CurrentImage/ada resmini tekrar açıyor)
        foreach (var w in Object.FindObjectsByType<WorkshopController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (w != null) Disable(w.gameObject, $"Workshop({w.gameObject.name})");

        // 2) İsimle eşleşen eski arka plan objelerini gizle (WorldMap dahil).
        // NOT: "BGImage" gibi GENEL isimler listede YOK — başka ekranların (Profile vb.)
        // arka planını yanlışlıkla kapatmasın.
        string[] names = { "WorldMap", "CurrentImage", "NextImage", "MapImage", "SkyEmptyV3", "WorldMapBG" };
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && System.Array.IndexOf(names, t.gameObject.name) >= 0)
                Disable(t.gameObject, t.gameObject.name);

        // 3) BottomTabController.homeOnlyElements: disable ettiklerimizi null'la (home'da tekrar AÇAMASIN)
        int nulled = 0;
        foreach (var tab in Object.FindObjectsByType<BottomTabController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tab == null) continue;
            var so = new SerializedObject(tab);
            var arr = so.FindProperty("homeOnlyElements");
            if (arr == null || !arr.isArray) continue;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var go = arr.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (go != null && !go.activeSelf) { arr.GetArrayElementAtIndex(i).objectReferenceValue = null; nulled++; }
            }
            so.ApplyModifiedProperties();
        }

        if (hidden.Count > 0 && scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Hide Legacy BG",
            (hidden.Count > 0 || nulled > 0)
                ? $"Gizlendi ({hidden.Count}): {string.Join(", ", hidden)}\n" +
                  $"homeOnlyElements'te {nulled} tekrar-açma referansı temizlendi.\n\n" +
                  "Wonder arka planı görünür olmalı. Geri almak: Undo (Cmd+Z) + Hierarchy'de aktif et. Cmd+S."
                : "Gizlenecek eski arka plan bulunamadı. Hierarchy'deki obje adını yaz, ekleyeyim.", "Tamam");
    }

    [MenuItem("TinyFixers/Wonders/Cleanup Leftover Test Canvas")]
    public static void CleanupLeftoverTest()
    {
        string[] names = { "WonderRevealTest_Canvas", "WonderScene_Canvas", "WonderDefinitionTest_Canvas" };
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int removed = 0;
        UnityEngine.SceneManagement.Scene scene = default;
        foreach (var t in all)
        {
            if (t == null) continue;
            if (System.Array.IndexOf(names, t.gameObject.name) >= 0 && t.parent == null)
            {
                scene = t.gameObject.scene;
                Object.DestroyImmediate(t.gameObject);
                removed++;
            }
        }
        if (removed > 0 && scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Cleanup",
            removed > 0
                ? $"{removed} leftover test canvas silindi. Sahneyi kaydet (Cmd+S)."
                : "Silinecek leftover test canvas bulunamadı.", "Tamam");
    }

    // WonderBackground'ı tab controller'ın homeOnlyElements'ine ekler → HOME dışı sekmelerde gizlenir.
    static void RegisterHomeOnly(GameObject go)
    {
        foreach (var tab in Object.FindObjectsByType<BottomTabController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tab == null) continue;
            var so = new SerializedObject(tab);
            var arr = so.FindProperty("homeOnlyElements");
            if (arr == null || !arr.isArray) continue;

            bool present = false;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == go) { present = true; break; }
            if (!present)
            {
                arr.arraySize++;
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = go;
            }
            so.ApplyModifiedProperties();
        }
    }

    static WonderCatalog EnsureCatalog()
    {
        Directory.CreateDirectory(WonderDir);
        var catalog = AssetDatabase.LoadAssetAtPath<WonderCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<WonderCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        // Settings/Wonders'taki tüm WonderDefinition'ları isim sırasıyla topla
        var defs = AssetDatabase.FindAssets("t:WonderDefinition", new[] { WonderDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<WonderDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null)
            .OrderBy(d => d.name)
            .ToArray();
        catalog.wonders = defs;

        if (catalog.defaultBackground == null)
            catalog.defaultBackground = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultBg);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    static WonderRevealOverlay BuildOverlay(Transform canvas)
    {
        // Zaten varsa yeniden kullan
        var existing = Object.FindFirstObjectByType<WonderRevealOverlay>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        var rootGo = new GameObject("WonderRevealOverlay",
            typeof(RectTransform), typeof(CanvasGroup), typeof(WonderRevealOverlay)) { layer = canvas.gameObject.layer };
        var rootRt = (RectTransform)rootGo.transform;
        rootRt.SetParent(canvas, false);
        Stretch(rootRt);
        rootRt.SetAsLastSibling(); // her şeyin üstünde

        // Karartma (menüyü karartır)
        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image)) { layer = canvas.gameObject.layer };
        var dimRt = (RectTransform)dim.transform;
        dimRt.SetParent(rootRt, false);
        Stretch(dimRt);
        var dimImg = dim.GetComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.85f);

        // Sahne kabı (WonderScene buraya kurulur; dim'in üstünde)
        var sceneParent = new GameObject("SceneParent", typeof(RectTransform)) { layer = canvas.gameObject.layer };
        var spRt = (RectTransform)sceneParent.transform;
        spRt.SetParent(rootRt, false);
        Stretch(spRt);

        var overlay = rootGo.GetComponent<WonderRevealOverlay>();
        var so = new SerializedObject(overlay);
        so.FindProperty("root").objectReferenceValue = rootGo;
        so.FindProperty("group").objectReferenceValue = rootGo.GetComponent<CanvasGroup>();
        so.FindProperty("sceneParent").objectReferenceValue = spRt;
        so.FindProperty("revealShader").objectReferenceValue = Shader.Find("UI/WonderReveal");
        so.FindProperty("weldLightSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SoftCircle);
        so.ApplyModifiedProperties();

        rootGo.SetActive(false); // reveal sırasında açılır
        return overlay;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
