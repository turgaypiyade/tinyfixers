using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bölge açma listesinin üstündeki ilerleme barı. Açılmış / toplam bölgeye göre dolar.
/// Bir bölge açılınca yumuşakça ilerler; hepsi açılınca (opsiyonel) sandık pop animasyonu.
/// (Workshop/RepairProgressBar'dan uyarlandı.)
/// </summary>
public sealed class RegionProgressBar : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapController worldMap;
    [Tooltip("Filled Image — Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left.")]
    [SerializeField] private Image fillImage;
    [Tooltip("\"3 / 10\" metni (opsiyonel).")]
    [SerializeField] private TMP_Text progressText;

    [Header("Chest (opsiyonel — hepsi açılınca)")]
    [SerializeField] private Image chestImage;
    [SerializeField] private Sprite chestOpenedSprite;
    [SerializeField, Min(0.05f)] private float chestPopDuration = 0.4f;
    [SerializeField] private float chestPopScale = 1.35f;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float fillTweenDuration = 0.4f;

    private float currentFill;

    private void OnEnable()
    {
        if (worldMap != null) worldMap.OnRegionUnlocked += HandleRegionUnlocked;
        ApplyInstant();
    }

    private void OnDisable()
    {
        if (worldMap != null) worldMap.OnRegionUnlocked -= HandleRegionUnlocked;
    }

    /// <summary>Anlık state'e göre yenile (panel açılınca / sahne başında).</summary>
    public void ApplyInstant()
    {
        if (worldMap == null) return;
        currentFill = worldMap.ProgressNormalized;
        if (fillImage != null) fillImage.fillAmount = currentFill;
        UpdateText();
        if (worldMap.AllUnlocked && chestImage != null && chestOpenedSprite != null)
            chestImage.sprite = chestOpenedSprite;
    }

    private void UpdateText()
    {
        if (progressText == null) return;
        progressText.text = $"{worldMap.UnlockedCount} / {worldMap.TotalRegions}";
    }

    private void HandleRegionUnlocked(WorldMapRegion _)
    {
        if (!gameObject.activeInHierarchy) { ApplyInstant(); return; }
        StartCoroutine(TweenFillTo(worldMap.ProgressNormalized));
    }

    private IEnumerator TweenFillTo(float target)
    {
        float start = currentFill;
        float t = 0f;
        while (t < fillTweenDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fillTweenDuration));
            currentFill = Mathf.Lerp(start, target, k);
            if (fillImage != null) fillImage.fillAmount = currentFill;
            yield return null;
        }
        currentFill = target;
        if (fillImage != null) fillImage.fillAmount = target;
        UpdateText();

        if (worldMap.AllUnlocked && chestImage != null)
            yield return PlayChestPop();
    }

    private IEnumerator PlayChestPop()
    {
        var rt = chestImage.rectTransform;
        Vector3 startScale = rt.localScale;
        Vector3 peak = startScale * chestPopScale;
        float half = chestPopDuration * 0.5f, t = 0f;
        while (t < half) { t += Time.unscaledDeltaTime; rt.localScale = Vector3.Lerp(startScale, peak, Mathf.SmoothStep(0f,1f,t/half)); yield return null; }
        t = 0f;
        while (t < half) { t += Time.unscaledDeltaTime; rt.localScale = Vector3.Lerp(peak, startScale, Mathf.SmoothStep(0f,1f,t/half)); yield return null; }
        rt.localScale = startScale;
        if (chestOpenedSprite != null) chestImage.sprite = chestOpenedSprite;
    }
}
