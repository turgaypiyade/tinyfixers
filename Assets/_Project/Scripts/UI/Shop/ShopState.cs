using System;
using UnityEngine;

/// <summary>
/// Teklif satın alma durumu — kalıcı (PlayerPrefs). Günlük teklif cooldown'u ve ömürde-bir
/// tekliflerin "sahip olundu" bilgisi. Always teklifler her zaman uygundur (kayıt tutulmaz).
/// </summary>
public static class ShopState
{
    private static string LastKey(string id)  => "shop_last_" + id;
    private static string OwnedKey(string id) => "shop_owned_" + id;

    public static bool IsAvailable(ShopOffer offer)
    {
        if (offer == null) return false;
        return offer.availability switch
        {
            ShopOffer.Availability.OnceEver   => !IsOwned(offer),
            ShopOffer.Availability.OncePerDay => CooldownRemaining(offer) <= TimeSpan.Zero,
            _ => true
        };
    }

    public static bool IsOwned(ShopOffer offer)
        => PlayerPrefs.GetInt(OwnedKey(offer.id), 0) == 1;

    public static TimeSpan CooldownRemaining(ShopOffer offer)
    {
        if (!long.TryParse(PlayerPrefs.GetString(LastKey(offer.id), "0"), out long ticks) || ticks == 0)
            return TimeSpan.Zero;

        var last = new DateTime(ticks, DateTimeKind.Utc);
        var end  = last.AddHours(Mathf.Max(1, offer.cooldownHours));
        var rem  = end - DateTime.UtcNow;
        return rem > TimeSpan.Zero ? rem : TimeSpan.Zero;
    }

    public static void RecordPurchase(ShopOffer offer)
    {
        if (offer == null) return;
        switch (offer.availability)
        {
            case ShopOffer.Availability.OnceEver:
                PlayerPrefs.SetInt(OwnedKey(offer.id), 1);
                break;
            case ShopOffer.Availability.OncePerDay:
                PlayerPrefs.SetString(LastKey(offer.id), DateTime.UtcNow.Ticks.ToString());
                break;
        }
        PlayerPrefs.Save();
    }

    /// <summary>Geri sayım etiketi: ">1s" ise "23:59", altındaysa "12:30:05".</summary>
    public static string FormatRemaining(TimeSpan t)
    {
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        return $"{t.Minutes:00}:{t.Seconds:00}";
    }
}
