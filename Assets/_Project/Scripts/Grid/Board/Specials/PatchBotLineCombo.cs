using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PatchBot + Line combo: PatchBot teleports to a random target,
/// then the Line effect fires from the target location.
/// </summary>
public class PatchBotLineCombo : IComboBehavior, IComboExecutor, ILightningComboBehavior
{
    public int Priority => 150;
    public bool HasLineActivation => true;

    // Cached from last Execute for GetLineStrikes
    private int lastTargetX, lastTargetY;
    private TileSpecial lastLineSpecial;
    private bool lastHadTarget;
    private float lastStartDelaySeconds;

    public bool Matches(TileSpecial a, TileSpecial b)
    {
        return (IsLine(a) && IsPatchBot(b)) || (IsLine(b) && IsPatchBot(a));
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
        TileSpecial specialA, TileSpecial specialB)
    {
        // Target is random — return origin as placeholder
        return new HashSet<Vector2Int> { new Vector2Int(originX, originY) };
    }

    public IEnumerable<LightningLineStrike> GetLineStrikes(int originX, int originY, TileSpecial a, TileSpecial b)
    {
        if (!lastHadTarget) yield break;

        bool isHorizontal = lastLineSpecial == TileSpecial.LineH;
        yield return new LightningLineStrike(new Vector2Int(lastTargetX, lastTargetY), isHorizontal, lastStartDelaySeconds);
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

        var lineTile = IsLine(sa) ? a : b;
        var patchBotTile = IsPatchBot(sa) ? a : b;

        lastLineSpecial = lineTile.GetSpecial();
        lastHadTarget = false;
        lastStartDelaySeconds = 0f;

        var target = ctx.PatchbotService.FindTarget(patchBotTile, lineTile, null);
        if (target.hasCell)
        {
            lastHadTarget = true;
            lastTargetX = target.x;
            lastTargetY = target.y;

            var fromCell = new Vector2Int(patchBotTile.X, patchBotTile.Y);
            var toCell = new Vector2Int(target.x, target.y);
            float travelDuration = board.PatchbotDashUI != null
                ? board.PatchbotDashUI.EstimateDashDuration(board, fromCell, toCell)
                : 0.22f;

            ctx.PatchbotService.EnqueueDash(patchBotTile, target.x, target.y);
            ctx.VisualService.PlayTeleportMarkers(patchBotTile, target.x, target.y);
            ctx.VisualService.PlayTeleportMarkers(lineTile, target.x, target.y);
            ctx.VisualService.PlayTransientSpecialPairTravelVisualAt(patchBotTile, lineTile, target.x, target.y, travelDuration);
            lastStartDelaySeconds = travelDuration;

            var cells = board.SpecialBehaviors.CalculateEffect(lineTile.GetSpecial(), board, target.x, target.y);
            foreach (var c in cells)
            {
                SpecialCellUtils.MarkAffectedCell(res, c.x, c.y, board);
                if (board.Tiles[c.x, c.y] != null) res.Affected.Add(board.Tiles[c.x, c.y]);
                if (board.Tiles[c.x, c.y] != null) res.LightningVisualTargets.Add(board.Tiles[c.x, c.y]);
            }

            res.LightningLineStrikes.Add(
                new LightningLineStrike(new Vector2Int(target.x, target.y), lastLineSpecial == TileSpecial.LineH, travelDuration));
            res.HasLineActivation = true;
        }
    }

    static bool IsLine(TileSpecial s) => s == TileSpecial.LineH || s == TileSpecial.LineV;
    static bool IsPatchBot(TileSpecial s) => s == TileSpecial.PatchBot;
}