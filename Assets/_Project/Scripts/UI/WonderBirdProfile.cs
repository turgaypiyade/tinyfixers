using System;
using UnityEngine;

/// <summary>Reusable body and wing poses for birds following WonderAmbientAgent paths.</summary>
[CreateAssetMenu(menuName = "TinyFixers/Wonders/Bird Profile", fileName = "WonderBirdProfile")]
public class WonderBirdProfile : ScriptableObject
{
    public Sprite body;
    public WingPose[] wingPoses;
    [Header("Kanat Çırpma / Süzülme")]
    [Min(0.1f)] public float flapFps = 20f;
    [Tooltip("Düz uçuşta hızlı kanat çırpma süresi (sn).")]
    [Min(0.05f)] public float flapBurstDuration = 0.6f;
    [Tooltip("Düz uçuşta her çırpış serisinden sonra süzülme süresi (sn).")]
    [Min(0.05f)] public float glideDuration = 1.5f;
    [Tooltip("Yükselirken kesintisiz kanat çırpma hızının çarpanı.")]
    [Min(1f)] public float climbFlapMultiplier = 1.3f;
    [Tooltip("Rotanın yataya göre bu açıdan fazla yükselmesi/alçalması uçuş modunu değiştirir.")]
    [Range(1f, 45f)] public float slopeThresholdDegrees = 8f;
    [Tooltip("Süzülürken açık tutulacak kanat pozu (Wing Poses içindeki sıra).")]
    [Min(0)] public int glidePoseIndex = 1;
    [Tooltip("Süzülürken dikey salınımın kalan oranı.")]
    [Range(0f, 1f)] public float glideBobMultiplier = 0.15f;

    [Header("Kanat Yerleşimi")]
    [Tooltip("Wing roots relative to the visual's width/height, centered on the body.")]
    public Vector2 leftShoulder = new Vector2(-0.2f, 0.02f);
    public Vector2 rightShoulder = new Vector2(0.2f, 0.02f);
    [Min(0.01f)] public float wingScale = 1f;

    [Serializable]
    public struct WingPose
    {
        public Sprite left;
        public Sprite right;
        public Vector2 leftPivot;
        public Vector2 rightPivot;
    }
}
