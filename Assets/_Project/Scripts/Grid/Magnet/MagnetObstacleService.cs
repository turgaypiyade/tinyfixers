using System.Collections.Generic;
using UnityEngine;

/// Manages all magnet pair obstacles in the current level.
/// Two magnets share an energy path. Hitting a magnet endpoint moves it one step
/// toward the other. When they meet, both vanish along with the connecting path.
public class MagnetObstacleService : MonoBehaviour
{
    private readonly Dictionary<int, MagnetInstance> magnetsByOrigin = new();
    private readonly Dictionary<int, int> cellToOrigin = new();
    private ObstacleStateService obstacleStateService;

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(ObstacleStateService service)
    {
        obstacleStateService = service;
        magnetsByOrigin.Clear();
        cellToOrigin.Clear();
    }

    public void RegisterMagnet(int originCellIndex, int[] orderedPath, MagnetView view)
    {
        if (orderedPath == null || orderedPath.Length < 2 || view == null) return;

        var instance = new MagnetInstance(orderedPath, view);
        magnetsByOrigin[originCellIndex] = instance;
        foreach (int idx in orderedPath)
            cellToOrigin[idx] = originCellIndex;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public bool HasMagnetAt(int cellIndex) => cellToOrigin.ContainsKey(cellIndex);

    /// True if cellIndex is one of the CURRENT endpoints (A or B). Middle path cells → false.
    public bool IsMagnetEndpoint(int cellIndex)
    {
        if (!cellToOrigin.TryGetValue(cellIndex, out int origin)) return false;
        if (!magnetsByOrigin.TryGetValue(origin, out var magnet)) return false;
        return cellIndex == magnet.Path[magnet.MagnetAIndex]
            || cellIndex == magnet.Path[magnet.MagnetBIndex];
    }

    // ── Hit handling ──────────────────────────────────────────────────────────

    /// Called by ObstacleStateService with the ACTUAL hit cell index (not origin).
    /// Returns TRUE if this was a current endpoint (A/B) and the magnet shrank/destroyed;
    /// FALSE if the cell is inert (middle path / not a magnet) → çağıran "no hit" sayar.
    public bool HandleMagnetHit(int hitCellIndex)
    {
        if (!cellToOrigin.TryGetValue(hitCellIndex, out int origin)) return false;
        if (!magnetsByOrigin.TryGetValue(origin, out var magnet)) return false;

        bool isA = hitCellIndex == magnet.Path[magnet.MagnetAIndex];
        bool isB = hitCellIndex == magnet.Path[magnet.MagnetBIndex];

        // Only endpoint magnets react — middle path cells are inert blockers.
        if (!isA && !isB) return false;

        int prevAIdx = magnet.MagnetAIndex;
        int prevBIdx = magnet.MagnetBIndex;

        if (isA)
        {
            int freed = magnet.Path[magnet.MagnetAIndex];
            cellToOrigin.Remove(freed);
            magnet.MagnetAIndex++;
            obstacleStateService?.FreeMagnetCell(freed);
        }
        else
        {
            int freed = magnet.Path[magnet.MagnetBIndex];
            cellToOrigin.Remove(freed);
            magnet.MagnetBIndex--;
            obstacleStateService?.FreeMagnetCell(freed);
        }

        if (magnet.MagnetAIndex >= magnet.MagnetBIndex)
        {
            DestroyMagnetPair(origin, magnet);
            return true;
        }

        magnet.View.UpdatePositions(magnet.MagnetAIndex, magnet.MagnetBIndex, prevAIdx, prevBIdx);
        return true;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void DestroyMagnetPair(int origin, MagnetInstance magnet)
    {
        // Free the meeting cell (where A and B now coincide or have just passed).
        int meetCell = magnet.Path[magnet.MagnetAIndex];
        cellToOrigin.Remove(meetCell);
        obstacleStateService?.FreeMagnetCell(meetCell);

        // Safety: free B's position too if they happened to be different (shouldn't occur).
        if (magnet.MagnetAIndex != magnet.MagnetBIndex)
        {
            int bCell = magnet.Path[magnet.MagnetBIndex];
            cellToOrigin.Remove(bCell);
            obstacleStateService?.FreeMagnetCell(bCell);
        }

        magnetsByOrigin.Remove(origin);
        magnet.View.PlayDestroyAnimation();
        obstacleStateService?.NotifyMagnetFullyDestroyed(origin);
    }

    // ── Inner class ───────────────────────────────────────────────────────────

    private sealed class MagnetInstance
    {
        public readonly int[] Path;
        public readonly MagnetView View;
        public int MagnetAIndex;
        public int MagnetBIndex;

        public MagnetInstance(int[] path, MagnetView view)
        {
            Path = path;
            View = view;
            MagnetAIndex = 0;
            MagnetBIndex = path.Length - 1;
        }
    }
}
