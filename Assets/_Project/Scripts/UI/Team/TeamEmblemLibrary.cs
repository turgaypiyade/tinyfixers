using System;
using UnityEngine;

/// <summary>
/// Takım amblemi seti (kalkanlar) — Resources/TeamEmblems/TeamEmblem_XX. Sahnede havuzu olmayan
/// yerler (liderlik Takım sekmesi) buradan isim/seed'e göre deterministik amblem alır.
/// </summary>
public static class TeamEmblemLibrary
{
    private static Sprite[] _all;

    public static Sprite[] All
    {
        get
        {
            if (_all == null)
            {
                _all = Resources.LoadAll<Sprite>("TeamEmblems");
                Array.Sort(_all, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            return _all;
        }
    }

    /// <summary>Aynı isim her yerde ve her cihazda aynı amblemi alır (process'ten bağımsız hash).</summary>
    public static Sprite ForName(string name)
    {
        var all = All;
        if (all == null || all.Length == 0) return null;
        int h = 0;
        if (!string.IsNullOrEmpty(name))
            foreach (char c in name) h = h * 31 + c;
        return all[((h % all.Length) + all.Length) % all.Length];
    }
}
