using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ObstacleLibrary inspector'ı: liste elemanları "Element 0/1/2" yerine
/// obstacle id adıyla ("Stone", "Mud", "WolfEgg"...) gösterilir.
/// Üstte arama kutusu, her satırda kopyala/sil, altta ekle + id'ye göre sırala.
/// </summary>
[CustomEditor(typeof(ObstacleLibrary))]
public class ObstacleLibraryEditor : Editor
{
    private SerializedProperty obstacles;
    private string search = "";
    private GUIStyle headerFoldout;

    private void OnEnable()
    {
        obstacles = serializedObject.FindProperty("obstacles");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        headerFoldout ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };

        DrawToolbar();
        DrawDuplicateIdWarning();

        EditorGUILayout.Space(4);

        int deleteIndex = -1;
        int duplicateIndex = -1;

        for (int i = 0; i < obstacles.arraySize; i++)
        {
            var element = obstacles.GetArrayElementAtIndex(i);
            string displayName = GetDisplayName(element, i);

            if (!MatchesSearch(displayName, i))
                continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, displayName, true, headerFoldout);
            if (GUILayout.Button(new GUIContent("⧉", "Kopyala"), EditorStyles.miniButton, GUILayout.Width(26)))
                duplicateIndex = i;
            if (GUILayout.Button(new GUIContent("✕", "Sil"), EditorStyles.miniButton, GUILayout.Width(26)))
                deleteIndex = i;
            EditorGUILayout.EndHorizontal();

            if (element.isExpanded)
            {
                EditorGUI.indentLevel++;
                DrawElementChildren(element);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        if (duplicateIndex >= 0)
        {
            obstacles.InsertArrayElementAtIndex(duplicateIndex);
            obstacles.GetArrayElementAtIndex(duplicateIndex + 1).isExpanded = true;
        }

        if (deleteIndex >= 0)
        {
            string name = GetDisplayName(obstacles.GetArrayElementAtIndex(deleteIndex), deleteIndex);
            if (EditorUtility.DisplayDialog("Obstacle sil", $"'{name}' silinsin mi?", "Sil", "Vazgeç"))
                obstacles.DeleteArrayElementAtIndex(deleteIndex);
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("+ Obstacle Ekle"))
        {
            obstacles.InsertArrayElementAtIndex(obstacles.arraySize);
            obstacles.GetArrayElementAtIndex(obstacles.arraySize - 1).isExpanded = true;
        }

        // obstacles dışında bir alan eklenirse yine görünsün
        DrawPropertiesExcluding(serializedObject, "m_Script", "obstacles");

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField($"{obstacles.arraySize} obstacle", EditorStyles.miniLabel, GUILayout.Width(90));
        GUILayout.FlexibleSpace();
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));
        if (GUILayout.Button("Id'ye göre sırala", EditorStyles.toolbarButton, GUILayout.Width(110)))
            SortById();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDuplicateIdWarning()
    {
        var seen = new HashSet<int>();
        var dupes = new List<string>();

        for (int i = 0; i < obstacles.arraySize; i++)
        {
            var idProp = obstacles.GetArrayElementAtIndex(i).FindPropertyRelative("id");
            if (idProp == null) continue;
            if (!seen.Add(idProp.intValue))
                dupes.Add($"{EnumName(idProp)} (index {i})");
        }

        if (dupes.Count > 0)
            EditorGUILayout.HelpBox("Aynı id birden fazla kez tanımlı — ilki kullanılır:\n" + string.Join(", ", dupes), MessageType.Warning);
    }

    private static void DrawElementChildren(SerializedProperty element)
    {
        var it = element.Copy();
        var end = element.GetEndProperty();
        bool enterChildren = true;

        while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
        {
            enterChildren = false;
            EditorGUILayout.PropertyField(it, true);
        }
    }

    private bool MatchesSearch(string displayName, int index)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        return displayName.IndexOf(search.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0
            || index.ToString() == search.Trim();
    }

    private static string GetDisplayName(SerializedProperty element, int index)
    {
        var idProp = element.FindPropertyRelative("id");
        if (idProp == null)
            return $"Element {index}";

        var sizeProp = element.FindPropertyRelative("size");
        var hitsProp = element.FindPropertyRelative("hits");

        string suffix = "";
        if (sizeProp != null && hitsProp != null)
        {
            var size = sizeProp.vector2IntValue;
            suffix = $"   ({size.x}x{size.y}, {hitsProp.intValue} hit)";
        }

        return $"{EnumName(idProp)}  ·  id {idProp.intValue}{suffix}";
    }

    private static string EnumName(SerializedProperty idProp)
    {
        int idx = idProp.enumValueIndex;
        var names = idProp.enumDisplayNames;
        return idx >= 0 && idx < names.Length ? names[idx] : "<?>";
    }

    private void SortById()
    {
        for (int i = 1; i < obstacles.arraySize; i++)
        {
            int j = i;
            while (j > 0 && IdAt(j - 1) > IdAt(j))
            {
                obstacles.MoveArrayElement(j, j - 1);
                j--;
            }
        }
    }

    private int IdAt(int index)
    {
        var idProp = obstacles.GetArrayElementAtIndex(index).FindPropertyRelative("id");
        return idProp != null ? idProp.intValue : int.MaxValue;
    }
}
