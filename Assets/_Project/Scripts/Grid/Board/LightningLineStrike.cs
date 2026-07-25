using UnityEngine;

public readonly struct LightningLineStrike
{
    public readonly Vector2Int originCell;
    public readonly bool isHorizontal;
    public readonly float startDelaySeconds;

    // Yalnızca Row booster (joker) yatay süpürmesi drill VFX'i kullanır. LineH special
    // ve diğer tüm çizgi kaynakları false bırakır → eski iki-yönlü roket davranışı korunur.
    public readonly bool useDrillSweep;

    public LightningLineStrike(Vector2Int originCell, bool isHorizontal, float startDelaySeconds = 0f, bool useDrillSweep = false)
    {
        this.originCell = originCell;
        this.isHorizontal = isHorizontal;
        this.startDelaySeconds = Mathf.Max(0f, startDelaySeconds);
        this.useDrillSweep = useDrillSweep;
    }
}
