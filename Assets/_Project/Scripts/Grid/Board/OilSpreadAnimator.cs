using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class OilSpreadAnimator : MonoBehaviour
{
    [SerializeField] private GameObject spreadFxPrefab;  // UI_OilSpreadFX
    [SerializeField] private BoardController board;
    [SerializeField] private float duration = 0.50f;
    [SerializeField, Min(0.05f)] private float spreadFrameSequenceDuration = 0.42f;
    [SerializeField, Range(0.05f, 0.95f)] private float spreadFrameCrossFadeRatio = 0.55f;
    [SerializeField] private Sprite oilOverlaySprite;
    [SerializeField] private Texture oilInteriorTexture;
    [SerializeField] private Sprite oilDropSprite;
    [SerializeField] private Sprite oilSpreadSprite;
    [SerializeField] private Sprite[] spreadUpFrames;
    [SerializeField] private Sprite[] spreadRightFrames;

    // Kalıcı oil görseli (cell-anchored blob) bu assetlerle çizilir — OilOverlayRenderer kullanır.
    public Sprite OilOverlaySprite => oilOverlaySprite;
    public Texture OilInteriorTexture => oilInteriorTexture;

    // One driver for all flights. Views (including drop/blend children) use the shared UI pool.
    // Each flight owns its level-end job explicitly, so disabling the component cannot leak it.
    private sealed class Flight
    {
        public GameObject root;
        public Image bridge, target, drop, blend;
        public RectTransform dropRect;
        public Vector2 dropStart, dropEnd;
        public Vector2Int cell;
        public float age, growTime, revealTime, frameTime, fadeTime;
        public Sprite finalSprite;
        public readonly List<Sprite> frames = new(8);
        public System.IDisposable job;
        public string poolKey;
    }

    private readonly List<Flight> active = new(4);
    private readonly Stack<Flight> reusable = new(4);
    private string poolKey;
    private int poolPrefabId;

    public bool IsAnimatingTarget(Vector2Int cell)
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i].cell == cell)
                return true;
        return false;
    }

    public void PlaySpread(IReadOnlyList<OilSpreadPair> pairs)
    {
        if (!isActiveAndEnabled || board == null || spreadFxPrefab == null || board.TilesRoot == null
            || pairs == null)
            return;

        int prefabId = spreadFxPrefab.GetInstanceID();
        if (poolKey == null || poolPrefabId != prefabId)
        {
            poolPrefabId = prefabId;
            poolKey = "OilSpread:" + prefabId;
        }

        for (int i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (board.ObstacleStateService == null
                || !board.ObstacleStateService.IsOilAt(pair.Target.x, pair.Target.y)
                || IsAnimatingTarget(pair.Target))
                continue;

            var flight = reusable.Count > 0 ? reusable.Pop() : new Flight();
            try
            {
                flight.poolKey = poolKey;
                flight.root = UiVfxPool.TryRent(poolKey, board.TilesRoot, spreadFxPrefab.name);
                if (flight.root == null)
                    flight.root = Instantiate(spreadFxPrefab, board.TilesRoot);
                Configure(flight, pair);
                flight.job = board.BeginJob(BoardController.BoardJobKind.OilSpread);
                active.Add(flight);
            }
            catch
            {
                Release(flight);
                throw;
            }
        }
    }

    private void Configure(Flight f, OilSpreadPair pair)
    {
        float ts = board.TileSize;
        int dx = pair.Target.x - pair.Source.x;
        int dy = pair.Target.y - pair.Source.y;
        bool same = dx == 0 && dy == 0;
        bool horizontal = !same && dy == 0;
        var source = new Vector2(pair.Source.x * ts + ts * .5f, -pair.Source.y * ts - ts * .5f);
        var target = new Vector2(pair.Target.x * ts + ts * .5f, -pair.Target.y * ts - ts * .5f);
        var midpoint = (source + target) * .5f;
        var rt = f.root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = midpoint;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        // UiVfxPool resets root size on reuse; restore prefab geometry as well.
        if (spreadFxPrefab.transform is RectTransform prefabRect)
            rt.sizeDelta = prefabRect.sizeDelta;
        rt.SetAsLastSibling();
        f.root.SetActive(true);

        var h = rt.Find("Bridge_H");
        var v = rt.Find("Bridge_V");
        if (h != null) h.gameObject.SetActive(horizontal);
        if (v != null) v.gameObject.SetActive(!horizontal);
        f.bridge = (horizontal ? h : v)?.GetComponent<Image>();
        if (f.bridge != null)
        {
            var bridgeRect = f.bridge.rectTransform;
            bridgeRect.anchorMin = bridgeRect.anchorMax = bridgeRect.pivot = new Vector2(.5f, .5f);
            bridgeRect.anchoredPosition = Vector2.zero;
            float cross = horizontal ? bridgeRect.sizeDelta.y : bridgeRect.sizeDelta.x;
            if (cross <= 1f) cross = 24f;
            float length = (same ? 1f : Mathf.Max(1, Mathf.Abs(horizontal ? dx : dy))) * ts;
            bridgeRect.sizeDelta = horizontal ? new Vector2(length, cross) : new Vector2(cross, length);
            f.bridge.type = Image.Type.Filled;
            f.bridge.fillMethod = horizontal ? Image.FillMethod.Horizontal : Image.FillMethod.Vertical;
            // Grid Y grows down, so downward flow fills from the TOP of the image.
            f.bridge.fillOrigin = horizontal
                ? (int)(dx > 0 ? Image.OriginHorizontal.Left : Image.OriginHorizontal.Right)
                : (int)(dy > 0 ? Image.OriginVertical.Top : Image.OriginVertical.Bottom);
            f.bridge.fillAmount = 0f;
            f.bridge.raycastTarget = false;
        }

        var targetTransform = rt.Find("TargetOil");
        f.target = targetTransform != null ? targetTransform.GetComponent<Image>() : null;
        f.blend = null;
        f.finalSprite = oilSpreadSprite != null ? oilSpreadSprite : oilOverlaySprite;
        if (f.target != null)
        {
            var targetRect = f.target.rectTransform;
            targetRect.anchorMin = targetRect.anchorMax = targetRect.pivot = new Vector2(.5f, .5f);
            targetRect.anchoredPosition = target - midpoint;
            targetRect.sizeDelta = new Vector2(ts, ts);
            targetRect.localScale = Vector3.one;
            float rotation = same ? 0f : horizontal ? (dx < 0 ? 180f : 0f) : (dy > 0 ? 180f : 0f);
            targetRect.localEulerAngles = new Vector3(0f, 0f, rotation);
            f.target.sprite = f.finalSprite;
            f.target.type = Image.Type.Simple;
            f.target.preserveAspect = false;
            f.target.color = Color.white;
            f.target.raycastTarget = false;
            f.blend = GetOrCreateImage(targetRect, "OilFrameBlend");
            var blendRect = f.blend.rectTransform;
            blendRect.anchorMin = Vector2.zero;
            blendRect.anchorMax = Vector2.one;
            blendRect.pivot = new Vector2(.5f, .5f);
            blendRect.anchoredPosition = blendRect.sizeDelta = Vector2.zero;
            blendRect.localScale = Vector3.one;
            blendRect.localRotation = Quaternion.identity;
            SetImageAlpha(f.blend, 0f);
            f.target.gameObject.SetActive(false);
        }

        f.drop = GetOrCreateImage(rt, "OilDrop");
        f.drop.sprite = oilDropSprite;
        f.drop.color = Color.white;
        f.drop.preserveAspect = true;
        f.drop.gameObject.SetActive(oilDropSprite != null);
        f.dropRect = f.drop.rectTransform;
        f.dropRect.anchorMin = f.dropRect.anchorMax = f.dropRect.pivot = new Vector2(.5f, .5f);
        f.dropStart = (same ? target : source) - midpoint + Vector2.up * ts * .45f;
        f.dropEnd = target - midpoint;
        f.dropRect.anchoredPosition = f.dropStart;
        f.dropRect.sizeDelta = new Vector2(ts * .72f, ts);
        f.dropRect.localScale = Vector3.one;
        f.dropRect.localRotation = Quaternion.identity;
        f.cell = pair.Target;
        f.age = 0f;
        f.growTime = Mathf.Max(.001f, duration * .65f);
        f.revealTime = Mathf.Max(.001f, Mathf.Max(duration * .35f, spreadFrameSequenceDuration));
        f.frames.Clear();
        if (!same && !horizontal) AddFrame(f.frames, f.finalSprite);
        if (!same) AddFrames(f.frames, horizontal ? spreadRightFrames : spreadUpFrames);
        AddFrame(f.frames, f.finalSprite);
        f.frameTime = Mathf.Max(.04f, f.revealTime / Mathf.Max(1, f.frames.Count));
        f.fadeTime = Mathf.Clamp(f.frameTime * spreadFrameCrossFadeRatio, .025f, f.frameTime);
    }

    private void Update()
    {
        bool changed = false;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var f = active[i];
            // A special can clear the incoming Oil while its blob is flying. Cancel its visual
            // immediately; never re-add gameplay Oil or reveal a stale target on arrival.
            if (board == null || !board.isActiveAndEnabled || f.root == null
                || board.ObstacleStateService == null
                || !board.ObstacleStateService.IsOilAt(f.cell.x, f.cell.y)
                || !Tick(f, Time.deltaTime))
            {
                active[i] = active[active.Count - 1];
                active.RemoveAt(active.Count - 1);
                Release(f);
                changed = true;
            }
        }
        // No full-board scans/overlay rebuilds per animation frame.
        if (changed && board != null && board.isActiveAndEnabled)
            board.RefreshOilOverlays();
    }

    private static bool Tick(Flight f, float deltaTime)
    {
        f.age += deltaTime;
        float k = Mathf.Clamp01(f.age / f.growTime);
        float eased = Mathf.SmoothStep(0f, 1f, k);
        if (f.bridge != null) f.bridge.fillAmount = eased;
        if (f.dropRect != null)
        {
            f.dropRect.anchoredPosition = Vector2.LerpUnclamped(f.dropStart, f.dropEnd, eased);
            f.dropRect.localScale = Vector3.one * Mathf.Lerp(1f, .72f, k);
        }
        if (f.age < f.growTime) return true;

        if (f.drop != null) f.drop.gameObject.SetActive(false);
        float revealAge = f.age - f.growTime;
        if (f.frames.Count == 0) return revealAge < f.revealTime;
        int index = (int)(revealAge / f.frameTime);
        if (index >= f.frames.Count) return false;
        if (f.target != null)
        {
            f.target.gameObject.SetActive(true);
            float blend = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((revealAge - index * f.frameTime) / f.fadeTime));
            f.target.sprite = index > 0 ? f.frames[index - 1] : f.finalSprite;
            SetImageAlpha(f.target, 1f - blend);
            if (f.blend != null)
            {
                f.blend.sprite = f.frames[index];
                SetImageAlpha(f.blend, blend);
            }
        }
        return true;
    }

    private void OnDisable() => CancelSpreads();

    public void CancelSpreads()
    {
        bool hadFlights = active.Count > 0;
        while (active.Count > 0)
        {
            var flight = active[active.Count - 1];
            active.RemoveAt(active.Count - 1);
            Release(flight);
        }
        if (hadFlights && board != null && board.isActiveAndEnabled)
            board.RefreshOilOverlays();
    }

    private void Release(Flight f)
    {
        try
        {
            if (f.root != null) UiVfxPool.Return(f.poolKey, f.root, 8);
        }
        finally
        {
            f.root = null;
            f.bridge = f.target = f.drop = f.blend = null;
            f.dropRect = null;
            f.frames.Clear();
            f.job?.Dispose();
            f.job = null;
            reusable.Push(f);
        }
    }

    private static Image GetOrCreateImage(RectTransform parent, string name)
    {
        var child = parent.Find(name);
        Image image = child != null ? child.GetComponent<Image>() : null;
        if (image == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            image = go.GetComponent<Image>();
        }
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        image.gameObject.SetActive(true);
        return image;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private static void AddFrames(List<Sprite> frames, Sprite[] sprites)
    {
        if (sprites == null) return;
        for (int i = 0; i < sprites.Length; i++) AddFrame(frames, sprites[i]);
    }

    private static void AddFrame(List<Sprite> frames, Sprite sprite)
    {
        if (sprite != null) frames.Add(sprite);
    }
}
