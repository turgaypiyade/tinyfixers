using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;

public sealed class GearCupTeamScore
{
    public string id, name;
    public int score, slot, emblem;
    public bool bot;
}

public sealed class GearCupEnrollment
{
    public int group, slot, personal;
    public string teamId;
}

public sealed class GearCupClaim
{
    public int rank, coins, item, amount;
}

/// <summary>Bounded 5-team groups; transactional attempt receipts update member/team/slot totals together.</summary>
public static class GearCupCloud
{
    private static DocumentReference Season(string season) => FirebaseFirestore.DefaultInstance.Collection("gearCupSeasons").Document(season);
    private static DocumentReference Slot(DocumentReference season, int group, int slot) =>
        season.Collection("groups").Document(group.ToString()).Collection("slots").Document(slot.ToString());
    private static int Number(DocumentSnapshot doc, string field) => doc.ContainsField(field) ? (int)doc.GetValue<long>(field) : 0;

    public static Task<GearCupEnrollment> Enroll(GearCupSave save, string uid, string playerName, bool existingOnly = false)
    {
        var root = Season(save.season);
        var player = root.Collection("players").Document(uid);
        string teamId = save.teamId, teamName = save.teamName;
        int emblem = save.emblem;
        var start = Timestamp.FromDateTime(new DateTime(save.startTicks, DateTimeKind.Utc));
        var end = Timestamp.FromDateTime(new DateTime(save.endTicks, DateTimeKind.Utc));
        return FirebaseFirestore.DefaultInstance.RunTransactionAsync(async tx =>
        {
            var member = await tx.GetSnapshotAsync(player);
            if (member.Exists)
                return new GearCupEnrollment { group = Number(member, "group"), slot = Number(member, "slot"),
                    personal = Number(member, "score"), teamId = member.GetValue<string>("teamId") };
            if (existingOnly) return null;
            var teamRef = root.Collection("teams").Document(teamId);
            var team = await tx.GetSnapshotAsync(teamRef);
            int group, slot;
            if (team.Exists) { group = Number(team, "group"); slot = Number(team, "slot"); }
            else
            {
                var allocator = await tx.GetSnapshotAsync(root);
                int next = allocator.Exists ? Number(allocator, "nextSlot") : 0;
                group = next / 5; slot = next % 5;
                var identity = new Dictionary<string, object> { { "teamId", teamId }, { "name", teamName },
                    { "emblem", emblem }, { "group", group }, { "slot", slot }, { "score", 0L },
                    { "startAt", start }, { "endAt", end } };
                tx.Set(root, new Dictionary<string, object> { { "nextSlot", next + 1L }, { "lastTeamId", teamId } });
                tx.Set(teamRef, identity);
                tx.Set(Slot(root, group, slot), identity);
            }
            tx.Set(player, new Dictionary<string, object> { { "teamId", teamId }, { "name", playerName },
                { "group", group }, { "slot", slot }, { "score", 0L }, { "startAt", start }, { "endAt", end } });
            return new GearCupEnrollment { group = group, slot = slot, teamId = teamId };
        });
    }

    public static Task<int> Submit(string season, string teamId, int group, int slot, string uid, GearCupWin win)
    {
        var root = Season(season);
        var receipt = root.Collection("wins").Document(uid + "_" + win.id);
        var player = root.Collection("players").Document(uid);
        var team = root.Collection("teams").Document(teamId);
        var slotRef = Slot(root, group, slot);
        int amount = win.amount, level = win.level;
        var wonAt = Timestamp.FromDateTime(new DateTime(win.wonTicks, DateTimeKind.Utc));
        return FirebaseFirestore.DefaultInstance.RunTransactionAsync(async tx =>
        {
            var existing = await tx.GetSnapshotAsync(receipt);
            var member = await tx.GetSnapshotAsync(player);
            if (existing.Exists) return Number(member, "score");
            var teamScore = await tx.GetSnapshotAsync(team);
            var slotScore = await tx.GetSnapshotAsync(slotRef);
            if (!member.Exists || !teamScore.Exists || !slotScore.Exists || member.GetValue<string>("teamId") != teamId)
                throw new InvalidOperationException("Gear Cup enrollment mismatch");
            int personal = checked(Number(member, "score") + amount);
            int total = checked(Number(teamScore, "score") + amount);
            var fields = new Dictionary<string, object> { { "score", total }, { "lastReceipt", receipt.Id } };
            tx.Update(team, fields);
            tx.Update(slotRef, fields);
            tx.Update(player, new Dictionary<string, object> { { "score", personal }, { "lastReceipt", receipt.Id } });
            tx.Set(receipt, new Dictionary<string, object> { { "uid", uid }, { "teamId", teamId }, { "amount", amount },
                { "level", level }, { "wonAt", wonAt }, { "createdAt", FieldValue.ServerTimestamp } });
            return personal;
        });
    }

    public static async Task<List<GearCupTeamScore>> Scores(string season, int group)
    {
        var snapshots = await Season(season).Collection("groups").Document(group.ToString())
            .Collection("slots").Limit(5).GetSnapshotAsync(Source.Server);
        var result = new List<GearCupTeamScore>(5);
        foreach (var doc in snapshots.Documents)
            result.Add(new GearCupTeamScore { id = doc.GetValue<string>("teamId"), name = doc.GetValue<string>("name"),
                score = Number(doc, "score"), emblem = Number(doc, "emblem"), slot = Number(doc, "slot") });
        return result;
    }

    public static async Task<int> Personal(string season, string uid)
    {
        var player = await Season(season).Collection("players").Document(uid).GetSnapshotAsync(Source.Server);
        return player.Exists ? Number(player, "score") : 0;
    }

    public static Task<GearCupClaim> Claim(GearCupSave save, string uid, GearCupPreviewConfig cfg)
    {
        var root = Season(save.season);
        var claim = root.Collection("claims").Document(uid);
        var player = root.Collection("players").Document(uid);
        int group = save.group, ownSlot = save.slot;
        int[] coins = new int[3], items = new int[3], amounts = new int[3];
        for (int i = 0; i < 3; i++) { coins[i] = cfg.rewards[i].coins; items[i] = (int)cfg.rewards[i].item; amounts[i] = cfg.rewards[i].itemAmount; }
        return FirebaseFirestore.DefaultInstance.RunTransactionAsync(async tx =>
        {
            var existing = await tx.GetSnapshotAsync(claim);
            if (existing.Exists) return new GearCupClaim { rank = Number(existing, "rank"), coins = Number(existing, "coins"),
                item = Number(existing, "item"), amount = Number(existing, "amount") };
            var member = await tx.GetSnapshotAsync(player);
            var slots = new DocumentSnapshot[5];
            for (int i = 0; i < 5; i++) slots[i] = await tx.GetSnapshotAsync(Slot(root, group, i));
            int ownScore = Number(slots[ownSlot], "score"), rank = 1;
            for (int i = 0; i < 5; i++)
            {
                int score = slots[i].Exists ? Number(slots[i], "score") : GearCupRules.BotFinalScore(group, i);
                if (GearCupRules.RanksBefore(score, i, ownScore, ownSlot)) rank++;
            }
            bool eligible = Number(member, "score") > 0 && rank <= 3;
            var result = new GearCupClaim { rank = rank, coins = eligible ? coins[rank - 1] : 0,
                item = eligible ? items[rank - 1] : 99, amount = eligible ? amounts[rank - 1] : 0 };
            tx.Set(claim, new Dictionary<string, object> { { "rank", rank }, { "coins", result.coins },
                { "item", result.item }, { "amount", result.amount }, { "createdAt", FieldValue.ServerTimestamp } });
            return result;
        });
    }

    public static async Task<List<string>> Members(string season, string teamId)
    {
        var docs = await Season(season).Collection("players").WhereEqualTo("teamId", teamId).Limit(50).GetSnapshotAsync();
        var rows = new List<string>(50);
        foreach (var doc in docs.Documents) rows.Add(doc.GetValue<string>("name") + "   " + Number(doc, "score").ToString("N0"));
        return rows;
    }
}
