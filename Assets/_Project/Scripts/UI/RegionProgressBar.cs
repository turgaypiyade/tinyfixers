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
    [Tooltip("Atanırsa wonder modu: aktif harikanın görev ilerlemesini gösterir.")]
    [SerializeField] private WonderCatalog wonderCatalog;
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

    [Header("Track (boş ray)")]
    [Tooltip("Dolgunun ARKASINDA her zaman görünen ray rengi. 0/N'de bile 'doldurulacak çubuk' okunur.")]
    [SerializeField] private Color trackColor = new Color(0.36f, 0.16f, 0.12f, 0.45f);

    private float currentFill;
    private Image trackImage;

    [Tooltip("Sandığı yukarı kaydırma (px). Kapalı sandık görselinin üstünde kapak payı var → görsel kutunun altına " +
             "oturur; bu kadar yukarı alınır ki çubuğun ortasına hizalı dursun. Sahneden bağımsız (kodla uygulanır).")]
    [SerializeField] private float chestRaise = 26f;
    private bool chestRaised;

    private void Awake()
    {
        if (chestImage != null && !chestRaised)
        {
            chestImage.rectTransform.anchoredPosition += new Vector2(0f, chestRaise);
            chestRaised = true;
        }
        FitFillBeforeChest();
        EnsureTrack();
    }

    [Tooltip("Çubuğun sandığa göre bitiş payı (px). Negatif = çubuk sandığın görünen sol kenarının biraz altına girer (sandık çubuğun ucuna takılı görünür); pozitif = arada boşluk.")]
    [SerializeField] private float chestGap = -10f;

    // Dolgu çubuğunun sağ kenarını sandığın sol kenarına çeker (sandık konumu sahnede değişse de uyum sağlar).
    // Sol kenar ve yükseklik sahnedeki gibi kalır. Sayaç yazısı yeni çubuğun ortasına alınır.
    private void FitFillBeforeChest()
    {
        if (fillImage == null || chestImage == null) return;
        var fill = fillImage.rectTransform;
        var parent = fill.parent as RectTransform;
        if (parent == null) return;
        Canvas.ForceUpdateCanvases();

        var fc = new Vector3[4]; fill.GetWorldCorners(fc);
        var cc = new Vector3[4]; chestImage.rectTransform.GetWorldCorners(cc);
        float left = parent.InverseTransformPoint(fc[0]).x;
        float right = parent.InverseTransformPoint(fc[2]).x;
        float chestLeft = parent.InverseTransformPoint(cc[0]).x + ChestVisibleInset();
        float newRight = Mathf.Min(right, chestLeft - chestGap);
        if (newRight <= left + 40f || Mathf.Approximately(newRight, right)) return;

        float y = parent.InverseTransformPoint((fc[0] + fc[2]) * 0.5f).y;
        float h = fill.rect.height;
        fill.anchorMin = fill.anchorMax = new Vector2(0.5f, 0.5f);
        fill.pivot = new Vector2(0.5f, 0.5f);
        fill.sizeDelta = new Vector2(newRight - left, h);
        fill.anchoredPosition = new Vector2((left + newRight) * 0.5f - parent.rect.center.x, y - parent.rect.center.y);

        if (progressText != null)
        {
            var t = progressText.rectTransform;
            var tc = new Vector3[4]; t.GetWorldCorners(tc);
            float ty = parent.InverseTransformPoint((tc[0] + tc[2]) * 0.5f).y;
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
            t.pivot = new Vector2(0.5f, 0.5f);
            t.SetParent(parent, true);
            t.anchoredPosition = new Vector2(fill.anchoredPosition.x, ty - parent.rect.center.y);
        }
    }

    // preserveAspect'li sandık kutusunda görselin sol kenarı kutunun solundan içeride olabilir.
    private float ChestVisibleInset()
    {
        if (chestImage == null || chestImage.sprite == null || !chestImage.preserveAspect) return 0f;
        var r = chestImage.rectTransform.rect;
        var sr = chestImage.sprite.rect;
        if (sr.height <= 0f || r.height <= 0f) return 0f;
        float drawnW = Mathf.Min(r.width, r.height * sr.width / sr.height);
        return (r.width - drawnW) * 0.5f;
    }

    // Ray: dolgu görüntüsünün kopyası (aynı rect + sprite), tam dolu, koyu yarı saydam, dolgunun hemen arkasında.
    private void EnsureTrack()
    {
        if (trackImage != null || fillImage == null) return;
        var src = fillImage.rectTransform;
        var go = new GameObject("FillTrack", typeof(RectTransform), typeof(Image));
        go.layer = fillImage.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(src.parent, false);
        rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax; rt.pivot = src.pivot;
        rt.sizeDelta = src.sizeDelta; rt.anchoredPosition = src.anchoredPosition;
        rt.localScale = src.localScale;
        rt.SetSiblingIndex(src.GetSiblingIndex());
        trackImage = go.GetComponent<Image>();
        trackImage.sprite = fillImage.sprite;
        trackImage.type = fillImage.type == Image.Type.Filled ? Image.Type.Simple : fillImage.type;
        trackImage.preserveAspect = fillImage.preserveAspect;
        trackImage.color = trackColor;
        trackImage.raycastTarget = false;
    }

    private void OnEnable()
    {
        if (worldMap != null) worldMap.OnRegionUnlocked += HandleRegionUnlocked;
        ApplyInstant();
    }

    private void OnDisable()
    {
        if (worldMap != null) worldMap.OnRegionUnlocked -= HandleRegionUnlocked;
    }

    private bool WonderMode => wonderCatalog != null;

    /// <summary>Anlık state'e göre yenile (panel açılınca / sahne başında).</summary>
    public void ApplyInstant()
    {
        if (WonderMode)
        {
            int active = WonderProgress.ActiveEventIndex(wonderCatalog);
            var w = wonderCatalog.Get(active);
            int count = w != null ? w.TaskCount : 0;
            currentFill = count > 0 ? WonderProgress.RevealNormalized(wonderCatalog, active) : 0f;
            if (fillImage != null) fillImage.fillAmount = currentFill;
            UpdateText();
            return;
        }

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
        if (WonderMode)
        {
            int active = WonderProgress.ActiveEventIndex(wonderCatalog);
            var w = wonderCatalog.Get(active);
            int count = w != null ? w.TaskCount : 0;
            int stage = w != null ? Mathf.Min(WonderProgress.StageOf(active), count) : 0;
            progressText.text = $"{stage} / {count}";
            return;
        }
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
