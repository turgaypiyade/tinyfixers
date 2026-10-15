using System;
using System.Collections.Generic;
using Firebase.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One app coordinator. Network work is throttled and never occurs per tile or per frame.</summary>
public sealed class GearCupRuntime : MonoBehaviour
{
    private static GearCupRuntime instance;
    private readonly List<GearCupTeamScore> realTeams = new List<GearCupTeamScore>(5);
    private readonly List<GearCupTeamScore> rows = new List<GearCupTeamScore>(5);
    private bool busy, visible;
    private float nextWork;
    private int failures;
    private GearCupSave enrolledSave, scoreSave;
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive || instance != null) return;
        var go = new GameObject("GearCupRuntime");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GearCupRuntime>();
        GearCupState.Initialize();
        SceneManager.sceneLoaded += instance.SceneLoaded;
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "01_Game") GearCupBoardFx.Install();
        RequestSync();
    }
    public static void RequestSync() { if (instance != null) instance.nextWork = 0; }
    public static void SetVisible(bool value)
    {
        if (instance == null) return;
        instance.visible = value;
        if (value) RequestSync();
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (instance == this) { instance = null; GearCupState.Shutdown(); }
    }
    private void Update()
    {
        if (busy || Time.unscaledTime < nextWork) return;
        nextWork = Time.unscaledTime + 1f;
        var cfg = GearCupPreviewConfig.Shared;
        var save = GearCupState.Data;
        if (cfg == null || !cfg.eventEnabled || !save.joined || !FirebaseAuthService.IsReady || !FirebaseCloudSaveService.RestoreResolved) return;
        string uid = FirebaseAuthService.UserId;
        if (!string.IsNullOrEmpty(save.accountId) && save.accountId != uid) return;
        save.accountId = uid;
        bool settledWindow = DateTime.UtcNow >= GearCupState.End.AddMinutes(cfg.settlementMinutes);
        // Reconcile server identity/totals once after startup, restore or a new season.
        // Persisted group/score fields can come from an older cloud-save snapshot.
        if (!ReferenceEquals(enrolledSave, save))
        {
            busy = true;
            GearCupCloud.Enroll(save, uid, PlayerProfile.PlayerName, settledWindow).ContinueWithOnMainThread(task =>
            {
                if (!Current(save, uid)) return;
                busy = false;
                if (task.IsFaulted || task.IsCanceled) { Retry(task.Exception); return; }
                var enrolled = task.Result;
                if (enrolled == null)
                {
                    // No enrollment ever reached the server; do not create an expired race entry.
                    save.outbox.Clear(); save.personal = save.acknowledged = 0; save.settledRank = 5;
                    save.group = save.slot = -1;
                    save.coinDelivered = save.itemDelivered = true;
                }
                else
                {
                    if (enrolled.teamId != save.teamId) { Retry(new InvalidOperationException("Team locked to earlier enrollment")); return; }
                    save.group = enrolled.group; save.slot = enrolled.slot; save.acknowledged = enrolled.personal;
                    save.personal = Math.Max(save.personal, enrolled.personal);
                }
                enrolledSave = save; scoreSave = null; realTeams.Clear(); failures = 0;
                GearCupState.Save(); nextWork = 0;
            });
            return;
        }
        if (save.group < 0) return;
        if (settledWindow && save.outbox.Count > 0)
        {
            // Resolve acknowledged-but-not-locally-confirmed receipts before closing the outbox.
            // Late writes are disallowed once final ranks/rewards can be claimed.
            busy = true;
            GearCupCloud.Personal(save.season, uid).ContinueWithOnMainThread(task =>
            {
                if (!Current(save, uid)) return;
                busy = false;
                if (task.IsFaulted || task.IsCanceled) { Retry(task.Exception); return; }
                save.outbox.Clear(); save.personal = save.acknowledged = task.Result;
                GearCupState.Save(); nextWork = 0;
            });
            return;
        }
        if (save.outbox.Count > 0)
        {
            var win = save.outbox[0];
            busy = true;
            GearCupCloud.Submit(save.season, save.teamId, save.group, save.slot, uid, win).ContinueWithOnMainThread(task =>
            {
                if (!Current(save, uid)) return;
                busy = false;
                if (task.IsFaulted || task.IsCanceled) { Retry(task.Exception); return; }
                int delta = Math.Max(0, task.Result - save.acknowledged);
                if (ReferenceEquals(scoreSave, save))
                    foreach (var row in realTeams) if (row.id == save.teamId) row.score += delta;
                save.outbox.RemoveAt(0); save.acknowledged = task.Result;
                save.personal = save.acknowledged;
                foreach (var pending in save.outbox) save.personal += pending.amount;
                failures = 0; GearCupState.Save(); nextWork = 0;
            });
        }
        else if (settledWindow && save.settledRank == 0)
        {
            busy = true;
            GearCupCloud.Claim(save, uid, cfg).ContinueWithOnMainThread(task =>
            {
                if (!Current(save, uid)) return;
                busy = false;
                if (task.IsFaulted || task.IsCanceled) { Retry(task.Exception); return; }
                save.settledRank = task.Result.rank; save.rewardCoins = task.Result.coins;
                save.rewardItem = task.Result.item; save.rewardAmount = task.Result.amount;
                GearCupState.Save(); GearCupRewardDelivery.Deliver(); Changed?.Invoke(); nextWork = 0;
            });
        }
        else if (save.settledRank > 0 && (!save.coinDelivered || !save.itemDelivered)) GearCupRewardDelivery.Deliver();
        else if (visible)
        {
            busy = true; nextWork = Time.unscaledTime + 30f;
            GearCupCloud.Scores(save.season, save.group).ContinueWithOnMainThread(task =>
            {
                if (!Current(save, uid)) return;
                busy = false;
                if (task.IsFaulted || task.IsCanceled) { Retry(task.Exception); return; }
                realTeams.Clear(); realTeams.AddRange(task.Result); scoreSave = save; failures = 0; Changed?.Invoke();
            });
        }
    }
    private bool Current(GearCupSave save, string uid)
    {
        if (this == null) return false;
        if (!ReferenceEquals(save, GearCupState.Data) || FirebaseAuthService.UserId != uid)
        { busy = false; nextWork = 0; realTeams.Clear(); scoreSave = null; enrolledSave = null; return false; }
        return true;
    }
    private void Retry(Exception error)
    {
        failures = Math.Min(6, failures + 1);
        nextWork = Time.unscaledTime + Math.Min(120, 5 * (1 << failures));
        if (failures == 1) Debug.LogWarning("[GearCup] Sync pending: " + error?.GetBaseException().Message);
    }
    public static IReadOnlyList<GearCupTeamScore> Standings()
    {
        if (instance == null) return Array.Empty<GearCupTeamScore>();
        var save = GearCupState.Data;
        instance.rows.Clear();
        for (int slot = 0; slot < 5; slot++)
        {
            GearCupTeamScore row = null;
            if (ReferenceEquals(instance.scoreSave, save))
                foreach (var real in instance.realTeams) if (real.slot == slot && save.group >= 0) { row = real; break; }
            bool own = slot == Math.Max(0, save.slot);
            if (row == null && own)
                row = new GearCupTeamScore { id = save.teamId, name = save.teamName, slot = slot, emblem = save.emblem, score = save.personal };
            else if (row == null)
                row = new GearCupTeamScore { id = "bot_" + slot, name = NamePool.TeamAt(800 + Math.Max(0, save.group) * 5 + slot),
                    slot = slot, emblem = slot, bot = true,
                    score = GearCupRules.BotScore(Math.Max(0, save.group), slot, GearCupState.Start, GearCupState.End, DateTime.UtcNow) };
            else if (row.id == save.teamId)
                row = new GearCupTeamScore { id = row.id, name = row.name, slot = slot, emblem = row.emblem,
                    score = row.score + Math.Max(0, save.personal - save.acknowledged) };
            instance.rows.Add(row);
        }
        instance.rows.Sort(Compare);
        return instance.rows;
    }
    private static int Compare(GearCupTeamScore a, GearCupTeamScore b) => a.score != b.score ? b.score.CompareTo(a.score) : a.slot.CompareTo(b.slot);
}
