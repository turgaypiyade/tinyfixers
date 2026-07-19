using UnityEngine;

/// <summary>
/// Bot evreni boyutu (Docs/ProductionPlan.md karar #1): canlıya SIFIR sosyal içerikle
/// çıkılmaz — ~15k simüle kullanıcıyla başlanır, GERÇEK kullanıcı görüldükçe bot sayısı
/// otomatik azalır. Gerçek kullanıcı sayısı, liderlik sorgularında görülen gerçek oyuncu
/// sayısının kalıcı maksimumundan tahmin edilir (sunucu sayacı gerekmez).
/// </summary>
public static class BotPopulation
{
    private const string KeyRealUsersSeen = "real_users_seen_max";

    private const int TargetBots = 15000;   // launch evreni (10-20k kuralı)
    private const int MinBots = 1500;       // gerçek kitle büyüse de kalan taban
    private const int DecayPerRealUser = 25; // görülen her gerçek oyuncu ~25 botu emekli eder

    /// <summary>Şu an aktif bot sayısı (liderlik evreni, sıra numaraları bundan çıkar).</summary>
    public static int ActiveCount
        => Mathf.Clamp(TargetBots - RealUsersSeen * DecayPerRealUser, MinBots, TargetBots);

    /// <summary>Bugüne dek herhangi bir sorguda görülen en yüksek gerçek oyuncu sayısı.</summary>
    public static int RealUsersSeen => PlayerPrefs.GetInt(KeyRealUsersSeen, 0);

    /// <summary>Bir sorguda görülen gerçek oyuncu sayısını bildir (maksimum saklanır).</summary>
    public static void ReportRealUsers(int count)
    {
        if (count <= RealUsersSeen) return;
        PlayerPrefs.SetInt(KeyRealUsersSeen, count);
        PlayerPrefs.Save();
    }
}
