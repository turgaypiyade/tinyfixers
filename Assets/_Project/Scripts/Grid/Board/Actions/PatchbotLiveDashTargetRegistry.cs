using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges queued PatchBot dash visuals to live target resolution.
///
/// BoardController.PatchbotDashRequest is consumed later by PatchbotDashUI. This registry lets
/// gameplay code register a resolver when the dash is queued; PatchbotDashUI acquires it at
/// dive start and keeps calling it THROUGHOUT the dive — every call re-validates the target
/// against logical board state and re-picks via the coordinator if the target died mid-flight.
/// </summary>
public static class PatchbotLiveDashTargetRegistry
{
    private readonly struct DashKey : IEquatable<DashKey>
    {
        private readonly Vector2Int from;
        private readonly Vector2Int initialTo;

        public DashKey(Vector2Int from, Vector2Int initialTo)
        {
            this.from = from;
            this.initialTo = initialTo;
        }

        public bool Equals(DashKey other) => from.Equals(other.from) && initialTo.Equals(other.initialTo);
        public override bool Equals(object obj) => obj is DashKey other && Equals(other);
        public override int GetHashCode() => (from.GetHashCode() * 397) ^ initialTo.GetHashCode();
    }

    private sealed class Entry
    {
        public Func<Vector2Int?> Resolver;
    }

    private static readonly Dictionary<DashKey, Queue<Entry>> Entries = new();

    public static void Register(Vector2Int from, Vector2Int initialTo, Func<Vector2Int?> resolver)
    {
        if (resolver == null)
            return;

        var key = new DashKey(from, initialTo);
        if (!Entries.TryGetValue(key, out var queue))
        {
            queue = new Queue<Entry>();
            Entries.Add(key, queue);
        }

        queue.Enqueue(new Entry { Resolver = resolver });
    }

    /// <summary>
    /// Hands this dash's live resolver to the flight visual. The resolver is meant to
    /// be invoked repeatedly during the dive; null result = no valid target right now
    /// (keep flying to the last known cell).
    /// </summary>
    public static bool TryAcquireLiveResolver(Vector2Int from, Vector2Int initialTo, out Func<Vector2Int?> resolver)
    {
        resolver = null;

        var key = new DashKey(from, initialTo);
        if (!Entries.TryGetValue(key, out var queue) || queue.Count == 0)
            return false;

        var entry = queue.Dequeue();
        if (queue.Count == 0)
            Entries.Remove(key);

        if (entry?.Resolver == null)
            return false;

        resolver = entry.Resolver;
        return true;
    }
}
