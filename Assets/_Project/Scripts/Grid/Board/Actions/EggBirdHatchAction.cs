using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The egg is already removed by normal obstacle damage. Flight is detached from
/// gravity through rise, split AND dive; only damage rejoins the sequencer and
/// reads current contents. The flame plays at physical arrival, independently of that queue.
/// </summary>
public sealed class EggBirdHatchAction : BoardAction
{
    private readonly BoardController board;
    private readonly Vector2Int origin;

    public EggBirdHatchAction(BoardController board, Vector2Int origin)
    {
        this.board = board;
        this.origin = origin;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (board == null || board.LevelData == null)
            yield break;

        var level = board.LevelData;
        var flightJob = board.BeginJob(BoardController.BoardJobKind.EggBirdFlight);
        EggBirdFlightView flight = null;
        try
        {
            var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(ObstacleId.EggBird) : null;
            flight = EggBirdFlightView.Create(board, origin, def);
            if (flight != null)
                yield return flight.RiseAndSplit();
            else
                // Let the destruction event finish removing the source tile even if art is missing.
                yield return null;

            if (!IsCurrentBoard(board, level))
                yield break;

            var targets = PickTargets(board);
            if (targets.Count == 0)
                yield break;

            if (flight != null)
                yield return flight.Dive(targets);
            if (!IsCurrentBoard(board, level))
                yield break;
            if (flight != null)
                flight.Impact(targets);

            // Flight never enters the main queue: a long cascade may continue under the birds.
            var impact = new ImpactAction(board, level, targets);
            board.EnqueueBoardAction(impact);
            // Keep the level-end flight guard until damage and chained specials are handed off.
            while (!impact.Completed && IsCurrentBoard(board, level))
                yield return null;

            if (flight != null && IsCurrentBoard(board, level))
                yield return flight.FinishBursts();
        }
        finally
        {
            if (flight != null)
                Object.Destroy(flight.gameObject);
            flightJob.Dispose();
            if (IsCurrentBoard(board, level))
                board.RequestResolveAfterActionSequence();
        }
    }

    private static bool IsCurrentBoard(BoardController board, LevelData level)
        => board != null && board.isActiveAndEnabled && board.LevelData == level;

    private static List<Vector2Int> PickTargets(BoardController board)
    {
        var candidates = new List<Vector2Int>();
        var obstacles = board.ObstacleStateService;
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            bool hasObstacle = obstacles != null && obstacles.HasObstacleAt(x, y);
            if (board.IsMaskHoleCell(x, y) && !hasObstacle)
                continue;
            if (obstacles != null && obstacles.IsExitAtBottomAt(x, y))
                continue;
            if (!hasObstacle && board.Tiles[x, y] == null)
                continue;
            candidates.Add(new Vector2Int(x, y));
        }

        var targets = new List<Vector2Int>(3);
        // Partial shuffle: uniform random cells, without goal bias or duplicate targets.
        for (int i = 0; i < 3 && i < candidates.Count; i++)
        {
            int picked = Random.Range(i, candidates.Count);
            Vector2Int selected = candidates[picked];
            candidates[picked] = candidates[i];
            candidates[i] = selected;
            targets.Add(selected);
        }
        // Tiny boards still show three birds. Shared cells receive one grouped hit.
        int uniqueCount = targets.Count;
        for (int i = uniqueCount; i < 3 && uniqueCount > 0; i++)
            targets.Add(targets[i % uniqueCount]);
        return targets;
    }

    private sealed class ImpactAction : BoardAction
    {
        private readonly BoardController board;
        private readonly LevelData level;
        private readonly List<Vector2Int> targets;
        public bool Completed { get; private set; }

        public ImpactAction(BoardController board, LevelData level,
            List<Vector2Int> targets)
        {
            this.board = board;
            this.level = level;
            this.targets = targets;
        }

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
}
