using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PatchBot + PulseCore combo: PatchBot teleports to a random target,
/// then a 3×3 pulse explosion fires from the target location.
/// </summary>
public class PatchBotPulseCombo : IComboBehavior, IComboExecutor
{
    public int Priority => 100;

    public bool Matches(TileSpecial a, TileSpecial b)
    {
        return (IsPatchBot(a) && IsPulse(b)) || (IsPulse(a) && IsPatchBot(b));
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
        TileSpecial specialA, TileSpecial specialB)
    {
        // Target is random — can't pre-calculate
        return new HashSet<Vector2Int> { new Vector2Int(originX, originY) };
    }

    public void Execute(ComboExecutionContext ctx)
    {
        var res = ctx.Resolution;
        var board = ctx.Board;
        var a = ctx.TileA;
        var b = ctx.TileB;
        var sa = ctx.SpecialA;
        var sb = ctx.SpecialB;

        ComboBehaviorEvents.EmitComboTriggered(sa, sb, new Vector2Int(a.X, a.Y));

        var pulseTile = IsPulse(sa) ? a : b;
        var patchBotTile = IsPatchBot(sa) ? a : b;

        var target = ctx.PatchbotService.FindTarget(patchBotTile, pulseTile, null);
        if (target.hasCell)
        {
            var fromCell = new Vector2Int(patchBotTile.X, patchBotTile.Y);
            var toCell = new Vector2Int(target.x, target.y);
            float travelDuration = board.PatchbotDashUI != null
                ? board.PatchbotDashUI.EstimateDashDuration(board, fromCell, toCell)
                : 0.22f;

            ctx.PatchbotService.EnqueueDash(patchBotTile, target.x, target.y);
            ctx.VisualService.PlayTeleportMarkers(patchBotTile, target.x, target.y);
            ctx.VisualService.PlayTeleportMarkers(pulseTile, target.x, target.y);
            ctx.VisualService.PlayTransientSpecialPairVisualAt(patchBotTile, pulseTile, target.x, target.y);

            // Hedefte tile olmasa bile (ör. obstacle hücresi) Pulse patlamasını
            // hedef hücre üzerinde mutlaka göster.
            ctx.VisualService.PlayPulseExplosionAtDelayed(target.x, target.y, travelDuration);
            SpecialCellUtils.AddSquare(res.Affected, res, board, target.x, target.y, 1);
        }
    }

    static bool IsPatchBot(TileSpecial s) => s == TileSpecial.PatchBot;
    static bool IsPulse(TileSpecial s) => s == TileSpecial.PulseCore;
}