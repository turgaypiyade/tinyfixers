using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bot havuzunu yönetir.
/// — İlk açılışta targetBotPoolSize kadar bot üretir
/// — Gerçek kullanıcı arttıkça botlar azalır
/// — Event ve Team sistemlerinde ortak kullanılır
/// </summary>
public class BotService
{
    private readonly BotConfig config;
    private readonly BotNameLanguage language;
    private readonly List<BotPlayer> pool = new();

    private const string KeyBotCount      = "bot_pool_count";
    private const string KeyRealUserCount = "real_user_count";

    public IReadOnlyList<BotPlayer> Pool => pool;

    public BotService(BotConfig config)
    {
        this.config = config;
        language    = BotNameGenerator.DetectLanguage(config);
    }

    // -----------------------------------------------------------------------
    // Başlatma

    public void Initialize()
    {
        pool.Clear();
        BotNameGenerator.ClearUsedNames();
        NamePool.ResetCursors();   // isim havuzunu baştan benzersiz dağıt

        int activeBotCount = CalculateActiveBotCount();
        for (int i = 0; i < activeBotCount; i++)
            pool.Add(CreateBot(i));

        Debug.Log($"[BotService] {pool.Count} bot hazırlandı (gerçek kullanıcı: {RealUserCount})");
    }

    private BotPlayer CreateBot(int index)
    {
        return new BotPlayer
        {
            botId       = $"bot_{index}",
            displayName = BotNameGenerator.Generate(language),
            level       = UnityEngine.Random.Range(1, 20),
            winRate     = config.botWinRate + UnityEngine.Random.Range(-0.1f, 0.1f),
        };
    }

    // -----------------------------------------------------------------------
    // Havuz boyutu

    public int CalculateActiveBotCount()
    {
        int real    = RealUserCount;
        int maxBots = config.targetBotPoolSize;
        int thresh  = config.realUserThreshold;

        if (real >= thresh) return 0;

        // Gerçek kullanıcı arttıkça lineer azal
        float ratio = 1f - (float)real / thresh;
        return Mathf.RoundToInt(maxBots * ratio);
    }

    // -----------------------------------------------------------------------
    // Gerçek kullanıcı sayısı (ileride sunucudan gelecek)

    public int RealUserCount
    {
        get => PlayerPrefs.GetInt(KeyRealUserCount, 0);
        set
        {
            PlayerPrefs.SetInt(KeyRealUserCount, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }

    // -----------------------------------------------------------------------
    // Havuzdan bot çekme

    /// <summary>Belirtilen level'a en yakın botları döner.</summary>
    public List<BotPlayer> GetBotsNearLevel(int playerLevel, int count)
    {
        if (pool.Count == 0) return new List<BotPlayer>();

        var sorted = new List<BotPlayer>(pool);
        sorted.Sort((a, b) =>
            Mathf.Abs(a.level - playerLevel).CompareTo(Mathf.Abs(b.level - playerLevel)));

        int take = Mathf.Min(count, sorted.Count);
        return sorted.GetRange(0, take);
    }

    /// <summary>Takım doldurmak için rastgele bot listesi döner.</summary>
    public List<BotPlayer> GetRandomBots(int count)
    {
        var result = new List<BotPlayer>(pool);

        // Fisher-Yates shuffle
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        int take = Mathf.Min(count, result.Count);
        return result.GetRange(0, take);
    }
}

// -----------------------------------------------------------------------

[Serializable]
public class BotPlayer
{
    public string botId;
    public string displayName;
    public int    level;
    public float  winRate;
    public string teamId;

    public bool SimulateWin() =>
        UnityEngine.Random.value < Mathf.Clamp01(winRate);
}
