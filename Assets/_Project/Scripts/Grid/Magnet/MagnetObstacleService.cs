using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Manages all magnet pair obstacles in the current level.
/// Two magnets share an energy path. Hitting a magnet endpoint moves it one step
/// toward the other. When they meet, both vanish along with the connecting path.
public class MagnetObstacleService : MonoBehaviour
{
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
        board = boardController;
        obstacleStateService = service;
        magnetsByOrigin.Clear();
        cellToOrigin.Clear();
        StopDrainRoutine();
        nextDrainTime = Time.time + Mathf.Max(0f, firstDrainDelay);
        if (enableDrainPulse)
            drainRoutine = StartCoroutine(DrainPulseRoutine());
    }

    public void Init(ObstacleStateService service) => Init(null, service);

    private void OnDisable() => StopDrainRoutine();

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

        public MagnetInstance(int[] path, MagnetView view)
        {
            Path = path;
            View = view;
            MagnetAIndex = 0;
            MagnetBIndex = path.Length - 1;
        }
    }
}
