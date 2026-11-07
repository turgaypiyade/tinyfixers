using UnityEditor;
using UnityEngine;

/// Ana menü arka planını test için hızlıca değiştirir (default / herhangi bir harika).
/// Yalnız editör; kayıtlı harika ilerlemesine dokunmaz. Play'deyken anında kayarak gelir.
public class WonderBackgroundPreviewWindow : EditorWindow
{
    WonderCatalog catalog;
    Vector2 scroll;

    [MenuItem("TinyFixers/Wonders/Arka Plan Önizleme")]
    static void Open() => GetWindow<WonderBackgroundPreviewWindow>("Arka Plan Önizleme");

    void OnEnable()
    {
        if (catalog != null) return;
        var guids = AssetDatabase.FindAssets("t:WonderCatalog");
        if (guids.Length > 0)
            catalog = AssetDatabase.LoadAssetAtPath<WonderCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    void OnGUI()
    {
        catalog = (WonderCatalog)EditorGUILayout.ObjectField("Katalog", catalog, typeof(WonderCatalog), false);
        if (catalog == null) { EditorGUILayout.HelpBox("WonderCatalog bulunamadı.", MessageType.Warning); return; }

        int current = WonderProgress.EditorPreviewBackground;
        EditorGUILayout.HelpBox(current == WonderProgress.EditorPreviewOff
            ? "Önizleme kapalı: oyuncunun gerçek arka planı gösterilir."
            : "Önizleme açık. Yalnız editörde geçerli; build'e girmez, ilerlemeyi değiştirmez.", MessageType.Info);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (Row("Varsayılan (MIS_DEFAULT)", current == -1)) WonderProgress.SetEditorPreviewBackground(-1);
        for (int i = 0; i < catalog.Count; i++)
        {
            var w = catalog.Get(i);
            string label = $"{i + 1}. {(w != null ? w.displayName : "—")}";
            if (Row(label, current == i)) WonderProgress.SetEditorPreviewBackground(i);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("Önizlemeyi Kapat (gerçek arka plan)", GUILayout.Height(28)))
            WonderProgress.SetEditorPreviewBackground(WonderProgress.EditorPreviewOff);
    }

    static bool Row(string label, bool active)
    {
        var old = GUI.backgroundColor;
        if (active) GUI.backgroundColor = new Color(.35f, .85f, 1f);
        bool clicked = GUILayout.Button(active ? "▶ " + label : label, GUILayout.Height(26));
        GUI.backgroundColor = old;
        return clicked;
    }
}
