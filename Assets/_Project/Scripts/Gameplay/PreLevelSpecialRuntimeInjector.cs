using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PreLevelSpecialRuntimeInjector : MonoBehaviour
{
    [SerializeField] private float boardReadyTimeout = 3f;
    [SerializeField] private float revealDuration = 0.28f;
    [SerializeField] private float startScale = 2.5f;
    [SerializeField] private float placementGap = 0.08f;

    private BoardController board;
    private readonly List<TileView> candidates = new();

    private IEnumerator Start()
    {
        yield return WaitForReadyBoard();

        if (board == null || !PreLevelSpecialSelectionState.HasSelection)
        {
            PreLevelSpecialSelectionState.Clear();
            Destroy(gameObject);
            yield break;
        }

        BuildCandidates();

        var selected = PreLevelSpecialSelectionState.SelectedSpecials;
        for (int i = 0; i < selected.Count && candidates.Count > 0; i++)
        {
            var tile = TakeRandomCandidate();
            if (tile == null)
                continue;

            ApplySpecial(tile, selected[i]);
            yield return Reveal(tile);

            if (placementGap > 0f)
                yield return new WaitForSeconds(placementGap);
        }

        PreLevelSpecialSelectionState.Clear();
        Destroy(gameObject);
    }

    private IEnumerator WaitForReadyBoard()
    {
        float elapsed = 0f;
        float timeout = Mathf.Max(0.1f, boardReadyTimeout);

        while (elapsed < timeout)
        {
            if (board == null)
                board = FindObjectOfType<BoardController>();

            if (IsBoardReady())
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool IsBoardReady()
    {
        if (board == null || board.Tiles == null || board.Holes == null)
            return false;

        if (board.Width <= 0 || board.Height <= 0)
            return false;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Tiles[x, y] != null)
                    return true;
            }
        }

        return false;
    }

    private void BuildCandidates()
    {
        candidates.Clear();

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                var tile = board.Tiles[x, y];
                if (IsEligible(tile, x, y))
                    candidates.Add(tile);
            }
        }
    }

    private bool IsEligible(TileView tile, int x, int y)
    {
        if (tile == null)
            return false;

        if (board.Holes[x, y])
            return false;

        if (tile.GetSpecial() != TileSpecial.None)
            return false;

        if (tile.TryGetCellState(out var state) && (state.hasObstacle || !state.canContainTile))
            return false;

        return true;
    }

    private TileView TakeRandomCandidate()
    {
        int index = Random.Range(0, candidates.Count);
        var tile = candidates[index];
        candidates.RemoveAt(index);
        return tile;
    }

    private void ApplySpecial(TileView tile, TileSpecial special)
    {
        if (special == TileSpecial.SystemOverride)
        {
            TileType baseType = tile.GetTileType();
            tile.SetSpecial(TileSpecial.SystemOverride, deferVisualUpdate: true);
            tile.SetOverrideBaseType(baseType);
        }
        else
        {
            tile.SetSpecial(special, deferVisualUpdate: true);
        }

        tile.RefreshIcon();
        board.SyncTileData(tile.X, tile.Y);
        board.RefreshTileObstacleVisual(tile);
        tile.ApplyTileSize(board.TileSize);
    }

    private IEnumerator Reveal(TileView tile)
    {
        if (tile == null || tile.IconImage == null)
            yield break;

        RectTransform rt = tile.IconImage.rectTransform;
        Vector3 baseScale = rt.localScale;
        Color baseColor = tile.IconImage.color;

        rt.localScale = baseScale * startScale;
        tile.IconImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, revealDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = GetRevealScale(t);
            float alpha = Mathf.Clamp01(t / 0.18f) * baseColor.a;

            rt.localScale = baseScale * scale;
            tile.IconImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }

        rt.localScale = baseScale;
        tile.IconImage.color = baseColor;
    }

    private float GetRevealScale(float t)
    {
        if (t < 0.58f)
            return Mathf.LerpUnclamped(startScale, 1.18f, EaseOut(t / 0.58f));

        if (t < 0.82f)
            return Mathf.LerpUnclamped(1.18f, 0.96f, EaseOut((t - 0.58f) / 0.24f));

        return Mathf.LerpUnclamped(0.96f, 1f, EaseOut((t - 0.82f) / 0.18f));
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }
}
