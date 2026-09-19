using System;
using UnityEngine;

/// Full-body poses share a ground anchor; source images need not share a canvas size.
[CreateAssetMenu(menuName = "TinyFixers/Boss Duel/Character Profile")]
public sealed class BossDuelCharacterProfile : ScriptableObject
{
    [Serializable]
    public sealed class Pose
    {
        public Sprite sprite;
        [Tooltip("Image height relative to the character's standing height.")]
        [Min(0.01f)] public float height = 1f;
        [Tooltip("Ground anchor in the source image, measured from bottom-left (0..1).")]
        public Vector2 groundPivot = new Vector2(0.5f, 0f);
        public Vector2 offset;
    }

    public Pose idle = new Pose();
    public Pose blink = new Pose();
    public Pose focused = new Pose();
    public Pose windup = new Pose();
    public Pose strike = new Pose();
    public Pose victory = new Pose();
    public Pose defeated = new Pose();
    public bool mirrorHorizontally;
    [Min(0.1f)] public float blinkInterval = 3.2f;
    [Min(0.02f)] public float blinkDuration = 0.13f;
    [Min(0.02f)] public float windupDuration = 0.24f;
    [Min(0.02f)] public float swingDuration = 0.12f;
    [Min(0.02f)] public float impactDuration = 0.09f;
    [Min(0.02f)] public float recoveryDuration = 0.22f;
    [Range(0f, 0.03f)] public float breathingAmount = 0.008f;
    public bool IsUsable => idle != null && idle.sprite != null;
}
