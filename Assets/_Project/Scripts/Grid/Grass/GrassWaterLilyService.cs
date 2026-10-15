using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Grass-backed water lilies. Event-driven neighbour checks and bounded pooled flights.</summary>
public sealed class GrassWaterLilyService : MonoBehaviour
{
    private const string FlightPool = "WaterLilySpecial", PetalPool = "WaterLilyPetal";
    private const int PetalCapacity = 32;
    public const float FlowerSizeRatio = 1.12f;
    private const float FallingPetalScale = 1.6f;
    private const float SwallowSeconds = 0.48f;
    private const float SpitWindup = 0.18f, SpitTravel = 0.62f, SpitLanding = 0.12f;
    private sealed class Lily
    {
        public int cell, skipEatMove = -1, version, destination;
        public WaterLilyProgress progress = new WaterLilyProgress();
        public WaterLilyPetalImage image;
        public RectTransform rect;
        public Image flight;
        public TileSpecial stored;
        public TileType baseType, overrideType;
        public bool hasOverride, releasePending, outbound, completed;
        public TileView target;
        public Vector3 from, to;
        public float age = -1, pose = 1, launchAge = -1, launchScale = 0.12f;
        public int lastFrame = -1;
        public Vector2 lean, home, spitDirection;
        public IDisposable job, hold;
        public readonly Vector2Int[] heldCell = new Vector2Int[1];
        public readonly NeighbourWatch[] neighbours = new NeighbourWatch[4];
    }
    private struct NeighbourWatch
    {
        public TileView tile;
        public TileSpecial special;
        public int version, sinceMove;
    }
    private struct PetalFx
    {
        public WaterLilyPetalImage image;
        public Vector2 from, velocity;
        public float age, spin;
    }
    private readonly List<Lily> flowers = new List<Lily>();
    private readonly Dictionary<int, Lily> byCell = new Dictionary<int, Lily>();
    private readonly PetalFx[] petals = new PetalFx[PetalCapacity];
    private int petalCount, move;
    private float tileSize;
    private BoardController board;
    private ObstacleStateService state;
    private RectTransform fxRoot;
    private IList<Sprite> frames;
    private bool needsIdlePass, bound;

    public void Init(BoardController owner, List<int> cells, float size)
    {
        board = owner; state = board.ObstacleStateService; tileSize = size;
        fxRoot = board.BreakFxParent;
        frames = board.ActiveLevelData.obstacleLibrary.Get(ObstacleId.GrassWaterLily)?.auxiliarySprites;
        if (frames == null || frames.Count < 5)
        {
            Debug.LogError("[GrassWaterLily] ObstacleLibrary requires CF1–CF5 in auxiliarySprites.");
            return;
        }
        cells.Sort();
        foreach (int cell in cells)
        {
            var go = new GameObject("WaterLily_" + cell, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = new Vector2((cell % board.Width + 0.5f) * size, -(cell / board.Width + 0.5f) * size);
            rect.sizeDelta = Vector2.one * size * FlowerSizeRatio;
            var image = go.AddComponent<WaterLilyPetalImage>();
            image.sprite = frames[0]; image.preserveAspect = true; image.raycastTarget = false;
            var lily = new Lily { cell = cell, rect = rect, home = rect.anchoredPosition, image = image };
            flowers.Add(lily); byCell.Add(cell, lily); EnsureFlight(lily);
        }
        // Warm every visual slot at setup; hits never create textures or instantiate particle objects.
        for (int i = 0; i < PetalCapacity; i++) petals[i].image = RentPetal();
        for (int i = 0; i < PetalCapacity; i++)
        {
            UiVfxPool.Return(PetalPool, petals[i].image.gameObject); petals[i] = default;
        }
        Subscribe(); needsIdlePass = true;
    }
    private void OnEnable()
    {
        if (board == null || frames == null) return;
        foreach (var lily in flowers)
        {
            EnsureFlight(lily);
            if (!lily.progress.IsDead) lily.image.sprite = frames[lily.stored == TileSpecial.None ? 0 : 4];
        }
        Subscribe(); needsIdlePass = true;
    }
    private void Subscribe()
    {
        if (bound) return;
        board.OnBecameIdle += BoardIdle;
        board.OnPlayerMoveResolved += MoveResolved;
        state.OnWaterLilyCleared += Cleared;
        state.WaterLilyHitInterceptor = Hit;
        state.WaterLilyRemainingHitsQuery = RemainingHits;
        bound = true;
    }
    private void MoveResolved() { move++; needsIdlePass = true; }
    private void BoardIdle() => needsIdlePass = true;
    private void ProcessIdle()
    {
        needsIdlePass = false;
        transform.SetAsLastSibling();
        foreach (var lily in flowers)
        {
            if (lily.image == null) continue;
            if (lily.releasePending)
            {
                if (lily.age < 0) BeginRelease(lily);
                else if (lily.outbound && lily.target == null) TryRelease(lily);
            }
            if (lily.completed) continue;
            bool visible = state.IsGrassVisibleAt(lily.cell);
            bool shown = visible && lily.progress.RemainingPetals > 0;
            if (lily.image.gameObject.activeSelf != shown) lily.image.gameObject.SetActive(shown);
            if (!visible || lily.age >= 0 || lily.releasePending) continue;
            if (!lily.progress.CanSwallow || lily.stored != TileSpecial.None) continue;
            int x = lily.cell % board.Width, y = lily.cell / board.Width;
            TileView target = null;
            for (int side = 0; side < 4; side++)
            {
                int nx = x + (side == 1 ? 1 : side == 3 ? -1 : 0);
                int ny = y + (side == 0 ? -1 : side == 2 ? 1 : 0);
                var observed = board.GetTileViewAt(nx, ny);
                if (observed != null && !observed.gameObject.activeInHierarchy) observed = null;
                // Observe identity even during its creation reveal, but eat only an eligible idle tile.
                bool waited = WaitedOneMove(ref lily.neighbours[side], observed, move);
                if (target == null && waited && Candidate(nx, ny) != null) target = observed;
            }
            if (target != null && move > lily.skipEatMove) Swallow(lily, target);
        }
    }
    private static bool WaitedOneMove(ref NeighbourWatch watch, TileView candidate, int currentMove)
    {
        if (candidate == null || candidate.GetSpecial() == TileSpecial.None)
        {
            watch = default;
            return false;
        }
        var special = candidate.GetSpecial();
        if (!ReferenceEquals(watch.tile, candidate) || watch.version != candidate.LifetimeVersion || watch.special != special)
        {
            watch = new NeighbourWatch { tile = candidate, version = candidate.LifetimeVersion,
                special = special, sinceMove = currentMove };
            return false;
        }
        return currentMove > watch.sinceMove;
    }
    private TileView Candidate(int x, int y)
    {
        var t = board.GetTileViewAt(x, y);
        if (t == null || !t.gameObject.activeInHierarchy || !t.IsRuntimeIdle || t.IsSpecialCreationRevealPlaying
            || t.WasDragging || t.GetSpecial() == TileSpecial.None || t.IsSpecialLocked
            || board.IsPendingTriggeredSpecialCell(x, y) || board.IsReservedTileTargetCell(x, y)
            || state.IsInteractionLockedAt(x, y) || state.IsMovableObstacleAt(x, y)) return null;
        return t;
    }
    private void Swallow(Lily lily, TileView target)
    {
        Array.Clear(lily.neighbours, 0, lily.neighbours.Length);
        lily.stored = target.GetSpecial(); lily.baseType = target.GetTileType();
        lily.hasOverride = target.GetOverrideBaseType(out lily.overrideType);
        Vector3 from = board.GetTileWorldCenter(target), to = lily.rect.position;
        Vector3 offset = transform.InverseTransformVector(from - to);
        lily.lean = new Vector2(offset.x, offset.y).normalized;
        // Only the special is removed: the normal tile stays, with no clear/goal/gear credit.
        target.SetSpecial(TileSpecial.None, deferVisualUpdate: true);
        if (lily.baseType != TileType.Gear && lily.baseType != TileType.Core
            && lily.baseType != TileType.Bolt && lily.baseType != TileType.Plate)
            target.SetType(TileType.Gear);
        target.RefreshIcon();
        board.SyncTileData(target.X, target.Y);
        BeginFlight(lily, from, to, false);
    }
    private bool? Hit(int cell, ObstacleHitContext context)
    {
        if (!byCell.TryGetValue(cell, out var lily) || lily.progress.RemainingPetals == 0) return null;
        if (context != ObstacleHitContext.SpecialActivation) return false;
        bool stored = lily.stored != TileSpecial.None;
        int previous = lily.progress.RemainingPetals;
        bool releaseOnly = lily.progress.Hit(stored);
        if (stored) { lily.releasePending = true; needsIdlePass = true; }
        if (!releaseOnly)
        {
            EmitPetal(lily, 4 - previous);
            lily.image.color = Color.white;
            ResetPose(lily);
            Array.Clear(lily.neighbours, 0, lily.neighbours.Length);
            // A stored special keeps the mouth closed until the reverse opening animation.
            if (!stored) lily.image.sprite = frames[0];
            lily.image.SetPetals((15 << (4 - lily.progress.RemainingPetals)) & 15);
            if (lily.progress.RemainingPetals == 0) lily.image.gameObject.SetActive(false);
        }
        // This callback already runs at the incoming special's cell impact. Only
        // swallowing waits for board idle; opening/ejection must start on this hit.
        if (stored && (lily.age < 0 || !lily.outbound)) BeginRelease(lily);
        return true;
    }
    private int RemainingHits(int cell)
    {
        if (!byCell.TryGetValue(cell, out var lily)) return 0;
        return lily.progress.RemainingPetals + (lily.stored != TileSpecial.None && !lily.progress.HasReleased ? 1 : 0);
    }
    private void BeginRelease(Lily lily)
    {
        lily.skipEatMove = move;
        BeginFlight(lily, lily.rect.position, lily.rect.position, true);
        TryRelease(lily);
    }
    private bool TryRelease(Lily lily)
    {
        if (lily.stored == TileSpecial.None) { lily.releasePending = false; return false; }
        TileView chosen = null; int seen = 0;
        // One bounded scan per release/idle attempt, never a board scan every frame.
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                if (!board.IsTileFreeForDynamicObstacle(x, y) || state.HasObstacleAt(x, y) || state.IsInteractionLockedAt(x, y)) continue;
                // Prefer landing outside all live mouths so a just-returned special can be played.
                bool adjacent = false;
                foreach (var other in flowers)
                    if (other.progress.CanSwallow && Mathf.Abs(other.cell % board.Width - x) + Mathf.Abs(other.cell / board.Width - y) == 1)
                    { adjacent = true; break; }
                if (adjacent) continue;
                if (UnityEngine.Random.Range(0, ++seen) == 0) chosen = board.GetTileViewAt(x, y);
            }
        if (chosen == null)
            for (int y = 0; y < board.Height && chosen == null; y++)
                for (int x = 0; x < board.Width; x++)
                    if (board.IsTileFreeForDynamicObstacle(x, y) && !state.HasObstacleAt(x, y) && !state.IsInteractionLockedAt(x, y))
                    { chosen = board.GetTileViewAt(x, y); break; }
        if (chosen == null) return false; // Keep it stored; retry only when the board changes/settles.
        lily.target = chosen; lily.version = chosen.LifetimeVersion;
        lily.destination = chosen.Y * board.Width + chosen.X;
        lily.heldCell[0] = new Vector2Int(chosen.X, chosen.Y);
        board.ReserveTileTargetCell(lily.heldCell[0]);
        lily.hold = board.HoldCells(lily.heldCell);
        // The mouth has been opening since impact, even if all landing cells were
        // busy. Continue from the hovering special without replaying the wind-up.
        lily.launchAge = Mathf.Max(SpitWindup, lily.age);
        lily.launchScale = lily.flight.rectTransform.localScale.x;
        if (lily.age > SpitWindup) lily.from = lily.flight.rectTransform.localPosition;
        Vector3 destination = board.GetTileWorldCenter(chosen);
        lily.to = fxRoot.InverseTransformPoint(destination);
        Vector3 direction = transform.InverseTransformVector(destination - lily.rect.position);
        lily.spitDirection = new Vector2(direction.x, direction.y).normalized;
        return true;
    }
    private void EnsureFlight(Lily lily)
    {
        if (lily.flight != null) return;
        lily.flight = UiVfxPool.RentImage(FlightPool, fxRoot, "WaterLilySpecialFlight");
        lily.flight.gameObject.layer = fxRoot.gameObject.layer;
        lily.flight.preserveAspect = true;
        lily.flight.gameObject.SetActive(false);
    }
    private Sprite SpecialSprite(Lily lily) => lily.stored == TileSpecial.SystemOverride && lily.hasOverride
        ? board.GetOverrideIcon(lily.overrideType)
        : lily.stored == TileSpecial.PatchBot ? board.GetPatchBotFullIcon() : board.GetSpecialIcon(lily.stored);
    private void BeginFlight(Lily lily, Vector3 from, Vector3 to, bool outbound)
    {
        EnsureFlight(lily);
        lily.outbound = outbound; lily.age = 0; lily.pose = 0; lily.lastFrame = -1;
        lily.launchAge = -1;
        lily.from = fxRoot.InverseTransformPoint(from); lily.to = fxRoot.InverseTransformPoint(to);
        Vector3 direction = transform.InverseTransformVector(to - from);
        lily.spitDirection = new Vector2(direction.x, direction.y).normalized;
        if (outbound && lily.spitDirection.sqrMagnitude < 0.001f) lily.spitDirection = Vector2.up;
        lily.rect.anchoredPosition = lily.home;
        lily.flight.sprite = SpecialSprite(lily); lily.flight.color = Color.white;
        lily.flight.rectTransform.sizeDelta = Vector2.one * tileSize * 0.72f;
        lily.flight.rectTransform.localPosition = lily.from;
        lily.flight.rectTransform.localScale = Vector3.one * (outbound ? 0.12f : 1f);
        lily.flight.rectTransform.localRotation = Quaternion.identity;
        // The special appears as the petals open, after the wind-up, rather than popping on hit.
        lily.flight.gameObject.SetActive(!outbound);
        if (outbound && !lily.completed) lily.image.sprite = frames[4];
        var previousJob = lily.job;
        lily.job = board.BeginJob(BoardController.BoardJobKind.KeyFlight);
        previousJob?.Dispose(); // An impact can interrupt the inbound swallowing flight.
    }
    private void CompleteFlight(Lily lily)
    {
        lily.age = -1; lily.flight.gameObject.SetActive(false);
        ResetPose(lily);
        if (lily.outbound)
        {
            var target = lily.target;
            if (target != null && target.IsCurrentLifetime(lily.version)
                && board.GetTileViewAt(target.X, target.Y) == target && target.GetSpecial() == TileSpecial.None
                && !state.HasObstacleAt(target.X, target.Y) && !state.IsInteractionLockedAt(target.X, target.Y))
            {
                target.SetType(lily.baseType);
                target.SetSpecial(lily.stored);
                if (lily.hasOverride) target.SetOverrideBaseType(lily.overrideType);
                board.SyncTileData(target.X, target.Y);
                lily.stored = TileSpecial.None; lily.releasePending = false;
            }
            else needsIdlePass = true; // The landing cell changed: preserve the special and retry.
        }
        ReleaseHolds(lily);
        // Keep level-end waiting if the reserved target was replaced during flight.
        // BeginFlight transfers this job on retry; OnDisable releases it on teardown.
        if (!lily.outbound || lily.stored == TileSpecial.None)
        {
            var job = lily.job; lily.job = null; job?.Dispose();
        }
        board.RequestResolveAfterActionSequence();
        needsIdlePass = true;
    }
    private void ReleaseHolds(Lily lily)
    {
        if (lily.hold == null) return;
        board.ClearReservedTileTargetCell(lily.heldCell[0]);
        var hold = lily.hold; lily.hold = null; hold.Dispose(); lily.target = null;
    }
    private WaterLilyPetalImage RentPetal()
    {
        var go = UiVfxPool.RentRect(PetalPool, fxRoot, "WaterLilyFallingPetal");
        go.layer = fxRoot.gameObject.layer;
        var image = go.GetComponent<WaterLilyPetalImage>();
        if (image == null) image = go.AddComponent<WaterLilyPetalImage>();
        image.raycastTarget = false; image.preserveAspect = true; return image;
    }
    private void EmitPetal(Lily lily, int petal)
    {
        if (petalCount == PetalCapacity) return;
        var image = RentPetal();
        image.sprite = frames[0]; image.color = Color.white; image.SetPetals(1 << petal, false);
        image.rectTransform.sizeDelta = Vector2.one * tileSize * FlowerSizeRatio * FallingPetalScale;
        Vector2 from = fxRoot.InverseTransformPoint(lily.rect.position);
        image.rectTransform.anchoredPosition = from;
        petals[petalCount++] = new PetalFx { image = image, from = from,
            velocity = new Vector2(UnityEngine.Random.Range(-90f, 90f), 100f), spin = UnityEngine.Random.Range(-220f, 220f) };
    }
    private void Update()
    {
        if (!bound) return;
        if (needsIdlePass && !board.Flow.IsSettling && !board.Flow.IsSpecialVisualInFlight) ProcessIdle();
        float dt = Time.deltaTime;
        foreach (var lily in flowers)
        {
            if (lily.age >= 0)
            {
                lily.age += dt;
                if (lily.outbound)
                {
                    AnimateSpit(lily);
                    if (lily.launchAge >= 0 && lily.age >= lily.launchAge + SpitTravel + SpitLanding) CompleteFlight(lily);
                }
                else
                {
                    float t = Mathf.Clamp01(lily.age / SwallowSeconds), k = Smooth(t);
                    var rt = lily.flight.rectTransform;
                    rt.localPosition = Vector3.Lerp(lily.from, lily.to, k) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * tileSize * 0.55f);
                    rt.localScale = Vector3.one * Mathf.Lerp(1, 0.12f, k);
                    lily.pose = t;
                    if (!lily.progress.IsDead && !lily.completed) SetFrame(lily, Mathf.Min(4, (int)(t * 5)));
                    float bend = Mathf.Sin(t * Mathf.PI);
                    lily.rect.localRotation = Quaternion.Euler(-lily.lean.y * 18f * bend, lily.lean.x * 18f * bend, -lily.lean.x * 9f * bend);
                    if (t >= 1) CompleteFlight(lily);
                }
            }
        }
        for (int i = petalCount - 1; i >= 0; i--)
        {
            var fx = petals[i]; fx.age += dt;
            float t = fx.age;
            fx.image.rectTransform.anchoredPosition = fx.from + fx.velocity * t + Vector2.down * (260f * t * t);
            fx.image.rectTransform.localRotation = Quaternion.Euler(0, 0, fx.spin * t);
            if (t < 0.6f) { petals[i] = fx; continue; }
            UiVfxPool.Return(PetalPool, fx.image.gameObject);
            petals[i] = petals[--petalCount]; petals[petalCount] = default;
        }
    }
    private static float Smooth(float t) => t * t * (3 - 2 * t);
    private void SetFrame(Lily lily, int frame)
    {
        if (frame == lily.lastFrame) return;
        lily.lastFrame = frame; lily.image.sprite = frames[frame];
    }
    private void AnimateSpit(Lily lily)
    {
        float windup = Smooth(Mathf.Clamp01(lily.age / SpitWindup));
        if (lily.age < SpitWindup)
        {
            lily.rect.anchoredPosition = lily.home - lily.spitDirection * (tileSize * 0.10f * windup);
            lily.rect.localScale = lily.progress.IsDead ? Vector3.one : new Vector3(1 + 0.12f * windup, 1 - 0.15f * windup, 1);
            lily.rect.localRotation = Quaternion.Euler(0, 0, lily.spitDirection.x * 10f * windup);
            return;
        }
        float opening = Smooth(Mathf.Clamp01((lily.age - SpitWindup) / 0.36f));
        if (!lily.completed) SetFrame(lily, 4 - Mathf.Min(4, (int)(opening * 5)));
        float recoil = Mathf.Sin(Mathf.Clamp01((lily.age - SpitWindup) / 0.30f) * Mathf.PI);
        lily.rect.anchoredPosition = lily.home - lily.spitDirection * (tileSize * 0.10f * (1 - opening))
            + lily.spitDirection * (tileSize * 0.08f * recoil);
        lily.rect.localScale = lily.progress.IsDead ? Vector3.one : new Vector3(1 + 0.12f * (1 - opening) - 0.06f * recoil,
            1 - 0.15f * (1 - opening) + 0.10f * recoil, 1);
        lily.rect.localRotation = Quaternion.Euler(0, 0, lily.spitDirection.x * (10f * (1 - opening) - 6f * recoil));

        var rt = lily.flight.rectTransform;
        if (!lily.flight.gameObject.activeSelf) lily.flight.gameObject.SetActive(true);
        if (lily.launchAge < 0)
        {
            // No safe target yet: show the ejection now and hold the special above
            // its mouth. Target selection retries on idle/change, never per frame.
            float rise = Smooth(Mathf.Clamp01((lily.age - SpitWindup) / 0.25f));
            rt.localPosition = lily.from + Vector3.up * (tileSize * 0.7f * rise);
            rt.localScale = Vector3.one * Mathf.Lerp(0.12f, 1, rise);
            return;
        }
        float flight = Mathf.Clamp01((lily.age - lily.launchAge) / SpitTravel);
        float k = Smooth(flight);
        float height = Mathf.Clamp(Vector3.Distance(lily.from, lily.to) * 0.24f, tileSize * 0.7f, tileSize * 1.8f);
        rt.localPosition = Vector3.Lerp(lily.from, lily.to, k) + Vector3.up * (Mathf.Sin(flight * Mathf.PI) * height);
        float settle = Mathf.Clamp01((lily.age - lily.launchAge - SpitTravel) / SpitLanding);
        float scale = flight < 1 ? Mathf.Lerp(lily.launchScale, 1.12f, Smooth(Mathf.Clamp01(flight / 0.22f)))
            : Mathf.Lerp(1.12f, 1, Smooth(settle));
        rt.localScale = Vector3.one * scale;
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(flight * Mathf.PI) * 24f * (lily.spitDirection.x < 0 ? -1 : 1));
    }
    private static void ResetPose(Lily lily)
    {
        lily.rect.anchoredPosition = lily.home;
        lily.rect.localScale = Vector3.one;
        lily.rect.localRotation = Quaternion.identity;
    }
    private void Cleared(int cell)
    {
        if (!byCell.TryGetValue(cell, out var lily)) return;
        lily.completed = true;
        if (lily.image != null) lily.image.gameObject.SetActive(false);
        byCell.Remove(cell);
    }
    private void OnDisable()
    {
        if (!bound) return;
        bound = false;
        board.OnBecameIdle -= BoardIdle; board.OnPlayerMoveResolved -= MoveResolved;
        state.OnWaterLilyCleared -= Cleared;
        if (state.WaterLilyHitInterceptor == Hit) state.WaterLilyHitInterceptor = null;
        if (state.WaterLilyRemainingHitsQuery == RemainingHits) state.WaterLilyRemainingHitsQuery = null;
        foreach (var lily in flowers)
        {
            Array.Clear(lily.neighbours, 0, lily.neighbours.Length);
            ReleaseHolds(lily);
            var job = lily.job; lily.job = null; job?.Dispose(); lily.age = -1;
            ResetPose(lily);
            if (lily.flight != null) UiVfxPool.Return(FlightPool, lily.flight.gameObject, 128);
            lily.flight = null;
        }
        for (int i = 0; i < petalCount; i++) UiVfxPool.Return(PetalPool, petals[i].image.gameObject);
        Array.Clear(petals, 0, petals.Length); petalCount = 0;
    }
}
