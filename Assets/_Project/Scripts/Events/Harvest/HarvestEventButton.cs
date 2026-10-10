using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LeftEventPanel'deki Bostan Hasadı ikonu. Event açıkken görünür (takvim + 25. seviye kapısı); logonun
/// alt bandında sezon bitişine kalan süre, köşe rozetinde kullanılabilir kürek sayısı (kürek varsa).
/// </summary>
public sealed class HarvestEventButton : MonoBehaviour
{
    [SerializeField] private HarvestScreen screen;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text badgeText;
    [SerializeField] private GameObject badge;
    [Tooltip("Görünürlük için açılıp kapanacak kök (ikonun görseli). Bu bileşen kapanmayan bir üst objede durur.")]
    [SerializeField] private GameObject visibilityRoot;

    private int lastShownSecond = int.MinValue;
    private float nextCheck;

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
        HarvestState.OnChanged += RefreshBadge;
        nextCheck = 0f;
        Refresh(force: true);
    }

    private void OnDisable() => HarvestState.OnChanged -= RefreshBadge;

    private void Update()
    {
        // Görünürlük + geri sayım saniyede bir (her karede tarih hesabı yapmaya gerek yok).
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        Refresh(force: false);
    }

    private void Refresh(bool force)
    {
        var cfg = HarvestConfig.Shared;
        DateTime now = DateTime.UtcNow;
        bool live = HarvestState.IsLive(cfg, now);
        if (visibilityRoot != null && visibilityRoot.activeSelf != live)
        {
            visibilityRoot.SetActive(live);
            if (live) { HarvestState.SyncCycle(cfg, now); force = true; }
        }
        if (!live) return;

        TimeSpan remaining = HarvestState.WindowEnd(cfg, now) - now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        int second = (int)Math.Ceiling(remaining.TotalSeconds);
        if (force || second != lastShownSecond)
        {
            lastShownSecond = second;
            if (labelText != null) labelText.text = TimeFormat.Countdown(remaining);
        }
        if (force) RefreshBadge();
    }

    private void RefreshBadge()
    {
        int trowels = HarvestState.Trowels;
        if (badge != null) badge.SetActive(trowels > 0);
        if (badgeText != null) badgeText.text = trowels.ToString();
    }

    private void OnClick()
    {
        HarvestState.SyncCycle(HarvestConfig.Shared, DateTime.UtcNow);
        if (screen != null) screen.Open();
    }
}
