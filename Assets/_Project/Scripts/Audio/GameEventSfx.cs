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
    private static AudioSource _levelWinLoopSrc;

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
        float vol = (hasWin ? c.levelWinVolume : c.chestOpenVolume) * Mathf.Clamp01(volumeScale);
        _src.PlayOneShot(clip, Mathf.Clamp01(vol));
    }

    public static void PlayLevelWin()
    {
        var c = Cfg;
        if (c == null) return;
        bool hasWin = c.levelWin != null;
        Play(hasWin ? c.levelWin : c.chestOpen, hasWin ? c.levelWinVolume : c.chestOpenVolume);
    }

    public static void StartLevelWinLoop()
    {
        var c = Cfg;
        if (c == null) return;

        bool hasWin = c.levelWin != null;
        AudioClip clip = hasWin ? c.levelWin : c.chestOpen;
        if (clip == null) return;

        EnsureLevelWinLoopSource();
        _levelWinLoopSrc.clip = clip;
        _levelWinLoopSrc.volume = Mathf.Clamp01(hasWin ? c.levelWinVolume : c.chestOpenVolume);
        _levelWinLoopSrc.loop = true;
        if (!_levelWinLoopSrc.isPlaying)
            _levelWinLoopSrc.Play();
    }

    public static void StopLevelWinLoop()
    {
        if (_levelWinLoopSrc != null && _levelWinLoopSrc.isPlaying)
            _levelWinLoopSrc.Stop();
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

    private static void EnsureLevelWinLoopSource()
    {
        if (_levelWinLoopSrc != null) return;
        var go = new GameObject("[GameEventSfx_LevelWinLoop]");
        Object.DontDestroyOnLoad(go);
        _levelWinLoopSrc = go.AddComponent<AudioSource>();
        _levelWinLoopSrc.playOnAwake = false;
        var c = Cfg;
        if (c != null && c.sfxGroup != null) _levelWinLoopSrc.outputAudioMixerGroup = c.sfxGroup;
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
