using System.Collections;
using UnityEngine;

/// <summary>
/// Clear effect: spawns a ghost that flies to the matching TopHUD goal slot.
/// Only used when ClearAnimationMode.GoalFlyToHud is requested.
/// </summary>
public sealed class GoalFlyTileClearEffect : ITileClearEffect
{
    private readonly BoardController board;
    private readonly TileAnimator tileAnimator;

    public GoalFlyTileClearEffect(BoardController board, TileAnimator tileAnimator)
    {
        this.board = board;
        this.tileAnimator = tileAnimator;
    }

    public bool CanHandle(ClearAnimationMode mode) => mode == ClearAnimationMode.GoalFlyToHud;

    public IEnumerator Play(TileView tile, float delay, float duration)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile == null || board == null)
            yield break;

        var hud = board.TopHud;
        var fx = board.GoalFlyFx;

        if (hud == null || fx == null ||
            !hud.TryGetGoalTargetRectForTile(tile.GetTileType(), out var target) || target == null)
        {
            if (tileAnimator != null)
                yield return tileAnimator.PlayPop(tile, duration);
            yield break;
        }

        board.StartCoroutine(fx.Play(tile, target, duration));

        if (tileAnimator != null)
            yield return tileAnimator.PlayPop(tile, duration);
    }
}
