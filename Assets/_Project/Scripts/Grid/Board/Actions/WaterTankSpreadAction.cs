using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kırılan WaterTank'ın su saçılımı: board'un rastgele N hücresine göktaşı damlası
/// (WaterTankSplashAnimator) + varışta o hücrenin WaterPuddle VIEW'ı + telefon ekranına yağmur
/// damlaları (ScreenRainDropsFx). BarrelSpreadAction kalıbı: async (ObstacleSpread) koşar →
/// §3a gereği birikinti VERİSİ animasyondan ÖNCE toplu commit edilir (akan cascade onu hemen
/// görür); yalnız view/goal bildirimi damla varışında kalır. Goal placeholder'ını bırakan
/// RaiseSplatSpreadResolved çağıranın (BoardController) finally'sindedir — hata olsa da düşer.
/// </summary>
public sealed class WaterTankSpreadAction : BoardAction
{
    private readonly BoardController _board;
    private readonly Vector2Int _origin;
    private readonly ObstacleId _tankId;

    public WaterTankSpreadAction(BoardController board, Vector2Int origin, ObstacleId tankId)
    {
        _board = board;
        _origin = origin;
        _tankId = tankId;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (_board == null)
            yield break;

        var obstacles = _board.ObstacleStateService;
        if (obstacles == null)
            yield break;

        var config = WaterTankConfig.Load();
        int count = config != null ? config.puddleCount : 8;

        // Depo boyutu (2x2 / 1x1) library def'inden — saçılım ve ekran efekti footprint merkezinden.
        var def = _board.LevelData?.obstacleLibrary?.Get(_tankId);
        Vector2Int size = def != null
            ? new Vector2Int(Mathf.Max(1, def.size.x), Mathf.Max(1, def.size.y))
            : Vector2Int.one;

        if (config == null || config.playScreenDrops)
            ScreenRainDropsFx.Play(TankScreenPoint(size), config);

        var targets = new WaterTankSplashService(_board, obstacles).ComputeRandomTargets(count);

        if (targets.Count > 0)
        {
            var committed = new HashSet<Vector2Int>();
            for (int i = 0; i < targets.Count; i++)
            {
                var c = targets[i];
                if (obstacles.TrySpawnSingleCellObstacleAt(c.x, c.y, ObstacleId.WaterPuddle))
                    committed.Add(c);
            }

            void OnLand(Vector2Int cell)
            {
                // Yalnız up-front commit edilen VE hâlâ birikinti olan hücre için view/goal bildir
                // (uçuş penceresinde cascade onu kurutmuş olabilir → phantom view oluşturma).
                if (committed.Contains(cell) && obstacles.IsWaterPuddleAt(cell.x, cell.y))
                    _board.RaiseObstacleCreatedDynamic(cell.x, cell.y);
            }

            if (!_board.TryGetComponent<WaterTankSplashAnimator>(out var animator))
                animator = _board.gameObject.AddComponent<WaterTankSplashAnimator>();
            animator.Init(_board, config);

            yield return animator.PlaySplash(_origin, size, targets, OnLand);
        }
    }

    private Vector2 TankScreenPoint(Vector2Int size)
    {
        Vector3 c0 = _board.GetCellWorldCenterPosition(_origin.x, _origin.y);
        Vector3 c1 = _board.GetCellWorldCenterPosition(_origin.x + size.x - 1, _origin.y + size.y - 1);
        // Board Screen Space Camera canvas'ında: kamerasız (null) çeviri dünya koordinatını piksel sanıp
        // noktayı ekranın sol-alt köşesine düşürüyordu → damla kümesi hep oraya gidiyordu.
        var canvas = _board.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.rootCanvas.worldCamera
            : null;
        return RectTransformUtility.WorldToScreenPoint(cam, (c0 + c1) * 0.5f);
    }
}
