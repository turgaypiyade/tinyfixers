using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// <see cref="EventSfxLibrary"/> seslerini çalar. Küçük bir AudioSource havuzu kullanır (süre sınırı + fade
/// için tek tek durdurulabilsin diye PlayOneShot değil). Çıkış GameEventSfxConfig.sfxGroup → genel ses
/// kısma ayarına uyar. Klip atanmamış ipucu sessizce atlanır.
/// </summary>
public static class EventSfx
{
    private const int PoolSize = 8;
    private const float FadeOut = 0.12f;

    private static EventSfxLibrary s_lib;
    private static bool s_loaded;
    private static AudioSource[] s_pool;
    private static int[] s_tokens;
    private static AudioSource s_loop;
    private static Runner s_runner;

    public static EventSfxLibrary Library
    {
        get
        {
            if (!s_loaded) { s_lib = Resources.Load<EventSfxLibrary>(EventSfxLibrary.ResourcePath); s_loaded = true; }
            return s_lib;
        }
    }

    /// Rakip sesleri kısık çalar.
    public static float ScaleFor(bool isPlayer) =>
        isPlayer ? 1f : (Library != null ? Library.botVolumeScale : 0.35f);

    /// <param name="durationOverride">>0 ise cue'nun maxDuration'ı yerine bu süre (ör. yürüyüş süresi).</param>
    public static void Play(Func<EventSfxLibrary, EventSfxCue> pick, float volumeScale = 1f, float pitch = 1f,
        float durationOverride = 0f)
    {
        var lib = Library;
        if (lib == null || !GameSettings.SoundEnabled) return;
        var cue = pick(lib);
        if (cue == null || cue.clips == null || cue.clips.Length == 0) return;
        var clip = cue.clips[UnityEngine.Random.Range(0, cue.clips.Length)];
        if (clip == null) return;

        Ensure();
        int i = PickSource();
        var src = s_pool[i];
        int token = ++s_tokens[i];
        src.Stop();
        src.clip = clip;
        src.volume = Mathf.Clamp01(cue.volume * volumeScale);
        src.pitch = pitch * (1f + UnityEngine.Random.Range(-cue.pitchJitter, cue.pitchJitter));
        src.Play();

        float limit = durationOverride > 0f ? durationOverride : cue.maxDuration;
        if (limit > 0f && limit < clip.length)
            s_runner.StartCoroutine(StopAfter(i, token, limit, src.volume));
    }

    public static void StartAmbient()
    {
        var lib = Library;
        if (lib == null || lib.ambientLoop == null || !GameSettings.SoundEnabled) return;
        Ensure();
        if (s_loop.isPlaying && s_loop.clip == lib.ambientLoop) return;
        s_loop.clip = lib.ambientLoop;
        s_loop.volume = lib.ambientVolume;
        s_loop.loop = true;
        s_loop.Play();
    }

    public static void StopAmbient()
    {
        if (s_loop != null && s_loop.isPlaying) s_loop.Stop();
    }

    private static int PickSource()
    {
        for (int i = 0; i < s_pool.Length; i++)
            if (!s_pool[i].isPlaying) return i;
        // Hepsi meşgul → en çok ilerlemiş olanı devral.
        int best = 0;
        for (int i = 1; i < s_pool.Length; i++)
            if (s_pool[i].time > s_pool[best].time) best = i;
        return best;
    }

    private static IEnumerator StopAfter(int index, int token, float seconds, float volume)
    {
        var src = s_pool[index];
        float hold = Mathf.Max(0f, seconds - FadeOut);
        float t = 0f;
        while (t < hold) { if (s_tokens[index] != token) yield break; t += Time.unscaledDeltaTime; yield return null; }
        t = 0f;
        while (t < FadeOut)
        {
            if (s_tokens[index] != token) yield break;
            t += Time.unscaledDeltaTime;
            src.volume = volume * (1f - t / FadeOut);
            yield return null;
        }
        if (s_tokens[index] == token) src.Stop();
    }

    private static void Ensure()
    {
        if (s_runner != null) return;
        var go = new GameObject("[EventSfx]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        s_runner = go.AddComponent<Runner>();
        var group = Resources.Load<GameEventSfxConfig>("Audio/GameEventSfxConfig")?.sfxGroup;
        s_pool = new AudioSource[PoolSize];
        s_tokens = new int[PoolSize];
        for (int i = 0; i <= PoolSize; i++)
        {
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            if (group != null) src.outputAudioMixerGroup = group;
            if (i < PoolSize) s_pool[i] = src; else s_loop = src;
        }
    }

    private sealed class Runner : MonoBehaviour { }
}
