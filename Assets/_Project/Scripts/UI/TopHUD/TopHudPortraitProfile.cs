using System;
using UnityEngine;

[CreateAssetMenu(menuName = "TinyFixers/UI/Portrait Profile")]
public class TopHudPortraitProfile : ScriptableObject
{
    [Serializable]
    public class Portrait
    {
        public Sprite sprite;
        [Min(0.01f)] public float scale = 1f;
        public Vector2 offset;
    }

    [Header("Placement (canvas units per source pixel)")]
    [Min(0.01f)] public float pixelScale = 0.42f;
    public Vector2 offset = new Vector2(5f, 40f);
    public bool allowOverflow = true;

    [Header("Expressions (first entry is the initial expression)")]
    public Portrait[] idle;
    public Portrait[] happy;
    public Portrait[] sad;
    public Portrait[] excited;

    public Portrait GetPortrait(TopHudRobotMood.Mood mood, int variation)
    {
        Portrait[] portraits;
        switch (mood)
        {
            case TopHudRobotMood.Mood.Happy: portraits = happy; break;
            case TopHudRobotMood.Mood.Sad: portraits = sad; break;
            case TopHudRobotMood.Mood.Excited: portraits = excited; break;
            default: portraits = idle; break;
        }

        Portrait result = FindPortrait(portraits, variation);
        if (result == null && mood == TopHudRobotMood.Mood.Excited)
            result = FindPortrait(happy, variation);

        return result ?? FindPortrait(idle, 0);
    }

    private static Portrait FindPortrait(Portrait[] portraits, int variation)
    {
        if (portraits == null || portraits.Length == 0)
            return null;

        int start = Mathf.Max(0, variation) % portraits.Length;
        for (int i = 0; i < portraits.Length; i++)
        {
            Portrait portrait = portraits[(start + i) % portraits.Length];
            if (portrait != null && portrait.sprite != null)
                return portrait;
        }

        return null;
    }
}
