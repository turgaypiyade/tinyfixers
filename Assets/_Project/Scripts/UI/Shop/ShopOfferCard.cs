using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tek bir mağaza teklifi kartı: sol hero görseli + miktar, içerik chip'leri, isim, fiyat butonu.
/// ShopScreenController bunu bölüm container'ına basıp Configure çağırır.
/// Prefab'ı bir kez kurarsın (referansları bağlarsın); içerik koddan dolar.
/// </summary>
public sealed class ShopOfferCard : MonoBehaviour
{
    [Header("Kart Yüzeyi")]
    [SerializeField] private Image cardBackground;

    [Header("Hero (sol)")]
    [SerializeField] private Image heroIcon;
    [SerializeField] private TMP_Text heroAmountText;

    [Header("İçerik chip'leri")]
    [Tooltip("Chip'lerin basılacağı container (GridLayoutGroup önerilir).")]
    [SerializeField] private Transform chipContainer;
    [SerializeField] private ShopRewardChip chipPrefab;

    [Header("Alt şerit")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Button priceButton;
    [SerializeField] private Image priceButtonBackground;
    [SerializeField] private TMP_Text priceText;

    private readonly List<ShopRewardChip> chips = new();
    private ShopOffer offer;
    private Action<ShopOffer> onPurchase;
    private UITheme theme;
    private bool countdownMode;     // OncePerDay cooldown sırasında her saniye etiketi tazele
    private int lastShownSecond = -1;

    public void Configure(ShopOffer data, UITheme uiTheme, Action<ShopOffer> purchaseHandler)
    {
        offer = data;
        theme = uiTheme;
        onPurchase = purchaseHandler;
        if (offer == null) return;

        if (theme != null)
            UITheme.ApplySurface(cardBackground, theme.cardBackground, theme.creamSurface);

        // Hero
        if (heroIcon != null)
        {
            heroIcon.sprite  = offer.heroIcon;
            heroIcon.enabled = offer.heroIcon != null;
        }
        if (heroAmountText != null)
        {
            bool show = offer.heroAmount > 0;
            heroAmountText.gameObject.SetActive(show);
            if (show)
            {
                heroAmountText.text = offer.heroAmount.ToString("N0");
                if (theme != null) theme.ApplyText(heroAmountText, theme.textLight, heading: true);
            }
        }

        // Chip'ler
        BuildChips(theme);

        // İsim
        if (nameText != null)
        {
            nameText.text = offer.displayName;
            if (theme != null) theme.ApplyText(nameText, theme.textOnCream, heading: true);
        }

        // Fiyat + uygunluk
        RefreshPrice();

        if (priceButton != null)
        {
            priceButton.onClick.RemoveAllListeners();
            priceButton.onClick.AddListener(HandleClick);
        }
    }

    private void Update()
    {
        if (!countdownMode) return;
        int sec = (int)ShopState.CooldownRemaining(offer).TotalSeconds;
        if (sec == lastShownSecond) return;
        lastShownSecond = sec;
        RefreshPrice();   // süre bitince otomatik tekrar uygun hâle döner
    }

    private void BuildChips(UITheme theme)
    {
        foreach (var c in chips) if (c != null) Destroy(c.gameObject);
        chips.Clear();

        if (chipPrefab == null || chipContainer == null || offer.contents == null) return;

        foreach (var reward in offer.contents)
        {
            var chip = Instantiate(chipPrefab, chipContainer);
            chip.Setup(reward, theme);
            chips.Add(chip);
        }
    }

    private void RefreshPrice()
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

            if (theme != null)
            {
                Color grey = theme.panelSurface; grey.a = 0.6f;
                UITheme.ApplySurface(priceButtonBackground, theme.buttonBackground, grey);
                theme.ApplyText(priceText, theme.textLight, heading: true);
            }
            if (priceButton != null) priceButton.interactable = false;
            return;
        }

        string label = offer.priceType switch
        {
            ShopOffer.PriceType.RealMoney => offer.priceLabel,
            ShopOffer.PriceType.Coins     => offer.priceAmount.ToString("N0"),
            ShopOffer.PriceType.Stars     => offer.priceAmount.ToString("N0"),
            ShopOffer.PriceType.Free      => "BEDAVA",
            _ => offer.priceLabel
        };
        priceText.text = label;

        if (theme == null) return;

        // RealMoney/Free → yeşil; Coins/Stars → amber. Karşılanamıyorsa soluk.
        bool affordable = CanAfford();
        Color bg = offer.priceType switch
        {
            ShopOffer.PriceType.Coins => theme.accentAmber,
            ShopOffer.PriceType.Stars => theme.accentAmber,
            _                         => theme.priceGreen
        };
        if (!affordable) bg.a = 0.45f;

        UITheme.ApplySurface(priceButtonBackground, theme.buttonBackground, bg);
        theme.ApplyText(priceText, theme.textLight, heading: true);
        if (priceButton != null) priceButton.interactable = affordable;
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
