using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Generated lightweight references. Contains no remote textures or materials.</summary>
public sealed class EventArtBindings : ScriptableObject
{
    public EventArtReference[] references = Array.Empty<EventArtReference>();
}

[Serializable]
public sealed class EventArtReference
{
    public string eventId, assetKey, propertyPath;
    public Object target;

    // Paths are generated from SerializedProperty, validated in the editor and preserved for IL2CPP.
    // Reflection occurs only on event open/scene teardown, never in a frame update.
    public void Set(Object value)
    {
        if (target == null) return;
        if (target is Image image)
        {
            if (propertyPath == "m_Sprite") { image.sprite = value as Sprite; return; }
            if (propertyPath == "m_Material") { image.material = value as Material; return; }
        }
        if (target is RawImage raw && propertyPath == "m_Texture") { raw.texture = value as Texture; return; }
        if (target is Graphic graphic && propertyPath == "m_Material") { graphic.material = value as Material; return; }
        SetField(target, propertyPath.Replace(".Array.data[", "[").Split('.'), 0, value);
    }

    private static void SetField(object owner, string[] parts, int index, Object value)
    {
        string part = parts[index];
        int bracket = part.IndexOf('[');
        string name = bracket < 0 ? part : part.Substring(0, bracket);
        FieldInfo field = null;
        for (var type = owner.GetType(); type != null && field == null; type = type.BaseType)
            field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (field == null) throw new InvalidOperationException("Event art binding field missing: " + name);
        if (bracket >= 0)
        {
            var list = (IList)field.GetValue(owner);
            int slot = int.Parse(part.Substring(bracket + 1, part.Length - bracket - 2));
            if (index == parts.Length - 1) list[slot] = value;
            else { var child = list[slot]; SetField(child, parts, index + 1, value); list[slot] = child; }
        }
        else if (index == parts.Length - 1) field.SetValue(owner, value);
        else { var child = field.GetValue(owner); SetField(child, parts, index + 1, value); field.SetValue(owner, child); }
    }
}
