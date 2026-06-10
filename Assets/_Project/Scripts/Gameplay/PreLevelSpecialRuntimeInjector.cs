using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public sealed class PreLevelSpecialRuntimeInjector : MonoBehaviour
{
    [SerializeField] private float boardReadyTimeout = 8f;
    [SerializeField] private int startupDelayFrames = 2;
    [SerializeField] private int stableReadyFrames = 2;
    [SerializeField] private float revealDuration = 0.4f;
    [SerializeField] private float startScale = 2.5f;
    [SerializeField] private float placementGap = 0.08f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip placeSfx;
    [SerializeField, Range(0f, 1f)] private float placeSfxVolume = 1f;

    private BoardController board;
    private readonly List<TileView> candidates = new();
    private readonly List<TileSpecial> pendingSelection = new();
    private bool initialized;

    public static PreLevelSpecialRuntimeInjector EnsureForSelection(IReadOnlyList<TileSpecial> selected)
    {
        if (selected == null || selected.Count == 0)
            return null;

        var existing = FindFirstObjectByType<PreLevelSpecialRuntimeInjector>(FindObjectsInactive.Include);
        if (existing == null)
        {
            var go = new GameObject("PreLevelSpecialInjector_Runtime");
            DontDestroyOnLoad(go);
            existing = go.AddComponent<PreLevelSpecialRuntimeInjector>();
        }
        else
        {
            // Ensure existing GO survives the scene change regardless of where it lives.
            DontDestroyOnLoad(existing.gameObject);
        }

        existing.Initialize(selected);
        Debug.Log($"[PreLevelSpecialRuntimeInjector] EnsureForSelection count={selected.Count}");
        return existing;
    }

    public void Initialize(IReadOnlyList<TileSpecial> selected)
    {
        pendingSelection.Clear();

        if (selected != null)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] != TileSpecial.None)
                    pendingSelection.Add(selected[i]);
            }
        }

        initialized = true;
        Debug.Log($"[PreLevelSpecialRuntimeInjector] Initialized with {pendingSelection.Count} selected specials.");
    }

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private IEnumerator Start()
    {
        var selected = initialized
            ? new List<TileSpecial>(pendingSelection)
            : PreLevelSpecialSelectionState.GetSelectionSnapshot();

        if (selected.Count == 0)
        {
            Debug.Log("[PreLevelSpecialRuntimeInjector] No selected specials; destroying injector.");
            Destroy(gameObject);
            yield break;
        }

        for (int i = 0; i < startupDelayFrames; i++)
            yield return null;

        yield return WaitForReadyBoard();

        if (!IsBoardReady())
        {
            Debug.LogWarning($"[PreLevelSpecialRuntimeInjector] Board was not ready before timeout; keeping pre-level selection for the next scene load. {GetBoardStatus()}");
            Destroy(gameObject);
            yield break;
        }

        // Loading ekranı board'u örterken special yerleştirme animasyonu + SFX
        // oynamasın — oyuncu sadece sesi duyup animasyonu göremiyor. Loading ekranı
        // tamamen kapanıp board görünür olana kadar bekle.
        yield return WaitForLoadingScreenHidden();

        BuildCandidates();
        Debug.Log($"[PreLevelSpecialRuntimeInjector] Board ready. candidates={candidates.Count}, selected={selected.Count}");

        int placedCount = 0;
        for (int i = 0; i < selected.Count && candidates.Count > 0; i++)
        {
            var tile = TakeRandomCandidate();
            if (tile == null)
                continue;

            if (!ApplySpecial(tile, selected[i]))
                continue;

            placedCount++;
            PlayPlacementSfx(selected[i]);
            yield return Reveal(tile);

            if (placementGap > 0f)
                yield return new WaitForSeconds(placementGap);
        }

        if (placedCount < selected.Count)
            Debug.LogWarning($"[PreLevelSpecialRuntimeInjector] Placed {placedCount}/{selected.Count} selected pre-level specials; not enough eligible normal tiles.");

        if (placedCount > 0)
            PreLevelSpecialSelectionState.Clear();

        Debug.Log($"[PreLevelSpecialRuntimeInjector] Finished placement. placed={placedCount}/{selected.Count}");
        Destroy(gameObject);
    }

    private IEnumerator WaitForReadyBoard()
    {
        float elapsed = 0f;
        float timeout = Mathf.Max(0.1f, boardReadyTimeout);
        int stableFrames = 0;
        int requiredStableFrames = Mathf.Max(1, stableReadyFrames);

        while (elapsed < timeout)
        {
            if (board == null)
                board = FindFirstObjectByType<BoardController>();

            if (IsBoardReady())
            {
                stableFrames++;
                if (stableFrames >= requiredStableFrames)
                    yield break;
            }
            else
            {
                stableFrames = 0;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForLoadingScreenHidden()
    {
        // Güvenlik tavanı: loading ekranı beklenmedik şekilde kapanmazsa sonsuza
        // kadar takılmayalım.
        const float maxWait = 10f;
        float elapsed = 0f;

        while (LoadingScreenManager.IsVisible && elapsed < maxWait)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool IsBoardReady()
    {
        if (board == null || board.Tiles == null || board.Holes == null || board.GridData == null)
            return false;

        if (board.Width <= 0 || board.Height <= 0)
            return false;

        if (board.IsBusy || board.ActiveBackgroundJobs > 0)
            return false;

        int filledCells = 0;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y])
                    continue;

                var tile = board.Tiles[x, y];
                if (tile == null || board.GridData[x, y] == null)
                    return false;

                if (tile.X != x || tile.Y != y)
                    return false;

                filledCells++;
            }
        }

        return filledCells > 0 && HasEligibleCandidate();
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

        if (board.Holes[x, y] || board.GridData[x, y] == null)
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

    private bool HasEligibleCandidate()
    {
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (IsEligible(board.Tiles[x, y], x, y))
                    return true;
            }
        }

        return false;
    }

    private string GetBoardStatus()
    {
        if (board == null)
            return "board=null";

        if (board.Tiles == null || board.Holes == null || board.GridData == null)
            return $"arrays tiles={board.Tiles != null} holes={board.Holes != null} gridData={board.GridData != null}";

        int tiles = 0;
        int gridData = 0;
        int holes = 0;
        int candidatesCount = 0;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y])
                {
                    holes++;
                    continue;
                }

                if (board.Tiles[x, y] != null)
                    tiles++;

                if (board.GridData[x, y] != null)
                    gridData++;

                if (IsEligible(board.Tiles[x, y], x, y))
                    candidatesCount++;
            }
        }

        return $"size={board.Width}x{board.Height} busy={board.IsBusy} bgJobs={board.ActiveBackgroundJobs} holes={holes} tiles={tiles} gridData={gridData} candidates={candidatesCount}";
    }

    private bool ApplySpecial(TileView tile, TileSpecial special)
    {
        if (tile == null || special == TileSpecial.None || board == null)
            return false;

        if (!IsEligible(tile, tile.X, tile.Y))
            return false;

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
        return true;
    }

    private void PlayPlacementSfx(TileSpecial special)
    {
        if (audioSource != null)
        {
            AudioClip clip = placeSfx != null ? placeSfx : audioSource.clip;
            if (clip != null && GameSettings.SoundEnabled)
                audioSource.PlayOneShot(clip, placeSfxVolume);

            return;
        }

        if (board == null || board.Audio == null || special == TileSpecial.None)
            return;

        board.Audio.Emit(BoardSfxRequest.SpecialCreate(special));
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
