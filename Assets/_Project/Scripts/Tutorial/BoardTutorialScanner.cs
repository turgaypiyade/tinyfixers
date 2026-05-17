using UnityEngine;

public static class BoardTutorialScanner
{
    public struct Opportunity
    {
        public TileView From;
        public TileView To;
        public bool IsValid => From != null && To != null;
    }

    private static readonly Vector2Int[] Dirs =
    {
        new(1, 0), new(0, 1), new(-1, 0), new(0, -1)
    };

    public static bool TryFind(TutorialId id, BoardController board, out Opportunity opp)
    {
        opp = default;
        if (!IsBoardReady(board)) return false;

        return id switch
        {
            TutorialId.BasicMatch       => TryFindByRunLength(board, 3, false, out opp),
            TutorialId.LineHCreated     => TryFindByRunLength(board, 4, false, out opp),
            TutorialId.PatchBotCreated  => TryFindPatchBot(board, out opp),
            TutorialId.LineHActivated   => TryFindSpecialOnBoard(board, TileSpecial.LineH, out opp),
            TutorialId.PulseCoreCreated => TryFindLTShape(board, out opp),
            TutorialId.OverrideCreated  => TryFindByRunLength(board, 5, true, out opp),
            _                           => false,
        };
    }

    // ── any swap producing a run of >= minCount (straight only if requireStraight) ──

    private static bool TryFindByRunLength(BoardController board, int minCount, bool requireStraight, out Opportunity opp)
    {
        opp = default;
        int w = board.Width, h = board.Height;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!IsPlayable(board, x, y)) continue;

            foreach (var d in Dirs)
            {
                int nx = x + d.x, ny = y + d.y;
                if (!IsPlayable(board, nx, ny)) continue;

                if (WouldCreateRunGe(board, x, y, nx, ny, minCount, requireStraight))
                {
                    opp = new Opportunity { From = board.Tiles[x, y], To = board.Tiles[nx, ny] };
                    return true;
                }
            }
        }

        return false;
    }

    // ── 2×2 square detection ──

    private static bool TryFindPatchBot(BoardController board, out Opportunity opp)
    {
        opp = default;
        int w = board.Width, h = board.Height;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!IsPlayable(board, x, y)) continue;

            foreach (var d in Dirs)
            {
                int nx = x + d.x, ny = y + d.y;
                if (!IsPlayable(board, nx, ny)) continue;

                if (WouldCreate2x2(board, x, y, nx, ny))
                {
                    opp = new Opportunity { From = board.Tiles[x, y], To = board.Tiles[nx, ny] };
                    return true;
                }
            }
        }

        return false;
    }

    // ── L/T shape: both h and v arms, combined unique >= 5 ──

    private static bool TryFindLTShape(BoardController board, out Opportunity opp)
    {
        opp = default;
        int w = board.Width, h = board.Height;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!IsPlayable(board, x, y)) continue;

            foreach (var d in Dirs)
            {
                int nx = x + d.x, ny = y + d.y;
                if (!IsPlayable(board, nx, ny)) continue;

                if (WouldCreateLT(board, x, y, nx, ny) || WouldCreateLT(board, nx, ny, x, y))
                {
                    opp = new Opportunity { From = board.Tiles[x, y], To = board.Tiles[nx, ny] };
                    return true;
                }
            }
        }

        return false;
    }

    // ── special tile already on board ──

    private static bool TryFindSpecialOnBoard(BoardController board, TileSpecial special, out Opportunity opp)
    {
        opp = default;
        int w = board.Width, h = board.Height;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var tile = board.Tiles[x, y];
            if (tile == null || tile.GetSpecial() != special) continue;

            foreach (var d in Dirs)
            {
                int nx = x + d.x, ny = y + d.y;
                if (!IsPlayable(board, nx, ny)) continue;

                opp = new Opportunity { From = tile, To = board.Tiles[nx, ny] };
                return true;
            }
        }

        return false;
    }

    // ── simulation helpers ──

    private static bool WouldCreateRunGe(BoardController board, int ax, int ay, int bx, int by, int minCount, bool requireStraight)
    {
        return RunGe(board, ax, ay, ax, ay, bx, by, minCount, requireStraight)
            || RunGe(board, bx, by, ax, ay, bx, by, minCount, requireStraight);
    }

    private static bool RunGe(BoardController board, int cx, int cy, int ax, int ay, int bx, int by, int minCount, bool requireStraight)
    {
        if (!TryType(board, cx, cy, ax, ay, bx, by, out var t)) return false;

        int h = 1 + Count(board, cx, cy, -1, 0, t, ax, ay, bx, by)
                  + Count(board, cx, cy, +1, 0, t, ax, ay, bx, by);
        int v = 1 + Count(board, cx, cy, 0, -1, t, ax, ay, bx, by)
                  + Count(board, cx, cy, 0, +1, t, ax, ay, bx, by);

        if (requireStraight)
            return h >= minCount || v >= minCount;

        return Mathf.Max(h, v) >= minCount;
    }

    private static bool WouldCreate2x2(BoardController board, int ax, int ay, int bx, int by)
    {
        return Check2x2Near(board, ax, ay, ax, ay, bx, by)
            || Check2x2Near(board, bx, by, ax, ay, bx, by);
    }

    private static bool Check2x2Near(BoardController board, int px, int py, int ax, int ay, int bx, int by)
    {
        for (int ox = -1; ox <= 0; ox++)
        for (int oy = -1; oy <= 0; oy++)
        {
            int sx = px + ox, sy = py + oy;
            if (sx < 0 || sx >= board.Width - 1 || sy < 0 || sy >= board.Height - 1) continue;

            if (!TryType(board, sx,     sy,     ax, ay, bx, by, out var t)) continue;
            if (!TryType(board, sx + 1, sy,     ax, ay, bx, by, out var t2) || t2 != t) continue;
            if (!TryType(board, sx,     sy + 1, ax, ay, bx, by, out var t3) || t3 != t) continue;
            if (!TryType(board, sx + 1, sy + 1, ax, ay, bx, by, out var t4) || t4 != t) continue;

            return true;
        }

        return false;
    }

    private static bool WouldCreateLT(BoardController board, int cx, int cy, int ox, int oy)
    {
        if (!TryType(board, cx, cy, cx, cy, ox, oy, out var t)) return false;

        int hl = Count(board, cx, cy, -1, 0, t, cx, cy, ox, oy);
        int hr = Count(board, cx, cy, +1, 0, t, cx, cy, ox, oy);
        int vd = Count(board, cx, cy, 0, -1, t, cx, cy, ox, oy);
        int vu = Count(board, cx, cy, 0, +1, t, cx, cy, ox, oy);

        int hTotal = 1 + hl + hr;
        int vTotal = 1 + vd + vu;

        // L/T: her iki eksenin de en az 2 taşı var ve toplam unique taş >= 5
        return hTotal >= 2 && vTotal >= 2 && (hTotal + vTotal - 1) >= 5;
    }

    private static int Count(BoardController board, int sx, int sy, int dx, int dy, TileType t, int ax, int ay, int bx, int by)
    {
        int count = 0;
        int x = sx + dx, y = sy + dy;

        while (TryType(board, x, y, ax, ay, bx, by, out var found) && found == t)
        {
            count++;
            x += dx;
            y += dy;
        }

        return count;
    }

    private static bool TryType(BoardController board, int x, int y, int ax, int ay, int bx, int by, out TileType type)
    {
        type = default;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return false;
        if (board.Holes[x, y]) return false;

        // swap sonrası hangi hücreden okuyacağız
        int rx = x, ry = y;
        if (x == ax && y == ay) { rx = bx; ry = by; }
        else if (x == bx && y == by) { rx = ax; ry = ay; }

        if (board.ObstacleStateService != null)
        {
            if (board.ObstacleStateService.IsMovableObstacleAt(rx, ry)) return false;
            if (board.ObstacleStateService.IsInteractionLockedAt(rx, ry)) return false;
        }

        var tile = board.Tiles[rx, ry];
        if (tile == null || tile.GetSpecial() != TileSpecial.None) return false;

        type = tile.GetTileType();
        return true;
    }

    private static bool IsPlayable(BoardController board, int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return false;
        if (board.Holes[x, y]) return false;
        if (board.Tiles[x, y] == null) return false;

        if (board.ObstacleStateService != null)
        {
            if (board.ObstacleStateService.IsMovableObstacleAt(x, y)) return false;
            if (board.ObstacleStateService.IsInteractionLockedAt(x, y)) return false;
        }

        return true;
    }

    private static bool IsBoardReady(BoardController board)
    {
        return board != null && board.isActiveAndEnabled
            && !board.IsBusy && !board.InputLocked
            && board.Tiles != null && board.Holes != null
            && board.Width > 0 && board.Height > 0;
    }
}
