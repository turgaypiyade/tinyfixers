using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Each four-hit shutter produces its authored quota and retires independently.
/// Diamonds are ordinary matchable tiles with a separate collectible arrival effect.
public sealed class DimerdalService : MonoBehaviour
{
    private sealed class Shutter
    {
        public ObstacleId id;
        public int remaining = DimerdalRules.OpeningHits;
        public int limit, committed, pendingReservations, inFlight;
        public DimerdalView view;
        public ObstacleDef definition;
        public Image host;
        public Sprite diamondSprite;
    }

    public sealed class MatchProduction
    {
        internal int epoch;
        internal readonly List<int> origins = new(4);
    }

    private BoardController board;
    private ObstacleStateService state;
    private readonly Dictionary<int, Shutter> shutters = new();
    private readonly List<DimerdalView> animated = new();
    private readonly List<int> retiringOrigins = new();
    private readonly Dictionary<int, TileView> matchCells = new();
    private readonly Dictionary<int, TileType> graphColors = new();
    private readonly HashSet<int> visited = new();
    private readonly List<int> queue = new();
    private readonly HashSet<int> nearbyOrigins = new();
    private readonly List<int> orderedOrigins = new();
    private readonly List<int> orderedCells = new();
    private readonly Stack<MatchProduction> batches = new();
    private readonly DimerdalProductionFx production = new();
    private readonly DimerdalCollectionFx collection = new();
    private int epoch;
    private bool retirementPending;

    private static bool IsExhausted(Shutter s) => s.committed >= s.limit
        && s.pendingReservations == 0 && s.inFlight == 0;
    private static bool HasCapacity(Shutter s) => s.committed < s.limit;

    public static DimerdalService Ensure(BoardController board)
    {
        if (!board.TryGetComponent<DimerdalService>(out var service))
            service = board.gameObject.AddComponent<DimerdalService>();
        service.production.OnFinished ??= service.OnProductionFinished;
        service.board = board;
        board.Dimerdals = service;
        return service;
    }

    public void Build(LevelData level)
    {
        Unbind();
        production.CancelAll();
        collection.CancelAll(false);
        epoch++;
        foreach (var shutter in shutters.Values) if (shutter.view != null) shutter.view.StopAnimations();
        shutters.Clear(); animated.Clear();
        retirementPending = false;
        Bind();
    }

    private void Bind()
    {
        if (board == null || board.ObstacleStateService == null) return;
        state = board.ObstacleStateService;
        state.DimerdalHitInterceptor = HandleHit;
        state.DimerdalRemainingHitsQuery = Remaining;
        state.DimerdalCanProduceQuery = CanProduce;
        state.DimerdalCanProduceAtQuery = CanProduceAt;
        state.OnObstacleDestroyed -= HandleObstacleDestroyed;
        state.OnObstacleDestroyed += HandleObstacleDestroyed;
    }

    private void Unbind()
    {
        if (state == null) return;
        if (state.DimerdalHitInterceptor == HandleHit) state.DimerdalHitInterceptor = null;
        if (state.DimerdalRemainingHitsQuery == Remaining) state.DimerdalRemainingHitsQuery = null;
        if (state.DimerdalCanProduceQuery == CanProduce) state.DimerdalCanProduceQuery = null;
        if (state.DimerdalCanProduceAtQuery == CanProduceAt) state.DimerdalCanProduceAtQuery = null;
        state.OnObstacleDestroyed -= HandleObstacleDestroyed;
        state = null;
    }

    private void OnEnable() { if (board != null) { Bind(); retirementPending = true; } }
    private void OnDisable()
    {
        Unbind();
        animated.Clear();
        production.CancelAll();
        foreach (var shutter in shutters.Values)
        {
            shutter.committed = Mathf.Max(0, shutter.committed - shutter.pendingReservations);
            shutter.pendingReservations = 0;
        }
        collection.CancelAll(true);
        epoch++;
        retirementPending = true;
        foreach (var shutter in shutters.Values) if (shutter.view != null) shutter.view.StopAnimations();
        animated.Clear();
    }
    private void OnDestroy() { Unbind(); if (board != null && board.Dimerdals == this) board.Dimerdals = null; }

    public void Register(int origin, ObstacleDef def, Image host)
    {
        if (def.auxiliarySprites == null || def.auxiliarySprites.Count < 3) return;
        if (!shutters.TryGetValue(origin, out var shutter))
            shutters.Add(origin, shutter = new Shutter { id = def.id,
                limit = board.ActiveLevelData.GetDimerdalProductionCount(origin, def.id) });
        shutter.view = DimerdalView.Ensure(host, def, shutter.remaining);
        shutter.definition = def;
        shutter.host = host;
        shutter.diamondSprite = def.auxiliarySprites[1];
        if (IsExhausted(shutter)) retirementPending = true;
    }

    private int Remaining(int origin) => shutters.TryGetValue(origin, out var s)
        ? HasCapacity(s) ? s.remaining : 0 : DimerdalRules.OpeningHits;
    private bool CanProduceAt(int origin) => isActiveAndEnabled && shutters.TryGetValue(origin, out var s) && HasCapacity(s);
    private bool CanProduce()
    {
        if (!isActiveAndEnabled) return false;
        foreach (var s in shutters.Values) if (HasCapacity(s)) return true;
        return false;
    }

    private bool HandleHit(int origin, ObstacleHitContext context, TileType? source)
    {
        if (!shutters.TryGetValue(origin, out var shutter)) return false;
        if (!HasCapacity(shutter)) return false;
        if (context == ObstacleHitContext.Scripted) return false;
        if (shutter.remaining == 0)
        {
            // Normal groups are counted once in PrepareMatches. Special/tool hits
            // use the existing obstacle strike deduplication and ignore stone color.
            if (context == ObstacleHitContext.NormalMatch) return false;
            if (!Launch(origin)) return false;
            return true;
        }
        if (context == ObstacleHitContext.NormalMatch && source != DimerdalRules.MatchColor(shutter.id)) return false;
        shutter.remaining--;
        if (shutter.view != null)
        {
            shutter.view.SetRemaining(shutter.remaining, true);
            shutter.view.PlayHit();
            Track(shutter.view);
        }
        // Each accepted hit raises one slat. Production hits use a separate sound.
        board.Audio?.PlayOneShotClip(shutter.definition.coverRemovedSound, shutter.definition.coverRemovedSoundVolume);
        return true;
    }

    private void Update()
    {
        production.Tick(Time.deltaTime);
        RetireCompletedShutters();
        collection.Tick();
        for (int i = animated.Count - 1; i >= 0; i--)
            if (animated[i] == null || !animated[i].Tick(Time.deltaTime)) animated.RemoveAt(i);
    }

    internal void CollectDiamond(TileView tile, ObstacleId id)
    {
        if (!DimerdalRules.IsDiamond(id)) return;
        if (!collection.Launch(this, board, tile, id))
            board?.TopHud?.NotifyCollectibleCollected(CollectibleId.Diamond, 1);
    }

    // Called BEFORE a clear marks its tiles as Clearing. Opening hits from this very clear
    // cannot also produce a diamond: only shutters already open at this snapshot qualify.
    public MatchProduction PrepareMatches(HashSet<TileView> matches, ClearPresentationPlan plan)
    {
        if (!isActiveAndEnabled || state == null || shutters.Count == 0 || matches.Count < 3) return null;
        matchCells.Clear(); graphColors.Clear(); visited.Clear(); orderedCells.Clear();
        foreach (var tile in matches)
            if (tile != null && tile.GetSpecial() == TileSpecial.None && DimerdalRules.IsColor(tile.GetTileType()))
            {
                int cell = tile.Y * board.Width + tile.X;
                matchCells[cell] = tile;
                graphColors[cell] = tile.GetTileType();
                orderedCells.Add(cell);
            }
        // A newly earned special is absent from the clear list. Keep its original
        // position as a graph bridge, but never turn the special itself into a diamond.
        if (plan != null)
            foreach (var effect in plan.Effects)
                if (effect is SpecialCreationFormationEffectDescriptor creation && creation.CreatedTile != null
                    && creation.TargetTiles != null && plan.TryGetCurrentTileLifetime(creation.CreatedTile, out _))
                    foreach (var contributor in creation.TargetTiles)
                        if (contributor != null && matchCells.ContainsKey(contributor.Y * board.Width + contributor.X))
                        {
                            graphColors[creation.CreatedTile.Y * board.Width + creation.CreatedTile.X] = contributor.GetTileType();
                            break;
                        }
        orderedCells.Sort();
        MatchProduction batch = null;
        foreach (int start in orderedCells)
        {
            if (!visited.Add(start)) continue;
            var color = matchCells[start].GetTileType();
            queue.Clear(); queue.Add(start); nearbyOrigins.Clear();
            for (int i = 0; i < queue.Count; i++)
            {
                int cell = queue[i];
                for (int d = 0; d < 4; d++)
                {
                    int x = cell % board.Width + (d == 0 ? -1 : d == 1 ? 1 : 0);
                    int y = cell / board.Width + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (x < 0 || y < 0 || x >= board.Width || y >= board.Height) continue;
                    int neighbor = y * board.Width + x;
                    if (graphColors.TryGetValue(neighbor, out var nextColor) && nextColor == color && visited.Add(neighbor)) queue.Add(neighbor);
                    int origin = state.GetObstacleOriginAt(x, y);
                    if (shutters.TryGetValue(origin, out var s) && s.remaining == 0 && HasCapacity(s) && DimerdalRules.MatchColor(s.id) == color && !nearbyOrigins.Contains(origin)
                        && state.GetObstacleIdAt(x, y) == s.id && !state.IsHitLockedAt(x, y)) nearbyOrigins.Add(origin);
                }
            }
            if (queue.Count < 3) continue;
            queue.Sort(); orderedOrigins.Clear(); orderedOrigins.AddRange(nearbyOrigins); orderedOrigins.Sort();
            foreach (int origin in orderedOrigins)
            {
                if (!shutters.TryGetValue(origin, out var shutter) || !HasCapacity(shutter)) continue;
                if (batch == null)
                {
                    batch = batches.Count > 0 ? batches.Pop() : new MatchProduction();
                    batch.epoch = epoch;
                }
                batch.origins.Add(origin);
                shutter.committed++;
                shutter.pendingReservations++;
                // A group touching several open boxes still creates exactly ONE diamond.
                break;
            }
        }
        matchCells.Clear();
        graphColors.Clear();
        return batch;
    }

    public void FinishMatches(MatchProduction batch, bool cleared)
    {
        if (batch == null) return;
        if (batch.epoch == epoch)
        {
            foreach (int origin in batch.origins)
                if (shutters.TryGetValue(origin, out var shutter))
                {
                    shutter.pendingReservations--;
                    if (!cleared || !Launch(origin, true)) shutter.committed = Mathf.Max(0, shutter.committed - 1);
                }
        }
        batch.origins.Clear(); batches.Push(batch);
    }

    private bool Launch(int origin, bool reserved = false)
    {
        if (!isActiveAndEnabled || board == null || !shutters.TryGetValue(origin, out var shutter)
            || (!reserved && !HasCapacity(shutter))
            || !production.Launch(board, shutter.host, shutter.diamondSprite, DimerdalRules.DiamondFor(shutter.id), origin)) return false;
        if (!reserved) shutter.committed++;
        shutter.inFlight++;
        // Start once with the gem's rise; retries/retargeting must not replay it.
        board.Audio?.PlayOneShotClip(shutter.definition.releaseSound, shutter.definition.releaseSoundVolume);
        shutter.view?.PlayHit();
        Track(shutter.view);
        return true;
    }

    private void Track(DimerdalView view)
    {
        if (view != null && view.IsAnimating && !animated.Contains(view)) animated.Add(view);
    }

    private void OnProductionFinished(int origin, bool placed)
    {
        if (!shutters.TryGetValue(origin, out var shutter)) return;
        shutter.inFlight = Mathf.Max(0, shutter.inFlight - 1);
        if (!placed) shutter.committed = Mathf.Max(0, shutter.committed - 1);
        if (IsExhausted(shutter)) retirementPending = true;
    }

    private void HandleObstacleDestroyed(int origin, ObstacleId id)
    {
        // Recheck exhausted sources when a cover opens, without a per-frame board scan.
        if (shutters.Count > 0) retirementPending = true;
    }

    private void RetireCompletedShutters()
    {
        if (!retirementPending || state == null) return;
        retirementPending = false;
        retiringOrigins.Clear();
        retiringOrigins.AddRange(shutters.Keys);
        var retiringState = state;
        int retiringEpoch = epoch;
        bool removed = false;
        foreach (int origin in retiringOrigins)
        {
            if (state != retiringState || epoch != retiringEpoch || !isActiveAndEnabled) break;
            if (!shutters.TryGetValue(origin, out var shutter) || !IsExhausted(shutter)) continue;
            // Destruction may synchronously reveal/register a new source at this
            // origin. Release the old registration before dispatching those events.
            shutters.Remove(origin);
            if (!retiringState.TryRetireDimerdal(origin, shutter.id))
            {
                shutters[origin] = shutter;
                continue;
            }
            if (shutter.view != null) animated.Remove(shutter.view);
            removed = true;
        }
        retiringOrigins.Clear();
        if (removed && state == retiringState && epoch == retiringEpoch && isActiveAndEnabled)
            board.RequestResolveAfterActionSequence();
    }
}
