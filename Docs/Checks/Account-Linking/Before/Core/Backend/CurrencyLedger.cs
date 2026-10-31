using UnityEngine;

/// <summary>
/// Altın & yıldız için "bulut taban + offline delta" defteri (anti-hack + offline-safe).
///
/// Fikir:
///  - base  : buluttan gelen GÜVENİLİR değer (son senkron).
///  - delta : sync kapısı açıldıktan SONRA yalnızca MEŞRU Add/Spend akışlarıyla biriken net değişim
///            (geçilen miktardan; mevcut player_coins toplamından TÜRETİLMEZ — türetilse hile tabanı
///            delta'ya sızardı). Henüz buluta gitmemiş offline/oturum kazancını temsil eder.
///  - Güvenilir değer = base + delta. Buluta BU yazılır; elle düzenlenebilir player_coins DEĞİL.
///
/// Restore'da player_coins = base(bulut) + delta ile ezilir:
///   → uygulama kapalıyken yapılan PlayerPrefs hilesi (delta'ya dokunmaz) SİLİNİR,
///   → offline kazanç (delta) KORUNUR (bulut değerine eklenir).
///
/// Sync kapısı: açılana kadar değişimler delta'ya YAZILMAZ. Böylece ilk-açılış hibeleri ve restore
/// öncesi yerel durum delta'ya girmez (reinstall'da çifte hibe olmaz); bunlar restore'da bulut tabanına
/// bırakılır. Kapı boot biterken açılır (offline'da da) → sonraki oyun-içi kazançlar delta olur.
///
/// Not: Saf istemci tarafı; kararlı bir saldırgan delta anahtarını da düzenleyebilir. Amaç yaygın
/// "kapalıyken pref şişirme" hilesini kesip offline kazancı korumak. Tam güvenlik için sunucu
/// doğrulaması (Firestore rules / cloud functions) gerekir.
/// </summary>
public static class CurrencyLedger
{
    private const string KCoinsBase  = "wallet_coins_base";
    private const string KCoinsDelta = "wallet_coins_delta";
    private const string KStarsBase  = "wallet_stars_base";
    private const string KStarsDelta = "wallet_stars_delta";
    private const string KInit       = "wallet_ledger_init";
    private const string KEverSynced = "wallet_ever_synced";

    private const string KeyCoins = "player_coins";
    private const string KeyStars = "player_total_stars";

    /// <summary>Açılana kadar Record* delta'ya yazmaz (pre-sync durum bulut tabanına bırakılır).</summary>
    public static bool SyncGateOpen { get; private set; }

    /// <summary>En az bir kez gerçek restore/adopt yapıldı mı (offline brand-new ayrımı için).</summary>
    public static bool EverSynced => PlayerPrefs.GetInt(KEverSynced, 0) == 1;

    public static int CoinsDelta => PlayerPrefs.GetInt(KCoinsDelta, 0);
    public static int StarsDelta => PlayerPrefs.GetInt(KStarsDelta, 0);

    public static int TrustedCoins => Mathf.Max(0, PlayerPrefs.GetInt(KCoinsBase, 0) + PlayerPrefs.GetInt(KCoinsDelta, 0));
    public static int TrustedStars => Mathf.Max(0, PlayerPrefs.GetInt(KStarsBase, 0) + PlayerPrefs.GetInt(KStarsDelta, 0));

    public static void EnsureInit()
    {
        if (PlayerPrefs.GetInt(KInit, 0) == 1) return;
        PlayerPrefs.SetInt(KCoinsBase, PlayerPrefs.GetInt(KeyCoins, 0));
        PlayerPrefs.SetInt(KCoinsDelta, 0);
        PlayerPrefs.SetInt(KStarsBase, PlayerPrefs.GetInt(KeyStars, 0));
        PlayerPrefs.SetInt(KStarsDelta, 0);
        PlayerPrefs.SetInt(KInit, 1);
        PlayerPrefs.Save();
    }

    public static void OpenSyncGate() => SyncGateOpen = true;

    // ── Meşru değişim kaydı (PlayerWallet çağırır) ──────────────────────
    public static void RecordCoins(int signedAmount)
    {
        if (!SyncGateOpen || signedAmount == 0) return;
        EnsureInit();
        PlayerPrefs.SetInt(KCoinsDelta, PlayerPrefs.GetInt(KCoinsDelta, 0) + signedAmount);
    }

    public static void RecordStars(int signedAmount)
    {
        if (!SyncGateOpen || signedAmount == 0) return;
        EnsureInit();
        PlayerPrefs.SetInt(KStarsDelta, PlayerPrefs.GetInt(KStarsDelta, 0) + signedAmount);
    }

    // ── Restore: bulut kazandı (coins/stars bulut-otoriter) ─────────────
    // base = bulut değeri; player_coins = base + delta → hile silinir, offline delta korunur.
    public static void ApplyCloudCoins(int cloudCoins)
    {
        EnsureInit();
        PlayerPrefs.SetInt(KCoinsBase, cloudCoins);
        PlayerPrefs.SetInt(KeyCoins, TrustedCoins);
        MarkSynced();
    }

    public static void ApplyCloudStars(int cloudStars)
    {
        EnsureInit();
        PlayerPrefs.SetInt(KStarsBase, cloudStars);
        PlayerPrefs.SetInt(KeyStars, TrustedStars);
        MarkSynced();
    }

    // ── Restore: bulut yok / yerel taban benimsenir ─────────────────────
    // Yerel değer doğrudan taban olur, delta sıfırlanır (bir sonraki push buluta yazar).
    public static void AdoptLocalCoins()
    {
        EnsureInit();
        PlayerPrefs.SetInt(KCoinsBase, PlayerPrefs.GetInt(KeyCoins, 0));
        PlayerPrefs.SetInt(KCoinsDelta, 0);
        MarkSynced();
    }

    public static void AdoptLocalStars()
    {
        EnsureInit();
        PlayerPrefs.SetInt(KStarsBase, PlayerPrefs.GetInt(KeyStars, 0));
        PlayerPrefs.SetInt(KStarsDelta, 0);
        MarkSynced();
    }

    public static void AdoptLocalAsBase()
    {
        AdoptLocalCoins();
        AdoptLocalStars();
    }

    // ── Push: gönderilen delta'yı tabana katla; player_coins'i güvenilir değere hizala ──
    // (mid-session pref hilesi push'ta buluta gitmez; 15sn'de bir düzeltilir.)
    public static void FoldAfterPush(int pushedCoinsDelta, int pushedStarsDelta)
    {
        PlayerPrefs.SetInt(KCoinsBase, PlayerPrefs.GetInt(KCoinsBase, 0) + pushedCoinsDelta);
        PlayerPrefs.SetInt(KCoinsDelta, PlayerPrefs.GetInt(KCoinsDelta, 0) - pushedCoinsDelta);
        PlayerPrefs.SetInt(KStarsBase, PlayerPrefs.GetInt(KStarsBase, 0) + pushedStarsDelta);
        PlayerPrefs.SetInt(KStarsDelta, PlayerPrefs.GetInt(KStarsDelta, 0) - pushedStarsDelta);
        PlayerPrefs.SetInt(KeyCoins, TrustedCoins);
        PlayerPrefs.SetInt(KeyStars, TrustedStars);
    }

    // Push'tan hemen önce player_coins/player_total_stars'ı güvenilir değere çeker → Collect()
    // ham (belki hilelenmiş) değeri değil, base+delta'yı toplar.
    public static void ReconcileForPush()
    {
        PlayerPrefs.SetInt(KeyCoins, TrustedCoins);
        PlayerPrefs.SetInt(KeyStars, TrustedStars);
    }

    private static void MarkSynced()
    {
        PlayerPrefs.SetInt(KEverSynced, 1);
        PlayerPrefs.Save();
    }
}
