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

        yield return tile.PopOut(duration);
    }
}

public sealed class DefaultPopTileClearEffect : ITileClearEffect
{
    public bool CanHandle(ClearAnimationMode mode) => mode == ClearAnimationMode.Default;

    public IEnumerator Play(TileView tile, float delay, float duration)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile != null)
            yield return tile.PopOut(duration);
    }
}

public sealed class LightningStrikeTileClearEffect : ITileClearEffect
{
    private readonly PulseCoreVfxPlayer boardVfxPlayer;
    private readonly Color lightningColor;

    public LightningStrikeTileClearEffect(PulseCoreVfxPlayer boardVfxPlayer, Color lightningColor)
    {
        this.boardVfxPlayer = boardVfxPlayer;
        this.lightningColor = lightningColor;
    }

    public bool CanHandle(ClearAnimationMode mode) => mode == ClearAnimationMode.LightningStrike;

    public IEnumerator Play(TileView tile, float delay, float duration)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile == null) yield break;

        boardVfxPlayer?.PlayLightningAtTile(tile, duration);
        yield return tile.PlayLightningStrikeAndShrink(duration, lightningColor);
    }


    public sealed class GoalFlyTileClearEffect : ITileClearEffect
    {
        private readonly BoardController board;

        public GoalFlyTileClearEffect(BoardController board)
        {
            this.board = board;
        }

        public bool CanHandle(ClearAnimationMode mode) => mode == ClearAnimationMode.GoalFlyToHud;

        public IEnumerator Play(TileView tile, float delay, float duration)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (tile == null || board == null)
                yield break;

            // HUD slot bul
            var hud = board.TopHud;
            if (hud == null || board.GoalFlyFx == null)
            {
                // Fallback: goal fly yoksa normal pop
                yield return tile.PopOut(duration);
                yield break;
            }

            if (!hud.TryGetGoalTargetRectForTile(tile.GetTileType(), out var target) || target == null)
            {
                // Bu tile aktif goal değil → normal pop
                yield return tile.PopOut(duration);
                yield break;
            }

            // Ghost ile uçuşu ayrı coroutine’de başlat (board logic beklemesin / tile destroy güvenli)
            board.StartCoroutine(board.GoalFlyFx.Play(tile, target, duration));

            // Gerçek tile normal clear akışı: pop-out (ghost zaten uçuyor)
            yield return tile.PopOut(duration);
        }
    }

}
