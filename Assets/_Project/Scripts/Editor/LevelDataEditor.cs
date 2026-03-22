using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private enum PaintMode { Mask, Obstacle, Erase }

    private PaintMode mode = PaintMode.Obstacle;
    private ObstacleId selectedObstacle = ObstacleId.Stone;

    private const int cellPx = 30;
    private const int paletteIcon = 44;

    private static readonly Color boardBg = new Color(0.70f, 0.83f, 0.95f, 1f);
    private static readonly Color normalCell = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color holeCell = new Color(0.03f, 0.06f, 0.10f, 0.95f);
    private static readonly Color gridLine = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color occupiedOverlay = new Color(0f, 0f, 0f, 0.12f);

    public override void OnInspectorGUI()
    {
        var level = (LevelData)target;

        DrawSettings(level);
        EditorGUILayout.Space(8);

        // IMPORTANT: Library reference is stored on LevelData asset (persistent).
        level.obstacleLibrary = (ObstacleLibrary)EditorGUILayout.ObjectField(
            "Obstacle Library",
            level.obstacleLibrary,
            typeof(ObstacleLibrary),
            false
        );

        EditorGUILayout.Space(6);

        mode = (PaintMode)GUILayout.Toolbar((int)mode, new[] { "Mask", "Obstacle", "Erase" });

        if (mode == PaintMode.Obstacle)
        {
            DrawPalette(level);
        }
        else
        {
            EditorGUILayout.HelpBox("Mask: ilk tık hücreyi Empty (hole), ikinci tık veya Erase hücreyi Normal yapar.", MessageType.None);
        }

        EnsureArrays(level);

        EditorGUILayout.Space(8);
        DrawGrid(level);

        if (GUI.changed)
            EditorUtility.SetDirty(level);
    }

    private void DrawSettings(LevelData level)
    {
        EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);
        level.width = EditorGUILayout.IntField("Width", level.width);
        level.height = EditorGUILayout.IntField("Height", level.height);
        level.moves = EditorGUILayout.IntField("Moves", level.moves);

        DrawGoals(level);
    }

    private void DrawGoals(LevelData level)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Goals", EditorStyles.boldLabel);

        if (level.goals == null)
            level.goals = System.Array.Empty<LevelGoalDefinition>();

        int removeIndex = -1;
        for (int i = 0; i < level.goals.Length; i++)
        {
            var goal = level.goals[i] ??= new LevelGoalDefinition();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Goal {i + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
                removeIndex = i;
            EditorGUILayout.EndHorizontal();

            goal.targetType = (LevelGoalTargetType)EditorGUILayout.EnumPopup("Target Type", goal.targetType);
            if (goal.targetType == LevelGoalTargetType.Tile)
                goal.tileType = (TileType)EditorGUILayout.EnumPopup("Tile", goal.tileType);
            else
                goal.obstacleId = (ObstacleId)EditorGUILayout.EnumPopup("Obstacle", goal.obstacleId);

            goal.amount = Mathf.Max(1, EditorGUILayout.IntField("Amount", goal.amount));
            EditorGUILayout.EndVertical();
        }

        if (removeIndex >= 0)
        {
            var list = new System.Collections.Generic.List<LevelGoalDefinition>(level.goals);
            list.RemoveAt(removeIndex);
            level.goals = list.ToArray();
        }

        if (GUILayout.Button("Add Goal"))
        {
            var list = new System.Collections.Generic.List<LevelGoalDefinition>(level.goals)
            {
                new LevelGoalDefinition()
            };
            level.goals = list.ToArray();
        }
    }

    private void EnsureArrays(LevelData level)
    {
        int size = Mathf.Max(1, level.width) * Mathf.Max(1, level.height);

        if (level.cells == null || level.cells.Length != size)
        {
            level.cells = new int[size];
            for (int i = 0; i < size; i++) level.cells[i] = (int)CellType.Normal;
        }

        if (level.obstacles == null || level.obstacles.Length != size)
        {
            level.obstacles = new int[size];
            for (int i = 0; i < size; i++) level.obstacles[i] = (int)ObstacleId.None;
        }

        if (level.obstacleOrigins == null || level.obstacleOrigins.Length != size)
        {
            level.obstacleOrigins = new int[size];
            for (int i = 0; i < size; i++) level.obstacleOrigins[i] = -1;
        }
    }

    private void DrawPalette(LevelData level)
    {
        EditorGUILayout.LabelField("Obstacle Palette", EditorStyles.boldLabel);

        var library = level.obstacleLibrary;

        if (library == null || library.obstacles == null || library.obstacles.Count == 0)
        {
            EditorGUILayout.HelpBox("ObstacleLibrary boş. Create → CoreCollapse → Obstacle Library oluşturup sprite/size ekle.", MessageType.Warning);
            selectedObstacle = (ObstacleId)EditorGUILayout.EnumPopup("Selected Obstacle (fallback)", selectedObstacle);
            return;
        }

        var seenIds = new System.Collections.Generic.HashSet<ObstacleId>();
        bool hasDuplicateIds = false;
        for (int d = 0; d < library.obstacles.Count; d++)
        {
            var def = library.obstacles[d];
            if (def == null) continue;
            if (!seenIds.Add(def.id))
            {
                hasDuplicateIds = true;
                break;
            }
        }

        if (hasDuplicateIds)
        {
            EditorGUILayout.HelpBox("ObstacleLibrary içinde duplicate ObstacleId var. Aynı Id birden fazla tanımlıysa ilk kayıt kullanılır; icon/BlocksCells beklenmedik görünebilir.", MessageType.Warning);
        }

        int perRow = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 40) / (paletteIcon + 8)));
        int i = 0;

        while (i < library.obstacles.Count)
        {
            EditorGUILayout.BeginHorizontal();
            for (int k = 0; k < perRow && i < library.obstacles.Count; k++, i++)
            {
                var def = library.obstacles[i];
                if (def == null) continue;

                bool isSel = (def.id == selectedObstacle);

                Rect r = GUILayoutUtility.GetRect(paletteIcon, paletteIcon, GUILayout.ExpandWidth(false));
                GUI.backgroundColor = isSel ? new Color(0.2f, 0.9f, 1f, 1f) : Color.white;

                if (GUI.Button(r, GUIContent.none))
                    selectedObstacle = def.id;

                GUI.backgroundColor = Color.white;

                DrawSpriteInRect(def.GetPreviewSprite(), r, 4);

                // küçük size etiketi
                var mini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerRight };
                GUI.Label(r, $"{def.size.x}x{def.size.y}", mini);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        var selDef = library.Get(selectedObstacle);
        if (selDef != null)
        {
            var stage0 = selDef.GetStageRuleForRemainingHits(selDef.hits);
            string stageInfo = stage0 == null
                ? "-"
                : $"BlocksCells: {stage0.blocksCells}  |  Behavior: {stage0.behavior}  |  AllowDiagonal: {stage0.allowDiagonal}";
            EditorGUILayout.HelpBox($"Selected: {selectedObstacle}  |  Size: {selDef.size.x}x{selDef.size.y}  |  Hits: {Mathf.Max(1, selDef.hits)}  |  {stageInfo}", MessageType.None);

            DrawSelectedObstacleStageEditor(library, selDef);
        }
    }

    private void DrawSelectedObstacleStageEditor(ObstacleLibrary library, ObstacleDef selDef)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Selected Obstacle Stage Rules", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Her stage tek satırda düzenlenir: Sprite + DamageRule + BlocksCells + Behavior + AllowDiagonal.", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        int newHits = Mathf.Max(1, EditorGUILayout.IntField("Hits", Mathf.Max(1, selDef.hits)));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(library, "Change Obstacle Hits");
            selDef.hits = newHits;
            selDef.EnsureStageSlots();
            EditorUtility.SetDirty(library);
        }

        selDef.EnsureStageSlots();

        for (int i = 0; i < selDef.stages.Count; i++)
        {
            var stage = selDef.stages[i];
            if (stage == null)
            {
                stage = new StageRule();
                selDef.stages[i] = stage;
            }

            string label = i == 0
                ? $"Stage {i} (Full HP)"
                : $"Stage {i} (After {i} hit{(i > 1 ? "s" : "")})";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);

            EditorGUI.BeginChangeCheck();
            stage.sprite = (Sprite)EditorGUILayout.ObjectField(stage.sprite, typeof(Sprite), false, GUILayout.MinWidth(120));
            stage.damageRule = (ObstacleDamageSourceRule)EditorGUILayout.EnumPopup(stage.damageRule, GUILayout.Width(110));
            stage.blocksCells = EditorGUILayout.ToggleLeft("Block", stage.blocksCells, GUILayout.Width(60));
            stage.behavior = (ObstacleBehaviorType)EditorGUILayout.EnumPopup(stage.behavior, GUILayout.Width(130));
            stage.allowDiagonal = EditorGUILayout.ToggleLeft("Diagonal", stage.allowDiagonal, GUILayout.Width(80));

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(library, "Edit Obstacle Stage Rule");
                EditorUtility.SetDirty(library);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawGrid(LevelData level)
    {
        EditorGUILayout.LabelField("Board", EditorStyles.boldLabel);

        Rect area = GUILayoutUtility.GetRect(level.width * cellPx + 12, level.height * cellPx + 12);
        EditorGUI.DrawRect(area, boardBg);

        float ox = area.x + 6;
        float oy = area.y + 6;

        // cells
        for (int y = 0; y < level.height; y++)
        {
            for (int x = 0; x < level.width; x++)
            {
                int idx = level.Index(x, y);
                Rect r = new Rect(ox + x * cellPx, oy + y * cellPx, cellPx - 1, cellPx - 1);

                bool isNormal = level.cells[idx] == (int)CellType.Normal;

                if (isNormal)
                    EditorGUI.DrawRect(r, normalCell);
                else
                    EditorGUI.DrawRect(r, holeCell);

                // obstacle overlay
                var obs = (ObstacleId)level.obstacles[idx];
                if (obs != ObstacleId.None)
                {
                    // origin değilse hafif overlay
                    if (level.obstacleOrigins[idx] != idx)
                        EditorGUI.DrawRect(r, occupiedOverlay);
                }

                // Click
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    ApplyPaint(level, x, y);
                    Event.current.Use();
                }
            }
        }

        // Draw obstacles sprites (origin cells only)
        var library = level.obstacleLibrary;
        for (int y = 0; y < level.height; y++)
        {
            for (int x = 0; x < level.width; x++)
            {
                int idx = level.Index(x, y);
                var obs = (ObstacleId)level.obstacles[idx];
                if (obs == ObstacleId.None) continue;

                // sadece origin hücre çizsin
                if (level.obstacleOrigins[idx] != idx) continue;

                var def = library != null ? library.Get(obs) : null;
                if (def == null || def.GetPreviewSprite() == null) continue;

                int w = Mathf.Max(1, def.size.x);
                int h = Mathf.Max(1, def.size.y);

                Rect big = new Rect(
                    ox + x * cellPx,
                    oy + y * cellPx,
                    w * cellPx - 1,
                    h * cellPx - 1
                );

                DrawSpriteInRect(def.GetPreviewSprite(), big, 2);
            }
        }

        // Grid lines
        Handles.BeginGUI();
        Handles.color = gridLine;
        for (int x = 0; x <= level.width; x++)
        {
            float px = ox + x * cellPx;
            Handles.DrawLine(new Vector3(px, oy), new Vector3(px, oy + level.height * cellPx));
        }
        for (int y = 0; y <= level.height; y++)
        {
            float py = oy + y * cellPx;
            Handles.DrawLine(new Vector3(ox, py), new Vector3(ox + level.width * cellPx, py));
        }
        Handles.EndGUI();
    }

    private void ApplyPaint(LevelData level, int x, int y)
    {
        int idx = level.Index(x, y);

        switch (mode)
        {
            case PaintMode.Mask:
                // toggle
                level.cells[idx] = (level.cells[idx] == (int)CellType.Normal) ? (int)CellType.Empty : (int)CellType.Normal;

                // empty yaptıysan obstacle temizle
                if (level.cells[idx] == (int)CellType.Empty)
                    ClearCell(level, idx);

                break;

            case PaintMode.Erase:
                ClearCell(level, idx);
                level.cells[idx] = (int)CellType.Normal;
                break;

            case PaintMode.Obstacle:
                StampObstacle(level, x, y, selectedObstacle);
                break;
        }
    }

    private void ClearCell(LevelData level, int idx)
    {
        level.obstacles[idx] = (int)ObstacleId.None;
        level.obstacleOrigins[idx] = -1;
    }

    private void StampObstacle(LevelData level, int ax, int ay, ObstacleId id)
    {
        var library = level.obstacleLibrary;
        var def = library != null ? library.Get(id) : null;
        Vector2Int size = def != null ? def.size : Vector2Int.one;

        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);

        // bounds
        if (!level.InBounds(ax, ay) || !level.InBounds(ax + w - 1, ay + h - 1))
            return;

        int originIdx = level.Index(ax, ay);

        // stamp all cells in area
        for (int y = ay; y < ay + h; y++)
        {
            for (int x = ax; x < ax + w; x++)
            {
                int idx = level.Index(x, y);
                level.obstacles[idx] = (int)id;
                level.obstacleOrigins[idx] = originIdx;

            }
        }

        // origin hücre: origins[idx] == idx olsun ki çizim tek yerde olsun
        level.obstacleOrigins[originIdx] = originIdx;
    }

    private void DrawSpriteInRect(Sprite sprite, Rect r, float padding)
    {
        if (sprite == null) return;

        Rect rr = new Rect(r.x + padding, r.y + padding, r.width - padding * 2, r.height - padding * 2);

        Texture2D tex = sprite.texture;
        Rect tr = sprite.textureRect;

        Rect uv = new Rect(
            tr.x / tex.width,
            tr.y / tex.height,
            tr.width / tex.width,
            tr.height / tex.height
        );

        GUI.DrawTextureWithTexCoords(rr, tex, uv, true);
    }
}
