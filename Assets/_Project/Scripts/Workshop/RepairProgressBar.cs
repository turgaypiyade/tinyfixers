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

    private float currentDisplayedFill;

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
        int cur = workshop.CurrentStage;
        int tot = workshop.TotalStages;
        progressText.text = $"{cur} / {tot}";
    }

    private void UpdateChestSprite()
    {
        if (chestImage == null) return;

        var reward = workshop.StageData != null ? workshop.StageData.finalReward : null;
        if (reward == null) return;

        if (workshop.FinalRewardClaimed && chestOpenedSprite != null)
            chestImage.sprite = chestOpenedSprite;
        else if (reward.chestIcon != null)
            chestImage.sprite = reward.chestIcon;
    }

    private void HandleStageCompleted(int completedIndex)
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(TweenFillTo(workshop.ProgressNormalized));
    }

    private void HandleFinalReward(WorkshopReward reward)
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
