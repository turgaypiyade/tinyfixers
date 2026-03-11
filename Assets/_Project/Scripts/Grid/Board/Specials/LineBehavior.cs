using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Event hub for line sweep lifecycle notifications.
/// Kept in line behavior model layer to avoid growing BoardController surface.
/// </summary>
public static class LineBehaviorEvents
{
    public static event Action<LightningLineStrike, float> SweepStarted;
    public static event Action<Vector2Int, LightningLineStrike> SweepCellReached;

    public static void EmitSweepStarted(LightningLineStrike strike, float delay)
    {
        SweepStarted?.Invoke(strike, delay);
    }

    public static void EmitSweepCellReached(Vector2Int cell, LightningLineStrike strike)
    {
        SweepCellReached?.Invoke(cell, strike);
    }
}

/// <summary>
/// Base line special behavior with shared line clear logic.
/// Visual playback is kept outside of behavior classes and driven by
/// LineBehaviorEvents + BoardAnimator orchestration.
/// </summary>
public abstract class LineBehaviorBase : ISpecialBehavior, ILightningBehavior
{
    public abstract TileSpecial SpecialType { get; }
    protected abstract bool IsHorizontal { get; }
    public bool HasLineActivation => true;

    public IEnumerable<LightningLineStrike> GetLineStrikes(int originX, int originY)
    {
        yield return new LightningLineStrike(new Vector2Int(originX, originY), IsHorizontal);
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY)
    {
        var cells = new HashSet<Vector2Int>();

        if (IsHorizontal)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (SpecialUtils.CanAffectCell(board, x, originY))
                    cells.Add(new Vector2Int(x, originY));
            }
        }
        else // LineV
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (SpecialUtils.CanAffectCell(board, originX, y))
                    cells.Add(new Vector2Int(originX, y));
            }
        }

        return cells;
    }
}

/// <summary>
/// LineH special: clears a full row from the activation origin.
/// </summary>
public sealed class LineHorizontalBehavior : LineBehaviorBase
{
    public override TileSpecial SpecialType => TileSpecial.LineH;
    protected override bool IsHorizontal => true;
}

/// <summary>
/// LineV special: clears a full column from the activation origin.
/// </summary>
public sealed class LineVerticalBehavior : LineBehaviorBase
{
    public override TileSpecial SpecialType => TileSpecial.LineV;
    protected override bool IsHorizontal => false;
}
