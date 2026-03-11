using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PatchBot special: teleports to a random target cell and hits it.
/// The affected area is just the target cell (1 cell).
/// </summary>
public class PatchBotBehavior : ISpecialBehavior
{
    public TileSpecial SpecialType => TileSpecial.PatchBot;

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY)
    {
        var cells = new HashSet<Vector2Int>();

        // PatchBot finds a random target cell.
        // Since it relies on PatchbotComboService for target selection,
        // we return just the origin — the actual target is resolved at activation time.
        // The chain system will handle triggering specials hit by the PatchBot.
        cells.Add(new Vector2Int(originX, originY));

        return cells;
    }

    public BoardAction CreateVisualAction(BoardController board, int originX, int originY,
                                           HashSet<Vector2Int> affectedCells)
    {
        // PatchBot VFX: dash animation. Handled by PatchbotComboService.EnqueueDash.
        return null;
    }
}
