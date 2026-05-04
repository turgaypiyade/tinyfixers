using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class OilSpreadAnimator : MonoBehaviour
{
    [SerializeField] private OilSpreadVisualConfig config;
    [SerializeField] private BoardController board;

    public IEnumerator PlaySpread(IReadOnlyList<Vector2Int> targets)
    {
        if (targets == null || targets.Count == 0)
            yield break;

        float duration = config != null ? config.spreadDuration : 0.22f;
        float stagger = config != null ? config.staggerDelay : 0.04f;
        Color targetColor = config != null ? config.overlayColor : new Color(0f, 0f, 0f, 0.45f);

        for (int i = 0; i < targets.Count; i++)
        {
            var cell = targets[i];
            var tile = board != null ? board.GetTileViewAt(cell.x, cell.y) : null;
            if (tile != null)
                StartCoroutine(FadeInOverlay(tile, duration, targetColor));

            if (stagger > 0f && i < targets.Count - 1)
                yield return new WaitForSeconds(stagger);
        }

        yield return new WaitForSeconds(duration);
    }

    public IEnumerator PlayRemove(Vector2Int cell)
    {
        var tile = board != null ? board.GetTileViewAt(cell.x, cell.y) : null;
        if (tile != null)
            tile.SetCoveredByCellOverlay(false);
        yield break;
    }

    private IEnumerator FadeInOverlay(TileView tile, float duration, Color targetColor)
    {
        tile.SetCoveredByCellOverlay(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            tile.SetOverlayAlpha(Mathf.Lerp(0f, targetColor.a, t));
            yield return null;
        }

        tile.SetOverlayAlpha(targetColor.a);
    }
}
