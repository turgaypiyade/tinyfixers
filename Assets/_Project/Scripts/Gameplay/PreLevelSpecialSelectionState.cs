using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class PreLevelSpecialSelectionState
{
    private const string PrefsKey = "prelevel_selected_specials";
    private static readonly List<TileSpecial> selectedSpecials = new();
    private static bool loadedFromPrefs;

    public static IReadOnlyList<TileSpecial> SelectedSpecials
    {
        get
        {
            EnsureLoadedFromPrefs();
            return selectedSpecials;
        }
    }

    public static bool HasSelection
    {
        get
        {
            EnsureLoadedFromPrefs();
            return selectedSpecials.Count > 0;
        }
    }

    public static void SetSelection(IEnumerable<TileSpecial> specials)
    {
        loadedFromPrefs = true;
        selectedSpecials.Clear();

        if (specials != null)
        {
            foreach (var special in specials)
            {
                if (special == TileSpecial.None)
                    continue;

                selectedSpecials.Add(special);
            }
        }

        SaveToPrefs();
    }

    public static void Clear()
    {
        loadedFromPrefs = true;
        selectedSpecials.Clear();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    public static List<TileSpecial> GetSelectionSnapshot()
    {
        EnsureLoadedFromPrefs();
        return new List<TileSpecial>(selectedSpecials);
    }

    private static void EnsureLoadedFromPrefs()
    {
        if (loadedFromPrefs)
            return;

        loadedFromPrefs = true;
        selectedSpecials.Clear();

        string raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(raw))
            return;

        string[] tokens = raw.Split(',');
        for (int i = 0; i < tokens.Length; i++)
        {
            if (int.TryParse(tokens[i], out int value) && System.Enum.IsDefined(typeof(TileSpecial), value))
            {
                var special = (TileSpecial)value;
                if (special != TileSpecial.None)
                    selectedSpecials.Add(special);
            }
        }
    }

    private static void SaveToPrefs()
    {
        if (selectedSpecials.Count == 0)
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < selectedSpecials.Count; i++)
        {
            if (i > 0)
                sb.Append(',');

            sb.Append((int)selectedSpecials[i]);
        }

        PlayerPrefs.SetString(PrefsKey, sb.ToString());
        PlayerPrefs.Save();
    }
}
