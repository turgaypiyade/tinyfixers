using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verilen hücrelere tek seferde "isabet" uygular (PatchBot isabet kuralıyla: taş kırılır, engel 1 vuruş,
/// special tetiklenir). Sequencer'a eklenir; hasar o anki içeriğe (düşmeler/temizlikler sonrası) uygulanır.
/// Kullananlar: EggBirdHatchAction (kuş dalışı), HamsterObstacleService (iniş 4x4).
/// </summary>
public sealed class CellsImpactAction : BoardAction
{
    private readonly BoardController board;
    private readonly LevelData level;
    private readonly List<Vector2Int> targets;
    public bool Completed { get; private set; }

    public CellsImpactAction(BoardController board, LevelData level,
        List<Vector2Int> targets)
    {
        this.board = board;
        this.level = level;
        this.targets = targets;
    }

    private static bool IsCurrentBoard(BoardController board, LevelData level)
        => board != null && board.isActiveAndEnabled && board.LevelData == level;

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        try
        {
            if (!IsCurrentBoard(board, level) || sequencer == null)
                yield break;

            var service = new PatchbotComboService(board);
            var context = new ResolutionContext { AffectedCells = new HashSet<Vector2Int>() };
            var dataMatches = new HashSet<TileData>();
            foreach (var cell in new HashSet<Vector2Int>(targets))
            {
                // Resolve here, after any queued falls/clears, never against stale TileViews.
                service.ResolveTargetImpact(dataMatches, cell.x, cell.y,
                    service.HasObstacleAt(cell.x, cell.y),
                    (x, y) => SpecialCellUtils.MarkPatchBotImpactCell(context, board, x, y),
                    tile => SpecialCellUtils.MarkAffectedCell(context, tile, board));
            }
            foreach (var data in dataMatches)
            {
                if (data == null || data.X < 0 || data.X >= board.Width
                    || data.Y < 0 || data.Y >= board.Height)
                    continue;
                var tile = board.Tiles[data.X, data.Y];
                if (tile != null)
                    context.Affected.Add(tile);
            }

            if (context.Affected.Count > 0 || context.AffectedCells.Count > 0 || context.ImpactCells.Count > 0)
            {
                yield return new MatchClearAction(context.Affected,
                    doShake: true,
                    affectedCells: context.AffectedCells,
                    includeAdjacentOverTileBlockerDamage: false,
                    staggerAnimTime: 0f,
                    isSpecialPhase: true,
                    impactCells: context.ImpactCells,
                    enqueueCascadeOnComplete: false).ExecuteVisuals(sequencer);
            }
        }
        finally
        {
            Completed = true;
            if (IsCurrentBoard(board, level))
                board.RequestResolveAfterActionSequence();
        }
    }
}
