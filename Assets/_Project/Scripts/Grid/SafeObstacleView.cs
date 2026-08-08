using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bir Safe (kasa) instance'ının görseli. GridSpawner her SafeEntry için bir tane spawn eder ve
/// Setup ile SafeObstacleService'e + origin'e bağlar. Görsel mantık:
///   - Body (mor gövde) GridSpawner tarafından NxN boyutuna ölçeklenir.
///   - LockPanel (ön yüz + 3 knob + 3 sayaç) sabit boyutta, gövdeyle büyümez (prefab'da kurulur).
///   - Her vuruşta: aktif kilidin sayacı güncellenir + knob'u yukarıdan aşağı kademeli kayar
///     (progress = (total-remaining)/total). Kilit kapanınca knob en altta sabit kalır.
///   - Kasa kırılınca: kırık sprite gösterilir, panel gizlenir, opsiyonel break particle.
///
/// Knob'ları prefab'da slot TEPESİNE yerleştir; view o pozisyonu "top" alır ve aşağı kaydırır.
/// </summary>
public sealed class SafeObstacleView : MonoBehaviour
{
    [Header("Body / Panel")]
    [Tooltip("Mor gövde — GridSpawner NxN boyutuna ölçekler.")]
    [SerializeField] private RectTransform bodyRect;
    [Tooltip("Ön yüz + knob + sayaçları içeren sabit panel (gövdeyle büyümez).")]
    [SerializeField] private GameObject lockPanel;
    [Tooltip("Kırık/açık kasa görseli — başta gizli, kırılınca gösterilir.")]
    [SerializeField] private GameObject brokenVisual;

    [Header("Locks (sıra: kırmızı, sarı, yeşil)")]
    [Tooltip("3 knob — prefab'da slot TEPESİNE yerleştir (top pozisyonu buradan alınır).")]
    [SerializeField] private RectTransform[] knobs = new RectTransform[3];
    [Tooltip("3 sayaç (kalan hit). Renkleri prefab'da ayarla.")]
    [SerializeField] private TMP_Text[] counters = new TMP_Text[3];

    [Header("Hit Mode Görseli")]
    [Tooltip("Ordered modda aktif olmayan ama hâlâ açık kilitlerin alpha değeri.")]
    [SerializeField, Range(0.1f, 1f)] private float inactiveOpenLockAlpha = 0.38f;
    [Tooltip("Kapanmış kilitlerin alpha değeri.")]
    [SerializeField, Range(0.1f, 1f)] private float closedLockAlpha = 0.58f;
    [Tooltip("Ordered modda aktif kilide uygulanacak hafif ölçek vurgusu.")]
    [SerializeField, Range(1f, 1.2f)] private float activeLockScale = 1.06f;

    [Header("Animasyon")]
    [Tooltip("Knob progress=1'de yukarıdan ne kadar AŞAĞI kayar (panel-local birim). Slot yüksekliği - knob yüksekliği.")]
    [SerializeField] private float knobTravelY = 102.797f;
    [SerializeField, Min(0.05f)] private float knobSlideDuration = 0.35f;

    [Header("Break FX (opsiyonel)")]
    [SerializeField] private GameObject breakParticlePrefab;
    [SerializeField, Min(0f)] private float brokenVisualDuration = 0.45f;

    [Header("Break Animasyonu")]
    [Tooltip("Kırılma öncesi kısa wind-up squash süresi.")]
    [SerializeField, Min(0f)] private float breakAnticipation = 0.07f;
    [Tooltip("Kırılma anında gövde titreme süresi/şiddeti (px).")]
    [SerializeField, Min(0f)] private float breakShakeDuration = 0.20f;
    [SerializeField, Min(0f)] private float breakShakeStrength = 11f;
    [Tooltip("Kırılınca gövdenin punch ölçeği (overshoot).")]
    [SerializeField, Range(1f, 1.4f)] private float breakBodyPunch = 1.14f;
    [Tooltip("Knob'ların patlama (scale-up + sönme) süresi.")]
    [SerializeField, Min(0f)] private float knobBurstDuration = 0.16f;
    [Tooltip("brokenVisual'ın pop-in süresi ve overshoot'u.")]
    [SerializeField, Min(0f)] private float revealPopDuration = 0.28f;
    [SerializeField, Range(1f, 1.6f)] private float revealPopOvershoot = 1.18f;

    [Header("Reveal Glow (opsiyonel)")]
    [Tooltip("İçeriğin ARKASINA konacak yumuşak radial glow Image'i. Başta gizli; kasa açılınca " +
             "büyüyüp sönerek 'kilit açıldı, ödül geldi' hissi verir. Atanmazsa no-op.")]
    [SerializeField] private RectTransform revealGlow;
    [Tooltip("Glow'un ulaşacağı ölçek ve toplam süre.")]
    [SerializeField, Range(1f, 2.5f)] private float revealGlowScale = 1.6f;
    [SerializeField, Min(0f)] private float revealGlowDuration = 0.5f;
    [Tooltip("Glow'un tepe alpha'sı.")]
    [SerializeField, Range(0f, 1f)] private float revealGlowPeakAlpha = 0.85f;

    private SafeObstacleService service;
    private int origin = -1;
    private Vector2[] knobTop;          // her knob'un başlangıç (top) anchoredPosition'ı
    private Vector3[] knobBaseScale;
    private Coroutine[] knobCo;
    private Coroutine focusRefreshCo;

    /// GridSpawner çağırır: service'e bağla, origin'i ata, knob başlangıçlarını yakala.
    public void Setup(SafeObstacleService svc, int safeOrigin)
    {
        service = svc;
        origin  = safeOrigin;

        knobTop = new Vector2[knobs.Length];
        knobBaseScale = new Vector3[knobs.Length];
        knobCo  = new Coroutine[knobs.Length];
        for (int i = 0; i < knobs.Length; i++)
        {
            if (knobs[i] == null) continue;
            knobTop[i] = knobs[i].anchoredPosition;
            knobBaseScale[i] = knobs[i].localScale;
        }

        if (brokenVisual != null) brokenVisual.SetActive(false);
        if (lockPanel != null)    lockPanel.SetActive(true);

        // Başlangıç sayaçları.
        for (int i = 0; i < counters.Length; i++)
            if (counters[i] != null && service != null)
                counters[i].text = service.GetTotal(origin, i).ToString();

        if (service != null)
        {
            service.OnSafeHit    += HandleSafeHit;
            service.OnLockClosed += HandleLockClosed;
            service.OnSafeBroken += HandleSafeBroken;
        }

        UpdateLockFocusVisuals();
    }

    /// Body'yi NxN boyutuna ölçekler (GridSpawner çağırır).
    public void SetBodySize(float width, float height)
    {
        if (bodyRect != null) bodyRect.sizeDelta = new Vector2(width, height);
    }

    private void OnDestroy()
    {
        if (service != null)
        {
            service.OnSafeHit    -= HandleSafeHit;
            service.OnLockClosed -= HandleLockClosed;
            service.OnSafeBroken -= HandleSafeBroken;
        }
    }

    private void HandleSafeHit(int o, int lockIdx, int remaining, int total)
    {
        if (o != origin) return;
        if (lockIdx < 0 || lockIdx >= knobs.Length) return;

        if (counters[lockIdx] != null) counters[lockIdx].text = remaining.ToString();

        float progress = total > 0 ? (float)(total - remaining) / total : 1f;
        SlideKnob(lockIdx, progress);

        if (remaining > 0)
            UpdateLockFocusVisuals();
        else
            ScheduleLockFocusRefresh();
    }

    private void HandleLockClosed(int o, int lockIdx)
    {
        if (o != origin) return;
        SlideKnob(lockIdx, 1f);   // en altta sabit
        ScheduleLockFocusRefresh();
    }

    private void HandleSafeBroken(int o)
    {
        if (o != origin) return;
        SetRaycastTargets(false);
        StartCoroutine(CoPlaySafeBreak());
    }

    // Kırılma sekansı: wind-up squash → particle + knob patlaması + gövde punch & shake →
    // brokenVisual overshoot ile pop-in → bekle → yok et. Tüm adımlar null-güvenli;
    // referanslar boşsa eski davranışa yakın (anında swap) düşer.
    private IEnumerator CoPlaySafeBreak()
    {
        var body = bodyRect != null ? bodyRect : transform as RectTransform;
        Vector3 baseScale = body != null ? body.localScale : Vector3.one;
        Vector2 basePos   = body != null ? body.anchoredPosition : Vector2.zero;

        // 1) Anticipation — kısa squash (yatay genişle, dikey bas).
        if (body != null && breakAnticipation > 0f)
            yield return CoScaleTo(body, baseScale,
                new Vector3(baseScale.x * 1.06f, baseScale.y * 0.90f, baseScale.z), breakAnticipation);

        // 2) Burst — particle + knob patlaması + gövde punch + shake (eşzamanlı).
        if (breakParticlePrefab != null)
            Instantiate(breakParticlePrefab, transform.position, Quaternion.identity, transform.parent);

        BurstKnobs();

        if (body != null)
        {
            StartCoroutine(CoShake(body, basePos, breakShakeStrength, breakShakeDuration));
            yield return CoScaleTo(body, body.localScale, baseScale * breakBodyPunch, 0.06f); // punch out
            yield return CoScaleTo(body, body.localScale, baseScale, 0.10f);                  // punch back
        }

        if (lockPanel != null) lockPanel.SetActive(false);

        // 3) Reveal — içeriğin arkasında glow açılır + brokenVisual overshoot ile pop-in.
        if (revealGlow != null)
            StartCoroutine(CoRevealGlow());

        if (brokenVisual != null)
        {
            brokenVisual.SetActive(true);
            if (brokenVisual.transform is RectTransform brt)
                yield return CoPopIn(brt, revealPopDuration, revealPopOvershoot);
        }

        // Gövde normale otur.
        if (body != null) { body.localScale = baseScale; body.anchoredPosition = basePos; }

        if (brokenVisualDuration > 0f)
            yield return new WaitForSeconds(brokenVisualDuration);

        Destroy(gameObject);
    }

    // Knob'ları yerinde patlat: hızlı büyü + sön (kilitler "kopuyor" hissi).
    private void BurstKnobs()
    {
        if (knobs == null) return;
        for (int i = 0; i < knobs.Length; i++)
            if (knobs[i] != null)
            {
                if (knobCo != null && i < knobCo.Length && knobCo[i] != null) StopCoroutine(knobCo[i]);
                StartCoroutine(CoKnobBurst(knobs[i]));
            }
    }

    private IEnumerator CoKnobBurst(RectTransform knob)
    {
        Vector3 s0 = knob.localScale;
        var graphics = knob.GetComponentsInChildren<Graphic>(true);
        float d = Mathf.Max(0.01f, knobBurstDuration);
        float t = 0f;
        while (t < d && knob != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            // 0→0.35: 1→1.35 büyü, 0.35→1: 1.35→0 küçül; alpha 1→0.
            float scale = k < 0.35f
                ? Mathf.Lerp(1f, 1.35f, k / 0.35f)
                : Mathf.Lerp(1.35f, 0f, (k - 0.35f) / 0.65f);
            knob.localScale = s0 * scale;
            for (int g = 0; g < graphics.Length; g++) SetGraphicAlpha(graphics[g], 1f - k);
            yield return null;
        }
        if (knob != null) knob.gameObject.SetActive(false);
    }

    private IEnumerator CoScaleTo(RectTransform rt, Vector3 from, Vector3 to, float dur)
    {
        float d = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < d && rt != null)
        {
            t += Time.deltaTime;
            float e = Mathf.Clamp01(t / d);
            e = e * e * (3f - 2f * e); // smoothstep
            rt.localScale = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }
        if (rt != null) rt.localScale = to;
    }

    private IEnumerator CoShake(RectTransform rt, Vector2 basePos, float strength, float dur)
    {
        float d = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < d && rt != null)
        {
            t += Time.deltaTime;
            float damp = 1f - Mathf.Clamp01(t / d);          // sönümlü
            Vector2 off = new Vector2(
                (Mathf.PerlinNoise(Time.time * 40f, 0f) - 0.5f),
                (Mathf.PerlinNoise(0f, Time.time * 40f) - 0.5f)) * (strength * 2f * damp);
            rt.anchoredPosition = basePos + off;
            yield return null;
        }
        if (rt != null) rt.anchoredPosition = basePos;
    }

    // Overshoot'lu pop-in: 0 → overshoot → 1 (back-ease hissi).
    private IEnumerator CoPopIn(RectTransform rt, float dur, float overshoot)
    {
        Vector3 target = rt.localScale;
        float d = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < d && rt != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            // 0→0.6: 0→overshoot, 0.6→1: overshoot→1
            float s = k < 0.6f
                ? Mathf.Lerp(0f, overshoot, k / 0.6f)
                : Mathf.Lerp(overshoot, 1f, (k - 0.6f) / 0.4f);
            rt.localScale = target * s;
            yield return null;
        }
        if (rt != null) rt.localScale = target;
    }

    // İçeriğin arkasında ışıma: 0.5→revealGlowScale büyür, alpha hızlı yükselip söner. Sonra gizle.
    private IEnumerator CoRevealGlow()
    {
        revealGlow.gameObject.SetActive(true);
        var graphics = revealGlow.GetComponentsInChildren<Graphic>(true);
        Vector3 baseScale = revealGlow.localScale;
        float d = Mathf.Max(0.01f, revealGlowDuration);
        float t = 0f;
        while (t < d && revealGlow != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            float ease = 1f - Mathf.Pow(1f - k, 3f);                       // OutCubic (büyüme)
            revealGlow.localScale = baseScale * Mathf.Lerp(0.5f, revealGlowScale, ease);
            // alpha: 0→peak (ilk %25) → 0 (kalan) — bloom sonra sön.
            float a = k < 0.25f ? Mathf.Lerp(0f, revealGlowPeakAlpha, k / 0.25f)
                                : Mathf.Lerp(revealGlowPeakAlpha, 0f, (k - 0.25f) / 0.75f);
            for (int g = 0; g < graphics.Length; g++) SetGraphicAlpha(graphics[g], a);
            yield return null;
        }
        if (revealGlow != null) revealGlow.gameObject.SetActive(false);
    }

    private void SetRaycastTargets(bool value)
    {
        var graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = value;
    }

    private void ScheduleLockFocusRefresh()
    {
        if (focusRefreshCo == null)
            focusRefreshCo = StartCoroutine(CoRefreshLockFocusNextFrame());
    }

    private IEnumerator CoRefreshLockFocusNextFrame()
    {
        yield return null;
        focusRefreshCo = null;
        UpdateLockFocusVisuals();
    }

    private void UpdateLockFocusVisuals()
    {
        if (service == null)
            return;

        SafeLockHitMode hitMode = service.GetHitMode(origin);
        int activeLock = service.GetActiveLock(origin);
        int count = Mathf.Max(knobs != null ? knobs.Length : 0, counters != null ? counters.Length : 0);

        for (int i = 0; i < count; i++)
        {
            bool open = service.GetRemaining(origin, i) > 0;
            bool active = open && hitMode == SafeLockHitMode.Ordered && i == activeLock;
            float alpha = ResolveLockAlpha(hitMode, open, active);

            SetLockAlpha(i, alpha);

            if (knobs != null && i < knobs.Length && knobs[i] != null && knobBaseScale != null && i < knobBaseScale.Length)
                knobs[i].localScale = knobBaseScale[i] * (active ? activeLockScale : 1f);
        }
    }

    private float ResolveLockAlpha(SafeLockHitMode hitMode, bool open, bool active)
    {
        if (!open)
            return closedLockAlpha;
        if (hitMode == SafeLockHitMode.AnyColor)
            return 1f;
        return active ? 1f : inactiveOpenLockAlpha;
    }

    private void SetLockAlpha(int lockIndex, float alpha)
    {
        if (knobs != null && lockIndex >= 0 && lockIndex < knobs.Length && knobs[lockIndex] != null)
        {
            var graphics = knobs[lockIndex].GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                SetGraphicAlpha(graphics[i], alpha);
        }

        if (counters != null && lockIndex >= 0 && lockIndex < counters.Length && counters[lockIndex] != null)
            SetGraphicAlpha(counters[lockIndex], alpha);
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private void SlideKnob(int i, float progress)
    {
        if (i < 0 || i >= knobs.Length || knobs[i] == null || knobTop == null) return;
        Vector2 target = knobTop[i] + Vector2.down * (knobTravelY * Mathf.Clamp01(progress));
        if (knobCo[i] != null) StopCoroutine(knobCo[i]);
        knobCo[i] = StartCoroutine(CoSlide(knobs[i], target));
    }

    private IEnumerator CoSlide(RectTransform knob, Vector2 target)
    {
        Vector2 from = knob.anchoredPosition;
        float t = 0f;
        while (t < knobSlideDuration && knob != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / knobSlideDuration);
            float e = k * k * (3f - 2f * k);   // smoothstep
            knob.anchoredPosition = Vector2.LerpUnclamped(from, target, e);
            yield return null;
        }
        if (knob != null) knob.anchoredPosition = target;
    }
}
