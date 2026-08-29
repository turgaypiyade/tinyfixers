using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mağaza içeriği — tamamen veri. Bölümler (Özel Teklifler, Altınlar…) ve her bölümdeki
/// teklifler. ShopScreenController bunu okuyup kart prefab'larını basar.
/// İllüstrasyonlar (kutu ikonları, coin görselleri) buraya Inspector'da bağlanır.
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

/// <summary>
/// Tek bir teklif/paket. İki görsel tip:
///  • Bundle  = altın çerçeve + yan yana ödül kutuları (groups) + mor bant (isim) + fiyat butonu.
///  • CoinRow = basit satır: tek coin görseli (groups[0]) + miktar + fiyat butonu.
/// </summary>
[Serializable]
public sealed class ShopOffer
{
    [Tooltip("Kalıcılık/analitik için benzersiz id (örn 'bundle_grand_safe', 'coins_1000').")]
    public string id = "offer_id";

    [Tooltip("Mor banttaki paket adı (örn 'Muhteşem Kasa'). CoinRow'da gösterilmez.")]
    public string displayName = "Teklif";

    public enum CardStyle { Bundle, CoinRow }
    [Tooltip("Bundle = kutulu büyük paket; CoinRow = basit altın satırı.")]
    public CardStyle cardStyle = CardStyle.Bundle;

    [Tooltip("Sol üst köşede 'En İyi Fırsat' kurdelesi göster.")]
    public bool showBestBadge = false;

    [Tooltip("Kartın ödül kutuları. Her grup = bir kutu (ikonlar + tek etiket). " +
             "CoinRow'da yalnız ilk grup kullanılır (coin ikonu + miktar).")]
    public List<ShopRewardGroup> groups = new();

    [Header("Uygunluk")]
    [Tooltip("Always = her zaman alınır; OncePerDay = günde bir (cooldown); OnceEver = ömürde bir.")]
    public Availability availability = Availability.Always;

    [Tooltip("OncePerDay için bekleme süresi (saat).")]
    public int cooldownHours = 24;

    public enum Availability { Always, OncePerDay, OnceEver }

    [Header("Fiyat")]
    public PriceType priceType = PriceType.RealMoney;

    [Tooltip("RealMoney için gösterilecek etiket (örn '3999.99 TL'). Diğer türlerde yok sayılır.")]
    public string priceLabel = "99,99 TL";

    [Tooltip("Coins / Stars fiyat türünde harcanacak miktar.")]
    public int priceAmount = 0;

    public enum PriceType { RealMoney, Coins, Stars, Free }
}

/// <summary>
/// Bir kutu: içine dizilen ikonlar (görsel) + tek etiket (miktar / adet / süre) + gerçek grant'lar.
/// Görsel (icons + label) ile ödül (grants) bilinçli olarak ayrık: "5 booster ikonu göster, x10 ver".
/// </summary>
[Serializable]
public sealed class ShopRewardGroup
{
    [Tooltip("Kutunun arka planı (örn MATGrup1/3/5 — ikon sayısına göre). Boşsa temadan gelir.")]
    public Sprite background;

    [Tooltip("Kutuya dizilecek ikonlar (kod bir grid'e yerleştirir).")]
    public List<Sprite> icons = new();

    public enum LabelMode { Currency, Count, Duration }
    [Tooltip("Currency = '50 000'; Count = 'x10'; Duration = '72s'.")]
    public LabelMode labelMode = LabelMode.Count;

    [Tooltip("Etikette gösterilecek sayı (miktar / adet / süre).")]
    public int labelValue = 1;

    [Tooltip("Duration modunda etiketin yanında küçük saat ikonu göster.")]
    public bool showTimerIcon = false;

    [Tooltip("Bu kutu satın alınınca oyuncuya GERÇEKTE verilenler (görselden bağımsız).")]
    public List<ShopReward> grants = new();

    /// <summary>Kutu altında gösterilecek etiket: "50 000" / "x10" / "72s".</summary>
    public string GroupLabel()
    {
        return labelMode switch
        {
            ShopRewardGroup.LabelMode.Currency => labelValue.ToString("N0"),
            ShopRewardGroup.LabelMode.Duration => labelValue + "s",
            _                                  => "x" + Mathf.Max(1, labelValue),
        };
    }
}

/// <summary>Grant edilecek tek ödül kalemi. Görsel taşımaz — yalnızca ne verileceği.</summary>
[Serializable]
public sealed class ShopReward
{
    public enum Kind { Coins, Stars, Life, Booster, InfiniteLifeTimed }
    public Kind kind = Kind.Coins;

    [Tooltip("Coins/Stars/Life miktarı, ya da Booster adedi.")]
    public int amount = 1;

    [Tooltip("Kind=Booster ise hangi booster.")]
    public BoardController.BoosterMode booster = BoardController.BoosterMode.Single;

    [Tooltip("Kind=InfiniteLifeTimed ise süre (saat).")]
    public int durationHours = 1;
}
