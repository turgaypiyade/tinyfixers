using TMPro;
using UnityEngine;

/// <summary>
/// Coin bakiyesi "pill" widget'ı (fail popup'taki FailWalletBalance ile aynı görünüm):
/// sol coin ikonu + üstüne binen pill arka plan + ortada sayı. PlayerWallet.Coins'i canlı gösterir.
/// Görsel çocuklar (ikon/bg/text) düzenleyici komutuyla kurulur; bu bileşen yalnız sayıyı tazeler.
/// </summary>
public sealed class WalletBalancePill : MonoBehaviour
{
    [SerializeField] private TMP_Text amountText;

    private void OnEnable()
    {
        PlayerWallet.OnCoinsChanged += Refresh;
        Refresh(PlayerWallet.Coins);
    }

    private void OnDisable()
    {
        PlayerWallet.OnCoinsChanged -= Refresh;
    }

    private void Refresh(int amount)
    {
        if (amountText != null) amountText.text = amount.ToString("N0");
    }
}
