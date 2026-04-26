#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class First10TutorialLevelGenerator
{
    private const string OutputFolder = "Assets/_Project/Settings/GeneratedTutorialLevels";
    private const string LevelNamePrefix = "LevelA";

    // Mapping:
    // PurpleCapObs       -> plastic
    // ChestClassicStg1   -> chest1
    // ChestClassicStg2   -> chest2
    private const ObstacleId PurpleCap = ObstacleId.plastic;
    private const ObstacleId ChestStg1 = ObstacleId.chest1;
    private const ObstacleId ChestStg2 = ObstacleId.chest2;

    [MenuItem("Tools/CoreCollapse/Tutorial Levels/Create Missing LevelA 001-010")]
    public static void CreateMissingFirst10()
    {
        EnsureFolder(OutputFolder);

        ObstacleLibrary obstacleLibrary = FindFirstAsset<ObstacleLibrary>();

        int createdCount = 0;
        int skippedCount = 0;

        foreach (LevelSpec spec in GetSpecs())
        {
            string path = GetLevelPath(spec.levelNumber);

            // Existing levels are not overwritten, so manual editor changes stay safe.
            if (AssetDatabase.LoadAssetAtPath<LevelData>(path) != null)
            {
                skippedCount++;
                continue;
            }

            CreateLevelAsset(spec, obstacleLibrary, path);
            createdCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"First10TutorialLevelGenerator: Created {createdCount}, skipped {skippedCount}. Existing LevelA assets were not touched.");
    }

    private static void CreateLevelAsset(LevelSpec spec, ObstacleLibrary obstacleLibrary, string path)
    {
        LevelData level = ScriptableObject.CreateInstance<LevelData>();
        level.name = $"{LevelNamePrefix}_{spec.levelNumber:000}";

        ApplySpec(level, spec, obstacleLibrary);

        AssetDatabase.CreateAsset(level, path);
        EditorUtility.SetDirty(level);
    }

    private static void ApplySpec(LevelData level, LevelSpec spec, ObstacleLibrary obstacleLibrary)
    {
        string[] pattern = spec.pattern;

        int height = pattern.Length;
        int width = pattern[0].Length;
        int size = width * height;

        level.width = width;
        level.height = height;
        level.moves = spec.moves;
        level.goals = spec.goals;
        level.obstacleLibrary = obstacleLibrary;

        level.cells = new int[size];
        level.obstacles = new int[size];
        level.obstacleOrigins = new int[size];

        for (int i = 0; i < size; i++)
        {
            level.cells[i] = (int)CellType.Normal;
            level.obstacles[i] = (int)ObstacleId.None;
            level.obstacleOrigins[i] = -1;
        }

        for (int y = 0; y < height; y++)
        {
            string row = pattern[y];

            for (int x = 0; x < width; x++)
            {
                int idx = level.Index(x, y);
                char c = row[x];

                switch (c)
                {
                    case '_':
                        level.cells[idx] = (int)CellType.Empty;
                        level.obstacles[idx] = (int)ObstacleId.None;
                        level.obstacleOrigins[idx] = -1;
                        break;

                    case 'P':
                        PlaceSingleCellObstacle(level, x, y, PurpleCap);
                        break;

                    case 'C':
                        PlaceSingleCellObstacle(level, x, y, ChestStg1);
                        break;

                    case 'D':
                        PlaceSingleCellObstacle(level, x, y, ChestStg2);
                        break;
                }
            }
        }
    }

    private static void PlaceSingleCellObstacle(LevelData level, int x, int y, ObstacleId id)
    {
        int idx = level.Index(x, y);

        level.cells[idx] = (int)CellType.Normal;
        level.obstacles[idx] = (int)id;
        level.obstacleOrigins[idx] = idx;
    }

    private static LevelSpec[] GetSpecs()
    {
        return new[]
        {
            new LevelSpec(
                1,
                18,
                new[]
                {
                    TileGoal(TileType.Gear, 8),
                    TileGoal(TileType.Bolt, 8),
                },
                new[]
                {
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                }
            ),

            new LevelSpec(
                2,
                20,
                new[]
                {
                    TileGoal(TileType.Core, 10),
                    TileGoal(TileType.Plate, 10),
                },
                new[]
                {
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                }
            ),

            new LevelSpec(
                3,
                22,
                new[]
                {
                    ObstacleGoal(PurpleCap, 9),
                    TileGoal(TileType.Gear, 8),
                },
                new[]
                {
                    "P........",
                    "P........",
                    "P........",
                    "P........",
                    "P........",
                    "P........",
                    "P........",
                    "P........",
                    "P........",
                }
            ),

            new LevelSpec(
                4,
                24,
                new[]
                {
                    ObstacleGoal(PurpleCap, 13),
                    TileGoal(TileType.Bolt, 10),
                    TileGoal(TileType.Plate, 10),
                },
                new[]
                {
                    "P........",
                    "P......P.",
                    "P........",
                    "P......P.",
                    "P........",
                    "P......P.",
                    "P........",
                    "P......P.",
                    "P........",
                }
            ),

            new LevelSpec(
                5,
                26,
                new[]
                {
                    ObstacleGoal(PurpleCap, 12),
                    ObstacleGoal(ChestStg1, 7),
                    TileGoal(TileType.Core, 10),
                },
                new[]
                {
                    "P......P.",
                    "P......P.",
                    "P......P.",
                    ".........",
                    ".CCCCCCC.",
                    ".........",
                    "P......P.",
                    "P......P.",
                    "P......P.",
                }
            ),

            new LevelSpec(
                6,
                28,
                new[]
                {
                    ObstacleGoal(PurpleCap, 6),
                    ObstacleGoal(ChestStg1, 7),
                    TileGoal(TileType.Gear, 12),
                },
                new[]
                {
                    ".........",
                    "....C....",
                    "P...C...P",
                    "....C....",
                    "....C....",
                    "P...C...P",
                    "....C....",
                    "P...C...P",
                    "....C....",
                    ".........",
                }
            ),

            new LevelSpec(
                7,
                30,
                new[]
                {
                    ObstacleGoal(PurpleCap, 12),
                    ObstacleGoal(ChestStg1, 7),
                    TileGoal(TileType.Bolt, 12),
                    TileGoal(TileType.Plate, 12),
                },
                new[]
                {
                    "_......._",
                    "P......P.",
                    "P......P.",
                    "P......P.",
                    ".CCCCCCC.",
                    "P......P.",
                    "P......P.",
                    "P......P.",
                    ".........",
                    "_......._",
                }
            ),

            new LevelSpec(
                8,
                32,
                new[]
                {
                    ObstacleGoal(PurpleCap, 8),
                    ObstacleGoal(ChestStg1, 6),
                    ObstacleGoal(ChestStg2, 1),
                    TileGoal(TileType.Core, 12),
                },
                new[]
                {
                    "_......._",
                    "P......P.",
                    "...CCC...",
                    "P......P.",
                    "....D....",
                    "P......P.",
                    "...CCC...",
                    "P......P.",
                    ".........",
                    "_......._",
                }
            ),

            new LevelSpec(
                9,
                34,
                new[]
                {
                    ObstacleGoal(PurpleCap, 12),
                    ObstacleGoal(ChestStg2, 2),
                    TileGoal(TileType.Gear, 14),
                    TileGoal(TileType.Bolt, 14),
                },
                new[]
                {
                    "_......._",
                    "P......P.",
                    "P......P.",
                    "P......P.",
                    ".........",
                    "...D.D...",
                    ".........",
                    "P......P.",
                    "P......P.",
                    "P......P.",
                    "_......._",
                }
            ),

            new LevelSpec(
                10,
                36,
                new[]
                {
                    ObstacleGoal(PurpleCap, 14),
                    ObstacleGoal(ChestStg1, 8),
                    ObstacleGoal(ChestStg2, 3),
                    TileGoal(TileType.Plate, 16),
                },
                new[]
                {
                    "_......._",
                    "P......P.",
                    "P.CCCCC.P",
                    "P......P.",
                    "..D...D..",
                    "....D....",
                    "P......P.",
                    "P.CCC..P.",
                    "P......P.",
                    "P......P.",
                    "_......._",
                }
            ),
        };
    }

    private static LevelGoalDefinition TileGoal(TileType tileType, int amount)
    {
        return new LevelGoalDefinition
        {
            targetType = LevelGoalTargetType.Tile,
            tileType = tileType,
            obstacleId = ObstacleId.None,
            amount = amount
        };
    }

    private static LevelGoalDefinition ObstacleGoal(ObstacleId obstacleId, int amount)
    {
        return new LevelGoalDefinition
        {
            targetType = LevelGoalTargetType.Obstacle,
            tileType = TileType.Gear,
            obstacleId = obstacleId,
            amount = amount
        };
    }

    private static string GetLevelPath(int levelNumber)
    {
        return $"{OutputFolder}/{LevelNamePrefix}_{levelNumber:000}.asset";
    }

    private static T FindFirstAsset<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

        if (guids == null || guids.Length == 0)
        {
            Debug.LogWarning($"First10TutorialLevelGenerator: No {typeof(T).Name} asset found.");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private readonly struct LevelSpec
    {
        public readonly int levelNumber;
        public readonly int moves;
        public readonly LevelGoalDefinition[] goals;
        public readonly string[] pattern;

        public LevelSpec(
            int levelNumber,
            int moves,
            LevelGoalDefinition[] goals,
            string[] pattern)
        {
            this.levelNumber = levelNumber;
            this.moves = moves;
            this.goals = goals;
            this.pattern = pattern;
        }
    }
}
#endif