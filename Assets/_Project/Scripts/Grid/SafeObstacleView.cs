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
    private Coroutine[] knobCo;

    /// GridSpawner çağırır: service'e bağla, origin'i ata, knob başlangıçlarını yakala.
    public void Setup(SafeObstacleService svc, int safeOrigin)
    {
        service = svc;
        origin  = safeOrigin;

        knobTop = new Vector2[knobs.Length];
        knobCo  = new Coroutine[knobs.Length];
        for (int i = 0; i < knobs.Length; i++)
            if (knobs[i] != null) knobTop[i] = knobs[i].anchoredPosition;

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
    }

    private void HandleLockClosed(int o, int lockIdx)
    {
        if (o != origin) return;
        SlideKnob(lockIdx, 1f);   // en altta sabit
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
