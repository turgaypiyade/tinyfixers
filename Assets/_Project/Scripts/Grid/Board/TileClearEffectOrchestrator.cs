using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ITileClearEffect
{
    bool CanHandle(ClearAnimationMode mode);
    IEnumerator Play(TileView tile, float delay, float duration, bool suppressBurst = false);
}

public sealed class TileClearEffectOrchestrator
{
    private readonly List<ITileClearEffect> effects = new List<ITileClearEffect>();

    public TileClearEffectOrchestrator(params ITileClearEffect[] effectSet)
    {
        if (effectSet == null) return;

        for (int i = 0; i < effectSet.Length; i++)
        {
            if (effectSet[i] != null)
                effects.Add(effectSet[i]);
        }
    }

    public IEnumerator Play(TileView tile, ClearAnimationMode mode, float delay, float duration, bool suppressBurst = false)
    {
        if (tile == null) yield break;

        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect == null || !effect.CanHandle(mode))
                continue;

            yield return effect.Play(tile, delay, duration, suppressBurst);
            yield break;
        }

        yield break;
    }
}

public sealed class DefaultPopTileClearEffect : ITileClearEffect
{
    private readonly TileAnimator tileAnimator;

    public DefaultPopTileClearEffect(TileAnimator tileAnimator)
    {
        this.tileAnimator = tileAnimator;
    }

    public bool CanHandle(ClearAnimationMode mode) => mode == ClearAnimationMode.Default;

    public IEnumerator Play(TileView tile, float delay, float duration, bool suppressBurst = false)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile != null && tileAnimator != null)
            yield return tileAnimator.PlayPop(tile, duration, suppressBurst);
    }
}

public sealed class ElevatorFlingTileClearEffect : ITileClearEffect
{
    private readonly BoardController board;
    private readonly TileAnimator tileAnimator;

    public ElevatorFlingTileClearEffect(BoardController board, TileAnimator tileAnimator)
    {
        this.board = board;
        this.tileAnimator = tileAnimator;
    }

    public bool CanHandle(ClearAnimationMode mode) => mode == ClearAnimationMode.ElevatorLift;

    public IEnumerator Play(TileView tile, float delay, float duration, bool suppressBurst = false)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile == null) yield break;

        // Asansör alttan yukarı tarar: temizlenme sırası ((Height-1) - y). Bu sıraya göre
        // biri sağa, sonraki sola savrulsun (alternasyon ascent yönünde tutarlı).
        int height = board != null ? board.Height : 0;
        int order = height > 0 ? (height - 1 - tile.Y) : tile.Y;
        int dir = (order % 2 == 0) ? 1 : -1;

        if (tileAnimator != null)
            yield return tileAnimator.PlayElevatorFling(tile, duration, dir, suppressBurst);
    }
}

public sealed class LightningStrikeTileClearEffect : ITileClearEffect
{
    private readonly PulseCoreVfxPlayer boardVfxPlayer;
    private readonly Color lightningColor;
    private readonly TileAnimator tileAnimator;

    public LightningStrikeTileClearEffect(
        PulseCoreVfxPlayer boardVfxPlayer,
        Color lightningColor,
        TileAnimator tileAnimator)
    {
        this.boardVfxPlayer = boardVfxPlayer;
        this.lightningColor = lightningColor;
        this.tileAnimator = tileAnimator;
    }

    public bool CanHandle(ClearAnimationMode mode) => mode == ClearAnimationMode.LightningStrike;

    public IEnumerator Play(TileView tile, float delay, float duration, bool suppressBurst = false)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile == null) yield break;

        boardVfxPlayer?.PlayLightningAtTile(tile, duration);

        if (tileAnimator != null)
            yield return tileAnimator.PlayLightningStrikeAndShrink(tile, duration, lightningColor);
    }
}
