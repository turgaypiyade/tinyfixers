using System.Collections.Generic;
using UnityEngine;

public class SpecialBehaviorRegistry
{
    private readonly Dictionary<TileSpecial, ISpecialBehavior> behaviors = new();
    private readonly List<IComboBehavior> combos = new();

    public SpecialBehaviorRegistry()
    {
        Register(new SystemOverrideBehavior());

        RegisterCombo(new PatchBotPulseCombo());
    }

    public void Register(ISpecialBehavior behavior)
    {
        behaviors[behavior.SpecialType] = behavior;
    }

    public void RegisterCombo(IComboBehavior combo)
    {
        combos.Add(combo);
    }

    public ISpecialBehavior Get(TileSpecial special)
    {
        behaviors.TryGetValue(special, out var behavior);
        return behavior;
    }

    public IComboBehavior FindCombo(TileSpecial a, TileSpecial b)
    {
        IComboBehavior best = null;
        int bestPriority = int.MinValue;

        foreach (var combo in combos)
        {
            if (!combo.Matches(a, b))
                continue;

            if (combo.Priority > bestPriority)
            {
                best = combo;
                bestPriority = combo.Priority;
            }
        }

        return best;
    }

    public HashSet<Vector2Int> CalculateComboEffect(
        TileSpecial a,
        TileSpecial b,
        BoardController board,
        int originX,
        int originY)
    {
        var combo = FindCombo(a, b);
        if (combo == null) return null;
        return combo.CalculateAffectedCells(board, originX, originY, a, b);
    }

    public HashSet<Vector2Int> CalculateEffect(TileSpecial special, BoardController board, int x, int y)
    {
        var behavior = Get(special);
        if (behavior == null)
            return new HashSet<Vector2Int>();

        return behavior.CalculateAffectedCells(board, x, y);
    }

    public bool HasBehavior(TileSpecial special)
    {
        return special != TileSpecial.None && behaviors.ContainsKey(special);
    }
}
