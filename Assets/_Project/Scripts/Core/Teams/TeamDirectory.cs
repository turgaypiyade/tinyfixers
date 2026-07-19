using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Takım tarayıcıda (Ara sekmesi) listelenen tek takım kaydı.</summary>
public sealed class TeamDirectoryEntry
{
    public string name;
    public int members;
    public int capacity = 50;
    public int emblemSeed;     // amblem havuzu index'i (controller havuz uzunluğuna mod'lar)
    public int minChapter;
    public string description;

    /// <summary>Firestore doküman id'si — GERÇEK takım. null = henüz materialize olmamış bot.</summary>
    public string teamId;

    /// <summary>Bot takımın deterministik dizin seed'i (materialize id'si bundan türer).</summary>
    public int directorySeed = -1;

    public bool IsReal => !string.IsNullOrEmpty(teamId);
}

/// <summary>
/// Katılınabilir takımlar dizini (Ara sekmesi) — GERÇEK Firestore takımları + deterministik
/// bot takımların harmanı (Docs/ProductionPlan.md P3b). Gerçekler önce gelir; bot listesinden
/// aynı isimli/aynı id'li (materialize olmuş) olanlar elenir. Firestore erişilemezse
/// (offline/auth yok) yalnız bot listesi döner — ekran asla boş kalmaz.
/// </summary>
public static class TeamDirectory
{
    // Liderlik takım havuzlarından (0..N Dünya, 300.. Türkiye) ayrı aralık.
    private const int BrowseBase = 600;
    private const int BrowseScan = 400;

    private static readonly string[] DescTemplates =
    {
        "Aktif ve eğlenceli bir takımız, katıl bize!",
        "Her gün oynayanlar için. Can atmayan atılır!",
        "Sakin, yardımsever takım. Herkes davetli.",
        "Hedef: haftalık ilk 10. Ciddi oyuncular aransın.",
        "Yeni kurulmadı ama taze kan arıyoruz.",
    };

    /// <summary>
    /// Takım listesi (async): gerçek takımlar + bot dolgu, en fazla count.
    /// query boş → keşfet listesi; dolu → isim araması (gerçekte prefix, botta içerir).
    /// </summary>
    public static void Browse(string query, int count, Action<List<TeamDirectoryEntry>> callback)
    {
        var bots = BrowseBots(query, count);

        FirebaseTeamCloud.QueryRealTeams(query, count, real =>
        {
            if (real == null || real.Count == 0)
            {
                callback?.Invoke(bots);
                return;
            }

            // Gerçekler önce; bot listesinden çakışanları ele (materialize id veya isim).
            var takenIds = new HashSet<string>();
            var takenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var merged = new List<TeamDirectoryEntry>();

            foreach (var r in real)
            {
                merged.Add(r);
                takenIds.Add(r.teamId);
                takenNames.Add(r.name);
            }

            foreach (var b in bots)
            {
                if (merged.Count >= count) break;
                if (takenIds.Contains(FirebaseTeamCloud.BotTeamId(b.directorySeed))) continue;
                if (takenNames.Contains(b.name)) continue;
                merged.Add(b);
            }

            callback?.Invoke(merged);
        });
    }

    private static List<TeamDirectoryEntry> BrowseBots(string query, int count)
    {
        bool filtered = !string.IsNullOrWhiteSpace(query);
        string q = filtered ? query.Trim() : null;

        var list = new List<TeamDirectoryEntry>();
        for (int i = 0; i < BrowseScan && list.Count < count; i++)
        {
            string name = NamePool.TeamAt(BrowseBase + i);
            if (filtered && name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
            list.Add(EntryFor(name, BrowseBase + i));
        }
        return list;
    }

    private static TeamDirectoryEntry EntryFor(string name, int seed)
    {
        int hash = Mathf.Abs(name.GetHashCode());
        return new TeamDirectoryEntry
        {
            name = name,
            members = Mathf.Clamp(BotProgression.TeamMembers(seed), 4, 49),   // hep katılınabilir
            capacity = 50,
            emblemSeed = hash,
            minChapter = (hash / 7) % 3 == 0 ? ((hash / 11) % 6) * 10 : 0,    // çoğu 0, bazıları 10-50
            description = DescTemplates[hash % DescTemplates.Length],
            teamId = null,
            directorySeed = seed,
        };
    }
}
