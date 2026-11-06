using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Nonblocking movable egg delivery; shared idle normal-tile replacement guard. Only the ghost flies; targets must
/// remain idle and are reselected if gravity or another action starts moving them.
/// A local view claim prevents duplicate eggs without holding any board cell.
internal sealed class FlyEggProductionFx
{
    private const string PoolKey = "FlyFruit.EggFlight";
    private const float RiseSeconds = .42f;
    private const float HoverSeconds = .04f;
    private const float FlySeconds = .62f;
    private const float LaunchScale = .65f;
    private const float PeakScale = 2f;
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
        public int lifetime, origin, slot;
        public ObstacleId diamond;
        public Vector2 start, peak, from, position;
        public float age, legAge, retry, cellSize, scale, legScale, tilt, legTilt, peakTilt;
        public bool soundPlayed;
    }

    private readonly List<Flight> active = new(8);
    private readonly Stack<Flight> pool = new();
    private readonly HashSet<TileView> claimed = new();
    private int generation;
    internal bool HasActiveFlights => active.Count > 0;
    public Action<int, int, bool> OnFinished;

    public void Warm(RectTransform root, int count)
    {
        count = Mathf.Clamp(count, 0, 96);
        if (active.Capacity < count) active.Capacity = count;
        for (int i = pool.Count; i < count; i++) pool.Push(new Flight());
        if (root == null) return;
        var images = new List<Image>(count);
        for (int i = 0; i < count; i++)
        {
            var image = UiVfxPool.RentImage(PoolKey, root, "FlyEggFlight");
            if (image != null) images.Add(image);
        }
        foreach (var image in images) UiVfxPool.Return(PoolKey, image.gameObject);
    }

    public bool Launch(BoardController board, Image source, Sprite sprite, ObstacleId diamond, int origin, int slot)
    {
        var root = board != null ? (board.BreakFxParent != null ? board.BreakFxParent : board.ContentRoot) : null;
        if (board == null || !board.isActiveAndEnabled || root == null || source == null || sprite == null) return false;
        var image = UiVfxPool.RentImage(PoolKey, root, "FlyEggFlight");
        if (image == null) return false;
        var flight = pool.Count > 0 ? pool.Pop() : new Flight();
        flight.board = board;
        flight.level = board.ActiveLevelData;
        flight.root = root;
        flight.image = image;
        flight.job = board.BeginJob(BoardController.BoardJobKind.KeyFlight);
        flight.diamond = diamond;
        flight.origin = origin;
        flight.slot = slot;
        flight.target = null;
        flight.age = flight.legAge = flight.retry = 0f;
        flight.scale = flight.legScale = LaunchScale;
        flight.tilt = flight.legTilt = 0f;
        flight.peakTilt = UnityEngine.Random.Range(-12f, 12f);
        flight.soundPlayed = false;
        flight.start = root.InverseTransformPoint(source.rectTransform.TransformPoint(source.rectTransform.rect.center));
        // Use a board cell measured in the overlay's coordinates (source can span 3x3).
        var a = board.WorldToAnchoredIn(root, board.GetCellWorldCenterPosition(0, 0));
        var b = board.WorldToAnchoredIn(root, board.GetCellWorldCenterPosition(board.Width > 1 ? 1 : 0, board.Width > 1 ? 0 : 1));
        flight.cellSize = Mathf.Max(1f, Vector2.Distance(a, b));
        float fan = ((active.Count % 3) - 1) * flight.cellSize * .32f;
        flight.peak = flight.start + new Vector2(fan, flight.cellSize * 1.9f);
        flight.peak.y = Mathf.Min(flight.peak.y, a.y + flight.cellSize * .8f);
        flight.position = flight.start;
        image.sprite = sprite;
        image.gameObject.layer = root.gameObject.layer;
        image.preserveAspect = true;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = root.pivot;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = Vector2.one * (flight.cellSize * .88f);
        rect.anchoredPosition = flight.start;
        rect.localScale = Vector3.one * LaunchScale;
        rect.localRotation = Quaternion.identity;
        rect.SetAsLastSibling();
        active.Add(flight);
        return true;
    }

    // Return failed commitments to the service; successful placement consumes its source slot.
    public int Tick(float delta)
    {
        int failed = 0;
        int tickGeneration = generation;
        for (int i = active.Count - 1; i >= 0 && generation == tickGeneration; i--)
        {
            var flight = active[i];
            bool placed = false;
            bool done = flight.board == null || !flight.board.isActiveAndEnabled
                || flight.board.ActiveLevelData != flight.level || flight.image == null || flight.root == null;
            if (!done) done = Advance(flight, delta, out placed);
            if (!done) continue;
            if (!placed) failed++;
            active.RemoveAt(i);
            try { OnFinished?.Invoke(flight.origin, flight.slot, placed); }
            finally { Release(flight, true); }
        }
        return failed;
    }

    private bool Advance(Flight flight, float delta, out bool placed)
    {
        placed = false;
        flight.age += delta;
        if (flight.age >= TimeoutSeconds) return true;
        var rect = flight.image.rectTransform;
        if (!flight.soundPlayed && flight.age >= .12f)
        {
            flight.soundPlayed = true;
            var def = flight.level.obstacleLibrary?.Get(ObstacleId.FlyEggBox);
            if (def != null) flight.board.Audio?.PlayOneShotClip(def.flightLoopSound, def.flightLoopSoundVolume);
        }
        if (flight.age < RiseSeconds + HoverSeconds)
        {
            float t = Mathf.Clamp01(flight.age / RiseSeconds);
            float e = 1f - Mathf.Pow(1f - t, 2.3f);
            flight.position = Vector2.LerpUnclamped(flight.start, flight.peak, e);
            rect.anchoredPosition = flight.position;
            flight.scale = Mathf.Lerp(LaunchScale, PeakScale, e);
            flight.tilt = flight.peakTilt * e - Mathf.Sin(t * Mathf.PI) * 8f;
            float stretch = Mathf.Sin(t * Mathf.PI) * .10f;
            rect.localScale = new Vector3(flight.scale * (1f - stretch * .5f), flight.scale * (1f + stretch), 1f);
            rect.localRotation = Quaternion.Euler(0, 0, flight.tilt);
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
            flight.legScale = flight.scale;
            flight.legTilt = flight.tilt;
            flight.legAge = 0f;
        }

        flight.legAge += delta;
        float k = Mathf.Clamp01(flight.legAge / FlySeconds);
        float eased = k * k * (3f - 2f * k);
        var targetRect = flight.target.IconImage != null ? flight.target.IconImage.rectTransform : flight.target.RectTransform;
        Vector2 end = flight.root.InverseTransformPoint(targetRect.TransformPoint(targetRect.rect.center));
        Vector2 control = (flight.from + end) * .5f + Vector2.up * (flight.cellSize * .55f);
        float u = 1f - eased;
        flight.position = u * u * flight.from + 2f * u * eased * control + eased * eased * end;
        rect.anchoredPosition = flight.position;
        flight.scale = Mathf.Lerp(flight.legScale, 1f, eased);
        flight.tilt = Mathf.Lerp(flight.legTilt, 0f, eased) + Mathf.Sin(k * Mathf.PI) * (end.x >= flight.from.x ? -10f : 10f);
        rect.localScale = Vector3.one * flight.scale;
        rect.localRotation = Quaternion.Euler(0, 0, flight.tilt);
        if (k < 1f) return false;

        // Synchronous replace at the target's CURRENT logical cell, preserving the
        // real TileView identity. It then joins the ordinary movable fall/swap path.
        placed = flight.board.TryPlaceFlyEgg(flight.target, flight.lifetime);
        if (placed) return true;
        Unclaim(flight);
        flight.retry = 0f;
        return false;
    }

    private bool ValidTarget(Flight flight) => flight.target != null
        && flight.target.IsCurrentLifetime(flight.lifetime) && flight.board.CanReplaceIdleColorTile(flight.target);

    private bool FindTarget(Flight flight)
    {
        int seen = 0;
        TileView chosen = null;
        for (int y = 0; y < flight.board.Height; y++)
            for (int x = 0; x < flight.board.Width; x++)
            {
                var tile = flight.board.GetTileViewAt(x, y);
                if (tile == null || claimed.Contains(tile) || !flight.board.CanReplaceIdleColorTile(tile)) continue;
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
        generation++;
        int canceled = active.Count;
        while (active.Count > 0)
        {
            int i = active.Count - 1;
            var flight = active[i];
            active.RemoveAt(i);
            try { OnFinished?.Invoke(flight.origin, flight.slot, false); }
            finally { Release(flight, false); }
        }
        claimed.Clear();
        return canceled;
    }
}
