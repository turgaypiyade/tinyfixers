using UnityEngine;

public enum LevelGoalTargetType : int
{
    Tile = 0,
    Obstacle = 1,
    Collectible = 2
}

public enum CollectibleId : int
{
    None = 0,
    EnergyOrb = 1
}

[System.Serializable]
public class LevelGoalDefinition
{
    public LevelGoalTargetType targetType = LevelGoalTargetType.Tile;
    public TileType tileType = TileType.Gear;
    public ObstacleId obstacleId = ObstacleId.Stone;
    public CollectibleId collectibleId = CollectibleId.EnergyOrb;
    [Tooltip("Optional HUD icon override for this goal. If empty, TopHUD uses the default tile / obstacle / collectible icon resolution.")]
    public Sprite iconOverride;
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
    chest1 = 4,
    chest2 = 5,
    chest3 = 6,
    plastic = 7,
    plastic_orange = 8,

    Plastic_Yellow = 9,
    PipeV_1x2 = 10,
    Plastic_Blue = 11,
    Plastic_Red = 12,
    Plastic_Green = 13,
    Oil = 14,

    Big_4x4 = 20,

    ColorChest = 21,
    EnergyContainer = 22,
    BatteryBox = 23,

    OmegaModul = 24,

    // Seamless dirt/mud overlay. Doesn't block, sits visually under the tile.
    // Damaged by adjacent matches; cleared after N hits.
    Mud = 25,

    // Multi-cell shrinking tube obstacle. Managed by TubeObstacleService.
    // Cells are stamped into obstacles[] at runtime from LevelData.tubes[].
    Tube = 26,
}

public enum TubeDirection { Up, Down, Left, Right }

[System.Serializable]
public struct TubeEntry
{
    [Tooltip("Flat cell index (y*width+x) of the BASE cell (socket end).")]
    public int originCellIndex;
    [Tooltip("Direction from base toward the open end.")]
    public TubeDirection direction;
    [Tooltip("Total number of cells the tube occupies (including base).")]
    [Min(2)] public int length;
}

[CreateAssetMenu(fileName = "Level_001", menuName = "CoreCollapse/Level Data", order = 1)]
public class LevelData : ScriptableObject
{
    public const int MinWidth = 1;
    public const int MaxWidth = 10;
    public const int MinHeight = 1;
    public const int MaxHeight = 11;

    public int width = 9;
    public int height = 9;
    public int moves = 25;
    [Min(0)] public int baseCoinReward = 100;
    public LevelGoalDefinition[] goals;

    [Header("Energy Container")]
    [Tooltip("How many EnergyOrb collectibles each EnergyContainer releases in this level. EnergyContainerRuntime can still provide a fallback, but level data owns the tuning.")]
    [Min(1)] public int energyPerContainer = 10;

    [Header("Tutorial")]
    [Tooltip("Bu level açılınca board'a inject edilecek combo tutorial. None = normal level.")]
    public ComboTutorialId comboTutorial = ComboTutorialId.None;

    [Header("Audio")]
    public AudioClip musicClip;

    [Range(0f, 1f)] public float musicVolume = 1f;

    [Header("Libraries")]
    public ObstacleLibrary obstacleLibrary;

    [Tooltip("0=Empty, 1=Normal. size = width*height")]
    public int[] cells;

    [Tooltip("Obstacle layer. 0=None. size = width*height")]
    public int[] obstacles;

    [Tooltip("For multi-cell obstacles: stores the origin cell index. -1 means none.")]
    public int[] obstacleOrigins;

    [Tooltip("Shrinking tube obstacles. Cells are stamped into obstacles[] at runtime by GridSpawner.")]
    public TubeEntry[] tubes;

    public int Index(int x, int y) => y * width + x;

    public bool InBounds(int x, int y) =>
        x >= 0 && y >= 0 && x < width && y < height;

    private void OnValidate()
    {
        width = Mathf.Clamp(width, MinWidth, MaxWidth);
        height = Mathf.Clamp(height, MinHeight, MaxHeight);
        baseCoinReward = Mathf.Max(0, baseCoinReward);
        energyPerContainer = Mathf.Max(1, energyPerContainer);
        int size = width * height;

        if (cells == null || cells.Length != size)
        {
            cells = new int[size];
            for (int i = 0; i < size; i++)
                cells[i] = (int)CellType.Normal;
        }

        if (obstacles == null || obstacles.Length != size)
        {
            obstacles = new int[size];
            for (int i = 0; i < size; i++)
                obstacles[i] = (int)ObstacleId.None;
        }

        if (obstacleOrigins == null || obstacleOrigins.Length != size)
        {
            obstacleOrigins = new int[size];
            for (int i = 0; i < size; i++)
                obstacleOrigins[i] = -1;
        }

        if (tubes == null)
            tubes = System.Array.Empty<TubeEntry>();

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
