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
        if (c != null) Play(c.chestOpen, c.chestOpenVolume);
    }

    public static void PlayLevelWin()
    {
        var c = Cfg;
        if (c == null) return;
        bool hasWin = c.levelWin != null;
        Play(hasWin ? c.levelWin : c.chestOpen, hasWin ? c.levelWinVolume : c.chestOpenVolume);
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
