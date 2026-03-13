using UnityEngine;

public readonly struct LightningLineStrike
{
    public readonly Vector2Int originCell;
    public readonly bool isHorizontal;
    public readonly float startDelaySeconds;

    public LightningLineStrike(Vector2Int originCell, bool isHorizontal, float startDelaySeconds = 0f)
    {
        this.originCell = originCell;
        this.isHorizontal = isHorizontal;
        this.startDelaySeconds = Mathf.Max(0f, startDelaySeconds);
    }
}
