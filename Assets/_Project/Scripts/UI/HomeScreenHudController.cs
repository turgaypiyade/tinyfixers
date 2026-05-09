using TMPro;
using UnityEngine;

/// <summary>
/// Home screen HUD: mevcut seviye, toplam coin ve toplam yıldızı gösterir.
/// PlayerWallet event'lerine abone olur — değer değişince otomatik güncellenir.
/// </summary>
public class HomeScreenHudController : MonoBehaviour
{
    [Header("Seviye")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private string levelPrefix = "Seviye ";
    [SerializeField] private string prefsLevelKey = "current_level";

    [Header("Coin")]
    [SerializeField] private TMP_Text coinsText;

    [Header("Toplam Yıldız")]
    [SerializeField] private TMP_Text totalStarsText;

    // -----------------------------------------------------------------------

    private void OnEnable()
    {
        PlayerWallet.OnCoinsChanged      += RefreshCoins;
        PlayerWallet.OnTotalStarsChanged += RefreshStars;
        RefreshAll();
    }

    private void OnDisable()
    {
        PlayerWallet.OnCoinsChanged      -= RefreshCoins;
        PlayerWallet.OnTotalStarsChanged -= RefreshStars;
    }

    // -----------------------------------------------------------------------

    private void RefreshAll()
    {
        RefreshLevel();
        RefreshCoins(PlayerWallet.Coins);
        RefreshStars(PlayerWallet.TotalStars);
    }

    private void RefreshLevel()
    {
        if (levelText == null) return;
        int level = PlayerPrefs.GetInt(prefsLevelKey, 1);
        levelText.text = levelPrefix + level;
    }

    private void RefreshCoins(int amount)
    {
        if (coinsText == null) return;
        if (CoinFlyToWalletAnimator.TryGetPendingReward(out _, out int pendingBefore, out _))
            amount = pendingBefore;

        coinsText.text = amount.ToString("N0");   // "1.400" gibi binlik ayraçlı
    }

    private void RefreshStars(int amount)
    {
        if (totalStarsText == null) return;
        if (StarFlyToWalletAnimator.TryGetPendingReward(out _, out int pendingBefore, out _))
            amount = pendingBefore;

        totalStarsText.text = amount.ToString();
    }
}
