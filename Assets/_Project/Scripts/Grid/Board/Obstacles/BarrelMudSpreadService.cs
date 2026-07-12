using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Barrel kırıldığında etrafına saçılacak mud hücrelerini hesaplar.
/// Bölge: barrel footprint'inin (1x1, 1x2 veya herhangi bir boyut) bounding-box merkezine
/// ortalanmış 4x4 kare. Barrel'ın tüm hücreleri de bu 4x4 içinde kalır (kırıldıktan sonra
/// None oldukları için mud hedefidir). Yalnızca board içinde, hole olmayan, tile'lı ve üzerinde
/// başka obstacle bulunmayan (ObstacleId.None) hücreler hedef sayılır.
/// (OilSpreadService kalıbında saf yardımcı sınıf.)
/// </summary>
public sealed class BarrelMudSpreadService
{
    private const int WindowSize = 4;   // yayılım alanı 4x4

    private readonly BoardController board;
    private readonly ObstacleStateService obstacles;

    public BarrelMudSpreadService(BoardController board, ObstacleStateService obstacles)
    {
        this.board = board;
        this.obstacles = obstacles;
    }

    /// <param name="origin">Barrel'ın sol-üst (min) hücresi.</param>
    /// <param name="size">Barrel footprint boyutu (ör. 1x2 = 1 sütun, 2 satır).</param>
    public List<Vector2Int> ComputeTargets(Vector2Int origin, Vector2Int size)
    {
        var result = new List<Vector2Int>();
        if (board == null || obstacles == null)
            return result;

        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);
        int minX = origin.x, minY = origin.y;
        int maxX = origin.x + w - 1, maxY = origin.y + h - 1;

        // 4x4 penceresi footprint merkezine ortalanır. 1x1'de -1..+2 (eski davranış); 1x2'de
        // dikey eksende barrel'ın iki hücresini de kapsar (y-1..y+2).
        int startX = Mathf.FloorToInt((minX + maxX) / 2f) - 1;
        int startY = Mathf.FloorToInt((minY + maxY) / 2f) - 1;

        for (int dy = 0; dy < WindowSize; dy++)
        for (int dx = 0; dx < WindowSize; dx++)
        {
            var cell = new Vector2Int(startX + dx, startY + dy);
            if (CanPlaceMudAt(cell))
                result.Add(cell);
        }

        return result;
    }

    private bool CanPlaceMudAt(Vector2Int cell)
    {
        if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) return false;
        if (board.Holes[cell.x, cell.y]) return false;
        if (board.GetTileViewAt(cell.x, cell.y) == null) return false;
        // Üzerinde başka bir obstacle (mud dahil) varsa dokunma.
        return obstacles.GetObstacleIdAt(cell.x, cell.y) == ObstacleId.None;
    }
}
