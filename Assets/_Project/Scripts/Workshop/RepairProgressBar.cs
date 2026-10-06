using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Task list panelinin üstünde duran ilerleme barı.
/// Sol uçtan sağ uça doğru dolar, sağ uçta ödül sandığı vardır.
/// 10/10'a ulaşınca sandık açılma animasyonunu tetikler.
/// </summary>
public class RepairProgressBar : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorkshopController workshop;
    [Tooltip("Filled Image — Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Krem rengi arka plan sprite (opsiyonel — sadece görsel referans).")]
    [SerializeField] private Image backgroundImage;
    [Tooltip("\"3 / 10\" metni (opsiyonel).")]
    [SerializeField] private TMP_Text progressText;

    [Header("Chest")]
    [SerializeField] private Image chestImage;
    [Tooltip("Bar dolunca chest'in scale up animasyonu için süre.")]
    [SerializeField, Min(0.05f)] private float chestPopDuration = 0.4f;
    [SerializeField] private float chestPopScale = 1.35f;
    [Tooltip("Reward verilince chest yerine açılmış chest sprite'ı (opsiyonel).")]
    [SerializeField] private Sprite chestOpenedSprite;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float fillTweenDuration = 0.4f;

    [Header("Track (boş ray)")]
    [Tooltip("Dolgunun ARKASINDA her zaman görünen ray rengi. 0/N'de bile 'doldurulacak çubuk' okunur.")]
    [SerializeField] private Color trackColor = new Color(0.36f, 0.16f, 0.12f, 0.45f);

    private float currentDisplayedFill;
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
        if (workshop != null)
        {
            workshop.OnStageCompleted   += HandleStageCompleted;
            workshop.OnFinalRewardGranted += HandleFinalReward;
        }
        ApplyInstant();
    }

    private void OnDisable()
    {
        if (workshop != null)
        {
            workshop.OnStageCompleted   -= HandleStageCompleted;
            workshop.OnFinalRewardGranted -= HandleFinalReward;
        }
    }

    /// Anlık state'e göre tüm görseli yenile (panel açılınca veya sahne başlangıcı).
    public void ApplyInstant()
    {
        if (workshop == null || workshop.StageData == null) return;

        float target = workshop.ProgressNormalized;
        currentDisplayedFill = target;

        if (fillImage != null) fillImage.fillAmount = target;
        UpdateProgressText();
        UpdateChestSprite();
    }

    private void UpdateProgressText()
    {
        if (progressText == null) return;
        int cur = workshop.TasksCompleted;
        int tot = workshop.TotalTasks;
        progressText.text = $"{cur} / {tot}";
    }

    private void UpdateChestSprite()
    {
        if (chestImage == null) return;

        var bundle = workshop.StageData != null ? workshop.StageData.finalReward : null;
        if (bundle == null) return;

        // Bundle'da chestOpenedSprite varsa onu kullan; yoksa inspector'daki fallback.
        Sprite openedSprite = bundle.chestOpenedSprite != null ? bundle.chestOpenedSprite : chestOpenedSprite;

        if (workshop.FinalRewardClaimed && openedSprite != null)
            chestImage.sprite = openedSprite;
        else if (bundle.chestIcon != null)
            chestImage.sprite = bundle.chestIcon;
    }

    private void HandleStageCompleted(int completedIndex)
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(TweenFillTo(workshop.ProgressNormalized));
    }

    private void HandleFinalReward(WorkshopRewardBundle bundle)
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(PlayChestOpenAnimation());
    }

    private IEnumerator TweenFillTo(float target)
    {
        float start = currentDisplayedFill;
        float t = 0f;
        while (t < fillTweenDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fillTweenDuration));
            currentDisplayedFill = Mathf.Lerp(start, target, k);
            if (fillImage != null) fillImage.fillAmount = currentDisplayedFill;
            yield return null;
        }
        currentDisplayedFill = target;
        if (fillImage != null) fillImage.fillAmount = target;
        UpdateProgressText();
    }

    private IEnumerator PlayChestOpenAnimation()
    {
        if (chestImage == null) yield break;

        var rt = chestImage.rectTransform;
        Vector3 startScale = rt.localScale;
        Vector3 peakScale  = startScale * chestPopScale;

        float t = 0f;
        float half = chestPopDuration * 0.5f;

        // Pop up
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / half));
            rt.localScale = Vector3.Lerp(startScale, peakScale, k);
            yield return null;
        }

        // Pop back
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / half));
            rt.localScale = Vector3.Lerp(peakScale, startScale, k);
            yield return null;
        }

        rt.localScale = startScale;
        UpdateChestSprite();
    }
}
