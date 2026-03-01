using UnityEngine;

public readonly struct LightningLineStrike
{
    public readonly Vector2Int originCell;
    public readonly bool isHorizontal;

    public LightningLineStrike(Vector2Int originCell, bool isHorizontal)
    {
        this.originCell = originCell;
        this.isHorizontal = isHorizontal;
    }
}
