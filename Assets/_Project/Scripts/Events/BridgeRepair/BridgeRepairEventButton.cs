using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LeftEventPanel'deki Bridge Repair ikonu. Görünürlüğü controller yönetir. Etiket HEP süre: event bitişine kalan,
/// yarış bitince ise yeni giriş beklemesi (20 dk; ikon pasif — Safari düşüş beklemesi gibi).
/// Yarış sürerken rozet köprü ilerlemesini (7/15) gösterir.
/// </summary>
public sealed class BridgeRepairEventButton : MonoBehaviour
{
    [SerializeField] private BridgeRepairController controller;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private GameObject progressBadge;
    [Tooltip("Görünürlük için açılıp kapanacak kök. Boşsa bu GameObject.")]
    [SerializeField] private GameObject visibilityRoot;

    private int lastShownSecond = int.MinValue;

    /// Yarış ekranı bu noktadan daireyle açılır / buraya kapanır.
    public RectTransform IconRect => EventScreenIris.IconRect(this, visibilityRoot);

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(() => controller?.OnIconClicked());
        if (visibilityRoot == null) visibilityRoot = gameObject;
    }

    private void OnEnable()
    {
        BridgeRepairState.OnChanged += RefreshProgress;
        RefreshProgress();
        RefreshLabel(force: true);
    }

    private void OnDisable() => BridgeRepairState.OnChanged -= RefreshProgress;

    private void Update() => RefreshLabel(force: false);

    public void SetVisible(bool visible)
    {
        if (visibilityRoot != null && visibilityRoot.activeSelf != visible)
            visibilityRoot.SetActive(visible);
        if (visible) { RefreshProgress(); RefreshLabel(force: true); }
    }

    // Etiket HER ZAMAN süre: yeni giriş beklemesi sürüyorsa o (ikon pasif), yoksa event bitişine kalan.
    private void RefreshLabel(bool force)
    {
        if (controller == null) return;
        DateTime now = DateTime.UtcNow;
        TimeSpan cooldown = controller.RejoinCooldown;
        bool inCooldown = cooldown > TimeSpan.Zero;
        if (button != null) button.interactable = !inCooldown;

        TimeSpan remaining = inCooldown ? cooldown : Remaining(controller.WindowEnd, now);
        int second = (int)Math.Ceiling(remaining.TotalSeconds);
        if (!force && second == lastShownSecond) return;
        lastShownSecond = second;
        if (labelText != null) labelText.text = TimeFormat.Countdown(remaining);
        if (inCooldown != shownCooldown) { shownCooldown = inCooldown; RefreshProgress(); }
    }

    private bool shownCooldown;

    private static TimeSpan Remaining(DateTime end, DateTime now)
    {
        if (end == DateTime.MinValue) return TimeSpan.Zero;
        var r = end - now;
        return r > TimeSpan.Zero ? r : TimeSpan.Zero;
    }

    private void RefreshProgress()
    {
        var cfg = controller != null ? controller.Config : BridgeRepairConfig.Shared;
        // Rozet yalnız yarış sürerken (bekleme sırasında gizli).
        bool joined = BridgeRepairState.HasJoined && cfg != null && !shownCooldown;
        if (progressBadge != null) progressBadge.SetActive(joined);
        if (progressText != null && joined)
            progressText.text = $"{Mathf.Min(BridgeRepairState.Wins, cfg.levelsToFinish)}/{cfg.levelsToFinish}";
    }
}
