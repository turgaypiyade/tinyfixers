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

    [Header("Localization")]
    [SerializeField] private string unlockButtonLocalizationKey = "worldmap_unlock_button";

    private WorldMapRegion region;
    private RegionUnlockListPanel panel;

    public WorldMapRegion Region => region;

    public void Bind(WorldMapRegion region, RegionUnlockListPanel panel, bool isActive)
    {
        this.region = region;
        this.panel  = panel;

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
        RefreshActiveState(isActive);
    }

    private Material _iconRevealMat;

    /// <summary>Wonder görev satırı — region olmadan ham değerlerle bağlar (aynı görsel).</summary>
    public void BindTask(string displayName, Sprite icon, int starCost, RegionUnlockListPanel panel, bool isActive)
    {
        this.region = null;
        this.panel = panel;

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
                                   int starCost, RegionUnlockListPanel panel, bool isActive)
    {
        this.region = null;
        this.panel = panel;

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

    private void OnDestroy()
    {
        if (_iconRevealMat != null) Destroy(_iconRevealMat);
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
                button.onClick.AddListener(panel.OnActiveItemClicked);
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
