using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bölge açma listesindeki tek satır. Bir WorldMapRegion'ı temsil eder.
/// Slot Y'si panel tarafından dışarıdan set edilir; animasyonlar item içinde yaşar.
/// (RepairTaskItem'dan uyarlandı — atölye yerine bölge açma.)
/// </summary>
public sealed class RegionUnlockItem : MonoBehaviour
{
    [Header("Refs (ikon-sol, metin-orta, buton-sağ)")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Buton üzerindeki yıldız sayısı (starCost).")]
    [SerializeField] private TMP_Text starCountText;
    [Tooltip("Buton üzerindeki 'AÇ' metni — lokalize anahtarı worldmap_unlock_button.")]
    [SerializeField] private TMP_Text unlockLabelText;
    [SerializeField] private Button button;
    [SerializeField] private GameObject activeHighlight; // sadece üstteki (aktif) satırda
    [SerializeField] private GameObject lockedOverlay;   // diğerlerinde (preview)
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Bölüm Kilidi (opsiyonel — atanmazsa isim alanı kullanılır)")]
    [Tooltip("Yıldız maliyeti grubu (ikon + sayı). Kilitliyken gizlenir.")]
    [SerializeField] private GameObject costRoot;
    [Tooltip("'Bölüm X bitince açılır' satırı. Atanmazsa metin isim alanına yazılır.")]
    [SerializeField] private TMP_Text lockedInfoText;

    [Header("Localization")]
    [SerializeField] private string unlockButtonLocalizationKey = "worldmap_unlock_button";
    [Tooltip("Bölüm kilidi varken buton üzerindeki kısa metin.")]
    [SerializeField] private string lockedButtonLocalizationKey = "wonder_task_locked_button";

    private WorldMapRegion region;
    private RegionUnlockListPanel panel;
    private int wonderEventIndex = -1;

    public WorldMapRegion Region => region;

    /// <summary>Wonder modunda bu satırın ait olduğu event indeksi. -1 = bölge satırı.</summary>
    public int WonderEventIndex => wonderEventIndex;

    public void Bind(WorldMapRegion region, RegionUnlockListPanel panel, bool isActive)
    {
        this.region = region;
        this.panel  = panel;
        this.wonderEventIndex = -1;

        if (iconImage != null)
        {
            if (region.TaskIcon != null) { iconImage.sprite = region.TaskIcon; iconImage.enabled = true; }
            else iconImage.enabled = false;
        }

        if (nameText != null)
            nameText.text = region.DisplayName;   // harita etiketiyle aynı kaynak (lokalize)

        if (starCountText != null) starCountText.text = region.StarCost.ToString();

        if (unlockLabelText != null)
        {
            if (!string.IsNullOrEmpty(unlockButtonLocalizationKey))
            {
                string s = GameLocalization.Get(unlockButtonLocalizationKey);
                unlockLabelText.text = (s == unlockButtonLocalizationKey) ? string.Empty : s;
            }
            else unlockLabelText.text = string.Empty;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (lockedInfoText != null) lockedInfoText.gameObject.SetActive(false);
        RefreshActiveState(isActive);
    }

    private Material _iconRevealMat;

    /// <summary>
    /// Wonder görev satırı — region olmadan ham değerlerle bağlar (aynı görsel).
    /// wonderEventIndex: satırın ait olduğu event; tıklanınca o event'in görevi yapılır.
    /// </summary>
    public void BindTask(string displayName, Sprite icon, int starCost, RegionUnlockListPanel panel,
                         bool isActive, int wonderEventIndex = -1)
    {
        this.region = null;
        this.panel = panel;
        this.wonderEventIndex = wonderEventIndex;

        if (iconImage != null)
        {
            iconImage.material = null;   // reveal-önizleme materyalini sıfırla
            if (icon != null) { iconImage.sprite = icon; iconImage.enabled = true; }
            else iconImage.enabled = false;
        }
        SetTaskLabels(displayName, starCost);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        RefreshActiveState(isActive);
    }

    /// <summary>İkon = harika imajının reveal shader'ıyla 'reveal' kadar açılmış mini önizlemesi.</summary>
    public void BindTaskRevealIcon(string displayName, Sprite wonderSprite, float reveal,
                                   int starCost, RegionUnlockListPanel panel, bool isActive,
                                   int wonderEventIndex = -1)
    {
        this.region = null;
        this.panel = panel;
        this.wonderEventIndex = wonderEventIndex;

        if (iconImage != null)
        {
            if (wonderSprite != null)
            {
                iconImage.sprite = wonderSprite;
                iconImage.enabled = true;
                var shader = Shader.Find("UI/WonderReveal");
                if (shader != null)
                {
                    if (_iconRevealMat == null) _iconRevealMat = new Material(shader) { name = "TaskRevealIcon" };
                    _iconRevealMat.SetFloat("_Reveal", Mathf.Clamp01(reveal));
                    iconImage.material = _iconRevealMat;
                }
            }
            else iconImage.enabled = false;
        }
        SetTaskLabels(displayName, starCost);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        RefreshActiveState(isActive);
    }

    private void SetTaskLabels(string displayName, int starCost)
    {
        if (costRoot != null && !costRoot.activeSelf) costRoot.SetActive(true);
        if (lockedInfoText != null) lockedInfoText.gameObject.SetActive(false);
        if (nameText != null) nameText.text = displayName;
        if (starCountText != null) starCountText.text = starCost.ToString();
        if (unlockLabelText != null)
        {
            if (!string.IsNullOrEmpty(unlockButtonLocalizationKey))
            {
                string s = GameLocalization.Get(unlockButtonLocalizationKey);
                unlockLabelText.text = (s == unlockButtonLocalizationKey) ? string.Empty : s;
            }
            else unlockLabelText.text = string.Empty;
        }
    }

    /// <summary>
    /// Bölüm kapısı kapalı: satır kilitli çizilir — maliyet gizlenir, "Bölüm X bitince açılır"
    /// yazısı görünür, buton tıklanamaz. Bind*'tan SONRA çağrılır (ikon mantığı aynen korunur).
    /// </summary>
    public void ApplyChapterLock(string lockText)
    {
        if (costRoot != null) costRoot.SetActive(false);
        else if (starCountText != null) starCountText.text = string.Empty;

        if (unlockLabelText != null)
        {
            string s = GameLocalization.Get(lockedButtonLocalizationKey);
            unlockLabelText.text = (s == lockedButtonLocalizationKey) ? string.Empty : s;
        }

        if (lockedInfoText != null)
        {
            lockedInfoText.gameObject.SetActive(true);
            lockedInfoText.text = lockText;
        }
        else if (nameText != null)
        {
            // Kilitli satırda "ne zaman açılır" bilgisi görev adından daha değerli.
            nameText.text = lockText;
        }

        RefreshActiveState(false);
    }

    private void OnDestroy()
    {
        if (_iconRevealMat != null) Destroy(_iconRevealMat);
    }

    public void RefreshActiveState(bool isActive)
    {
        if (activeHighlight != null) activeHighlight.SetActive(isActive);
        if (lockedOverlay   != null) lockedOverlay.SetActive(!isActive);

        // Uzun adlar (ör. "Repair the Foundation Stones") iki satıra kaymasın: tek satır + otomatik küçülme.
        FitNameToRow();

        // Aktif (yapılabilir) görevin butonu hafifçe nabız atar.
        if (button != null)
        {
            if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
            button.transform.localScale = Vector3.one;
            if (isActive && isActiveAndEnabled)
                pulseRoutine = StartCoroutine(PulseButton(button.transform));
        }

        if (button != null)
        {
            button.interactable = isActive;
            button.onClick.RemoveAllListeners();
            if (isActive && panel != null)
            {
                if (wonderEventIndex >= 0)
                {
                    int e = wonderEventIndex;           // closure için kopya
                    var p = panel;
                    button.onClick.AddListener(() => p.OnWonderTaskClicked(e));
                }
                else button.onClick.AddListener(panel.OnActiveItemClicked);
            }
        }
    }

    private Coroutine pulseRoutine;

    // Ad tek satırda kalır ve butondan ÖNCE biter: yazı alanı ikonun sağından butonun soluna kadar daraltılır,
    // otomatik boyut ASIL puntoyu aşamaz (yalnız küçülür).
    private float baseNameFontSize = -1f;
    private void FitNameToRow()
    {
        if (nameText == null) return;
        if (baseNameFontSize < 0f) baseNameFontSize = nameText.fontSize;
        var rowRt = (RectTransform)transform;
        var nameRt = nameText.rectTransform;
        float rowW = rowRt.rect.width;
        if (rowW > 0f && button != null)
        {
            var btnRt = (RectTransform)button.transform;
            float btnLeft = rowRt.InverseTransformPoint(btnRt.TransformPoint(new Vector3(btnRt.rect.xMin, 0f, 0f))).x;
            float left = -rowW * 0.5f + rowW * 0.16f;          // ikonun sağı
            if (iconImage != null)
            {
                var iconRt = iconImage.rectTransform;
                left = rowRt.InverseTransformPoint(iconRt.TransformPoint(new Vector3(iconRt.rect.xMax, 0f, 0f))).x + 10f;
            }
            float right = btnLeft - 10f;
            if (right > left + 50f)
            {
                nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 0.5f);
                nameRt.pivot = new Vector2(0.5f, 0.5f);
                nameRt.sizeDelta = new Vector2(right - left, nameRt.sizeDelta.y);
                nameRt.anchoredPosition = new Vector2((left + right) * 0.5f, nameRt.anchoredPosition.y);
            }
        }
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Overflow;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMax = baseNameFontSize;
        nameText.fontSizeMin = baseNameFontSize * 0.6f;
        nameText.horizontalAlignment = HorizontalAlignmentOptions.Left;
    }

    private static IEnumerator PulseButton(Transform t)
    {
        while (t != null)
        {
            float s = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 4f);
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    public void SetInstantY(float y)
    {
        var rt = (RectTransform)transform;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }

    // ─── Animasyonlar ──────────────────────────────────────────────────────────

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
