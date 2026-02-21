using System.Collections;
using UnityEngine;

/// <summary>
/// Clear effect: spawns a ghost that flies to the matching TopHUD goal slot.
/// Only used when ClearAnimationMode.GoalFlyToHud is requested.
/// </summary>
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

    var hud = board.TopHud;
    var fx  = board.GoalFlyFx;

    if (hud == null || fx == null ||
        !hud.TryGetGoalTargetRectForTile(tile.GetTileType(), out var target) || target == null)
    {
        yield return tile.PopOut(duration);
        yield break;
    }

    // 1️⃣ GERÇEK TAŞ BOARD'DA BÜYÜSÜN (POP)
    float popTime = Mathf.Min(0.15f, duration * 0.25f);
    Vector3 baseScale = tile.transform.localScale;

    float t = 0f;
    while (t < popTime)
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / popTime);

        // EaseOut büyüme
        float s = 1f + 0.30f * (1f - (1f - k) * (1f - k));
        tile.transform.localScale = baseScale * s;

        yield return null;
    }

    tile.transform.localScale = baseScale;

    // 2️⃣ GERÇEK TAŞI GİZLE (ghost uçacak)
    tile.SetIconAlpha(0f);

    // 3️⃣ GHOST HUD'A UÇSUN
    float flyTime = Mathf.Clamp(duration * 1.35f, 0.28f, 0.55f); // daha görünür ama aşırı değil
    board.StartCoroutine(fx.Play(tile, target, flyTime));
    yield return new WaitForSeconds(Mathf.Min(0.06f, flyTime * 0.25f));

    tile.SetIconAlpha(0f);
    //tile.transform.localScale = Vector3.zero;
}

}
