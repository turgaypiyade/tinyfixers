using System;
using UnityEngine;

/// <summary>Ödüllü reklam yerleri (analitikte sayı olarak gider — sırayı DEĞİŞTİRME, yalnız sona ekle).</summary>
public enum AdPlacement { Continue = 0, Life = 1, WheelSpin = 2 }

/// <summary>
/// Ödüllü reklamın TEK kapısı: yer başına günlük sınır + analitik (Docs/Monetization/RewardedAds_Plan.md).
/// Continue = seviye başına 2 hak (+2, +1 hamle; LevelAdRight); diğerleri yerel günde N.
/// Hak yalnız reklam GERÇEKTEN izlenince harcanır; yarıda kapanan/yüklenemeyen reklam hakkı yemez.
/// Sayaçlar cihaz yerelidir (bulut kaydına girmez): ödülün kendisi cüzdan/envanter yoluyla kaydedilir.
/// </summary>
public static class RewardedAdCaps
{
    private const string DayKey = "ad_cap_day";
    private const string CountPrefix = "ad_cap_count_";

    public static int DailyLimit(AdPlacement placement) => placement switch
    {
        AdPlacement.Life => 3,
        AdPlacement.WheelSpin => 1,
        _ => int.MaxValue,
    };

    public static bool CanOffer(AdPlacement placement) => placement == AdPlacement.Continue
        ? !LevelAdRight.IsUsed
        : UsedToday(placement) < DailyLimit(placement);

    public static int RemainingToday(AdPlacement placement) => placement == AdPlacement.Continue
        ? Mathf.Max(0, LevelAdRight.MaxPerLevel - LevelAdRight.UsedCount)
        : Mathf.Max(0, DailyLimit(placement) - UsedToday(placement));

    /// <summary>Teklif ekranda gösterildi (izlenme oranı = ad_reward / ad_offer). Ekran açılışı başına bir kez çağır.</summary>
    public static void NoteOffer(AdPlacement placement)
        => GameAnalytics.LogEvent("ad_offer", ("placement", (long)placement), ("level", CurrentLevel.Played));

    /// <summary>
    /// Reklamı gösterir; izlenip hak harcanabildiyse onReward çalışır. onDone(granted) her durumda bir kez gelir.
    /// Reklam hazır değilse AdService kendi "Reklam hazır değil" penceresini gösterir (hak harcanmaz).
    /// </summary>
    public static void Show(AdPlacement placement, Action onReward, Action<bool> onDone = null)
    {
        if (!CanOffer(placement) || AdService.IsShowing)
        {
            onDone?.Invoke(false);
            return;
        }
        AdService.ShowRewarded(earned =>
        {
            bool granted = earned && TryConsume(placement);
            if (earned)
                GameAnalytics.LogEvent("ad_reward", ("placement", (long)placement), ("level", CurrentLevel.Played),
                    ("granted", granted ? 1 : 0));
            if (granted) onReward?.Invoke();
            onDone?.Invoke(granted);
        });
    }

    private static bool TryConsume(AdPlacement placement)
    {
        if (placement == AdPlacement.Continue) return LevelAdRight.TryConsume();
        int used = UsedToday(placement);
        if (used >= DailyLimit(placement)) return false;
        PlayerPrefs.SetInt(CountPrefix + (int)placement, used + 1);
        PlayerPrefs.Save();
        return true;
    }

    private static int UsedToday(AdPlacement placement)
    {
        string today = DateTime.Now.ToString("yyyyMMdd");
        if (PlayerPrefs.GetString(DayKey, "") != today)
        {
            foreach (AdPlacement p in Enum.GetValues(typeof(AdPlacement)))
                PlayerPrefs.DeleteKey(CountPrefix + (int)p);
            PlayerPrefs.SetString(DayKey, today);
            PlayerPrefs.Save();
        }
        return PlayerPrefs.GetInt(CountPrefix + (int)placement, 0);
    }
}
