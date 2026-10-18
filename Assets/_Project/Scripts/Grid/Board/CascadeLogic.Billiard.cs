using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bilardo topu yuvarlanması (Docs/BilliardBall_Plan.md kural 1–5, 8). Planlayıcının İLK adımıdır:
/// dinlenen her top, taşlar planlanmadan ÖNCE boş komşu hücreye yuvarlanır (kural 4). Öncelik:
/// aşağı (yerçekimi — sim yürütür) → çapraz aşağı → yana; yukarı asla. Eşitlikte cebe yakın taraf.
/// Yana yuvarlanan top, aynı yönde sürer (geri dönmez) — havadayken de: yoldaki topun önünde hücre boşalırsa
/// (temizlenen taşlar hücreleri tek tek bırakır) top o hücreyi de taşlardan önce alır, yolu uzar. Taşların düşüş
/// kuralları değişmez; uzatma, havadaki taşı yeniden hedefleyen mevcut motorla akar. Yol, topun VirtualTile.Path'ine yazılır;
/// görseli diğer taşlar gibi düşüş motoru sürer. Cebe varan top plandan çıkar, verisi toplanır
/// (hedef +1) ve görseli BilliardBallService'e devredilir.
/// </summary>
public partial class CascadeLogic
{
    private readonly List<(TileView view, Vector2Int[] path)> pocketedBallsThisPlan = new();
    private readonly List<Vector2Int> ballCellsBuffer = new();

    private void RollBilliardBalls(VirtualTile[,] vb)
    {
        var obstacles = board.ObstacleStateService;
        var billiard = board.Billiard;
        if (obstacles == null || billiard == null || !billiard.HasBalls)
            return;

        // Alttaki top önce yuvarlanır: üstteki onun boşalttığı hücreyi görsün.
        ballCellsBuffer.Clear();
        for (int y = board.Height - 1; y >= 0; y--)
            for (int x = 0; x < board.Width; x++)
                if (vb[x, y] != null && obstacles.IsBilliardBallAt(x, y))
                    ballCellsBuffer.Add(new Vector2Int(x, y));

        foreach (var cell in ballCellsBuffer)
        {
            var ball = vb[cell.x, cell.y];
            if (ball == null) continue;

            // Duran top her yöne bakar; yana yuvarlanmakta olan top yalnız aynı yönde uzar.
            int sideDir = 0;
            bool atRest = IsBallAtRest(ball, cell.x, cell.y);
            if (!atRest && !(billiard.TryGetRollDir(ball.View, out sideDir) && CanExtendRoll(ball, cell.x, cell.y)))
                continue;

            int x = cell.x, y = cell.y;
            bool pocketed = false;
            for (int guard = board.Width * board.Height; guard > 0; guard--)
            {
                if (!TryPickBallStep(vb, x, y, ref sideDir, out var next, out pocketed))
                    break;

                vb[x, y] = null;
                ball.Path.Add(next);
                if (pocketed)
                    break;
                vb[next.x, next.y] = ball;
                x = next.x;
                y = next.y;
            }

            if (pocketed)
            {
                billiard.ClearRollDir(ball.View);
                pocketedBallsThisPlan.Add((ball.View, ball.Path.ToArray()));
            }
            else if (ball.Path.Count > 1)
            {
                // Yana adımla bitti ve altı dolu → yuvarlanıyor (sonraki planlar uzatabilir). Aksi hâlde düşecek.
                var prev = ball.Path[ball.Path.Count - 2];
                bool sideways = prev.y == y && sideDir != 0;
                if (sideways && !(billiard.TryGetCellBelow(x, y, out var below) && IsSlotEmpty(vb, below.x, below.y)))
                    billiard.SetRollDir(ball.View, sideDir);
                else
                    billiard.ClearRollDir(ball.View);
            }
            else if (atRest)
                billiard.ClearRollDir(ball.View);
        }
    }

    // Havadaki top uzatılabilir mi: mantıksal hedefinde (son planın yazdığı hücre), tutulmuyor, kurtarılmıyor.
    private bool CanExtendRoll(VirtualTile ball, int x, int y)
    {
        var view = ball.View;
        return view != null && ball.Path.Count == 1 && view.X == x && view.Y == y
            && !board.IsCellHeld(x, y) && !board.Billiard.IsLifting(view);
    }

    // Top yalnız hücresinde DURURKEN yuvarlanır (havadaki top önce iner; iniş yeni planı uyandırır).
    private bool IsBallAtRest(VirtualTile ball, int x, int y)
    {
        var view = ball.View;
        return view != null && ball.Path.Count == 1 && view.X == x && view.Y == y
            && view.IsRuntimeIdle && !board.FallMotion.HasPendingMotion(view)
            && !board.IsCellHeld(x, y)          // roket/pulse hücreyi tutarken taş gibi top da kalır
            && !board.Billiard.IsLifting(view);
    }

    // Sıradaki adım. false: top durur ya da aşağı düşecek (dikey adımı yerçekimi simülasyonu atar).
    private bool TryPickBallStep(VirtualTile[,] vb, int x, int y, ref int sideDir, out Vector2Int next, out bool pocket)
    {
        next = default;
        pocket = false;
        var billiard = board.Billiard;

        // 1) Aşağı: hole'lardan geçerek ilk hücre. Cep → gir; boş yuva → yerçekimi düşürür.
        if (billiard.TryGetCellBelow(x, y, out var below))
        {
            if (board.ObstacleStateService.IsBilliardPocketAt(below.x, below.y))
            {
                next = below;
                pocket = true;
                return true;
            }
            if (IsSlotEmpty(vb, below.x, below.y))
                return false;
        }

        // 2) Çapraz aşağı, 3) yana (yalnız süregelen yönde). Her kademede cep ya da boş yuva aday.
        if (TryPickBallSideways(vb, x, y, 1, 0, ref next, ref pocket))
            return true;
        if (TryPickBallSideways(vb, x, y, 0, sideDir, ref next, ref pocket))
        {
            sideDir = next.x - x;
            return true;
        }
        return false;
    }

    // dy=1 → çapraz aşağı, dy=0 → yana. lockedDir≠0 ise yalnız o yön (yana yuvarlanma geri dönmez).
    private bool TryPickBallSideways(VirtualTile[,] vb, int x, int y, int dy, int lockedDir,
        ref Vector2Int next, ref bool pocket)
    {
        var billiard = board.Billiard;
        int bestDist = int.MaxValue;
        bool found = false;
        int ty = y + dy;
        if (ty >= board.Height) return false;

        // Eşitlikte sağ (taş kayma kuralıyla aynı yön tercihi).
        for (int dx = 1; dx >= -1; dx -= 2)
        {
            if (lockedDir != 0 && dx != lockedDir) continue;
            int tx = x + dx;
            if (tx < 0 || tx >= board.Width) continue;
            if (dy == 1 && !IsDiagonalPassableCell(tx, y) && !IsDiagonalPassableCell(x, ty)) continue;

            bool isPocket = board.ObstacleStateService.IsBilliardPocketAt(tx, ty);
            if (!isPocket && !IsSlotEmpty(vb, tx, ty)) continue;

            int dist = isPocket ? 0 : billiard.DistanceToPocket(tx, ty);
            if (found && dist >= bestDist) continue;
            found = true;
            bestDist = dist;
            next = new Vector2Int(tx, ty);
            pocket = isPocket;
        }
        return found;
    }

    // Plan simülasyonu bitti, tahtaya yazılmadan önce: cebe giren topların verisini topla (hedef +1).
    // board.Tiles bu anda boş (taşlar VirtualTile'da) → OnObstacleDestroyed hücredeki taşa dokunamaz.
    private void CollectPocketedBallData()
    {
        foreach (var (view, path) in pocketedBallsThisPlan)
            board.Billiard.CollectBallData(path[0]);
    }

    // Plan tahtaya yazıldıktan sonra: cebe giren topların görselini (yuvarlan → cebe bat → hedefe uç) başlat.
    private void StartPocketedBallVisuals()
    {
        foreach (var (view, path) in pocketedBallsThisPlan)
            board.Billiard.PlayPocketed(view, path);
        pocketedBallsThisPlan.Clear();
    }
}
