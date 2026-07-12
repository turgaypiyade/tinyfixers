using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bir hamlede kırılan barrel'ların her biri için 4x4 mud yayılımını oynatır: damla
/// animasyonu (BarrelSplatterAnimator) + her damla varışında o hücreye mud stamp'i.
/// OilSpreadAction kalıbı: görsel önce, veri damla varışında (onLand) uygulanır.
/// </summary>
public sealed class BarrelSpreadAction : BoardAction
{
    private readonly BoardController _board;
    private readonly List<Vector2Int> _barrelOrigins;

    public BarrelSpreadAction(BoardController board, List<Vector2Int> barrelOrigins)
    {
        _board = board;
        _barrelOrigins = barrelOrigins;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (_board == null || _barrelOrigins == null || _barrelOrigins.Count == 0)
            yield break;

        var obstacles = _board.ObstacleStateService;
        if (obstacles == null)
            yield break;

        var spread = new BarrelMudSpreadService(_board, obstacles);
        var animator = _board.GetComponent<BarrelSplatterAnimator>();

        // Barrel footprint boyutu (ör. 1x2 = 1 sütun, 2 satır) library def'inden.
        var def = _board.LevelData?.obstacleLibrary?.Get(ObstacleId.Barrel);
        Vector2Int size = def != null
            ? new Vector2Int(Mathf.Max(1, def.size.x), Mathf.Max(1, def.size.y))
            : Vector2Int.one;

        for (int i = 0; i < _barrelOrigins.Count; i++)
        {
            var origin = _barrelOrigins[i];
            var targets = spread.ComputeTargets(origin, size);

            if (targets.Count > 0)
            {
                void OnLand(Vector2Int cell)
                {
                    if (obstacles.TrySpawnSingleCellObstacleAt(cell.x, cell.y, ObstacleId.Mud))
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
