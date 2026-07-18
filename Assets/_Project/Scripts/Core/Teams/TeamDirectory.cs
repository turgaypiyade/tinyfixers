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
}

/// <summary>
/// Katılınabilir takımlar dizini (Ara sekmesi). v1: NamePool bot evreninden deterministik
/// üretir; arama isim-içerir filtresidir. Gerçek backend gelince aynı API Firestore
/// sorgusuna bağlanır — UI değişmez.
/// </summary>
public static class TeamDirectory
{
    // Liderlik takım havuzlarından (0..100 Dünya, 300.. Türkiye) ayrı aralık.
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
    /// Takım listesi. query boşsa ilk count takım; doluysa isim-içerir eşleşenlerden
    /// en fazla count tanesi. Kapasitesi dolu takımlar da listelenir (Katıl orada kapalı).
    /// </summary>
    public static List<TeamDirectoryEntry> Browse(string query, int count = 20)
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
        };
    }
}
