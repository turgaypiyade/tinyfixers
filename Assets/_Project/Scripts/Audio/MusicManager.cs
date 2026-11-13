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

        AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;

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
        {
            ScheduleStallCheck();   // "çalıyor" görünüp takılmış olabilir (iOS kesinti sonrası)
            return;
        }

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        float targetVolume = Mathf.Clamp01(masterVolume * volumeMultiplier);
        fadeRoutine = StartCoroutine(FadeToClip(clip, targetVolume));
    }

    // ── iOS kesinti koruması ──
    // Telefon kilitlenip açılınca (ses oturumu kesintisi) Streaming müzik kaynağı isPlaying=true kalıp sessiz
    // takılabiliyor; aynı parçayı isteyen sahne "zaten çalıyor" deyip geçince müzik hiç gelmiyordu (kullanıcı
    // 2026-11-13, iPhone). Öne gelişte ve aynı-parça isteğinde kısa denetim: zaman ilerlemiyorsa yeniden başlat.
    // Coroutine yalnız ~1 sn yaşar; sürekli izleme yok.
    private Coroutine stallRoutine;
    private float lastAudioReset = -100f;

    private void OnDestroy()
    {
        if (Instance == this) AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
    }

    // Android/iOS: telefon görüşmesi, kulaklık/bluetooth ses çıkışını değiştirir → Unity ses sistemini sıfırlar ve
    // çalan TÜM kaynakları durdurur; müziği yeniden başlatan yoktu (kuzen, Android: "telefon gelince dönünce sesler gidiyor").
    private void HandleAudioConfigurationChanged(bool deviceWasChanged)
    {
        if (Time.realtimeSinceStartup - lastAudioReset < 1f) return;   // kendi Reset'imizin bildirimi
        ScheduleStallCheck();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) ScheduleStallCheck();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) ScheduleStallCheck();
    }

    private void ScheduleStallCheck()
    {
        if (!isActiveAndEnabled) return;
        if (stallRoutine != null) StopCoroutine(stallRoutine);
        stallRoutine = StartCoroutine(CheckStall());
    }

    private IEnumerator CheckStall()
    {
        yield return new WaitForSecondsRealtime(0.4f);   // kesinti dönüşü otursun
        if (source == null || source.clip == null || fadeRoutine != null) { stallRoutine = null; yield break; }
        if (!source.isPlaying)
        {
            source.Play();
            stallRoutine = null;
            yield break;
        }
        float before = source.time;
        yield return new WaitForSecondsRealtime(0.6f);
        if (source != null && source.clip != null && fadeRoutine == null && source.isPlaying
            && Mathf.Approximately(source.time, before))
        {
            // Kaynak takılı: ses çıkışı da düşmüş olabilir → bir kez yeniden kur (yalnız takılma görülünce; çalan
            // kısa efektler kesilir, sonrakiler normal çalar), sonra müziği baştan başlat.
            if (Time.realtimeSinceStartup - lastAudioReset > 10f)   // Reset de bildirim tetikler → döngü olmasın
            {
                lastAudioReset = Time.realtimeSinceStartup;
                AudioSettings.Reset(AudioSettings.GetConfiguration());
            }
            source.Stop();
            source.Play();
        }
        stallRoutine = null;
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