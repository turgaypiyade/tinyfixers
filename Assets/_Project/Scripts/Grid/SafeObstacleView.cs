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
    [Tooltip("Kasa çökerken merkezden saçılan 'dökülme' partikülü (opsiyonel). Boşsa breakParticlePrefab kullanılır.")]
    [SerializeField] private GameObject dissolveParticlePrefab;
    [SerializeField, Min(0f)] private float brokenVisualDuration = 0.45f;

    [Header("Break Animasyonu")]
    [Tooltip("Patlama anında gövde titreme süresi/şiddeti (px).")]
    [SerializeField, Min(0f)] private float breakShakeDuration = 0.20f;
    [SerializeField, Min(0f)] private float breakShakeStrength = 11f;
    [Tooltip("Patlamada gövdenin dev genişleme (overshoot) ölçeği. Kod tabanı en az 1.5 garantiler.")]
    [SerializeField, Range(1f, 1.8f)] private float breakBodyPunch = 1.5f;
    [Tooltip("Knob'ların patlama (scale-up + sönme) süresi.")]
    [SerializeField, Min(0f)] private float knobBurstDuration = 0.16f;
    [Tooltip("brokenVisual'ın pop-in süresi ve overshoot'u.")]
    [SerializeField, Min(0f)] private float revealPopDuration = 0.28f;
    [SerializeField, Range(1f, 1.6f)] private float revealPopOvershoot = 1.18f;

    [Header("Break Juice (basınç + dönme)")]
    [Tooltip("Basınç zirvesinde gövdenin gerilme ölçeği. Kod tabanı en az 1.3 garantiler.")]
    [SerializeField, Range(1f, 1.6f)] private float breakSwellScale = 1.3f;
    [Tooltip("Patlama sarsıntısında gövdenin dönme titremesi (derece). 0 = kapalı.")]
    [SerializeField, Min(0f)] private float breakShakeRotation = 8f;

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

    // BUHAR KAZANI PATLAMASI: basınç birikir (kazan nabız gibi şişip titrer, ısınıp kızarır) →
    // zirvede gerilim (donma anı) → ŞİDDETLİ PATLAMA (flash + dev genişleme + sert dönmeli sarsıntı +
    // knob/perçin fırlar + particle); kasa aynı anda açılır → kısa nefes → küçülüp sönerek çıkar.
    // Tüm adımlar null-güvenli. Ölçek/şiddet code-side garantili (prefab serialize değerinden bağımsız).
    private IEnumerator CoPlaySafeBreak()
    {
        var body = bodyRect != null ? bodyRect : transform as RectTransform;
        Vector3 baseScale = body != null ? body.localScale : Vector3.one;
        Vector2 basePos   = body != null ? body.anchoredPosition : Vector2.zero;
        Quaternion baseRot = body != null ? body.localRotation : Quaternion.identity;
        var bodyGraphic = body != null ? body.GetComponent<Graphic>() : null;
        Color bodyBaseColor = bodyGraphic != null ? bodyGraphic.color : Color.white;

        float shakeDur   = Mathf.Max(breakShakeDuration, 0.42f);
        float revealDur  = Mathf.Max(revealPopDuration, 0.34f);
        float breatheDur = Mathf.Max(brokenVisualDuration, 0.55f);

        // 1) BASINÇ BİRİKİMİ — kazan gerilir: nabız halinde şişip titrer, giderek kızarır.
        if (body != null)
            yield return CoPressureBuildUp(body, basePos, baseScale, bodyGraphic, bodyBaseColor);

        // 2) PATLAMA — particle + perçin (knob) fırlaması + şiddetli dönmeli sarsıntı; kasa aynı anda açılır.
        if (breakParticlePrefab != null)
            Instantiate(breakParticlePrefab, SafeCenterWorld(), Quaternion.identity, transform.parent);

        BurstKnobs();
        if (lockPanel != null) lockPanel.SetActive(false);

        if (body != null)
            StartCoroutine(CoShake(body, basePos, baseRot, breakShakeStrength * 1.5f, breakShakeRotation * 1.4f, shakeDur));

        // Reveal, patlama ile ÖRTÜŞSÜN — arada bekleme/kopukluk olmasın.
        if (revealGlow != null)
            StartCoroutine(CoRevealGlow());

        if (brokenVisual != null)
        {
            brokenVisual.SetActive(true);
            if (brokenVisual.transform is RectTransform brt)
                StartCoroutine(CoPopIn(brt, revealDur, revealPopOvershoot));
        }

        if (body != null)
        {
            // Flash: gövde bir an beyaza/sıcağa patlar → patlama vurgusu.
            if (bodyGraphic != null)
                StartCoroutine(CoFlash(bodyGraphic, bodyBaseColor));

            // Ani DEV genişleme (kazan parçalanıyor) → sonra hızla çöker. Ölçek code-side garantili.
            float popScale = Mathf.Max(breakBodyPunch, breakSwellScale + 0.1f, 1.5f);
            yield return CoScaleTo(body, body.localScale, baseScale * popScale, 0.045f); // patla
            yield return CoScaleTo(body, body.localScale, baseScale, 0.17f);             // çök
            body.localScale = baseScale; body.anchoredPosition = basePos; body.localRotation = baseRot;
            if (bodyGraphic != null) bodyGraphic.color = bodyBaseColor;
        }

        // Kırık kasa bir nefes alsın, sonra ANİDEN yok olmasın: kısa küçül + sön ile sahneden çıksın.
        yield return new WaitForSeconds(breatheDur);
        yield return CoFadeShrinkOut(0.24f);

        Destroy(gameObject);
    }

    // Kazan basıncı birikiyor: 2 "nabız" halinde şişip hafif geri çekilir; her nabızda titreme +
    // ısınma (kırmızıya kayma) artar. Sonra zirvede kısa gerilim (patlamadan hemen önceki donma).
    private IEnumerator CoPressureBuildUp(RectTransform body, Vector2 basePos, Vector3 baseScale,
        Graphic g, Color baseColor)
    {
        const int pulses = 2;
        Color hotColor = new Color(1f, 0.5f, 0.32f, baseColor.a);   // ısınan metal

        for (int p = 0; p < pulses; p++)
        {
            float pn = (p + 1f) / pulses;                            // 0..1 basınç birikimi
            Vector3 peak   = baseScale * (1f + 0.14f * pn);          // giderek büyür
            Vector3 valley = baseScale * (1f + 0.05f * pn);
            float tremor   = 3.5f + 9f * pn;                          // titreme şiddeti artar

            if (g != null) g.color = Color.Lerp(baseColor, hotColor, 0.45f * pn);

            yield return CoStrainScale(body, body.localScale, peak,   0.11f, basePos, tremor);
            yield return CoStrainScale(body, body.localScale, valley, 0.06f, basePos, tremor * 0.6f);
        }

        // Zirve: max gerilim + en şiddetli titreme, sonra kısa donma (patlama öncesi sessizlik).
        float peakScale = Mathf.Max(breakSwellScale, 1.3f);
        if (g != null) g.color = Color.Lerp(baseColor, hotColor, 0.6f);
        yield return CoStrainScale(body, body.localScale, baseScale * peakScale, 0.08f, basePos, 14f);
        yield return new WaitForSeconds(0.035f);
    }

    // Ölçek lerp'i + pozisyon titremesi (gerilim hissi). Bittiğinde pozisyonu base'e oturtur.
    private IEnumerator CoStrainScale(RectTransform rt, Vector3 from, Vector3 to, float dur,
        Vector2 basePos, float tremor)
    {
        float d = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < d && rt != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            float e = k * k * (3f - 2f * k);   // smoothstep
            rt.localScale = Vector3.LerpUnclamped(from, to, e);
            Vector2 off = new Vector2(
                (Mathf.PerlinNoise(Time.time * 70f, 1.3f) - 0.5f),
                (Mathf.PerlinNoise(2.7f, Time.time * 70f) - 0.5f)) * (tremor * 2f);
            rt.anchoredPosition = basePos + off;
            yield return null;
        }
        if (rt != null) { rt.localScale = to; rt.anchoredPosition = basePos; }
    }

    // Patlama flash'i: gövde bir an sıcak-beyaza parlar (hızlı yükselir, yavaş söner).
    private IEnumerator CoFlash(Graphic g, Color baseColor)
    {
        Color hot = new Color(1f, 0.97f, 0.85f, baseColor.a);
        float d = 0.16f;
        float t = 0f;
        while (t < d && g != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            float a = k < 0.3f ? Mathf.Lerp(0f, 1f, k / 0.3f) : Mathf.Lerp(1f, 0f, (k - 0.3f) / 0.7f);
            g.color = Color.Lerp(baseColor, hot, a);
            yield return null;
        }
        if (g != null) g.color = baseColor;
    }

    // Kasanın görsel merkezinin dünya pozisyonu (pivot köşede olsa da). Partikülleri buraya doğur.
    private Vector3 SafeCenterWorld()
    {
        var r = bodyRect != null ? bodyRect : transform as RectTransform;
        if (r == null) return transform.position;
        Vector2 size = r.rect.size;
        Vector3 localCenter = new Vector3((0.5f - r.pivot.x) * size.x, (0.5f - r.pivot.y) * size.y, 0f);
        return r.TransformPoint(localCenter);
    }

    // Sahneden çıkış: MERKEZE doğru büzülür + alpha söner + merkezden partikül dökülür.
    private IEnumerator CoFadeShrinkOut(float dur)
    {
        var root = transform as RectTransform;
        if (root == null) yield break;

        // Merkeze büzülsün: ölçek pivot etrafında küçülür; pivot köşedeyse (UI'da tipik sol-üst)
        // oraya kaçardı. Pivot'u merkeze al + pozisyonu telafi et (görsel yerinde kalır).
        Vector2 size = root.rect.size;
        Vector2 dPivot = new Vector2(0.5f, 0.5f) - root.pivot;
        root.anchoredPosition += new Vector2(dPivot.x * size.x * root.localScale.x,
                                             dPivot.y * size.y * root.localScale.y);
        root.pivot = new Vector2(0.5f, 0.5f);

        // Dökülme partikülü: kasa çökerken merkezden bir kez saç (ayrı prefab yoksa break partikülü).
        var dissolveFx = dissolveParticlePrefab != null ? dissolveParticlePrefab : breakParticlePrefab;
        if (dissolveFx != null)
            Instantiate(dissolveFx, SafeCenterWorld(), Quaternion.identity, transform.parent);

        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        Vector3 s0 = root.localScale;
        float d = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            float e = k * k * (3f - 2f * k);   // smoothstep
            root.localScale = Vector3.LerpUnclamped(s0, s0 * 0.25f, e);   // merkeze belirgin çöküş
            cg.alpha = 1f - e;
            yield return null;
        }
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

    // Dönmeli sarsıntı: pozisyon + z-rotasyon titremesi, erken sert / geç yumuşak sönüm.
    private IEnumerator CoShake(RectTransform rt, Vector2 basePos, Quaternion baseRot,
        float strength, float rotStrength, float dur)
    {
        float d = Mathf.Max(0.01f, dur);
        float t = 0f;
        while (t < d && rt != null)
        {
            t += Time.deltaTime;
            float damp = 1f - Mathf.Clamp01(t / d);
            float e = damp * damp;                            // başta şiddetli, sonra hızla sönsün
            Vector2 off = new Vector2(
                (Mathf.PerlinNoise(Time.time * 55f, 0f) - 0.5f),
                (Mathf.PerlinNoise(0f, Time.time * 55f) - 0.5f)) * (strength * 2.4f * e);
            rt.anchoredPosition = basePos + off;
            float rot = (Mathf.PerlinNoise(Time.time * 48f, 7.3f) - 0.5f) * 2f * rotStrength * e;
            rt.localRotation = baseRot * Quaternion.Euler(0f, 0f, rot);
            yield return null;
        }
        if (rt != null) { rt.anchoredPosition = basePos; rt.localRotation = baseRot; }
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
