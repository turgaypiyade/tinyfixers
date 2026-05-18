using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class BoardIdleHintAndComboGlowController : MonoBehaviour
{
    private const float IdleHintDelay = 5f;
    private const float HintStepDuration = 0.10f;
    private const int HintLoopCount = 2;
    private const float BoardScanInterval = 0.5f;
    private const float GlowRefreshInterval = 0.04f;
    private const string GlowObjectName = "SpecialComboGlow";

    private static BoardIdleHintAndComboGlowController instance;
    private static Sprite glowSprite;

    private readonly List<BoardController> boards = new();
    private readonly Dictionary<BoardController, BoardFxState> states = new();

    private float nextBoardScanTime;

    private sealed class BoardFxState
    {
        public float idleTimer;
        public float nextGlowRefreshTime;
        public int lastHintStartIndex;
        public Coroutine hintRoutine;
        public TileView hintedTile;
        public TileView hintedOtherTile;
        public readonly HashSet<TileView> glowingTiles = new();
        public readonly HashSet<TileView> nextGlowingTiles = new();
    }

    private struct HintCandidate
    {
        public readonly TileView tile;
        public readonly TileView otherTile;
        public readonly Vector2Int direction;

        public HintCandidate(TileView tile, TileView otherTile, Vector2Int direction)
        {
            this.tile = tile;
            this.otherTile = otherTile;
            this.direction = direction;
        }
    }

    private static readonly Vector2Int[] HintDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 0),
        new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] ComboDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(0, 1)
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        var go = new GameObject("BoardIdleHintAndComboGlowController");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<BoardIdleHintAndComboGlowController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        RefreshBoards();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextBoardScanTime)
            RefreshBoards();

        bool pointerDown = PointerDownThisFrame();

        for (int i = 0; i < boards.Count; i++)
        {
            var board = boards[i];
            if (board == null) continue;

            var state = GetState(board);
            TickBoard(board, state, pointerDown);
        }
    }

    private void RefreshBoards()
    {
        nextBoardScanTime = Time.unscaledTime + BoardScanInterval;
        boards.Clear();

        var foundBoards = FindObjectsOfType<BoardController>();
        for (int i = 0; i < foundBoards.Length; i++)
        {
            if (foundBoards[i] != null && foundBoards[i].isActiveAndEnabled)
                boards.Add(foundBoards[i]);
        }
    }

    private BoardFxState GetState(BoardController board)
    {
        if (states.TryGetValue(board, out var state))
            return state;

        state = new BoardFxState();
        states[board] = state;
        return state;
    }

    private void TickBoard(BoardController board, BoardFxState state, bool pointerDown)
    {
        if (!IsBoardReadyForFx(board))
        {
            state.idleTimer = 0f;
            CancelHint(board, state);
            HideAllGlow(state);
            return;
        }

        RefreshSpecialComboGlows(board, state);

        if (pointerDown)
        {
            state.idleTimer = 0f;
            CancelHint(board, state);
            return;
        }

        if (state.hintRoutine != null)
            return;

        state.idleTimer += Time.deltaTime;

        if (state.idleTimer < IdleHintDelay)
            return;

        if (TryFindPossibleMatchMove(board, state, out var candidate))
        {
            state.hintRoutine = StartCoroutine(
                PlayHintRoutine(board, state, candidate.tile, candidate.otherTile)
            );
        }
        else
        {
            state.idleTimer = IdleHintDelay * 0.5f;
        }
    }

    private static bool IsBoardReadyForFx(BoardController board)
    {
        if (board == null || !board.isActiveAndEnabled) return false;
        if (board.IsBusy || board.InputLocked) return false;
        if (board.Tiles == null || board.Holes == null) return false;

        return board.Width > 0 && board.Height > 0 && board.TileSize > 0;
    }

    private static bool PointerDownThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.press.wasPressedThisFrame)
                    return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
    if (UnityEngine.Input.GetMouseButtonDown(0))
        return true;

    for (int i = 0; i < UnityEngine.Input.touchCount; i++)
    {
        if (UnityEngine.Input.GetTouch(i).phase == TouchPhase.Began)
            return true;
    }
#endif

        return false;
    }

    private IEnumerator PlayHintRoutine(
      BoardController board,
      BoardFxState state,
      TileView tile,
      TileView otherTile)
    {
        state.hintedTile = tile;
        state.hintedOtherTile = otherTile;

        if (board == null || tile == null || otherTile == null)
        {
            FinishHint(state);
            yield break;
        }

        var rtA = tile.RectTransform;
        var rtB = otherTile.RectTransform;

        if (rtA == null || rtB == null)
        {
            FinishHint(state);
            yield break;
        }

        Vector2 originA = new Vector2(tile.X * board.TileSize, -tile.Y * board.TileSize);
        Vector2 originB = new Vector2(otherTile.X * board.TileSize, -otherTile.Y * board.TileSize);

        rtA.anchoredPosition = originA;
        rtB.anchoredPosition = originB;

        float swapPreviewDuration = 0.18f;
        float holdDuration = 0.10f;
        float returnDuration = 0.18f;

        if (!IsBoardReadyForFx(board))
        {
            CancelHint(board, state);
            yield break;
        }

        // 1) Görsel olarak yer değiştir
        yield return MoveTwoRects(
            rtA, originA, originB,
            rtB, originB, originA,
            swapPreviewDuration
        );

        // 2) Çok kısa orada bekle, oyuncu match'i görsün
        float holdTimer = 0f;
        while (holdTimer < holdDuration)
        {
            if (!IsBoardReadyForFx(board) || tile == null || otherTile == null)
            {
                CancelHint(board, state);
                yield break;
            }

            holdTimer += Time.deltaTime;
            yield return null;
        }

        // 3) Eski görsel pozisyonlarına dön
        yield return MoveTwoRects(
            rtA, originB, originA,
            rtB, originA, originB,
            returnDuration
        );

        if (tile != null)
            tile.SnapToGrid(board.TileSize);

        if (otherTile != null)
            otherTile.SnapToGrid(board.TileSize);

        FinishHint(state);
    }

    private static IEnumerator MoveTwoRects(
    RectTransform a,
    Vector2 aFrom,
    Vector2 aTo,
    RectTransform b,
    Vector2 bFrom,
    Vector2 bTo,
    float duration)
    {
        float t = 0f;

        while (t < 1f)
        {
            if (a == null || b == null)
                yield break;

            t += Time.deltaTime / Mathf.Max(0.0001f, duration);

            float k = Mathf.Clamp01(t);
            k = k * k * (3f - 2f * k); // smoothstep

            a.anchoredPosition = Vector2.LerpUnclamped(aFrom, aTo, k);
            b.anchoredPosition = Vector2.LerpUnclamped(bFrom, bTo, k);

            yield return null;
        }

        if (a != null) a.anchoredPosition = aTo;
        if (b != null) b.anchoredPosition = bTo;
    }
    private static IEnumerator MoveRect(RectTransform rt, Vector2 from, Vector2 to, float duration)
    {
        float t = 0f;

        while (t < 1f)
        {
            if (rt == null) yield break;

            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float k = Mathf.Clamp01(t);
            k = 1f - Mathf.Pow(1f - k, 3f);

            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            yield return null;
        }

        if (rt != null)
            rt.anchoredPosition = to;
    }

    public static void CancelHintsForBoard(BoardController board)
    {
        if (instance == null || board == null) return;
        if (instance.states.TryGetValue(board, out var state))
            instance.CancelHint(board, state);
    }

    private void CancelHint(BoardController board, BoardFxState state)
    {
        if (state.hintRoutine != null)
        {
            StopCoroutine(state.hintRoutine);
            state.hintRoutine = null;
        }

        if (board != null && state.hintedTile != null)
            state.hintedTile.SnapToGrid(board.TileSize);

        if (board != null && state.hintedOtherTile != null)
            state.hintedOtherTile.SnapToGrid(board.TileSize);

        state.hintedTile = null;
        state.hintedOtherTile = null;
    }

    private static void FinishHint(BoardFxState state)
    {
        state.hintRoutine = null;
        state.hintedTile = null;
        state.hintedOtherTile = null;
        state.idleTimer = 0f;
    }
    private static bool TryFindPossibleMatchMove(
        BoardController board,
        BoardFxState state,
        out HintCandidate candidate)
    {
        candidate = default;

        int cellCount = board.Width * board.Height;
        if (cellCount <= 0) return false;

        int startIndex = Mathf.Abs(state.lastHintStartIndex) % cellCount;

        for (int step = 0; step < cellCount; step++)
        {
            int index = (startIndex + step) % cellCount;
            int x = index % board.Width;
            int y = index / board.Width;

            if (!IsPlayableTile(board, x, y))
                continue;

            for (int i = 0; i < HintDirections.Length; i++)
            {
                Vector2Int dir = HintDirections[i];
                int nx = x + dir.x;
                int ny = y + dir.y;

                if (!IsPlayableTile(board, nx, ny))
                    continue;

                if (!WouldSwapCreateMatch(board, x, y, nx, ny))
                    continue;

                state.lastHintStartIndex = (index + 1) % cellCount;
                candidate = new HintCandidate(board.Tiles[x, y], board.Tiles[nx, ny], dir);
                return true;
            }
        }

        return false;
    }

    private static bool IsPlayableTile(BoardController board, int x, int y)
    {
        if (board == null || x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (board.Holes[x, y])
            return false;

        if (board.Tiles[x, y] == null)
            return false;

        // Movable obstacle tile'ları normal match/swap hint'ine dahil etme (MatchFinder ile tutarlı)
        if (board.ObstacleStateService != null && board.ObstacleStateService.IsMovableObstacleAt(x, y))
            return false;

        // Over-tile (locksInteraction) obstacle altındaki tile'lar swap edilemez — hint'e alma
        if (board.ObstacleStateService != null && board.ObstacleStateService.IsInteractionLockedAt(x, y))
            return false;

        return true;
    }

    private static bool WouldSwapCreateMatch(BoardController board, int ax, int ay, int bx, int by)
    {
        var a = board.Tiles[ax, ay];
        var b = board.Tiles[bx, by];

        if (a == null || b == null)
            return false;

        // Special + special combo ayrı glow ile gösterilecek.
        if (a.GetSpecial() != TileSpecial.None && b.GetSpecial() != TileSpecial.None)
            return false;

        if (HasLineMatchAtAfterSwap(board, ax, ay, ax, ay, bx, by))
            return true;

        if (HasLineMatchAtAfterSwap(board, bx, by, ax, ay, bx, by))
            return true;

        return Has2x2MatchNearAfterSwap(board, ax, ay, ax, ay, bx, by)
            || Has2x2MatchNearAfterSwap(board, bx, by, ax, ay, bx, by);
    }

    private static bool HasLineMatchAtAfterSwap(
        BoardController board,
        int x,
        int y,
        int ax,
        int ay,
        int bx,
        int by)
    {
        if (!TryGetTypeAfterSwap(board, x, y, ax, ay, bx, by, out var type))
            return false;

        int horizontal = 1
            + CountSameTypeAfterSwap(board, x, y, -1, 0, type, ax, ay, bx, by)
            + CountSameTypeAfterSwap(board, x, y, 1, 0, type, ax, ay, bx, by);

        if (horizontal >= 3)
            return true;

        int vertical = 1
            + CountSameTypeAfterSwap(board, x, y, 0, -1, type, ax, ay, bx, by)
            + CountSameTypeAfterSwap(board, x, y, 0, 1, type, ax, ay, bx, by);

        return vertical >= 3;
    }

    private static int CountSameTypeAfterSwap(
        BoardController board,
        int startX,
        int startY,
        int dx,
        int dy,
        TileType type,
        int ax,
        int ay,
        int bx,
        int by)
    {
        int count = 0;
        int x = startX + dx;
        int y = startY + dy;

        while (TryGetTypeAfterSwap(board, x, y, ax, ay, bx, by, out var otherType)
               && otherType.Equals(type))
        {
            count++;
            x += dx;
            y += dy;
        }

        return count;
    }

    private static bool Has2x2MatchNearAfterSwap(
        BoardController board,
        int x,
        int y,
        int ax,
        int ay,
        int bx,
        int by)
    {
        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = x + ox;
                int sy = y + oy;

                if (sx < 0 || sx >= board.Width - 1 || sy < 0 || sy >= board.Height - 1)
                    continue;

                if (!TryGetTypeAfterSwap(board, sx, sy, ax, ay, bx, by, out var type))
                    continue;

                if (!TryGetTypeAfterSwap(board, sx + 1, sy, ax, ay, bx, by, out var bType)
                    || !bType.Equals(type))
                    continue;

                if (!TryGetTypeAfterSwap(board, sx, sy + 1, ax, ay, bx, by, out var cType)
                    || !cType.Equals(type))
                    continue;

                if (!TryGetTypeAfterSwap(board, sx + 1, sy + 1, ax, ay, bx, by, out var dType)
                    || !dType.Equals(type))
                    continue;

                return true;
            }
        }

        return false;
    }

    private static bool TryGetTypeAfterSwap(
        BoardController board,
        int x,
        int y,
        int ax,
        int ay,
        int bx,
        int by,
        out TileType type)
    {
        type = default;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (board.Holes[x, y])
            return false;

        // Swap sonrası gerçek hücreyi belirle
        int realX = x, realY = y;
        if (x == ax && y == ay) { realX = bx; realY = by; }
        else if (x == bx && y == by) { realX = ax; realY = ay; }

        // Hedef hücre movable veya interaction-locked obstacle ise match'e sayma
        if (board.ObstacleStateService != null && board.ObstacleStateService.IsMovableObstacleAt(realX, realY))
            return false;
        if (board.ObstacleStateService != null && board.ObstacleStateService.IsInteractionLockedAt(realX, realY))
            return false;

        TileView tile;
        if (x == ax && y == ay)
            tile = board.Tiles[bx, by];
        else if (x == bx && y == by)
            tile = board.Tiles[ax, ay];
        else
            tile = board.Tiles[x, y];

        if (tile == null)
            return false;

        type = tile.GetTileType();
        return true;
    }

    private void RefreshSpecialComboGlows(BoardController board, BoardFxState state)
    {
        if (Time.unscaledTime < state.nextGlowRefreshTime)
            return;

        state.nextGlowRefreshTime = Time.unscaledTime + GlowRefreshInterval;
        state.nextGlowingTiles.Clear();

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (!IsPlayableTile(board, x, y))
                    continue;

                var tile = board.Tiles[x, y];

                if (!IsComboSpecial(tile))
                    continue;

                for (int i = 0; i < ComboDirections.Length; i++)
                {
                    var dir = ComboDirections[i];

                    int nx = x + dir.x;
                    int ny = y + dir.y;

                    if (!IsPlayableTile(board, nx, ny))
                        continue;

                    var neighbor = board.Tiles[nx, ny];

                    if (!CanSpecialCombo(tile, neighbor))
                        continue;

                    state.nextGlowingTiles.Add(tile);
                    state.nextGlowingTiles.Add(neighbor);
                }
            }
        }

        foreach (var oldTile in state.glowingTiles)
        {
            if (oldTile != null && !state.nextGlowingTiles.Contains(oldTile))
                SetComboGlowActive(oldTile, false, board.TileSize);
        }

        foreach (var newTile in state.nextGlowingTiles)
        {
            if (newTile != null)
                SetComboGlowActive(newTile, true, board.TileSize);
        }

        state.glowingTiles.Clear();

        foreach (var tile in state.nextGlowingTiles)
            state.glowingTiles.Add(tile);
    }

    private static bool IsComboSpecial(TileView tile)
    {
        return tile != null && tile.GetSpecial() != TileSpecial.None;
    }

    private static bool CanSpecialCombo(TileView a, TileView b)
    {
        return IsComboSpecial(a) && IsComboSpecial(b);
    }

    private static void HideAllGlow(BoardFxState state)
    {
        foreach (var tile in state.glowingTiles)
        {
            if (tile != null)
                SetComboGlowActive(tile, false, 0);
        }

        state.glowingTiles.Clear();
        state.nextGlowingTiles.Clear();
    }

    private static void SetComboGlowActive(TileView tile, bool active, int tileSize)
    {
        if (tile == null || tile.RectTransform == null)
            return;

        var image = EnsureGlowImage(tile, tileSize);

        if (image == null)
            return;

        image.gameObject.SetActive(active);

        if (!active)
            return;

        UpdateGlowVisual(image, tileSize > 0 ? tileSize : Mathf.RoundToInt(tile.RectTransform.rect.width));
    }

    private static Image EnsureGlowImage(TileView tile, int tileSize)
    {
        var parent = tile.RectTransform;

        if (parent == null)
            return null;

        Transform existing = parent.Find(GlowObjectName);

        RectTransform glowRt;
        Image image;

        if (existing == null)
        {
            var go = new GameObject(GlowObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glowRt = go.GetComponent<RectTransform>();
            glowRt.SetParent(parent, false);

            image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = GetGlowSprite();
            image.type = Image.Type.Simple;
        }
        else
        {
            glowRt = existing as RectTransform;
            image = existing.GetComponent<Image>();
        }

        if (glowRt == null || image == null)
            return null;

        glowRt.anchorMin = new Vector2(0.5f, 0.5f);
        glowRt.anchorMax = new Vector2(0.5f, 0.5f);
        glowRt.pivot = new Vector2(0.5f, 0.5f);
        glowRt.anchoredPosition = Vector2.zero;
        glowRt.localRotation = Quaternion.identity;
        glowRt.SetAsFirstSibling();

        int resolvedSize = tileSize > 0 ? tileSize : Mathf.RoundToInt(parent.rect.width);
        if (resolvedSize <= 0)
            resolvedSize = 96;

        glowRt.sizeDelta = Vector2.one * resolvedSize * 1.95f;

        return image;
    }

    private static void UpdateGlowVisual(Image image, int tileSize)
    {
        if (image == null)
            return;

        // Sürekli hafif pulse
        float softPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.5f);

        // Yaklaşık 2 saniyede bir daha güçlü flare
        float flareCycle = Mathf.Repeat(Time.unscaledTime, 2f) / 2f;
        float flare = Mathf.Exp(-Mathf.Pow((flareCycle - 0.08f) * 8f, 2f));

        float alpha = Mathf.Clamp01(
            Mathf.Lerp(0.65f, 0.95f, softPulse) + flare * 0.35f
        );

        float scale = Mathf.Lerp(1.12f, 1.32f, softPulse) + flare * 0.22f;

        // Daha sıcak, daha görünür sarı-beyaz glow
        image.color = new Color(1f, 0.92f, 0.20f, alpha);
        image.rectTransform.localScale = Vector3.one * scale;

        if (tileSize > 0)
            image.rectTransform.sizeDelta = Vector2.one * tileSize * 1.95f;
    }

    private static Sprite GetGlowSprite()
    {
        if (glowSprite != null)
            return glowSprite;

        const int size = 96;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GeneratedSpecialComboGlow";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        var pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxRadius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * alpha;

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        glowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );

        glowSprite.name = "GeneratedSpecialComboGlowSprite";
        return glowSprite;
    }
}