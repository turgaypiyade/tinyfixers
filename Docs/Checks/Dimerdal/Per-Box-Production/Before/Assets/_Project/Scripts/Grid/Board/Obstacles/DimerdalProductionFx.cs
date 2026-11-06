using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// KeyGenerator-style nonblocking production. Only the ghost flies; targets must
/// remain idle and are reselected if gravity or another action starts moving them.
/// A local view claim prevents duplicate diamonds without holding any board cell.
internal sealed class DimerdalProductionFx
{
    private const string PoolKey = "Dimerdal.DiamondFlight";
    private const float RiseSeconds = .3f;
    private const float HoverSeconds = .06f;
    private const float FlySeconds = .55f;
    private const float RetrySeconds = .1f;
    private const float TimeoutSeconds = 3f;

    private sealed class Flight
    {
        public BoardController board;
        public LevelData level;
        public Image image;
        public RectTransform root;
        public IDisposable job;
        public TileView target;
        public int lifetime;
        public ObstacleId diamond;
        public Vector2 start, peak, from, position;
        public float age, legAge, retry, cellSize;
    }

    private readonly List<Flight> active = new(8);
    private readonly Stack<Flight> pool = new();
    private readonly HashSet<TileView> claimed = new();
    internal bool HasActiveFlights => active.Count > 0;

    public bool Launch(BoardController board, Image source, Sprite sprite, ObstacleId diamond)
    {
        var root = board != null ? (board.BreakFxParent != null ? board.BreakFxParent : board.ContentRoot) : null;
        if (board == null || !board.isActiveAndEnabled || root == null || source == null || sprite == null) return false;
        var image = UiVfxPool.RentImage(PoolKey, root, "DimerdalDiamondFlight");
        if (image == null) return false;
        var flight = pool.Count > 0 ? pool.Pop() : new Flight();
        flight.board = board;
        flight.level = board.ActiveLevelData;
        flight.root = root;
        flight.image = image;
        flight.job = board.BeginJob(BoardController.BoardJobKind.KeyFlight);
        flight.diamond = diamond;
        flight.target = null;
        flight.age = flight.legAge = flight.retry = 0f;
        flight.start = root.InverseTransformPoint(source.rectTransform.TransformPoint(source.rectTransform.rect.center));
        // Use a board cell measured in the overlay's coordinates (source can span 3x3).
        var a = board.WorldToAnchoredIn(root, board.GetCellWorldCenterPosition(0, 0));
        var b = board.WorldToAnchoredIn(root, board.GetCellWorldCenterPosition(1, 0));
        flight.cellSize = Mathf.Max(1f, Vector2.Distance(a, b));
        float fan = ((active.Count % 3) - 1) * flight.cellSize * .32f;
        flight.peak = flight.start + new Vector2(fan, flight.cellSize * 2.5f);
        flight.position = flight.start;
        image.sprite = sprite;
        image.gameObject.layer = root.gameObject.layer;
        image.preserveAspect = true;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = root.pivot;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = Vector2.one * (flight.cellSize * DimerdalRules.BoardDiamondSize);
        rect.anchoredPosition = flight.start;
        rect.localScale = Vector3.one * .25f;
        rect.SetAsLastSibling();
        active.Add(flight);
        return true;
    }

    // Return failed commitments to the service; successful placement keeps its quota.
    public int Tick(float delta)
    {
        int failed = 0;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var flight = active[i];
            bool placed = false;
            bool done = flight.board == null || !flight.board.isActiveAndEnabled
                || flight.board.ActiveLevelData != flight.level || flight.image == null || flight.root == null;
            if (!done) done = Advance(flight, delta, out placed);
            if (!done) continue;
            if (!placed) failed++;
            Release(flight, true);
            active.RemoveAt(i);
        }
        return failed;
    }

    private bool Advance(Flight flight, float delta, out bool placed)
    {
        placed = false;
        flight.age += delta;
        if (flight.age >= TimeoutSeconds) return true;
        var rect = flight.image.rectTransform;
        if (flight.age < RiseSeconds + HoverSeconds)
        {
            float t = Mathf.Clamp01(flight.age / RiseSeconds);
            float e = 1f - (1f - t) * (1f - t);
            flight.position = Vector2.LerpUnclamped(flight.start, flight.peak, e);
            rect.anchoredPosition = flight.position;
            rect.localScale = Vector3.one * Mathf.Lerp(.25f, 1.45f, e);
            return false;
        }

        // Revalidate the same view/lifetime and its idle state. A target that
        // starts moving during the flight must be replaced with a fresh idle stone.
        if (!ValidTarget(flight))
        {
            Unclaim(flight);
            flight.retry -= delta;
            if (flight.retry > 0f) return false;
            flight.retry = RetrySeconds;
            if (!FindTarget(flight)) return false;
            flight.from = flight.position;
            flight.legAge = 0f;
        }

        flight.legAge += delta;
        float k = Mathf.Clamp01(flight.legAge / FlySeconds);
        float eased = k * k * (3f - 2f * k);
        var targetRect = flight.target.IconImage != null ? flight.target.IconImage.rectTransform : flight.target.RectTransform;
        Vector2 end = flight.root.InverseTransformPoint(targetRect.TransformPoint(targetRect.rect.center));
        Vector2 control = (flight.from + end) * .5f + Vector2.up * (flight.cellSize * 1.4f);
        float u = 1f - eased;
        flight.position = u * u * flight.from + 2f * u * eased * control + eased * eased * end;
        rect.anchoredPosition = flight.position;
        rect.localScale = Vector3.one * Mathf.Lerp(1.45f, 1f, eased);
        if (k < 1f) return false;

        // Synchronous replace at the target's CURRENT logical cell, preserving the
        // real TileView's presentation and every motion/target owner.
        placed = flight.board.TryConvertDimerdalDiamond(flight.target, flight.diamond);
        if (placed) return true;
        Unclaim(flight);
        flight.retry = 0f;
        return false;
    }

    private bool ValidTarget(Flight flight) => flight.target != null
        && flight.target.IsCurrentLifetime(flight.lifetime) && flight.board.CanConvertDimerdalDiamond(flight.target);

    private bool FindTarget(Flight flight)
    {
        int seen = 0;
        TileView chosen = null;
        for (int y = 0; y < flight.board.Height; y++)
            for (int x = 0; x < flight.board.Width; x++)
            {
                var tile = flight.board.GetTileViewAt(x, y);
                if (tile == null || claimed.Contains(tile) || !flight.board.CanConvertDimerdalDiamond(tile)) continue;
                if (UnityEngine.Random.Range(0, ++seen) == 0) chosen = tile;
            }
        if (chosen == null) return false;
        flight.target = chosen;
        flight.lifetime = chosen.LifetimeVersion;
        claimed.Add(chosen);
        return true;
    }

    private void Unclaim(Flight flight)
    {
        if (flight.target != null) claimed.Remove(flight.target);
        flight.target = null;
    }

    private void Release(Flight flight, bool resolve)
    {
        Unclaim(flight);
        if (flight.image != null) UiVfxPool.Return(PoolKey, flight.image.gameObject);
        flight.job?.Dispose();
        if (resolve && flight.board != null && flight.board.isActiveAndEnabled && flight.board.ActiveLevelData == flight.level)
            flight.board.RequestResolveAfterActionSequence();
        flight.job = null;
        flight.image = null;
        flight.root = null;
        flight.board = null;
        flight.level = null;
        pool.Push(flight);
    }

    public int CancelAll()
    {
        int canceled = active.Count;
        foreach (var flight in active) Release(flight, false);
        active.Clear();
        claimed.Clear();
        return canceled;
    }
}
