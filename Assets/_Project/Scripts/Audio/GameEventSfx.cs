using UnityEngine;

/// <summary>
/// Oyun-olayı seslerini çalar (sandık açılışı, oyun-sonu win). Kod'dan oluşan
/// overlay/popup'lardan çağrılır. Ses, config'teki sfxGroup üzerinden çıkar →
/// global ses kontrolüne (mute) bağlıdır. Config: Resources/Audio/GameEventSfxConfig.
/// </summary>
public static class GameEventSfx
{
    private const string ResourcePath = "Audio/GameEventSfxConfig";

    private static GameEventSfxConfig _cfg;
    private static bool _loaded;
    private static AudioSource _src;
    private static AudioSource _loopSrc;

    private static GameEventSfxConfig Cfg
    {
        get
        {
            if (!_loaded) { _cfg = Resources.Load<GameEventSfxConfig>(ResourcePath); _loaded = true; }
            return _cfg;
        }
    }

    public static void PlayChestOpen()
    {
        var c = Cfg;
        if (c == null) return;

        AudioClip clip = c.chestOpen;
        if (c.chestOpenVariants != null && c.chestOpenVariants.Length > 0)
        {
            var pick = c.chestOpenVariants[Random.Range(0, c.chestOpenVariants.Length)];
            if (pick != null) clip = pick;
        }

        Play(clip, c.chestOpenVolume);
    }

    /// <summary>
    /// Kazanma logosundaki HER görsel havai fişek patlamasında çağrılır — tek-atış,
    /// hafif rastgele pitch (kesintili, üst üste binmeyen his). Klip: levelWin (yoksa chestOpen).
    /// </summary>
    public static void PlayFireworkBurst(float volumeScale = 1f)
    {
        var c = Cfg;
        if (c == null) return;

        bool hasWin = c.levelWin != null;
        AudioClip clip = hasWin ? c.levelWin : c.chestOpen;
        if (clip == null) return;

        EnsureSource();
        _src.pitch = Random.Range(0.92f, 1.08f);
        float vol = (hasWin ? c.levelWinVolume * c.levelWinBurstVolume : c.chestOpenVolume) * Mathf.Clamp01(volumeScale);
        _src.PlayOneShot(clip, Mathf.Clamp01(vol));
    }

    public static void PlayLevelWin()
    {
        var c = Cfg;
        if (c == null) return;
        bool hasWin = c.levelWin != null;
        Play(hasWin ? c.levelWin : c.chestOpen, hasWin ? c.levelWinVolume : c.chestOpenVolume);
    }

    // ── UI + oyun anları (2026-10-29) ─────────────────────────────────────────────────────────────
    // Hepsi sfxGroup üzerinden: ses ayarı kapalıyken susar. Hızlı tekrarlananlar seyreltilir (vızıltı olmasın).
    public enum UiSound { Tap, Confirm, Back, Denied }

    private const float UiMinInterval = 0.05f, CoinTickMinInterval = 0.045f;
    private static float _lastUi = -1f, _lastCoinTick = -1f;
    private static int _tickIndex;

    public static void PlayUi(UiSound sound)
    {
        if (!GameSettings.SoundEnabled) return;
        var c = Cfg;
        if (c == null || !Throttle(ref _lastUi, UiMinInterval)) return;
        var clip = sound switch
        {
            UiSound.Confirm => c.uiConfirm,
            UiSound.Back => c.uiBack,
            UiSound.Denied => c.uiDenied,
            _ => c.uiTap,
        };
        Play(clip, c.uiVolume);
    }

    /// <summary>Popup'a özel UI klibi; aynı kalıcı kaynak ve SFX mixer kullanılır.</summary>
    public static void PlayUiClip(AudioClip clip)
    {
        if (clip == null || !GameSettings.SoundEnabled) return;
        var c = Cfg;
        if (c != null) Play(clip, c.uiVolume);
    }

    public static void PlayGoalComplete()
    {
        var c = Cfg;
        if (c != null) Play(c.goalComplete, c.gameMomentVolume);
    }

    public static void PlayMovesLow()
    {
        var c = Cfg;
        if (c != null) Play(c.movesLow, c.gameMomentVolume);
    }

    /// tier: 0 GOOD, 1 WOW, 2 GREAT.
    public static void PlayPraise(int tier)
    {
        var c = Cfg;
        if (c == null || c.praise == null || c.praise.Length == 0) return;
        Play(c.praise[Mathf.Clamp(tier, 0, c.praise.Length - 1)], c.gameMomentVolume);
    }

    /// Cüzdan sayacının her adımında çağrılır; tıklar sırayla ve hafif perde farkıyla — makine gibi durmaz.
    public static void PlayCoinTick()
    {
        var c = Cfg;
        if (c == null || c.coinTicks == null || c.coinTicks.Length == 0 || !Throttle(ref _lastCoinTick, CoinTickMinInterval)) return;
        var clip = c.coinTicks[_tickIndex++ % c.coinTicks.Length];
        if (clip == null) return;
        EnsureSource();
        _src.pitch = 1f + 0.03f * (_tickIndex % 3);
        _src.PlayOneShot(clip, Mathf.Clamp01(c.coinVolume));
    }

    private static bool Throttle(ref float last, float minInterval)
    {
        float now = Time.unscaledTime;
        if (last >= 0f && now - last < minInterval) return false;
        last = now;
        return true;
    }

    /// <summary>Kaynakçı robot kaynak yaparken (loop) çağrılır.</summary>
    public static void StartWelding()
    {
        var c = Cfg;
        if (c == null || c.weldingLoop == null) return;
        EnsureLoopSource();
        if (_loopSrc.isPlaying) return;
        _loopSrc.clip = c.weldingLoop;
        _loopSrc.volume = Mathf.Clamp01(c.weldingVolume);
        _loopSrc.loop = true;
        _loopSrc.Play();
    }

    public static void StopWelding()
    {
        if (_loopSrc != null && _loopSrc.isPlaying) _loopSrc.Stop();
    }

    private static void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        EnsureSource();
        _src.pitch = 1f;
        _src.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static void EnsureLoopSource()
    {
        if (_loopSrc != null) return;
        var go = new GameObject("[GameEventSfx_Loop]");
        Object.DontDestroyOnLoad(go);
        _loopSrc = go.AddComponent<AudioSource>();
        _loopSrc.playOnAwake = false;
        var c = Cfg;
        if (c != null && c.sfxGroup != null) _loopSrc.outputAudioMixerGroup = c.sfxGroup; // mute'a bağlan
    }

    private static void EnsureSource()
    {
        if (_src != null) return;
        var go = new GameObject("[GameEventSfx]");
        Object.DontDestroyOnLoad(go);
        _src = go.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        var c = Cfg;
        if (c != null && c.sfxGroup != null) _src.outputAudioMixerGroup = c.sfxGroup; // mute'a bağlan
    }
}
