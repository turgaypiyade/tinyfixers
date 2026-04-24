using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pulse+Pulse combo — SADECE charge animasyonu.
/// Squash → Stretch → Wobble (şişip inen + yamuk) → Peak Hold
///
/// Patlama bu component'te YOK — BoardController charge bittikten sonra
/// mevcut PulseCoreImpactService.PlayPulseCoreExplosionVfxAtCell() ile
/// daha geniş alanda (5x5) patlatır.
///
/// Start()'ta otomatik başlar. ChargeDuration property'si BoardController
/// tarafından senkron için okunur — değeri koruyoruz.
/// </summary>
public class PulsePulseExplosionVfx : MonoBehaviour
{
    [Header("Charge — Bomb")]
    [Tooltip("PulseCore bomba sprite'ı. TileIconLibrary'deki PulseCore.")]
    [SerializeField] private Sprite bombSprite;
    [SerializeField] private float bombBaseSize = 130f;
    [SerializeField] private float chargeDuration = 2.0f;

    [Header("Charge — Phase Ratios (toplamı 1.0 olmalı)")]
    [Tooltip("Çöküp genişleme (anticipation)")]
    [SerializeField, Range(0f, 1f)] private float phaseSquashRatio = 0.15f;
    [Tooltip("Yukarı uzayıp incelme")]
    [SerializeField, Range(0f, 1f)] private float phaseStretchRatio = 0.15f;
    [Tooltip("Şişip inen + yamuk salınımlı büyüme")]
    [SerializeField, Range(0f, 1f)] private float phaseWobbleRatio = 0.55f;
    [Tooltip("En büyükte nabız atan duruş")]
    [SerializeField, Range(0f, 1f)] private float phasePeakRatio = 0.15f;

    [Header("Charge — Squash/Stretch Amounts")]
    [SerializeField] private float squashScaleX = 1.22f;
    [SerializeField] private float squashScaleY = 0.75f;
    [SerializeField] private float stretchScaleX = 0.82f;
    [SerializeField] private float stretchScaleY = 1.32f;

    [Header("Charge — Wobble (yamuk/şişen his)")]
    [Tooltip("x ve y ekseninde zıt fazlı sinüs genliği (0.12 = %12)")]
    [SerializeField] private float wobbleAmplitude = 0.14f;
    [Tooltip("Saniyede kaç şişip-inme döngüsü")]
    [SerializeField] private float wobbleFrequency = 5.5f;
    [Tooltip("Z ekseninde hafif rotasyon salınımı — yamuk hissi verir")]
    [SerializeField] private float tiltDegrees = 8f;

    [Header("Charge — Peak")]
    [Tooltip("Wobble sonunda ulaşılan maksimum uniform scale")]
    [SerializeField] private float peakScale = 1.45f;
    [Tooltip("Peak hold'daki hızlı nabız genliği (%)")]
    [SerializeField] private float peakPulseAmplitude = 0.03f;
    [SerializeField] private float peakPulseFrequency = 18f;

    [Header("Charge — Glow")]
    [Tooltip("Glow sprite (Knob). Bombanın arkasında yumuşak parlama.")]
    [SerializeField] private Sprite glowSprite;
    [SerializeField] private float glowSizeMultiplier = 2.8f;
    [SerializeField] private Color glowColorStart = new Color(0.4f, 0.6f, 1f, 0f);
    [SerializeField] private Color glowColorPeak = new Color(1f, 0.95f, 0.7f, 0.8f);

    private Animator animator;

    public float ChargeDuration => chargeDuration;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator) animator.enabled = false;

        // Mevcut ring/flash child'larını gizle — kullanmıyoruz
        string[] names = { "Ring", "Ring2", "Flash" };
        foreach (var name in names)
        {
            var t = transform.Find(name);
            if (t) t.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        Debug.Log("[PulsePulseExplosionVfx] Start — squash/stretch charge begin");
        StartCoroutine(CoCharge());
    }

    public void PlayStreaks()
    {
        // Geriye uyumluluk — Start() zaten başlatıyor
    }

    // ════════════════════════════════════════════════
    //  CHARGE: Squash → Stretch → Wobble → Peak
    // ════════════════════════════════════════════════
    private IEnumerator CoCharge()
    {
        var parent = transform as RectTransform;
        if (!parent) yield break;

        var container = CreateContainer(parent);

        // Glow (arkada)
        Image glowImg = null;
        if (glowSprite)
        {
            float glowSize = bombBaseSize * glowSizeMultiplier;
            glowImg = CreateUIImage("ChargeGlow", container, glowSprite, glowSize);
            glowImg.color = glowColorStart;
        }

        // Bomba (önde) — deformasyon bunun üstünde yaşayacak
        RectTransform bombRt = null;
        Image bombImg = null;
        if (bombSprite)
        {
            bombImg = CreateUIImage("ChargeBomb", container, bombSprite, bombBaseSize);
            bombImg.preserveAspect = true;
            bombImg.color = Color.white;
            bombRt = bombImg.rectTransform;
        }

        // Evre süreleri — ratio'lar 1'e toplanmayabilir, normalize et
        float ratioSum = Mathf.Max(0.0001f,
            phaseSquashRatio + phaseStretchRatio + phaseWobbleRatio + phasePeakRatio);
        float tSquash = chargeDuration * (phaseSquashRatio / ratioSum);
        float tStretch = chargeDuration * (phaseStretchRatio / ratioSum);
        float tWobble = chargeDuration * (phaseWobbleRatio / ratioSum);
        float tPeak = chargeDuration * (phasePeakRatio / ratioSum);

        Vector3 baseScale = Vector3.one;
        float totalElapsed = 0f;

        // ─── 1) SQUASH — çöküp yanlara genişle ─────────────────
        Vector3 squashTarget = new Vector3(squashScaleX, squashScaleY, 1f);
        yield return AnimatePhase(tSquash, (k, dt) =>
        {
            totalElapsed += dt;
            float eased = EaseOutQuad(k);
            Vector3 s = Vector3.LerpUnclamped(baseScale, squashTarget, eased);
            ApplyScale(container, bombRt, s);
            UpdateGlow(glowImg, totalElapsed, bombImg);
        });

        // ─── 2) STRETCH — yukarı uzayıp incelme ────────────────
        Vector3 stretchTarget = new Vector3(stretchScaleX, stretchScaleY, 1f);
        yield return AnimatePhase(tStretch, (k, dt) =>
        {
            totalElapsed += dt;
            float eased = EaseInOutQuad(k);
            Vector3 s = Vector3.LerpUnclamped(squashTarget, stretchTarget, eased);
            ApplyScale(container, bombRt, s);
            UpdateGlow(glowImg, totalElapsed, bombImg);
        });

        // ─── 3) WOBBLE — şişip inen + yamuk salınımlı büyüme ───
        Vector3 wobbleStart = stretchTarget;
        Vector3 wobbleEnd = Vector3.one * peakScale;
        float wobbleElapsed = 0f;
        yield return AnimatePhase(tWobble, (k, dt) =>
        {
            totalElapsed += dt;
            wobbleElapsed += dt;
            float eased = EaseOutCubic(k);

            // Ana büyüme: stretch → peak
            Vector3 mid = Vector3.Lerp(wobbleStart, wobbleEnd, eased);

            // Non-uniform sinüs: x ve y zıt fazda → şişip inen his
            // Sönümlü: (1 - k) çarpanı ile salınım peak'e yaklaştıkça azalır
            float phase = wobbleElapsed * wobbleFrequency * Mathf.PI * 2f;
            float damp = 1f - k;
            float sx = 1f + Mathf.Sin(phase) * wobbleAmplitude * damp;
            float sy = 1f + Mathf.Sin(phase + Mathf.PI) * wobbleAmplitude * damp;

            Vector3 deformed = new Vector3(mid.x * sx, mid.y * sy, 1f);
            ApplyScale(container, bombRt, deformed);

            // Yamuk his — Z rotasyonu damped sine
            float tilt = Mathf.Sin(phase * 0.8f) * tiltDegrees * damp;
            if (bombRt) bombRt.localRotation = Quaternion.Euler(0f, 0f, tilt);

            UpdateGlow(glowImg, totalElapsed, bombImg);
        });

        // ─── 4) PEAK HOLD — en büyükte hızlı nabız ─────────────
        if (bombRt) bombRt.localRotation = Quaternion.identity;
        float peakElapsed = 0f;
        yield return AnimatePhase(tPeak, (k, dt) =>
        {
            totalElapsed += dt;
            peakElapsed += dt;

            float pulse = Mathf.Sin(peakElapsed * peakPulseFrequency) * peakPulseAmplitude;
            Vector3 s = wobbleEnd * (1f + pulse);
            ApplyScale(container, bombRt, s);

            // Son evrede glow full-peak + hafif flash
            UpdateGlowPeak(glowImg, totalElapsed, bombImg, k);
        });

        Destroy(container.gameObject);
        Debug.Log("[PulsePulseExplosionVfx] Charge done");
    }

    // ────────────────────────────────────────────────
    //  Phase runner — süre bitene kadar her frame callback çağırır
    //  Callback: (k = normalized progress, dt = unscaledDeltaTime)
    // ────────────────────────────────────────────────
    private IEnumerator AnimatePhase(float duration, System.Action<float, float> onTick)
    {
        if (duration <= 0f) yield break;

        float t = 0f;
        while (t < duration)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / duration);
            onTick(k, dt);
            yield return null;
        }
    }

    // Scale'i hem container hem bomb'a uygula. Glow'u bozmamak için
    // sadece bomb üzerinde non-uniform deformasyon yapıyoruz; container'a
    // uniform genel ölçek veriyoruz ki glow da büyüsün ama yamulmasın.
    private void ApplyScale(RectTransform container, RectTransform bombRt, Vector3 nonUniform)
    {
        // Container uniform: ortalama büyüme (glow da orantılı büyür)
        float uniform = (nonUniform.x + nonUniform.y) * 0.5f;
        container.localScale = Vector3.one * uniform;

        // Bomb non-uniform üstüne binecek — container zaten uniform büyüttü,
        // yani bomb'un kendi local scale'i sadece "oran farkını" vermeli:
        if (bombRt)
        {
            float bx = uniform > 0.0001f ? nonUniform.x / uniform : 1f;
            float by = uniform > 0.0001f ? nonUniform.y / uniform : 1f;
            bombRt.localScale = new Vector3(bx, by, 1f);
        }
    }

    private void UpdateGlow(Image glowImg, float totalElapsed, Image bombImg)
    {
        if (!glowImg) return;
        float u = Mathf.Clamp01(totalElapsed / chargeDuration);
        Color gc = Color.Lerp(glowColorStart, glowColorPeak, u * u);
        glowImg.color = gc;

        if (bombImg)
        {
            float flashU = Mathf.Clamp01((u - 0.75f) / 0.25f);
            bombImg.color = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.78f), flashU);
        }
    }

    private void UpdateGlowPeak(Image glowImg, float totalElapsed, Image bombImg, float peakK)
    {
        if (glowImg)
        {
            Color gc = glowColorPeak;
            // Peak hold'da hafif titreyen alfa
            float pulse = Mathf.Sin(totalElapsed * peakPulseFrequency) * 0.1f;
            gc.a = Mathf.Clamp01(gc.a + pulse);
            glowImg.color = gc;
        }

        if (bombImg)
        {
            // Peak'in son %40'ında beyaza doğru çok hafif yıkama
            float bleach = Mathf.Clamp01((peakK - 0.6f) / 0.4f) * 0.35f;
            bombImg.color = Color.Lerp(new Color(1f, 0.92f, 0.78f), Color.white, bleach);
        }
    }

    // ────────────────────────────────────────────────
    //  Utility
    // ────────────────────────────────────────────────
    private RectTransform CreateContainer(RectTransform parent)
    {
        var go = new GameObject("ChargeContainer", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private Image CreateUIImage(string name, RectTransform parent, Sprite sprite, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // Easing
    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
}