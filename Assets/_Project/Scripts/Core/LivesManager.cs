using System;
using UnityEngine;

/// <summary>
/// Can/hak sistemi.
///
/// Kurallar:
///   - Başlangıç: 5 can.
///   - RegenCap (5) altına düşünce timer başlar; 30 dk'da 1 can.
///   - RegenCap'e ulaşınca timer durur.
///   - Etkinlik/reklam ile RegenCap ve MaxLives üzeri can alınabilir;
///     regen yine ancak RegenCap altına düşünce devreye girer.
///   - Can = 0 iken oynamak engellenir; reklam ile 1 can kazanılabilir.
/// </summary>
public static class LivesManager
{
    // ── Ayarlar ──────────────────────────────────────────────────────────────

    /// <summary>Regen'in doldurduğu tavan (5). Bu sayıda veya üzerinde timer durar.</summary>
    public static int RegenCapLives { get; set; } = 5;

    /// <summary>Etkinlik/hediye/reklam ile ulaşılabilecek mutlak maksimum.</summary>
    public static int MaxLives { get; set; } = 10;

    /// <summary>Her canın gelmesi için gereken dakika.</summary>
    public static int RegenIntervalMinutes { get; set; } = 30;

    // ── PlayerPrefs anahtarları ───────────────────────────────────────────────

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
    public static bool HasLives { get { EnsureInit(); return _lives > 0; } }

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
        if (_lives <= 0) return false;

        // RegenCap'teyken düşüyorsa timer'ı başlat.
        if (_lives >= RegenCapLives)
            _nextLifeTime = DateTime.UtcNow.AddMinutes(RegenIntervalMinutes);

        _lives--;
        Save();
        OnLivesChanged?.Invoke();
        return true;
    }

    /// <summary>Can ekler. MaxLives üzerine çıkabilir (etkinlik, reklam ödülü).</summary>
    public static void AddLives(int amount)
    {
        EnsureInit();
        if (amount <= 0) return;
        _lives += amount;
        Save();
        OnLivesChanged?.Invoke();
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

        _lives = PlayerPrefs.GetInt(KeyLives, 5);

        long ticks = long.TryParse(PlayerPrefs.GetString(KeyNextTicks, "0"), out long t) ? t : 0;
        _nextLifeTime = ticks > 0
            ? new DateTime(ticks, DateTimeKind.Utc)
            : DateTime.UtcNow.AddMinutes(RegenIntervalMinutes);

        ProcessOfflineRegen();
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
