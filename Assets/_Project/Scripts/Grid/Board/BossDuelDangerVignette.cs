using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss Düello can uyarısı (kullanıcı 2026-11-08): ekran KENARINDA kırmızı, yumuşak, "nefes alan" çerçeve +
/// her nabızda kalp/alarm sesi. Orta tamamen şeffaf (tahta örtülmez), dokunmayı engellemez.
/// Seviye 0 kapalı · 1 yavaş nabız · 2 hızlı nabız. Yalnız açıkken Update döner (kapalıyken bileşen disabled).
/// Görsel: Resources/UIEffects/DangerVignette (9-slice). Ses: Resources/Audio/BossDuelsSound/DangerHeartbeatAlarm,
/// merkezi tahta sesi (BoardAudioDirector.PlayOneShotClip → GameSettings.SoundEnabled'a uyar).
/// </summary>
public sealed class BossDuelDangerVignette : MonoBehaviour
{
    private static readonly Color Red = new Color(1f, 0.08f, 0.06f, 1f);
    private const float FadeOutSeconds = 0.35f;

    private Image image;
    private RectTransform frame;
    private BoardAudioDirector audioDirector;
    private AudioClip pulseClip;
    private int level;
    private float phase;
    private float fadeOut = -1f;
    private float lastAlpha;

    public static BossDuelDangerVignette Create(BoardAudioDirector audio)
    {
        var go = new GameObject("BossDuelDangerVignette", typeof(RectTransform));
        go.layer = 5;   // UI
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 400;   // oyun arayüzünün üstü; düello bitince/fail'de kapanır
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer));
        frameGo.layer = 5;
        var rt = (RectTransform)frameGo.transform;
        rt.SetParent(go.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = frameGo.AddComponent<Image>();
        img.sprite = Resources.Load<Sprite>("UIEffects/DangerVignette");
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.6f;   // kenar bandı ~200 birim (ekran genişliğinin ~%18'i)
        img.raycastTarget = false;
        img.color = new Color(Red.r, Red.g, Red.b, 0f);

        var view = go.AddComponent<BossDuelDangerVignette>();
        view.image = img;
        view.frame = rt;
        view.audioDirector = audio;
        view.pulseClip = Resources.Load<AudioClip>("Audio/BossDuelsSound/DangerHeartbeatAlarm");
        go.SetActive(false);
        return view;
    }

    /// <summary>0 kapalı (yumuşak söner), 1 yavaş, 2 hızlı. Aynı seviye tekrar verilirse dokunmaz.</summary>
    public void SetLevel(int newLevel, bool instant = false)
    {
        newLevel = Mathf.Clamp(newLevel, 0, 2);
        if (newLevel == 0)
        {
            level = 0;
            if (instant || !gameObject.activeSelf) { Hide(); return; }
            if (fadeOut < 0f) fadeOut = 0f;
            return;
        }
        fadeOut = -1f;
        if (level == 0)
        {
            phase = 0f;   // yeni uyarı: ilk nabız + ses hemen
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            PlayPulseSound();
        }
        level = newLevel;
    }

    private void Hide()
    {
        fadeOut = -1f;
        if (image != null) image.color = new Color(Red.r, Red.g, Red.b, 0f);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (fadeOut >= 0f)
        {
            fadeOut += dt;
            float a = Mathf.Lerp(lastAlpha, 0f, fadeOut / FadeOutSeconds);
            image.color = new Color(Red.r, Red.g, Red.b, a);
            if (fadeOut >= FadeOutSeconds) Hide();
            return;
        }

        float period = level >= 2 ? 0.6f : 1.2f;
        phase += dt / period;
        if (phase >= 1f)
        {
            phase -= Mathf.Floor(phase);
            PlayPulseSound();
        }

        // Nabız: hızlı parlama, yavaş sönme; parlakken çerçeve içeri gelir (ölçek 1), sönükken dışarı kayar.
        float beat = phase < 0.18f ? phase / 0.18f : 1f - (phase - 0.18f) / 0.82f;
        beat = beat * beat * (3f - 2f * beat);
        float minA = level >= 2 ? 0.22f : 0.10f, maxA = level >= 2 ? 0.80f : 0.55f;
        lastAlpha = Mathf.Lerp(minA, maxA, beat);
        image.color = new Color(Red.r, Red.g, Red.b, lastAlpha);
        frame.localScale = Vector3.one * Mathf.Lerp(1.07f, 1f, beat);
    }

    private void PlayPulseSound()
    {
        if (audioDirector != null && pulseClip != null)
            audioDirector.PlayOneShotClip(pulseClip, level >= 2 ? 1.25f : 1f);   // dosya -12 LUFS (düello vuruş sesleri ayarında)
    }
}
