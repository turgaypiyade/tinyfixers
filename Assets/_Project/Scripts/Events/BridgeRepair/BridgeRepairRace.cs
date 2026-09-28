using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Yarıştaki bir yarışmacı (oyuncu veya bot) — sunum için gereken her şey.</summary>
public struct BridgeContestant
{
    public int    slot;            // 0 = oyuncu, 1.. = botlar (bot index)
    public bool   isPlayer;
    public string displayName;
    public Sprite avatar;
    public int    characterIndex;  // config.characters içindeki karakter
    public int    lane;            // 0 = en üst köprü
}

/// <summary>
/// Bridge Repair yarış simülasyonu — saf, kayıtsız. Her şey koşu seed'i + katılım zamanından hesaplanır:
///  - Her botun bitiş süresi [botMinFinishHours, botMaxFinishHours] aralığında TAMAMEN rastgele (sıra oranı yok).
///  - Bot adımları o süreye rastgele ağırlıklarla dağıtılır → ilerleme her an hesaplanabilir, oyuncu
///    çevrimdışıyken de rakipler ilerler; aynı koşu hep aynı sonucu verir.
///
/// PRODUCTION NOTU: rakipler şimdilik bot. Gerçek yarışmacı eşleştirmesi (Firestore) geldiğinde
/// <see cref="BuildContestants"/> + bot zaman çizelgesi gerçek oyuncu ilerlemesiyle değiştirilecek;
/// gerçek oyuncu bulunamayan koltuklar bu botlarla doldurulur (kullanıcı kuralı).
/// </summary>
public static class BridgeRepairRace
{
    public static int BotCount(BridgeRepairConfig config) => Mathf.Max(0, config.contestantCount - 1);

    /// Koşu hâlâ geçerli mi (katıldı + aynı pencere sürüyor).
    public static bool IsRunLive(BridgeRepairConfig config, DateTime utcNow)
    {
        if (config == null || !BridgeRepairState.HasJoined) return false;
#if UNITY_EDITOR
        if (config.debugForceAvailable) return true;
#endif
        return BridgeRepairSchedule.GetCycleKey(config, utcNow) == BridgeRepairState.CycleKey
               && BridgeRepairSchedule.IsActiveNow(config, utcNow);
    }

    // ── Bot zaman çizelgesi ──────────────────────────────────────

    /// Botun i. adımı (1..N) tamamladığı UTC zamanı.
    public static DateTime BotStepUtc(BridgeRepairConfig config, int bot, int step)
    {
        var times = BotStepHours(config, bot);
        step = Mathf.Clamp(step, 1, times.Length);
        return BridgeRepairState.JoinUtc.AddHours(times[step - 1]);
    }

    public static DateTime BotFinishUtc(BridgeRepairConfig config, int bot) =>
        BotStepUtc(config, bot, config.levelsToFinish);

    public static int BotProgressAt(BridgeRepairConfig config, int bot, DateTime utc)
    {
        var times = BotStepHours(config, bot);
        double elapsed = (utc - BridgeRepairState.JoinUtc).TotalHours;
        int done = 0;
        while (done < times.Length && times[done] <= elapsed) done++;
        return done;
    }

    public static int BotsFinishedBefore(BridgeRepairConfig config, DateTime utc)
    {
        int count = 0;
        for (int b = 1; b <= BotCount(config); b++)
            if (BotFinishUtc(config, b) <= utc) count++;
        return count;
    }

    /// Oyuncu bitirmeden ilk N sıra doldu mu? (Bu event'te oyuncu için tek kaybetme kuralı.)
    public static bool IsEliminated(BridgeRepairConfig config, DateTime utc)
    {
        if (config == null || !BridgeRepairState.HasJoined || BridgeRepairState.IsFinished) return false;
        return BotsFinishedBefore(config, utc) >= Mathf.Max(1, config.prizeRanks);
    }

    /// Yarış oyuncu için bitti mi (bitirdi ya da ilk N doldu).
    public static bool RunEnded(BridgeRepairConfig config, DateTime utc) =>
        BridgeRepairState.HasJoined && (BridgeRepairState.IsFinished || IsEliminated(config, utc));

    /// Yarışın oyuncu için bittiği an: bitirdiyse bitiş damgası, elendiyse N. botun bitiş anı.
    public static DateTime RunEndUtc(BridgeRepairConfig config, DateTime utc)
    {
        if (!BridgeRepairState.HasJoined) return DateTime.MinValue;
        if (BridgeRepairState.IsFinished) return BridgeRepairState.FinishUtc;
        if (!IsEliminated(config, utc)) return DateTime.MinValue;
        var finishes = new List<DateTime>();
        for (int b = 1; b <= BotCount(config); b++) finishes.Add(BotFinishUtc(config, b));
        finishes.Sort();
        return finishes[Mathf.Clamp(config.prizeRanks - 1, 0, finishes.Count - 1)];
    }

    /// Yeni yarışa giriş beklemesi (bitişten itibaren rejoinCooldownMinutes). Bitmediyse / süre dolduysa Zero.
    public static TimeSpan RejoinCooldownRemaining(BridgeRepairConfig config, DateTime utc)
    {
        DateTime end = RunEndUtc(config, utc);
        if (end == DateTime.MinValue) return TimeSpan.Zero;
        var remaining = end.AddMinutes(Mathf.Max(0, config.rejoinCooldownMinutes)) - utc;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public static int ProgressAt(BridgeRepairConfig config, BridgeContestant c, DateTime utc, int playerWins) =>
        c.isPlayer ? Mathf.Min(playerWins, config.levelsToFinish) : BotProgressAt(config, c.slot, utc);

    /// Bitiş sırası (1..N) — yalnız verilen ana kadar bitirmiş olanlar için; bitirmediyse 0.
    public static int FinishRankAt(BridgeRepairConfig config, BridgeContestant c, DateTime utc)
    {
        DateTime mine;
        if (c.isPlayer)
        {
            if (!BridgeRepairState.IsFinished || BridgeRepairState.FinishUtc > utc) return 0;
            return Mathf.Max(1, BridgeRepairState.FinalRank);
        }

        mine = BotFinishUtc(config, c.slot);
        if (mine > utc) return 0;

        int rank = 1;
        for (int b = 1; b <= BotCount(config); b++)
            if (b != c.slot && BotFinishUtc(config, b) < mine) rank++;
        if (BridgeRepairState.IsFinished && BridgeRepairState.FinishUtc <= mine) rank++;
        return rank;
    }

    private static readonly Dictionary<long, double[]> s_cache = new();

    // Adım saatleri (katılımdan itibaren, artan). Koşu seed'i + bot index'ten deterministik.
    private static double[] BotStepHours(BridgeRepairConfig config, int bot)
    {
        int seed = BridgeRepairState.RunSeed;
        int steps = Mathf.Max(1, config.levelsToFinish);
        long key = ((long)seed << 20) ^ ((long)bot << 12) ^ steps
                   ^ ((long)BitConverter.SingleToInt32Bits(config.botMinFinishHours) << 32)
                   ^ ((long)BitConverter.SingleToInt32Bits(config.botMaxFinishHours) << 40);
        if (s_cache.TryGetValue(key, out var cached)) return cached;

        var rng = new System.Random(unchecked(seed * 7919 + bot * 104729));
        double min = Mathf.Min(config.botMinFinishHours, config.botMaxFinishHours);
        double max = Mathf.Max(config.botMinFinishHours, config.botMaxFinishHours);
        double total = min + (max - min) * rng.NextDouble();

        var weights = new double[steps];
        double sum = 0;
        for (int i = 0; i < steps; i++) { weights[i] = 0.4 + rng.NextDouble() * 1.2; sum += weights[i]; }

        var times = new double[steps];
        double acc = 0;
        for (int i = 0; i < steps; i++) { acc += weights[i]; times[i] = total * acc / sum; }

        if (s_cache.Count > 64) s_cache.Clear();
        s_cache[key] = times;
        return times;
    }

    // ── Yarışmacılar ─────────────────────────────────────────────

    /// Rakip isimleri — katılımda bir kez üretilir ve BridgeRepairState'e yazılır.
    public static string[] GenerateBotNames(BridgeRepairConfig config)
    {
        var lang = Application.systemLanguage == SystemLanguage.Turkish
            ? BotNameLanguage.Turkish : BotNameLanguage.English;
        var names = new string[BotCount(config)];
        for (int i = 0; i < names.Length; i++) names[i] = BotNameGenerator.Generate(lang);
        return names;
    }

    /// Oyuncu + botlar; karakter ve köprü (şerit) ataması koşu seed'inden (her koşuda farklı, koşu içinde sabit).
    public static List<BridgeContestant> BuildContestants(BridgeRepairConfig config)
    {
        int count = Mathf.Max(1, config.contestantCount);
        int seed = BridgeRepairState.RunSeed;
        var rng = new System.Random(seed ^ 0x5bd1e995);
        var names = BridgeRepairState.BotNames;

        int characterCount = Mathf.Max(1, config.characters != null ? config.characters.Count : 1);
        var characterOrder = Shuffled(characterCount, rng);
        var laneOrder = Shuffled(count, rng);

        var list = new List<BridgeContestant>(count);
        for (int i = 0; i < count; i++)
        {
            bool player = i == 0;
            string botId = $"bridge_bot_{seed}_{i}";
            list.Add(new BridgeContestant
            {
                slot           = i,
                isPlayer       = player,
                displayName    = player ? PlayerProfile.PlayerName
                                        : (i - 1 < names.Length ? names[i - 1] : $"Fixer {i}"),
                avatar         = player ? PlayerAvatarProvider.Current : PlayerAvatarProvider.PickForSeed(botId),
                characterIndex = characterOrder[i % characterOrder.Count],
                lane           = laneOrder[i],
            });
        }
        return list;
    }

    private static List<int> Shuffled(int n, System.Random rng)
    {
        var list = new List<int>(n);
        for (int i = 0; i < n; i++) list.Add(i);
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
