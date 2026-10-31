using UnityEditor;
using UnityEngine;

/// <summary>
/// Katalog listesinde "Element 0, 1…" yerine gerçek seviye numarası görünür: "Level 12" (boşsa "Level 12 — boş").
/// Seviye numarası entry'nin `level` alanıdır (liste indeksi değil; katalogda boşluk olabilir).
/// </summary>
[CustomPropertyDrawer(typeof(LevelCatalog.LevelEntry))]
public sealed class LevelCatalogEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var level = property.FindPropertyRelative("level");
        var data = property.FindPropertyRelative("levelData");
        if (level != null)
        {
            string text = "Level " + level.intValue;
            if (data != null && data.objectReferenceValue == null) text += " — boş";
            label = new GUIContent(text, label.tooltip);
        }
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property, label, true);
}
