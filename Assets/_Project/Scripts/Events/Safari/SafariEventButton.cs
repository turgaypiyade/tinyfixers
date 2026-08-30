using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana ekran Tiny Safari event ikonu (diğer event ikonları gibi). Görünürlüğü controller yönetir
/// (level kapısı + aktif gün). Tık → controller.OnIconClicked (katılmadıysa popup, katıldıysa harita).
///
/// İdle animasyon için aynı objeye <see cref="EventIconAnimator"/> eklenebilir.
/// </summary>
public sealed class SafariEventButton : MonoBehaviour
{
    [SerializeField] private SafariEventController controller;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [Tooltip("Görünürlük için açılıp kapanacak kök. Boşsa bu GameObject kullanılır.")]
    [SerializeField] private GameObject visibilityRoot;

    private string defaultLabel = "SAFARI";
    private int lastShownSeconds = int.MinValue;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClicked);
        if (visibilityRoot == null) visibilityRoot = gameObject;
        if (labelText != null && !string.IsNullOrEmpty(labelText.text))
            defaultLabel = labelText.text;
        RefreshLabel();
    }

    private void OnEnable()
    {
        SafariState.OnChanged += RefreshLabel;
        RefreshLabel();
    }

    private void OnDisable()
    {
        SafariState.OnChanged -= RefreshLabel;
    }

    private void Update()
    {
        // Etiket sürekli geri-sayım (event kalan süresi veya düşüş cooldown'ı) → her saniye tazele.
        int seconds = CurrentCountdownSeconds();
        if (seconds == lastShownSeconds) return;
        RefreshLabel();
    }

    private void OnClicked()
    {
        if (SafariState.FallCooldownRemaining(DateTime.UtcNow) > TimeSpan.Zero)
        {
            RefreshLabel();
            return;
        }

        if (controller != null) controller.OnIconClicked();
    }

    public void SetVisible(bool visible)
    {
        if (visibilityRoot != null && visibilityRoot.activeSelf != visible)
            visibilityRoot.SetActive(visible);
        if (visible) RefreshLabel();
    }

    private void RefreshLabel()
    {
        TimeSpan cooldown = SafariState.FallCooldownRemaining(DateTime.UtcNow);
        bool inCooldown = cooldown > TimeSpan.Zero;

        // Cooldown süresince buton pasif (tekrar oynanamaz); event sayarken aktif.
        if (button != null)
            button.interactable = !inCooldown;

        if (labelText != null)
        {
            if (inCooldown)
            {
                // Evente girip kaybedince: kalan bekleme (cooldown) süresi.
                labelText.text = FormatCountdown(cooldown);
            }
            else
            {
                // Normal: eventin bitişine kalan toplam süre. (Aktif pencere yoksa etikete geri dön.)
                TimeSpan eventRemaining = EventRemaining();
                labelText.text = eventRemaining > TimeSpan.Zero ? FormatCountdown(eventRemaining) : defaultLabel;
            }
        }

        lastShownSeconds = CurrentCountdownSeconds();
    }

    // Etikette gösterilecek geri-sayım saniyesi: düşüş cooldown'ı öncelikli, yoksa event penceresi kalanı.
    // Aktif geri-sayım yoksa int.MinValue (etiket defaultLabel'a döner, her frame tazelemeye gerek kalmaz).
    private int CurrentCountdownSeconds()
    {
        TimeSpan cooldown = SafariState.FallCooldownRemaining(DateTime.UtcNow);
        if (cooldown > TimeSpan.Zero)
            return Mathf.Max(0, Mathf.CeilToInt((float)cooldown.TotalSeconds));

        TimeSpan eventRemaining = EventRemaining();
        return eventRemaining > TimeSpan.Zero
            ? Mathf.Max(0, Mathf.CeilToInt((float)eventRemaining.TotalSeconds))
            : int.MinValue;
    }

    // Aktif event penceresinin bitişine kalan süre (SafariSchedule). Aktif pencere yoksa Zero.
    private TimeSpan EventRemaining()
    {
        var cfg = controller != null ? controller.Config : null;
        if (cfg == null) return TimeSpan.Zero;

        DateTime end = SafariSchedule.GetWindowEnd(cfg, DateTime.UtcNow);
        if (end == DateTime.MinValue) return TimeSpan.Zero;

        TimeSpan remaining = end - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    // >= 1 saat kalınca SS:DD:ss, aksi halde DD:ss.
    private static string FormatCountdown(TimeSpan remaining)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;
        return h > 0 ? $"{h:00}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }
}
