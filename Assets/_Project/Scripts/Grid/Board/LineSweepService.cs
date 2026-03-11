using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages lightning strike VFX, line sweep animations, and line-travel instance spawning.
/// Coroutines are started via board.StartCoroutine.
/// </summary>
public class LineSweepService
{
    private readonly BoardController board;
    private readonly List<Vector3> targetPositionsBuffer = new List<Vector3>(32);
    private bool didLogMissingSpawner;
    private const float MinLightningLeadTime = 0.05f;

    public LineSweepService(BoardController board)
    {
        this.board = board;
    }

    public float PlayLightningStrikeForTiles(
        LightningSpawner lightningSpawner,
        IReadOnlyCollection<TileView> matches,
        TileView originTile = null,
        Vector2Int? fallbackOriginCell = null,
        IReadOnlyCollection<TileView> visualTargets = null,
        bool allowCondense = true,
        Action<TileView> onTargetBeamSpawned = null)
    {
        if (lightningSpawner == null)
        {
            if (!didLogMissingSpawner)
            {
                didLogMissingSpawner = true;
                Debug.LogWarning("[Lightning][LineSweepService] lightningSpawner is null, skipping emitter lightning VFX.");
            }
            return 0f;
        }

        if (matches == null || matches.Count == 0) return 0f;

        var targetsForVisuals = visualTargets ?? matches;
        if (onTargetBeamSpawned != null) allowCondense = false;

        Vector3 originWorldPos;
        if (originTile != null)
        {
            originWorldPos = board.GetTileWorldCenter(originTile);
        }
        else if (fallbackOriginCell.HasValue)
        {
            originWorldPos = GetCellWorldCenterPosition(fallbackOriginCell.Value.x, fallbackOriginCell.Value.y);
        }
        else
        {
            originWorldPos = default;
            bool found = false;
            foreach (var t in targetsForVisuals)
            {
                if (t == null) continue;
                originWorldPos = board.GetTileWorldCenter(t);
                found = true;
                break;
            }
            if (!found) return 0f;
        }

        targetPositionsBuffer.Clear();
        var lightningTargetTiles = new List<TileView>(32);

        const float kMinDistFromOrigin = 0.05f;
        float minDistSqr = kMinDistFromOrigin * kMinDistFromOrigin;

        foreach (var tile in targetsForVisuals)
        {
            if (tile == null) continue;
            var p = board.GetTileWorldCenter(tile);
            if ((p - originWorldPos).sqrMagnitude <= minDistSqr) continue;

            bool dup = false;
            for (int i = 0; i < targetPositionsBuffer.Count; i++)
            {
                if ((targetPositionsBuffer[i] - p).sqrMagnitude <= 0.0001f) { dup = true; break; }
            }
            if (!dup)
            {
                targetPositionsBuffer.Add(p);
                lightningTargetTiles.Add(tile);
            }
        }

        if (allowCondense && visualTargets == null)
            TryCondenseLightningTargetsToSingleLine(originWorldPos, targetPositionsBuffer);

        if (targetPositionsBuffer.Count == 0)
        {
            foreach (var t in targetsForVisuals)
            {
                if (t == null) continue;
                targetPositionsBuffer.Add(board.GetTileWorldCenter(t));
                lightningTargetTiles.Add(t);
                break;
            }
        }

        if (targetPositionsBuffer.Count == 0) return 0f;

        float playbackDuration = lightningSpawner.GetPlaybackDuration(targetPositionsBuffer.Count);
        if (playbackDuration <= 0f)
        {
            playbackDuration = MinLightningLeadTime;
            Debug.LogWarning($"[Lightning] Spawner playbackDuration was <= 0. Using fallback {playbackDuration:0.000}s.");
        }

        if (onTargetBeamSpawned != null)
        {
            lightningSpawner.PlayEmitterLightning(originWorldPos, targetPositionsBuffer, idx =>
            {
                if (idx < 0 || idx >= lightningTargetTiles.Count) return;
                onTargetBeamSpawned(lightningTargetTiles[idx]);
            });
        }
        else
        {
            lightningSpawner.PlayEmitterLightning(originWorldPos, targetPositionsBuffer);
        }

        return playbackDuration;
    }

    private void TryCondenseLightningTargetsToSingleLine(Vector3 originWorldPos, List<Vector3> targets)
    {
        if (targets == null || targets.Count < 2) return;

        float tolerance = Mathf.Max(0.01f, board.TileSize * 0.18f);
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        for (int i = 0; i < targets.Count; i++)
        {
            var p = targets[i];
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        bool looksLikeRow = (maxY - minY) <= tolerance;
        bool looksLikeCol = (maxX - minX) <= tolerance;
        if (!looksLikeRow && !looksLikeCol) return;

        targets.Clear();
        if (looksLikeRow)
        {
            targets.Add(new Vector3(minX, originWorldPos.y, originWorldPos.z));
            targets.Add(new Vector3(maxX, originWorldPos.y, originWorldPos.z));
            return;
        }

        targets.Add(new Vector3(originWorldPos.x, minY, originWorldPos.z));
        targets.Add(new Vector3(originWorldPos.x, maxY, originWorldPos.z));
    }

    public float PlayLightningLineStrikes(
        LightningSpawner lightningSpawner,
        LineTravelSplitSwapTestUI lineTravelPlayer,
        IReadOnlyList<LightningLineStrike> lineStrikes,
        Action<Vector2Int> onSweepCellReached = null)
    {
        if (lineStrikes == null || lineStrikes.Count == 0) return 0f;
        if (lineTravelPlayer == null && lightningSpawner == null) return 0f;

        const float StrikeStagger = 0.03f;
        float maxEndTime = 0f;

        for (int i = 0; i < lineStrikes.Count; i++)
        {
            var strike = lineStrikes[i];
            int x = strike.originCell.x;
            int y = strike.originCell.y;
            if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;

            float delay = StrikeStagger * i;

            board.OnLineSweepStartedInternal(strike, delay);

            void EmitSweepCell(Vector2Int cell)
            {
                onSweepCellReached?.Invoke(cell);
                board.OnLineSweepCellReachedInternal(cell, strike);
            }

            float endTime = PlayTwoWaySweep(
                lightningSpawner, lineTravelPlayer,
                x, y, strike.isHorizontal, delay, EmitSweepCell);

            if (endTime > maxEndTime) maxEndTime = endTime;
        }

        return maxEndTime;
    }

    private float PlayTwoWaySweep(
        LightningSpawner lightningSpawner,
        LineTravelSplitSwapTestUI lineTravelPlayer,
        int originX, int originY, bool isHorizontal,
        float delaySeconds,
        Action<Vector2Int> onSweepCellReached)
    {
        if (lineTravelPlayer != null)
        {
            Vector3 worldCenter = GetCellWorldCenterPosition(
                isHorizontal ? originX : originX,
                isHorizontal ? originY : originY);

            RectTransform spaceRt = lineTravelPlayer.afterImageParent != null
                ? lineTravelPlayer.afterImageParent
                : (board.LineTravelSpawnParent as RectTransform);

            if (spaceRt == null && lineTravelPlayer.transform.parent != null)
                spaceRt = lineTravelPlayer.transform.parent as RectTransform;

            if (spaceRt != null)
            {
                Vector2 originAnchored = board.WorldToAnchoredIn(spaceRt, worldCenter);
                int steps = isHorizontal
                    ? Mathf.Max(originX, board.Width - 1 - originX)
                    : Mathf.Max(originY, board.Height - 1 - originY);

                var axis = isHorizontal
                    ? LineTravelSplitSwapTestUI.LineAxis.Horizontal
                    : LineTravelSplitSwapTestUI.LineAxis.Vertical;

                var originCell = new Vector2Int(originX, originY);

                PlayLineTravelInstanceWithStep(lineTravelPlayer, axis, originAnchored, originCell,
                    steps, board.TileSize, delaySeconds, onSweepCellReached);

                float duration = lineTravelPlayer.EstimateDuration(steps);
                return delaySeconds + duration;
            }
        }

        // Fallback: lightning sweep
        if (lightningSpawner == null) return 0f;

        if (isHorizontal)
        {
            var left = new List<Vector3>(originX + 1);
            for (int x = originX; x >= 0; x--) left.Add(GetCellWorldCenterPosition(x, originY));
            var right = new List<Vector3>(board.Width - originX);
            for (int x = originX; x < board.Width; x++) right.Add(GetCellWorldCenterPosition(x, originY));

            lightningSpawner.PlayLineSweepSteps(left);
            lightningSpawner.PlayLineSweepSteps(right);

            float sweepDur = Mathf.Max(
                lightningSpawner.GetPlaybackDuration(left.Count),
                lightningSpawner.GetPlaybackDuration(right.Count));

            EmitSweepCallbacks(originX, originY, true, delaySeconds, sweepDur, onSweepCellReached);
            return delaySeconds + sweepDur;
        }
        else
        {
            var down = new List<Vector3>(originY + 1);
            for (int y = originY; y >= 0; y--) down.Add(GetCellWorldCenterPosition(originX, y));
            var up = new List<Vector3>(board.Height - originY);
            for (int y = originY; y < board.Height; y++) up.Add(GetCellWorldCenterPosition(originX, y));

            lightningSpawner.PlayLineSweepSteps(down);
            lightningSpawner.PlayLineSweepSteps(up);

            float sweepDur = Mathf.Max(
                lightningSpawner.GetPlaybackDuration(down.Count),
                lightningSpawner.GetPlaybackDuration(up.Count));

            EmitSweepCallbacks(originX, originY, false, delaySeconds, sweepDur, onSweepCellReached);
            return delaySeconds + sweepDur;
        }
    }

    private void EmitSweepCallbacks(int originX, int originY, bool horizontal,
        float delaySeconds, float sweepDuration, Action<Vector2Int> onSweepCellReached)
    {
        if (onSweepCellReached == null) return;

        int maxDistance = horizontal
            ? Mathf.Max(originX, board.Width - 1 - originX)
            : Mathf.Max(originY, board.Height - 1 - originY);
        float stepInterval = maxDistance > 0 ? sweepDuration / maxDistance : 0f;

        board.StartCoroutine(CoEmitLineSweepCellCallbacks(delaySeconds, stepInterval, maxDistance, step =>
        {
            if (horizontal)
            {
                int leftX = originX - step;
                if (leftX >= 0 && leftX < board.Width) onSweepCellReached(new Vector2Int(leftX, originY));
                if (step > 0) { int rightX = originX + step; if (rightX < board.Width) onSweepCellReached(new Vector2Int(rightX, originY)); }
            }
            else
            {
                int downY = originY - step;
                if (downY >= 0 && downY < board.Height) onSweepCellReached(new Vector2Int(originX, downY));
                if (step > 0) { int upY = originY + step; if (upY < board.Height) onSweepCellReached(new Vector2Int(originX, upY)); }
            }
        }));
    }

    private IEnumerator CoEmitLineSweepCellCallbacks(float delaySeconds, float stepInterval, int maxDistance, Action<int> emitStep)
    {
        if (delaySeconds > 0f) yield return new WaitForSeconds(delaySeconds);
        for (int step = 0; step <= maxDistance; step++)
        {
            emitStep?.Invoke(step);
            if (stepInterval > 0f) yield return new WaitForSeconds(stepInterval);
            else yield return null;
        }
    }

    public float PlayLineTravelInstanceWithStep(
        LineTravelSplitSwapTestUI lineTravelPlayer,
        LineTravelSplitSwapTestUI.LineAxis axis,
        Vector2 originAnchored, Vector2Int originCell,
        int steps, float cellSizePx, float delaySeconds,
        Action<Vector2Int> onStep)
    {
        if (lineTravelPlayer == null) return 0f;

        Transform parentTr = board.LineTravelSpawnParent != null
            ? board.LineTravelSpawnParent
            : (lineTravelPlayer.transform.parent != null ? lineTravelPlayer.transform.parent : board.transform);

        var go = UnityEngine.Object.Instantiate(lineTravelPlayer.gameObject, parentTr);
        go.SetActive(true);

        var inst = go.GetComponent<LineTravelSplitSwapTestUI>();
        if (inst == null) { UnityEngine.Object.Destroy(go); return 0f; }

        if (inst.afterImageParent == null && lineTravelPlayer.afterImageParent != null)
            inst.afterImageParent = lineTravelPlayer.afterImageParent;
        if (inst.impactParent == null && lineTravelPlayer.impactParent != null)
            inst.impactParent = lineTravelPlayer.impactParent;

        board.StartCoroutine(CoPlayLineTravel(inst, axis, originAnchored, originCell, steps, cellSizePx, delaySeconds, onStep));

        float dur = lineTravelPlayer.EstimateDuration(steps);
        float totalLife = Mathf.Max(0f, delaySeconds) + dur + 0.15f;
        board.StartCoroutine(CoDestroyAfterUnscaled(go, totalLife));

        return delaySeconds + dur;
    }

    private IEnumerator CoPlayLineTravel(
        LineTravelSplitSwapTestUI inst,
        LineTravelSplitSwapTestUI.LineAxis axis,
        Vector2 originAnchored, Vector2Int originCell,
        int steps, float cellSizePx, float delaySeconds,
        Action<Vector2Int> onStep)
    {
        if (delaySeconds > 0f) yield return new WaitForSecondsRealtime(delaySeconds);
        if (inst != null) inst.Play(axis, originAnchored, originCell, steps, cellSizePx, onStep);
    }

    private IEnumerator CoDestroyAfterUnscaled(GameObject go, float delaySeconds)
    {
        if (delaySeconds > 0f) yield return new WaitForSecondsRealtime(delaySeconds);
        if (go != null) UnityEngine.Object.Destroy(go);
    }

    public Vector3 GetCellWorldCenterPosition(int x, int y)
    {
        Vector3 localCenter = new Vector3(x * board.TileSize + board.TileSize * 0.5f, -y * board.TileSize - board.TileSize * 0.5f, 0f);
        if (board.Parent != null) return board.Parent.TransformPoint(localCenter);
        return board.transform.TransformPoint(localCenter);
    }
}