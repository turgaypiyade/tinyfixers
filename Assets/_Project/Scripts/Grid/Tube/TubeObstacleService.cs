using System.Collections.Generic;
using UnityEngine;

/// Manages all tube obstacles in the current level.
/// Each tube is a multi-cell over-tile blocker that shrinks from its open end
/// when any of its cells is damaged (by a neighboring match or by a special).
public class TubeObstacleService : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [Range(0f, 1f)] [SerializeField] private float hitSoundVolume = 1f;
    [SerializeField] private AudioSource sfxSource;

    private readonly Dictionary<int, TubeInstance> tubesByOrigin = new();
    private readonly Dictionary<int, int> cellIndexToOrigin = new();
    private ObstacleStateService obstacleStateService;

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(ObstacleStateService service)
    {
        obstacleStateService = service;

        if (sfxSource == null)
            sfxSource = FindFirstObjectByType<BoardController>()?.SfxSource;
        tubesByOrigin.Clear();
        cellIndexToOrigin.Clear();
    }

    public void RegisterTube(int originCellIndex, int[] orderedCellIndices, TubeView view)
    {
        if (orderedCellIndices == null || orderedCellIndices.Length == 0 || view == null) return;

        var tube = new TubeInstance(orderedCellIndices, view);
        tubesByOrigin[originCellIndex] = tube;
        foreach (int idx in orderedCellIndices)
            cellIndexToOrigin[idx] = originCellIndex;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public bool HasTubeAt(int cellIndex) => cellIndexToOrigin.ContainsKey(cellIndex);

    // ── Hit handling (called by ObstacleStateService interceptor) ─────────────

    /// Called with the ORIGIN (base cell index) of the hit tube.
    public void HandleTubeHit(int originCellIndex)
    {
        if (!tubesByOrigin.TryGetValue(originCellIndex, out var tube)) return;

        if (hitSound != null && GameSettings.SoundEnabled)
            sfxSource?.PlayOneShot(hitSound, hitSoundVolume);

        int freedCell = tube.PopOpenEnd();
        cellIndexToOrigin.Remove(freedCell);

        obstacleStateService?.FreeTubeCell(freedCell);

        if (tube.IsEmpty)
        {
            tubesByOrigin.Remove(originCellIndex);
            tube.View.DestroyView();

            // Goal counter güncellensin diye OnObstacleDestroyed fire edilir.
            // (Normal obstacle path bu event'i kendisi fire eder; tube kendi akışında olduğu için elle çağırıyoruz.)
            obstacleStateService?.NotifyTubeFullyDestroyed(originCellIndex);
        }
        else
        {
            tube.View.AnimateShrink(tube.RemainingLength);
        }
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    /// Returns ordered cell indices [base → open end], or null if out of bounds.
    public static int[] GetCellIndices(TubeEntry entry, int gridWidth, int gridHeight)
    {
        int baseX = entry.originCellIndex % gridWidth;
        int baseY = entry.originCellIndex / gridWidth;

        int dx = 0, dy = 0;
        switch (entry.direction)
        {
            case TubeDirection.Up:    dy = -1; break;
            case TubeDirection.Down:  dy = +1; break;
            case TubeDirection.Left:  dx = -1; break;
            case TubeDirection.Right: dx = +1; break;
        }

        var cells = new int[entry.length];
        for (int i = 0; i < entry.length; i++)
        {
            int cx = baseX + dx * i;
            int cy = baseY + dy * i;
            if (cx < 0 || cx >= gridWidth || cy < 0 || cy >= gridHeight)
                return null;
            cells[i] = cy * gridWidth + cx;
        }
        return cells;
    }

    /// Returns the top-left grid cell (x,y) of the tube's bounding box.
    public static (int x, int y) GetTopLeftCell(TubeEntry entry, int gridWidth)
    {
        int baseX = entry.originCellIndex % gridWidth;
        int baseY = entry.originCellIndex / gridWidth;

        return entry.direction switch
        {
            TubeDirection.Up   => (baseX, baseY - (entry.length - 1)),
            TubeDirection.Left => (baseX - (entry.length - 1), baseY),
            _                  => (baseX, baseY),
        };
    }

    // ── Inner class ───────────────────────────────────────────────────────────

    private sealed class TubeInstance
    {
        private readonly List<int> cells;
        public readonly TubeView View;
        public readonly int TotalLength;

        public int RemainingLength => cells.Count;
        public bool IsEmpty => cells.Count == 0;

        public TubeInstance(int[] orderedCells, TubeView view)
        {
            cells = new List<int>(orderedCells);
            View  = view;
            TotalLength = orderedCells.Length;
        }

        /// Removes and returns the open-end (last) cell index.
        public int PopOpenEnd()
        {
            int last = cells[cells.Count - 1];
            cells.RemoveAt(cells.Count - 1);
            return last;
        }
    }
}
