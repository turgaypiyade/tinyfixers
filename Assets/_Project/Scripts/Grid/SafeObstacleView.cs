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

        if (breakParticlePrefab != null)
            Instantiate(breakParticlePrefab, transform.position, Quaternion.identity, transform.parent);

        if (lockPanel != null)    lockPanel.SetActive(false);
        if (brokenVisual != null) brokenVisual.SetActive(true);
        SetRaycastTargets(false);
        StartCoroutine(CoDestroyAfterBrokenVisual());
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

    private IEnumerator CoDestroyAfterBrokenVisual()
    {
        if (brokenVisualDuration > 0f)
            yield return new WaitForSeconds(brokenVisualDuration);

        Destroy(gameObject);
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
