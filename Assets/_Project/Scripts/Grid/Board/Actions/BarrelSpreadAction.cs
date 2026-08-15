using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bir hamlede kırılan barrel'ların her biri için 4x4 mud yayılımını oynatır: damla
/// animasyonu (BarrelSplatterAnimator) + damla varışında o hücrenin mud VIEW'ı.
/// Async (ObstacleSpread) çalışır → §3a gereği mud VERİSİ splatter'dan ÖNCE toplu commit edilir
/// (akan cascade mud'ı hemen görür); yalnız view/goal bildirimi damla varışında kalır.
/// </summary>
public sealed class BarrelSpreadAction : BoardAction
{
    public readonly struct BarrelSource
    {
        public readonly Vector2Int Origin;
        public readonly ObstacleId ObstacleId;

        public BarrelSource(Vector2Int origin, ObstacleId obstacleId)
        {
            Origin = origin;
            ObstacleId = obstacleId;
        }
    }

    private readonly BoardController _board;
    private readonly List<BarrelSource> _barrels;

    public BarrelSpreadAction(BoardController board, List<BarrelSource> barrels)
    {
        _board = board;
        _barrels = barrels;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (_board == null || _barrels == null || _barrels.Count == 0)
            yield break;

        var obstacles = _board.ObstacleStateService;
        if (obstacles == null)
            yield break;

        var spread = new BarrelMudSpreadService(_board, obstacles);
        var animator = _board.GetComponent<BarrelSplatterAnimator>();

        for (int i = 0; i < _barrels.Count; i++)
        {
            var barrel = _barrels[i];
            var origin = barrel.Origin;

            // Barrel türevlerinin footprint boyutu library def'inden gelir.
            var def = _board.LevelData?.obstacleLibrary?.Get(barrel.ObstacleId);
            Vector2Int size = def != null
                ? new Vector2Int(Mathf.Max(1, def.size.x), Mathf.Max(1, def.size.y))
                : Vector2Int.one;

            var targets = spread.ComputeTargets(origin, size, ObstacleId.Mud);

            if (targets.Count > 0)
            {
                // §3a: mud DATA'sını UP-FRONT commit et — böylece splatter oynarken akan cascade mud'ı
                // hemen görür (yanlış/kaymış hücreye stamp veya boş hücreye düşüş olmaz). View + goal
                // bildirimi damla varışında kalır → progressive görsel korunur.
                var committed = new HashSet<Vector2Int>();
                for (int t = 0; t < targets.Count; t++)
                {
                    var c = targets[t];
                    if (obstacles.TrySpawnSingleCellObstacleAtOrBeneathOverTile(c.x, c.y, ObstacleId.Mud))
                        committed.Add(c);
                }

                void OnLand(Vector2Int cell)
                {
                    // Yalnız up-front commit edilen VE hâlâ mud olan hücre için view/goal bildir
                    // (async pencerede cascade mud'ı temizlemiş olabilir → phantom view oluşturma).
                    if (committed.Contains(cell) && obstacles.IsMudAt(cell.x, cell.y))
                        _board.RaiseObstacleCreatedDynamic(cell.x, cell.y);
                }

                if (animator != null)
                {
                    yield return animator.PlaySplatter(origin, size, targets, OnLand);
                }
                else
                {
                    Debug.LogWarning("[Barrel] BarrelSplatterAnimator component NOT found on BoardController GameObject — mud stamped without animation.");
                    for (int t = 0; t < targets.Count; t++)
                        OnLand(targets[t]);
                }
            }

            // Mud stamp edildikten SONRA barrel'ın Mud-goal placeholder'ını serbest bırak
            // (0 hedef olsa bile). Böylece sayaç mud eklenmeden 0'a inip erken WIN vermez.
            _board.RaiseBarrelResolved();
        }
    }
}
