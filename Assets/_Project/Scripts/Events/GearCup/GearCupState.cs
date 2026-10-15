using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GearCupWin
{
    public string id;
    public int amount, level;
    public long wonTicks;
}

[Serializable]
public sealed class GearCupSave
{
    public string season = "", teamId = "", teamName = "", accountId = "";
    public int emblem, group = -1, slot = -1, personal, acknowledged;
    public long startTicks, endTicks;
    public bool joined, coinDelivered, itemDelivered, resultPresented;
    public int settledRank, rewardCoins, rewardItem = 99, rewardAmount;
    public List<GearCupWin> outbox = new List<GearCupWin>();
}

/// <summary>One persistent season record; all per-attempt accumulation stays in memory until a win.</summary>
public static class GearCupState
{
    public const string SaveKey = "gearcup_state_v1";
    private static GearCupSave data;
    private static int pending;
    private static bool tracking;
    private static string attemptId;
    public static event Action Changed;
    public static event Action<TileType, int> Gained;
    public static GearCupSave Data => data ?? (data = Load());
    public static int Pending => pending;
    public static bool Tracking => tracking;
    public static bool HasJoined => Data.joined;
    public static DateTime Start => new DateTime(Data.startTicks, DateTimeKind.Utc);
    public static DateTime End => new DateTime(Data.endTicks, DateTimeKind.Utc);
    public static bool IsUnlocked => GearCupPreviewConfig.Shared != null &&
        CurrentLevel.Global >= GearCupPreviewConfig.Shared.minLevelGate;
    public static bool CanEarn => HasJoined && IsUnlocked && GearCupPreviewConfig.Shared.eventEnabled &&
        PlayerTeamState.HasTeam && PlayerTeamState.TeamId == Data.teamId && DateTime.UtcNow < End;

    public static void Initialize()
    {
        LevelAttemptStats.OnAttemptStarted -= Begin;
        LevelAttemptStats.OnAttemptEnded -= Finish;
        GameEventBus.OnTileCleared -= Clear;
        FirebaseCloudSaveService.OnRestored -= Reload;
        LevelAttemptStats.OnAttemptStarted += Begin;
        LevelAttemptStats.OnAttemptEnded += Finish;
        GameEventBus.OnTileCleared += Clear;
        FirebaseCloudSaveService.OnRestored += Reload;
        LevelLossRegistry.Register("gearcup", Loss);
    }

    public static void Shutdown()
    {
        LevelAttemptStats.OnAttemptStarted -= Begin;
        LevelAttemptStats.OnAttemptEnded -= Finish;
        GameEventBus.OnTileCleared -= Clear;
        FirebaseCloudSaveService.OnRestored -= Reload;
        LevelLossRegistry.Unregister("gearcup");
        tracking = false; pending = 0; data = null;
    }

    private static GearCupSave Load()
    {
        try
        {
            var loaded = JsonUtility.FromJson<GearCupSave>(PlayerPrefs.GetString(SaveKey, "")) ?? new GearCupSave();
            if (loaded.outbox == null) loaded.outbox = new List<GearCupWin>();
            return loaded;
        }
        catch (ArgumentException) { return new GearCupSave(); }
    }
    private static void Reload()
    {
        var before = data;
        data = Load();
        if (before != null && before.season == data.season && before.teamId == data.teamId &&
            before.accountId == data.accountId)
        {
            foreach (var win in before.outbox)
                if (!data.outbox.Exists(x => x.id == win.id)) data.outbox.Add(win);
            data.personal = Math.Max(data.personal, before.personal);
            if (before.settledRank > 0 && data.settledRank == 0)
            {
                data.settledRank = before.settledRank; data.rewardCoins = before.rewardCoins;
                data.rewardItem = before.rewardItem; data.rewardAmount = before.rewardAmount;
            }
            data.coinDelivered |= before.coinDelivered;
            data.itemDelivered |= before.itemDelivered;
            data.resultPresented |= before.resultPresented;
        }
        Changed?.Invoke();
    }
    public static void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
        FirebaseCloudSaveService.MarkDirty();
        Changed?.Invoke();
    }
    public static bool TryJoin(out string message)
    {
        message = "";
        var cfg = GearCupPreviewConfig.Shared;
        if (cfg == null || !cfg.eventEnabled || !IsUnlocked)
        { message = GameLocalization.GetFormat("gearcup_unlock", cfg != null ? cfg.minLevelGate : 51); return false; }
        if (!PlayerTeamState.HasTeam || string.IsNullOrEmpty(PlayerTeamState.TeamId))
        { message = GameLocalization.GetOr("gearcup_need_team", "Join a team first to enter Gear Cup."); return false; }
        if (HasJoined && Data.teamId != PlayerTeamState.TeamId && DateTime.UtcNow < End)
        { message = GameLocalization.GetOr("gearcup_team_locked", "Your team is fixed for this event."); return false; }
        if (HasJoined && DateTime.UtcNow < End) return true;
        // Do not overwrite an unfinished claim or offline score delivery from the previous season.
        if (HasJoined && (Data.outbox.Count > 0 || Data.settledRank == 0 || !Data.coinDelivered || !Data.itemDelivered))
        { message = GameLocalization.GetOr("gearcup_settling", "Your event result is being updated. Please try again shortly."); GearCupRuntime.RequestSync(); return false; }
        DateTime start = GearCupRules.SeasonStart(DateTime.UtcNow, cfg.seasonHours);
        data = new GearCupSave { joined = true, season = GearCupRules.SeasonKey(start), startTicks = start.Ticks,
            endTicks = start.AddHours(cfg.seasonHours).Ticks, teamId = PlayerTeamState.TeamId,
            teamName = PlayerTeamState.TeamName, emblem = PlayerTeamState.EmblemIndex,
            accountId = FirebaseAuthService.IsReady ? FirebaseAuthService.UserId : "" };
        Save();
        GearCupRuntime.RequestSync();
        return true;
    }
    private static void Begin(int level, int attempt)
    {
        pending = 0;
        attemptId = Guid.NewGuid().ToString("N");
        tracking = CanEarn && !RuntimeSimulationSession.IsActive;
        Changed?.Invoke();
    }
    private static void Clear(TileType type, int count)
    {
        var cfg = GearCupPreviewConfig.Shared;
        if (!tracking || !CanEarn || count <= 0 || !GearCupRules.Counts(type, cfg.onlyYellowTiles)) return;
        int gained = (int)Math.Min((long)count * Math.Max(1, cfg.gearsPerTile), Math.Max(0, cfg.maxGearsPerLevel - pending));
        if (gained <= 0) return;
        pending += gained;
        Gained?.Invoke(type, gained);
    }
    private static void Finish(LevelAttemptResult result)
    {
        if (!tracking) return;
        tracking = false;
        if (result.Won && pending > 0 && DateTime.UtcNow <= End)
        {
            Data.outbox.Add(new GearCupWin { id = attemptId, level = result.Level, amount = pending, wonTicks = DateTime.UtcNow.Ticks });
            Data.personal += pending;
            pending = 0;
            Save();
            GearCupRuntime.RequestSync();
        }
        else { pending = 0; Changed?.Invoke(); }
    }
    private static IEnumerable<LevelLossItem> Loss()
    {
        if (tracking && pending > 0)
            yield return new LevelLossItem(GearCupPreviewConfig.Shared.gear,
                GameLocalization.GetOr("gearcup_gears", "Gears"), pending, true);
    }
}
