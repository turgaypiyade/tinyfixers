using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpecialResolver
{
    private readonly BoardController board;
    private readonly MatchFinder matchFinder;
    private readonly BoardAnimator boardAnimator;
    private readonly PulseCoreImpactService pulseCoreImpactService;
    private HashSet<Vector2Int> specialAffectedCells;
    private readonly List<TopHudController.ActiveGoal> activeGoalsBuffer = new();

    public SpecialResolver(BoardController board, MatchFinder matchFinder, BoardAnimator boardAnimator, PulseCoreImpactService pulseCoreImpactService)
    {
        this.board = board;
        this.matchFinder = matchFinder;
        this.boardAnimator = boardAnimator;
        this.pulseCoreImpactService = pulseCoreImpactService;
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
        specialAffectedCells = new HashSet<Vector2Int>();

        var affected = new HashSet<TileView> { a, b };
        MarkAffectedCell(a);
        MarkAffectedCell(b);
        var processed = new HashSet<TileView>();
        bool hasLineActivation = false;
        var lightningVisualTargets = new HashSet<TileView>(); // lightning only for Line path tiles
        var queued = new HashSet<TileView>();
        var queue = new Queue<SpecialActivation>();

        TileSpecial sa = a.GetSpecial();
        TileSpecial sb = b.GetSpecial();

        bool saIsLine  = sa == TileSpecial.LineH || sa == TileSpecial.LineV;
        bool sbIsLine  = sb == TileSpecial.LineH || sb == TileSpecial.LineV;
        bool saIsPulse = sa == TileSpecial.PulseCore;
        bool sbIsPulse = sb == TileSpecial.PulseCore;
        bool suppressPulseImpactAnimations = saIsPulse && sbIsPulse;

        // Pulse + Line combo'da base LightningStrike animasyonunu kapatacağız
        bool suppressBaseLightning = (saIsLine && sbIsPulse) || (sbIsLine && saIsPulse);

        if (!suppressBaseLightning)
        {
            hasLineActivation = hasLineActivation || saIsLine || sbIsLine;
        }


        if (sa != TileSpecial.None && sb != TileSpecial.None)
        {
            ApplyComboEffect(affected, queue, queued, processed, a, b, sa, sb, lightningVisualTargets);
            processed.Add(a);
            processed.Add(b);
        }
        else
        {
            var specialTile = sa != TileSpecial.None ? a : b;
            var partnerTile = sa != TileSpecial.None ? b : a;
            EnqueueActivation(queue, queued, specialTile, partnerTile);
        }

        // Pulse + Line combo’da: önce combo VFX görünsün, sonra lightning/clear başlasın
        if (suppressBaseLightning) // bu değişken sende zaten var (Pulse+Line tespiti)
        {
            yield return new WaitForSeconds(0.20f); // 0.15–0.30 arası tune edebilirsin
        }

        EnqueueChainSpecials(affected, queue, queued, processed);

        while (queue.Count > 0)
        {
            var activation = queue.Dequeue();
            queued.Remove(activation.special);
            if (activation.special == null || processed.Contains(activation.special)) continue;

            processed.Add(activation.special);
            ApplySpecialActivation(affected, activation.special, activation.partner, ref hasLineActivation, lightningVisualTargets);
            EnqueueChainSpecials(affected, queue, queued, processed);
        }

        Dictionary<TileView, float> stagger = suppressPulseImpactAnimations
            ? null
            : pulseCoreImpactService.BuildStaggerDelays(affected, processed);
        var animationMode = hasLineActivation ? ClearAnimationMode.LightningStrike : ClearAnimationMode.Default;
        // Special zincirinde yalnızca gerçekten etkilenen hücreler hasar alsın.
        // Komşu over-tile blocker ek hasarı, satır/sütun special'larda yan hücrelerde
        // beklenmeyen stage düşüşüne neden olabiliyor.
        yield return board.StartCoroutine(boardAnimator.ClearMatchesAnimated(affected, doShake: true, staggerDelays: stagger, staggerAnimTime: board.PulseImpactAnimTime, animationMode: animationMode, affectedCells: specialAffectedCells, includeAdjacentOverTileBlockerDamage: false, lightningVisualTargets: lightningVisualTargets));
        yield return board.StartCoroutine(boardAnimator.CollapseAndSpawnAnimated());
        board.IsSpecialActivationPhase = false;
        specialAffectedCells = null;
    }

    public void ExpandSpecialChain(HashSet<TileView> affected, HashSet<Vector2Int> affectedCells, out bool hasLineActivation, out bool hasAnySpecialActivation)
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
            ApplySpecialActivation(affected, activation.special, activation.partner, ref hasLineActivation);
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

    void ApplySpecialActivation(HashSet<TileView> matches, TileView specialTile, TileView partnerTile, ref bool hasLineActivation, HashSet<TileView> lightningVisualTargets = null)
    {
        if (specialTile == null) return;
        switch (specialTile.GetSpecial())
        {
            case TileSpecial.LineH:
            case TileSpecial.LineV:
                hasLineActivation = true;
                AddLineEffect(matches, specialTile, specialTile.GetSpecial());
                if (lightningVisualTargets != null)
                    AddLineEffect(lightningVisualTargets, specialTile, specialTile.GetSpecial());
                break;
            case TileSpecial.PulseCore:
                AddSquare(matches, specialTile.X, specialTile.Y, 2);
                break;
            case TileSpecial.SystemOverride:
                var type = partnerTile != null ? partnerTile.GetTileType() : specialTile.GetTileType();
                AddAllOfType(matches, type);
                break;
            case TileSpecial.PatchBot:
                if (partnerTile != null)
                    ApplyPatchBotTeleportHit(matches, specialTile, partnerTile, lightningVisualTargets);
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

    void ApplyPatchBotTeleportHit(HashSet<TileView> matches, TileView patchBotTile, TileView partnerTile, HashSet<TileView> lightningVisualTargets = null)
    {
        if (patchBotTile == null || partnerTile == null) return;

        var target = FindPatchBotTarget(patchBotTile, partnerTile, null);
        if (!target.hasCell) return;

        bool partnerIsSpecial = partnerTile.GetSpecial() != TileSpecial.None;
        PlayTeleportMarkers(patchBotTile, target.x, target.y);

        if (partnerIsSpecial)
        {
            TriggerPartnerEffectAt(matches, patchBotTile, partnerTile, target.x, target.y, lightningVisualTargets);
            return;
        }

        ApplyPatchBotTeleportToCell(matches, patchBotTile, target.x, target.y);
    }

    void ApplyPatchBotTeleportToCell(HashSet<TileView> matches, TileView patchBotTile, int targetX, int targetY)
    {
        if (patchBotTile == null) return;
        if (targetX < 0 || targetX >= board.Width || targetY < 0 || targetY >= board.Height) return;
        if (board.Holes[targetX, targetY]) return;

        int sourceX = patchBotTile.X;
        int sourceY = patchBotTile.Y;
        var tileAtTarget = board.Tiles[targetX, targetY];

        board.Tiles[sourceX, sourceY] = null;
        board.Tiles[targetX, targetY] = patchBotTile;
        patchBotTile.SetCoords(targetX, targetY);
        patchBotTile.SnapToGrid(board.TileSize);

        HitCellOnce(matches, targetX, targetY, tileAtTarget);

        MarkAffectedCell(sourceX, sourceY);
        MarkAffectedCell(targetX, targetY);
        board.RefreshTileObstacleVisual(patchBotTile);
    }

    void TriggerPartnerEffectAt(HashSet<TileView> matches, TileView patchBotTile, TileView partnerTile, int originX, int originY, HashSet<TileView> lightningVisualTargets = null)
    {
        if (partnerTile == null) return;
        var special = partnerTile.GetSpecial();
        if (special == TileSpecial.None) return;

        if (special == TileSpecial.LineH || special == TileSpecial.LineV)
        {
            if (special == TileSpecial.LineH)
                AddRow(matches, originY);
            else
                AddCol(matches, originX);

            if (lightningVisualTargets != null)
            {
                if (special == TileSpecial.LineH)
                    AddRow(lightningVisualTargets, originY);
                else
                    AddCol(lightningVisualTargets, originX);
            }

            return;
        }

        if (special == TileSpecial.PulseCore)
        {
            AddSquare(matches, originX, originY, 2);
            return;
        }

        if (special == TileSpecial.SystemOverride)
        {
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

        var target = FindPatchBotTarget(autoPatchBot, patchBotTile, null, systemOverrideTile);
        if (!target.hasCell) return;

        PlayTeleportMarkers(autoPatchBot, target.x, target.y);
        HitCellOnce(matches, target.x, target.y, target.tile);
    }

    void HitCellOnce(HashSet<TileView> matches, int x, int y, TileView tileAtCell = null)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return;
        if (board.Holes[x, y] && (board.ObstacleStateService == null || !board.ObstacleStateService.HasObstacleAt(x, y))) return;

        var obstacleService = board.ObstacleStateService;
        if (obstacleService != null && obstacleService.GetObstacleIdAt(x, y) != ObstacleId.None)
        {
            MarkAffectedCell(x, y);
            return;
        }

        var tile = tileAtCell ?? board.Tiles[x, y];
        if (tile == null) return;

        matches.Add(tile);
        MarkAffectedCell(tile);
    }

    (TileView tile, int x, int y, bool hasCell) FindPatchBotTarget(TileView patchBotTile, TileView partnerTile, HashSet<TileView> excluded, params TileView[] additionalExcluded)
    {
        var goalCells = new List<(int x, int y, TileView tile)>();
        var obstacleCells = new List<(int x, int y, TileView tile)>();
        var normalCells = new List<(int x, int y, TileView tile)>();

        var activeGoals = board.TopHud;
        activeGoalsBuffer.Clear();
        activeGoals?.GetActiveGoals(activeGoalsBuffer);

        bool IsExcluded(TileView tile)
        {
            if (tile == null) return true;
            if (tile == patchBotTile || tile == partnerTile) return true;
            if (excluded != null && excluded.Contains(tile)) return true;
            if (additionalExcluded != null)
            {
                for (int i = 0; i < additionalExcluded.Length; i++)
                {
                    if (tile == additionalExcluded[i]) return true;
                }
            }
            return false;
        }

        bool IsGoalCell(int x, int y, TileView tile)
        {
            for (int i = 0; i < activeGoalsBuffer.Count; i++)
            {
                var goal = activeGoalsBuffer[i];
                if (goal.targetType == LevelGoalTargetType.Tile)
                {
                    if (tile != null && tile.GetTileType() == goal.tileType)
                        return true;
                    continue;
                }

                if (board.ObstacleStateService != null && board.ObstacleStateService.GetObstacleIdAt(x, y) == goal.obstacleId)
                    return true;
            }
            return false;
        }

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y] && (board.ObstacleStateService == null || !board.ObstacleStateService.HasObstacleAt(x, y))) continue;

                var tile = board.Tiles[x, y];
                if (tile != null && IsExcluded(tile)) continue;

                if (IsGoalCell(x, y, tile))
                    goalCells.Add((x, y, tile));

                if (board.ObstacleStateService != null && board.ObstacleStateService.GetObstacleIdAt(x, y) != ObstacleId.None)
                    obstacleCells.Add((x, y, tile));
                else if (tile != null)
                    normalCells.Add((x, y, tile));
            }

        if (goalCells.Count > 0)
        {
            var pick = goalCells[Random.Range(0, goalCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (obstacleCells.Count > 0)
        {
            var pick = obstacleCells[Random.Range(0, obstacleCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (normalCells.Count > 0)
        {
            var pick = normalCells[Random.Range(0, normalCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        return (null, -1, -1, false);
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

    void ApplyComboEffect(HashSet<TileView> matches, Queue<SpecialActivation> queue, HashSet<TileView> queued, HashSet<TileView> processed, TileView a, TileView b, TileSpecial sa, TileSpecial sb, HashSet<TileView> lightningVisualTargets = null)
    {
        bool IsLine(TileSpecial s) => s == TileSpecial.LineH || s == TileSpecial.LineV;
        bool IsPulse(TileSpecial s) => s == TileSpecial.PulseCore;
        bool IsPatchBot(TileSpecial s) => s == TileSpecial.PatchBot;
        bool IsOverride(TileSpecial s) => s == TileSpecial.SystemOverride;

        if (IsOverride(sa) && IsOverride(sb))
        {
            board.PlaySystemOverrideComboVfx(); 
            AddAllTiles(matches);
            return;
        }

        if (IsOverride(sa) || IsOverride(sb))
        {
            var overrideTile = IsOverride(sa) ? a : b;
            var otherTile = IsOverride(sa) ? b : a;

            if (otherTile == null || overrideTile == null)
                return;

            TileType baseType = overrideTile.GetOverrideBaseType(out var storedType) ? storedType : overrideTile.GetTileType();
            TileSpecial targetSpecial = otherTile.GetSpecial();

            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.Holes[x, y]) continue;
                    var tile = board.Tiles[x, y];
                    if (tile == null) continue;
                    if (!tile.GetTileType().Equals(baseType)) continue;
                    if (tile.GetSpecial() != TileSpecial.None) continue;

                    if (targetSpecial == TileSpecial.PatchBot)
                    {
                        tile.SetSpecial(TileSpecial.PatchBot);
                        AutoPatchBotTeleportHitAndVanish(matches, tile, otherTile, overrideTile);
                        continue;
                    }

                    tile.SetSpecial(targetSpecial);
                    matches.Add(tile);
                    MarkAffectedCell(tile);
                    EnqueueActivation(queue, queued, tile, otherTile);
                }

            return;
        }

        if (IsLine(sa) && IsLine(sb))
        {
            AddRow(matches, a.Y);
            AddCol(matches, a.X);
            if (lightningVisualTargets != null)
            {
                AddRow(lightningVisualTargets, a.Y);
                AddCol(lightningVisualTargets, a.X);
            }
            return;
        }

        if (IsLine(sa) && IsPatchBot(sb) || (IsLine(sb) && IsPatchBot(sa)))
        {
            var lineTile = IsLine(sa) ? a : b;
            var patchBotTile = IsPatchBot(sa) ? a : b;
            var target = FindPatchBotTarget(patchBotTile, lineTile, null);
            if (target.hasCell)
            {
                TeleportTile(lineTile, target.x, target.y);
                AddLineEffect(matches, lineTile, lineTile.GetSpecial());
                if (lightningVisualTargets != null)
                    AddLineEffect(lightningVisualTargets, lineTile, lineTile.GetSpecial());
            }
            return;
        }

        if (IsLine(sa) && IsPulse(sb) || (IsLine(sb) && IsPulse(sa)))
        {
            board.PlayPulseEmitterComboVfxAtCell(a.X, a.Y);
            AddRow(matches, a.Y - 1);
            AddRow(matches, a.Y);
            AddRow(matches, a.Y + 1);
            AddCol(matches, a.X - 1);
            AddCol(matches, a.X);
            AddCol(matches, a.X + 1);
            if (lightningVisualTargets != null)
            {
                AddRow(lightningVisualTargets, a.Y - 1);
                AddRow(lightningVisualTargets, a.Y);
                AddRow(lightningVisualTargets, a.Y + 1);
                AddCol(lightningVisualTargets, a.X - 1);
                AddCol(lightningVisualTargets, a.X);
                AddCol(lightningVisualTargets, a.X + 1);
            }
            return;
        }

        if (IsPatchBot(sa) && IsPatchBot(sb))
        {
            var usedTargets = new HashSet<TileView>();
            for (int i = 0; i < 3; i++)
            {
                var target = FindPatchBotTarget(a, b, usedTargets);
                if (!target.hasCell) break;
                if (target.tile != null)
                    usedTargets.Add(target.tile);
                PlayTeleportMarkers(a, target.x, target.y);
                HitCellOnce(matches, target.x, target.y, target.tile);
                TriggerPartnerEffectAt(matches, a, b, target.x, target.y);
            }
            return;
        }

        if ((IsPatchBot(sa) && IsPulse(sb)) || (IsPulse(sa) && IsPatchBot(sb)))
        {
            var pulseTile = IsPulse(sa) ? a : b;
            var patchBotTile = IsPatchBot(sa) ? a : b;
            var target = FindPatchBotTarget(patchBotTile, pulseTile, null);
            if (target.hasCell)
            {
                TeleportTile(pulseTile, target.x, target.y);
                AddSquareEven(matches, pulseTile.X, pulseTile.Y, board.PatchBotPulseComboSize);
            }
            return;
        }

        if (IsPulse(sa) && IsPulse(sb))
        {
                // Combo patlama VFX
            board.PlayPulsePulseExplosionVfxAtCell(a.X, a.Y);
            AddSquare(matches, a.X, a.Y, 3);
            return;
        }

        if (IsLine(sa) || IsLine(sb))
        {
            TileSpecial line = IsLine(sa) ? sa : sb;

            if (line == TileSpecial.LineH)
            {
                AddRow(matches, a.Y - 1);
                AddRow(matches, a.Y);
                AddRow(matches, a.Y + 1);
            }
            else
            {
                AddCol(matches, a.X - 1);
                AddCol(matches, a.X);
                AddCol(matches, a.X + 1);
            }
            return;
        }
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
