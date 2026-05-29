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

        // Orbit carry (PulseCore gibi taşınan special)
        public RectTransform carryRt;

        public PatchBotPropellerView propellerView;
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

    private readonly int bonusPhantomBots;

    public OverridePatchBotAirborneGroupAction(BoardController board, List<Vector2Int> sourceCells, int bonusPhantomBots = 0)
    {
        this.board = board;
        this.sourceCells = sourceCells != null ? new List<Vector2Int>(sourceCells) : new List<Vector2Int>();
        this.impactMode = AirborneImpactMode.StandardPatchBot;
        this.bonusPhantomBots = Mathf.Max(0, bonusPhantomBots);
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
        // var sprite = board.GetSpecialIcon(TileSpecial.PatchBot);
        var sprite = board.GetPatchBotFlightIcon();

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
            // tile'ı geçerek en uzak hedefin seçilmesi sağlanır.
            var (intent, hasIntent) = coordinator.PickIntent(tile, null, null);
            bot.intent = hasIntent ? intent : null;

            bots.Add(bot);

            // Source cell'i ANINDA boşalt — cascade buradan akabilsin.
            tile.PropellerView?.Stop();
            SpecialVisualService.HideTileVisualForCombo(tile);
            board.ClearCell(cell.x, cell.y);
            board.ClearCellVisualOnly(cell, bot.sourceType, tile);

            board.StartCoroutine(CoTakeoffAndHover(bot, i));
        }

        // Phantom bots: extra ghosts that fly from midpoint between real bots.
        int realBotCount = bots.Count;
        var flightRootForPhantom = GetFlightRoot();
        for (int b = 0; b < bonusPhantomBots && realBotCount > 0; b++)
        {
            var template = bots[b % realBotCount];
            int phantomIndex = realBotCount + b;

            // Start phantom from midpoint of the two real source cells so it doesn't overlap either bot.
            Vector2 phantomStart;
            if (flightRootForPhantom != null && realBotCount >= 2)
            {
                Vector2 p0 = CellAnchored(bots[0].sourceCell.x, bots[0].sourceCell.y, flightRootForPhantom);
                Vector2 p1 = CellAnchored(bots[1 % realBotCount].sourceCell.x, bots[1 % realBotCount].sourceCell.y, flightRootForPhantom);
                phantomStart = (p0 + p1) * 0.5f;
            }
            else if (flightRootForPhantom != null)
            {
                phantomStart = CellAnchored(template.sourceCell.x, template.sourceCell.y, flightRootForPhantom)
                               + new Vector2(board.TileSize * 0.4f, 0f);
            }
            else
            {
                phantomStart = Vector2.zero;
            }

            var phantom = CreatePhantomBot(template, sprite, phantomIndex, phantomStart);
            if (phantom == null) continue;

            var (pIntent, pHasIntent) = coordinator.PickIntentFrom(template.sourceCell);
            phantom.intent = pHasIntent ? pIntent : null;

            bots.Add(phantom);
            board.StartCoroutine(CoTakeoffAndHover(phantom, phantomIndex));
        }

        return bots;
    }

    private AirborneBot CreatePhantomBot(AirborneBot template, Sprite sprite, int index, Vector2 startAnchoredPos)
    {
        var flightRoot = GetFlightRoot();
        if (flightRoot == null || template == null)
            return null;

        var ghost = new GameObject("PatchBotPhantom", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = ghost.GetComponent<RectTransform>();
        var image = ghost.GetComponent<Image>();

        rect.SetParent(flightRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(board.TileSize, board.TileSize);
        rect.anchoredPosition = startAnchoredPos;
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();

        image.sprite = sprite != null ? sprite : board.GetSpecialIcon(TileSpecial.PatchBot);
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        var cell = template.sourceCell;
        int side = ((cell.x + cell.y + index) & 1) == 0 ? -1 : 1;
        float lateral = board.TileSize * (0.46f + 0.08f * (index % 3));
        float altitude = board.TileSize * (0.90f + 0.05f * (index % 2));
        Vector2 hoverAnchor = rect.anchoredPosition + new Vector2(side * lateral, altitude);

        PatchBotPropellerView propellerView = null;
        var propellerSprite = board.PatchBotPropellerSprite;
        if (propellerSprite != null)
        {
            var propGo = new GameObject("PatchBotPropeller",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PatchBotPropellerView));
            propGo.transform.SetParent(rect, false);

            var propRt = propGo.GetComponent<RectTransform>();
            propRt.anchorMin = new Vector2(0.5f, 0.5f);
            propRt.anchorMax = new Vector2(0.5f, 0.5f);
            propRt.pivot    = new Vector2(0.5f, 0.5f);
            propRt.sizeDelta = new Vector2(board.TileSize, board.TileSize);
            propRt.anchoredPosition = Vector2.zero;

            var propImg = propGo.GetComponent<Image>();
            propImg.sprite = propellerSprite;
            propImg.preserveAspect = true;
            propImg.raycastTarget = false;
            propImg.color = Color.white;

            propellerView = propGo.GetComponent<PatchBotPropellerView>();
            propellerView.StartActivationSpin(board.PatchBotPropellerFlightSpeed);
        }

        return new AirborneBot
        {
            sourceCell = template.sourceCell,
            sourceType = template.sourceType,
            ghost = ghost,
            rect = rect,
            flightRoot = flightRoot,
            image = image,
            hoverAnchor = hoverAnchor,
            propellerView = propellerView
        };
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

        // image.sprite = sprite != null ? sprite : board.GetSpecialIcon(TileSpecial.PatchBot);
        image.sprite = sprite != null
            ? sprite
            : board.GetPatchBotFlightIcon() != null
                ? board.GetPatchBotFlightIcon()
                : board.GetSpecialIcon(TileSpecial.PatchBot);
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        int side = ((cell.x + cell.y + index) & 1) == 0 ? -1 : 1;
        float lateral = board.TileSize * (0.46f + 0.08f * (index % 3));
        float altitude = board.TileSize * (0.90f + 0.05f * (index % 2));
        Vector2 start = rect.anchoredPosition;
        Vector2 hoverAnchor = start + new Vector2(side * lateral, altitude);

        RectTransform carryRt = null;
        if (impactMode == AirborneImpactMode.PulseCoreAtTarget)
        {
            var pulseSprite = board.GetSpecialIcon(TileSpecial.PulseCore);
            if (pulseSprite != null)
            {
                var carryGo = new GameObject("PatchbotCarryPulse", typeof(RectTransform), typeof(Image));
                carryGo.transform.SetParent(rect, false);

                carryRt = carryGo.GetComponent<RectTransform>();
                var carryImg = carryGo.GetComponent<Image>();

                float carrySize = board.TileSize * 0.72f;
                carryRt.anchorMin = new Vector2(0.5f, 0.5f);
                carryRt.anchorMax = new Vector2(0.5f, 0.5f);
                carryRt.pivot = new Vector2(0.5f, 0.5f);
                carryRt.sizeDelta = new Vector2(carrySize, carrySize);
                carryRt.anchoredPosition = Vector2.down * (board.TileSize * 0.32f);
                carryRt.localRotation = Quaternion.identity;

                carryImg.sprite = pulseSprite;
                carryImg.preserveAspect = true;
                carryImg.raycastTarget = false;
                carryImg.color = Color.white;
            }
        }

        PatchBotPropellerView propellerView = null;
        var propellerSprite = board.PatchBotPropellerSprite;
        if (propellerSprite != null)
        {
            var propGo = new GameObject("PatchBotPropeller",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PatchBotPropellerView));
            propGo.transform.SetParent(rect, false);

            var propRt = propGo.GetComponent<RectTransform>();
            propRt.anchorMin = new Vector2(0.5f, 0.5f);
            propRt.anchorMax = new Vector2(0.5f, 0.5f);
            propRt.pivot    = new Vector2(0.5f, 0.5f);
            propRt.sizeDelta = new Vector2(board.TileSize, board.TileSize);
            propRt.anchoredPosition = Vector2.zero;

            var propImg = propGo.GetComponent<Image>();
            propImg.sprite = propellerSprite;
            propImg.preserveAspect = true;
            propImg.raycastTarget = false;
            propImg.color = Color.white;

            propellerView = propGo.GetComponent<PatchBotPropellerView>();
            propellerView.StartActivationSpin(board.PatchBotPropellerFlightSpeed);
        }

        return new AirborneBot
        {
            sourceCell = cell,
            sourceType = tile.GetTileType(),
            ghost = ghost,
            rect = rect,
            flightRoot = flightRoot,
            image = image,
            hoverAnchor = hoverAnchor,
            carryRt = carryRt,
            propellerView = propellerView
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
            bot.rect.localScale = Vector3.one * Mathf.Lerp(1.00f, 2.5f, eased);
            UpdateCarryOrbit(bot, elapsed);
            yield return null;
        }

        while (bot != null && bot.rect != null && !bot.arrived && !bot.diving)
        {
            elapsed += Time.deltaTime;
            float wobbleX = Mathf.Sin(elapsed * 8.5f + phase) * board.TileSize * 0.055f;
            float wobbleY = Mathf.Sin(elapsed * 10.0f + phase) * board.TileSize * 0.035f;

            bot.rect.anchoredPosition = hover + new Vector2(wobbleX, wobbleY);
            bot.rect.localScale = Vector3.one * 2.5f;
            bot.rect.SetAsLastSibling();
            UpdateCarryOrbit(bot, elapsed);
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

            var resolved = coordinator.ResolveIntentFrom(bot.intent, bot.sourceCell);
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

        // ─── PHASE 2: Tüm dive'lar bitene kadar sabit süreli bekle ───
        yield return new WaitForSeconds(board.ApplySpecialChainTempo(SYNC_DIVE_DURATION));

        Debug.Log($"[OverridePatchBotAirborne] all_arrived");

        // ─── PHASE 2.5: Ghost'ları hedef konuma SNAP et ve ANINDA gizle ───
        // SetActive(false) immediate'tır — Object.Destroy'un aksine end-of-frame'e
        // ertelenmez. Bu sayede ghost kayboluşu ve taş kırılışı aynı frame'de başlar.
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot == null || !bot.hasTarget) continue;

            if (bot.rect != null)
                bot.rect.anchoredPosition = CellAnchored(bot.targetX, bot.targetY, bot.flightRoot);

            if (bot.ghost != null)
                bot.ghost.SetActive(false);
        }

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

            if (bot.intent != null)
            {
                coordinator.ReleaseIntent(bot.intent);
                bot.intent = null;
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
            // Ana coroutine ghost'u SetActive(false) yaptıysa devam etme.
            if (bot.ghost == null || !bot.ghost.activeSelf)
            {
                bot.arrived = true;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float curve = Mathf.Sin(t * Mathf.PI) * arc;
            float snap = Mathf.Sin(t * Mathf.PI * 2f) * board.TileSize * 0.025f;

            if (bot.rect != null)
            {
                bot.rect.anchoredPosition = Vector2.LerpUnclamped(start, end, eased) + normal * (curve + snap);
                bot.rect.localScale = Vector3.one * Mathf.Lerp(2.5f, 1.0f, t);
                bot.rect.SetAsLastSibling();
                UpdateCarryOrbit(bot, elapsed);
            }

            yield return null;
        }

        bot.arrived = true;
    }

    private void UpdateCarryOrbit(AirborneBot bot, float elapsed)
    {
        if (bot.carryRt == null)
            return;

        float footOffset = board.TileSize * 0.32f;
        bot.carryRt.anchoredPosition = Vector2.down * footOffset;
        bot.carryRt.localRotation = Quaternion.identity;
    }

    private void DestroyGhosts(List<AirborneBot> bots)
    {
        if (bots == null) return;

        for (int i = 0; i < bots.Count; i++)
        {
            var ghost = bots[i]?.ghost;
            if (ghost != null)
                Object.Destroy(ghost);
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