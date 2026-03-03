using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpecialResolver
{
    private readonly BoardController board;
    private readonly MatchFinder matchFinder;
    private readonly BoardAnimator boardAnimator;
    private readonly PulseCoreImpactService pulseCoreImpactService;
    private readonly PatchbotComboService patchbotComboService;
    private HashSet<Vector2Int> specialAffectedCells;

    // SystemOverride fan-out lightning (single shot) support
    private TileView overrideFanoutOrigin;
    private readonly List<TileView> overrideFanoutTargets = new();
    private float pendingOverrideOverrideClearDelay = 0f;
    private bool overrideForceDefaultClearAnim;
    private bool overrideSuppressPerTileClearVfx;
    private bool overrideFanoutShowSelectionPulse;
    private bool overrideFanoutPulseTriggeredByBeam;
    private readonly List<PendingOverrideImplant> pendingOverrideImplants = new();

    private readonly struct PendingOverrideImplant
    {
        public readonly TileView target;
        public readonly TileSpecial special;
        public readonly TileView partnerTile;
        public readonly TileView overrideTile;

        public PendingOverrideImplant(TileView target, TileSpecial special, TileView partnerTile, TileView overrideTile)
        {
            this.target = target;
            this.special = special;
            this.partnerTile = partnerTile;
            this.overrideTile = overrideTile;
        }
    }

    public SpecialResolver(BoardController board, MatchFinder matchFinder, BoardAnimator boardAnimator, PulseCoreImpactService pulseCoreImpactService)
    {
        this.board = board;
        this.matchFinder = matchFinder;
        this.boardAnimator = boardAnimator;
        this.pulseCoreImpactService = pulseCoreImpactService;
        patchbotComboService = new PatchbotComboService(board);
    }

    
    private static void HideTileVisualForCombo(TileView t)
    {
        if (t == null) return;

        // Hide only visually so the clear pipeline can still reference transforms for VFX.
        // CanvasGroup should exist on Tile root; if not, add one in prefab.
        if (!t.TryGetComponent<CanvasGroup>(out var cg))
            cg = t.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

public TileView TryCreateSpecial(HashSet<TileView> matches)
    {
        // 1) Eğer bu tur kullanıcı swap’inin ilk resolve turuysa: eski davranış (A/B'den winner seç)
        if (board.LastSwapUserMove && board.LastSwapA != null && board.LastSwapB != null)
        {
            board.LastSwapUserMove = false;

            TileSpecial aSpec = matchFinder.DecideSpecialAt(board.LastSwapA.X, board.LastSwapA.Y);
            TileSpecial bSpec = matchFinder.DecideSpecialAt(board.LastSwapB.X, board.LastSwapB.Y);

            (TileView winner, TileSpecial wSpec) = PickWinner(board.LastSwapA, aSpec, board.LastSwapB, bSpec);

            if (winner == null || wSpec == TileSpecial.None)
                return null;

            winner.SetSpecial(wSpec);
            if (wSpec == TileSpecial.SystemOverride)
                winner.SetOverrideBaseType(winner.GetTileType());

            // Special tile'ın kendisi patlamasın
            matches.Remove(winner);

            return winner;
        }

        // 2) Cascade turu: match setinin içinden en güçlü special adayını bul
        TileView bestTile = null;
        TileSpecial bestSpec = TileSpecial.None;
        int bestScore = 0;

        foreach (var t in matches)
        {
            if (t == null) continue;

            TileSpecial spec = matchFinder.DecideSpecialAt(t.X, t.Y);
            int score = SpecialScore(spec);

            if (score > bestScore)
            {
                bestScore = score;
                bestSpec = spec;
                bestTile = t;
            }
        }

        if (bestTile == null || bestSpec == TileSpecial.None)
            return null;

        bestTile.SetSpecial(bestSpec);
        if (bestSpec == TileSpecial.SystemOverride)
            bestTile.SetOverrideBaseType(bestTile.GetTileType());

        // Special tile'ın kendisi patlamasın
        matches.Remove(bestTile);

        return bestTile;
    }

    public int SpecialScore(TileSpecial s)
    {
        switch (s)
        {
            case TileSpecial.SystemOverride: return 60;
            case TileSpecial.PulseCore: return 50;
            case TileSpecial.LineH:
            case TileSpecial.LineV: return 30;
            case TileSpecial.PatchBot: return 20;
            default: return 0;
        }
    }

    public (TileView winner, TileSpecial spec) PickWinner(TileView a, TileSpecial aSpec, TileView b, TileSpecial bSpec)
    {
        int Score(TileSpecial s)
        {
            switch (s)
            {
                case TileSpecial.SystemOverride: return 60;
                case TileSpecial.PulseCore: return 50;
                case TileSpecial.LineH:
                case TileSpecial.LineV: return 30;
                case TileSpecial.PatchBot: return 20;
                default: return 0;
            }
        }

        int ascore = Score(aSpec);
        int bscore = Score(bSpec);

        if (ascore == 0 && bscore == 0) return (null, TileSpecial.None);
        if (ascore >= bscore) return (a, aSpec);
        return (b, bSpec);
    }

    public IEnumerator ResolveSpecialSwap(TileView a, TileView b)
    {
        board.ShakeNextClear = true;
        board.LastSwapUserMove = false;
        board.IsSpecialActivationPhase = true;

        TileSpecial sa = a.GetSpecial();
        TileSpecial sb = b.GetSpecial();

        bool saIsLine  = sa == TileSpecial.LineH || sa == TileSpecial.LineV;
        bool sbIsLine  = sb == TileSpecial.LineH || sb == TileSpecial.LineV;
        bool saIsPulse = sa == TileSpecial.PulseCore;
        bool sbIsPulse = sb == TileSpecial.PulseCore;

        // ✅ PULSE + LINE: toplu ClearMatchesAnimated yapma.
        if ((saIsPulse && sbIsLine) || (sbIsPulse && saIsLine))
        {
            var center = saIsPulse ? a : b;
            int cx = center.X;
            int cy = center.Y;

            // Bu coroutine zaten: lineTravel + step step clear + collapse + resolve yapıyor
            yield return board.StartCoroutine(board.PlayPulseEmitterComboAndClear(cx, cy));

            board.IsSpecialActivationPhase = false;
            yield break;
        }

        specialAffectedCells = new HashSet<Vector2Int>();

        // Reset SystemOverride fan-out state for this resolution
        overrideFanoutOrigin = null;
        overrideFanoutTargets.Clear();
        overrideForceDefaultClearAnim = false;
        overrideSuppressPerTileClearVfx = false;
        overrideFanoutShowSelectionPulse = false;
        overrideFanoutPulseTriggeredByBeam = false;
        pendingOverrideOverrideClearDelay = 0f;
        pendingOverrideImplants.Clear();

        var affected = new HashSet<TileView> { a, b };
        MarkAffectedCell(a);
        MarkAffectedCell(b);
        var processed = new HashSet<TileView>();
        bool hasLineActivation = false;
        var lightningVisualTargets = new HashSet<TileView>(); // lightning only for Line path tiles
        var lightningLineStrikes = new List<LightningLineStrike>();
        var queued = new HashSet<TileView>();
        var queue = new Queue<SpecialActivation>();

      //  TileSpecial sa = a.GetSpecial();
       // TileSpecial sb = b.GetSpecial();

/*        bool saIsLine  = sa == TileSpecial.LineH || sa == TileSpecial.LineV;
        bool sbIsLine  = sb == TileSpecial.LineH || sb == TileSpecial.LineV;
        bool saIsPulse = sa == TileSpecial.PulseCore;
        bool sbIsPulse = sb == TileSpecial.PulseCore;*/
        bool suppressPulseImpactAnimations = saIsPulse && sbIsPulse;
        bool suppressPerTileClearVfx = (saIsPulse && sbIsLine) || (sbIsPulse && saIsLine);

        // Satır/sütun etkisi üreten tüm özel zincirlerde hedefe lightning gidip ardından tile clear olsun.
        hasLineActivation = hasLineActivation || saIsLine || sbIsLine;


        if (sa != TileSpecial.None && sb != TileSpecial.None)
        {
            ApplyComboEffect(affected, queue, queued, processed, a, b, sa, sb, lightningVisualTargets, lightningLineStrikes);
            processed.Add(a);
            processed.Add(b);
        }
        else
        {
            var specialTile = sa != TileSpecial.None ? a : b;
            var partnerTile = sa != TileSpecial.None ? b : a;
            EnqueueActivation(queue, queued, specialTile, partnerTile);
        }

        EnqueueChainSpecials(affected, queue, queued, processed);

        while (queue.Count > 0)
        {
            var activation = queue.Dequeue();
            queued.Remove(activation.special);
            if (activation.special == null || processed.Contains(activation.special)) continue;

            processed.Add(activation.special);
            ApplySpecialActivation(affected, activation.special, activation.partner, ref hasLineActivation, lightningVisualTargets, lightningLineStrikes);
            EnqueueChainSpecials(affected, queue, queued, processed);
        }

        // SystemOverride: show a single fan-out lightning mark to all targets before clearing/activating.
        if (overrideFanoutOrigin != null && overrideFanoutTargets.Count > 0)
        {
            float lightningDur = board.PlayLightningStrikeForTiles(
                overrideFanoutTargets,
                originTile: overrideFanoutOrigin,
                visualTargets: overrideFanoutTargets,
                allowCondense: false,
                onTargetBeamSpawned: tile =>
                {
                    if (overrideFanoutShowSelectionPulse && tile != null)
                    {
                        overrideFanoutPulseTriggeredByBeam = true;
                        tile.PlaySelectionPulse(1.20f, 0.08f, 0.10f);
                    }

                    ApplyPendingOverrideImplantForTile(affected, queue, queued, tile);
                });

            // Wait at least a tiny bit so the mark is readable.
            yield return new WaitForSeconds(Mathf.Max(0.06f, lightningDur));

            if (overrideFanoutShowSelectionPulse && !overrideFanoutPulseTriggeredByBeam)
            {
                for (int i = 0; i < overrideFanoutTargets.Count; i++)
                    overrideFanoutTargets[i]?.PlaySelectionPulse(1.20f, 0.08f, 0.10f);
            }

            // Safety: any implant that did not get a beam callback (unexpected filtering, duplicates, etc.).
            if (pendingOverrideImplants.Count > 0)
                ApplyPendingOverrideImplants(affected, queue, queued);
        }
        else if (pendingOverrideImplants.Count > 0)
        {
            // Safety: if no fan-out visual was produced, still apply queued implants.
            ApplyPendingOverrideImplants(affected, queue, queued);
        }

        if (queue.Count > 0)
        {
            EnqueueChainSpecials(affected, queue, queued, processed);

            while (queue.Count > 0)
            {
                var activation = queue.Dequeue();
                queued.Remove(activation.special);
                if (activation.special == null || processed.Contains(activation.special)) continue;

                processed.Add(activation.special);
                ApplySpecialActivation(affected, activation.special, activation.partner, ref hasLineActivation, lightningVisualTargets, lightningLineStrikes);
                EnqueueChainSpecials(affected, queue, queued, processed);
            }
        }


        // If Override+Override combo VFX played, wait for it to finish before clearing tiles.
        if (pendingOverrideOverrideClearDelay > 0f)
            yield return new WaitForSeconds(pendingOverrideOverrideClearDelay);

        Dictionary<TileView, float> stagger = suppressPulseImpactAnimations
            ? null
            : pulseCoreImpactService.BuildStaggerDelays(affected, processed);
        var animationMode = (hasLineActivation && !overrideForceDefaultClearAnim)
            ? ClearAnimationMode.LightningStrike
            : ClearAnimationMode.Default;
        // Special zincirinde yalnızca gerçekten etkilenen hücreler hasar alsın.
        // Komşu over-tile blocker ek hasarı, satır/sütun special'larda yan hücrelerde
        // beklenmeyen stage düşüşüne neden olabiliyor.
        yield return board.StartCoroutine(boardAnimator.ClearMatchesAnimated(
            affected,
            doShake: true,
            staggerDelays: stagger,
            staggerAnimTime: board.PulseImpactAnimTime,
            animationMode: animationMode,
            affectedCells: specialAffectedCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: lightningVisualTargets,
            lightningLineStrikes: lightningLineStrikes,
            suppressPerTileClearVfx: (suppressPerTileClearVfx || overrideSuppressPerTileClearVfx)));
        yield return board.StartCoroutine(boardAnimator.CollapseAndSpawnAnimated());
        board.IsSpecialActivationPhase = false;
        specialAffectedCells = null;
    }

   // public void ExpandSpecialChain(HashSet<TileView> affected, HashSet<Vector2Int> affectedCells, out bool hasLineActivation, out bool hasAnySpecialActivation)
    public void ExpandSpecialChain(
        HashSet<TileView> affected,
        HashSet<Vector2Int> affectedCells,
        out bool hasLineActivation,
        out bool hasAnySpecialActivation,
        HashSet<TileView> lightningVisualTargets = null,
        List<LightningLineStrike> lightningLineStrikes = null)

    {
        hasLineActivation = false;
        hasAnySpecialActivation = false;
        if (affected == null || affected.Count == 0)
            return;

        bool previousSpecialPhase = board.IsSpecialActivationPhase;
        var previousAffectedCells = specialAffectedCells;

        board.IsSpecialActivationPhase = true;
        specialAffectedCells = affectedCells ?? new HashSet<Vector2Int>();

        var processed = new HashSet<TileView>();
        var queued = new HashSet<TileView>();
        var queue = new Queue<SpecialActivation>();

        if (affectedCells != null)
        {
            foreach (var cell in affectedCells)
            {
                if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) continue;
                var tileAtCell = board.Tiles[cell.x, cell.y];
                if (tileAtCell == null) continue;
                affected.Add(tileAtCell);
            }
        }

        foreach (var tile in affected)
        {
            if (tile == null) continue;
            MarkAffectedCell(tile);
            if (tile.GetSpecial() == TileSpecial.None) continue;
            EnqueueActivation(queue, queued, tile, null);
        }

        while (queue.Count > 0)
        {
            var activation = queue.Dequeue();
            queued.Remove(activation.special);
            if (activation.special == null || processed.Contains(activation.special)) continue;

            processed.Add(activation.special);
            hasAnySpecialActivation = true;
           // ApplySpecialActivation(affected, activation.special, activation.partner, ref hasLineActivation);
            ApplySpecialActivation(
            affected,
            activation.special,
            activation.partner,
            ref hasLineActivation,
            lightningVisualTargets,
            lightningLineStrikes);

            EnqueueChainSpecials(affected, queue, queued, processed);
        }

        board.IsSpecialActivationPhase = previousSpecialPhase;
        specialAffectedCells = previousAffectedCells;
    }

    void EnqueueActivation(Queue<SpecialActivation> queue, HashSet<TileView> queued, TileView special, TileView partner)
    {
        if (special == null || queued.Contains(special)) return;
        if (special.GetSpecial() == TileSpecial.None) return;
        queued.Add(special);
        queue.Enqueue(new SpecialActivation(special, partner));
    }

    void EnqueueChainSpecials(HashSet<TileView> affected, Queue<SpecialActivation> queue, HashSet<TileView> queued, HashSet<TileView> processed)
    {
        foreach (var tile in affected)
        {
            if (tile == null) continue;
            if (tile.GetSpecial() == TileSpecial.None) continue;
            if (processed.Contains(tile)) continue;
            EnqueueActivation(queue, queued, tile, null);
        }
    }

    void ApplySpecialActivation(HashSet<TileView> matches, TileView specialTile, TileView partnerTile, ref bool hasLineActivation, HashSet<TileView> lightningVisualTargets = null, List<LightningLineStrike> lightningLineStrikes = null)
    {
        if (specialTile == null) return;
        switch (specialTile.GetSpecial())
        {
            case TileSpecial.LineH:

            case TileSpecial.LineV:
                hasLineActivation = true;
                ApplyLineAt(matches, specialTile.X, specialTile.Y, specialTile.GetSpecial(), lightningVisualTargets, lightningLineStrikes);
                break;
            case TileSpecial.PulseCore:
                AddSquare(matches, specialTile.X, specialTile.Y, 1); //efected grid 3X3
                break;
            case TileSpecial.SystemOverride:
                {
                    TileType type;
                    if (partnerTile != null) type = partnerTile.GetTileType();
                    else if (specialTile.GetOverrideBaseType(out var storedType)) type = storedType;
                    else type = specialTile.GetTileType();
                    // Single-shot fan-out lightning: mark all targets first, then clear together.
                    overrideFanoutOrigin = specialTile;
                    CollectAllOfType(overrideFanoutTargets, type, excludeSpecials: false);
                    overrideForceDefaultClearAnim = true;            // avoid sequential LightningStrike mode
                    overrideSuppressPerTileClearVfx = true;          // let the fan-out read cleanly
                    AddAllOfType(matches, type);
                    break;
                }
            case TileSpecial.PatchBot:
                if (partnerTile != null)
                    ApplyPatchBotTeleportHit(matches, specialTile, partnerTile, lightningVisualTargets, lightningLineStrikes);
                else
                    ApplyPatchBotSoloHit(matches, specialTile);   // ✅
                break;
        }
    }

    void AddLineEffect(HashSet<TileView> matches, TileView origin, TileSpecial line)
    {
        if (origin == null) return;
        if (line == TileSpecial.LineH)
            AddRow(matches, origin.Y);
        else if (line == TileSpecial.LineV)
            AddCol(matches, origin.X);
    }

// Centralized helpers so line-style specials (LineH/LineV and any combos that rely on LineTravel)
// don't have their row/col + strike logic copy-pasted across combo branches.
    void ApplyLineAt(
        HashSet<TileView> matches,
        int x, int y,
        TileSpecial line,
        HashSet<TileView> lightningVisualTargets,
        List<LightningLineStrike> lightningLineStrikes)
    {
        if (line == TileSpecial.LineH)
        {
            AddRow(matches, y);
            AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineH);
            if (lightningVisualTargets != null) AddRow(lightningVisualTargets, y);
        }
        else if (line == TileSpecial.LineV)
        {
            AddCol(matches, x);
            AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineV);
            if (lightningVisualTargets != null) AddCol(lightningVisualTargets, x);
        }
    }

    void ApplyFatLineAt(
        HashSet<TileView> matches,
        int x, int y,
        TileSpecial line,
        HashSet<TileView> lightningVisualTargets,
        List<LightningLineStrike> lightningLineStrikes)
    {
        if (line == TileSpecial.LineH)
        {
            AddRow(matches, y - 1);
            AddRow(matches, y);
            AddRow(matches, y + 1);
            AddLineStrike(lightningLineStrikes, x, y - 1, TileSpecial.LineH);
            AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineH);
            AddLineStrike(lightningLineStrikes, x, y + 1, TileSpecial.LineH);

            if (lightningVisualTargets != null)
            {
                AddRow(lightningVisualTargets, y - 1);
                AddRow(lightningVisualTargets, y);
                AddRow(lightningVisualTargets, y + 1);
            }
        }
        else if (line == TileSpecial.LineV)
        {
            AddCol(matches, x - 1);
            AddCol(matches, x);
            AddCol(matches, x + 1);
            AddLineStrike(lightningLineStrikes, x - 1, y, TileSpecial.LineV);
            AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineV);
            AddLineStrike(lightningLineStrikes, x + 1, y, TileSpecial.LineV);

            if (lightningVisualTargets != null)
            {
                AddCol(lightningVisualTargets, x - 1);
                AddCol(lightningVisualTargets, x);
                AddCol(lightningVisualTargets, x + 1);
            }
        }
    }

    void ApplyCrossAt(
        HashSet<TileView> matches,
        int x, int y,
        HashSet<TileView> lightningVisualTargets,
        List<LightningLineStrike> lightningLineStrikes)
    {
        AddRow(matches, y);
        AddCol(matches, x);
        AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineH);
        AddLineStrike(lightningLineStrikes, x, y, TileSpecial.LineV);

        if (lightningVisualTargets != null)
        {
            AddRow(lightningVisualTargets, y);
            AddCol(lightningVisualTargets, x);
        }
    }

    void ApplyPulseLineAt(
        HashSet<TileView> matches,
        int cx, int cy,
        HashSet<TileView> lightningVisualTargets,
        List<LightningLineStrike> lightningLineStrikes)
    {
        // 3 rows + 3 cols sweep (LineTravel)
        AddRow(matches, cy - 1);
        AddRow(matches, cy);
        AddRow(matches, cy + 1);
        AddCol(matches, cx - 1);
        AddCol(matches, cx);
        AddCol(matches, cx + 1);

        AddLineStrike(lightningLineStrikes, cx, cy - 1, TileSpecial.LineH);
        AddLineStrike(lightningLineStrikes, cx, cy, TileSpecial.LineH);
        AddLineStrike(lightningLineStrikes, cx, cy + 1, TileSpecial.LineH);
        AddLineStrike(lightningLineStrikes, cx - 1, cy, TileSpecial.LineV);
        AddLineStrike(lightningLineStrikes, cx, cy, TileSpecial.LineV);
        AddLineStrike(lightningLineStrikes, cx + 1, cy, TileSpecial.LineV);

        if (lightningVisualTargets != null)
        {
            AddRow(lightningVisualTargets, cy - 1);
            AddRow(lightningVisualTargets, cy);
            AddRow(lightningVisualTargets, cy + 1);
            AddCol(lightningVisualTargets, cx - 1);
            AddCol(lightningVisualTargets, cx);
            AddCol(lightningVisualTargets, cx + 1);
        }
    }

    void ApplyPatchBotTeleportHit(HashSet<TileView> matches, TileView patchBotTile, TileView partnerTile, HashSet<TileView> lightningVisualTargets = null, List<LightningLineStrike> lightningLineStrikes = null)
    {
        if (patchBotTile == null || partnerTile == null) return;

        var target = patchbotComboService.FindTarget(patchBotTile, partnerTile, null);
        if (!target.hasCell) return;

        patchbotComboService.EnqueueDash(patchBotTile, target.x, target.y);

        bool partnerIsSpecial = partnerTile.GetSpecial() != TileSpecial.None;
        PlayTeleportMarkers(patchBotTile, target.x, target.y);

        if (partnerIsSpecial)
        {
            TriggerPartnerEffectAt(matches, patchBotTile, partnerTile, target.x, target.y, lightningVisualTargets, lightningLineStrikes);
            return;
        }

        ApplyPatchBotTeleportToCell(matches, patchBotTile, partnerTile, target.x, target.y);
    }

    void ApplyPatchBotTeleportToCell(HashSet<TileView> matches, TileView patchBotTile, TileView partnerTile, int targetX, int targetY)
    {
        if (patchBotTile == null || partnerTile == null) return;
        if (targetX < 0 || targetX >= board.Width || targetY < 0 || targetY >= board.Height) return;

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(targetX, targetY);
        if (board.Holes[targetX, targetY] && !hasObstacleAtTarget) return;

        patchbotComboService.ConsumeSwapSource(matches, patchBotTile, partnerTile, MarkAffectedCell);
        patchbotComboService.ResolveTargetImpact(matches, targetX, targetY, hasObstacleAtTarget, MarkAffectedCell, MarkAffectedCell);
    }

    void TriggerPartnerEffectAt(HashSet<TileView> matches, TileView patchBotTile, TileView partnerTile, int originX, int originY, HashSet<TileView> lightningVisualTargets = null, List<LightningLineStrike> lightningLineStrikes = null)
    {
        if (partnerTile == null) return;
        var special = partnerTile.GetSpecial();
        if (special == TileSpecial.None) return;

        if (special == TileSpecial.LineH || special == TileSpecial.LineV)
        {
            PlayTeleportMarkers(partnerTile, originX, originY);
            ApplyLineAt(matches, originX, originY, special, lightningVisualTargets, lightningLineStrikes);
            return;
        }

        if (special == TileSpecial.PulseCore)
        {
            PlayTeleportMarkers(partnerTile, originX, originY);
            AddSquare(matches, originX, originY, 2);
            return;
        }

        if (special == TileSpecial.SystemOverride)
        {
            PlayTeleportMarkers(partnerTile, originX, originY);
            TriggerSystemOverridePatchBotConversion(matches, patchBotTile, partnerTile);
        }
    }

    void TriggerSystemOverridePatchBotConversion(HashSet<TileView> matches, TileView patchBotTile, TileView systemOverrideTile)
    {
        if (systemOverrideTile == null) return;

        TileType baseType = systemOverrideTile.GetOverrideBaseType(out var storedType) ? storedType : systemOverrideTile.GetTileType();

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y]) continue;
                var tile = board.Tiles[x, y];
                if (tile == null) continue;
                if (tile == patchBotTile || tile == systemOverrideTile) continue;
                if (!tile.GetTileType().Equals(baseType)) continue;
                if (tile.GetSpecial() != TileSpecial.None) continue;

                tile.SetSpecial(TileSpecial.PatchBot);
                AutoPatchBotTeleportHitAndVanish(matches, tile, patchBotTile, systemOverrideTile);
            }
    }

    void AutoPatchBotTeleportHitAndVanish(HashSet<TileView> matches, TileView autoPatchBot, TileView patchBotTile, TileView systemOverrideTile)
    {
        if (autoPatchBot == null) return;

        matches.Add(autoPatchBot);
        MarkAffectedCell(autoPatchBot);

        var target = patchbotComboService.FindTarget(autoPatchBot, patchBotTile, null, systemOverrideTile);
        if (!target.hasCell) return;

        patchbotComboService.EnqueueDash(autoPatchBot, target.x, target.y);
        
        PlayTeleportMarkers(autoPatchBot, target.x, target.y);
        patchbotComboService.HitCellOnce(matches, target.x, target.y, target.tile, MarkAffectedCell, MarkAffectedCell);
    }

    void PlayTeleportMarkers(TileView sourceTile, int targetX, int targetY)
    {
        if (board.BoardVfxPlayer == null || sourceTile == null) return;

        static Vector3 WorldCenter(TileView tv)
        {
            if (tv == null) return Vector3.zero;
            var rt = tv.GetComponent<RectTransform>();
            if (rt != null) return rt.TransformPoint(rt.rect.center);
            return tv.transform.position;
        }

        Vector3 CellWorldCenterVia(Transform reference, int x, int y, float ts)
        {
            var local = new Vector3(x * ts + ts * 0.5f, -y * ts - ts * 0.5f, 0f);
            return reference.TransformPoint(local);
        }

        var fromWorld = WorldCenter(sourceTile);
        var targetTile = board.Tiles[targetX, targetY];
        Vector3 toWorld;
        if (targetTile != null)
            toWorld = WorldCenter(targetTile);
        else
        {
            var reference = sourceTile.transform.parent != null ? sourceTile.transform.parent : sourceTile.transform;
            toWorld = CellWorldCenterVia(reference, targetX, targetY, board.TileSize);
        }

        board.BoardVfxPlayer.PlayTeleportMarkers(toWorld, fromWorld);
    }


    void PlayTransientSpecialVisualAt(TileView sourceTile, int targetX, int targetY)
    {
        if (sourceTile == null) return;

        var sprite = sourceTile.GetIconSprite();
        if (sprite == null) return;

        var parent = board.Parent != null ? board.Parent : sourceTile.transform.parent as RectTransform;
        if (parent == null) return;

        var ghostGo = new GameObject("PatchBotSpecialGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var ghostRt = ghostGo.GetComponent<RectTransform>();
        ghostRt.SetParent(parent, false);
        ghostRt.anchorMin = new Vector2(0.5f, 0.5f);
        ghostRt.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRt.pivot = new Vector2(0.5f, 0.5f);
        ghostRt.sizeDelta = new Vector2(board.TileSize, board.TileSize);

        var image = ghostGo.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.95f);

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(targetX, targetY);
        float yOffset = hasObstacleAtTarget ? board.TileSize * 0.22f : 0f;
        ghostRt.anchoredPosition = new Vector2(targetX * board.TileSize + board.TileSize * 0.5f, -targetY * board.TileSize - board.TileSize * 0.5f + yOffset);
        ghostRt.localScale = hasObstacleAtTarget ? Vector3.one * 1.08f : Vector3.one;

        board.StartCoroutine(FadeAndDestroySpecialGhost(image, ghostRt, 0.24f));
    }

    IEnumerator FadeAndDestroySpecialGhost(UnityEngine.UI.Image image, RectTransform ghostRt, float duration)
    {
        float elapsed = 0f;
        Vector2 startPos = ghostRt != null ? ghostRt.anchoredPosition : Vector2.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            if (image != null)
            {
                var c = image.color;
                c.a = Mathf.Lerp(0.95f, 0f, t);
                image.color = c;
            }

            if (ghostRt != null)
            {
                float rise = board.TileSize * 0.08f * t;
                ghostRt.anchoredPosition = new Vector2(startPos.x, startPos.y + rise);
            }

            yield return null;
        }

        if (ghostRt != null)
            Object.Destroy(ghostRt.gameObject);
    }

    void TeleportTile(TileView tile, int targetX, int targetY)
    {
        
        if (tile == null) return;
        if (targetX < 0 || targetX >= board.Width || targetY < 0 || targetY >= board.Height) return;
        if (board.Holes[targetX, targetY]) return;

        var targetTile = board.Tiles[targetX, targetY];
        int sourceX = tile.X;
        int sourceY = tile.Y;
        PlayTeleportMarkers(tile, targetX, targetY);


        board.Tiles[sourceX, sourceY] = targetTile;
        if (targetTile != null)
        {
            targetTile.SetCoords(sourceX, sourceY);
            targetTile.SnapToGrid(board.TileSize);
        }

        board.Tiles[targetX, targetY] = tile;
        tile.SetCoords(targetX, targetY);
        tile.SnapToGrid(board.TileSize);

        board.RefreshTileObstacleVisual(tile);
        board.RefreshTileObstacleVisual(targetTile);
    }


    bool CanSpecialAffectCell(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (!board.Holes[x, y])
            return true;

        return board.ObstacleStateService != null && board.ObstacleStateService.HasObstacleAt(x, y);
    }

    void ApplyComboEffect(HashSet<TileView> matches, Queue<SpecialActivation> queue, HashSet<TileView> queued, HashSet<TileView> processed, TileView a, TileView b, TileSpecial sa, TileSpecial sb, HashSet<TileView> lightningVisualTargets = null, List<LightningLineStrike> lightningLineStrikes = null)
    {
        bool IsLine(TileSpecial s) => s == TileSpecial.LineH || s == TileSpecial.LineV;
        bool IsPulse(TileSpecial s) => s == TileSpecial.PulseCore;
        bool IsPatchBot(TileSpecial s) => s == TileSpecial.PatchBot;
        bool IsOverride(TileSpecial s) => s == TileSpecial.SystemOverride;

        if (IsOverride(sa) && IsOverride(sb))
        {
            // Only show Override+Override combo VFX when both tiles are real overrides (have stored base type)
            bool aHasBase = a != null && a.GetOverrideBaseType(out _);
            bool bHasBase = b != null && b.GetOverrideBaseType(out _);
            if (aHasBase && bHasBase)
            {
                // Hide the two source tiles immediately so the combo runner is the only visible representation.
                HideTileVisualForCombo(a);
                HideTileVisualForCombo(b);

                pendingOverrideOverrideClearDelay = Mathf.Max(pendingOverrideOverrideClearDelay, board.PlaySystemOverrideComboVfxAndGetDuration());
            }
            AddAllTiles(matches);
            return;
        }

        if (IsOverride(sa) || IsOverride(sb))
        {
            var overrideTile = IsOverride(sa) ? a : b;
            var otherTile = IsOverride(sa) ? b : a;

            if (otherTile == null || overrideTile == null)
                return;

            // Ensure swap sources are consumed (used)
            matches.Add(overrideTile);
            matches.Add(otherTile);
            MarkAffectedCell(overrideTile);
            MarkAffectedCell(otherTile);

            TileSpecial targetSpecial = otherTile.GetSpecial();
            bool targetIsLine = targetSpecial == TileSpecial.LineH || targetSpecial == TileSpecial.LineV;
            TileType baseType = targetSpecial == TileSpecial.None
                ? otherTile.GetTileType()
                : (overrideTile.GetOverrideBaseType(out var storedType) ? storedType : overrideTile.GetTileType());

            // Single-shot fan-out lightning from the override tile to all affected targets
            overrideFanoutOrigin = overrideTile;
            // Keep default clear unless partner is a line special; line combos must preserve LineTravel strikes.
            overrideForceDefaultClearAnim = !targetIsLine;
            overrideSuppressPerTileClearVfx = true;

            // Build conversion/clear list (only normal tiles of the base type)
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    if (!CanSpecialAffectCell(x, y)) continue;
                    var tile = board.Tiles[x, y];
                    if (tile == null) continue;
                    if (!tile.GetTileType().Equals(baseType)) continue;
                    if (tile.GetSpecial() != TileSpecial.None) continue;

                    overrideFanoutTargets.Add(tile);

                    if (targetSpecial == TileSpecial.None)
                    {
                        // Normal partner: fan-out hedefleri beam ulaştığında kısa bir seçilme pulse
                        // oynatıp ardından normal clear akışına bırak.
                        overrideFanoutShowSelectionPulse = true;
                        matches.Add(tile);
                        MarkAffectedCell(tile);
                        continue;
                    }

                    if (targetSpecial == TileSpecial.PatchBot)
                    {
                        pendingOverrideImplants.Add(new PendingOverrideImplant(tile, TileSpecial.PatchBot, otherTile, overrideTile));
                        continue;
                    }

                    // Special partner: beam targetında taşı dönüştür, sonra özel etkiyi zincire ekle.
                    pendingOverrideImplants.Add(new PendingOverrideImplant(tile, targetSpecial, otherTile, overrideTile));
                }

            return;
        }

        if (IsLine(sa) && IsLine(sb))
        {
            ApplyCrossAt(matches, a.X, a.Y, lightningVisualTargets, lightningLineStrikes);
            return;
        }

        if (IsLine(sa) && IsPatchBot(sb) || (IsLine(sb) && IsPatchBot(sa)))
        {
            var lineTile = IsLine(sa) ? a : b;
            var patchBotTile = IsPatchBot(sa) ? a : b;
            var target = patchbotComboService.FindTarget(patchBotTile, lineTile, null);
            if (target.hasCell)
            {
                patchbotComboService.EnqueueDash(patchBotTile, target.x, target.y);
                PlayTeleportMarkers(patchBotTile, target.x, target.y);
                PlayTeleportMarkers(lineTile, target.x, target.y);
                ApplyLineAt(matches, target.x, target.y, lineTile.GetSpecial(), lightningVisualTargets, lightningLineStrikes);
            }
            return;
        }

        if (IsLine(sa) && IsPulse(sb) || (IsLine(sb) && IsPulse(sa)))
        {
            // Pulse + Line: sadece 3 satır + 3 sütun taraması (LineTravel). Per-tile pop VFX kapatılacak.
            var center = IsPulse(sa) ? a : b;
            if (center == null) return;

            ApplyPulseLineAt(matches, center.X, center.Y, lightningVisualTargets, lightningLineStrikes);
            return;
        }

        if (IsPatchBot(sa) && IsPatchBot(sb))
        {
            var usedTargets = new HashSet<TileView>();

            var firstTarget = patchbotComboService.FindTarget(a, b, usedTargets);
            if (firstTarget.hasCell)
            {
                if (firstTarget.tile != null)
                    usedTargets.Add(firstTarget.tile);
                patchbotComboService.EnqueueDash(a, firstTarget.x, firstTarget.y);
                PlayTeleportMarkers(a, firstTarget.x, firstTarget.y);
                patchbotComboService.HitCellOnce(matches, firstTarget.x, firstTarget.y, firstTarget.tile, MarkAffectedCell, MarkAffectedCell);
            }

            var secondTarget = patchbotComboService.FindTarget(b, a, usedTargets);
            if (secondTarget.hasCell)
            {
                if (secondTarget.tile != null)
                    usedTargets.Add(secondTarget.tile);
                patchbotComboService.EnqueueDash(b, secondTarget.x, secondTarget.y);
                PlayTeleportMarkers(b, secondTarget.x, secondTarget.y);
                patchbotComboService.HitCellOnce(matches, secondTarget.x, secondTarget.y, secondTarget.tile, MarkAffectedCell, MarkAffectedCell);
            }
            return;
        }

        if ((IsPatchBot(sa) && IsPulse(sb)) || (IsPulse(sa) && IsPatchBot(sb)))
        {
            var pulseTile = IsPulse(sa) ? a : b;
            var patchBotTile = IsPatchBot(sa) ? a : b;
            var target = patchbotComboService.FindTarget(patchBotTile, pulseTile, null);
            if (target.hasCell)
            {
                patchbotComboService.EnqueueDash(patchBotTile, target.x, target.y);
                PlayTeleportMarkers(patchBotTile, target.x, target.y);
                PlayTeleportMarkers(pulseTile, target.x, target.y);
                //AddSquareEven(matches, target.x, target.y, board.PatchBotPulseComboSize);
                AddSquare(matches, target.x, target.y, 1); // 3x3
            }
            return;
        }

        if (IsPulse(sa) && IsPulse(sb))
        {
                // Combo patlama VFX
            board.PlayPulsePulseExplosionVfxAtCell(a.X, a.Y);
            AddSquare(matches, a.X, a.Y, 2); //5X5
            return;
        }

        if (IsLine(sa) || IsLine(sb))
        {
            TileSpecial line = IsLine(sa) ? sa : sb;
            // Keep legacy behavior: apply from tile 'a' coords (swap origin),
            // but route through a single helper to avoid copy-paste.
            ApplyFatLineAt(matches, a.X, a.Y, line, lightningVisualTargets, lightningLineStrikes);
            return;
        }

    }

    void ApplyPendingOverrideImplantForTile(HashSet<TileView> matches, Queue<SpecialActivation> queue, HashSet<TileView> queued, TileView target)
    {
        if (target == null || pendingOverrideImplants.Count == 0)
            return;

        for (int i = pendingOverrideImplants.Count - 1; i >= 0; i--)
        {
            var pending = pendingOverrideImplants[i];
            if (pending.target != target)
                continue;

            ApplyPendingOverrideImplant(matches, queue, queued, pending);
            pendingOverrideImplants.RemoveAt(i);
        }
    }

    void ApplyPendingOverrideImplants(HashSet<TileView> matches, Queue<SpecialActivation> queue, HashSet<TileView> queued)
    {
        for (int i = 0; i < pendingOverrideImplants.Count; i++)
            ApplyPendingOverrideImplant(matches, queue, queued, pendingOverrideImplants[i]);

        pendingOverrideImplants.Clear();
    }

    void ApplyPendingOverrideImplant(HashSet<TileView> matches, Queue<SpecialActivation> queue, HashSet<TileView> queued, PendingOverrideImplant pending)
    {
        if (pending.target == null)
            return;

        pending.target.SetSpecial(pending.special);

        if (pending.special == TileSpecial.PatchBot)
        {
            AutoPatchBotTeleportHitAndVanish(matches, pending.target, pending.partnerTile, pending.overrideTile);
            return;
        }

        matches.Add(pending.target);
        MarkAffectedCell(pending.target);
        EnqueueActivation(queue, queued, pending.target, pending.partnerTile);
    }


    void AddLineStrike(List<LightningLineStrike> lineStrikes, int x, int y, TileSpecial lineSpecial)
    {
        if (lineStrikes == null)
            return;

        bool isHorizontal = lineSpecial == TileSpecial.LineH;
        bool isVertical = lineSpecial == TileSpecial.LineV;
        if (!isHorizontal && !isVertical)
            return;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;

        lineStrikes.Add(new LightningLineStrike(new Vector2Int(x, y), isHorizontal));
    }

    void AddRow(HashSet<TileView> matches, int y)
    {
        if (y < 0 || y >= board.Height) return;
        for (int x = 0; x < board.Width; x++)
            if (CanSpecialAffectCell(x, y))
            {
                MarkAffectedCell(x, y);
                if (board.Tiles[x, y] != null)
                    matches.Add(board.Tiles[x, y]);
            }
    }

    void AddCol(HashSet<TileView> matches, int x)
    {
        if (x < 0 || x >= board.Width) return;
        for (int y = 0; y < board.Height; y++)
            if (CanSpecialAffectCell(x, y))
            {
                MarkAffectedCell(x, y);
                if (board.Tiles[x, y] != null)
                    matches.Add(board.Tiles[x, y]);
            }
    }

    void AddSquare(HashSet<TileView> matches, int cx, int cy, int radius)
    {
        for (int x = cx - radius; x <= cx + radius; x++)
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;
                if (!CanSpecialAffectCell(x, y)) continue;
                MarkAffectedCell(x, y);
                if (board.Tiles[x, y] != null) matches.Add(board.Tiles[x, y]);
            }
    }

    void AddSquareEven(HashSet<TileView> matches, int cx, int cy, int size)
    {
        if (size < 2) return;
        int half = size / 2;
        int startX = cx - (half - 1);
        int startY = cy - (half - 1);
        int endX = startX + size - 1;
        int endY = startY + size - 1;

        for (int x = startX; x <= endX; x++)
            for (int y = startY; y <= endY; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;
                if (!CanSpecialAffectCell(x, y)) continue;
                MarkAffectedCell(x, y);
                if (board.Tiles[x, y] != null) matches.Add(board.Tiles[x, y]);
            }
    }

    void AddAllTiles(HashSet<TileView> matches)
    {
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!CanSpecialAffectCell(x, y)) continue;
                MarkAffectedCell(x, y);
                if (board.Tiles[x, y] != null) matches.Add(board.Tiles[x, y]);
            }
    }

    
    void CollectAllOfType(List<TileView> buffer, TileType type, bool excludeSpecials)
    {
        if (buffer == null) return;
        buffer.Clear();

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!CanSpecialAffectCell(x, y)) continue;
                var t = board.Tiles[x, y];
                if (t == null) continue;
                if (!t.GetTileType().Equals(type)) continue;
                if (excludeSpecials && t.GetSpecial() != TileSpecial.None) continue;
                buffer.Add(t);
            }
    }

void AddAllOfType(HashSet<TileView> matches, TileType type)
    {
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!CanSpecialAffectCell(x, y)) continue;
                if (board.Tiles[x, y] != null && board.Tiles[x, y].GetTileType().Equals(type))
                {
                    MarkAffectedCell(x, y);
                    matches.Add(board.Tiles[x, y]);
                }
            }
    }

    void MarkAffectedCell(TileView tile)
    {
        if (tile == null) return;
        MarkAffectedCell(tile.X, tile.Y);
    }

    void MarkAffectedCell(int x, int y)
    {
        if (specialAffectedCells == null) return;
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return;
        if (!CanSpecialAffectCell(x, y)) return;
        specialAffectedCells.Add(new Vector2Int(x, y));
    }

    void ApplyPatchBotSoloHit(HashSet<TileView> matches, TileView patchBotTile)
    {
        if (patchBotTile == null) return;

        // PatchBot zaten clear edilecek; sadece kendi cell’ini affected olarak işaretlemek yeterli
        matches.Add(patchBotTile);
        MarkAffectedCell(patchBotTile);

        // 1 hedef seç ve vur
        var target = patchbotComboService.FindTarget(patchBotTile, null, null);
        if (!target.hasCell) return;

        patchbotComboService.EnqueueDash(patchBotTile, target.x, target.y);

        PlayTeleportMarkers(patchBotTile, target.x, target.y);

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(target.x, target.y);

        patchbotComboService.ResolveTargetImpact(matches, target.x, target.y, hasObstacleAtTarget, MarkAffectedCell, MarkAffectedCell);
    }

    
    readonly struct SpecialActivation
    {
        public readonly TileView special;
        public readonly TileView partner;

        public SpecialActivation(TileView special, TileView partner)
        {
            this.special = special;
            this.partner = partner;
        }
    }
}
