using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ITileClearEffect
{
    bool CanHandle(ClearAnimationMode mode);
    IEnumerator Play(TileView tile, float delay, float duration);
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

    public IEnumerator Play(TileView tile, ClearAnimationMode mode, float delay, float duration)
    {
        if (tile == null) yield break;

        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect == null || !effect.CanHandle(mode))
                continue;

            yield return effect.Play(tile, delay, duration);
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

    public IEnumerator Play(TileView tile, float delay, float duration)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile != null && tileAnimator != null)
            yield return tileAnimator.PlayPop(tile, duration);
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

    public IEnumerator Play(TileView tile, float delay, float duration)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile == null) yield break;

        boardVfxPlayer?.PlayLightningAtTile(tile, duration);

        if (tileAnimator != null)
            yield return tileAnimator.PlayLightningStrikeAndShrink(tile, duration, lightningColor);
    }
}
