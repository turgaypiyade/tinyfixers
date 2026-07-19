using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources/SeedData altındaki büyük isim listelerini (10K oyuncu, ~1K takım) yükler ve
/// benzersiz sırayla dağıtır. Havuz tükenirse numara ekleyerek benzersizliği sürdürür.
/// BotNameGenerator ve TeamManager buradan besleniyor; liste yoksa onların kendi
/// prosedürel üretimine düşülür.
/// </summary>
public static class NamePool
{
    private static List<string> players;
    private static List<string> teams;
    private static int playerCursor;
    private static int teamCursor;

    public static bool HasPlayers { get { EnsureLoaded(); return players.Count > 0; } }
    public static bool HasTeams   { get { EnsureLoaded(); return teams.Count > 0; } }

    private static void EnsureLoaded()
    {
        if (players != null) return;
        players = Load("SeedData/player_names");
        teams   = Load("SeedData/team_names");
        Shuffle(players);
        Shuffle(teams);
    }

    private static List<string> Load(string resourcePath)
    {
        var list = new List<string>();
        var asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[NamePool] Bulunamadı: Resources/{resourcePath}");
            return list;
        }
        foreach (var line in asset.text.Split('\n'))
        {
            string t = line.Trim();
            if (t.Length > 0) list.Add(t);
        }
        return list;
    }

    public static string NextPlayerName()
    {
        EnsureLoaded();
        if (players.Count == 0) return "Player" + Random.Range(1, 99999);
        return Take(players, ref playerCursor, joiner: "");
    }

    public static string NextTeamName()
    {
        EnsureLoaded();
        if (teams.Count == 0) return "Team" + Random.Range(1, 9999);
        return Take(teams, ref teamCursor, joiner: " ");
    }

    // Havuzu bir tur dolaşınca aynı ismi "isim 2", "isim 3" diye benzersiz kılar.
    private static string Take(List<string> list, ref int cursor, string joiner)
    {
        int loop = cursor / list.Count;
        string name = list[cursor % list.Count];
        cursor++;
        return loop == 0 ? name : name + joiner + (loop + 1);
    }

    /// <summary>
    /// Index'e göre SABİT oyuncu ismi (bot i her zaman aynı isim). Havuz boyu aşılırsa
    /// (15k bot / 10k isim) sarımda numara eklenir → aynı listede birebir kopya isim olmaz.
    /// </summary>
    public static string PlayerAt(int index)
    {
        EnsureLoaded();
        if (players.Count == 0) return "Oyuncu" + index;
        int normalized = ((index % players.Count) + players.Count) % players.Count;
        int loop = index >= 0 ? index / players.Count : 0;
        string name = players[normalized];
        return loop == 0 ? name : name + (loop + 1);
    }

    /// <summary>Index'e göre SABİT takım ismi (sarımda numaralı — bkz. PlayerAt).</summary>
    public static string TeamAt(int index)
    {
        EnsureLoaded();
        if (teams.Count == 0) return "Takim" + index;
        int normalized = ((index % teams.Count) + teams.Count) % teams.Count;
        int loop = index >= 0 ? index / teams.Count : 0;
        string name = teams[normalized];
        return loop == 0 ? name : name + " " + (loop + 1);
    }

    /// <summary>Bot havuzu yeniden üretilirken çağrılır (baştan benzersiz dağıtım).</summary>
    public static void ResetCursors()
    {
        playerCursor = 0;
        teamCursor = 0;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
