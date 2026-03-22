using UnityEngine;

public enum LevelGoalTargetType : int
{
    Tile = 0,
    Obstacle = 1
}

[System.Serializable]
public class LevelGoalDefinition
{
    public LevelGoalTargetType targetType = LevelGoalTargetType.Tile;
    public TileType tileType = TileType.Gear;
    public ObstacleId obstacleId = ObstacleId.Stone;
    [Min(1)] public int amount = 1;
}

public enum CellType : int
{
    Empty = 0,
    Normal = 1
}

public enum ObstacleId : int
{
    None = 0,
    Stone = 1,
    Shield1 = 2,
    Shield2 = 3,
    PipeV_1x2 = 10,
    Big_4x4 = 20
}

[CreateAssetMenu(fileName = "Level_001", menuName = "CoreCollapse/Level Data", order = 1)]
public class LevelData : ScriptableObject
{
    public int width = 9;
    public int height = 9;
    public int moves = 25;
    public LevelGoalDefinition[] goals;

    [Header("Libraries")]
    public ObstacleLibrary obstacleLibrary;

    [Tooltip("0=Empty, 1=Normal. size = width*height")]
    public int[] cells;

    [Tooltip("Obstacle layer. 0=None. size = width*height")]
    public int[] obstacles;

    [Tooltip("For multi-cell obstacles: stores the origin cell index. -1 means none.")]
    public int[] obstacleOrigins;

    public int Index(int x, int y) => y * width + x;
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        int size = width * height;

        if (cells == null || cells.Length != size)
        {
            cells = new int[size];
            for (int i = 0; i < size; i++) cells[i] = (int)CellType.Normal;
        }

        if (obstacles == null || obstacles.Length != size)
        {
            obstacles = new int[size];
            for (int i = 0; i < size; i++) obstacles[i] = (int)ObstacleId.None;
        }

        if (obstacleOrigins == null || obstacleOrigins.Length != size)
        {
            obstacleOrigins = new int[size];
            for (int i = 0; i < size; i++) obstacleOrigins[i] = -1;
        }

        if (goals == null)
            goals = System.Array.Empty<LevelGoalDefinition>();

        for (int i = 0; i < goals.Length; i++)
        {
            if (goals[i] == null)
                goals[i] = new LevelGoalDefinition();

            goals[i].amount = Mathf.Max(1, goals[i].amount);
        }
    }
}
