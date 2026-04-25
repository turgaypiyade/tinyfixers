using System.Collections;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource source;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.45f;

    [Header("Fade")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.75f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (source == null)
            source = GetComponent<AudioSource>();

        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f; // 2D music
        source.volume = masterVolume;
    }

    public void Play(AudioClip clip, float volumeMultiplier = 1f, bool restartIfSame = false)
    {
        if (clip == null)
            return;

        if (source.clip == clip && source.isPlaying && !restartIfSame)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        float targetVolume = Mathf.Clamp01(masterVolume * volumeMultiplier);
        fadeRoutine = StartCoroutine(FadeToClip(clip, targetVolume));
    }

    public void Stop()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeOutAndStop());
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);

        if (source != null)
            source.volume = masterVolume;
    }

    private IEnumerator FadeToClip(AudioClip newClip, float targetVolume)
    {
        if (fadeSeconds <= 0f)
        {
            source.Stop();
            source.clip = newClip;
            source.volume = targetVolume;
            source.Play();
            fadeRoutine = null;
            yield break;
        }

        float startVolume = source.volume;

        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(startVolume, 0f, t / fadeSeconds);
            yield return null;
        }

        source.Stop();
        source.clip = newClip;
        source.volume = 0f;
        source.Play();

        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(0f, targetVolume, t / fadeSeconds);
            yield return null;
        }

        source.volume = targetVolume;
        fadeRoutine = null;
    }

    private IEnumerator FadeOutAndStop()
    {
        if (source == null)
            yield break;

        if (fadeSeconds <= 0f)
        {
            source.Stop();
            source.clip = null;
            source.volume = masterVolume;
            fadeRoutine = null;
            yield break;
        }

        float startVolume = source.volume;

        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(startVolume, 0f, t / fadeSeconds);
            yield return null;
        }

        source.Stop();
        source.clip = null;
        source.volume = masterVolume;
        fadeRoutine = null;
    }
}