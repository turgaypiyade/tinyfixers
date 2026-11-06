using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Uses the very same pop/orbit/gather/launch/arrival routine as TarpStatue.
/// Copies leave immediately; gravity never waits, only level completion does.
internal sealed class DimerdalCollectionFx
{
    private sealed class Flight : ICollectionFxTarget
    {
        public BoardController board;
        public LevelData level;
        public RectTransform target;
        public AncientSealCollectFx fx;
        public IEnumerator routine;
        public IDisposable job;
        public bool arrived;
        public readonly Action arrival;

        public Flight() { arrival = Arrive; }
        public bool IsCollectionAlive => board != null && board.isActiveAndEnabled
            && board.ActiveLevelData == level && target != null && target.gameObject.activeInHierarchy;
        public Vector3 GetCollectionTarget(int color) => target.TransformPoint(target.rect.center);
        public bool IsCollectionTargetReady(int color) => true;

        public void Arrive()
        {
            if (arrived) return;
            arrived = true;
            if (board != null && board.ActiveLevelData == level)
                board.TopHud?.NotifyCollectibleCollected(CollectibleId.Diamond, 1);
        }
    }

    private readonly List<Flight> active = new(8);
    private readonly Stack<Flight> pool = new();
    private AncientSealService.GatherBatch batch;
    private int batchFrame = -1;

    public bool Launch(DimerdalService host, BoardController board, TileView tile, ObstacleId id)
    {
        var hud = board != null ? board.TopHud : null;
        if (host == null || !host.isActiveAndEnabled || tile == null || hud == null
            || !hud.TryGetGoalTargetRectForCollectible(CollectibleId.Diamond, out var target)) return false;
        var overlay = board.GoalFlyFx != null ? board.GoalFlyFx.OverlayRoot : null;
        var sprite = tile.GetIconSprite();
        if (overlay == null || sprite == null) return false;
        if (batchFrame != Time.frameCount)
        {
            batch = new AncientSealService.GatherBatch();
            batchFrame = Time.frameCount;
        }
        var source = tile.IconImage != null ? tile.IconImage.rectTransform : tile.RectTransform;
        Vector3 start = source.TransformPoint(source.rect.center);
        int order = batch.count++;
        batch.worldSum += start;
        var flight = pool.Count > 0 ? pool.Pop() : new Flight();
        flight.board = board;
        flight.level = board.ActiveLevelData;
        flight.target = target;
        flight.arrived = false;
        flight.job = board.BeginJob(BoardController.BoardJobKind.GoalOrbFlight);
        int color = DimerdalRules.ColorIndex(id);
        Color tint = color == 0 ? new Color(.2f, .65f, 1f) : color == 1 ? new Color(.3f, 1f, .45f)
            : color == 2 ? new Color(1f, .82f, .2f) : new Color(1f, .25f, .3f);
        flight.fx = new AncientSealCollectFx(host, overlay, board, flight, TileType.Normal, color,
            start, batch, order, tint, spriteOverride: sprite, pooledVisuals: true);
        flight.routine = flight.fx.Play(flight.arrival);
        active.Add(flight);
        return true;
    }

    public void Tick()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var flight = active[i];
            bool running = false;
            try { running = flight.IsCollectionAlive && flight.routine.MoveNext(); }
            finally
            {
                // Also release when a shared effect exits early or throws. A hit
                // already removed the real diamond; same-level credit must survive.
                if (!running) { Release(flight, true); active.RemoveAt(i); }
            }
        }
    }

    private void Release(Flight flight, bool credit)
    {
        if (credit) flight.Arrive();
        (flight.routine as IDisposable)?.Dispose();
        flight.fx?.Dispose();
        flight.job?.Dispose();
        if (credit && flight.board != null && flight.board.isActiveAndEnabled && flight.board.ActiveLevelData == flight.level)
            flight.board.RequestResolveAfterActionSequence();
        flight.routine = null;
        flight.fx = null;
        flight.job = null;
        flight.target = null;
        flight.board = null;
        flight.level = null;
        pool.Push(flight);
    }

    public void CancelAll(bool credit)
    {
        foreach (var flight in active) Release(flight, credit);
        active.Clear();
        batch = null;
        batchFrame = -1;
    }
}
