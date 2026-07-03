using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mağaza içeriği — tamamen veri. Bölümler (Özel Teklifler, Mega Fırsatlar, Coin Paketleri…)
/// ve her bölümdeki teklifler. ShopScreenController bunu okuyup kart prefab'larını basar.
/// İllüstrasyonlar (teklif görseli, item ikonları) buraya Inspector'da bağlanır.
///
/// Oluştur: Assets > Create > TinyFixers > Shop Catalog
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Shop Catalog", fileName = "ShopCatalog")]
public sealed class ShopCatalog : ScriptableObject
{
    public List<ShopSection> sections = new();
}

/// <summary>Mağaza bölümü — başlık + altındaki teklifler. Stil banner rengini seçer.</summary>
[Serializable]
public sealed class ShopSection
{
    public string title = "Bölüm";

    public enum BandStyle { Header, Special }
    [Tooltip("Header = mor band; Special = magenta 'Özel Teklifler' band.")]
    public BandStyle bandStyle = BandStyle.Header;

    public List<ShopOffer> offers = new();
}

/// <summary>Tek bir teklif/paket. Sol büyük görsel + miktar, içerik chip'leri, fiyat butonu.</summary>
[Serializable]
public sealed class ShopOffer
{
    [Tooltip("Kalıcılık/analitik için benzersiz id (örn 'coins_2000', 'mega_5000').")]
    public string id = "offer_id";

    public string displayName = "Teklif";

    [Tooltip("Sol taraftaki büyük görsel (coin yığını / kupa / sandık).")]
    public Sprite heroIcon;

    [Tooltip("Hero görselin altındaki büyük miktar (örn coin 2000). 0 = gizle.")]
    public int heroAmount = 0;

    [Tooltip("Kartın içerdiği ödül chip'leri (booster ikonları, sonsuz/süreli rozetler).")]
    public List<ShopReward> contents = new();

    [Header("Uygunluk")]
    [Tooltip("Always = her zaman alınır; OncePerDay = günde bir (cooldown); OnceEver = ömürde bir.")]
    public Availability availability = Availability.Always;

    [Tooltip("OncePerDay için bekleme süresi (saat).")]
    public int cooldownHours = 24;

    public enum Availability { Always, OncePerDay, OnceEver }

    [Header("Fiyat")]
    public PriceType priceType = PriceType.RealMoney;

    [Tooltip("RealMoney için gösterilecek etiket (örn '99,99 TL'). Diğer türlerde yok sayılır.")]
    public string priceLabel = "99,99 TL";

    [Tooltip("Coins / Stars fiyat türünde harcanacak miktar.")]
    public int priceAmount = 0;

    public enum PriceType { RealMoney, Coins, Stars, Free }
}

/// <summary>Bir teklifin içindeki tek ödül kalemi: ikon + miktar/rozet + grant edilecek şey.</summary>
[Serializable]
public sealed class ShopReward
{
    public Sprite icon;

    public enum Kind { Coins, Stars, Life, Booster, InfiniteLifeTimed }
    public Kind kind = Kind.Coins;

    [Tooltip("Coins/Stars/Life miktarı, ya da Booster adedi.")]
    public int amount = 1;

    [Tooltip("Kind=Booster ise hangi booster.")]
    public BoardController.BoosterMode booster = BoardController.BoosterMode.Single;

    [Tooltip("Kind=InfiniteLifeTimed ise süre (saat). Chip'te '1s' gibi gösterilir.")]
    public int durationHours = 1;

    [Tooltip("Sonsuz rozet göster (örn sınırsız booster süreli teklif).")]
    public bool showInfinite = false;

    /// <summary>Chip üzerinde gösterilecek etiket: "x5" / "∞" / "1s".</summary>
    public string ChipLabel()
    {
        if (kind == Kind.InfiniteLifeTimed) return durationHours + "s";
        if (showInfinite) return "∞";
        return "x" + Mathf.Max(1, amount);
    }
}
