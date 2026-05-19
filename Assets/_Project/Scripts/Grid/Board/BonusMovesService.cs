using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bonus round when the player wins with remaining moves.
/// Phase 1: Each remaining move fires a shooting-star comet; on landing, LineV/LineH is placed.
/// Phase 2: All placed specials trigger simultaneously.
///
/// Single click does nothing from LevelEndSimplePopupController.
/// Double click calls RequestHardSkip(), which stops this flow immediately and lets the success popup open.
/// </summary>
public class BonusMovesService : MonoBehaviour
{
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;
    [SerializeField] private RectTransform vfxOverlayRoot;

    [Header("Timing")]
    [SerializeField] private float cometDuration = 0.28f;  // travel time per comet
    [SerializeField] private float cometStagger = 0.10f;  // delay between comet starts
    [SerializeField] private float preTriggerPause = 0.18f; // pause before mass trigger
    [SerializeField] private float drainMoveDelay = 0.04f;  // when no cells available

    [Header("Comet Visual")]
    [SerializeField] private int trailDots = 7;
    [SerializeField] private float headSize = 0.38f; // fraction of tileSize
    [SerializeField] private float tailEndSize = 0.07f; // smallest tail dot fraction

    // -------------------------------------------------------------------------
    private bool _skipRequested;      // soft skip: skips comet waiting, still runs bonus effects
    private bool _hardSkipRequested;  // hard skip: exits the whole bonus round
    private readonly List<GameObject> spawnedCometVfx = new();

    public bool HardSkipRequested => _hardSkipRequested;

    public void RequestSkip() => _skipRequested = true;

    public void RequestHardSkip()
    {
        _hardSkipRequested = true;
        _skipRequested = true;
        ClearSpawnedCometVfx();
    }

    // -------------------------------------------------------------------------
    public IEnumerator RunBonusRound()
    {
        _skipRequested = false;
        _hardSkipRequested = false;
        ClearSpawnedCometVfx();

        if (board == null)
            yield break;

        int total = board.RemainingMoves;
        if (total <= 0)
            yield break;

        // Pre-select target cells. Track used cells to avoid duplicates.
        var placements = new List<BoardController.BonusLinePlacement>(total);
        var usedCells = new HashSet<Vector2Int>();

        for (int i = 0; i < total; i++)
        {
            if (_hardSkipRequested)
                yield break;

            var cell = PickRandomNormalCell(usedCells);
            if (!cell.HasValue)
                break;

            bool isH = Random.value > 0.5f;
            placements.Add(new BoardController.BonusLinePlacement(cell.Value.x, cell.Value.y, isH));
            usedCells.Add(cell.Value);
        }

        int noPlacement = total - placements.Count;

        // Phase 1: fire comets with stagger, place specials on landing.
        RectTransform movesRect = topHud != null ? topHud.MovesTextRect : null;
        int placed = placements.Count;
        bool[] done = new bool[placed];

        for (int i = 0; i < placed; i++)
        {
            if (_hardSkipRequested)
                yield break;

            int idx = i;
            if (!_skipRequested)
            {
                StartCoroutine(CometAndPlace(movesRect, placements[idx], () => done[idx] = true));

                if (i < placed - 1)
                    yield return StartCoroutine(InterruptibleWait(cometStagger));
            }
            else
            {
                PlaceSpecialInstant(placements[idx]);
                done[idx] = true;
            }
        }

        if (_hardSkipRequested)
            yield break;

        // Wait for the last comet to land. Soft-skip places unfinished specials instantly.
        if (placed > 0)
        {
            float waitTime = cometDuration + 0.06f;
            float elapsed = 0f;

            while (elapsed < waitTime && !_skipRequested && !_hardSkipRequested)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_hardSkipRequested)
                yield break;

            if (_skipRequested)
            {
                for (int i = 0; i < placed; i++)
                {
                    if (!done[i])
                    {
                        PlaceSpecialInstant(placements[i]);
                        done[i] = true;
                    }
                }
            }

            // The fixed wait above is only a visual minimum. On device, coroutine resume
            // order can differ by a frame; do not build validPlacements until every
            // CometAndPlace has actually written its special to the board.
            while (!_hardSkipRequested && !AreAllCometsDone(done, placed))
                yield return null;
        }

        if (_hardSkipRequested)
            yield break;

        // Drain moves that had no target cell.
        for (int i = 0; i < noPlacement; i++)
        {
            if (_hardSkipRequested)
                yield break;

            board.ConsumeBonusMove();

            if (!_skipRequested)
                yield return StartCoroutine(InterruptibleWait(drainMoveDelay));
        }

        if (_hardSkipRequested || placed == 0)
            yield break;

        // Brief pause before mass trigger.
        if (!_skipRequested)
        {
            yield return StartCoroutine(InterruptibleWait(preTriggerPause));
            yield return StartCoroutine(WaitForPlacedSpecialReveals(placements));
        }

        if (_hardSkipRequested)
            yield break;

        // Phase 2: trigger all placed specials together using the same queued
        // LineV/LineH activation path as OverrideSpecialized line batches.
        var validPlacements = new List<BoardController.BonusLinePlacement>(placed);
        foreach (var p in placements)
        {
            if (_hardSkipRequested)
                yield break;

            var tile = board.GetTileViewAt(p.x, p.y);
            if (tile == null)
                continue;

            var sp = tile.GetSpecial();
            if (sp == TileSpecial.LineH || sp == TileSpecial.LineV)
                validPlacements.Add(p);
        }

        if (_hardSkipRequested)
            yield break;

        if (validPlacements.Count > 0)
        {
            yield return BonusLineOverrideStyleRunner.Run(
                board,
                validPlacements,
                () => _hardSkipRequested);

            while (!_hardSkipRequested && (board.IsBusy || board.ActiveBackgroundJobs > 0))
                yield return null;
        }

        if (_hardSkipRequested)
            yield break;

        // Wait for full cascade, but allow hard-skip to return immediately.
        while ((board.IsBusy || board.ActiveBackgroundJobs > 0) && !_hardSkipRequested)
            yield return null;
    }

    // Instant placement without comet visual. Used in soft skip mode.
    private void PlaceSpecialInstant(BoardController.BonusLinePlacement p)
    {
        if (_hardSkipRequested)
            return;

        var tileView = board.GetTileViewAt(p.x, p.y);
        if (tileView != null && tileView.GetSpecial() == TileSpecial.None)
        {
            var specialType = p.isHorizontal ? TileSpecial.LineH : TileSpecial.LineV;
            tileView.SetSpecial(specialType);
            SpecialCellUtils.SyncAfterSpecialChange(board, tileView);
        }

        board.ConsumeBonusMove();
    }

    // -------------------------------------------------------------------------
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
            yield return StartCoroutine(InterruptibleWait(cometDuration));

        if (_hardSkipRequested)
        {
            onDone?.Invoke();
            yield break;
        }

        // Place special on landing.
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

    private IEnumerator InterruptibleWait(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < seconds && !_hardSkipRequested)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForPlacedSpecialReveals(List<BoardController.BonusLinePlacement> placements)
    {
        const float maxWaitSeconds = 0.45f;
        float elapsed = 0f;

        while (!_hardSkipRequested && elapsed < maxWaitSeconds && HasActivePlacedSpecialReveal(placements))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool HasActivePlacedSpecialReveal(List<BoardController.BonusLinePlacement> placements)
    {
        if (placements == null || board == null)
            return false;

        for (int i = 0; i < placements.Count; i++)
        {
            var p = placements[i];
            var tile = board.GetTileViewAt(p.x, p.y);

            if (tile != null && tile.IsSpecialCreationRevealPlaying)
                return true;
        }

        return false;
    }

    private static bool AreAllCometsDone(bool[] done, int count)
    {
        if (done == null)
            return true;

        int limit = Mathf.Min(count, done.Length);
        for (int i = 0; i < limit; i++)
        {
            if (!done[i])
                return false;
        }

        return true;
    }

    private IEnumerator RunSkippableRoutine(IEnumerator routine)
    {
        bool done = false;
        Coroutine running = StartCoroutine(RunRoutineAndMarkDone(routine, () => done = true));

        while (!done && !_hardSkipRequested)
            yield return null;

        if (_hardSkipRequested && running != null)
            StopCoroutine(running);
    }

    private IEnumerator RunRoutineAndMarkDone(IEnumerator routine, System.Action onDone)
    {
        yield return routine;
        onDone?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Shooting-star comet: bright white head + fading dot trail.
    // -------------------------------------------------------------------------
    private IEnumerator PlayComet(RectTransform from, RectTransform to)
    {
        if (vfxOverlayRoot == null)
            yield break;

        float cellSize = board != null ? board.TileSize : 100f;
        int dotCount = trailDots + 1; // [0] = head, [1..N] = trail

        var rts = new RectTransform[dotCount];
        var cgs = new CanvasGroup[dotCount];

        for (int i = 0; i < dotCount; i++)
        {
            float t = (float)i / Mathf.Max(1, dotCount - 1);
            float size = Mathf.Lerp(headSize, tailEndSize, t) * cellSize;
            rts[i] = (i == 0) ? CreateHeadDot(size) : CreateGlowDot(size);
            cgs[i] = rts[i].GetComponent<CanvasGroup>();
        }

        Vector2 startPos = WorldToLocalIn(vfxOverlayRoot, from);
        Vector2 endPos = WorldToLocalIn(vfxOverlayRoot, to);

        // Slight arc control point.
        float hDir = (endPos.x >= startPos.x) ? 1f : -1f;
        Vector2 ctrl = (startPos + endPos) * 0.5f + new Vector2(45f * hDir, 30f);

        // Ring buffer of positions for trail.
        var prevPos = new Vector2[dotCount];
        for (int i = 0; i < dotCount; i++)
            prevPos[i] = startPos;

        float duration = Mathf.Max(0.12f, cometDuration);
        float elapsed = 0f;

        while (elapsed < duration && !_hardSkipRequested)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - (1f - k) * (1f - k); // ease-out

            Vector2 pos = Bezier(startPos, ctrl, endPos, ease);

            for (int i = dotCount - 1; i > 0; i--)
                prevPos[i] = prevPos[i - 1];
            prevPos[0] = pos;

            for (int i = 0; i < dotCount; i++)
            {
                if (rts[i] == null)
                    continue;

                rts[i].anchoredPosition = prevPos[i];
                float alpha = Mathf.Lerp(1f, 0f, (float)i / (dotCount - 1));
                cgs[i].alpha = alpha * alpha;
            }

            yield return null;
        }

        for (int i = 0; i < dotCount; i++)
        {
            if (rts[i] != null)
            {
                spawnedCometVfx.Remove(rts[i].gameObject);
                Destroy(rts[i].gameObject);
            }
        }

        spawnedCometVfx.RemoveAll(go => go == null);
    }

    // Head: soft radial glow circle + 4 star-spike rays.
    private RectTransform CreateHeadDot(float size)
    {
        var rt = MakeGlowRect("CometHead", size, size);

        float longLen = size * 1.9f;
        float shortLen = size * 1.3f;
        float width = size * 0.14f;

        for (int i = 0; i < 4; i++)
        {
            float len = (i % 2 == 0) ? longLen : shortLen;
            float angle = i * 45f;

            var ray = MakeGlowRect("Ray", width, len, parent: rt);
            ray.localRotation = Quaternion.Euler(0f, 0f, angle);
            ray.anchoredPosition = Vector2.zero;

            var rayCg = ray.GetComponent<CanvasGroup>();
            if (rayCg != null)
                rayCg.alpha = 0.75f;
        }

        return rt;
    }

    private RectTransform CreateGlowDot(float size)
    {
        return MakeGlowRect("CometDot", size, size);
    }

    private RectTransform MakeGlowRect(string name, float w, float h, RectTransform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        spawnedCometVfx.Add(go);

        var rt = (RectTransform)go.transform;
        rt.SetParent(parent != null ? parent : vfxOverlayRoot, worldPositionStays: false);
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = Color.white;
        img.sprite = GetGlowSprite();

        return rt;
    }

    private void ClearSpawnedCometVfx()
    {
        for (int i = 0; i < spawnedCometVfx.Count; i++)
        {
            if (spawnedCometVfx[i] != null)
                Destroy(spawnedCometVfx[i]);
        }

        spawnedCometVfx.Clear();
    }

    // Cached soft radial-gradient sprite.
    private static Texture2D _glowTex;
    private static Sprite _glowSprite;

    private static Sprite GetGlowSprite()
    {
        if (_glowSprite != null)
            return _glowSprite;

        const int size = 64;
        _glowTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float t = Mathf.Clamp01(d / c);
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

    // -------------------------------------------------------------------------
    private Vector2Int? PickRandomNormalCell(HashSet<Vector2Int> exclude = null)
    {
        if (board == null)
            return null;

        var candidates = new List<Vector2Int>();
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (board.Holes[x, y])
                    continue;

                if (exclude != null && exclude.Contains(new Vector2Int(x, y)))
                    continue;

                var tile = board.GetTileViewAt(x, y);
                if (tile == null)
                    continue;

                if (tile.GetSpecial() != TileSpecial.None)
                    continue;

                if (board.ObstacleStateService != null &&
                    board.ObstacleStateService.IsMovableObstacleAt(x, y))
                    continue;

                candidates.Add(new Vector2Int(x, y));
            }
        }

        return candidates.Count == 0
            ? (Vector2Int?)null
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
