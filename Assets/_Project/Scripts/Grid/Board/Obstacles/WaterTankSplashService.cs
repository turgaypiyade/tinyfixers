using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WaterTank kırıldığında su birikintisi bırakılacak hücreleri seçer: board'un TAMAMINDAN rastgele
/// en fazla N hücre. Hedef = oynanabilir (canContainTile), hole değil ve üzerinde obstacle yok
/// (ObstacleId.None) — o an taş olup olmaması önemsiz (birikinti under-tile; depo cascade
/// sürerken kırılır, Barrel mud kuralıyla aynı). (BarrelMudSpreadService kalıbında saf yardımcı.)
/// </summary>
public sealed class WaterTankSplashService
{
    private readonly BoardController board;
    private readonly ObstacleStateService obstacles;

    public WaterTankSplashService(BoardController board, ObstacleStateService obstacles)
    {
        this.board = board;
        this.obstacles = obstacles;
    }

    public List<Vector2Int> ComputeRandomTargets(int count)
    {
        var result = new List<Vector2Int>();
        if (board == null || obstacles == null || count <= 0)
            return result;

        var candidates = new List<Vector2Int>();
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            if (board.Holes[x, y]) continue;
            if (obstacles.GetObstacleIdAt(x, y) != ObstacleId.None) continue;
            if (!board.TryGetCellState(x, y, out var state) || !state.canContainTile) continue;
            candidates.Add(new Vector2Int(x, y));
        }

        // Kısmi Fisher-Yates: ilk `count` eleman rastgele seçilir.
        int take = Mathf.Min(count, candidates.Count);
        for (int i = 0; i < take; i++)
        {
            int j = Random.Range(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            result.Add(candidates[i]);
        }

        return result;
    }
}
