// Backward-compatible extension surface for branches/call-sites that still invoke
// ObstacleStateService.GetAllowDiagonalAt / TryGetStageSnapshotAt as if they were instance APIs.
// If instance members exist, they take precedence; if not, these extensions bridge to compat helpers.
public static class ObstacleStateServiceLegacyApiExtensions
{
/*     public static bool GetAllowDiagonalAt(this ObstacleStateService service, int x, int y)
        => service.GetAllowDiagonalAtCompat(x, y);
 */

    public static bool GetAllowDiagonalAt(this ObstacleStateService service, int x, int y)
        => service != null && service.IsDiagonalAllowedAt(x, y);

    public static bool TryGetStageSnapshotAt(this ObstacleStateService service, int x, int y, out ObstacleStageSnapshot snapshot)
        => service.TryGetStageSnapshotAtCompat(x, y, out snapshot);
}
