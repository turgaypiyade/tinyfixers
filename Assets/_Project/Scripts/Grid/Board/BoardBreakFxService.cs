using UnityEngine;

public class BoardBreakFxService
{
    private readonly BoardController board;

    public BoardBreakFxService(BoardController board)
    {
        this.board = board;
    }

    public void PlayTileBreak(TileView tile)
    {
        if (tile == null)
            return;

        SpawnAtWorld(
            board.TileBreakFxPrefab,
            board.TileBreakFxLifetime,
            board.GetTileWorldCenter(tile));
    }

    public void PlayObstacleBreak(ObstacleVisualChange change)
    {
        if (change.originIndex < 0 || board.Width <= 0 || board.Height <= 0)
            return;

        GameObject prefab = change.cleared
            ? board.ObstacleBreakFxPrefab
            : board.ObstacleHitFxPrefab;

        float lifetime = change.cleared
            ? board.ObstacleBreakFxLifetime
            : board.ObstacleHitFxLifetime;

        int x = change.originIndex % board.Width;
        int y = change.originIndex / board.Width;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;

        SpawnAtWorld(prefab, lifetime, board.GetCellWorldCenterPosition(x, y));
    }

    private void SpawnAtWorld(GameObject prefab, float lifetime, Vector3 worldPos)
    {
        if (prefab == null)
            return;

        RectTransform parent = board.BreakFxParent;
        GameObject go;

        if (parent != null)
        {
            go = Object.Instantiate(prefab, parent);
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = board.WorldToAnchoredIn(parent, worldPos);
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
            }
            else
            {
                go.transform.position = worldPos;
            }
        }
        else
        {
            go = Object.Instantiate(prefab, worldPos, Quaternion.identity);
        }

        go.SetActive(true);

        if (lifetime > 0f)
            Object.Destroy(go, lifetime);
    }
}