using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>Device-local ownership and recovery copy. Contains game data, never auth tokens.</summary>
public static class LocalAccountData
{
    private const string PendingKey = "account_switch_pending";
    public static bool IsSwitchPending => PlayerPrefs.GetInt(PendingKey, 0) == 1;
    private const string OwnerKey = "local_save_owner_uid";
    public static string Owner => PlayerPrefs.GetString(OwnerKey, "");
    public static bool NeedsReplacement(string uid) => !string.IsNullOrEmpty(uid)
        && !string.IsNullOrEmpty(Owner) && Owner != uid;

    public static void SetOwner(string uid)
    {
        PlayerPrefs.SetString(OwnerKey, uid);
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
    }

    public static void PrepareSwitch(string uid)
    {
        var snapshot = new Snapshot { owner = uid, savedUtc = DateTime.UtcNow.ToString("O") };
        foreach (var item in CloudSaveManifest.Collect())
            snapshot.entries.Add(new Entry { key = item.Key, isString = item.Value is string,
                text = item.Value is string s ? s : null,
                number = item.Value is long l ? l : item.Value is int n ? n : 0 });
        // Preserve the source account's pending telemetry independently of its cloud save.
        snapshot.telemetry = PlayerPrefs.GetString("level_telemetry_outbox_v2", "");
        string folder = Path.Combine(Application.persistentDataPath, "account-recovery");
        Directory.CreateDirectory(folder);
        string filename;
        using (var sha = SHA256.Create())
            filename = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(uid))).Replace("-", "");
        string path = Path.Combine(folder, filename + ".json");
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonUtility.ToJson(snapshot));
        File.Copy(temp, path, true);
        File.Delete(temp);
        SetOwner(uid);
        PlayerPrefs.SetInt(PendingKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Different UID: replace, never merge the old wallet, progress or friend list.</summary>
    public static void ReplaceWith(IDictionary<string, object> incoming)
    {
        // Payment fulfilment receipts remain device-wide to prevent delivering a purchase twice.
        foreach (var entry in CloudSaveManifest.Collect())
            if (entry.Key != "iap_processed_transactions") PlayerPrefs.DeleteKey(entry.Key);
        foreach (var key in new[] { "pending_star_reward", "pending_star_before", "pending_star_after",
            "pending_coin_reward", "pending_coin_before", "pending_coin_after", "stats_first_launch_ticks",
            "gearcup_join_asked_v1" }) PlayerPrefs.DeleteKey(key);
        CurrencyLedger.ResetForAccount();
        PlayerPrefs.SetInt("current_level", 1);
        PlayerPrefs.SetInt("player_total_score", 0);
        PlayerPrefs.SetInt("wonder_selected_background", -1);
        PlayerPrefs.SetInt("wonder_last_completed", -1);
        PlayerPrefs.SetInt("wonder_model_v2", 2);
        // Do not carry ad/purchase evidence across account identities.
        string deviceReceipts = PlayerPrefs.GetString("iap_processed_transactions", "");
        CloudSaveManifest.Apply(incoming);
        var receipts = new HashSet<string>(deviceReceipts.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
        receipts.UnionWith(PlayerPrefs.GetString("iap_processed_transactions", "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
        if (receipts.Count > 0) PlayerPrefs.SetString("iap_processed_transactions", string.Join("|", receipts));
        FriendState.ReloadFromPrefs();
        LivesManager.ReloadFromPrefs();
        BackendServices.ResetTeam();
    }

    [Serializable] private sealed class Snapshot
    {
        public string owner, savedUtc, telemetry;
        public List<Entry> entries = new List<Entry>();
    }
    [Serializable] private sealed class Entry
    {
        public string key, text;
        public bool isString;
        public long number;
    }
}
