using System.Collections.Generic;

public static class PreLevelSpecialSelectionState
{
    private static readonly List<TileSpecial> selectedSpecials = new();

    public static IReadOnlyList<TileSpecial> SelectedSpecials => selectedSpecials;
    public static bool HasSelection => selectedSpecials.Count > 0;

    public static void SetSelection(IEnumerable<TileSpecial> specials)
    {
        selectedSpecials.Clear();

        if (specials == null)
            return;

        foreach (var special in specials)
        {
            if (special == TileSpecial.None)
                continue;

            selectedSpecials.Add(special);
        }
    }

    public static void Clear()
    {
        selectedSpecials.Clear();
    }
}
