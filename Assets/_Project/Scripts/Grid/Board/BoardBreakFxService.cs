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

        Color color = ResolveBreakColor(tile);

        SpawnAtWorld(
            board.TileBreakFxPrefab,
            board.TileBreakFxLifetime,
            board.GetTileWorldCenter(tile),
            color);
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

        SpawnAtWorld(
            prefab,
            lifetime,
            board.GetCellWorldCenterPosition(x, y),
            Color.white);
    }

    private void SpawnAtWorld(GameObject prefab, float lifetime, Vector3 worldPos, Color color)
    {
        if (prefab == null)
            return;

        RectTransform parent = board.BreakFxParent;
        GameObject go;

        if (parent != null)
        {
            go = Object.Instantiate(prefab, parent);

            RectTransform rt = go.GetComponent<RectTransform>();
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
        ApplyColor(go, color);

        if (lifetime > 0f)
            Object.Destroy(go, lifetime);
    }

    private void ApplyColor(GameObject go, Color color)
    {
        if (go == null)
            return;

        ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            main.startColor = color;
        }
    }

    private Color ResolveBreakColor(TileView tile)
    {
        if (tile == null)
            return Color.white;

        TileType type = tile.GetTileType();

        // SystemOverride için base tipi kullan
        if (tile.GetSpecial() == TileSpecial.SystemOverride &&
            tile.GetOverrideBaseType(out var baseType))
        {
            type = baseType;
        }

        return type switch
        {
            TileType.Gear => new Color(0.95f, 0.30f, 0.30f, 1f),
            TileType.Core => new Color(0.35f, 0.85f, 0.45f, 1f),
            TileType.Bolt => new Color(0.30f, 0.60f, 1.00f, 1f),
            TileType.Plate => new Color(1.00f, 0.78f, 0.25f, 1f),

            // fallback'ler
            TileType.LineEmitter_H => new Color(0.95f, 0.30f, 0.30f, 1f),
            TileType.LineEmitter_V => new Color(0.30f, 0.60f, 1.00f, 1f),
            TileType.PatchBot => new Color(1.00f, 0.78f, 0.25f, 1f),
            TileType.SystemOverride => Color.white,
            TileType.Normal => Color.white,
            _ => Color.white
        };
    }
}