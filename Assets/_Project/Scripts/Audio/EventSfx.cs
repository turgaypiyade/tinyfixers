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

    /// <summary>Token ownership prevents an old screen from stopping a voice subsequently reused by the pool.</summary>
    public readonly struct Handle
    {
        internal readonly int Index, Token;
        internal Handle(int index, int token) { Index = index; Token = token; }
    }

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
        => PlayTracked(pick, volumeScale, pitch, durationOverride);

    public static Handle PlayTracked(Func<EventSfxLibrary, EventSfxCue> pick, float volumeScale = 1f, float pitch = 1f,
        float durationOverride = 0f)
    {
        var lib = Library;
        if (lib == null || pick == null || !GameSettings.SoundEnabled) return default;
        var cue = pick(lib);
        if (cue == null || cue.clips == null || cue.clips.Length == 0) return default;
        var clip = cue.clips[UnityEngine.Random.Range(0, cue.clips.Length)];
        if (clip == null) return default;

        Ensure();
        int i = PickSource();
        if (i < 0) return default;
        var src = s_pool[i];
        int token = ++s_tokens[i];
        src.Stop();
        src.clip = clip;
        src.loop = false;
        src.volume = Mathf.Clamp01(cue.volume * volumeScale);
        src.pitch = Mathf.Clamp(pitch * (1f + UnityEngine.Random.Range(-cue.pitchJitter, cue.pitchJitter)), 0.1f, 3f);
        src.mute = !GameSettings.SoundEnabled;
        src.Play();

        float limit = durationOverride > 0f ? durationOverride : cue.maxDuration;
        if (limit > 0f && limit < clip.length)
            s_runner.StartCoroutine(StopAfter(i, token, limit, src.volume));
        return new Handle(i, token);
    }

    // BoardAudioDirector borrows the existing central voice pool for owned clip loops.
    // Loop voices are protected from one-shot stealing; no obstacle creates AudioSources.
    public static Handle PlayTrackedClip(AudioClip clip, float volume, bool loop, float pitch = 1f)
    {
        if (clip == null || !GameSettings.SoundEnabled) return default;
        Ensure();
        int i = PickSource();
        if (i < 0) return default;
        var src = s_pool[i]; int token = ++s_tokens[i];
        src.Stop(); src.clip = clip; src.loop = loop; src.volume = Mathf.Clamp01(volume);
        src.pitch = Mathf.Clamp(pitch, 0.1f, 3f); src.mute = !GameSettings.SoundEnabled; src.Play();
        return new Handle(i, token);
    }

    public static bool IsPlaying(Handle handle) => handle.Token != 0 && s_pool != null
        && handle.Index >= 0 && handle.Index < s_pool.Length && s_tokens[handle.Index] == handle.Token
        && s_pool[handle.Index] != null && s_pool[handle.Index].isPlaying;

    public static void SetVolume(Handle handle, float volume)
    {
        if (handle.Token == 0 || s_pool == null || handle.Index < 0 || handle.Index >= s_pool.Length
            || s_tokens[handle.Index] != handle.Token || s_pool[handle.Index] == null) return;
        s_pool[handle.Index].volume = Mathf.Clamp01(volume);
    }

    public static void Stop(Handle handle)
    {
        if (handle.Token == 0 || s_pool == null || handle.Index < 0 || handle.Index >= s_pool.Length
            || s_tokens[handle.Index] != handle.Token) return;
        ++s_tokens[handle.Index];
        var source = s_pool[handle.Index];
        if (source == null) return;
        source.Stop();
        source.clip = null;
        source.loop = false;
    }

    public static void StartAmbient()
    {
        var lib = Library;
        if (lib == null || lib.ambientLoop == null || !GameSettings.SoundEnabled) return;
        Ensure();
        s_runner.StopAmbientFade();
        s_loop.volume = lib.ambientVolume;
        if (s_loop.isPlaying && s_loop.clip == lib.ambientLoop) return;
        s_loop.clip = lib.ambientLoop;
        s_loop.loop = true;
        s_loop.mute = !GameSettings.SoundEnabled;
        s_loop.Play();
    }

    public static void StopAmbient()
    {
        if (s_runner != null) s_runner.StopAmbientFade();
        if (s_loop != null) { s_loop.Stop(); s_loop.clip = null; }
    }

    // Ekrana özel ortam döngüsü (ör. şehir gezisi uğultusu): merkezi loop kaynağını kullanır, ses ayarına uyar
    // (kapalıysa başlamaz; açıkken kapatılırsa ApplySound anında susturur). Yumuşak giriş.
    public static void StartAmbientClip(AudioClip clip, float volume, float fadeIn = 0.6f)
    {
        if (clip == null || !GameSettings.SoundEnabled) return;
        Ensure();
        s_runner.StopAmbientFade();
        s_loop.clip = clip;
        s_loop.loop = true;
        s_loop.volume = 0f;
        s_loop.mute = !GameSettings.SoundEnabled;
        s_loop.Play();
        s_runner.FadeAmbient(Mathf.Clamp01(volume), fadeIn, stopAtEnd: false);
    }

    // Yalnız verilen klip çalıyorsa yavaşça kısıp durdurur (başka ortam sesine dokunmaz).
    public static void FadeOutAmbientClip(AudioClip clip, float seconds = 0.4f)
    {
        if (s_loop == null || s_runner == null || clip == null || s_loop.clip != clip || !s_loop.isPlaying) return;
        s_runner.FadeAmbient(0f, seconds, stopAtEnd: true);
    }

    private static int PickSource()
    {
        for (int i = 0; i < s_pool.Length; i++)
            if (!s_pool[i].isPlaying) return i;
        // Hepsi meşgul → en çok ilerlemiş olanı devral.
        int best = -1;
        for (int i = 0; i < s_pool.Length; i++)
            if (!s_pool[i].loop && (best < 0 || s_pool[i].time > s_pool[best].time)) best = i;
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
        s_runner.ApplySound(GameSettings.SoundEnabled);
    }

    private sealed class Runner : MonoBehaviour
    {
        private Coroutine _ambientFade;
        private float _ambientTargetVolume;
        private bool _ambientStopAtEnd;

        public void StopAmbientFade()
        {
            if (_ambientFade != null) StopCoroutine(_ambientFade);
            _ambientFade = null;
        }

        public void FadeAmbient(float to, float seconds, bool stopAtEnd)
        {
            StopAmbientFade();
            _ambientTargetVolume = to;
            _ambientStopAtEnd = stopAtEnd;
            _ambientFade = StartCoroutine(FadeAmbientRoutine(to, seconds, stopAtEnd));
        }

        private IEnumerator FadeAmbientRoutine(float to, float seconds, bool stopAtEnd)
        {
            float from = s_loop.volume;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                s_loop.volume = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            s_loop.volume = to;
            if (stopAtEnd) { s_loop.Stop(); s_loop.clip = null; }
            _ambientFade = null;
        }

        private void OnEnable()
        {
            GameSettings.OnSoundChanged += ApplySound;
            ApplySound(GameSettings.SoundEnabled);
        }

        public void ApplySound(bool enabled)
        {
            if (!enabled)
            {
                // Kapatma fade'ini yarım bırakıp sonraki unmute'ta eski şehri çalma.
                // Giriş fade'inde ise hedef sesi sakla; yeniden açılınca normal seviyede duyulsun.
                if (_ambientFade != null && s_loop != null)
                {
                    StopAmbientFade();
                    if (_ambientStopAtEnd) { s_loop.Stop(); s_loop.clip = null; }
                    else s_loop.volume = _ambientTargetVolume;
                }
                StopAllCoroutines();
            }
            if (s_pool != null)
                for (int i = 0; i < s_pool.Length; i++)
                {
                    var source = s_pool[i];
                    if (source == null) continue;
                    source.mute = !enabled;
                    if (enabled) continue;
                    ++s_tokens[i];
                    source.Stop(); source.clip = null;
                }
            if (s_loop != null) s_loop.mute = !enabled;
        }

        private void OnDisable()
        {
            GameSettings.OnSoundChanged -= ApplySound;
            ApplySound(false);
            if (s_loop != null) s_loop.Stop();
        }

        private void OnDestroy()
        {
            if (s_runner != this) return;
            s_runner = null; s_pool = null; s_tokens = null; s_loop = null;
        }
    }
}
