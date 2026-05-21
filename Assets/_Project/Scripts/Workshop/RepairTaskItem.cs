using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Task list paneli içindeki tek bir görev (task) UI öğesi.
/// Slot pozisyonu (Y) panel tarafından dışarıdan set edilir.
/// Animasyon coroutine'leri item'in kendi içinde yaşar.
/// </summary>
public class RepairTaskItem : MonoBehaviour
{
    [Header("Refs (Layout: ikon-sol, metin-orta, buton-sağ)")]
    [SerializeField] private Image iconImage;
    [Tooltip("Task adı (orta). Localization key varsa GameLocalization.Get(key) ile basılır, yoksa stageName fallback'i.")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Buton üzerindeki yıldız sayısı metni. Örn \"2\".")]
    [SerializeField] private TMP_Text starCountText;
    [Tooltip("Buton üzerindeki \"ONAR\" metni — lokalize anahtarı workshop_repair_button.")]
    [SerializeField] private TMP_Text repairLabelText;
    [SerializeField] private Button button;
    [SerializeField] private GameObject activeHighlight; // sadece üstteki (aktif) görevde görünür
    [SerializeField] private GameObject lockedOverlay;    // diğerlerinde görünür (preview)
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Localization")]
    [Tooltip("Buton üzerindeki ONAR/REPAIR metni için lokalizasyon anahtarı.")]
    [SerializeField] private string repairButtonLocalizationKey = "workshop_repair_button";

    private int stageIndex;
    private RepairTaskListPanel panel;

    public int StageIndex => stageIndex;

    public void Bind(int stageIndex, WorkshopStage stage, RepairTaskListPanel panel, bool isActive)
    {
        this.stageIndex = stageIndex;
        this.panel      = panel;

        if (iconImage != null && stage.taskIcon != null) iconImage.sprite = stage.taskIcon;

        if (nameText != null)
        {
            string localized = !string.IsNullOrEmpty(stage.nameLocalizationKey)
                ? GameLocalization.Get(stage.nameLocalizationKey)
                : null;
            // Eğer key yok veya key bulunamadıysa (Get key'i geri döndürürse), stageName fallback.
            if (string.IsNullOrEmpty(localized) || localized == stage.nameLocalizationKey)
                localized = stage.stageName;
            nameText.text = localized;
        }

        if (starCountText != null)
            starCountText.text = stage.starCost.ToString();

        if (repairLabelText != null && !string.IsNullOrEmpty(repairButtonLocalizationKey))
            repairLabelText.text = GameLocalization.Get(repairButtonLocalizationKey);

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        RefreshActiveState(isActive);
    }

    public void RefreshActiveState(bool isActive)
    {
        if (activeHighlight != null) activeHighlight.SetActive(isActive);
        if (lockedOverlay   != null) lockedOverlay.SetActive(!isActive);

        if (button != null)
        {
            button.interactable = isActive;
            button.onClick.RemoveAllListeners();
            if (isActive && panel != null)
                button.onClick.AddListener(panel.OnActiveTaskClicked);
        }
    }

    public void SetInstantY(float y)
    {
        var rt = (RectTransform)transform;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }

    // ─── Animasyonlar ────────────────────────────────────────────────────────

    /// Üstteki tamamlanan görev için: hafif büyüt + fade out.
    public IEnumerator PlayCompleteAnimation(float duration)
    {
        var rt = (RectTransform)transform;
        Vector3 startScale = rt.localScale;
        Vector3 endScale   = startScale * 1.15f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (canvasGroup != null) canvasGroup.alpha = 1f - k;
            rt.localScale = Vector3.Lerp(startScale, endScale, k);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    /// Belirtilen Y pozisyonuna yumuşak kayma (slide up).
    public IEnumerator SlideToY(float targetY, float duration)
    {
        var rt = (RectTransform)transform;
        float startY = rt.anchoredPosition.y;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, Mathf.Lerp(startY, targetY, k));
            yield return null;
        }
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, targetY);
    }

    /// Yeni eklenen görev için: aşağıdan kayarak gelir + fade in.
    public IEnumerator SlideInFromBelow(float targetY, float startOffsetY, float duration)
    {
        var rt = (RectTransform)transform;
        float startY = targetY - startOffsetY;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, startY);
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, Mathf.Lerp(startY, targetY, k));
            if (canvasGroup != null) canvasGroup.alpha = k;
            yield return null;
        }
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, targetY);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }
}
