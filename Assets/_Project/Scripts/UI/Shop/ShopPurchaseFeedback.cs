using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared purchase receipt UI. Only the post-grant event may show success.</summary>
public static class ShopPurchaseFeedback
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        // Also safe when entering Play Mode with domain reload disabled.
        ShopPurchaseService.OnPurchased -= ShowSuccess;
        ShopPurchaseService.OnPurchased += ShowSuccess;
    }

    private static void ShowSuccess(ShopOffer offer)
    {
        if (offer == null || RuntimeSimulationSession.IsActive) return;
        var items = new List<RuntimeChoicePopup.RewardItem>();
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
                }
            }
        }

        RuntimeChoicePopup.ShowRewards(GameLocalization.Get("shop_purchase_success_title"),
            GameLocalization.Get("shop_purchase_success_message"), items,
            GameLocalization.Get("shop_purchase_success_button"));
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
