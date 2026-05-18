// Minimum interface SimMatchFinder needs from an obstacle layer.
// Implemented by both ObstacleStateService (fidelity test) and SimObstacleLayer (simulation).
public interface ISimObstacleQuery
{
    bool HasObstacleAt(int x, int y);
    bool IsMovableObstacleAt(int x, int y);
    bool IsInteractionLockedAt(int x, int y);
}
