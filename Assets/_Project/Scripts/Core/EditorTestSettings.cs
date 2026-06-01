using UnityEngine;

[CreateAssetMenu(fileName = "EditorTestSettings", menuName = "Debug/Editor Test Settings")]
public class EditorTestSettings : ScriptableObject
{
    [Min(1)]
    public int testLevel = 1;
}
