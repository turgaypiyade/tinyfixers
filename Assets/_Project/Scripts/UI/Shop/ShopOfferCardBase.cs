using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mağaza kartlarının ortak tabanı: kart yüzeyi + fiyat butonu + uygunluk/cooldown/afford mantığı.
/// Gövde (kutular ya da coin satırı) alt-sınıflarda <see cref="BuildBody"/> ile kurulur.
/// </summary>
public abstract class ShopOfferCardBase : MonoBehaviour
{
    [Header("Ortak")]
    [SerializeField] protected Image cardBackground;
    [SerializeField] protected Button priceButton;
    [SerializeField] protected Image priceButtonBackground;
    [SerializeField] protected TMP_Text priceText;

    protected ShopOffer offer;
    protected UITheme theme;
    private Action<ShopOffer> onPurchase;

    private bool countdownMode;     // OncePerDay cooldown sırasında her saniye etiketi tazele
    private int lastShownSecond = -1;

    public void Configure(ShopOffer data, UITheme uiTheme, Action<ShopOffer> purchaseHandler)
    {
        offer = data;
        theme = uiTheme;
        onPurchase = purchaseHandler;
        if (offer == null) return;

        // NOT: cardBackground'a DOKUNMUYORUZ — kart çerçevesi (MegaAwards1/OnlyGolds) prefab'ta
        // sabit atanır; runtime'da ezersek sprite kaybolur.

        BuildBody();
        RefreshPrice();

        if (priceButton != null)
        {
            priceButton.onClick.RemoveAllListeners();
            priceButton.onClick.AddListener(HandleClick);
        }
    }

    /// <summary>Kartın gövdesini kur (kutular / coin görseli / isim). Configure içinden çağrılır.</summary>
    protected abstract void BuildBody();

    private void Update()
    {
        if (!countdownMode || offer == null) return;
        int sec = (int)ShopState.CooldownRemaining(offer).TotalSeconds;
        if (sec == lastShownSecond) return;
        lastShownSecond = sec;
        RefreshPrice();   // süre bitince otomatik tekrar uygun hâle döner
    }

    protected void RefreshPrice()
    {
        if (priceText == null || offer == null) return;

        // Önce uygunluk: cooldown'da mı, sahip olunmuş mu?
        countdownMode = false;
        if (!ShopState.IsAvailable(offer))
        {
            bool owned = offer.availability == ShopOffer.Availability.OnceEver;
            priceText.text = owned
                ? "Alındı"
                : ShopState.FormatRemaining(ShopState.CooldownRemaining(offer));
            countdownMode = !owned;   // cooldown ise her saniye tazele
            ApplyButtonState(available: false, affordable: false);
            return;
        }

        priceText.text = offer.priceType switch
        {
            ShopOffer.PriceType.RealMoney => offer.priceLabel,
            ShopOffer.PriceType.Coins     => offer.priceAmount.ToString("N0"),
            ShopOffer.PriceType.Stars     => offer.priceAmount.ToString("N0"),
            ShopOffer.PriceType.Free      => "BEDAVA",
            _ => offer.priceLabel
        };

        ApplyButtonState(available: true, affordable: CanAfford());
    }

    /// <summary>
    /// Butonun görünürlüğü: özel bir buton sprite'ı (örn BuyButton) varsa rengini BOZMAZ, yalnız
    /// karşılanamaz/uygun-değil durumda soluklaştırır. Sprite yoksa temadan renk basar (yeşil/amber).
    /// </summary>
    private void ApplyButtonState(bool available, bool affordable)
    {
        float alpha = !available ? 0.6f : (affordable ? 1f : 0.45f);

        if (priceButtonBackground != null)
        {
            if (priceButtonBackground.sprite != null)
            {
                var c = Color.white; c.a = alpha;
                priceButtonBackground.color = c;   // art'ı koru, yalnız soluklaştır
            }
            else if (theme != null)
            {
                Color bg = offer.priceType switch
                {
                    ShopOffer.PriceType.Coins => theme.accentAmber,
                    ShopOffer.PriceType.Stars => theme.accentAmber,
                    _                         => theme.priceGreen
                };
                bg.a = alpha;
                UITheme.ApplySurface(priceButtonBackground, theme.buttonBackground, bg);
            }
        }

        if (theme != null) theme.ApplyText(priceText, theme.textLight, heading: true);
        if (priceButton != null) priceButton.interactable = available && affordable;
    }

    private bool CanAfford()
    {
        return offer.priceType switch
        {
            ShopOffer.PriceType.Coins => PlayerWallet.HasEnoughCoins(offer.priceAmount),
            ShopOffer.PriceType.Stars => PlayerWallet.HasEnoughStars(offer.priceAmount),
            _ => true // RealMoney / Free her zaman tıklanabilir
        };
    }

    private void HandleClick() => onPurchase?.Invoke(offer);
}
