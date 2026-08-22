using System;
using UnityEngine;

/// <summary>
/// Tek bir "dünya harikası"nın tüm verisi: arka plan imajı, kaynak kademeleri,
/// kaynakçı kareleri ve üzerinde gezen karakterler (her biri kendi yolu + kareleriyle).
/// WonderScene bu veriden sahneyi runtime kurar. [[project_wonder_reveal_background]]
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Wonder Definition", fileName = "Wonder_")]
public class WonderDefinition : ScriptableObject
{
    [Header("Kimlik")]
    public string wonderId = "wonder";
    public string displayName = "";

    [Header("Arka Plan")]
    public Sprite backgroundSprite;
    [Min(1)] public int totalStages = 5;

    [Header("Kaynakçı")]
    public Sprite[] welderFrames;
    public float welderFps = 10f;
    public Vector2 welderSize = new Vector2(240, 240);
    [Tooltip("Kaynakçının başlangıç/park konumu (anchored)")]
    public Vector2 welderHome = Vector2.zero;

    [Header("Karakterler")]
    public WonderCharacter[] characters;
}

/// <summary>Harika üzerinde gezen bir karakter (robot/dron) — yolu + kareleri + ayarları.</summary>
[Serializable]
public class WonderCharacter
{
    public string name = "robot";
    public WonderAmbientAgent.FacingMode facingMode = WonderAmbientAgent.FacingMode.DirectionalFrontBack;

    [Header("Kareler")]
    public Sprite[] frontFrames;   // ileri giderken (bize dönük)
    public Sprite[] backFrames;    // dönüşte (arkası dönük)
    public Sprite[] walkFrames;    // SideMirror/dron
    public float walkFps = 5f;
    public bool mirrorBySide = false;

    [Header("Hareket")]
    public float speed = 90f;
    public float bobAmplitude = 8f;
    public float bobFrequency = 6f;
    public float visualSize = 200f;
    public bool pingPong = true;
    public float pauseAtPoint = 0.4f;

    [Header("Yol (anchored noktalar)")]
    public Vector2[] path;
}
