using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bir hamlede tetiklenen RocketBasket roketlerini fırlatır. Hedefleme + impact PatchBot ile
/// BİREBİR aynı (PatchBotTargetCoordinator + PatchbotComboService.ResolveTargetImpact + tek
/// MatchClearAction), yalnızca uçuş görseli roket-mermi (RocketProjectileFlight).
/// Referans: OverridePatchBotAirborneGroupAction.SynchronizedDiveAndClear.
/// </summary>
public sealed class RocketBasketLaunchAction : BoardAction
{
    public struct Launch
    {
        public Vector2Int origin;
        public TileType color;
        public Sprite rocketSprite;
    }

    private sealed class Rocket
    {
        public Vector2Int origin;
        public Sprite sprite;
        public PatchBotIntent intent;
        public int targetX, targetY;
        public bool hasTarget;
    }

    private readonly BoardController board;
    private readonly List<Launch> launches;

    public RocketBasketLaunchAction(BoardController board, List<Launch> launches)
    {
        this.board = board;
        this.launches = launches != null ? new List<Launch>(launches) : new List<Launch>();
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (board == null || launches.Count == 0)
            yield break;

        var patchbotService = new PatchbotComboService(board);
        var coordinator = new PatchBotTargetCoordinator(board, patchbotService);
        var flight = board.GetComponent<RocketProjectileFlight>();

        // ─── Hedef seç (PatchBot önceliği; coordinator çakışmayı önler) ───
        var rockets = new List<Rocket>(launches.Count);
        foreach (var l in launches)
        {
            var rocket = new Rocket { origin = l.origin, sprite = l.rocketSprite };

            var (intent, has) = coordinator.PickIntentFrom(l.origin);
            if (has && intent != null)
            {
                var cell = intent.CurrentCell(board);
                if (cell.x >= 0 && cell.y >= 0)
                {
                    rocket.intent = intent;
                    rocket.targetX = cell.x;
                    rocket.targetY = cell.y;
                    rocket.hasTarget = true;
                }
                else
                {
                    coordinator.ReleaseIntent(intent);
                }
            }
            rockets.Add(rocket);
        }

        // ─── Uçuş (hepsi eşzamanlı) ───
        board.BeginPatchBotDashFlight();
        try
        {
            int flying = 0;
            for (int i = 0; i < rockets.Count; i++)
            {
                var r = rockets[i];
                if (!r.hasTarget) continue;

                if (flight != null && r.sprite != null)
                {
                    flying++;
                    var target = new Vector2Int(r.targetX, r.targetY);
                    board.StartCoroutine(flight.Fly(r.origin, target, r.sprite, () => flying--));
                }
            }

            while (flying > 0)
                yield return null;
        }
        finally
        {
            board.EndPatchBotDashFlight();
        }

        // ─── Grup impact: her hedefe PatchBot impact'i topla → TEK MatchClearAction ───
        var groupCtx = new ResolutionContext { AffectedCells = new HashSet<Vector2Int>() };

        for (int i = 0; i < rockets.Count; i++)
        {
            var r = rockets[i];
            if (!r.hasTarget) continue;

            bool hasObstacle = patchbotService.HasObstacleAt(r.targetX, r.targetY);
            var dataMatches = new HashSet<TileData>();

            patchbotService.ResolveTargetImpact(
                dataMatches,
                r.targetX,
                r.targetY,
                hasObstacle,
                (x, y) => SpecialCellUtils.MarkAffectedCell(groupCtx, x, y, board),
                t => SpecialCellUtils.MarkAffectedCell(groupCtx, t, board));

            foreach (var data in dataMatches)
            {
                if (data == null) continue;
                if (data.X < 0 || data.X >= board.Width || data.Y < 0 || data.Y >= board.Height) continue;
                var tile = board.Tiles[data.X, data.Y];
                if (tile != null)
                    groupCtx.Affected.Add(tile);
            }

            if (r.intent != null)
            {
                coordinator.ReleaseIntent(r.intent);
                r.intent = null;
            }
        }

        if (groupCtx.Affected.Count > 0 || groupCtx.AffectedCells.Count > 0 || groupCtx.ImpactCells.Count > 0)
        {
            var clearAction = new MatchClearAction(
                groupCtx.Affected,
                doShake: true,
                animationMode: ClearAnimationMode.Default,
                affectedCells: groupCtx.AffectedCells,
                includeAdjacentOverTileBlockerDamage: false,
                staggerDelays: null,
                staggerAnimTime: 0f,
                isSpecialPhase: true,
                impactCells: groupCtx.ImpactCells,
                enqueueCascadeOnComplete: false);

            yield return clearAction.ExecuteVisuals(sequencer);
        }

        // ─── Tek cascade ───
        var cascades = board.CascadeLogic.CalculateCascades();
        if (cascades != null)
            for (int i = 0; i < cascades.Count; i++)
                yield return cascades[i].ExecuteVisuals(sequencer);

        board.RefreshAllSortingOrders();
        board.RequestResolveAfterActionSequence();
    }
}
