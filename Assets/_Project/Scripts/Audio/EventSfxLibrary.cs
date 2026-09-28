using System;
using UnityEngine;

/// <summary>Tek ses ipucu: varyasyonlardan rastgele biri, hafif pitch oynaması, istenirse süre sınırı.</summary>
[Serializable]
public sealed class EventSfxCue
{
    public AudioClip[] clips = Array.Empty<AudioClip>();
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("Rastgele pitch sapması (0.05 = ±%5).")]
    [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
    [Tooltip("Dosyanın başından yalnız bu kadar çal (sn), sonda kısa fade. 0 = tamamı.")]
    [Min(0f)] public float maxDuration;
}

/// <summary>
/// Event'lerin ORTAK ses kütüphanesi (Bridge Repair, Safari/Yükseliş, ileride diğerleri).
/// Asset: Resources/Audio/EventSfxLibrary.asset — klipler Audio/SFX/BridgeRepairEvents'ten
/// (TinyFixers ▸ Audio ▸ Event SFX Kütüphanesini Kur, dosya adıyla otomatik bağlar). Çalan: <see cref="EventSfx"/>.
/// </summary>
[CreateAssetMenu(fileName = "EventSfxLibrary", menuName = "TinyFixers/Audio/Event SFX Library")]
public sealed class EventSfxLibrary : ScriptableObject
{
    public const string ResourcePath = "Audio/EventSfxLibrary";

    [Header("Arayüz")]
    public EventSfxCue popupOpen = new();
    public EventSfxCue uiTap = new();
    [Header("Tanıtım / Nasıl oynanır")]
    public EventSfxCue stepPop = new();
    public EventSfxCue arrowWhoosh = new();
    [Header("Yarış / harita")]
    [Tooltip("Harita açıkken döngü (Bridge Repair: rüzgâr, dalga, martı).")]
    public AudioClip ambientLoop;
    [Range(0f, 1f)] public float ambientVolume = 0.35f;
    public EventSfxCue plankPlace = new();
    public EventSfxCue hammerTap = new();
    public EventSfxCue footstep = new();
    public EventSfxCue rankBadge = new();
    public EventSfxCue podiumArrive = new();
    public EventSfxCue eliminated = new();
    [Tooltip("Rakip (bot) sesleri oyuncuya göre bu oranda kısık.")]
    [Range(0f, 1f)] public float botVolumeScale = 0.35f;
    [Header("Kazanma")]
    public EventSfxCue victoryFanfare = new();
    public EventSfxCue confetti = new();
}
