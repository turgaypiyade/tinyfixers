using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Board-level VFX: combo explosions, pulse emitter effects.
/// No coroutines — just play/instantiate calls.
/// BoardController delegates here and forwards serialized references.
/// </summary>
public class BoardVfxService
{
    private readonly BoardController board;

    public BoardVfxService(BoardController board)
    {
        this.board = board;
    }

    public float PlaySystemOverrideComboVfxAndGetDuration(OverrideComboController vfx)
    {
        if (vfx == null) return 0f;
        vfx.gameObject.SetActive(true);
        vfx.Play();
        float duration = vfx.GetTotalDuration();
        SystemOverrideBehaviorEvents.EmitOverrideComboVfxPlayed(duration);
        return duration;
    }

    public void PlayPulseEmitterComboVfxAtCell(PulseEmitterComboController vfx, RectTransform vfxSpace, int x, int y)
    {
        if (vfx == null || vfxSpace == null) return;

        vfx.gameObject.SetActive(true);

        Vector3 worldMid = ResolveWorldCenterForCell(x, y);
        Vector2 localMid = board.WorldToAnchoredIn(vfxSpace, worldMid);
        Vector2 boardSize = vfxSpace.rect.size;
        if (boardSize.sqrMagnitude < 1f)
            boardSize = new Vector2(board.Width * board.TileSize, board.Height * board.TileSize);

        vfx.SetTileSize(board.TileSize);
        vfx.PlayAt(localMid, boardSize);
    }

    public void PlayPulsePulseExplosionVfxAtCell(GameObject prefab, RectTransform vfxSpace, float lifetime, int x, int y)
    {
        if (prefab == null || vfxSpace == null) return;

        PulseBehaviorEvents.EmitPulseExplosionPlayed(new Vector2Int(x, y));

        Vector3 worldMid = ResolveWorldCenterForCell(x, y);
        Vector2 localMid = board.WorldToAnchoredIn(vfxSpace, worldMid);

        var go = Object.Instantiate(prefab, vfxSpace);
        go.SetActive(true);

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = localMid;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
        else
        {
            go.transform.position = worldMid;
        }

        Object.Destroy(go, lifetime);
    }


    private Vector3 ResolveWorldCenterForCell(int x, int y)
    {
        bool inBounds = x >= 0 && x < board.Width && y >= 0 && y < board.Height;
        if (inBounds)
        {
            TileView tile = board.Tiles[x, y];
            if (tile != null)
                return board.GetTileWorldCenter(tile);

            // Obstacle / empty tile fallback: use geometric center of the board cell.
            Vector3 topLeft = board.GetCellWorldPosition(x, y);
            return topLeft + new Vector3(board.TileSize * 0.5f, -board.TileSize * 0.5f, 0f);
        }

        // Last resort: preserve previous behavior and use last swap midpoint.
        TileView ta = board.LastSwapA;
        TileView tb = board.LastSwapB;
        if (ta != null && tb != null)
            return (board.GetTileWorldCenter(ta) + board.GetTileWorldCenter(tb)) * 0.5f;
        if (ta != null)
            return board.GetTileWorldCenter(ta);
        if (tb != null)
            return board.GetTileWorldCenter(tb);

        return Vector3.zero;
    }

    public HashSet<Vector2Int> BuildPulseEmitterTargets(int cx, int cy)
    {
        var set = new HashSet<Vector2Int>();

        for (int yy = cy - 1; yy <= cy + 1; yy++)
        {
            if (yy < 0 || yy >= board.Height) continue;
            for (int x = 0; x < board.Width; x++)
                if (!board.IsMaskHoleCell(x, yy)) set.Add(new Vector2Int(x, yy));
        }

        for (int xx = cx - 1; xx <= cx + 1; xx++)
        {
            if (xx < 0 || xx >= board.Width) continue;
            for (int y = 0; y < board.Height; y++)
                if (!board.IsMaskHoleCell(xx, y)) set.Add(new Vector2Int(xx, y));
        }

        return set;
    }
}
