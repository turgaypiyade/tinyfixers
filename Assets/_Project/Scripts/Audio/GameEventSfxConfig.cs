using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Oyun-olayı sesleri (sandık açılışı, oyun-sonu win). Kod'dan oluşan overlay/popup'lar
/// Resources/Audio/GameEventSfxConfig'ten okur. sfxGroup atanınca sesler global ses
/// kontrolüne (SoundSettingsBridge → SFXVolume) bağlı olur (kullanıcı kapatınca susar).
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Audio/Game Event Sfx", fileName = "GameEventSfxConfig")]
public class GameEventSfxConfig : ScriptableObject
{
    [Header("Sandık açılışı")]
    public AudioClip chestOpen;
    [Tooltip("Sandık açılış varyantları — doluysa her açılışta rastgele biri çalınır (yoksa chestOpen).")]
    public AudioClip[] chestOpenVariants;
    [Range(0f, 1f)] public float chestOpenVolume = 1f;

    [Header("Oyun-sonu (win) — boşsa chestOpen kullanılır")]
    public AudioClip levelWin;
    [Range(0f, 1f)] public float levelWinVolume = 1f;
    [Tooltip("Her havai fişek patlamasında çalan levelWin sesinin ölçeği (fişekler sırayla patlar, sesler binmez).")]
    [Range(0f, 1f)] public float levelWinBurstVolume = 1f;

    [Header("Kaynakçı robot (reveal sırasında loop)")]
    public AudioClip weldingLoop;
    [Range(0f, 1f)] public float weldingVolume = 1f;

    [Header("UI butonları (UiClickSounds — tüm butonlar merkezi)")]
    public AudioClip uiTap;
    public AudioClip uiConfirm;
    public AudioClip uiBack;
    public AudioClip uiDenied;
    [Range(0f, 1f)] public float uiVolume = 1f;

    [Header("Oyun anları")]
    [Tooltip("Bir hedef sayacı 0'a indiğinde (tik işareti).")]
    public AudioClip goalComplete;
    [Tooltip("Son 5 hamle uyarısı (level başına bir kez).")]
    public AudioClip movesLow;
    [Tooltip("Övgü kademeleri: 0 GOOD, 1 WOW, 2 GREAT.")]
    public AudioClip[] praise = new AudioClip[3];
    [Range(0f, 1f)] public float gameMomentVolume = 1f;

    [Header("Altın")]
    [Tooltip("Cüzdan sayacı sayarken tıklar — sırayla, hafif perde farkıyla çalınır.")]
    public AudioClip[] coinTicks;
    [Range(0f, 1f)] public float coinVolume = 1f;

    [Header("Global ses kontrolü")]
    [Tooltip("SFX mixer group (GameAudioMixer). Atanınca mute/açma bu seslere de etki eder.")]
    public AudioMixerGroup sfxGroup;
}
