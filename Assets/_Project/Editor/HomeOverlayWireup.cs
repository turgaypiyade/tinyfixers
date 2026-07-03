using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// BottomTabController'ın "homeOnlyElements" listesine main-menu overlay'lerini (RightEventPanel
/// vb.) otomatik bağlar — HOME dışı sekmelerde gizlensinler. Menü: TinyFixers > Mockup > Wire Home-Only Overlays.
/// </summary>
public static class HomeOverlayWireup
{
    // HOME dışında gizlenecek overlay'lerin isimleri (gerekirse çoğalt).
    private static readonly string[] OverlayNames = { "RightEventPanel" };

    [MenuItem("TinyFixers/Mockup/Wire Home-Only Overlays")]
    public static void Wire()
    {
        var tab = MockupUI.FindTabController();
        if (tab == null)
        {
            EditorUtility.DisplayDialog("Home Overlays", "MainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var so = new SerializedObject(tab);
        var arr = so.FindProperty("homeOnlyElements");
        if (arr == null)
        {
            EditorUtility.DisplayDialog("Home Overlays",
                "BottomTabController'da homeOnlyElements alanı yok — script derlendi mi?", "Tamam");
            return;
        }

        var existing = new HashSet<Object>();
        for (int i = 0; i < arr.arraySize; i++)
            existing.Add(arr.GetArrayElementAtIndex(i).objectReferenceValue);

        int added = 0;
        foreach (var name in OverlayNames)
        {
            var go = FindInScene(name);
            if (go == null || existing.Contains(go)) continue;
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = go;
            existing.Add(go);
            added++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(tab.gameObject.scene);
        EditorUtility.DisplayDialog("Home Overlays",
            $"{added} overlay bağlandı. HOME dışı sekmelerde gizlenecek. Sahneyi kaydet (Cmd+S).", "Tamam");
    }

    private static GameObject FindInScene(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid())
                return t.gameObject;
        return null;
    }
}
