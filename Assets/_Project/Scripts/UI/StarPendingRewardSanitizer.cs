using UnityEngine;

public static class StarPendingRewardSanitizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ClearStalePendingStarReward()
    {
        int pendingReward = PlayerPrefs.GetInt(StarFlyToWalletAnimator.PendingRewardKey, 0);
        if (pendingReward <= 0)
            return;

        int walletTotal = PlayerWallet.TotalStars;
        int pendingAfter = PlayerPrefs.GetInt(StarFlyToWalletAnimator.PendingAfterKey, walletTotal);
        int pendingBefore = PlayerPrefs.GetInt(
            StarFlyToWalletAnimator.PendingBeforeKey,
            pendingAfter - pendingReward);

        if (pendingAfter == walletTotal)
            return;

        Debug.LogWarning(
            $"[StarPendingRewardSanitizer] Stale pending star reward cleared. " +
            $"reward={pendingReward}, before={pendingBefore}, after={pendingAfter}, wallet={walletTotal}");
        StarFlyToWalletAnimator.ClearPendingReward();
    }
}
