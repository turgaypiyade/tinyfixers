using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardAnimator
{
    private readonly BoardController board;
    private readonly TileClearEffectOrchestrator clearEffectOrchestrator;
    private readonly TileAnimator tileAnimator;

    private readonly Color lightningColor = new Color(0.70f, 0.90f, 1f, 1f);

    private static readonly List<BoardController.PatchbotDashRequest> _patchbotDashBuffer = new();
    private readonly List<IClearEffectPlayer> clearEffectPlayers = new List<IClearEffectPlayer>();
    private static readonly Vector2Int[] OrthogonalDirs =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] DiagonalDirs =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    // Cache WaitForSeconds instances to avoid GC allocations in frequently-called coroutines.
    // Keyed by milliseconds to keep lookups stable.
    private static readonly Dictionary<int, WaitForSeconds> _waitCache = new Dictionary<int, WaitForSeconds>(64);


    private static WaitForSeconds Wait(float seconds)
    {
        if (seconds <= 0f) return null;
        int ms = Mathf.Max(1, Mathf.RoundToInt(seconds * 1000f));
        if (_waitCache.TryGetValue(ms, out var w)) return w;
        w = new WaitForSeconds(ms / 1000f);
        _waitCache[ms] = w;
        return w;
    }

    public BoardAnimator(BoardController board)
    {
        this.board = board;
        tileAnimator = new TileAnimator(board);
        clearEffectOrchestrator = new TileClearEffectOrchestrator(
            new GoalFlyTileClearEffect(board, tileAnimator),
            new LightningStrikeTileClearEffect(board.BoardVfxPlayer, lightningColor, tileAnimator),
            new DefaultPopTileClearEffect(tileAnimator)
        );

        clearEffectPlayers.Add(new PulseWaveEffectPlayer());
        clearEffectPlayers.Add(new LineSweepEffectPlayer());
        clearEffectPlayers.Add(new OverrideRadialEffectPlayer());
        clearEffectPlayers.Add(new PatchBotDashEffectPlayer());
        clearEffectPlayers.Add(new SpecialCreationFormationEffectPlayer());
    }

    private void StartPatchbotDashRequests(IReadOnlyList<BoardController.PatchbotDashRequest> requests)
    {
        if (requests == null || requests.Count == 0)
            return;

        if (board.PatchbotDashUI != null)
        {
            board.PatchbotDashUI.PlayDashParallel(
                new List<BoardController.PatchbotDashRequest>(requests),
                board);
            return;
        }

        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            request.onArrived?.Invoke();
        }
    }

    private void StartPatchbotDashRequestsForLineCell(
        List<BoardController.PatchbotDashRequest> pendingRequests,
        Vector2Int cell)
    {
        if (pendingRequests == null || pendingRequests.Count == 0)
            return;

        List<BoardController.PatchbotDashRequest> readyRequests = null;
        for (int i = pendingRequests.Count - 1; i >= 0; i--)
        {
            var request = pendingRequests[i];
            if (request.from != cell)
                continue;

            readyRequests ??= new List<BoardController.PatchbotDashRequest>();
            readyRequests.Add(request);
            pendingRequests.RemoveAt(i);
        }

        if (readyRequests == null || readyRequests.Count == 0)
            return;

        readyRequests.Reverse();
        StartPatchbotDashRequests(readyRequests);
    }

    private void FlushPendingPatchbotDashRequests(
        List<BoardController.PatchbotDashRequest> pendingRequests)
    {
        if (pendingRequests == null || pendingRequests.Count == 0)
            return;

        StartPatchbotDashRequests(pendingRequests);
        pendingRequests.Clear();
    }

    /// <summary>
    /// Short "selected" pulse: scale up then back to original.
    /// Call this when a lightning/marker reaches a target to give feedback.
    /// </summary>
    public void PlaySelectionPulse(
        TileView tile,
        float delay = 0f,
        float peakScale = 1.12f,
        float upTime = 0.06f,
        float downTime = 0.08f)
    {
        tileAnimator?.PlaySelectionPulse(tile, delay, peakScale, upTime, downTime);
    }

    public IEnumerator PlaySpecialCreationMerge(
        TileView createdTile,
        IEnumerable<TileView> sourceTiles,
        float duration = -1f)
    {
        if (tileAnimator == null || createdTile == null)
            yield break;

        float animDuration;
        if (duration > 0f)
        {
            animDuration = duration;
        }
        else
        {
            // Amaç: özel taş oluşsun ama board'ı 0.18+ kadar kilitlemesin.
            // Clear ile fall arasına daha doğal otursun.
            float clearBased = board.ApplySpecialChainTempo(board.ClearDuration * 1.6f);
            float fallBased = board.FallDurationWithMultiplier * 0.70f;

            animDuration = Mathf.Clamp(
                Mathf.Max(clearBased, fallBased),
                0.08f,
                0.12f
            );
        }

        yield return tileAnimator.PlaySpecialCreationMerge(createdTile, sourceTiles, animDuration);
    }

    public IEnumerator SwapTilesAnimated(TileView a, TileView b, float duration)
    {
        yield return RunTogether(
            a.MoveToGrid(board.TileSize, duration, board.SwapMoveCurve),
            b.MoveToGrid(board.TileSize, duration, board.SwapMoveCurve)
        );
    }

    private IEnumerator RunTogether(IEnumerator c1, IEnumerator c2)
    {
        bool d1 = false, d2 = false;
        board.StartCoroutine(Wrap(c1, () => d1 = true));
        board.StartCoroutine(Wrap(c2, () => d2 = true));
        while (!d1 || !d2) yield return null;
    }

    private static Transform GetVisualTarget(TileView tile)
    {
        if (tile == null)
            return null;

        Image icon = tile.IconImage;
        if (icon != null && icon.transform != null && icon.transform != tile.transform)
            return icon.transform;

        return tile.transform;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private IEnumerator Wrap(IEnumerator c, Action onDone)
    {
        return SafeWrap(c, 0f, onDone);
    }

    private IEnumerator WrapWithDelay(IEnumerator c, float delay, Action onDone)
    {
        return SafeWrap(c, delay, onDone);
    }

    /// <summary>
    /// Steps through a coroutine manually so that exceptions are caught
    /// and onDone is always called — preventing RunMany from hanging.
    /// </summary>
    private IEnumerator SafeWrap(IEnumerator c, float delay, Action onDone)
    {
        if (delay > 0f)
        {
            var w = Wait(delay);
            if (w != null) yield return w;
        }

        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = c.MoveNext();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                break;
            }
            if (!hasNext) break;
            yield return c.Current;
        }

        onDone?.Invoke();
    }

    public IEnumerator RunMany(List<IEnumerator> routines)
    {
        int done = 0;
        for (int i = 0; i < routines.Count; i++)
            board.StartCoroutine(Wrap(routines[i], () => done++));

        while (done < routines.Count) yield return null;
    }

    public IEnumerator RunManyWithDelays(List<IEnumerator> routines, List<float> delays)
    {
        if (routines.Count != delays.Count)
        {
            yield return RunMany(routines);
            yield break;
        }

        int done = 0;
        for (int i = 0; i < routines.Count; i++)
            board.StartCoroutine(WrapWithDelay(routines[i], delays[i], () => done++));

        while (done < routines.Count) yield return null;
    }

    public IEnumerator ClearMatchesAnimated(
        HashSet<TileView> matches,
        bool doShake,
        Dictionary<TileView, float> staggerDelays = null,
        float staggerAnimTime = 0.16f,
        ClearAnimationMode animationMode = ClearAnimationMode.Default,
        HashSet<Vector2Int> affectedCells = null,
        IReadOnlyList<Vector2Int> explicitImpactCells = null,
        ObstacleHitContext? obstacleHitContext = null,
        bool includeAdjacentOverTileBlockerDamage = true,
        TileView lightningOriginTile = null,
        Vector2Int? lightningOriginCell = null,
        IReadOnlyCollection<TileView> lightningVisualTargets = null,
        IReadOnlyList<LightningLineStrike> lightningLineStrikes = null,
        bool suppressPerTileClearVfx = false,
        Dictionary<TileView, float> perTileClearDelays = null,
        Vector2Int? implodeTargetCell = null)
    {
        var list = new List<TileView>(matches);
        var pops = new List<IEnumerator>();
        var pulseImpacts = new List<IEnumerator>();
        var shouldClearTile = new Dictionary<TileView, bool>();
        var clearedByType = new Dictionary<TileType, int>();
        var lineHitClearedTiles = new HashSet<TileView>();
        var lineSweepCandidates = new HashSet<TileView>();
        var skipBreakFxTiles = new HashSet<TileView>();
        var lineHitDamagedObstacleCells = new HashSet<Vector2Int>();
        var implodeTiles = new List<TileView>();
        bool lineHitWindowOpen = false;

        float maxStaggerDelay = 0f;
        var impactCells = new List<Vector2Int>();
        var impactSourceTileTypes = new List<TileType?>();
        var obstacleDamageSources = new Dictionary<Vector2Int, List<TileType?>>();

        Debug.Log(
            $"[PulseClearDebug][BA] ENTER " +
            $"list={list.Count} " +
            $"mode={animationMode} " +
            $"specialPhase={board.IsSpecialActivationPhase} " +
            $"stagger={(staggerDelays != null ? staggerDelays.Count : 0)} " +
            $"perTile={(perTileClearDelays != null ? perTileClearDelays.Count : 0)} " +
            $"suppress={suppressPerTileClearVfx}");

        board.ConsumePatchbotDashRequests(_patchbotDashBuffer);
        List<BoardController.PatchbotDashRequest> lineSweepPatchbotDashes = null;

        // Line sweep modunda PatchBot taşına sıra gelene kadar beklenmeli,
        // ama sweep'i bloklamadan asenkron çalışmalı.
        bool hasLineStrikes = animationMode == ClearAnimationMode.LightningStrike
            && lightningLineStrikes != null && lightningLineStrikes.Count > 0;

        if (_patchbotDashBuffer.Count > 0)
        {
            if (hasLineStrikes)
            {
                lineSweepPatchbotDashes = new List<BoardController.PatchbotDashRequest>(_patchbotDashBuffer);
            }
            else
            {
                StartPatchbotDashRequests(_patchbotDashBuffer);
            }
        }

        ObstacleHitContext damageContext = obstacleHitContext ?? (board.IsSpecialActivationPhase
            ? ObstacleHitContext.SpecialActivation
            : ObstacleHitContext.NormalMatch);

        HashSet<TileView> lightningVisualSet = null;
        if (animationMode == ClearAnimationMode.LightningStrike && lightningVisualTargets != null)
            lightningVisualSet = new HashSet<TileView>(lightningVisualTargets);

        if (animationMode == ClearAnimationMode.LightningStrike)
        {
            SortTilesForLightning(list, lightningOriginTile, lightningOriginCell);
        }

        List<TileView> orderedStrikeTargets = null;
        if (animationMode == ClearAnimationMode.LightningStrike)
        {
            orderedStrikeTargets = lightningVisualTargets != null
                ? new List<TileView>(lightningVisualTargets)
                : new List<TileView>(list);

            SortTilesForLightning(orderedStrikeTargets, lightningOriginTile, lightningOriginCell);
        }

        float lightningStepDelay = animationMode == ClearAnimationMode.LightningStrike
            ? board.GetLightningStrikeStepDelay()
            : 0f;
        int lightningIndex = 0;

        if (explicitImpactCells != null && explicitImpactCells.Count > 0)
        {
            for (int i = 0; i < explicitImpactCells.Count; i++)
            {
                impactCells.Add(explicitImpactCells[i]);
                impactSourceTileTypes.Add(null);
            }
        }
        else
        {
            if (affectedCells != null)
            {
                foreach (var cell in affectedCells)
                {
                    impactCells.Add(cell);
                    impactSourceTileTypes.Add(null);
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                var tile = list[i];
                if (tile == null) continue;
                bool debugLive =
                tile.X >= 0 && tile.X < board.Width &&
                tile.Y >= 0 && tile.Y < board.Height &&
                board.Tiles[tile.X, tile.Y] == tile;

                bool debugHasStagger =
                    staggerDelays != null && staggerDelays.ContainsKey(tile);

                bool debugHasPerTile =
                    perTileClearDelays != null && perTileClearDelays.ContainsKey(tile);

                Debug.Log(
                    $"[PulseClearDebug][BA] LOOP tile=({tile.X},{tile.Y}) " +
                    $"type={tile.GetTileType()} special={tile.GetSpecial()} " +
                    $"live={debugLive} hasStagger={debugHasStagger} hasPerTile={debugHasPerTile}");

                impactCells.Add(new Vector2Int(tile.X, tile.Y));
                impactSourceTileTypes.Add(
                    damageContext == ObstacleHitContext.NormalMatch
                        ? tile.GetTileType()
                        : (TileType?)null);
            }
        }

        if (suppressPerTileClearVfx
            && animationMode == ClearAnimationMode.LightningStrike
            && (lightningLineStrikes == null || lightningLineStrikes.Count == 0))
        {
            suppressPerTileClearVfx = false;
        }

        bool useLineHitDrivenClear = animationMode == ClearAnimationMode.LightningStrike
            && lightningLineStrikes != null
            && lightningLineStrikes.Count > 0;

        if (useLineHitDrivenClear)
        {
            lineHitWindowOpen = true; // Sadece o spesifik hatlar için takip açılır.
        }


        for (int i = 0; i < list.Count; i++)
        {
            var tile = list[i];
            if (tile == null) continue;
            if (lineHitClearedTiles.Contains(tile)) continue;

            if (!board.IsSpecialActivationPhase && tile.GetSpecial() != TileSpecial.None)
                continue;

            bool clearTile = true;
            if (board.ObstacleStateService != null)
            {
                clearTile =
                    !board.ObstacleStateService.IsCellBlocked(tile.X, tile.Y) &&
                    !board.ObstacleStateService.IsInteractionLockedAt(tile.X, tile.Y);
            }

            shouldClearTile[tile] = clearTile;
            if (!clearTile) continue;

            bool useLightningEffect = animationMode == ClearAnimationMode.LightningStrike
                && (lightningVisualSet == null || lightningVisualSet.Contains(tile));

            // Tile eger gercekten Line tarafindan supurulecekse popup vs. baskilansin
            bool isSweptOff = useLineHitDrivenClear && useLightningEffect;
            bool shouldSuppressVfx = suppressPerTileClearVfx || isSweptOff;

            if (isSweptOff)
                lineSweepCandidates.Add(tile);

            if (!shouldSuppressVfx && staggerDelays != null && staggerDelays.TryGetValue(tile, out var d))
            {
                Debug.Log($"[PulseClearDebug][BA] STAGGER_BRANCH tile=({tile.X},{tile.Y}) delay={d:0.000}");
                pulseImpacts.Add(tileAnimator.PlayPulseImpact(tile, d, staggerAnimTime));
                if (d > maxStaggerDelay) maxStaggerDelay = d;
            }
            else
            {
                if (shouldSuppressVfx)
                    continue; // LineTravel / lightning sweep handles visuals; skip per-tile pop/fly/impact.

                // Goal tile mı?
                bool isGoalTile = false;
                var hud = board.TopHud;
                if (hud != null && board.GoalFlyFx != null)
                {
                    isGoalTile = hud.TryGetGoalTargetRectForTile(tile.GetTileType(), out _);
                }

                if (isGoalTile)
                    skipBreakFxTiles.Add(tile);

                // Öncelik: goal fly > lightning per-tile > default
                float delay = 0f;
                bool isRadialWaveTile = false;
                if (perTileClearDelays != null && perTileClearDelays.TryGetValue(tile, out float customDelay))
                {
                    delay = Mathf.Max(0f, customDelay);
                    isRadialWaveTile = true;
                }
                else if (useLightningEffect)
                    delay = lightningIndex * lightningStepDelay;

                // Override+Override radial wave: play a "hit" pulse on each tile as the
                // shockwave reaches it, right before the clear animation kicks in.
                if (isRadialWaveTile && !isGoalTile)
                {
                    float pulseDelay = Mathf.Max(0f, delay - 0.03f);
                    pops.Add(DelayedSelectionPulse(tile, pulseDelay, 1.22f, 0.05f, 0.07f));
                }

                var tileAnimationMode =
                    isGoalTile ? ClearAnimationMode.GoalFlyToHud :
                    (useLightningEffect ? ClearAnimationMode.LightningStrike : ClearAnimationMode.Default);
                Debug.Log(
                    $"[PulseClearDebug][BA] POP_BRANCH tile=({tile.X},{tile.Y}) " +
                    $"delay={delay:0.000} mode={tileAnimationMode}");

                if (tileAnimationMode == ClearAnimationMode.Default && implodeTargetCell.HasValue && !shouldSuppressVfx)
                {
                    implodeTiles.Add(tile);
                }
                else
                {
                    pops.Add(clearEffectOrchestrator.Play(tile, tileAnimationMode, delay, board.GetClearDurationForCurrentPass()));
                }

                if (!isSweptOff && !implodeTiles.Contains(tile))
                {
                    board.StartCoroutine(ClearCellDataAfterDelay(tile, delay));
                }

                if (useLightningEffect)
                    lightningIndex++;
            }
        }

        float lightningDuration = 0f;
        if (animationMode == ClearAnimationMode.LightningStrike)
        {
            if (lightningLineStrikes != null && lightningLineStrikes.Count > 0)
            {
                if (board.Audio != null)
                {
                    TileSpecial lineSpecial = TileSpecial.LineH;

                    if (!lightningLineStrikes[0].isHorizontal)
                        lineSpecial = TileSpecial.LineV;

                    board.Audio.Emit(
                        BoardSfxRequest.SpecialActivate(
                            lineSpecial,
                            intensity: Mathf.Max(1, lightningLineStrikes.Count)
                        )
                    );
                }

                lightningDuration = board.PlayLightningLineStrikes(
                    lightningLineStrikes,
                    cell =>
                    {
                        StartPatchbotDashRequestsForLineCell(lineSweepPatchbotDashes, cell);
                        TryClearTileOnLineSweepHit(cell);
                        ApplyObstacleDamageOnLineSweepHit(cell);
                    }
                );

                if (lightningDuration <= 0.001f)
                {
                    suppressPerTileClearVfx = false; // tile bazlı animasyonlara izin ver
                }
            }
            else
            {
                var strikeTargets = orderedStrikeTargets ?? list;
                lightningDuration = board.PlayLightningStrikeForTiles(
                    strikeTargets,
                    lightningOriginTile,
                    lightningOriginCell,
                    strikeTargets
                );
            }
        }

        if (doShake)
        {
            if (board.PreClearDelay > 0f)
            {
                var __w = Wait(board.PreClearDelay);
                if (__w != null) yield return __w;
            }

            if (board.ShakeTarget != null)
            {
                board.StartCoroutine(ShakeBoard(board.ShakeDuration, board.ShakeStrength));

                // Override+Override radial wave: add escalating micro-shakes during the wave
                if (perTileClearDelays != null && perTileClearDelays.Count > 0)
                {
                    float maxRadialDelay = 0f;
                    foreach (var kv in perTileClearDelays)
                        if (kv.Value > maxRadialDelay) maxRadialDelay = kv.Value;

                    if (maxRadialDelay > 0.1f)
                    {
                        int waveShakeSteps = 3;
                        for (int ws = 0; ws < waveShakeSteps; ws++)
                        {
                            float t = (ws + 1f) / waveShakeSteps;
                            float shakeDelay = t * maxRadialDelay * 0.7f;
                            float shakeStrength = Mathf.Lerp(board.ShakeStrength * 0.3f, board.ShakeStrength * 0.8f, t);
                            board.StartCoroutine(DelayedMicroShake(shakeDelay, 0.10f, shakeStrength));
                        }
                    }
                }
            }
        }

        if (pulseImpacts.Count > 0)
        {
            for (int i = 0; i < pulseImpacts.Count; i++)
                board.StartCoroutine(pulseImpacts[i]);
        }

        if (implodeTiles.Count > 0 && implodeTargetCell.HasValue)
        {
            // Burst VFX'i taşlar birleştiğinde göster (küçük gecikme ile)
            float implodeDuration = board.GetClearDurationForCurrentPass();
            if (board != null && board.Parent != null)
            {
                board.StartCoroutine(DelayedImplodeBurst(implodeTargetCell.Value, Mathf.Max(0.05f, implodeDuration * 0.6f)));
            }

            pops.Add(tileAnimator.PlayTilesImplodeToCell(
                implodeTargetCell.Value,
                implodeTiles,
                implodeDuration,
                0.7f,
                tile =>
                {
                    if (tile == null) return;
                    FinalizeTileClear(tile);
                }
            ));
        }

        if (pops.Count > 0)
            yield return RunMany(pops);

        if (lightningDuration > 0f)
        {
            var __w = Wait(lightningDuration);
            if (__w != null) yield return __w;
        }

        FlushPendingPatchbotDashRequests(lineSweepPatchbotDashes);
        lineHitWindowOpen = false;

        if (pulseImpacts.Count > 0)
        {
            var __w = Wait(maxStaggerDelay + staggerAnimTime);
            if (__w != null) yield return __w;
        }

        Debug.Log(
            $"[PulseClearDebug][BA] BEFORE_FINAL " +
            $"list={list.Count} shouldClear={shouldClearTile.Count} " +
            $"pulseImpacts={pulseImpacts.Count} pops={pops.Count}");

        for (int i = 0; i < list.Count; i++)
        {
            var tile = list[i];
            if (tile == null) continue;
            if (lineHitClearedTiles.Contains(tile)) continue;

            if (!board.IsSpecialActivationPhase && tile.GetSpecial() != TileSpecial.None)
                continue;

            if (shouldClearTile.TryGetValue(tile, out var clearTile) && !clearTile)
                continue;

            FinalizeTileClear(tile);
        }

        IEnumerator ClearCellDataAfterDelay(TileView t, float waitTime)
        {
            if (waitTime > 0f) yield return new WaitForSeconds(waitTime);
            if (t != null && board.Tiles[t.X, t.Y] == t)
            {
                board.ClearCellDataOnly(new Vector2Int(t.X, t.Y));
            }
        }

        void ApplyObstacleDamageOnLineSweepHit(Vector2Int tileCell)
        {
            if (!useLineHitDrivenClear || !lineHitWindowOpen) return;
            if (tileCell.x < 0 || tileCell.x >= board.Width || tileCell.y < 0 || tileCell.y >= board.Height) return;
            if (board.ObstacleStateService == null) return;

            void TryHit(Vector2Int c)
            {
                if (c.x < 0 || c.x >= board.Width || c.y < 0 || c.y >= board.Height) return;
                if (lineHitDamagedObstacleCells.Contains(c)) return;
                if (!board.ObstacleStateService.HasObstacleAt(c.x, c.y)) return;
                lineHitDamagedObstacleCells.Add(c);
                var hit = board.ApplyObstacleDamageAt(c.x, c.y, damageContext, null);
                if (hit.didHit) board.TriggerObstacleVisualChange(hit.visualChange);
            }

            // Beam hücresini her durumda hit et (chest gibi blocked hücreler dahil).
            TryHit(tileCell);

            // Adjacent hit'ler sadece normal tile varsa — chest hücrelerinin çapraz
            // komşularını zincirlememek için.
            if (board.Tiles[tileCell.x, tileCell.y] != null && !board.ObstacleStateService.IsInteractionLockedAt(tileCell.x, tileCell.y))
            {
                TryHit(new Vector2Int(tileCell.x + 1, tileCell.y));
                TryHit(new Vector2Int(tileCell.x - 1, tileCell.y));
                TryHit(new Vector2Int(tileCell.x, tileCell.y + 1));
                TryHit(new Vector2Int(tileCell.x, tileCell.y - 1));
            }
        }

        void TryClearTileOnLineSweepHit(Vector2Int cell)
        {
            if (!useLineHitDrivenClear || !lineHitWindowOpen)
                return;

            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                return;

            var tileAtCell = board.Tiles[cell.x, cell.y];
            if (tileAtCell == null || lineHitClearedTiles.Contains(tileAtCell))
                return;

            if (!board.IsSpecialActivationPhase && tileAtCell.GetSpecial() != TileSpecial.None)
                return;

            if (!lineSweepCandidates.Contains(tileAtCell))
                return;

            if (!shouldClearTile.TryGetValue(tileAtCell, out var shouldClearNow) || !shouldClearNow)
                return;

            lineHitClearedTiles.Add(tileAtCell);
            FinalizeTileClear(tileAtCell);
        }

        void FinalizeTileClear(TileView tile)
        {
            if (tile == null)
            {
                Debug.Log("[PulseClearDebug] FinalizeTileClear skip tile=null");
                return;
            }

            bool live =
                tile.X >= 0 && tile.X < board.Width &&
                tile.Y >= 0 && tile.Y < board.Height &&
                board.Tiles[tile.X, tile.Y] == tile;

            Debug.Log(
                $"[PulseClearDebug] FinalizeTileClear tile=({tile.X},{tile.Y}) " +
                $"type={tile.GetTileType()} special={tile.GetSpecial()} " +
                $"live={live} specialPhase={board.IsSpecialActivationPhase}");

            if (!skipBreakFxTiles.Contains(tile))
                board.BreakFx?.PlayTileBreak(tile);

            board.ClearAndDestroyTile(tile, clearedByType);
        }

        foreach (var pair in clearedByType)
            board.NotifyTilesCleared(pair.Key, pair.Value);

        if (board.ObstacleStateService == null)
            yield break;

        void AddObstacleDamageCell(Vector2Int cell, TileType? sourceTileType)
        {
            if (obstacleDamageSources.TryGetValue(cell, out var sources))
            {
                sources.Add(sourceTileType);
            }
            else
            {
                obstacleDamageSources[cell] = new List<TileType?> { sourceTileType };
            }
        }

        for (int impactIndex = 0; impactIndex < impactCells.Count; impactIndex++)
        {
            var cell = impactCells[impactIndex];

            TileType? sourceTileType =
                impactIndex >= 0 && impactIndex < impactSourceTileTypes.Count
                    ? impactSourceTileTypes[impactIndex]
                    : null;

            AddObstacleDamageCell(cell, sourceTileType);

            if (includeAdjacentOverTileBlockerDamage)
                CollectAdjacentOverTileBlockers(cell, obstacleDamageSources, sourceTileType);

            CollectAdjacentMudCells(cell, obstacleDamageSources, sourceTileType);
        }

        // Mud per-match max 1 hit (line-sweep tarafı da)
        if (board.ObstacleStateService != null)
        {
            foreach (var kvp in obstacleDamageSources)
            {
                if (kvp.Value == null || kvp.Value.Count <= 1) continue;
                if (board.ObstacleStateService.GetObstacleIdAt(kvp.Key.x, kvp.Key.y) != ObstacleId.Mud) continue;
                var first = kvp.Value[0];
                kvp.Value.Clear();
                kvp.Value.Add(first);
            }
        }

        foreach (var kv in obstacleDamageSources)
        {
            var cell = kv.Key;
            if (lineHitDamagedObstacleCells.Contains(cell)) continue;

            var sources = kv.Value;

            if (sources == null)
                continue;

            for (int i = 0; i < sources.Count; i++)
            {
                var hit = board.ApplyObstacleDamageAt(cell.x, cell.y, damageContext, sources[i]);
                if (hit.didHit)
                    board.TriggerObstacleVisualChange(hit.visualChange);
            }
        }
    }


    private IClearEffectPlayer ResolveEffectPlayer(IClearEffectDescriptor effect)
    {
        if (effect == null)
            return null;

        for (int i = 0; i < clearEffectPlayers.Count; i++)
        {
            if (clearEffectPlayers[i] != null && clearEffectPlayers[i].CanPlay(effect))
                return clearEffectPlayers[i];
        }

        return null;
    }

    public IEnumerator PlayPulseImpactSingle(TileView tile, float animTime)
    {
        if (tile == null)
            yield break;

        yield return tileAnimator.PlayPulseImpact(tile, 0f, animTime);
    }

    public IEnumerator PlayCreatedSpecialFormation(
        TileView createdTile,
        IReadOnlyList<TileView> sourceTiles,
        float duration)
    {
        if (createdTile == null)
            yield break;

        yield return tileAnimator.PlaySpecialCreationMerge(createdTile, sourceTiles, duration);
    }

    public IEnumerator PlayClearPresentation(ClearPresentationPlan plan)
    {
        if (plan == null)
            yield break;

        System.Collections.Generic.List<Vector2Int> impactedCells =
            new System.Collections.Generic.List<Vector2Int>();

        System.Collections.Generic.Dictionary<TileType, int> clearedByType =
            new System.Collections.Generic.Dictionary<TileType, int>();

        var ctx = new ClearEffectPlaybackContext();
        var cleared = new System.Collections.Generic.HashSet<TileView>();

        // Presentation path normal match ise, final clear tile'ların type bilgisini
        // merkezi plana yaz. Böylece effect impact'i sonradan geldiğinde obstacle damage
        // doğru TileType ile üretilebilir.
        if (plan.ObstacleHitContext == ObstacleHitContext.NormalMatch)
        {
            foreach (var tile in plan.FinalClearTiles)
                plan.RegisterNormalMatchSource(tile);
        }

        bool IsInteractionLocked(TileView tile)
        {
            if (tile == null)
                return false;

            if (board == null || board.ObstacleStateService == null)
                return false;

            if (tile.X < 0 || tile.X >= board.Width || tile.Y < 0 || tile.Y >= board.Height)
                return false;

            return board.ObstacleStateService.IsInteractionLockedAt(tile.X, tile.Y);
        }

        void AddImpactedCell(Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                return;

            if (!impactedCells.Contains(cell))
                impactedCells.Add(cell);
        }

        void FinalizePresentationTileClear(TileView tile)
        {
            if (tile == null || cleared.Contains(tile))
                return;

            var cell = new Vector2Int(tile.X, tile.Y);

            if (plan.ObstacleHitContext == ObstacleHitContext.NormalMatch)
                plan.RegisterNormalMatchSource(tile);

            // Önemli:
            // Oil / CellAnchoredOverlay gibi locksInteraction olan hücrelerde
            // alttaki tile temizlenmez; ama cell impact olarak kalır.
            // Böylece special/clear presentation oil'i vurur, tile'a dokunmaz.
            AddImpactedCell(cell);

            if (IsInteractionLocked(tile))
                return;

            cleared.Add(tile);
            board.BreakFx?.PlayTileBreak(tile);
            board.ClearAndDestroyTile(tile, clearedByType);
        }

        ctx.ClearTileNow = delegate (TileView tile)
        {
            FinalizePresentationTileClear(tile);
        };

        ctx.NotifyCellImpactNow = delegate (Vector2Int cell)
        {
            AddImpactedCell(cell);
        };

        if (plan.DoBoardShake && board.ShakeTarget != null)
            board.StartCoroutine(ShakeBoard(board.ShakeDuration, board.ShakeStrength));

        for (int i = 0; i < plan.Effects.Count; i++)
        {
            var effect = plan.Effects[i];
            if (effect == null)
                continue;

            var player = ResolveEffectPlayer(effect);
            if (player == null)
                continue;

            yield return player.Play(effect, board, ctx);
        }

        foreach (TileView tile in plan.FinalClearTiles)
            FinalizePresentationTileClear(tile);

        foreach (var pair in clearedByType)
            board.NotifyTilesCleared(pair.Key, pair.Value);

        ApplyPresentationObstacleDamage(impactedCells, plan);
    }

    private void ApplyPresentationObstacleDamage(
        System.Collections.Generic.List<Vector2Int> impactedCells,
        ClearPresentationPlan plan)
    {
        if (board.ObstacleStateService == null || impactedCells == null || impactedCells.Count == 0)
            return;

        var obstacleDamageRequests =
            new System.Collections.Generic.Dictionary<Vector2Int, System.Collections.Generic.List<ObstacleDamageRequest>>();

        void AddDamageRequest(ObstacleDamageRequest request)
        {
            if (obstacleDamageRequests.TryGetValue(request.cell, out var requests))
            {
                requests.Add(request);
            }
            else
            {
                obstacleDamageRequests[request.cell] =
                    new System.Collections.Generic.List<ObstacleDamageRequest> { request };
            }
        }

        for (int i = 0; i < impactedCells.Count; i++)
        {
            Vector2Int cell = impactedCells[i];

            TileType? sourceTileType = plan.GetNormalMatchSourceTileType(cell);

            var request = new ObstacleDamageRequest(
                cell,
                plan.ObstacleHitContext,
                sourceTileType);

            AddDamageRequest(request);

            // DEBUG: match'in dahil olduğu cell'de Mud var mı?
            if (board.ObstacleStateService != null
                && board.ObstacleStateService.GetObstacleIdAt(cell.x, cell.y) == ObstacleId.Mud)
            {
                Debug.Log($"[MudDebug] Match cell ({cell.x},{cell.y}) Mud içeriyor, damage request eklendi");
            }

            if (plan.IncludeAdjacentOverTileBlockerDamage)
                CollectAdjacentOverTileBlockers(cell, obstacleDamageRequests, request);

            // Mud her zaman match'in komşularına yayılır — opt-in flag'e gerek yok.
            CollectAdjacentMudCells(cell, obstacleDamageRequests, request);
        }

        // Mud per-match max 1 hit garantisi: aynı cell birden fazla request taşıyorsa
        // ilkine indir. (Match'in ortasındaki Mud cell self + neighbor olarak çift kuyruğa girebiliyor.)
        if (board.ObstacleStateService != null)
        {
            foreach (var kvp in obstacleDamageRequests)
            {
                if (kvp.Value == null || kvp.Value.Count <= 1) continue;
                if (board.ObstacleStateService.GetObstacleIdAt(kvp.Key.x, kvp.Key.y) != ObstacleId.Mud) continue;
                var first = kvp.Value[0];
                kvp.Value.Clear();
                kvp.Value.Add(first);
            }
        }

        // DEBUG: kuyruğa alınan Mud cell'leri tek tek logla
        foreach (var kvp in obstacleDamageRequests)
        {
            if (board.ObstacleStateService != null
                && board.ObstacleStateService.GetObstacleIdAt(kvp.Key.x, kvp.Key.y) == ObstacleId.Mud)
            {
                Debug.Log($"[MudDebug] Queued damage for Mud cell ({kvp.Key.x},{kvp.Key.y}) — {kvp.Value.Count} request(s)");
            }
        }

        foreach (var kv in obstacleDamageRequests)
        {
            var requests = kv.Value;
            if (requests == null)
                continue;

            for (int i = 0; i < requests.Count; i++)
            {
                var hit = board.ApplyObstacleDamage(requests[i]);
                if (hit.didHit)
                    board.TriggerObstacleVisualChange(hit.visualChange);
            }
        }
    }
    private static void SortTilesForLightning(List<TileView> tiles, TileView originTile, Vector2Int? originCell)
    {
        if (tiles == null || tiles.Count <= 1)
            return;

        Vector2Int origin = originTile != null
            ? new Vector2Int(originTile.X, originTile.Y)
            : originCell ?? new Vector2Int(tiles[0] != null ? tiles[0].X : 0, tiles[0] != null ? tiles[0].Y : 0);

        tiles.Sort((a, b) =>
        {
            if (a == b) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int da = Mathf.Abs(a.X - origin.x) + Mathf.Abs(a.Y - origin.y);
            int db = Mathf.Abs(b.X - origin.x) + Mathf.Abs(b.Y - origin.y);
            int byDistance = da.CompareTo(db);
            if (byDistance != 0) return byDistance;

            int byRow = a.Y.CompareTo(b.Y);
            if (byRow != 0) return byRow;

            return a.X.CompareTo(b.X);
        });
    }

    private void CollectAdjacentOverTileBlockers(Vector2Int centerCell, Dictionary<Vector2Int, int> obstacleDamageCounts)
    {
        if (board == null || board.Obstacles == null)
            return;

        for (int dir = 0; dir < 4; dir++)
        {
            Vector2Int neighbor = dir switch
            {
                0 => new Vector2Int(centerCell.x + 1, centerCell.y),
                1 => new Vector2Int(centerCell.x - 1, centerCell.y),
                2 => new Vector2Int(centerCell.x, centerCell.y + 1),
                _ => new Vector2Int(centerCell.x, centerCell.y - 1),
            };

            if (neighbor.x < 0 || neighbor.x >= board.Width || neighbor.y < 0 || neighbor.y >= board.Height)
                continue;

            if (!board.Obstacles.IsOverTileBlockerAt(neighbor.x, neighbor.y) && !board.ObstacleStateService.IsOilAt(neighbor.x, neighbor.y))
                continue;

            if (obstacleDamageCounts.TryGetValue(neighbor, out int existing))
                obstacleDamageCounts[neighbor] = existing + 1;
            else
                obstacleDamageCounts[neighbor] = 1;
        }
    }


    private void CollectAdjacentOverTileBlockers(
     Vector2Int centerCell,
     Dictionary<Vector2Int, List<TileType?>> result,
     TileType? sourceTileType)
    {
        if (board == null || board.ObstacleStateService == null || result == null)
            return;

        void TryCollect(Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                return;

            bool isDamageableOverTile =
                board.Obstacles != null && board.Obstacles.IsOverTileBlockerAt(cell.x, cell.y);

            bool isOil =
                board.ObstacleStateService.IsOilAt(cell.x, cell.y);

            if (!isDamageableOverTile && !isOil)
                return;

            if (result.TryGetValue(cell, out var sources))
                sources.Add(sourceTileType);
            else
                result[cell] = new List<TileType?> { sourceTileType };
        }

        // Normal match obstacle damage sadece 4 yön komşuluk kullanır.
        // AllowDiagonal burada kullanılmaz; o sadece cascade/fall diagonal kayma içindir.
        for (int i = 0; i < OrthogonalDirs.Length; i++)
            TryCollect(centerCell + OrthogonalDirs[i]);
    }

    private void CollectAdjacentOverTileBlockers(
        Vector2Int centerCell,
        Dictionary<Vector2Int, List<ObstacleDamageRequest>> obstacleDamageRequests,
        ObstacleDamageRequest sourceRequest)
    {
        if (board == null || board.Obstacles == null || obstacleDamageRequests == null)
            return;

        void AddRequest(Vector2Int cell)
        {
            var request = new ObstacleDamageRequest(
                cell,
                sourceRequest.context,
                sourceRequest.normalMatchTileType);

            if (obstacleDamageRequests.TryGetValue(cell, out var requests))
            {
                requests.Add(request);
            }
            else
            {
                obstacleDamageRequests[cell] = new List<ObstacleDamageRequest> { request };
            }
        }

        void TryCollect(Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                return;

            if (!board.Obstacles.IsOverTileBlockerAt(cell.x, cell.y)
                && !board.ObstacleStateService.IsOilAt(cell.x, cell.y))
                return;

            AddRequest(cell);
        }

        for (int i = 0; i < OrthogonalDirs.Length; i++)
            TryCollect(centerCell + OrthogonalDirs[i]);

        for (int i = 0; i < DiagonalDirs.Length; i++)
        {
            Vector2Int diagonal = centerCell + DiagonalDirs[i];

            if (diagonal.x < 0 || diagonal.x >= board.Width || diagonal.y < 0 || diagonal.y >= board.Height)
                continue;

            if (!board.Obstacles.IsDiagonalAllowedAt(diagonal.x, diagonal.y))
                continue;

            TryCollect(diagonal);
        }
    }

    // Match'in 4 yön komşularındaki Mud hücrelerine ek damage request kuyruğa alır.
    // Mud her match'in komşusunda otomatik damage alır (Candy Crush / Homescapes davranışı).
    // ÖNEMLİ: Bir Mud cell aynı match içinde zaten kuyruğa alındıysa tekrar eklenmez —
    // aksi takdirde match çizgisinin ortasındaki Mud cell 2-3 hit birden alır ve
    // stage geçişi göze çarpmaz.
    private void CollectAdjacentMudCells(
        Vector2Int centerCell,
        Dictionary<Vector2Int, List<ObstacleDamageRequest>> obstacleDamageRequests,
        ObstacleDamageRequest sourceRequest)
    {
        if (board == null || board.ObstacleStateService == null || obstacleDamageRequests == null)
            return;

        for (int i = 0; i < OrthogonalDirs.Length; i++)
        {
            Vector2Int n = centerCell + OrthogonalDirs[i];
            if (n.x < 0 || n.x >= board.Width || n.y < 0 || n.y >= board.Height) continue;
            if (board.ObstacleStateService.GetObstacleIdAt(n.x, n.y) != ObstacleId.Mud) continue;

            // Bu Mud cell için zaten bir request varsa atla — Mud per-match en fazla 1 hit alsın.
            if (obstacleDamageRequests.ContainsKey(n)) continue;

            var request = new ObstacleDamageRequest(n, sourceRequest.context, sourceRequest.normalMatchTileType);
            obstacleDamageRequests[n] = new List<ObstacleDamageRequest> { request };
        }
    }

    // Line sweep / lightning yolu için aynı işin sourceTileType-tabanlı versiyonu.
    private void CollectAdjacentMudCells(
        Vector2Int centerCell,
        Dictionary<Vector2Int, List<TileType?>> obstacleDamageSources,
        TileType? sourceTileType)
    {
        if (board == null || board.ObstacleStateService == null || obstacleDamageSources == null)
            return;

        for (int i = 0; i < OrthogonalDirs.Length; i++)
        {
            Vector2Int n = centerCell + OrthogonalDirs[i];
            if (n.x < 0 || n.x >= board.Width || n.y < 0 || n.y >= board.Height) continue;
            if (board.ObstacleStateService.GetObstacleIdAt(n.x, n.y) != ObstacleId.Mud) continue;

            // Mud için per-match max 1 hit.
            if (obstacleDamageSources.ContainsKey(n)) continue;

            obstacleDamageSources[n] = new List<TileType?> { sourceTileType };
        }
    }

    public IEnumerator ShakeBoard(float duration, float strength)
    {
        if (board.ShakeTarget == null) yield break;

        board.ShakeBasePos = board.ShakeTarget.anchoredPosition;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float damper = 1f - (t / Mathf.Max(0.0001f, duration));

            float ox = UnityEngine.Random.Range(-strength, strength) * damper;
            float oy = UnityEngine.Random.Range(-strength, strength) * damper;

            board.ShakeTarget.anchoredPosition = board.ShakeBasePos + new Vector2(ox, oy);
            yield return null;
        }

        board.ShakeTarget.anchoredPosition = board.ShakeBasePos;
    }

    public IEnumerator MicroShake(float duration, float strength)
    {
        var target = board.ShakeTarget != null ? board.ShakeTarget : board.Parent;
        if (target == null)
            target = board.GetComponent<RectTransform>();
        if (target == null) yield break;

        Vector2 basePos = target.anchoredPosition;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float damper = 1f - (t / Mathf.Max(0.0001f, duration));

            float ox = UnityEngine.Random.Range(-strength, strength) * damper;
            float oy = UnityEngine.Random.Range(-strength, strength) * damper;

            target.anchoredPosition = basePos + new Vector2(ox, oy);
            yield return null;
        }

        target.anchoredPosition = basePos;
    }

    /// <summary>
    /// Plays a selection pulse on a tile after a delay.
    /// Used by Override+Override radial wave to give each tile a visible "hit"
    /// feedback as the shockwave reaches it.
    /// </summary>
    private IEnumerator DelayedSelectionPulse(TileView tile, float delay, float peakScale, float upTime, float downTime)
    {
        if (tile == null) yield break;
        if (delay > 0f)
        {
            var w = Wait(delay);
            if (w != null) yield return w;
        }
        if (tile == null) yield break;
        tileAnimator?.PlaySelectionPulse(tile, 0f, peakScale, upTime, downTime);
        // Wait for the pulse to finish so RunMany tracks it correctly.
        var wUp = Wait(upTime + downTime);
        if (wUp != null) yield return wUp;
    }

    /// <summary>
    /// Fires a micro-shake after a delay. Used to create escalating shakes
    /// during the Override+Override radial clear wave.
    /// </summary>
    private IEnumerator DelayedMicroShake(float delay, float duration, float strength)
    {
        if (delay > 0f)
        {
            var w = Wait(delay);
            if (w != null) yield return w;
        }
        yield return MicroShake(duration, strength);
    }

    private IEnumerator DelayedImplodeBurst(Vector2Int cell, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (board == null || board.Parent == null)
            yield break;

        // Hücrenin world pozisyonunu tile'ın kendisinden al (en doğru yol)
        TileView tileAtCell = (cell.x >= 0 && cell.x < board.Width && cell.y >= 0 && cell.y < board.Height)
            ? board.Tiles[cell.x, cell.y]
            : null;

        Vector3 worldCenter;
        if (tileAtCell != null && tileAtCell.IconImage != null && tileAtCell.IconImage.rectTransform != null)
        {
            Vector3[] corners = new Vector3[4];
            tileAtCell.IconImage.rectTransform.GetWorldCorners(corners);
            worldCenter = (corners[0] + corners[2]) * 0.5f;
        }
        else
        {
            // Fallback: grid koordinatından hesapla
            RectTransform parent = board.Parent;
            Vector2 localPos = new Vector2(
                cell.x * board.TileSize + board.TileSize * 0.5f,
                -cell.y * board.TileSize - board.TileSize * 0.5f);
            worldCenter = parent.TransformPoint(localPos);
        }

        board.StartCoroutine(TileClearBurstVfx.CoPlayBurstAtWorldPosition(
            worldCenter, board.Parent, board, 0.35f));
    }

    public IEnumerator CollapseColumnsAnimated()
    {
        var moves = new List<IEnumerator>();
        var moveDelays = new List<float>();

        for (int x = 0; x < board.Width; x++)
        {
            int segStartY = board.Height - 1;
            for (int y = board.Height - 1; y >= -1; y--)
            {
                bool isBoundary = (y == -1) || IsObstacleBlockedCell(x, y);

                if (!isBoundary)
                    continue;

                int segEndY = y + 1;
                if (segEndY <= segStartY)
                {
                    var slots = new List<int>();
                    for (int yy = segStartY; yy >= segEndY; yy--)
                    {
                        if (board.Holes[x, yy]) continue;
                        slots.Add(yy);
                    }

                    var existing = new List<TileView>();
                    for (int yy = segStartY; yy >= segEndY; yy--)
                    {
                        if (board.Holes[x, yy]) continue;
                        var tv = board.Tiles[x, yy];
                        if (tv != null)
                            existing.Add(tv);
                    }

                    for (int i = 0; i < slots.Count; i++)
                        board.Tiles[x, slots[i]] = null;

                    for (int i = 0; i < existing.Count && i < slots.Count; i++)
                    {
                        int toY = slots[i];
                        var tile = existing[i];
                        int fromY = tile.Y;

                        if (fromY != toY)
                        {
                            board.Tiles[x, toY] = tile;
                            board.Tiles[x, fromY] = null;

                            tile.SetCoords(x, toY);
                            board.SyncTileData(x, toY);
                            board.SyncTileData(x, fromY);
                            int fallDistance = Mathf.Abs(toY - fromY);
                            float fallDuration = board.GetFallDurationForDistance(fallDistance);
                            bool useFallSettle = board.ShouldEnableFallSettleThisPass();

                            moves.Add(tile.MoveToGrid(
                                board.TileSize,
                                fallDuration,
                                board.FallMoveCurve,
                                useFallSettle,
                                board.FallSettleDuration,
                                board.FallSettleStrength,
                                board.FallSettleStretchX,
                                board.FallSettleOvershoot
                            ));
                            moveDelays.Add(0f);
                        }
                        else
                        {
                            board.Tiles[x, toY] = tile;
                            board.SyncTileData(x, toY);
                        }
                    }
                }

                segStartY = y - 1;
            }
        }

        if (moves.Count > 0)
            yield return RunManyWithDelays(moves, moveDelays);
    }

    public IEnumerator SpawnNewTilesAnimated()
    {
        var moves = new List<IEnumerator>();

        for (int x = 0; x < board.Width; x++)
        {
            int nextSpawnY = Mathf.Min(-1, board.SpawnStartOffsetY);

            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y]) continue;
                if (board.Tiles[x, y] != null) continue;

                var go = UnityEngine.Object.Instantiate(board.TilePrefab, board.Parent);
                var view = go.GetComponent<TileView>();
                if (view == null)
                {
                    Debug.LogError("BoardController: Spawned prefab missing TileView.");
                    UnityEngine.Object.Destroy(go);
                    continue;
                }

                view.Init(board, x, y);
                board.ConfigureTileView(view);

                view.SetCoords(x, nextSpawnY);
                view.SnapToGrid(board.TileSize);
                nextSpawnY--;

                view.SetCoords(x, y);
                board.Tiles[x, y] = view;

                view.SetType(GetRandomType());
                view.SetSpecial(TileSpecial.None);
                board.SyncTileData(x, y); // Sync Data model AFTER setting type and special
                board.RefreshTileObstacleVisual(view);

                int dist = Mathf.Abs(y - nextSpawnY);
                float duration = board.GetFallDurationForDistance(dist);

                moves.Add(view.MoveToGrid(
                    board.TileSize,
                    duration,
                    board.FallMoveCurve,
                    board.ShouldEnableFallSettleThisPass(),
                    board.FallSettleDuration,
                    board.FallSettleStrength,
                    board.FallSettleStretchX,
                    board.FallSettleOvershoot
                ));
            }
        }

        if (moves.Count > 0)
            yield return RunMany(moves);

        board.RefreshAllTileObstacleVisuals();
    }


    [System.Obsolete("Use CascadeLogic.CalculateCascades() instead. This method will be removed.")]
    public IEnumerator CollapseAndSpawnAnimated()
    {
        board.IncrementFallGeneration();

        for (int xx = 0; xx < board.Width; xx++)
        {
            for (int yy = 0; yy < board.Height; yy++)
            {
                var tv = board.Tiles[xx, yy];
                if (tv != null)
                    tv.MarkPlannedToMoveThisFallPass(false);
            }
        }

        var moves = new List<IEnumerator>();
        var moveDelays = new List<float>();

        for (int x = 0; x < board.Width; x++)
        {
            var colTiles = new List<TileView>(board.Height);
            var colTargetY = new List<int>(board.Height);
            var colDuration = new List<float>(board.Height);
            var colDist = new List<int>(board.Height);

            int segmentTop = board.Height - 1;
            while (segmentTop >= 0)
            {
                while (segmentTop >= 0 && IsObstacleBlockedCell(x, segmentTop))
                    segmentTop--;

                if (segmentTop < 0)
                    break;

                int segmentBottom = segmentTop;
                while (segmentBottom >= 0 && !IsObstacleBlockedCell(x, segmentBottom))
                    segmentBottom--;

                int topY = segmentBottom + 1;
                bool touchesSpawnEdge = IsSegmentConnectedToSpawnEdge(x, topY);

                var slots = new List<int>();
                var existing = new List<TileView>();

                for (int y = segmentTop; y >= topY; y--)
                {
                    if (board.Holes[x, y]) continue;
                    slots.Add(y);

                    if (board.Tiles[x, y] != null)
                        existing.Add(board.Tiles[x, y]);
                }

                for (int i = 0; i < slots.Count; i++)
                    board.Tiles[x, slots[i]] = null;

                for (int i = 0; i < existing.Count && i < slots.Count; i++)
                {
                    int targetY = slots[i];
                    var tile = existing[i];
                    int fromY = tile.Y;

                    board.Tiles[x, targetY] = tile;
                    tile.SetCoords(x, targetY);
                    board.SyncTileData(x, targetY); // Sync Data model

                    int dist = Mathf.Abs(targetY - fromY);
                    if (dist <= 0)
                        continue;

                    tile.MarkPlannedToMoveThisFallPass(true);

                    float duration = board.GetFallDurationForDistance(dist);
                    colTiles.Add(tile);
                    colTargetY.Add(targetY);
                    colDuration.Add(duration);
                    colDist.Add(dist);
                }

                if (touchesSpawnEdge)
                {
                    int nextSpawnY = topY + board.SpawnStartOffsetY;

                    for (int y = topY; y <= segmentTop; y++)
                    {
                        if (board.Holes[x, y]) continue;
                        if (board.Tiles[x, y] != null) continue;

                        var go = UnityEngine.Object.Instantiate(board.TilePrefab, board.Parent);
                        var view = go.GetComponent<TileView>();
                        if (view == null)
                        {
                            Debug.LogError("BoardController: Spawned prefab missing TileView.");
                            UnityEngine.Object.Destroy(go);
                            continue;
                        }

                        view.Init(board, x, y);
                        board.ConfigureTileView(view);
                        view.MarkPlannedToMoveThisFallPass(true);

                        int spawnFromY = nextSpawnY;
                        view.SetCoords(x, spawnFromY);
                        view.SnapToGrid(board.TileSize);
                        nextSpawnY--;

                        view.SetCoords(x, y);
                        board.Tiles[x, y] = view;

                        view.SetType(GetRandomType());
                        view.SetSpecial(TileSpecial.None);
                        board.SyncTileData(x, y); // ← EKSİK OLAN BUYDU! Veritabanına kaydet
                        board.RefreshTileObstacleVisual(view);

                        int dist = Mathf.Abs(y - spawnFromY);
                        float duration = board.GetFallDurationForDistance(dist);

                        colTiles.Add(view);
                        colTargetY.Add(y);
                        colDuration.Add(duration);
                        colDist.Add(dist);
                    }
                }

                segmentTop = segmentBottom - 1;
            }

            for (int i = 0; i < colTiles.Count; i++)
            {
                var tile = colTiles[i];
                int targetY = colTargetY[i];
                int dist = colDist[i];

                // Videoda her düşen taş kendi settle'ını yapıyor — dist veya altındaki taşın
                // hareket durumuna bakmıyoruz. Pass bayrağına göre açık/kapalı karar veriyoruz.
                bool useFallSettle = board.ShouldEnableFallSettleThisPass() && dist > 0;

                moves.Add(tile.MoveToGrid(
                    board.TileSize,
                    colDuration[i],
                    board.FallMoveCurve,
                    useFallSettle,
                    board.FallSettleDuration,
                    board.FallSettleStrength,
                    board.FallSettleStretchX,
                    board.FallSettleOvershoot
                ));
                moveDelays.Add(0f);
            }
        }

        if (moves.Count > 0)
            yield return RunManyWithDelays(moves, moveDelays);

        board.RefreshAllTileObstacleVisuals();
    }

    internal IEnumerator SlideFillAnimated()
    {
        const int maxPass = 32;

        for (int pass = 0; pass < maxPass; pass++)
        {
            bool movedAny = false;
            var moves = new List<IEnumerator>();
            var moveDelays = new List<float>();
            var movedThisPass = new HashSet<TileView>();

            for (int y = board.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.IsMaskHoleCell(x, y) || IsObstacleBlockedCell(x, y))
                        continue;

                    if (board.Tiles[x, y] != null) continue;
                    if (!IsSlideFillTarget(x, y)) continue;

                    bool TrySource(int sx, int sy)
                    {
                        if (sx < 0 || sx >= board.Width || sy < 0 || sy >= board.Height) return false;
                        if (board.IsMaskHoleCell(sx, sy) || IsObstacleBlockedCell(sx, sy)) return false;

                        LogVerbose($"[SOURCE] candidate=({sx},{sy}) target=({x},{y}) straightDown={CanTileFallStraightDown(sx, sy)}");

                        var t = board.Tiles[sx, sy];
                        if (t == null) return false;

                        bool targetIsObstaclePocket = IsObstacleBlockedCell(x, y - 1);

                        bool HasUsableOtherSource()
                        {
                            int otherSx = (sx == x - 1) ? (x + 1) : (x - 1);
                            int otherSy = y - 1;

                            if (otherSx < 0 || otherSx >= board.Width || otherSy < 0 || otherSy >= board.Height)
                                return false;

                            if (board.IsMaskHoleCell(otherSx, otherSy) || IsObstacleBlockedCell(otherSx, otherSy))
                                return false;

                            return board.Tiles[otherSx, otherSy] != null;
                        }

                        bool otherSourceExists = HasUsableOtherSource();

                        // Eski davranış:
                        // obstacle pocket değilse ve source düz düşebiliyorsa diyagonal kaydırma.
                        //
                        // Yeni davranış:
                        // Bu kuralı sadece hedefin karşı tarafında da kullanılabilir başka bir source varsa uygula.
                        // Yani edge pocket durumunda tek taraftan akış devam etsin.
                        if (!targetIsObstaclePocket && otherSourceExists && CanTileFallStraightDown(sx, sy))
                            return false;

                        return TryDiagonalFrom(sx, sy, x, y, movedThisPass, moves, moveDelays);
                    }

                    bool moved = TrySource(x - 1, y - 1) || TrySource(x + 1, y - 1);
                    if (moved) movedAny = true;
                }
            }

            if (moves.Count > 0)
                yield return RunManyWithDelays(moves, moveDelays);

            if (!movedAny)
            {
                // Sigorta: bazen CanTileFallStraightDown / obstacle state yüzünden bu pass "no move" sanılıyor.
                // Ama hâlâ doldurulabilir boşluk varsa bir kez daha collapse dene ve tekrar pass'e gir.
                if (!HasAnySlideFillTargetsRemaining())
                    yield break;

                yield return CollapseColumnsAnimated();
                continue;
            }

            yield return CollapseColumnsAnimated();

        }
    }

    private bool HasAnySlideFillTargetsRemaining()
    {
        for (int y = board.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (board.IsMaskHoleCell(x, y) || IsObstacleBlockedCell(x, y))
                    continue;

                if (board.Tiles[x, y] != null)
                    continue;

                if (!IsSlideFillTarget(x, y))
                    continue;

                return true; // hâlâ hedef boşluk var
            }
        }
        return false;
    }

    private bool IsSegmentConnectedToSpawnEdge(int x, int topY)
    {
        if (topY <= 0) return true;

        for (int y = topY - 1; y >= 0; y--)
        {
            if (IsObstacleBlockedCell(x, y))
                return false;

            if (!board.IsSpawnPassThroughCell(x, y))
                return false;
        }

        return true;
    }

    private bool IsSlideFillTarget(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (board.Tiles[x, y] != null)
            return false;

        if (IsNonObstacleHoleCell(x, y))
            return false;

        bool obstacleAbove = IsObstacleBlockedCell(x, y - 1);

        // Normalde mask hole komşuluğunu slide target saymıyoruz.
        // Ama obstacle pocket ise özellikle board kenarında diyagonal akış devam etsin.
        if (IsAdjacentToMaskHole(x, y) && !obstacleAbove)
            return false;

        if (obstacleAbove)
            return true;

        if (IsFloorPocketTarget(x, y))
            return true;

        // ── Spawn alamayan segment ──────────────────────────────────
        // Obstacle altındaki sütun spawn edge'e bağlı değilse
        // (örn. obstacle duvara dayalı olduğunda karşı sütun),
        // bu hücreler sadece diagonal kayma ile doldurulabilir.
        if (IsInNonSpawnableSegment(x, y))
            return true;

        return false;
    }

    /// <summary>
    /// Hücrenin bulunduğu dikey segmentin spawn edge'e bağlı olup olmadığını kontrol eder.
    /// Segment üst sınırı obstacle veya board üstüdür.
    /// </summary>
    private bool IsInNonSpawnableSegment(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        // Segment'in üst sınırını bul (obstacle veya board üstü)
        int topY = y;
        while (topY > 0 && !IsObstacleBlockedCell(x, topY - 1))
            topY--;

        return !IsSegmentConnectedToSpawnEdge(x, topY);
    }

    private bool IsObstacleBlockedCell(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        var obstacleStateService = board.ObstacleStateService;
        if (obstacleStateService == null)
            return false;

        return obstacleStateService.IsCellBlocked(x, y);
    }

    private bool IsNonObstacleHoleCell(int hx, int hy)
    {
        return hx >= 0 && hx < board.Width && hy >= 0 && hy < board.Height
               && board.Holes[hx, hy]
               && !IsObstacleBlockedCell(hx, hy);
    }

    private bool HasAnyTileAboveInSameSegment(int x, int y)
    {
        for (int yy = y - 1; yy >= 0; yy--)
        {
            if (IsObstacleBlockedCell(x, yy))
                break;

            if (IsNonObstacleHoleCell(x, yy))
                continue;

            if (board.Tiles[x, yy] != null)
                return true;
        }
        return false;
    }

    private bool IsFloorPocketTarget(int x, int y)
    {
        bool hasBottomVoid = (y >= board.Height - 1) || IsNonObstacleHoleCell(x, y + 1);
        if (!hasBottomVoid)
            return false;

        if (HasAnyTileAboveInSameSegment(x, y))
            return false;

        return true;
    }

    private bool IsAdjacentToMaskHole(int x, int y)
    {
        if (IsNonObstacleHoleCell(x, y)) return true;

        if (x > 0 && IsNonObstacleHoleCell(x - 1, y)) return true;
        if (x < board.Width - 1 && IsNonObstacleHoleCell(x + 1, y)) return true;
        if (y > 0 && IsNonObstacleHoleCell(x, y - 1)) return true;
        if (y < board.Height - 1 && IsNonObstacleHoleCell(x, y + 1)) return true;

        return false;
    }

    private bool CanTileFallStraightDown(int fromX, int fromY)
    {
        if (fromX < 0 || fromX >= board.Width || fromY < 0 || fromY >= board.Height)
            return false;

        int y = fromY + 1;
        while (y < board.Height)
        {
            if (IsObstacleBlockedCell(fromX, y))
                return false;

            if (board.Holes[fromX, y] && !IsObstacleBlockedCell(fromX, y))
            {
                y++;
                continue;
            }

            return board.Tiles[fromX, y] == null;
        }

        return false;
    }

    private bool TrySlideFrom(
        int fromX, int fromY,
        int toX, int toY,
        HashSet<TileView> movedThisPass,
        List<IEnumerator> moves,
        List<float> delays)
    {
        if (fromX < 0 || fromX >= board.Width) return false;
        if (fromY < 0 || fromY >= board.Height) return false;

        if (board.Holes[fromX, fromY]) return false;

        var tile = board.Tiles[fromX, fromY];
        if (tile == null) return false;
        if (movedThisPass.Contains(tile)) return false;

        board.Tiles[fromX, fromY] = null;
        board.Tiles[toX, toY] = tile;
        tile.SetCoords(toX, toY);
        Debug.Log($"[Slide] PATH ({fromX},{fromY}) -> ({fromX},{toY}) -> ({toX},{toY})");
        float slideDuration = board.GetFallDurationForDistance(1) * 0.6f;
        moves.Add(tile.MoveToGrid(
            board.TileSize,
            slideDuration,
            board.FallMoveCurve,
            false,
            0f,
            0f
        ));

        delays.Add(0f);
        movedThisPass.Add(tile);

        return true;
    }

    /* private bool TryDiagonalFrom(
         int fromX, int fromY,
         int toX, int toY,
         HashSet<TileView> movedThisPass,
         List<IEnumerator> moves,
         List<float> delays)
     {
         return TrySlideFrom(fromX, fromY, toX, toY, movedThisPass, moves, delays);
     }*/

    private bool TryDiagonalFrom(
        int fromX, int fromY,
        int toX, int toY,
        HashSet<TileView> movedThisPass,
        List<IEnumerator> moves,
        List<float> delays)
    {
        // Corner hücreler
        int cax = fromX, cay = toY;
        int cbx = toX, cby = fromY;

        LogVerbose($"[DIAG-TRY] from=({fromX},{fromY}) to=({toX},{toY})");

        // Board sınırı
        if (cax < 0 || cax >= board.Width || cay < 0 || cay >= board.Height) return false;
        if (cbx < 0 || cbx >= board.Width || cby < 0 || cby >= board.Height) return false;

        // Mask hole / oynanamaz köşeden diagonal geçmesin
        if (board.IsMaskHoleCell(cax, cay) || board.IsMaskHoleCell(cbx, cby)) return false;

        var obs = board.ObstacleStateService;
        if (obs != null)
        {
            if (obs.IsCellBlocked(cax, cay))
            {
                if (!obs.GetAllowDiagonalAt(cax, cay))
                    return false;
            }

            if (obs.IsCellBlocked(cbx, cby))
            {
                if (!obs.GetAllowDiagonalAt(cbx, cby))
                    return false;
            }
        }
        bool ok = TrySlideFrom(fromX, fromY, toX, toY, movedThisPass, moves, delays);
        LogVerbose($"[DIAG-RESULT] from=({fromX},{fromY}) to=({toX},{toY}) ok={ok}");
        return ok;
        // return TrySlideFrom(fromX, fromY, toX, toY, movedThisPass, moves, delays);
    }
    public IEnumerator PlayTilesImplodeToCell(
    Vector2Int targetCell,
    IReadOnlyList<TileView> sourceTiles,
    float duration,
    float clearAtNormalizedTime,
    Action<TileView> onTileClear)
    {
        if (tileAnimator == null || sourceTiles == null || sourceTiles.Count == 0)
            yield break;

        yield return tileAnimator.PlayTilesImplodeToCell(
            targetCell,
            sourceTiles,
            duration,
            clearAtNormalizedTime,
            onTileClear);
    }
    private TileType GetRandomType()
    {
        if (board.RandomPool == null || board.RandomPool.Length == 0)
            return TileType.Gear;

        return board.RandomPool[UnityEngine.Random.Range(0, board.RandomPool.Length)];
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogVerbose(string message)
    {
        Debug.Log(message);
    }
}
