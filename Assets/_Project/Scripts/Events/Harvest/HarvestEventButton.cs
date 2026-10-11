using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LeftEventPanel'deki Bostan Hasadı ikonu. 25. seviye kapısından sonra hep görünür; logonun
/// alt bandında açıkken sezon bitişine, ara günlerde açılışa kalan süre, köşe rozetinde kullanılabilir kürek sayısı (kürek varsa).
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
    private bool live;
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
        // İkon level kapısından sonra hep görünür: açıkken sezon bitişine, ara günlerde açılışa geri sayar.
        bool unlocked = HarvestState.IsUnlocked(cfg);
        if (visibilityRoot != null && visibilityRoot.activeSelf != unlocked)
        {
            visibilityRoot.SetActive(unlocked);
            force = true;
        }
        if (!unlocked) return;

        bool isLive = HarvestState.IsLive(cfg, now);
        if (isLive != live) { live = isLive; force = true; }
        // Kapalıyken de: pencere kapandıysa sezon sonu dönüşümü (kalan kürek → altın) burada olur.
        if (force) HarvestState.SyncCycle(cfg, now);
        if (force) AnnounceLeftoverGold();

        DateTime target = live ? HarvestState.WindowEnd(cfg, now) : HarvestSchedule.GetNextStart(cfg, now);
        TimeSpan remaining = target == DateTime.MinValue ? TimeSpan.Zero : target - now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        int second = (int)Math.Ceiling(remaining.TotalSeconds);
        if (force || second != lastShownSecond)
        {
            lastShownSecond = second;
            if (labelText != null) labelText.text = TimeFormat.Countdown(remaining);
        }
        if (force) RefreshBadge();
    }

    // Event bitince harcanmamış kürekler altına döndü (altın zaten verildi) → ana menüde bir kez söyle.
    private static void AnnounceLeftoverGold()
    {
        if (!HarvestState.TryTakeLeftoverAnnouncement(out int trowels, out int gold)) return;
        var cfg = HarvestConfig.Shared;
        var coinIcon = TileIconLibrary.Shared != null ? TileIconLibrary.Shared.GetRewardIcon(DailySlotRewardType.Coins) : null;
        string title = GameLocalization.GetOr("harvest_leftover_title", "Hasat Bitti!");
        string message = string.Format(GameLocalization.GetOr("harvest_leftover_msg", "Kalan {0} kürek altına dönüştü."), trowels);
        RuntimeChoicePopup.ShowRewards(title, message, new[]
        {
            new RuntimeChoicePopup.RewardItem(coinIcon, gold.ToString(), GameLocalization.GetOr("shop_reward_coins", "Altın")),
        }, GameLocalization.GetOr("common_ok", "Tamam"));
    }

    private void RefreshBadge()
    {
        int trowels = live ? HarvestState.Trowels : 0;   // ara günlerde eski sezonun küreği gösterilmez
        if (badge != null) badge.SetActive(trowels > 0);
        if (badgeText != null) badgeText.text = trowels.ToString();
    }

    private void OnClick()
    {
        if (!HarvestState.IsLive(HarvestConfig.Shared, DateTime.UtcNow)) return;   // ara gün: yalnız geri sayım
        HarvestState.SyncCycle(HarvestConfig.Shared, DateTime.UtcNow);
        if (screen != null) screen.Open(EventScreenIris.IconRect(this, visibilityRoot));
    }
}
