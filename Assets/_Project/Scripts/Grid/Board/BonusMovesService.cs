using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bonus round when the player wins with remaining moves.
/// Phase 1: Each remaining move fires a shooting-star comet; on landing, LineV/LineH is placed.
/// Phase 2: All placed specials trigger simultaneously.
/// </summary>
public class BonusMovesService : MonoBehaviour
{
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;
    [SerializeField] private RectTransform vfxOverlayRoot;

    [Header("Timing")]
    [SerializeField] private float cometDuration  = 0.28f;  // travel time per comet
    [SerializeField] private float cometStagger   = 0.10f;  // delay between comet starts
    [SerializeField] private float preTriggerPause = 0.18f; // pause before mass trigger
    [SerializeField] private float drainMoveDelay = 0.04f;  // when no cells available

    [Header("Comet Visual")]
    [SerializeField] private int   trailDots  = 7;
    [SerializeField] private float headSize   = 0.38f; // fraction of tileSize
    [SerializeField] private float tailEndSize = 0.07f; // smallest tail dot fraction

    // ─────────────────────────────────────────────────────────────────
    public IEnumerator RunBonusRound()
    {
        if (board == null) yield break;

        int total = board.RemainingMoves;
        if (total <= 0) yield break;

        // Pre-select target cells (track used cells to avoid duplicates)
        var placements = new List<BoardController.BonusLinePlacement>(total);
        var usedCells  = new HashSet<Vector2Int>();

        for (int i = 0; i < total; i++)
        {
            var cell = PickRandomNormalCell(usedCells);
            if (!cell.HasValue) break;
            bool isH = Random.value > 0.5f;
            placements.Add(new BoardController.BonusLinePlacement(cell.Value.x, cell.Value.y, isH));
            usedCells.Add(cell.Value);
        }

        int noPlacement = total - placements.Count;

        // ── Phase 1: fire comets with stagger, place specials on landing ──
        RectTransform movesRect = topHud != null ? topHud.MovesTextRect : null;
        int  placed  = placements.Count;
        bool[] done  = new bool[placed];

        for (int i = 0; i < placed; i++)
        {
            int idx = i;
            StartCoroutine(CometAndPlace(movesRect, placements[idx], () => done[idx] = true));

            if (i < placed - 1)
                yield return new WaitForSeconds(cometStagger);
        }

        // Wait for the last comet to land
        if (placed > 0)
        {
            float remaining = cometDuration + 0.06f;
            yield return new WaitForSeconds(remaining);
        }

        // Drain moves that had no target cell
        for (int i = 0; i < noPlacement; i++)
        {
            board.ConsumeBonusMove();
            yield return new WaitForSeconds(drainMoveDelay);
        }

        if (placed == 0) yield break;

        // Brief pause before mass trigger
        yield return new WaitForSeconds(preTriggerPause);

        // ── Phase 2: trigger all at once ──
        var validPlacements = new List<BoardController.BonusLinePlacement>(placed);
        foreach (var p in placements)
        {
            var tile = board.GetTileViewAt(p.x, p.y);
            if (tile == null) continue;
            var sp = tile.GetSpecial();
            if (sp == TileSpecial.LineH || sp == TileSpecial.LineV)
                validPlacements.Add(p);
        }

        if (validPlacements.Count > 0)
        {
            var routine = board.StartBonusLinesRoutine(validPlacements);
            if (routine != null) yield return routine;
        }

        // Wait for full cascade
        while (board.IsBusy || board.ActiveBackgroundJobs > 0)
            yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    private IEnumerator CometAndPlace(
        RectTransform movesRect,
        BoardController.BonusLinePlacement p,
        System.Action onDone)
    {
        var tileView = board.GetTileViewAt(p.x, p.y);
        RectTransform targetRect = tileView != null ? tileView.RectTransform : null;

        if (movesRect != null && targetRect != null && vfxOverlayRoot != null)
            yield return StartCoroutine(PlayComet(movesRect, targetRect));
        else
            yield return new WaitForSeconds(cometDuration);

        // Place special on landing
        tileView = board.GetTileViewAt(p.x, p.y);
        if (tileView != null && tileView.GetSpecial() == TileSpecial.None)
        {
            var specialType = p.isHorizontal ? TileSpecial.LineH : TileSpecial.LineV;
            tileView.SetSpecial(specialType);
            SpecialCellUtils.SyncAfterSpecialChange(board, tileView);
            tileView.PlaySpecialCreationReveal(specialType, board.TileSize);
        }

        board.ConsumeBonusMove();
        onDone?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    //  Shooting-star comet: bright white head + fading dot trail
    // ─────────────────────────────────────────────────────────────────
    private IEnumerator PlayComet(RectTransform from, RectTransform to)
    {
        if (vfxOverlayRoot == null) yield break;

        float cellSize  = board != null ? board.TileSize : 100f;
        int   dotCount  = trailDots + 1; // [0] = head, [1..N] = trail

        var rts = new RectTransform[dotCount];
        var cgs = new CanvasGroup[dotCount];

        for (int i = 0; i < dotCount; i++)
        {
            float t    = (float)i / Mathf.Max(1, dotCount - 1);
            float size = Mathf.Lerp(headSize, tailEndSize, t) * cellSize;
            rts[i] = (i == 0) ? CreateHeadDot(size) : CreateGlowDot(size);
            cgs[i] = rts[i].GetComponent<CanvasGroup>();
        }

        Vector2 startPos = WorldToLocalIn(vfxOverlayRoot, from);
        Vector2 endPos   = WorldToLocalIn(vfxOverlayRoot, to);

        // Slight arc control point
        float hDir = (endPos.x >= startPos.x) ? 1f : -1f;
        Vector2 ctrl = (startPos + endPos) * 0.5f + new Vector2(45f * hDir, 30f);

        // Ring buffer of positions for trail
        var prevPos = new Vector2[dotCount];
        for (int i = 0; i < dotCount; i++) prevPos[i] = startPos;

        float duration = Mathf.Max(0.12f, cometDuration);
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k    = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - (1f - k) * (1f - k); // ease-out → accelerates

            Vector2 pos = Bezier(startPos, ctrl, endPos, ease);

            // Shift previous positions back one slot
            for (int i = dotCount - 1; i > 0; i--)
                prevPos[i] = prevPos[i - 1];
            prevPos[0] = pos;

            // Update visuals
            for (int i = 0; i < dotCount; i++)
            {
                rts[i].anchoredPosition = prevPos[i];
                float alpha = Mathf.Lerp(1f, 0f, (float)i / (dotCount - 1));
                // quadratic fade so trail tapers quickly
                cgs[i].alpha = alpha * alpha;
            }

            yield return null;
        }

        // Snap head to exact landing position, fade all out over 1 frame
        for (int i = 0; i < dotCount; i++)
            if (rts[i] != null) Destroy(rts[i].gameObject);
    }

    // ── Head: soft radial glow circle + 4 star-spike rays ──────────
    private RectTransform CreateHeadDot(float size)
    {
        var rt = MakeGlowRect("CometHead", size, size);

        // 4 spike rays: thin rectangles at 0 / 45 / 90 / 135 degrees
        // Longer axis and shorter diagonal rays give an uneven star shape
        float longLen  = size * 1.9f;
        float shortLen = size * 1.3f;
        float width    = size * 0.14f;

        for (int i = 0; i < 4; i++)
        {
            float len   = (i % 2 == 0) ? longLen : shortLen;
            float angle = i * 45f;

            var ray = MakeGlowRect("Ray", width, len, parent: rt);
            ray.localRotation      = Quaternion.Euler(0f, 0f, angle);
            ray.anchoredPosition   = Vector2.zero;

            // Slightly dimmer than the core
            var rayCg = ray.GetComponent<CanvasGroup>();
            if (rayCg != null) rayCg.alpha = 0.75f;
        }

        return rt;
    }

    // ── Trail dot: just the soft radial circle ────────────────────
    private RectTransform CreateGlowDot(float size)
    {
        return MakeGlowRect("CometDot", size, size);
    }

    // ── Shared: create a RectTransform + Image using the glow sprite ─
    private RectTransform MakeGlowRect(string name, float w, float h, RectTransform parent = null)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        var rt  = (RectTransform)go.transform;
        rt.SetParent(parent != null ? parent : vfxOverlayRoot, worldPositionStays: false);
        rt.localScale  = Vector3.one;
        rt.anchorMin   = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot       = new Vector2(0.5f, 0.5f);
        rt.sizeDelta   = new Vector2(w, h);

        var img            = go.GetComponent<Image>();
        img.raycastTarget  = false;
        img.color          = Color.white;
        img.sprite         = GetGlowSprite();

        return rt;
    }

    // ── Cached soft radial-gradient sprite ───────────────────────
    private static Texture2D _glowTex;
    private static Sprite    _glowSprite;

    private static Sprite GetGlowSprite()
    {
        if (_glowSprite != null) return _glowSprite;

        const int size = 64;
        _glowTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };

        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float t = Mathf.Clamp01(d / c);
                // Smooth core: bright center fading to transparent edge
                float a = (1f - t) * (1f - t);
                _glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        _glowTex.Apply();

        _glowSprite = Sprite.Create(
            _glowTex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: size);

        return _glowSprite;
    }

    // ─────────────────────────────────────────────────────────────────
    private Vector2Int? PickRandomNormalCell(HashSet<Vector2Int> exclude = null)
    {
        if (board == null) return null;

        var candidates = new List<Vector2Int>();
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (board.Holes[x, y]) continue;
                if (exclude != null && exclude.Contains(new Vector2Int(x, y))) continue;

                var tile = board.GetTileViewAt(x, y);
                if (tile == null) continue;
                if (tile.GetSpecial() != TileSpecial.None) continue;
                if (board.ObstacleStateService != null &&
                    board.ObstacleStateService.IsMovableObstacleAt(x, y)) continue;

                candidates.Add(new Vector2Int(x, y));
            }
        }

        return candidates.Count == 0 ? (Vector2Int?)null
                                     : candidates[Random.Range(0, candidates.Count)];
    }

    private static Vector2 WorldToLocalIn(RectTransform root, RectTransform other)
    {
        Vector3 world = other.TransformPoint(other.rect.center);
        return root.InverseTransformPoint(world);
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }
}
