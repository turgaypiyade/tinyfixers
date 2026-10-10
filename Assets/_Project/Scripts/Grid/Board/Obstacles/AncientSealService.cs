using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A separate collection obstacle. Stones reduce the counter only on arrival.</summary>
public sealed class AncientSealService : MonoBehaviour
{
    public const int LockCount = 3;
    private sealed class SealState
    {
        public readonly int[] remaining = new int[LockCount];
        public readonly int[] total = new int[LockCount];
        public readonly int[] inFlight = new int[LockCount];
        public int startColor, activeStep;
        public int active => activeStep < LockCount ? (startColor + activeStep) % LockCount : LockCount;
    }

    private sealed class CollectionFlight
    {
        public int origin, color;
        public SealState state;
        public IDisposable job;
        public AncientSealCollectFx fx;
        public bool arrived;
    }

    private BoardController board;
    internal BoardController Board => board;
    private readonly Dictionary<int, SealState> seals = new();
    private readonly Dictionary<int, AncientSealView> views = new();
    private readonly List<int> eligibleOrigins = new();
    private readonly Dictionary<int, int> startingColors = new();
    private readonly int[] lastAssignedOrigin = { -1, -1, -1 };
    private readonly HashSet<CollectionFlight> flights = new();
    private RectTransform flightRoot;
    public event Action<int, int, int, int> OnCollected;
    public event Action<int, int> OnLockClosed;
    public event Action<int> OnSealBroken;

    // Called synchronously before stamping, so the first clear cannot precede binding.
    public void Initialize(BoardController owner, AncientSealEntry[] entries)
    {
        Unbind();
        CancelFlights();
        board = owner;
        seals.Clear();
        views.Clear();
        ConfigureStartingColors(entries);
        for (int i = 0; i < LockCount; i++) lastAssignedOrigin[i] = -1;
        if (isActiveAndEnabled && board != null) board.OnTileCollectedAt += HandleTileCollectedAt;
    }

    private void OnEnable()
    {
        if (board == null) return;
        board.OnTileCollectedAt -= HandleTileCollectedAt;
        board.OnTileCollectedAt += HandleTileCollectedAt;
    }
    private void OnDisable()
    {
        Unbind();
        CancelFlights();
    }
    private void Unbind()
    {
        if (board != null) board.OnTileCollectedAt -= HandleTileCollectedAt;
    }

    public void RegisterView(int origin, AncientSealView view) => views[origin] = view;
    public void UnregisterView(int origin, AncientSealView view)
    {
        if (views.TryGetValue(origin, out var current) && current == view) views.Remove(origin);
    }

    private void ConfigureStartingColors(AncientSealEntry[] entries)
    {
        startingColors.Clear();
        var usage = new int[LockCount];
        var ordered = new List<AncientSealEntry>(entries ?? Array.Empty<AncientSealEntry>());
        ordered.Sort((a, b) => a.originCellIndex.CompareTo(b.originCellIndex));
        // Reserve explicit choices first, regardless of authored or stack order.
        foreach (var entry in ordered)
        {
            int color = (int)entry.startColor - 1;
            if (color < 0 || color >= LockCount) continue;
            startingColors[entry.originCellIndex] = color;
            usage[color]++;
        }
        foreach (var entry in ordered)
        {
            if (startingColors.ContainsKey(entry.originCellIndex)) continue;
            // Ties prefer red, then green, then yellow: two default seals start differently.
            int color = 0;
            if (usage[2] < usage[color]) color = 2;
            if (usage[1] < usage[color]) color = 1;
            startingColors[entry.originCellIndex] = color;
            usage[color]++;
        }
    }

    public int GetLockAtStep(int origin, int step)
        => seals.TryGetValue(origin, out var s) && step >= 0 && step < LockCount
            ? (s.startColor + step) % LockCount : -1;

    public void RegisterSeal(AncientSealEntry entry)
    {
        var state = new SealState
        {
            startColor = startingColors.TryGetValue(entry.originCellIndex, out int color) ? color : 0
        };
        state.remaining[0] = state.total[0] = Mathf.Max(1, entry.redCount);
        state.remaining[1] = state.total[1] = Mathf.Max(1, entry.yellowCount);
        state.remaining[2] = state.total[2] = Mathf.Max(1, entry.greenCount);
        seals[entry.originCellIndex] = state;
    }

    public int GetActiveLock(int origin) => seals.TryGetValue(origin, out var s) ? s.active : -1;
    public int GetRemaining(int origin, int color)
        => seals.TryGetValue(origin, out var s) && color >= 0 && color < LockCount ? s.remaining[color] : 0;
    public int GetRemainingTotal(int origin)
        => GetRemaining(origin, 0) + GetRemaining(origin, 1) + GetRemaining(origin, 2);

    private void HandleTileCollectedAt(TileType type, Vector3 worldPosition)
    {
        int color = type == TileType.Core ? 0 : type == TileType.Gear ? 1 : type == TileType.Plate ? 2 : -1;
        var obstacles = board != null ? board.ObstacleStateService : null;
        if (color < 0 || obstacles == null) return;

        // Reserve only the missing stones. A 3-match with two remaining launches two flights.
        eligibleOrigins.Clear();
        foreach (var pair in seals)
        {
            int origin = pair.Key;
            var s = pair.Value;
            if (s.active != color || s.remaining[color] <= s.inFlight[color]
                || obstacles.IsBuriedAnywhere(ObstacleId.AncientSeal, origin)) continue;
            if (obstacles.GetObstacleIdAt(origin % board.Width, origin / board.Width) == ObstacleId.AncientSeal)
                eligibleOrigins.Add(origin);
        }
        if (eligibleOrigins.Count == 0) return;
        eligibleOrigins.Sort();
        // Each cleared stone has ONE destination. Equal-color seals take turns, including
        // reservations still in flight, so a single match cannot credit every seal.
        int selected = eligibleOrigins.FindIndex(origin => origin > lastAssignedOrigin[color]);
        int targetOrigin = eligibleOrigins[selected >= 0 ? selected : 0];
        lastAssignedOrigin[color] = targetOrigin;
        LaunchCollection(targetOrigin, color, type, worldPosition);
    }

    private void LaunchCollection(int origin, int color, TileType type, Vector3 worldPosition)
    {
        var s = seals[origin];
        float stagger = Mathf.Min(s.inFlight[color] * 0.045f, 0.27f);
        s.inFlight[color]++;
        var flight = new CollectionFlight { origin = origin, color = color, state = s };
        // Rendering can be disabled for board tooling; preserve its logical collection path.
        if (!views.TryGetValue(origin, out var view) || view == null || !view.isActiveAndEnabled
            || !EnsureFlightRoot() || board.GetIcon(type) == null)
        {
            Arrive(flight);
            return;
        }

        // Flights let gravity continue, but keep win/lose evaluation waiting for arrivals.
        flight.job = board.BeginJob(BoardController.BoardJobKind.GoalOrbFlight);
        flight.fx = new AncientSealCollectFx(this, flightRoot, board, view, type, color, worldPosition, stagger);
        flights.Add(flight);
        StartCoroutine(Fly(flight));
    }

    private IEnumerator Fly(CollectionFlight flight)
    {
        try
        {
            yield return flight.fx.Play(() => Arrive(flight));
        }
        finally
        {
            ReleaseFlight(flight);
        }
    }

    private void Arrive(CollectionFlight flight)
    {
        if (flight.arrived) return;
        flight.arrived = true;
        var s = flight.state;
        int color = flight.color, origin = flight.origin;
        s.inFlight[color] = Mathf.Max(0, s.inFlight[color] - 1);
        if (!seals.TryGetValue(origin, out var current) || current != s || s.active != color) return;
        s.remaining[color] = Mathf.Max(0, s.remaining[color] - 1);
        OnCollected?.Invoke(origin, color, s.remaining[color], s.total[color]);
        if (s.remaining[color] > 0) return;
        s.activeStep++;
        OnLockClosed?.Invoke(origin, color);
        if (s.activeStep < LockCount) return;

        OnSealBroken?.Invoke(origin);
        var obstacles = board != null ? board.ObstacleStateService : null;
        obstacles?.NotifyAncientSealBroken(origin);
        obstacles?.RevealStampedBeneathByOverOrigin(origin);
        // The original clear may already have settled while the stones were flying.
        board?.RequestResolveAfterActionSequence();
    }

    private bool EnsureFlightRoot()
    {
        if (flightRoot != null) return true;
        var canvas = board.TilesRoot != null ? board.TilesRoot.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return false;
        var go = new GameObject("AncientSealFlights", typeof(RectTransform));
        go.layer = canvas.rootCanvas.gameObject.layer;
        flightRoot = (RectTransform)go.transform;
        flightRoot.SetParent(canvas.rootCanvas.transform, false);
        flightRoot.anchorMin = Vector2.zero;
        flightRoot.anchorMax = Vector2.one;
        flightRoot.pivot = new Vector2(0.5f, 0.5f);
        flightRoot.offsetMin = flightRoot.offsetMax = Vector2.zero;
        return true;
    }

    private void ReleaseFlight(CollectionFlight flight)
    {
        if (!flights.Remove(flight)) return;
        if (!flight.arrived)
            flight.state.inFlight[flight.color] = Mathf.Max(0, flight.state.inFlight[flight.color] - 1);
        flight.fx?.Dispose();
        flight.job?.Dispose();
    }

    private void CancelFlights()
    {
        StopAllCoroutines();
        // Unity teardown need not dispose stopped coroutine enumerators; release explicitly too.
        foreach (var flight in new List<CollectionFlight>(flights)) ReleaseFlight(flight);
        if (flightRoot != null) Destroy(flightRoot.gameObject);
        flightRoot = null;
    }
}
