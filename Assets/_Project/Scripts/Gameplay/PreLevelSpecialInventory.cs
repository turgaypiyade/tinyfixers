using UnityEngine;

public static class PreLevelSpecialInventory
{
    private const int DefaultCount = 10;
    private const string LineHKey = "prelevel_special_lineh_count";
    private const string PulseCoreKey = "prelevel_special_pulsecore_count";
    private const string OverrideKey = "prelevel_special_override_count";

    public static int GetCount(TileSpecial special)
    {
        return PlayerPrefs.GetInt(GetPrefsKey(special), DefaultCount);
    }

    public static void SetCount(TileSpecial special, int count)
    {
        PlayerPrefs.SetInt(GetPrefsKey(special), count);
    }

    public static int Add(TileSpecial special, int amount)
    {
        int value = GetCount(special) + amount;
        SetCount(special, value);
        return value;
    }

    public static int Spend(TileSpecial special, int amount = 1)
    {
        int value = GetCount(special) - Mathf.Max(0, amount);
        SetCount(special, value);
        return value;
    }

    private static string GetPrefsKey(TileSpecial special)
    {
        return special switch
        {
            TileSpecial.LineH => LineHKey,
            TileSpecial.PulseCore => PulseCoreKey,
            TileSpecial.SystemOverride => OverrideKey,
            _ => "prelevel_special_unknown_count"
        };
    }
}
