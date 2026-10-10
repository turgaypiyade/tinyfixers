using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared purchase receipt UI. Only the post-grant event may show success.</summary>
public static class ShopPurchaseFeedback
{
    /// Açık: siyah overlay üzerinde sırayla pop eden kutlama (ShopPurchaseCelebration).
    /// Kapalı: eski makbuz popup'ı (RuntimeChoicePopup.ShowRewards).
    public static bool UseCelebration = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        // Also safe when entering Play Mode with domain reload disabled.
        ShopPurchaseService.OnReceipt -= ShowSuccess;
        ShopPurchaseService.OnReceipt += ShowSuccess;
    }

    private static void ShowSuccess(ShopOffer offer, bool purchaseThanks)
    {
        if (offer == null || RuntimeSimulationSession.IsActive) return;
        var items = new List<RuntimeChoicePopup.RewardItem>();
        var targets = new List<CollectTarget>();   // items ile aynı sıra: öğe nereye uçacak
        int coinGain = 0;
        if (offer.groups != null)
        {
            foreach (var group in offer.groups)
            {
                if (group?.grants == null) continue;
                for (int i = 0; i < group.grants.Count; i++)
                {
                    var reward = group.grants[i];
                    if (reward == null) continue;
                    bool timed = reward.kind == ShopReward.Kind.Timed || reward.kind == ShopReward.Kind.InfiniteLifeTimed;
                    string amount = timed
                        ? GameLocalization.GetFormat("shop_purchase_duration", Mathf.Max(1, reward.durationHours))
                        : "+" + Mathf.Max(1, reward.amount).ToString("N0");
                    // Catalog icons correspond by index only when both lists match.
                    // Otherwise resolve by reward type rather than show a misleading icon.
                    Sprite icon = group.icons != null && group.icons.Count == group.grants.Count
                        ? group.icons[i] : null;
                    var library = TileIconLibrary.Shared;
                    if (icon == null && library != null)
                    {
                        if (reward.kind == ShopReward.Kind.Booster) icon = library.GetBoosterIcon(reward.booster);
                        else if (timed) icon = library.GetRewardIcon(
                            reward.kind == ShopReward.Kind.InfiniteLifeTimed
                                ? DailySlotRewardType.Lives : reward.timedType);
                    }
                    items.Add(new RuntimeChoicePopup.RewardItem(icon, amount,
                        GameLocalization.Get(NameKey(reward))));
                    targets.Add(TargetOf(reward));
                    if (reward.kind == ShopReward.Kind.Coins) coinGain += Mathf.Max(1, reward.amount);
                }
            }
        }

        if (purchaseThanks)
        {
            // A distinct receipt item makes the gesture visible, including overflow at the life cap.
            string giftName = GameLocalization.Get(LivesManager.PendingPurchaseThanks > 0
                ? "shop_purchase_thanks_pending" : "shop_purchase_thanks_life");
            items.Add(new RuntimeChoicePopup.RewardItem(null, "+1", giftName));
            targets.Add(CollectTarget.Lives);
        }

        if (UseCelebration)
        {
            // Yalnız ana menü marketi: devamda ana ekrana dön, öğeler hedeflerine uçsun. Oyun içinde
            // (alt menü yok) akış DEĞİŞMEZ — kutlama kapanır, oyun kaldığı yerden sürer.
            var collect = MarketNavigator.IsMenuMarketOpen() ? BuildMenuCollect(targets, coinGain) : null;
            ShopPurchaseCelebration.Show(items, null, collect);
            return;
        }

        RuntimeChoicePopup.ShowRewards(GameLocalization.Get("shop_purchase_success_title"),
            GameLocalization.Get("shop_purchase_success_message"), items,
            GameLocalization.Get("shop_purchase_success_button"));
    }

    private enum CollectTarget { Coins, Lives, LevelButton }

    private static CollectTarget TargetOf(ShopReward reward)
    {
        if (reward.kind == ShopReward.Kind.Coins) return CollectTarget.Coins;
        if (reward.kind == ShopReward.Kind.Life || reward.kind == ShopReward.Kind.InfiniteLifeTimed) return CollectTarget.Lives;
        if (reward.kind == ShopReward.Kind.Timed && reward.timedType == DailySlotRewardType.Lives) return CollectTarget.Lives;
        return CollectTarget.LevelButton;
    }

    // Altın → üst bardaki altın ikonu (sayaç satın alma öncesinden yeni değere sayar), can → kalp,
    // diğer her şey → level butonu.
    private static ShopPurchaseCelebration.CollectRoute BuildMenuCollect(List<CollectTarget> targets, int coinGain)
    {
        MainMenuWalletDisplay wallet = null;
        int coinsAfter = PlayerWallet.Coins;
        int coinsBefore = Mathf.Max(0, coinsAfter - coinGain);
        int coinItemsLeft = targets.FindAll(t => t == CollectTarget.Coins).Count;

        return new ShopPurchaseCelebration.CollectRoute
        {
            SwitchToHome = () =>
            {
                MarketNavigator.ReturnHome();
                wallet = Object.FindFirstObjectByType<MainMenuWalletDisplay>();
                if (wallet != null && coinGain > 0) wallet.SetCoinsInstant(coinsBefore);
            },
            TargetFor = i =>
            {
                var kind = i >= 0 && i < targets.Count ? targets[i] : CollectTarget.LevelButton;
                switch (kind)
                {
                    case CollectTarget.Coins:
                        var text = wallet != null ? wallet.ChipMoneyText : null;
                        if (text == null) break;
                        var icon = text.transform.parent != null ? text.transform.parent.Find("GoldMoney") : null;
                        return icon != null ? (RectTransform)icon : text.rectTransform;
                    case CollectTarget.Lives:
                        var lives = Object.FindFirstObjectByType<MainMenuLivesDisplay>();
                        if (lives != null) return lives.HeartTarget;
                        break;
                }
                var level = Object.FindFirstObjectByType<MainMenuLevelButtonController>();
                return level != null ? (RectTransform)level.transform : null;
            },
            OnLanded = i =>
            {
                if (i < 0 || i >= targets.Count || targets[i] != CollectTarget.Coins) return;
                if (--coinItemsLeft > 0 || wallet == null || wallet.ChipMoneyText == null) return;
                wallet.StartCoroutine(UiNumberTween.Tween(wallet.ChipMoneyText, coinsBefore, coinsAfter, 0.5f));
            },
        };
    }

    private static string NameKey(ShopReward reward)
    {
        switch (reward.kind)
        {
            case ShopReward.Kind.Coins: return "shop_reward_coins";
            case ShopReward.Kind.Stars: return "shop_reward_stars";
            case ShopReward.Kind.Life: return "shop_reward_lives";
            case ShopReward.Kind.InfiniteLifeTimed: return "shop_reward_infinite_lives";
            case ShopReward.Kind.Booster:
                return reward.booster switch
                {
                    BoardController.BoosterMode.Single => "shop_reward_hammer",
                    BoardController.BoosterMode.Row => "shop_reward_row",
                    BoardController.BoosterMode.Column => "shop_reward_column",
                    BoardController.BoosterMode.Shuffle => "shop_reward_shuffle",
                    _ => "shop_reward_booster"
                };
            case ShopReward.Kind.Timed:
                return reward.timedType switch
                {
                    DailySlotRewardType.Lives => "shop_reward_infinite_lives",
                    DailySlotRewardType.Joker_LineH => "shop_reward_line",
                    DailySlotRewardType.Joker_Line => "shop_reward_line",
                    DailySlotRewardType.Joker_PulseCore => "shop_reward_pulsecore",
                    DailySlotRewardType.Joker_SystemOverride => "shop_reward_override",
                    DailySlotRewardType.Booster_Hammer => "shop_reward_hammer",
                    DailySlotRewardType.Booster_Row => "shop_reward_row",
                    DailySlotRewardType.Booster_Column => "shop_reward_column",
                    DailySlotRewardType.Booster_Shuffle => "shop_reward_shuffle",
                    _ => "shop_reward_timed"
                };
            default: return "shop_reward_booster";
        }
    }
}
