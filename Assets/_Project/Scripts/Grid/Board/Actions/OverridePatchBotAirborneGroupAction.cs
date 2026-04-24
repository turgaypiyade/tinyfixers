using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class OverridePatchBotAirborneGroupAction : BoardAction
{
    private sealed class AirborneBot
    {
        public Vector2Int sourceCell;
        public TileType sourceType;
        public GameObject ghost;
        public RectTransform rect;
        public Image image;
        public bool arrived;
    }

    private readonly BoardController board;
    private readonly List<Vector2Int> sourceCells;

    public override bool Blocking => true;

    public OverridePatchBotAirborneGroupAction(BoardController board, List<Vector2Int> sourceCells)
    {
        this.board = board;
        this.sourceCells = sourceCells != null ? new List<Vector2Int>(sourceCells) : new List<Vector2Int>();
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (board == null || sequencer == null || sourceCells.Count == 0)
            yield break;

        var patchbotService = new PatchbotComboService(board);
        var coordinator = new PatchBotTargetCoordinator(board, patchbotService);
        var bots = BuildAndLiftBots();

        Debug.Log($"[OverridePatchBotAirborne] takeoff count={bots.Count}");

        if (bots.Count == 0)
            yield break;

        // Source cells are already empty now; let the board settle while bots hover.
        yield return RunInitialCascadeWhileHovering(sequencer);

        // Targets are selected after the board has changed, so PatchBots use the fresh board.
        yield return DiveBotsAgainstCurrentBoard(sequencer, bots, patchbotService, coordinator);

        DestroyGhosts(bots);
    }

    private List<AirborneBot> BuildAndLiftBots()
    {
        var bots = new List<AirborneBot>();
        var sprite = board.GetSpecialIcon(TileSpecial.PatchBot);

        for (int i = 0; i < sourceCells.Count; i++)
        {
            var cell = sourceCells[i];
            if (!IsInside(cell))
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null || tile.GetSpecial() != TileSpecial.PatchBot)
                continue;

            var bot = CreateGhost(cell, tile, sprite);
            if (bot == null)
                continue;

            bots.Add(bot);

            // The source is consumed immediately so normal board fall/cascade can happen
            // while the PatchBot is airborne.
            SpecialVisualService.HideTileVisualForCombo(tile);
            board.ClearCell(cell.x, cell.y);
            board.ClearCellVisualOnly(cell, bot.sourceType, tile);

            board.StartCoroutine(CoHover(bot, i));
        }

        return bots;
    }

    private AirborneBot CreateGhost(Vector2Int cell, TileView tile, Sprite sprite)
    {
        var parent = board.Parent;
        if (parent == null)
            return null;

        var ghost = new GameObject("OverridePatchBotAirborne", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = ghost.GetComponent<RectTransform>();
        var image = ghost.GetComponent<Image>();

        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(board.TileSize, board.TileSize);
        rect.anchoredPosition = CellAnchored(cell.x, cell.y);
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();

        image.sprite = sprite != null ? sprite : tile.GetIconSprite();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        return new AirborneBot
        {
            sourceCell = cell,
            sourceType = tile.GetTileType(),
            ghost = ghost,
            rect = rect,
            image = image
        };
    }

    private IEnumerator CoHover(AirborneBot bot, int index)
    {
        if (bot == null || bot.rect == null)
            yield break;

        Vector2 basePos = CellAnchored(bot.sourceCell.x, bot.sourceCell.y);
        float phase = index * 0.73f;
        float lift = board.TileSize * 0.42f;
        float sway = board.TileSize * 0.16f;
        float takeoffDuration = board.ApplySpecialChainTempo(0.18f);
        float elapsed = 0f;

        while (bot != null && bot.rect != null && !bot.arrived)
        {
            elapsed += Time.deltaTime;
            float t = takeoffDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / takeoffDuration);
            float wobble = Mathf.Sin((elapsed * 9f) + phase) * sway;
            float bob = Mathf.Sin((elapsed * 11f) + phase) * board.TileSize * 0.035f;

            bot.rect.anchoredPosition = basePos + new Vector2(wobble, lift * t + bob);
            bot.rect.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.16f, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
    }

    private IEnumerator RunInitialCascadeWhileHovering(ActionSequencer sequencer)
    {
        var cascades = board.CascadeLogic.CalculateCascades();
        Debug.Log($"[OverridePatchBotAirborne] source_cascade actions={(cascades != null ? cascades.Count : 0)}");

        if (cascades == null || cascades.Count == 0)
            yield break;

        for (int i = 0; i < cascades.Count; i++)
            yield return cascades[i].ExecuteVisuals(sequencer);

        board.RefreshAllSortingOrders();
    }

    private IEnumerator DiveBotsAgainstCurrentBoard(
        ActionSequencer sequencer,
        List<AirborneBot> bots,
        PatchbotComboService patchbotService,
        PatchBotTargetCoordinator coordinator)
    {
        var groupCtx = new ResolutionContext();
        const float stagger = 0.035f;
        int active = 0;

        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot == null || bot.rect == null)
                continue;

            var target = coordinator.ReserveTarget(null, null, null);
            if (!target.hasCell)
            {
                bot.arrived = true;
                if (bot.ghost != null) Object.Destroy(bot.ghost);
                continue;
            }

            Debug.Log($"[OverridePatchBotAirborne] acquire step={i + 1}/{bots.Count} from={bot.sourceCell} target=({target.x},{target.y})");

            active++;
            board.StartCoroutine(CoDive(bot, target.x, target.y, patchbotService, coordinator, groupCtx, () => active--));

            yield return new WaitForSeconds(board.ApplySpecialChainTempo(stagger));
        }

        while (active > 0)
            yield return null;

        if (groupCtx.Affected.Count > 0 || groupCtx.AffectedCells.Count > 0 || groupCtx.ImpactCells.Count > 0)
        {
            var clearAction = new MatchClearAction(
                groupCtx.Affected,
                doShake: true,
                animationMode: ClearAnimationMode.Default,
                affectedCells: groupCtx.AffectedCells,
                impactCells: groupCtx.ImpactCells,
                includeAdjacentOverTileBlockerDamage: false,
                isSpecialPhase: true,
                enqueueCascadeOnComplete: false);

            yield return clearAction.ExecuteVisuals(sequencer);
        }

        var cascades = board.CascadeLogic.CalculateCascades();
        Debug.Log($"[OverridePatchBotAirborne] final_cascade actions={(cascades != null ? cascades.Count : 0)}");
        if (cascades != null)
        {
            for (int i = 0; i < cascades.Count; i++)
                yield return cascades[i].ExecuteVisuals(sequencer);
        }

        board.RefreshAllSortingOrders();
    }

    private IEnumerator CoDive(
        AirborneBot bot,
        int targetX,
        int targetY,
        PatchbotComboService patchbotService,
        PatchBotTargetCoordinator coordinator,
        ResolutionContext groupCtx,
        System.Action onComplete)
    {
        Vector2 start = bot.rect != null ? bot.rect.anchoredPosition : CellAnchored(bot.sourceCell.x, bot.sourceCell.y);
        Vector2 end = CellAnchored(targetX, targetY);
        Vector2 delta = end - start;
        Vector2 normal = delta.sqrMagnitude > 0.001f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;
        float arc = Mathf.Clamp(delta.magnitude * 0.18f, board.TileSize * 0.18f, board.TileSize * 0.80f);
        float duration = board.ApplySpecialChainTempo(Mathf.Clamp(delta.magnitude / 760f, 0.18f, 0.42f));
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float curve = Mathf.Sin(t * Mathf.PI) * arc;
            float wiggle = Mathf.Sin(t * Mathf.PI * 4f) * board.TileSize * 0.04f;

            if (bot.rect != null)
            {
                bot.rect.anchoredPosition = Vector2.LerpUnclamped(start, end, eased) + normal * (curve + wiggle);
                bot.rect.localScale = Vector3.one * Mathf.Lerp(1.12f, 0.92f, t);
            }

            yield return null;
        }

        bot.arrived = true;
        coordinator.ReleaseReservation(targetX, targetY);

        if (bot.ghost != null)
            Object.Destroy(bot.ghost);

        bool hasObstacle = patchbotService.HasObstacleAt(targetX, targetY);
        var dataMatches = new HashSet<TileData>();

        patchbotService.ResolveTargetImpact(
            dataMatches,
            targetX,
            targetY,
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

        Debug.Log($"[OverridePatchBotAirborne] arrived target=({targetX},{targetY}) affected={groupCtx.Affected.Count}");
        onComplete?.Invoke();
    }

    private void DestroyGhosts(List<AirborneBot> bots)
    {
        if (bots == null)
            return;

        for (int i = 0; i < bots.Count; i++)
        {
            if (bots[i]?.ghost != null)
                Object.Destroy(bots[i].ghost);
        }
    }

    private bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < board.Width && cell.y >= 0 && cell.y < board.Height;
    }

    private Vector2 CellAnchored(int x, int y)
    {
        return new Vector2(
            x * board.TileSize + board.TileSize * 0.5f,
            -y * board.TileSize - board.TileSize * 0.5f);
    }
}