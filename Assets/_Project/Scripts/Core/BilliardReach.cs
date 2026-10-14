using System;
using System.Collections.Generic;

/// <summary>
/// Bilardo topu → cep statik ulaşılabilirliği (Docs/BilliardBall_Plan.md). Runtime (BilliardBallService: yuvarlanma
/// eşitlik bozucu + takılma fail'i) ve level editörü (tasarım uyarısı) AYNI kuralı kullanır.
/// Topun adımları: aşağı (hole'lardan düşerek ilk hücreye), çapraz aşağı (iki köşeden biri hole değil), yana.
/// Yukarı asla. Yalnız hole + kenar + cep kalıcıdır; kırılabilir engeller zamanla açılır sayılır.
/// </summary>
public static class BilliardReach
{
    public const int Unreachable = int.MaxValue;

    /// dist[x,y] = cebe adım sayısı (cep 0; yol yoksa Unreachable). dist en az width×height olmalı.
    public static void ComputePocketDistances(int width, int height, Func<int, int, bool> isHole,
        Func<int, int, bool> isPocket, int[,] dist, Queue<(int x, int y)> queue)
    {
        queue.Clear();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                bool pocket = isPocket(x, y);
                dist[x, y] = pocket ? 0 : Unreachable;
                if (pocket) queue.Enqueue((x, y));
            }

        // Cepten geriye BFS: n'ye bir adımda gelebilen her c.
        while (queue.Count > 0)
        {
            var (nx, ny) = queue.Dequeue();
            int d = dist[nx, ny] + 1;

            // Aşağı: n'nin üstündeki ilk hole olmayan hücre.
            for (int cy = ny - 1; cy >= 0; cy--)
            {
                if (isHole(nx, cy)) continue;
                Relax(nx, cy);
                break;
            }

            for (int dx = -1; dx <= 1; dx += 2)
            {
                // Çapraz aşağı: c=(nx−dx, ny−1); köşelerden biri (c'nin yanı ya da altı) hole olmamalı.
                int cx = nx - dx, cy = ny - 1;
                if (cy >= 0 && cx >= 0 && cx < width && (!isHole(nx, cy) || !isHole(cx, ny)))
                    Relax(cx, cy);

                // Yana: c=(nx+dx, ny).
                Relax(nx + dx, ny);
            }

            void Relax(int x, int y)
            {
                if (x < 0 || x >= width || y < 0 || y >= height) return;
                if (isHole(x, y) || isPocket(x, y) || dist[x, y] <= d) return;
                dist[x, y] = d;
                queue.Enqueue((x, y));
            }
        }
    }
}
