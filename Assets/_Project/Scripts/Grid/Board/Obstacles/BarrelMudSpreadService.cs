using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Barrel kırıldığında etrafına saçılacak mud hücrelerini hesaplar.
/// Bölge: barrel footprint'inin (1x1, 1x2 veya herhangi bir boyut) bounding-box merkezine
/// ortalanmış 4x4 kare. Barrel'ın tüm hücreleri de bu 4x4 içinde kalır (kırıldıktan sonra
/// None oldukları için mud hedefidir). Yalnızca board içinde, hole olmayan, tile'lı ve üzerinde
/// başka obstacle bulunmayan (ObstacleId.None) hücreler hedef sayılır.
/// Over-tile obstacle hücreleri de hedef olabilir; mud beneath store'a yazılır.
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
    public List<Vector2Int> ComputeTargets(Vector2Int origin, Vector2Int size, ObstacleId splatObstacleId = ObstacleId.Mud)
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
            if (CanPlaceSplatAt(cell, splatObstacleId))
                result.Add(cell);
        }

        return result;
    }

    private bool CanPlaceSplatAt(Vector2Int cell, ObstacleId splatObstacleId)
    {
        if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) return false;
        if (board.Holes[cell.x, cell.y]) return false;
        var current = obstacles.GetObstacleIdAt(cell.x, cell.y);

        if (current == ObstacleId.None)
        {
            // Mud under-tile'dır: hedef, footprint içindeki her PLAYABLE hücre — o an taş
            // olup olmaması ÖNEMSİZ. Barrel kırılır kırılmaz (board hâlâ cascade'deyken)
            // saçıldığı için taşlar akıyor olabilir; taş-varlığı şartı hücreleri yanlışlıkla
            // eler ("dağınık mud" hatası). Sabit topolojiye (canContainTile) bakılır.
            return board.TryGetCellState(cell.x, cell.y, out var state) && state.canContainTile;
        }

        return splatObstacleId == ObstacleId.Mud
            && obstacles.IsOverTileBlockerAt(cell.x, cell.y)
            && !obstacles.IsMovableObstacleAt(cell.x, cell.y);
    }
}
