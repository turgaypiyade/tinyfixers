using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class OverridePatchBotAirborneGroupAction : BoardAction
{
    private enum AirborneImpactMode
    {
        StandardPatchBot,
        PulseCoreAtTarget
    }

    private sealed class AirborneBot
    {
        public Vector2Int sourceCell;
        public TileType sourceType;
        public GameObject ghost;
        public RectTransform rect;
        public RectTransform flightRoot;
        public Image image;
        public Vector2 hoverAnchor;
        public bool diving;
        public bool arrived;

        // Bot'un hafızası — neyi hedefliyor?
        public PatchBotIntent intent;

        // Dive sonu hedef cell (resolve sonrası)
        public int targetX = -1;
        public int targetY = -1;
        public bool hasTarget;
    }

    private readonly BoardController board;
    private readonly List<Vector2Int> sourceCells;
    private readonly AirborneImpactMode impactMode;
    private readonly PulseCorePatchBotComboExecutionRuntime pulseRuntime;
    private readonly PulseCoreSpecial pulseCoreSpecial;
    private readonly TileView patchBotSourceTile;
    private readonly TileView pulseSourceTile;

    public override bool Blocking => true;

    // Tüm botların aynı anda inmesi için sabit dive süresi (saniye).
    private const float SYNC_DIVE_DURATION = 0.22f;

    public OverridePatchBotAirborneGroupAction(BoardController board, List<Vector2Int> sourceCells)
    {
        this.board = board;
        this.sourceCells = sourceCells != null ? new List<Vector2Int>(sourceCells) : new List<Vector2Int>();
        this.impactMode = AirborneImpactMode.StandardPatchBot;
    }

    public OverridePatchBotAirborneGroupAction(
        BoardController board,
        Vector2Int patchBotCell,
        TileView patchBotSourceTile,
        TileView pulseSourceTile,
        PulseCorePatchBotComboExecutionRuntime pulseRuntime,
        int pulseAffectedCellCount)
    {
        this.board = board;
        this.sourceCells = new List<Vector2Int> { patchBotCell };
        this.impactMode = AirborneImpactMode.PulseCoreAtTarget;
        this.patchBotSourceTile = patchBotSourceTile;
        this.pulseSourceTile = pulseSourceTile;
        this.pulseRuntime = pulseRuntime;
        this.pulseCoreSpecial = new PulseCoreSpecial(Mathf.Max(1, pulseAffectedCellCount));
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (board == null || sequencer == null || sourceCells.Count == 0)
            yield break;

        float startTime = Time.realtimeSinceStartup;
        Debug.Log($"[OverridePatchBotAirborne] BEGIN sources={sourceCells.Count}");

        var patchbotService = new PatchbotComboService(board);
        var coordinator = new PatchBotTargetCoordinator(board, patchbotService);
        var bots = BuildAndLiftBots(coordinator);
        ClearPulseSourceForPulseMode();

        Debug.Log($"[OverridePatchBotAirborne] takeoff count={bots.Count} t={Time.realtimeSinceStartup - startTime:0.000}s");

        if (bots.Count == 0)
            yield break;

        // Cascade boyunca botlar hover'da, board match'leri ve düşüşü çalışır.
        yield return RunInitialCascadeWhileHovering(sequencer);
        Debug.Log($"[OverridePatchBotAirborne] cascade_done t={Time.realtimeSinceStartup - startTime:0.000}s");

        // Cascade bitti. Her bot intent'ini yeniden değerlendirir, dive hedefini netleştirir.
        ResolveAllDiveTargets(bots, coordinator);
        Debug.Log($"[OverridePatchBotAirborne] targets_resolved t={Time.realtimeSinceStartup - startTime:0.000}s");

        // Tüm botlar AYNI ANDA dive eder, AYNI ANDA iner. Hepsi indikten sonra TEK clear.
        yield return SynchronizedDiveAndClear(sequencer, bots, patchbotService, coordinator);
        Debug.Log($"[OverridePatchBotAirborne] END t={Time.realtimeSinceStartup - startTime:0.000}s");

        DestroyGhosts(bots);
    }

    private List<AirborneBot> BuildAndLiftBots(PatchBotTargetCoordinator coordinator)
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

            var bot = CreateGhost(cell, tile, sprite, i);
            if (bot == null)
                continue;

            // Kalkış anında soft reservation — hedef bilgisi.
            // Cascade sonrasında bu intent ya canlı kalır (TileView ref'i ile takip),
            // ya da yenilenir (ResolveAllDiveTargets içinde).
            var (intent, hasIntent) = coordinator.PickIntent(null, null, null);
            bot.intent = hasIntent ? intent : null;

            bots.Add(bot);

            // Source cell'i ANINDA boşalt — cascade buradan akabilsin.
            SpecialVisualService.HideTileVisualForCombo(tile);
            board.ClearCell(cell.x, cell.y);
            board.ClearCellVisualOnly(cell, bot.sourceType, tile);

            board.StartCoroutine(CoTakeoffAndHover(bot, i));
        }

        return bots;
    }

    private AirborneBot CreateGhost(Vector2Int cell, TileView tile, Sprite sprite, int index)
    {
        var flightRoot = GetFlightRoot();
        if (flightRoot == null)
            return null;

        var ghost = new GameObject("OverridePatchBotAirborne", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = ghost.GetComponent<RectTransform>();
        var image = ghost.GetComponent<Image>();

        rect.SetParent(flightRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(board.TileSize, board.TileSize);
        rect.anchoredPosition = CellAnchored(cell.x, cell.y, flightRoot);
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();

        image.sprite = sprite != null ? sprite : board.GetSpecialIcon(TileSpecial.PatchBot);
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        int side = ((cell.x + cell.y + index) & 1) == 0 ? -1 : 1;
        float lateral = board.TileSize * (0.46f + 0.08f * (index % 3));
        float altitude = board.TileSize * (0.90f + 0.05f * (index % 2));
        Vector2 start = rect.anchoredPosition;
        Vector2 hoverAnchor = start + new Vector2(side * lateral, altitude);

        return new AirborneBot
        {
            sourceCell = cell,
            sourceType = tile.GetTileType(),
            ghost = ghost,
            rect = rect,
            flightRoot = flightRoot,
            image = image,
            hoverAnchor = hoverAnchor
        };
    }

    private IEnumerator CoTakeoffAndHover(AirborneBot bot, int index)
    {
        if (bot == null || bot.rect == null)
            yield break;

        Vector2 start = bot.rect.anchoredPosition;
        Vector2 hover = bot.hoverAnchor;
        float phase = index * 0.77f;
        float burstDuration = board.ApplySpecialChainTempo(0.13f);
        float elapsed = 0f;

        while (elapsed < burstDuration && bot != null && bot.rect != null && !bot.arrived && !bot.diving)
        {
            elapsed += Time.deltaTime;
            float t = burstDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / burstDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float punch = Mathf.Sin(t * Mathf.PI) * board.TileSize * 0.10f;

            bot.rect.anchoredPosition = Vector2.LerpUnclamped(start, hover, eased) + Vector2.up * punch;
            bot.rect.localScale = Vector3.one * Mathf.Lerp(1.00f, 1.18f, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        while (bot != null && bot.rect != null && !bot.arrived && !bot.diving)
        {
            elapsed += Time.deltaTime;
            float wobbleX = Mathf.Sin(elapsed * 8.5f + phase) * board.TileSize * 0.055f;
            float wobbleY = Mathf.Sin(elapsed * 10.0f + phase) * board.TileSize * 0.035f;

            bot.rect.anchoredPosition = hover + new Vector2(wobbleX, wobbleY);
            bot.rect.localScale = Vector3.one * 1.08f;
            bot.rect.SetAsLastSibling();
            yield return null;
        }
    }

    private void ClearPulseSourceForPulseMode()
    {
        if (impactMode != AirborneImpactMode.PulseCoreAtTarget)
            return;

        if (board == null || pulseSourceTile == null)
            return;

        int x = pulseSourceTile.X;
        int y = pulseSourceTile.Y;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;

        if (board.Tiles[x, y] != pulseSourceTile)
            return;

        var cell = new Vector2Int(x, y);
        var sourceType = pulseSourceTile.GetTileType();

        SpecialVisualService.HideTileVisualForCombo(pulseSourceTile);
        board.ClearCell(x, y);
        board.ClearCellVisualOnly(cell, sourceType, pulseSourceTile);
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

    /// <summary>
    /// Cascade bittikten sonra her botun intent'ini yeniden değerlendirir.
    /// Intent canlıysa GÜNCEL cell'i kullanır (taş düştü → yeni y).
    /// Intent öldüyse (taş match'te yok oldu) → coordinator yeni hedef seçer.
    /// </summary>
    private void ResolveAllDiveTargets(List<AirborneBot> bots, PatchBotTargetCoordinator coordinator)
    {
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot == null || bot.rect == null) continue;

            var resolved = coordinator.ResolveIntentToCell(bot.intent, null, null, null);
            if (!resolved.hasCell)
            {
                bot.hasTarget = false;
                Debug.Log($"[OverridePatchBotAirborne] no_target step={i} from={bot.sourceCell}");
                continue;
            }

            bot.intent = resolved.intent;
            bot.targetX = resolved.cell.x;
            bot.targetY = resolved.cell.y;
            bot.hasTarget = true;

            Debug.Log($"[OverridePatchBotAirborne] target_resolved step={i} from={bot.sourceCell} target=({bot.targetX},{bot.targetY})");
        }
    }

    /// <summary>
    /// Tüm botlar AYNI ANDA dive coroutine başlatır.
    /// Hepsi SYNC_DIVE_DURATION süresinde iner.
    /// Hepsi indikten sonra → ghost'lar destroy + TEK MatchClearAction yield ile çalışır.
    /// → "Hepsi aynı anda iner, hepsi aynı anda patlar."
    /// </summary>
    private IEnumerator SynchronizedDiveAndClear(
        ActionSequencer sequencer,
        List<AirborneBot> bots,
        PatchbotComboService patchbotService,
        PatchBotTargetCoordinator coordinator)
    {
        // ─── PHASE 1: Tüm dive'ları AYNI ANDA başlat ───
        int diveCount = 0;
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot == null || bot.rect == null) continue;

            if (!bot.hasTarget)
            {
                // Hedef yok — bot sessizce yok ol.
                bot.arrived = true;
                if (bot.ghost != null) Object.Destroy(bot.ghost);
                continue;
            }

            diveCount++;
            board.StartCoroutine(CoSynchronizedDive(bot));
        }

        Debug.Log($"[OverridePatchBotAirborne] sync_dive_start count={diveCount} duration={SYNC_DIVE_DURATION}");

        if (diveCount == 0)
            yield break;

        // ─── PHASE 2: Tüm dive'lar bitene kadar bekle ───
        // Sabit süreli bekleme + güvenlik için kalan diving bot kontrolü.
        yield return new WaitForSeconds(board.ApplySpecialChainTempo(SYNC_DIVE_DURATION));

        bool anyDiving = true;
        float guard = 0f;
        while (anyDiving && guard < 0.5f)
        {
            anyDiving = false;
            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i] != null && bots[i].diving && !bots[i].arrived)
                {
                    anyDiving = true;
                    break;
                }
            }
            if (anyDiving)
            {
                yield return null;
                guard += Time.deltaTime;
            }
        }

        Debug.Log($"[OverridePatchBotAirborne] all_arrived");

        if (impactMode == AirborneImpactMode.PulseCoreAtTarget)
        {
            yield return ExecutePulseCoreImpactAndFinalCascade(sequencer, bots, coordinator);
            yield break;
        }

        // ─── PHASE 3: Topla — tüm hedef cell'lerin TileData/TileView'ları ───
        var groupCtx = new ResolutionContext();
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot == null || !bot.hasTarget) continue;

            bool hasObstacle = patchbotService.HasObstacleAt(bot.targetX, bot.targetY);
            var dataMatches = new HashSet<TileData>();

            patchbotService.ResolveTargetImpact(
                dataMatches,
                bot.targetX,
                bot.targetY,
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

            // Intent serbest.
            if (bot.intent != null)
            {
                coordinator.ReleaseIntent(bot.intent);
                bot.intent = null;
            }
        }

        // ─── PHASE 4: Ghost'ları destroy — clear başlamadan ÖNCE ───
        // Görsel: "bot taşa değdi, taş patladı, bot da o anda kayboldu" (senkron).
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot != null && bot.ghost != null)
            {
                Object.Destroy(bot.ghost);
                bot.ghost = null;
            }
        }

        Debug.Log($"[OverridePatchBotAirborne] group_clear affected={groupCtx.Affected.Count} cells={groupCtx.AffectedCells.Count}");

        // ─── PHASE 5: TEK MatchClearAction — sequencer'ı bypass eden direct call ───
        if (groupCtx.Affected.Count > 0 || groupCtx.AffectedCells.Count > 0 || groupCtx.ImpactCells.Count > 0)
        {
            var clearAction = new MatchClearAction(
                groupCtx.Affected,
                doShake: true,
                animationMode: ClearAnimationMode.Default,
                affectedCells: groupCtx.AffectedCells,
                impactCells: groupCtx.ImpactCells,
                includeAdjacentOverTileBlockerDamage: false,
                staggerDelays: null,           // EXPLICIT: hiç stagger yok
                staggerAnimTime: 0f,           // EXPLICIT: tüm taşlar AYNI ANDA patlar
                isSpecialPhase: true,
                enqueueCascadeOnComplete: false); // Cascade'i biz manuel çalıştırıyoruz

            // Direct ExecuteVisuals çağrısı = sequencer kuyruğunu bypass.
            // Bu şu an Blocking action'ın koroutine'i içinde çalışıyor,
            // sequencer queue'ya yeni action girmiyor → ANINDA başlar.
            yield return clearAction.ExecuteVisuals(sequencer);
        }

        // ─── PHASE 6: Tek cascade ───
        var cascades = board.CascadeLogic.CalculateCascades();
        Debug.Log($"[OverridePatchBotAirborne] final_cascade actions={(cascades != null ? cascades.Count : 0)}");
        if (cascades != null)
        {
            for (int i = 0; i < cascades.Count; i++)
                yield return cascades[i].ExecuteVisuals(sequencer);
        }

        board.RefreshAllSortingOrders();
    }

    private IEnumerator ExecutePulseCoreImpactAndFinalCascade(
        ActionSequencer sequencer,
        List<AirborneBot> bots,
        PatchBotTargetCoordinator coordinator)
    {
        AirborneBot impactBot = null;

        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot != null && bot.hasTarget)
            {
                impactBot = bot;
                break;
            }
        }

        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot != null && bot.ghost != null)
            {
                Object.Destroy(bot.ghost);
                bot.ghost = null;
            }
        }

        if (impactBot != null && pulseRuntime != null && pulseCoreSpecial != null)
        {
            Debug.Log($"[OverridePatchBotAirborne] pulsecore_impact target=({impactBot.targetX},{impactBot.targetY})");

            var arrivalCtx = new ResolutionContext();
            var pulseResult = pulseCoreSpecial.ExecuteAtTarget(new PulseCoreExecutionRuntime
            {
                Board = board,
                Context = arrivalCtx,
                Origin = pulseSourceTile,
                Partner = patchBotSourceTile,
                FinalizeAtEnd = true,
                ActivateSpecial = pulseRuntime.ActivateSpecial,
                ProcessFanout = pulseRuntime.ProcessFanout,
                CleanupImplantedTiles = pulseRuntime.CleanupImplantedTiles,
                FireOverrideOverrideSpecialVisuals = pulseRuntime.FireOverrideOverrideSpecialVisuals,
                EmitBoardSignal = pulseRuntime.EmitBoardSignal,
                EnqueueChainSpecials = pulseRuntime.EnqueueChainSpecials,
                ProcessQueue = pulseRuntime.ProcessQueue,
                SuppressVisualSideEffects = false,
                SkipOriginRegistration = true,
                ForcedOriginSpecial = TileSpecial.PulseCore,
                SignalSourceTile = pulseSourceTile
            }, impactBot.targetX, impactBot.targetY);

            if (pulseResult != null && pulseResult.Actions != null)
            {
                for (int i = 0; i < pulseResult.Actions.Count; i++)
                {
                    var action = pulseResult.Actions[i];
                    if (action != null)
                        yield return action.ExecuteVisuals(sequencer);
                }
            }
        }

        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot != null && bot.intent != null)
            {
                coordinator.ReleaseIntent(bot.intent);
                bot.intent = null;
            }
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

    /// <summary>
    /// Bot için sabit süreli dive. SYNC_DIVE_DURATION ile tüm botlar aynı anda biter.
    /// Mesafe ne olursa olsun süre sabit — hız mesafeye göre kendi kendine ölçeklenir.
    /// Ghost burada DESTROY EDİLMEZ — toplu clear başlamadan önce merkez tarafından destroy edilir.
    /// </summary>
    private IEnumerator CoSynchronizedDive(AirborneBot bot)
    {
        if (bot == null || bot.rect == null)
            yield break;

        bot.diving = true;
        bot.rect.SetAsLastSibling();

        Vector2 start = bot.rect.anchoredPosition;
        Vector2 end = CellAnchored(bot.targetX, bot.targetY, bot.flightRoot);
        Vector2 delta = end - start;
        Vector2 normal = delta.sqrMagnitude > 0.001f
            ? new Vector2(-delta.y, delta.x).normalized
            : Vector2.up;
        float arc = Mathf.Clamp(delta.magnitude * 0.11f, board.TileSize * 0.14f, board.TileSize * 0.48f);
        float duration = board.ApplySpecialChainTempo(SYNC_DIVE_DURATION);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float curve = Mathf.Sin(t * Mathf.PI) * arc;
            float snap = Mathf.Sin(t * Mathf.PI * 2f) * board.TileSize * 0.025f;

            if (bot.rect != null)
            {
                bot.rect.anchoredPosition = Vector2.LerpUnclamped(start, end, eased) + normal * (curve + snap);
                bot.rect.localScale = Vector3.one * Mathf.Lerp(1.10f, 0.92f, t);
                bot.rect.SetAsLastSibling();
            }

            yield return null;
        }

        bot.arrived = true;
    }

    private void DestroyGhosts(List<AirborneBot> bots)
    {
        if (bots == null) return;

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

    private RectTransform GetFlightRoot()
    {
        if (board.BoardVfxPlayer != null && board.BoardVfxPlayer.VfxRoot != null)
            return board.BoardVfxPlayer.VfxRoot;

        return board.Parent;
    }

    private Vector2 CellAnchored(int x, int y, RectTransform targetSpace)
    {
        if (targetSpace == null)
            return Vector2.zero;

        Vector3 worldPos = board.GetCellWorldPosition(x, y);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetSpace,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out var localPoint);

        return localPoint;
    }
}