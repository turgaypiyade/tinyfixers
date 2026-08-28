using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Manages all magnet pair obstacles in the current level.
/// Two magnets share an energy path. Hitting a magnet endpoint moves it one step
/// toward the other. When they meet, both vanish along with the connecting path.
public class MagnetObstacleService : MonoBehaviour
{
    [Header("Special Cage")]
    [Tooltip("Cage sprite (diagonal laser bars) that grows from the cell centre over a locked special. " +
             "Assign the SpecialLocker sprite here; falls back to a generated ring if empty.")]
    [SerializeField] private Sprite specialLockerSprite;

    [Header("Drain Pulse")]
    [SerializeField] private bool enableDrainPulse = true;
    [SerializeField, Min(0.5f)] private float drainInterval = 3.5f;
    [SerializeField, Min(0f)] private float firstDrainDelay = 1.2f;
    [SerializeField, Range(1, 4)] private int drainMaxTargets = 1;
    [SerializeField, Range(1, 4)] private int drainRadius = 2; // 2 => 5x5
    [SerializeField, Min(0.05f)] private float drainFlyDuration = 0.32f;
    [SerializeField, Min(0.05f)] private float drainElectricMarkDuration = 0.58f;
    [SerializeField, Min(0.05f)] private float drainSuctionDuration = 0.46f;
    [SerializeField, Range(0.05f, 0.6f)] private float drainPullEndScale = 0.22f;
    [SerializeField, Min(0.05f)] private float drainTubeTravelDuration = 0.68f;
    [SerializeField, Range(0.12f, 0.65f)] private float drainTubeCarrierScale = 0.56f;
    [SerializeField, Range(0.08f, 0.5f)] private float drainTubeTileScale = 0.3f;
    [SerializeField, Range(4, 32)] private int drainTubeSamples = 18;
    [SerializeField, Range(0f, 0.45f)] private float drainSuctionCurveStrength = 0.24f;
    [SerializeField, Range(3, 9)] private int drainElectricSegments = 7;
    [SerializeField, Range(0.015f, 0.12f)] private float drainElectricThicknessRatio = 0.065f;
    [SerializeField] private bool drainIgnoresGoalCredit = true;

    private static Sprite whitePixelSprite;
    private static Sprite tubeCarrierSprite;
    private readonly Dictionary<int, MagnetInstance> magnetsByOrigin = new();
    private readonly Dictionary<int, int> cellToOrigin = new();
    private ObstacleStateService obstacleStateService;
    private BoardController board;
    private Coroutine drainRoutine;
    private float nextDrainTime;
    private readonly List<MagnetInstance> drainSnapshot = new();

    // ── Special cage state ──────────────────────────────────────────────────────
    // A special that sits idle next to a magnet gets caged (generic SpecialLockCoordinator). This
    // service is the cage owner: it snapshots candidates at move-start, cages the idle ones at
    // move-end, casts the lightning + cage visual, and on timeout pulls the caged special through
    // the tube and re-drops it near the far endpoint.
    private struct CageCandidate { public TileView Tile; public int Cell; public int Origin; }
    private readonly List<CageCandidate> cageSnapshot = new();
    private readonly HashSet<TileView> cageSnapshotSet = new();
    private readonly Dictionary<TileView, GameObject> cageVisuals = new();
    private static Sprite cageRingSprite;
    private static Sprite loadedLockerSprite;
    private static bool triedLoadLockerSprite;

    // Authored cage sprite: inspector field first, else loaded from Resources/Magnet/SpecialLocker.
    private Sprite ResolveCageSprite()
    {
        if (specialLockerSprite != null)
            return specialLockerSprite;
        if (!triedLoadLockerSprite)
        {
            triedLoadLockerSprite = true;
            loadedLockerSprite = Resources.Load<Sprite>("Magnet/SpecialLocker");
        }
        return loadedLockerSprite;
    }

    private static readonly TileType[] FallbackDrainTypes =
    {
        TileType.Gear,
        TileType.Core,
        TileType.Bolt,
        TileType.Plate
    };

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(BoardController boardController, ObstacleStateService service)
    {
        UnsubscribeMoveEvents();          // detach from any previous board before rebinding
        board = boardController;
        obstacleStateService = service;
        magnetsByOrigin.Clear();
        cellToOrigin.Clear();
        ClearAllCageVisuals();
        StopDrainRoutine();
        SubscribeMoveEvents();
        // Random normal-tile drain-pulse retired: the magnet now only acts on idle specials (cage /
        // collect / throw). The drain VISUAL helpers below are still reused by the tube-throw.
    }

    public void Init(ObstacleStateService service) => Init(null, service);

    private void OnDisable()
    {
        StopDrainRoutine();
        UnsubscribeMoveEvents();
        ClearAllCageVisuals();
    }

    private void SubscribeMoveEvents()
    {
        if (board != null)
        {
            board.OnPlayerMoveConsumed -= CaptureCageCandidates;
            board.OnPlayerMoveResolved -= EvaluateCaging;
            board.OnPlayerMoveConsumed += CaptureCageCandidates;
            board.OnPlayerMoveResolved += EvaluateCaging;
        }
        if (obstacleStateService != null)
        {
            obstacleStateService.OnCellUnlocked -= HandleCellUnlocked;
            obstacleStateService.OnCellUnlocked += HandleCellUnlocked;
        }
    }

    private void UnsubscribeMoveEvents()
    {
        if (board != null)
        {
            board.OnPlayerMoveConsumed -= CaptureCageCandidates;
            board.OnPlayerMoveResolved -= EvaluateCaging;
        }
        if (obstacleStateService != null)
            obstacleStateService.OnCellUnlocked -= HandleCellUnlocked;
    }

    public void RegisterMagnet(int originCellIndex, int[] orderedPath, MagnetView view)
    {
        if (orderedPath == null || orderedPath.Length < 2 || view == null) return;

        var instance = new MagnetInstance(orderedPath, view);
        magnetsByOrigin[originCellIndex] = instance;
        foreach (int idx in orderedPath)
            cellToOrigin[idx] = originCellIndex;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public bool HasMagnetAt(int cellIndex) => cellToOrigin.ContainsKey(cellIndex);

    /// True if cellIndex is one of the CURRENT endpoints (A or B). Middle path cells → false.
    public bool IsMagnetEndpoint(int cellIndex)
    {
        if (!cellToOrigin.TryGetValue(cellIndex, out int origin)) return false;
        if (!magnetsByOrigin.TryGetValue(origin, out var magnet)) return false;
        return cellIndex == magnet.Path[magnet.MagnetAIndex]
            || cellIndex == magnet.Path[magnet.MagnetBIndex];
    }

    // ── Drain pulse ──────────────────────────────────────────────────────────

    private IEnumerator DrainPulseRoutine()
    {
        while (true)
        {
            if (!enableDrainPulse || board == null || obstacleStateService == null || magnetsByOrigin.Count == 0)
            {
                yield return null;
                continue;
            }

            if (Time.time < nextDrainTime || board.IsBusy || board.InputLocked)
            {
                yield return null;
                continue;
            }

            drainSnapshot.Clear();
            foreach (var kv in magnetsByOrigin)
                drainSnapshot.Add(kv.Value);

            for (int i = 0; i < drainSnapshot.Count; i++)
            {
                if (board == null || board.IsBusy || board.InputLocked)
                    break;

                var magnet = drainSnapshot[i];
                if (magnet == null || magnet.View == null)
                    continue;

                int endpointCell = magnet.UseAEndpointForDrain
                    ? magnet.Path[magnet.MagnetAIndex]
                    : magnet.Path[magnet.MagnetBIndex];
                magnet.UseAEndpointForDrain = !magnet.UseAEndpointForDrain;

                var targets = FindDrainTargets(Mathf.Max(1, drainMaxTargets));
                if (targets.Count == 0)
                    continue;

                TileType targetType = targets[0].GetTileType();
                magnet.View.SetDrainColor(ColorForTileType(targetType));

                yield return DrainTargets(magnet, endpointCell, targets, targetType);
                yield return null;
            }

            nextDrainTime = Time.time + Mathf.Max(0.5f, drainInterval);
            yield return null;
        }
    }

    private List<TileView> FindDrainTargets(int maxTargets)
    {
        var result = new List<TileView>(maxTargets);
        if (board == null || board.Tiles == null)
            return result;

        bool[,] reachable = board.CascadeLogic != null
            ? board.CascadeLogic.ComputeGravityReachableMask()
            : null;

        var candidates = new List<TileView>();
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            if (!CanDrainTileAt(x, y, out var tile))
                continue;
            if (!IsGravityReachableDrainCell(x, y, reachable))
                continue;

            candidates.Add(tile);
        }

        while (candidates.Count > 0 && result.Count < maxTargets)
        {
            int i = Random.Range(0, candidates.Count);
            result.Add(candidates[i]);
            int last = candidates.Count - 1;
            candidates[i] = candidates[last];
            candidates.RemoveAt(last);
        }

        return result;
    }

    private bool CanDrainTileAt(int x, int y, out TileView tile)
    {
        tile = null;
        if (board == null || x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;
        if (board.IsMaskHoleCell(x, y))
            return false;

        tile = board.Tiles[x, y];
        if (tile == null || !tile)
            return false;
        if (tile.GetSpecial() != TileSpecial.None || tile.GetTileType() == TileType.Key)
            return false;

        if (obstacleStateService != null)
        {
            if (obstacleStateService.HasObstacleAt(x, y))
                return false;
            if (obstacleStateService.IsOilAt(x, y) || obstacleStateService.IsGrassAt(x, y))
                return false;
            if (obstacleStateService.IsInteractionLockedAt(x, y))
                return false;
        }

        return true;
    }

    private bool IsGravityReachableDrainCell(int x, int y, bool[,] reachable)
    {
        if (board == null || reachable == null)
            return true;

        return x >= 0 && x < board.Width
            && y >= 0 && y < board.Height
            && reachable[x, y];
    }

    private IEnumerator DrainTargets(MagnetInstance magnet, int endpointCell, List<TileView> targets, TileType targetType)
    {
        Vector3 endpointWorld = magnet.View != null
            ? magnet.View.GetEndpointWorldPosition(endpointCell)
            : Vector3.zero;
        Color drainColor = ColorForTileType(targetType);

        for (int i = 0; i < targets.Count; i++)
        {
            var tile = targets[i];
            if (tile == null || !tile)
                continue;

            int x = tile.X;
            int y = tile.Y;
            if (x < 0 || x >= board.Width || y < 0 || y >= board.Height || board.Tiles[x, y] != tile)
                continue;

            TileType clearedType = tile.GetTileType();
            var visual = CreateDrainVisual(tile);

            board.ClearCell(x, y);
            board.ReleaseTile(tile);
            if (!drainIgnoresGoalCredit)
                board.NotifyTilesCleared(clearedType, 1);

            board.RequestResolveAfterActionSequence();
            StartCoroutine(CompleteDrainVisual(visual, endpointWorld, magnet.View, endpointCell, drainColor));
            yield return null;
        }
    }

    private IEnumerator CompleteDrainVisual(DrainVisual visual, Vector3 endpointWorld, MagnetView magnetView, int endpointCell, Color drainColor)
    {
        try
        {
            if (visual == null || visual.Root == null)
                yield break;

            yield return PlayDrainElectricMark(visual, endpointWorld, drainColor);
            yield return PullVisualToMagnet(visual.Root, endpointWorld);
            yield return MoveVisualThroughTubeCarrier(visual.Root, magnetView, endpointCell, drainColor);
        }
        finally
        {
            if (visual != null && visual.Root != null)
                Object.Destroy(visual.Root.gameObject);
        }
    }

    private DrainVisual CreateDrainVisual(TileView tile)
    {
        if (tile == null || !tile || board == null || board.BreakFxParent == null)
            return null;

        var sourceImage = tile.IconImage;
        var go = new GameObject("MagnetDrainTileVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(board.BreakFxParent, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.position = tile.transform.position;
        rt.localScale = Vector3.one;
        rt.sizeDelta = Vector2.one * Mathf.Max(1f, board.TileSize);

        var img = go.GetComponent<Image>();
        img.sprite = tile.GetIconSprite();
        img.raycastTarget = false;
        img.preserveAspect = sourceImage == null || sourceImage.preserveAspect;
        img.color = sourceImage != null ? sourceImage.color : Color.white;
        if (img.sprite == null)
            img.sprite = WhiteSprite();

        return new DrainVisual(rt, img);
    }

    private IEnumerator PlayDrainElectricMark(DrainVisual visual, Vector3 magnetWorld, Color color)
    {
        if (visual == null || visual.Root == null)
            yield break;

        RectTransform parent = board != null ? board.BreakFxParent : null;
        if (parent == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, drainElectricMarkDuration));
            yield break;
        }

        Vector2 start = board.WorldToAnchoredIn(parent, magnetWorld);
        Vector2 end = board.WorldToAnchoredIn(parent, visual.Root.position);
        Vector2 delta = end - start;
        Vector2 perp = delta.sqrMagnitude > 0.001f
            ? new Vector2(-delta.y, delta.x).normalized
            : Vector2.up;

        int segmentCount = Mathf.Max(3, drainElectricSegments);
        var points = new Vector2[segmentCount + 1];
        points[0] = start;
        points[segmentCount] = end;
        float wiggle = Mathf.Max(4f, board.TileSize * 0.13f);
        for (int i = 1; i < segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float offset = Mathf.Sin(t * Mathf.PI * 3f) * wiggle;
            offset += Random.Range(-wiggle * 0.35f, wiggle * 0.35f);
            points[i] = Vector2.Lerp(start, end, t) + perp * offset;
        }

        var segments = new RectTransform[segmentCount];
        var images = new Image[segmentCount];
        float thickness = Mathf.Max(2f, board.TileSize * drainElectricThicknessRatio);
        Sprite sprite = WhiteSprite();
        for (int i = 0; i < segmentCount; i++)
        {
            var go = new GameObject("MagnetDrainBolt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, 0f);

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            PlaceBoltSegment(rt, points[i], points[i + 1], thickness);

            segments[i] = rt;
            images[i] = img;
        }

        float duration = Mathf.Max(0.05f, drainElectricMarkDuration);
        float travelDuration = duration * 0.72f;
        float impactDuration = duration - travelDuration;
        float elapsed = 0f;
        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / travelDuration);
            float head = k * segmentCount;
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null)
                    continue;

                float reveal = Mathf.Clamp01(head - i);
                float tail = 1f - Mathf.Clamp01((k - 0.78f) / 0.22f);
                float flicker = Random.value < 0.18f ? 0.45f : 1f;
                Color c = Color.Lerp(Color.white, color, 0.45f);
                c.a = reveal * tail * flicker * 0.95f;
                images[i].color = c;
            }

            yield return null;
        }

        yield return PlayDrainImpact(visual.Root, parent, end, color, impactDuration);

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] != null)
                Object.Destroy(segments[i].gameObject);
        }
    }

    private IEnumerator PlayDrainImpact(RectTransform visualRt, RectTransform parent, Vector2 pos, Color color, float duration)
    {
        if (parent == null || visualRt == null)
            yield break;

        var go = new GameObject("MagnetDrainImpact", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.sprite = TubeCarrierSprite();
        img.raycastTarget = false;
        img.color = Color.white;

        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        float size = board != null ? board.TileSize * 0.92f : 72f;
        rt.sizeDelta = Vector2.one * size;

        float dur = Mathf.Max(0.08f, duration);
        float elapsed = 0f;
        while (elapsed < dur && visualRt != null)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / dur);
            float pulse = k < 0.35f
                ? Mathf.Lerp(0.45f, 1.12f, k / 0.35f)
                : Mathf.Lerp(1.12f, 0.72f, (k - 0.35f) / 0.65f);

            rt.localScale = Vector3.one * pulse;
            Color c = Color.Lerp(Color.white, color, 0.35f);
            c.a = 1f - Mathf.SmoothStep(0.45f, 1f, k);
            img.color = c;
            yield return null;
        }

        Object.Destroy(go);
    }

    private IEnumerator PullVisualToMagnet(RectTransform visualRt, Vector3 magnetWorld)
    {
        if (visualRt == null)
            yield break;

        Transform tr = visualRt.transform;
        tr.SetAsLastSibling();

        Vector3 start = tr.position;
        Vector3 originalScale = tr.localScale;
        float endScale = Mathf.Clamp(drainPullEndScale, 0.05f, 0.6f);
        float duration = Mathf.Max(0.05f, drainSuctionDuration);
        Vector3 delta = magnetWorld - start;
        Vector3 perp = new Vector3(-delta.y, delta.x, 0f);
        if (perp.sqrMagnitude > 0.0001f)
            perp.Normalize();

        float curve = Mathf.Min(delta.magnitude * Mathf.Clamp01(drainSuctionCurveStrength), board != null ? board.TileSize * 0.42f : delta.magnitude * 0.2f);
        float elapsed = 0f;
        while (elapsed < duration && visualRt != null)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            float arc = Mathf.Sin(k * Mathf.PI) * curve;
            tr.position = Vector3.LerpUnclamped(start, magnetWorld, e) + perp * arc;

            float scale = Mathf.Lerp(1f, endScale, Mathf.SmoothStep(0f, 1f, k));
            tr.localScale = originalScale * scale;
            yield return null;
        }

        if (visualRt != null)
        {
            tr.position = magnetWorld;
            tr.localScale = originalScale * endScale;
        }
    }

    private IEnumerator MoveVisualThroughTubeCarrier(RectTransform visualRt, MagnetView magnetView, int endpointCell, Color color)
    {
        if (visualRt == null || magnetView == null)
            yield break;

        if (!magnetView.TryGetDrainRouteWorld(
                endpointCell,
                drainTubeSamples,
                out Vector3 entryWorld,
                out Vector3 exitWorld,
                out Vector3[] route))
            yield break;

        Transform tr = visualRt.transform;
        float tileScale = Mathf.Clamp(drainTubeTileScale, 0.08f, 0.5f);

        RectTransform carrierRt = CreateTubeCarrier(visualRt, color);
        Image carrierImage = carrierRt != null ? carrierRt.GetComponent<Image>() : null;

        float duration = Mathf.Max(0.05f, drainTubeTravelDuration);
        float elapsed = 0f;
        while (elapsed < duration && visualRt != null)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float e = Mathf.SmoothStep(0f, 1f, k);
            Vector3 pos = SampleRoute(route, e, entryWorld, exitWorld);
            float pulse = 1f + Mathf.Sin(k * Mathf.PI * 4f) * 0.055f;

            tr.position = pos;
            tr.localScale = Vector3.one * tileScale * pulse;
            if (carrierRt != null)
            {
                carrierRt.position = pos;
                carrierRt.localScale = Vector3.one * pulse;
            }

            yield return null;
        }

        if (visualRt != null)
        {
            tr.position = exitWorld;
            yield return PopTubeCarrierAndVisual(visualRt, carrierRt, carrierImage, tileScale);
        }

        if (carrierRt != null)
            Object.Destroy(carrierRt.gameObject);

    }

    private RectTransform CreateTubeCarrier(RectTransform visualRt, Color color)
    {
        if (visualRt == null)
            return null;

        var visualParent = visualRt.parent as RectTransform;
        if (visualParent == null)
            return null;

        var go = new GameObject("MagnetTubeCarrier", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(visualParent, false);
        go.transform.SetSiblingIndex(Mathf.Max(0, visualRt.GetSiblingIndex()));

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        float size = board != null ? board.TileSize * drainTubeCarrierScale : 42f;
        rt.sizeDelta = Vector2.one * size;
        rt.position = visualRt.position;

        var img = go.GetComponent<Image>();
        img.sprite = TubeCarrierSprite();
        img.raycastTarget = false;
        Color c = Color.Lerp(Color.white, color, 0.35f);
        c.a = 0.86f;
        img.color = c;
        return rt;
    }

    private IEnumerator PopTubeCarrierAndVisual(RectTransform visualRt, RectTransform carrierRt, Image carrierImage, float tileScale)
    {
        if (visualRt == null)
            yield break;

        Transform tr = visualRt.transform;
        const float duration = 0.12f;
        float elapsed = 0f;
        while (elapsed < duration && visualRt != null)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float pop = k < 0.42f
                ? Mathf.Lerp(1f, 1.22f, k / 0.42f)
                : Mathf.Lerp(1.22f, 0.02f, (k - 0.42f) / 0.58f);

            tr.localScale = Vector3.one * tileScale * pop;
            if (carrierRt != null)
                carrierRt.localScale = Vector3.one * pop;
            if (carrierImage != null)
            {
                Color c = carrierImage.color;
                c.a = Mathf.Lerp(0.62f, 0f, k);
                carrierImage.color = c;
            }

            yield return null;
        }
    }

    private sealed class DrainVisual
    {
        public readonly RectTransform Root;
        public readonly Image Image;

        public DrainVisual(RectTransform root, Image image)
        {
            Root = root;
            Image = image;
        }
    }

    private static void PlaceBoltSegment(RectTransform rt, Vector2 from, Vector2 to, float thickness)
    {
        if (rt == null)
            return;

        Vector2 d = to - from;
        rt.anchoredPosition = (from + to) * 0.5f;
        rt.sizeDelta = new Vector2(Mathf.Max(1f, d.magnitude), thickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    private static Sprite WhiteSprite()
    {
        if (whitePixelSprite != null)
            return whitePixelSprite;

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        return whitePixelSprite;
    }

    private static Sprite TubeCarrierSprite()
    {
        if (tubeCarrierSprite != null)
            return tubeCarrierSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        float inner = radius * 0.72f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center);
            float edge = Mathf.Clamp01((radius - d) / 2.2f);
            float core = Mathf.Clamp01((inner - d) / 3.5f);
            float a = Mathf.Max(edge, core * 0.45f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, a);
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tubeCarrierSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return tubeCarrierSprite;
    }

    private static Vector3 SampleRoute(Vector3[] route, float t, Vector3 fallbackStart, Vector3 fallbackEnd)
    {
        if (route == null || route.Length == 0)
            return Vector3.LerpUnclamped(fallbackStart, fallbackEnd, t);
        if (route.Length == 1)
            return route[0];

        float scaled = Mathf.Clamp01(t) * (route.Length - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, route.Length - 2);
        float localT = scaled - i;
        return Vector3.LerpUnclamped(route[i], route[i + 1], localT);
    }

    private TileType PickDrainType()
    {
        var pool = board != null ? board.RandomPool : null;
        if (pool != null && pool.Length > 0)
            return pool[Random.Range(0, pool.Length)];
        return FallbackDrainTypes[Random.Range(0, FallbackDrainTypes.Length)];
    }

    private static Color ColorForTileType(TileType type)
    {
        return type switch
        {
            TileType.Gear => new Color(1f, 0.82f, 0.18f, 1f),
            TileType.Core => new Color(1f, 0.28f, 0.26f, 1f),
            TileType.Bolt => new Color(0.22f, 0.55f, 1f, 1f),
            TileType.Plate => new Color(0.28f, 0.92f, 0.46f, 1f),
            _ => Color.white,
        };
    }

    private void StopDrainRoutine()
    {
        if (drainRoutine == null)
            return;
        StopCoroutine(drainRoutine);
        drainRoutine = null;
    }

    // ── Special cage ────────────────────────────────────────────────────────────

    /// Move-start (ConsumeMove): snapshot every special sitting 4-adjacent to a magnet cell/path.
    /// Freshly created specials aren't in the board yet here, so they get their free move.
    private void CaptureCageCandidates()
    {
        cageSnapshot.Clear();
        cageSnapshotSet.Clear();
        if (board == null || board.Tiles == null || magnetsByOrigin.Count == 0)
            return;

        int w = board.Width;
        foreach (var kv in cellToOrigin)
        {
            int cell = kv.Key;
            int origin = kv.Value;
            int cx = cell % w;
            int cy = cell / w;
            TryAddCageCandidate(cx + 1, cy, origin);
            TryAddCageCandidate(cx - 1, cy, origin);
            TryAddCageCandidate(cx, cy + 1, origin);
            TryAddCageCandidate(cx, cy - 1, origin);
        }
    }

    private void TryAddCageCandidate(int x, int y, int origin)
    {
        if (board == null || x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;
        var tile = board.Tiles[x, y];
        if (tile == null || !tile)
            return;
        if (tile.GetSpecial() == TileSpecial.None)
            return;                                             // only specials get caged
        if (cageSnapshotSet.Contains(tile))
            return;                                             // adjacent to two magnet cells → dedupe
        if (board.SpecialLocks != null && board.SpecialLocks.IsLocked(tile))
            return;                                             // already caged

        cageSnapshotSet.Add(tile);
        cageSnapshot.Add(new CageCandidate { Tile = tile, Cell = y * board.Width + x, Origin = origin });
    }

    /// Move-end (board settled): a snapshotted special that is STILL the same tile on the SAME cell and
    /// STILL a special means it neither moved nor took effect this move → cage it.
    private void EvaluateCaging()
    {
        // Reconcile collectors every move-end: any magnet that is now both-ends-open with a backlog throws
        // it ALL out. This is the authoritative flush trigger — uncovering a magnet endpoint (a covering
        // obstacle clearing) restores the magnet via the stamped-beneath path, which does NOT fire
        // OnCellUnlocked, so the event-based flush alone would leave collected specials stuck forever.
        FlushOpenMagnets();

        if (board == null || cageSnapshot.Count == 0)
        {
            cageSnapshot.Clear();
            cageSnapshotSet.Clear();
            return;
        }

        int w = board.Width;
        for (int i = 0; i < cageSnapshot.Count; i++)
        {
            var c = cageSnapshot[i];
            var tile = c.Tile;
            if (tile == null || !tile)
                continue;

            int x = c.Cell % w;
            int y = c.Cell / w;
            if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
                continue;
            if (board.Tiles[x, y] != tile)
                continue;                                       // moved / replaced → it took part in the move
            if (tile.GetSpecial() == TileSpecial.None)
                continue;                                       // consumed / activated → not idle
            if (board.SpecialLocks == null || board.SpecialLocks.IsLocked(tile))
                continue;
            if (!magnetsByOrigin.TryGetValue(c.Origin, out var magnet))
                continue;                                       // magnet destroyed meanwhile
            if (OpenEndpointCount(magnet) == 0)
                continue;                                       // both endpoints covered → magnet inert

            CageSpecial(tile, x, y, c.Origin, magnet);
        }

        cageSnapshot.Clear();
        cageSnapshotSet.Clear();
    }

    private void CageSpecial(TileView tile, int x, int y, int origin, MagnetInstance magnet)
    {
        if (board.SpecialLocks == null)
            return;

        int nearEndpoint = GetNearestOpenEndpointCell(magnet, x, y);
        if (nearEndpoint < 0)
            return;                                             // no open endpoint to act from

        board.SpecialLocks.LockSpecial(
            tile,
            unlockWindowMoves: 1,
            onTimeout: t => StartCoroutine(HandleCagedTimeout(t, origin)),
            onReleased: RemoveCageVisual,
            owner: "Magnet");

        ShowCageVisual(tile);
        if (magnet.View != null)
            StartCoroutine(CastCageLightning(magnet.View.GetEndpointWorldPosition(nearEndpoint), tile));
    }

    private int GetNearestEndpointCell(MagnetInstance magnet, int x, int y)
    {
        int w = board.Width;
        int aCell = magnet.Path[magnet.MagnetAIndex];
        int bCell = magnet.Path[magnet.MagnetBIndex];
        int da = Mathf.Abs((aCell % w) - x) + Mathf.Abs((aCell / w) - y);
        int db = Mathf.Abs((bCell % w) - x) + Mathf.Abs((bCell / w) - y);
        return da <= db ? aCell : bCell;
    }

    private int OppositeEndpointCell(MagnetInstance magnet, int endpointCell)
    {
        int aCell = magnet.Path[magnet.MagnetAIndex];
        int bCell = magnet.Path[magnet.MagnetBIndex];
        return endpointCell == aCell ? bCell : aCell;
    }

    // An endpoint is "open" when the magnet is the TOP obstacle layer at that cell. If another obstacle
    // is stamped over it, the magnet sits beneath (covered) → that endpoint is closed.
    private bool IsEndpointOpen(int endpointCell)
    {
        if (obstacleStateService == null || board == null)
            return true;
        int w = board.Width;
        return obstacleStateService.GetObstacleIdAt(endpointCell % w, endpointCell / w) == ObstacleId.Magnet;
    }

    private int OpenEndpointCount(MagnetInstance magnet)
    {
        int n = 0;
        if (IsEndpointOpen(magnet.ACell)) n++;
        if (IsEndpointOpen(magnet.BCell)) n++;
        return n;
    }

    /// Nearest OPEN endpoint to (x,y), or -1 if neither is open.
    private int GetNearestOpenEndpointCell(MagnetInstance magnet, int x, int y)
    {
        int w = board.Width;
        int aCell = magnet.ACell;
        int bCell = magnet.BCell;
        bool aOpen = IsEndpointOpen(aCell);
        bool bOpen = IsEndpointOpen(bCell);
        if (!aOpen && !bOpen) return -1;
        if (aOpen && !bOpen) return aCell;
        if (bOpen && !aOpen) return bCell;
        int da = Mathf.Abs((aCell % w) - x) + Mathf.Abs((aCell / w) - y);
        int db = Mathf.Abs((bCell % w) - x) + Mathf.Abs((bCell / w) - y);
        return da <= db ? aCell : bCell;
    }

    /// Timeout: the caged special wasn't rescued in its window. Behavior depends on open endpoints:
    ///   • 2 open  → pull to near endpoint, route through tube, re-drop as a special near the far endpoint.
    ///   • 1 open  → collector: pull into the open endpoint and accumulate it (thrown out later when both open).
    ///   • 0 open  → magnet went inert meanwhile → leave the (now unlocked) special on the board.
    private IEnumerator HandleCagedTimeout(TileView tile, int origin)
    {
        RemoveCageVisual(tile);

        if (board == null || tile == null || !tile)
            yield break;
        if (!magnetsByOrigin.TryGetValue(origin, out var magnet) || magnet.View == null)
            yield break;

        int x = tile.X;
        int y = tile.Y;
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height || board.Tiles[x, y] != tile)
            yield break;

        TileSpecial kind = tile.GetSpecial();
        if (kind == TileSpecial.None)
            yield break;

        int nearEndpoint = GetNearestOpenEndpointCell(magnet, x, y);
        if (nearEndpoint < 0)
            yield break;                                        // no open endpoint → leave it be
        bool bothOpen = OpenEndpointCount(magnet) >= 2;

        // Capture the look, then pull the special off the board (it's being sucked into the tube).
        var visual = CreateDrainVisual(tile);
        board.ClearCell(x, y);
        board.ReleaseTile(tile);
        board.RequestResolveAfterActionSequence();

        Vector3 nearWorld = magnet.View.GetEndpointWorldPosition(nearEndpoint);
        if (visual != null && visual.Root != null)
            yield return PullVisualToMagnet(visual.Root, nearWorld);

        // Suck the special through the tubes (pops at the far end of the route).
        if (visual != null && visual.Root != null)
        {
            yield return MoveVisualThroughTubeCarrier(visual.Root, magnet.View, nearEndpoint, Color.white);
            Object.Destroy(visual.Root.gameObject);
        }

        if (bothOpen)
        {
            // Squeeze it out of the far endpoint (gel-pop) onto a nearby NORMAL tile.
            int farEndpoint = OppositeEndpointCell(magnet, nearEndpoint);
            yield return EjectSpecialFromEndpoint(magnet, farEndpoint, kind);
        }
        else
        {
            // Collector: held inside until the other endpoint opens.
            magnet.Collected.Add(kind);
        }
    }

    /// Gel-squeeze eject: the special's icon pops out of the endpoint and arcs onto a RANDOM normal-tile
    /// cell anywhere on the board, where it materializes. Waits for a free cell rather than dropping.
    private IEnumerator EjectSpecialFromEndpoint(MagnetInstance magnet, int endpointCell, TileSpecial kind)
    {
        if (board == null || magnet == null || magnet.View == null)
            yield break;

        Vector3 exitWorld = magnet.View.GetEndpointWorldPosition(endpointCell);
        yield return EjectSpecialToRandomTile(exitWorld, kind);
    }

    // A flush/eject often starts while the board is still resolving (few cells momentarily targetable) and
    // every stamp turns one normal cell into a special → later placements can find no cell. Instead of
    // dropping the special we wait (bounded) for the board to settle and free a valid cell.
    private const float EjectPlacementTimeoutSeconds = 5f;

    /// Shared eject-to-random-tile: the special's icon flies from originWorld to a random normal tile, then
    /// materializes there. Used by throw / flush / destroy-release. Never drops the special unless the board
    /// stays saturated (no normal cell) for the whole timeout.
    private IEnumerator EjectSpecialToRandomTile(Vector3 originWorld, TileSpecial kind)
    {
        int tx = -1, ty = -1;

        // Wait for a valid normal cell before launching (board may be mid-resolve / momentarily full).
        float waited = 0f;
        while (!TryFindRandomNormalTile(out tx, out ty))
        {
            waited += Time.deltaTime;
            if (waited > EjectPlacementTimeoutSeconds || board == null)
                yield break;                                    // board stayed saturated → nothing to place onto
            yield return null;
        }

        var blob = CreateSpecialCarrierVisual(originWorld, kind, sizeScale: 0.9f);
        Vector3 targetWorld = board.Tiles[tx, ty] != null ? board.Tiles[tx, ty].transform.position : originWorld;

        yield return GelEject(blob, targetWorld);

        if (blob != null)
            Object.Destroy(blob.gameObject);

        // The pre-chosen cell may have been consumed mid-flight (another eject / cascade). Re-pick so the
        // special is never silently lost; only give up if no normal cell frees within the timeout.
        if (!IsNormalTileCell(tx, ty))
        {
            waited = 0f;
            while (!TryFindRandomNormalTile(out tx, out ty))
            {
                waited += Time.deltaTime;
                if (waited > EjectPlacementTimeoutSeconds || board == null)
                    yield break;
                yield return null;
            }
        }

        var t = board.Tiles[tx, ty];
        t.SetSpecial(kind);
        board.SyncTileData(tx, ty);
        board.RequestResolveAfterActionSequence();
    }

    /// Release collected specials onto RANDOM board tiles — used when the magnet is destroyed outright
    /// (so accumulated specials are never lost).
    private IEnumerator ReleaseCollectedRandom(List<TileSpecial> kinds, Vector3 originWorld)
    {
        if (kinds == null)
            yield break;
        for (int i = 0; i < kinds.Count; i++)
        {
            yield return EjectSpecialToRandomTile(originWorld, kinds[i]);
            yield return null;
        }
    }

    /// A "normal tile" cell = holds a plain (non-special) color tile, no obstacle/lock/movable, not a hole.
    private bool IsNormalTileCell(int x, int y)
    {
        if (board == null || board.Tiles == null)
            return false;
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;
        if (board.IsMaskHoleCell(x, y))
            return false;
        var t = board.Tiles[x, y];
        if (t == null || !t || t.GetSpecial() != TileSpecial.None)
            return false;
        if (obstacleStateService != null &&
            (obstacleStateService.HasObstacleAt(x, y)
             || obstacleStateService.IsInteractionLockedAt(x, y)
             || obstacleStateService.IsMovableObstacleAt(x, y)))
            return false;
        return SpecialUtils.CanTargetTileContent(board, x, y);
    }

    /// A RANDOM normal-tile cell anywhere on the board (user wants random placement, not nearest).
    private readonly List<Vector2Int> _normalTileScratch = new();
    private bool TryFindRandomNormalTile(out int tx, out int ty)
    {
        tx = ty = -1;
        if (board == null)
            return false;

        _normalTileScratch.Clear();
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
            if (IsNormalTileCell(x, y))
                _normalTileScratch.Add(new Vector2Int(x, y));

        if (_normalTileScratch.Count == 0)
            return false;

        var pick = _normalTileScratch[Random.Range(0, _normalTileScratch.Count)];
        tx = pick.x;
        ty = pick.y;
        return true;
    }

    /// Squash → stretched arc → landing bounce. The blob flies from its current spot onto targetWorld.
    private IEnumerator GelEject(RectTransform rt, Vector3 targetWorld)
    {
        if (rt == null || board == null)
            yield break;

        Vector2 start = rt.anchoredPosition;
        Vector2 end = board.WorldToAnchoredIn(board.BreakFxParent, targetWorld);
        Vector2 dir = end - start;
        float dist = dir.magnitude;
        Vector2 perp = dist > 0.001f ? new Vector2(-dir.y, dir.x).normalized : Vector2.up;

        // Squeeze (x/y ezme) YOK — ölçek her zaman UNIFORM. Küçük imajdan başlar, uçuş boyunca
        // büyüyüp hedefe varınca NORMAL boyuta gelir. Hareket hızlı ve "sıçrama" gibi: güçlü
        // ease-out (baştan fışkırır) + sönen yanal seğirme (yağ sıçraması hissi).
        const float startScale = 0.32f;
        rt.localScale = Vector3.one * startScale;
        rt.localRotation = Quaternion.identity;

        float travelDur = Mathf.Clamp(dist / Mathf.Max(1f, board.TileSize * 16f), 0.14f, 0.34f);
        float arc = Mathf.Min(dist * 0.18f, board.TileSize * 0.6f);
        float wobbleAmp = Mathf.Min(board.TileSize * 0.15f, dist * 0.12f);

        float t = 0f;
        while (t < travelDur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / travelDur);
            float e = 1f - Mathf.Pow(1f - k, 3f);               // güçlü ease-out → fışkırma

            // Ana yol + kavis + sönen yüksek-frekans yanal seğirme (sıçrama titremesi).
            Vector2 pos = Vector2.LerpUnclamped(start, end, e)
                        + perp * (Mathf.Sin(k * Mathf.PI) * arc)
                        + perp * (Mathf.Sin(k * Mathf.PI * 5f) * wobbleAmp * (1f - k));
            rt.anchoredPosition = pos;

            float s = Mathf.Lerp(startScale, 1f, e);            // küçük → normal (uniform)
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        rt.anchoredPosition = end;
        rt.localScale = Vector3.one;                             // hedefte tam normal boyut
        rt.localRotation = Quaternion.identity;
    }

    // ── Collector flush (both endpoints open again) ──────────────────────────────

    /// Every move-end: any magnet that has a backlog AND both endpoints open throws it ALL out. Reliable
    /// trigger regardless of HOW the endpoint re-opened (obstacle cleared, etc.).
    private void FlushOpenMagnets()
    {
        if (board == null || magnetsByOrigin.Count == 0)
            return;

        foreach (var kv in magnetsByOrigin)
        {
            var magnet = kv.Value;
            if (magnet == null || magnet.Flushing)
                continue;
            if (magnet.Collected.Count == 0)
                continue;
            if (OpenEndpointCount(magnet) < 2)
                continue;                                       // still needs both ends open

            // Eject out whichever endpoint is open (A preferred); specials travel in from the opposite end.
            int exit = IsEndpointOpen(magnet.ACell) ? magnet.ACell : magnet.BCell;
            StartCoroutine(FlushCollected(kv.Key, openedEndpoint: exit));
        }
    }

    /// A covered cell was restored. If it's a magnet endpoint and BOTH endpoints are now open, throw out
    /// everything this magnet collected. NOTE: uncovering a magnet via the stamped-beneath path does NOT
    /// fire OnCellUnlocked, so this rarely runs — the move-end FlushOpenMagnets pass is the real trigger.
    private void HandleCellUnlocked(int cellIndex)
    {
        if (!cellToOrigin.TryGetValue(cellIndex, out int origin))
            return;
        if (!magnetsByOrigin.TryGetValue(origin, out var magnet))
            return;
        if (cellIndex != magnet.ACell && cellIndex != magnet.BCell)
            return;                                             // only endpoints act as gates
        if (magnet.Flushing || magnet.Collected.Count == 0)
            return;
        if (OpenEndpointCount(magnet) < 2)
            return;                                             // still needs both ends open

        // Specials exit from the endpoint that JUST opened; they travel in from the opposite (old-open) end.
        StartCoroutine(FlushCollected(origin, openedEndpoint: cellIndex));
    }

    private IEnumerator FlushCollected(int origin, int openedEndpoint)
    {
        if (!magnetsByOrigin.TryGetValue(origin, out var magnet) || magnet.View == null)
            yield break;
        if (magnet.Flushing || magnet.Collected.Count == 0)
            yield break;                                        // re-entry guard (double trigger)

        // Snapshot + clear up front so a second trigger can't double-flush.
        magnet.Flushing = true;
        var kinds = new List<TileSpecial>(magnet.Collected);
        magnet.Collected.Clear();

        int exit = openedEndpoint;                              // eject out the newly opened side
        int entry = OppositeEndpointCell(magnet, exit);

        for (int i = 0; i < kinds.Count; i++)
        {
            var rt = CreateSpecialCarrierVisual(magnet.View.GetEndpointWorldPosition(entry), kinds[i], sizeScale: 1f);
            if (rt != null)
            {
                yield return MoveVisualThroughTubeCarrier(rt, magnet.View, entry, Color.white);
                Object.Destroy(rt.gameObject);
            }

            yield return EjectSpecialFromEndpoint(magnet, exit, kinds[i]);
            yield return null;
        }

        magnet.Flushing = false;   // backlog emptied → allow a future collect/flush cycle
    }

    private RectTransform CreateSimpleCarrierVisual(Vector3 worldPos, float sizeScale = 0.42f)
    {
        if (board == null || board.BreakFxParent == null)
            return null;

        var go = new GameObject("MagnetCollectedCarrier", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(board.BreakFxParent, false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.sprite = TubeCarrierSprite();
        img.raycastTarget = false;
        img.color = new Color(0.7f, 0.9f, 1f, 0.95f);

        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * (board.TileSize * sizeScale);
        rt.anchoredPosition = board.WorldToAnchoredIn(board.BreakFxParent, worldPos);
        return rt;
    }

    /// Carrier that shows the SPECIAL's own icon (not the abstract blur blob) — used for the throw/eject
    /// sequence so the player sees the actual special squeeze out and fly onto the board. Falls back to the
    /// blur blob only if the special has no registered icon.
    private RectTransform CreateSpecialCarrierVisual(Vector3 worldPos, TileSpecial kind, float sizeScale = 0.9f)
    {
        Sprite icon = board != null ? board.GetSpecialIcon(kind) : null;
        if (icon == null)
            return CreateSimpleCarrierVisual(worldPos, sizeScale);

        if (board.BreakFxParent == null)
            return null;

        var go = new GameObject("MagnetSpecialCarrier", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(board.BreakFxParent, false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.sprite = icon;
        img.raycastTarget = false;
        img.preserveAspect = true;
        img.color = Color.white;

        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * (board.TileSize * sizeScale);
        rt.anchoredPosition = board.WorldToAnchoredIn(board.BreakFxParent, worldPos);
        return rt;
    }

    // ── Cage visuals ─────────────────────────────────────────────────────────────

    private void ShowCageVisual(TileView tile)
    {
        if (tile == null || !tile)
            return;
        if (cageVisuals.ContainsKey(tile))
            return;

        // Parent to the TILE (exactly like an obstacle sits on a cell): it fills the cell and rides the
        // tile through gravity automatically — no per-frame repositioning, so no drift / left-right wobble.
        var go = new GameObject("MagnetCage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(tile.transform, false);
        go.transform.SetAsLastSibling();

        Sprite authoredSprite = ResolveCageSprite();
        bool hasAuthored = authoredSprite != null;
        var img = go.GetComponent<Image>();
        img.sprite = hasAuthored ? authoredSprite : CageRingSprite();
        img.raycastTarget = false;
        img.preserveAspect = hasAuthored;
        img.color = hasAuthored ? Color.white : new Color(0.55f, 0.85f, 1f, 0.9f);

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;                 // stretch to fill the tile exactly (whole cell)
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.zero;                // grows from the centre

        cageVisuals[tile] = go;
        StartCoroutine(FollowCage(tile, rt, img));
    }

    private IEnumerator FollowCage(TileView tile, RectTransform rt, Image img)
    {
        // Grow from the centre with a small elastic overshoot → snaps shut over the special.
        const float growDur = 0.22f;
        float g = 0f;
        while (g < growDur && tile != null && tile && rt != null)
        {
            g += Time.deltaTime;
            rt.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(g / growDur));
            yield return null;
        }
        if (rt != null)
            rt.localScale = Vector3.one;

        // Hold until unlocked. Only a gentle alpha shimmer — NO rotation/position/scale movement, so the
        // cage stays locked to the cell (no wobble). Position handled by the parent tile (falls with it).
        Color baseColor = img != null ? img.color : Color.white;
        while (tile != null && tile && tile.IsSpecialLocked && rt != null)
        {
            if (img != null)
            {
                var c = baseColor;
                c.a = Mathf.Clamp01(baseColor.a * (0.85f + Mathf.Sin(Time.time * 4f) * 0.12f));
                img.color = c;
            }
            yield return null;
        }
        RemoveCageVisual(tile);
    }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = k - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    private void RemoveCageVisual(TileView tile)
    {
        if (tile == null)
            return;
        if (cageVisuals.TryGetValue(tile, out var go))
        {
            cageVisuals.Remove(tile);
            if (go != null)
                Object.Destroy(go);
        }
    }

    private void ClearAllCageVisuals()
    {
        foreach (var kv in cageVisuals)
            if (kv.Value != null)
                Object.Destroy(kv.Value);
        cageVisuals.Clear();
    }

    private IEnumerator CastCageLightning(Vector3 fromWorld, TileView tile)
    {
        RectTransform parent = board != null ? board.BreakFxParent : null;
        if (parent == null || tile == null)
            yield break;

        Vector2 start = board.WorldToAnchoredIn(parent, fromWorld);
        Vector2 end = board.WorldToAnchoredIn(parent, tile.transform.position);
        Vector2 delta = end - start;
        Vector2 perp = delta.sqrMagnitude > 0.001f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;

        int segmentCount = Mathf.Max(3, drainElectricSegments);
        var points = new Vector2[segmentCount + 1];
        points[0] = start;
        points[segmentCount] = end;
        float wiggle = Mathf.Max(4f, board.TileSize * 0.14f);
        for (int i = 1; i < segmentCount; i++)
        {
            float k = i / (float)segmentCount;
            float offset = Mathf.Sin(k * Mathf.PI * 3f) * wiggle + Random.Range(-wiggle * 0.35f, wiggle * 0.35f);
            points[i] = Vector2.Lerp(start, end, k) + perp * offset;
        }

        var segments = new RectTransform[segmentCount];
        var images = new Image[segmentCount];
        float thickness = Mathf.Max(2f, board.TileSize * drainElectricThicknessRatio);
        Sprite sprite = WhiteSprite();
        Color boltColor = new Color(0.55f, 0.85f, 1f, 1f);
        for (int i = 0; i < segmentCount; i++)
        {
            var go = new GameObject("MagnetCageBolt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.color = new Color(boltColor.r, boltColor.g, boltColor.b, 0f);

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            PlaceBoltSegment(rt, points[i], points[i + 1], thickness);
            segments[i] = rt;
            images[i] = img;
        }

        float duration = Mathf.Max(0.12f, drainElectricMarkDuration * 0.6f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float head = k * segmentCount;
            float fade = 1f - Mathf.Clamp01((k - 0.7f) / 0.3f);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null)
                    continue;
                float reveal = Mathf.Clamp01(head - i);
                float flicker = Random.value < 0.2f ? 0.5f : 1f;
                Color c = boltColor;
                c.a = reveal * fade * flicker;
                images[i].color = c;
            }
            yield return null;
        }

        for (int i = 0; i < segments.Length; i++)
            if (segments[i] != null)
                Object.Destroy(segments[i].gameObject);
    }

    private static Sprite CageRingSprite()
    {
        if (cageRingSprite != null)
            return cageRingSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outer = size * 0.48f;
        float inner = size * 0.37f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center);
            float ring = Mathf.Clamp01((outer - d) / 2f) * Mathf.Clamp01((d - inner) / 2f);
            // Corner brackets: brighten the four diagonal quadrants' outer arc a touch for a "cage" read.
            pixels[y * size + x] = new Color(1f, 1f, 1f, ring);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cageRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cageRingSprite;
    }

    // ── Hit handling ──────────────────────────────────────────────────────────

    /// Called by ObstacleStateService with the ACTUAL hit cell index (not origin).
    /// Returns TRUE if this was a current endpoint (A/B) and the magnet shrank/destroyed;
    /// FALSE if the cell is inert (middle path / not a magnet) → çağıran "no hit" sayar.
    public bool HandleMagnetHit(int hitCellIndex)
    {
        if (!cellToOrigin.TryGetValue(hitCellIndex, out int origin)) return false;
        if (!magnetsByOrigin.TryGetValue(origin, out var magnet)) return false;

        bool isA = hitCellIndex == magnet.Path[magnet.MagnetAIndex];
        bool isB = hitCellIndex == magnet.Path[magnet.MagnetBIndex];

        // Only endpoint magnets react — middle path cells are inert blockers.
        if (!isA && !isB) return false;

        int prevAIdx = magnet.MagnetAIndex;
        int prevBIdx = magnet.MagnetBIndex;

        if (isA)
        {
            int freed = magnet.Path[magnet.MagnetAIndex];
            cellToOrigin.Remove(freed);
            magnet.MagnetAIndex++;
            obstacleStateService?.FreeMagnetCell(freed);
        }
        else
        {
            int freed = magnet.Path[magnet.MagnetBIndex];
            cellToOrigin.Remove(freed);
            magnet.MagnetBIndex--;
            obstacleStateService?.FreeMagnetCell(freed);
        }

        if (magnet.MagnetAIndex >= magnet.MagnetBIndex)
        {
            DestroyMagnetPair(origin, magnet);
            return true;
        }

        magnet.View.UpdatePositions(magnet.MagnetAIndex, magnet.MagnetBIndex, prevAIdx, prevBIdx);
        return true;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void DestroyMagnetPair(int origin, MagnetInstance magnet)
    {
        // Free the meeting cell (where A and B now coincide or have just passed).
        int meetCell = magnet.Path[magnet.MagnetAIndex];

        // Magnet is being destroyed (e.g. a special chain broke its covering obstacle AND the magnet
        // in one go). Release whatever it collected so those specials are never lost.
        if (magnet.Collected.Count > 0)
        {
            var kinds = new List<TileSpecial>(magnet.Collected);
            magnet.Collected.Clear();
            Vector3 originWorld = magnet.View != null
                ? magnet.View.GetEndpointWorldPosition(meetCell)
                : Vector3.zero;
            StartCoroutine(ReleaseCollectedRandom(kinds, originWorld));
        }

        cellToOrigin.Remove(meetCell);
        obstacleStateService?.FreeMagnetCell(meetCell);

        // Safety: free B's position too if they happened to be different (shouldn't occur).
        if (magnet.MagnetAIndex != magnet.MagnetBIndex)
        {
            int bCell = magnet.Path[magnet.MagnetBIndex];
            cellToOrigin.Remove(bCell);
            obstacleStateService?.FreeMagnetCell(bCell);
        }

        magnetsByOrigin.Remove(origin);
        magnet.View.PlayDestroyAnimation();
        obstacleStateService?.NotifyMagnetFullyDestroyed(origin);
    }

    // ── Inner class ───────────────────────────────────────────────────────────

    private sealed class MagnetInstance
    {
        public readonly int[] Path;
        public readonly MagnetView View;
        public int MagnetAIndex;
        public int MagnetBIndex;
        public bool UseAEndpointForDrain = true;
        // Collector mode: specials pulled in while only one endpoint is open, thrown out once both open.
        public readonly List<TileSpecial> Collected = new();
        // True while a flush coroutine is draining Collected → blocks re-entrant / double flushes.
        public bool Flushing;

        public MagnetInstance(int[] path, MagnetView view)
        {
            Path = path;
            View = view;
            MagnetAIndex = 0;
            MagnetBIndex = path.Length - 1;
        }

        public int ACell => Path[MagnetAIndex];
        public int BCell => Path[MagnetBIndex];
    }
}
