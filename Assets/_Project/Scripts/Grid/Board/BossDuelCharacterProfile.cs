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
    public Pose idleAlternate = new Pose();
    public Pose blink = new Pose();
    public Pose focused = new Pose();
    public Pose windup = new Pose();
    [Tooltip("Pose during the dash toward the opponent; falls back to windup.")]
    public Pose swing = new Pose();
    public Pose strike = new Pose();
    public Pose shield = new Pose();
    [Tooltip("Optional braced pose on a blocked hit; falls back to shield.")]
    public Pose shieldHit = new Pose();
    public Pose victory = new Pose();
    public Pose defeated = new Pose();
    public bool mirrorHorizontally;
    [Min(0.1f)] public float idleAlternateInterval = 4f;
    [Min(0.02f)] public float idleAlternateDuration = 0.7f;
    [Min(0.1f)] public float blinkInterval = 3.2f;
    [Min(0.02f)] public float blinkDuration = 0.13f;
    [Min(0.02f)] public float windupDuration = 0.24f;
    [Min(0.02f)] public float swingDuration = 0.12f;
    [Min(0.02f)] public float impactDuration = 0.09f;
    [Min(0.02f)] public float recoveryDuration = 0.22f;
    [Header("Dash and colored afterimages")]
    [Min(0f)] public float contactDistance = 0.6f;
    public Material afterimageMaterial;
    public Color trailColor = new Color(0.2f, 0.8f, 1f, 0.45f);
    public Color trailAccentColor = new Color(1f, 0.75f, 0.15f, 0.3f);
    [Min(0.01f)] public float trailInterval = 0.025f;
    [Min(0.02f)] public float trailLifetime = 0.2f;
    [Header("Weapon impact")]
    [Tooltip("Weapon contact point in the strike sprite, measured from bottom-left (0..1).")]
    public Vector2 weaponImpactPoint = new Vector2(0.8f, 0.15f);
    [Tooltip("Power giving the largest visual effect. This never caps actual damage.")]
    [Min(1)] public int powerForFullImpact = 40;
    [Min(0.02f)] public float impactFxDuration = 0.24f;
    [Range(0f, 0.03f)] public float breathingAmount = 0.008f;
    [Header("Shield block")]
    [Min(0.02f)] public float shieldBlockDuration = 0.2f;
    [Min(0f)] public float shieldBlockRecoil = 0.035f;
    public bool IsUsable => idle != null && idle.sprite != null;
}
