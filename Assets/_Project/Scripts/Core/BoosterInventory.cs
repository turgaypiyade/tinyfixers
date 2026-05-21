using System;
using UnityEngine;

/// <summary>
/// Hammer / Row / Column / Shuffle gibi in-level boosterlar için sayaç deposu.
/// PreLevelSpecialInventory ile aynı pattern: PlayerPrefs tabanlı, static API.
/// </summary>
public static class BoosterInventory
{
    private const int DefaultCount = 0;

    private const string KeyHammer  = "booster_hammer_count";
    private const string KeyRow     = "booster_row_count";
    private const string KeyColumn  = "booster_column_count";
    private const string KeyShuffle = "booster_shuffle_count";

    public static event Action<BoardController.BoosterMode, int> OnCountChanged;

    public static int GetCount(BoardController.BoosterMode mode)
    {
        return PlayerPrefs.GetInt(GetPrefsKey(mode), DefaultCount);
    }

    public static void SetCount(BoardController.BoosterMode mode, int count)
    {
        int safeCount = Mathf.Max(0, count);
        PlayerPrefs.SetInt(GetPrefsKey(mode), safeCount);
        PlayerPrefs.Save();
        OnCountChanged?.Invoke(mode, safeCount);
    }

    public static int Add(BoardController.BoosterMode mode, int amount)
    {
        if (amount <= 0) return GetCount(mode);
        int value = GetCount(mode) + amount;
        SetCount(mode, value);
        return value;
    }

    public static int Spend(BoardController.BoosterMode mode, int amount = 1)
    {
        int value = Mathf.Max(0, GetCount(mode) - Mathf.Max(1, amount));
        SetCount(mode, value);
        return value;
    }

    public static bool HasAny(BoardController.BoosterMode mode) => GetCount(mode) > 0;

    private static string GetPrefsKey(BoardController.BoosterMode mode)
    {
        return mode switch
        {
            BoardController.BoosterMode.Single  => KeyHammer,
            BoardController.BoosterMode.Row     => KeyRow,
            BoardController.BoosterMode.Column  => KeyColumn,
            BoardController.BoosterMode.Shuffle => KeyShuffle,
            _ => "booster_unknown_count"
        };
    }
}
