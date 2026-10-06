using System;
using UnityEngine;

/// <summary>
/// Bridge Repair kalıcı durumu (PlayerPrefs; anahtarlar CloudSaveManifest'te → cloud-save'li).
/// Pencere (cycle) değişince tüm koşu sıfırlanır.
///
/// Kazanç sayımı ana menüye dönüşü BEKLEMEZ: <see cref="PlayerStats.OnLevelCleared"/> anında sayılır ve
/// son level'da bitiş zamanı damgalanır (sıralama botların bitiş zamanıyla bu damga karşılaştırılarak
/// bulunur). Kaybetmenin cezası yok → fail tarafında hiçbir kanca yok.
/// </summary>
public static class BridgeRepairState
{
    private const string KeyCycle          = "bridge_cycle";
    private const string KeyJoined         = "bridge_joined";
    private const string KeyJoinTicks      = "bridge_join_ticks";
    private const string KeyLastAsk        = "bridge_lastask_ticks";
    private const string KeyRunSeed        = "bridge_run_seed";
    private const string KeyWins           = "bridge_wins";
    private const string KeyFinishTicks    = "bridge_finish_ticks";
    private const string KeyFinalRank      = "bridge_final_rank";
    private const string KeyRewardClaimed  = "bridge_reward_claimed";
    private const string KeyPresentedWins  = "bridge_presented_wins";
    private const string KeyPresentedTicks = "bridge_presented_ticks";
    private const string KeyEndPresented   = "bridge_end_presented";
    private const string KeyBotNames       = "bridge_bot_names";
    private const string KeyHowToCycle     = "bridge_howto_cycle";   // Nasıl oynanır bu pencerede gösterildi mi

    public static event Action OnChanged;

    // ── Cycle ────────────────────────────────────────────────────

    public static string CycleKey => PlayerPrefs.GetString(KeyCycle, "");

    /// Aktif pencereyle senkronla; pencere değiştiyse koşuyu sıfırla. Her giriş noktasında çağrılır.
    public static void SyncCycle(BridgeRepairConfig config, DateTime utcNow)
    {
        string current = BridgeRepairSchedule.GetCycleKey(config, utcNow);
#if UNITY_EDITOR
        // Debug zorlamada pencere yokken "idle" döner; yine de koşu devam edebilsin (yalnız editör).
        if (config != null && config.debugForceAvailable && current == "idle" && HasJoined) return;
#endif
        if (current == CycleKey) return;

        PlayerPrefs.SetString(KeyCycle, current);
        ResetRun();
        PlayerPrefs.SetInt(KeyJoined, 0);
        PlayerPrefs.SetString(KeyJoinTicks, "0");
        PlayerPrefs.SetString(KeyLastAsk, "0");
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    private static void ResetRun()
    {
        PlayerPrefs.SetInt(KeyWins, 0);
        PlayerPrefs.SetString(KeyFinishTicks, "0");
        PlayerPrefs.SetInt(KeyFinalRank, 0);
        PlayerPrefs.SetInt(KeyRewardClaimed, 0);
        PlayerPrefs.SetInt(KeyPresentedWins, 0);
        PlayerPrefs.SetString(KeyPresentedTicks, "0");
        PlayerPrefs.SetInt(KeyEndPresented, 0);
        PlayerPrefs.SetString(KeyBotNames, "");
    }

    // ── Katılım ──────────────────────────────────────────────────

    public static bool HasJoined => PlayerPrefs.GetInt(KeyJoined, 0) == 1;
    public static DateTime JoinUtc => ReadUtc(KeyJoinTicks);
    public static int RunSeed => PlayerPrefs.GetInt(KeyRunSeed, 1);

    /// Yeni koşu: seed + rakip isimleri burada BİR KEZ üretilip saklanır (ekran her açılışta aynı rakipleri görür).
    public static void BeginRun(DateTime utcNow, string[] botNames)
    {
        ResetRun();
        int seed = UnityEngine.Random.Range(1, int.MaxValue);
        PlayerPrefs.SetInt(KeyRunSeed, seed == 0 ? 1 : seed);
        PlayerPrefs.SetString(KeyBotNames, botNames != null ? string.Join("|", botNames) : "");
        PlayerPrefs.SetInt(KeyJoined, 1);
        PlayerPrefs.SetString(KeyJoinTicks, utcNow.Ticks.ToString());
        PlayerPrefs.SetString(KeyPresentedTicks, utcNow.Ticks.ToString());
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static string[] BotNames
    {
        get
        {
            string raw = PlayerPrefs.GetString(KeyBotNames, "");
            return string.IsNullOrEmpty(raw) ? Array.Empty<string>() : raw.Split('|');
        }
    }

    /// Nasıl oynanır yalnız pencerenin İLK katılımında (yeniden girişlerde tekrar gösterilmez).
    public static bool HowToShownThisCycle => PlayerPrefs.GetString(KeyHowToCycle, "") == CycleKey;

    public static void MarkHowToShown()
    {
        PlayerPrefs.SetString(KeyHowToCycle, CycleKey);
        PlayerPrefs.Save();
    }

    public static DateTime LastAskUtc => ReadUtc(KeyLastAsk);

    public static void MarkAsked(DateTime utcNow)
    {
        PlayerPrefs.SetString(KeyLastAsk, utcNow.Ticks.ToString());
        PlayerPrefs.Save();
    }

    // ── İlerleme ─────────────────────────────────────────────────

    public static int Wins => PlayerPrefs.GetInt(KeyWins, 0);
    public static DateTime FinishUtc => ReadUtc(KeyFinishTicks);
    public static bool IsFinished => FinishUtc != DateTime.MinValue;
    /// Bitirdiyse kesinleşen sıra (1..N), bitirmediyse 0.
    public static int FinalRank => PlayerPrefs.GetInt(KeyFinalRank, 0);
    public static bool RewardClaimed => PlayerPrefs.GetInt(KeyRewardClaimed, 0) == 1;

    public static void MarkRewardClaimed()
    {
        PlayerPrefs.SetInt(KeyRewardClaimed, 1);
        PlayerPrefs.Save();
        if (FinalRank == 1) StoreReviewService.MarkHappyMoment();
        OnChanged?.Invoke();
    }

    // ── Sunum (harita en son neyi gösterdi) ──────────────────────

    public static int PresentedWins => PlayerPrefs.GetInt(KeyPresentedWins, 0);
    public static DateTime PresentedUtc => ReadUtc(KeyPresentedTicks);
    /// Bitiş / eleme ekranı gösterildi mi (tekrar otomatik açılmasın).
    public static bool EndPresented => PlayerPrefs.GetInt(KeyEndPresented, 0) == 1;

    public static void MarkPresented(int wins, DateTime utcNow)
    {
        PlayerPrefs.SetInt(KeyPresentedWins, wins);
        PlayerPrefs.SetString(KeyPresentedTicks, utcNow.Ticks.ToString());
        PlayerPrefs.Save();
    }

    public static void MarkEndPresented()
    {
        PlayerPrefs.SetInt(KeyEndPresented, 1);
        PlayerPrefs.Save();
    }

    // ── Kazanç kancası ───────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterWinHook()
    {
        PlayerStats.OnLevelCleared -= HandleLevelCleared;
        PlayerStats.OnLevelCleared += HandleLevelCleared;
    }

    private static void HandleLevelCleared()
    {
        var config = BridgeRepairConfig.Shared;
        if (config == null || !HasJoined) return;

        DateTime now = DateTime.UtcNow;
        if (!BridgeRepairRace.IsRunLive(config, now)) return;   // pencere bitti / değişti
        if (Wins >= config.levelsToFinish) return;
        if (BridgeRepairRace.IsEliminated(config, now)) return; // ilk 3 doldu → yarış bitti

        int wins = Wins + 1;
        PlayerPrefs.SetInt(KeyWins, wins);
        if (wins >= config.levelsToFinish)
        {
            PlayerPrefs.SetString(KeyFinishTicks, now.Ticks.ToString());
            PlayerPrefs.SetInt(KeyFinalRank, BridgeRepairRace.BotsFinishedBefore(config, now) + 1);
        }
        PlayerPrefs.Save();

        // Ödül bitiş ANINDA verilir (sunumu beklemez): oyuncu ana menüye dönmeden pencere kapanıp
        // cycle sıfırlansa bile ödül kaybolmaz. Harita yalnız sandık törenini gösterir (EndPresented).
        if (wins >= config.levelsToFinish)
            GrantFinishReward(config);
        Debug.Log($"[BridgeRepair] Level kazanıldı → köprü {wins}/{config.levelsToFinish}" +
                  (wins >= config.levelsToFinish ? $" (BİTTİ, sıra {FinalRank})" : ""));
        OnChanged?.Invoke();
    }

    private static void GrantFinishReward(BridgeRepairConfig config)
    {
        if (RewardClaimed) return;
        int rank = FinalRank;
        MarkRewardClaimed();   // önce işaretle → tekrar tetiklenirse çift ödül olmaz
        if (rank < 1 || rank > config.prizeRanks) return;
        foreach (var reward in config.RewardsForRank(rank))
            DailySlotRewardService.Grant(reward);
        Debug.Log($"[BridgeRepair] {rank}. sıra ödülü verildi.");
    }

    // ── Yardımcı ─────────────────────────────────────────────────

    private static DateTime ReadUtc(string key)
    {
        long.TryParse(PlayerPrefs.GetString(key, "0"), out long ticks);
        return ticks <= 0 ? DateTime.MinValue : new DateTime(ticks, DateTimeKind.Utc);
    }

#if UNITY_EDITOR
    public static void DebugAddWin() => HandleLevelCleared();

    public static void DebugClearAll()
    {
        foreach (var k in new[] { KeyCycle, KeyJoined, KeyJoinTicks, KeyLastAsk, KeyRunSeed, KeyWins,
                                  KeyFinishTicks, KeyFinalRank, KeyRewardClaimed, KeyPresentedWins,
                                  KeyPresentedTicks, KeyEndPresented, KeyBotNames, KeyHowToCycle })
            PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// Katılım zamanını geri alır → botlar o kadar ilerlemiş görünür (zaman atlama testi).
    public static void DebugShiftJoin(TimeSpan back)
    {
        if (!HasJoined) return;
        PlayerPrefs.SetString(KeyJoinTicks, (JoinUtc - back).Ticks.ToString());
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
#endif
}
