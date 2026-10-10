using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Can/hak sistemi.
///
/// Kurallar:
///   - Başlangıç: 5 can.
///   - RegenCap (5) altına düşünce timer başlar; 30 dk'da 1 can.
///   - RegenCap'e ulaşınca timer durur.
///   - Takım, etkinlik ve reklam dahil toplam can en fazla 10 olabilir.
///     Regen yalnız RegenCap altına düşünce devreye girer.
///   - Can = 0 iken oynamak engellenir; reklam ile 1 can kazanılabilir.
/// </summary>
public static class LivesManager
{
    // ── Ayarlar ──────────────────────────────────────────────────────────────

    /// <summary>Regen'in doldurduğu tavan (5). Bu sayıda veya üzerinde timer durar.</summary>
    private static int regenCapLives = 5;
    public static int RegenCapLives
    {
        get => regenCapLives;
        set => regenCapLives = Mathf.Clamp(value, 1, MaxLives);
    }

    /// <summary>Etkinlik/hediye/reklam ile ulaşılabilecek mutlak maksimum.</summary>
    public const int MaxLives = 10;

    /// <summary>Her canın gelmesi için gereken dakika.</summary>
    public static int RegenIntervalMinutes { get; set; } = 30;

    // ── PlayerPrefs anahtarları ───────────────────────────────────────────────

    private const string KeyPurchaseThanks = "purchase_thanks_receipts_v1";
    private const string KeyPendingThanks = "purchase_thanks_pending_lives";

    private const string KeyLives     = "lives_current";
    private const string KeyNextTicks = "lives_next_ticks";

    // ── Durum ────────────────────────────────────────────────────────────────

    private static int      _lives;
    private static DateTime _nextLifeTime;
    private static bool     _initialized;

    // ── Olaylar ──────────────────────────────────────────────────────────────

    public static event Action OnLivesChanged;

    // ── Özellikler ───────────────────────────────────────────────────────────

    public static int  Current  { get { EnsureInit(); return _lives; } }
    public static bool HasLives { get { EnsureInit(); return _lives > 0 || TimedRewardService.IsLivesFree(); } }

    /// <summary>RegenCap'e ulaşıldıysa timer gösterilmez.</summary>
    public static bool IsRegenFull { get { EnsureInit(); return _lives >= RegenCapLives; } }

    public static DateTime NextLifeTime { get { EnsureInit(); return _nextLifeTime; } }

    public static TimeSpan TimeUntilNextLife
    {
        get
        {
            EnsureInit();
            if (_lives >= RegenCapLives) return TimeSpan.Zero;
            var span = _nextLifeTime - DateTime.UtcNow;
            return span < TimeSpan.Zero ? TimeSpan.Zero : span;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public static bool SpendLife()
    {
        EnsureInit();
        if (TimedRewardService.IsLivesFree()) return true; // ücretsiz can aktif
        if (_lives <= 0) return false;

        // RegenCap'teyken düşüyorsa timer'ı başlat.
        if (_lives >= RegenCapLives)
            _nextLifeTime = DateTime.UtcNow.AddMinutes(RegenIntervalMinutes);

        _lives--;
        Save();
        OnLivesChanged?.Invoke();
        return true;
    }

    /// <summary>Her kaynaktan gelen canı ortak 10 can sınırına uygular.</summary>
    public static void AddLives(int amount)
    {
        EnsureInit();
        amount = Mathf.Min(amount, MaxLives - _lives);
        if (amount <= 0) return;
        _lives += amount;
        Save();
        OnLivesChanged?.Invoke();
    }

    public static int PendingPurchaseThanks => Mathf.Max(0, PlayerPrefs.GetInt(KeyPendingThanks, 0));

    // Called after verified fulfilment (or explicitly labelled development simulation).
    // The receipt and gift balance share the same Save; duplicate callbacks cannot add another gift.
    internal static bool GrantPurchaseThanks(string transactionId)
    {
        if (RuntimeSimulationSession.IsActive || string.IsNullOrWhiteSpace(transactionId)) return false;
        EnsureInit();
        string receipt;
        using (var sha = SHA256.Create())
            receipt = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(transactionId)));
        string receipts = PlayerPrefs.GetString(KeyPurchaseThanks, "");
        if (("\n" + receipts).Contains("\n" + receipt + "\n")) return false;

        PlayerPrefs.SetString(KeyPurchaseThanks, receipts + receipt + "\n");
        PlayerPrefs.SetInt(KeyPendingThanks, PendingPurchaseThanks + 1);
        ApplyPendingPurchaseThanks();
        Save();
        OnLivesChanged?.Invoke();
        return true;
    }

    /// Claim overflow gifts at a new attempt, not during fail/continue life refunds.
    public static void ClaimPendingPurchaseThanks()
    {
        if (RuntimeSimulationSession.IsActive) return;
        EnsureInit();
        if (!ApplyPendingPurchaseThanks()) return;
        Save();
        OnLivesChanged?.Invoke();
    }

    private static bool ApplyPendingPurchaseThanks()
    {
        int gain = Mathf.Min(PendingPurchaseThanks, MaxLives - _lives);
        if (gain <= 0) return false;
        PlayerPrefs.SetInt(KeyPendingThanks, PendingPurchaseThanks - gain);
        _lives += gain;
        return true;
    }

    /// <summary>Timer servisi tarafından saniyede bir çağrılır.</summary>
    public static bool TickRegen()
    {
        EnsureInit();
        if (_lives >= RegenCapLives) return false;
        if (DateTime.UtcNow < _nextLifeTime) return false;

        _lives++;

        if (_lives < RegenCapLives)
            _nextLifeTime = _nextLifeTime.AddMinutes(RegenIntervalMinutes);

        Save();
        OnLivesChanged?.Invoke();
        return true;
    }

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        int savedLives = PlayerPrefs.GetInt(KeyLives, 5);
        _lives = Mathf.Clamp(savedLives, 0, MaxLives);

        long ticks = long.TryParse(PlayerPrefs.GetString(KeyNextTicks, "0"), out long t) ? t : 0;
        _nextLifeTime = ticks > 0
            ? new DateTime(ticks, DateTimeKind.Utc)
            : DateTime.UtcNow.AddMinutes(RegenIntervalMinutes);

        ProcessOfflineRegen();
        bool claimedThanks = ApplyPendingPurchaseThanks();
        if (claimedThanks || _lives != savedLives) Save();
    }

    // ── Dahili ───────────────────────────────────────────────────────────────

    private static void EnsureInit()
    {
        if (!_initialized) Initialize();
    }

    private static void ProcessOfflineRegen()
    {
        bool changed = false;
        while (_lives < RegenCapLives && DateTime.UtcNow >= _nextLifeTime)
        {
            _lives++;
            changed = true;
            if (_lives < RegenCapLives)
                _nextLifeTime = _nextLifeTime.AddMinutes(RegenIntervalMinutes);
        }

        if (changed) Save();
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(KeyLives, _lives);
        PlayerPrefs.SetString(KeyNextTicks, _nextLifeTime.Ticks.ToString());
        PlayerPrefs.Save();
    }
}
