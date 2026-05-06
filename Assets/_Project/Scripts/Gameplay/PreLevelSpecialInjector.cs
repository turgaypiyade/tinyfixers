using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(250)]
public class PreLevelSpecialInjector : MonoBehaviour
{
    [SerializeField] private BoardController board;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip placeSfx;
    [SerializeField] private float startDelay = 0.08f;
    [SerializeField] private float gapBetweenPlacements = 0.08f;
    [SerializeField] private float revealDuration = 0.28f;
    [SerializeField] private float startScale = 2.5f;

    private readonly List<TileView> candidates = new();

    private void Awake()
    {
        if (board == null)
            board = FindObjectOfType<BoardController>();
    }

    private IEnumerator Start()
    {
        if (!PreLevelSpecialSelectionState.HasSelection)
            yield break;

        if (board == null)
            board = FindObjectOfType<BoardController>();

        if (board == null)
        {
            PreLevelSpecialSelectionState.Clear();
            yield break;
        }

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        yield return InjectSelectedSpecials();
        PreLevelSpecialSelectionState.Clear();
    }

    public IEnumerator InjectSelectedSpecials()
    {
        var selected = PreLevelSpecialSelectionState.SelectedSpecials;
        if (selected == null || selected.Count == 0 || board == null)
            yield break;

        BuildCandidateList();

        for (int i = 0; i < selected.Count; i++)
        {
            if (candidates.Count == 0)
                yield break;

            TileSpecial special = selected[i];
            TileView tile = TakeRandomCandidate();

            if (tile == null)
                continue;

            ApplySpecial(tile, special);
            yield return PlayPlacementReveal(tile);

            if (gapBetweenPlacements > 0f)
                yield return new WaitForSeconds(gapBetweenPlacements);
        }
    }

    private void BuildCandidateList()
    {
        candidates.Clear();

        if (board == null || board.Tiles == null)
            return;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                TileView tile = board.Tiles[x, y];
                if (!IsEligible(tile, x, y))
                    continue;

                candidates.Add(tile);
            }
        }
    }

    private bool IsEligible(TileView tile, int x, int y)
    {
        if (tile == null)
            return false;

        if (board.Holes != null && board.Holes[x, y])
            return false;

        if (tile.GetSpecial() != TileSpecial.None)
            return false;

        if (tile.TryGetCellState(out var state))
        {
            if (state.hasObstacle || !state.canContainTile)
                return false;
        }

        return true;
    }

    private TileView TakeRandomCandidate()
    {
        if (candidates.Count == 0)
            return null;

        int index = Random.Range(0, candidates.Count);
        TileView tile = candidates[index];
        candidates.RemoveAt(index);
        return tile;
    }

    private void ApplySpecial(TileView tile, TileSpecial special)
    {
        if (tile == null || special == TileSpecial.None || board == null)
            return;

        if (special == TileSpecial.SystemOverride)
        {
            TileType baseType = tile.GetTileType();
            tile.SetSpecial(TileSpecial.SystemOverride, deferVisualUpdate: true);
            tile.SetOverrideBaseType(baseType);
            tile.RefreshIcon();
        }
        else
        {
            tile.SetSpecial(special);
            tile.RefreshIcon();
        }

        board.SyncTileData(tile.X, tile.Y);
        board.RefreshTileObstacleVisual(tile);
        tile.ApplyTileSize(board.TileSize);
    }

    private IEnumerator PlayPlacementReveal(TileView tile)
    {
        if (tile == null)
            yield break;

        ImageTarget imageTarget = ImageTarget.From(tile);
        if (!imageTarget.IsValid)
            yield break;

        RectTransform rt = imageTarget.RectTransform;
        Vector3 baseScale = rt.localScale;
        UnityEngine.Color baseColor = imageTarget.Color;

        rt.localScale = baseScale * startScale;
        imageTarget.Color = new UnityEngine.Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        PlayOneShot(placeSfx);

        float duration = Mathf.Max(0.01f, revealDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (rt == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float scale;
            if (t < 0.58f)
            {
                float k = t / 0.58f;
                scale = Mathf.LerpUnclamped(startScale, 1.18f, EaseOut(k));
            }
            else if (t < 0.82f)
            {
                float k = (t - 0.58f) / 0.24f;
                scale = Mathf.LerpUnclamped(1.18f, 0.96f, EaseOut(k));
            }
            else
            {
                float k = (t - 0.82f) / 0.18f;
                scale = Mathf.LerpUnclamped(0.96f, 1f, EaseOut(k));
            }

            float alpha = Mathf.Clamp01(t / 0.18f);
            rt.localScale = baseScale * scale;
            imageTarget.Color = new UnityEngine.Color(baseColor.r, baseColor.g, baseColor.b, alpha * baseColor.a);

            yield return null;
        }

        if (rt != null)
            rt.localScale = baseScale;

        imageTarget.Color = baseColor;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    private readonly struct ImageTarget
    {
        private readonly UnityEngine.UI.Image image;
        public readonly RectTransform RectTransform;
        public bool IsValid => image != null && RectTransform != null;
        public UnityEngine.Color Color
        {
            get => image != null ? image.color : UnityEngine.Color.white;
            set
            {
                if (image != null)
                    image.color = value;
            }
        }

        private ImageTarget(UnityEngine.UI.Image image, RectTransform rectTransform)
        {
            this.image = image;
            RectTransform = rectTransform;
        }

        public static ImageTarget From(TileView tile)
        {
            if (tile == null || tile.IconImage == null)
                return new ImageTarget(null, null);

            return new ImageTarget(tile.IconImage, tile.IconImage.rectTransform);
        }
    }
}
