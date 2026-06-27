using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private enum PaintMode { Mask, Obstacle, Tube, Magnet, Tiles, Safe, Overlay, Erase }

    private PaintMode mode = PaintMode.Obstacle;
    private ObstacleId selectedObstacle = ObstacleId.Stone;

    // Safe (kasa) settings — tıklanan hücre sol-üst origin; NxN bölgeyi kaplar.
    private int selectedSafeW = 2;
    private int selectedSafeH = 2;
    private int selectedSafeRed = 3;
    private int selectedSafeYellow = 3;
    private int selectedSafeGreen = 3;
    private SafeLockHitMode selectedSafeHitMode = SafeLockHitMode.Ordered;
    private SafeLockColor selectedSafeFirstLock = SafeLockColor.Red;
    private SafeLockColor selectedSafeSecondLock = SafeLockColor.Yellow;
    private SafeLockColor selectedSafeThirdLock = SafeLockColor.Green;
    private static readonly Color safeFillColor   = new Color(0.55f, 0.30f, 0.85f, 0.55f);
    private static readonly Color safeOriginColor = new Color(0.75f, 0.45f, 1.00f, 0.80f);
    private static readonly Color stackedFillColor   = new Color(0.95f, 0.55f, 0.15f, 0.45f);
    private static readonly Color stackedOriginColor = new Color(1.00f, 0.70f, 0.25f, 0.75f);

    // Tube settings
    private TubeDirection selectedTubeDir = TubeDirection.Up;
    private int selectedTubeLength = 3;

    // Magnet settings
    private readonly System.Collections.Generic.List<int> magnetPathBuilding = new();
    private static readonly Color magnetEndpointColor = new Color(0.15f, 0.45f, 1f, 0.90f);
    private static readonly Color magnetPathColor     = new Color(0.30f, 0.65f, 1f, 0.55f);
    private static readonly Color magnetBuildingColor = new Color(0.80f, 0.95f, 0.40f, 0.75f);

    // Tile pin settings  (-1 = rastgele, tile tipi pinlenmez)
    private int selectedPinTileType = -1;
    private TileSpecial selectedPinSpecial = TileSpecial.None;
    private static readonly Color pinnedTileColor    = new Color(1.00f, 0.85f, 0.10f, 0.80f);
    private static readonly Color pinnedSpecialColor = new Color(0.20f, 1.00f, 0.60f, 0.85f);
    private TileIconLibrary _cachedIconLib;

    private TileIconLibrary GetIconLibrary()
    {
        if (_cachedIconLib != null) return _cachedIconLib;
        var guids = UnityEditor.AssetDatabase.FindAssets("t:TileIconLibrary");
        if (guids.Length == 0) return null;
        var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
        _cachedIconLib = UnityEditor.AssetDatabase.LoadAssetAtPath<TileIconLibrary>(path);
        return _cachedIconLib;
    }

    private const int cellPx = 30;
    private const int paletteIcon = 44;

    private static readonly Color boardBg       = new Color(0.70f, 0.83f, 0.95f, 1f);
    private static readonly Color normalCell    = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color holeCell      = new Color(0.03f, 0.06f, 0.10f, 0.95f);
    private static readonly Color gridLine      = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color occupiedOverlay = new Color(0f, 0f, 0f, 0.12f);
    private static readonly Color tubeBaseColor = new Color(0.20f, 0.80f, 0.40f, 0.75f);
    private static readonly Color tubeBodyColor = new Color(0.15f, 0.65f, 0.30f, 0.55f);

    public override void OnInspectorGUI()
    {
        var level = (LevelData)target;

        DrawSettings(level);
        EditorGUILayout.Space(8);

        level.obstacleLibrary = (ObstacleLibrary)EditorGUILayout.ObjectField(
            "Obstacle Library",
            level.obstacleLibrary,
            typeof(ObstacleLibrary),
            false
        );

        EditorGUILayout.Space(6);

        mode = (PaintMode)GUILayout.Toolbar((int)mode, new[] { "Mask", "Obstacle", "Tube", "Magnet", "Tiles", "Safe", "Overlay", "Erase" });

        if (mode == PaintMode.Obstacle)
            DrawPalette(level);
        else if (mode == PaintMode.Tube)
            DrawTubePalette(level);
        else if (mode == PaintMode.Magnet)
            DrawMagnetPalette(level);
        else if (mode == PaintMode.Tiles)
            DrawTilePinPalette(level);
        else if (mode == PaintMode.Safe)
            DrawSafePalette(level);
        else if (mode == PaintMode.Overlay)
            DrawOverlayPalette(level);
        else
            EditorGUILayout.HelpBox("Mask: ilk tık hücreyi Empty (hole), ikinci tık veya Erase hücreyi Normal yapar.", MessageType.None);

        EnsureArrays(level);

        EditorGUILayout.Space(8);
        DrawGrid(level);

        if (GUI.changed)
            EditorUtility.SetDirty(level);
    }

    private void DrawSettings(LevelData level)
    {
        EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);
        level.width = EditorGUILayout.IntSlider("Width", level.width, LevelData.MinWidth, LevelData.MaxWidth);
        level.height = EditorGUILayout.IntSlider("Height", level.height, LevelData.MinHeight, LevelData.MaxHeight);
        EditorGUILayout.HelpBox($"Grid size limits: Width {LevelData.MinWidth}-{LevelData.MaxWidth}, Height {LevelData.MinHeight}-{LevelData.MaxHeight}.", MessageType.Info);
        level.moves = EditorGUILayout.IntField("Moves", level.moves);
        level.baseCoinReward = Mathf.Max(0, EditorGUILayout.IntField("Base Coin Reward", Mathf.Max(0, level.baseCoinReward)));
        DrawLevelKind(level);
        DrawRandomPool(level);
        DrawGoals(level);
        DrawEnergyContainerSettings(level);
        DrawAudio(level);
    }

    private void DrawEnergyContainerSettings(LevelData level)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Energy Container", EditorStyles.boldLabel);
        level.energyPerContainer = Mathf.Max(1, EditorGUILayout.IntField("Energy Per Container", Mathf.Max(1, level.energyPerContainer)));
        EditorGUILayout.HelpBox("Bu değer level bazlıdır. EnergyContainerRuntime sadece fallback olarak kalır.", MessageType.None);
    }

    private void DrawAudio(LevelData level)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
        level.musicClip = (AudioClip)EditorGUILayout.ObjectField("Music Clip", level.musicClip, typeof(AudioClip), false);
        level.musicVolume = EditorGUILayout.Slider("Music Volume", level.musicVolume, 0f, 1f);
    }

    private void DrawLevelKind(LevelData level)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Level Kind", EditorStyles.boldLabel);
        level.levelKind = (LevelKind)EditorGUILayout.EnumPopup("Kind", level.levelKind);

        level.usesCustomIntro = EditorGUILayout.Toggle(
            new GUIContent("Uses Custom Intro",
                "Açık: bu level'e girerken default loading screen yerine, iki parçanın soldan/sağdan " +
                "gelip ortada birleştiği özel intro 'load' olur (sahne async yüklenirken oynar). " +
                "Kapalı VEYA iki sprite'tan biri boşsa default load çalışır."),
            level.usesCustomIntro);

        if (level.usesCustomIntro)
        {
            EditorGUI.indentLevel++;
            level.introLeftSprite = (Sprite)EditorGUILayout.ObjectField(
                "Left Sprite (soldan)", level.introLeftSprite, typeof(Sprite), false);
            level.introRightSprite = (Sprite)EditorGUILayout.ObjectField(
                "Right Sprite (sağdan)", level.introRightSprite, typeof(Sprite), false);
            level.introSlideInDuration = Mathf.Max(0.05f,
                EditorGUILayout.FloatField("Slide In Duration (sn)", level.introSlideInDuration));
            level.introHoldDuration = Mathf.Max(0f,
                EditorGUILayout.FloatField("Hold Duration (sn)", level.introHoldDuration));
            if (level.introLeftSprite == null || level.introRightSprite == null)
                EditorGUILayout.HelpBox("İki sprite de atanmalı; biri boşsa default load çalışır.", MessageType.Info);
            EditorGUI.indentLevel--;
        }

        if (level.levelKind != LevelKind.BossDuel)
            return;

        EditorGUILayout.LabelField("Battlefield", EditorStyles.miniBoldLabel);
        level.playerMaxHp = Mathf.Max(1, EditorGUILayout.IntField("Player Max HP (yeşil bar)", Mathf.Max(1, level.playerMaxHp)));
        level.damagePerClearedTile = Mathf.Max(0, EditorGUILayout.IntField("Damage Per Cleared Tile", Mathf.Max(0, level.damagePerClearedTile)));
        level.enemyAttackBaseDamage = Mathf.Max(0, EditorGUILayout.IntField("Enemy Base Damage", Mathf.Max(0, level.enemyAttackBaseDamage)));
        level.enemyAttackDamageGrowth = Mathf.Max(0, EditorGUILayout.IntField("Enemy Damage Growth / Attack", Mathf.Max(0, level.enemyAttackDamageGrowth)));
        level.enemyAttackInterval = Mathf.Max(0f, EditorGUILayout.FloatField("Enemy Attack Interval (sn, 0=default)", level.enemyAttackInterval));
        level.battlefieldBackground = (Sprite)EditorGUILayout.ObjectField("Arena Background (boş=mevcut)", level.battlefieldBackground, typeof(Sprite), false);

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Oil (opsiyonel baskı)", EditorStyles.miniBoldLabel);
        level.bossAttackEveryMoves = Mathf.Max(1, EditorGUILayout.IntField("Oil Every N Turns", Mathf.Max(1, level.bossAttackEveryMoves)));
        level.bossAttackOilCount = Mathf.Max(0, EditorGUILayout.IntField("Oil Per Attack (0 = kapalı)", Mathf.Max(0, level.bossAttackOilCount)));

        bool hasBossGoal = false;
        if (level.goals != null)
        {
            foreach (var g in level.goals)
            {
                if (g != null && g.targetType == LevelGoalTargetType.Collectible && g.collectibleId == CollectibleId.BossDamage)
                {
                    hasBossGoal = true;
                    break;
                }
            }
        }

        if (hasBossGoal)
        {
            EditorGUILayout.HelpBox(
                "Boss Duel hazır: BossDamage goal'ü boss HP'sini tanımlıyor. " +
                "Sahnede BossDuelController bağlı olmalı.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Boss HP tanımlı değil! Goals'a şunu ekle: Target Type = Collectible, " +
                "Collectible = BossDamage, Amount = boss HP (örn. 150), Icon Override = boss ikonu.",
                MessageType.Warning);
        }
    }

    // Level havuzuna seçilebilecek temel taş tipleri. Yeni renk eklenirse buraya da ekle.
    private static readonly TileType[] RandomPoolCandidates =
    {
        TileType.Gear, TileType.Core, TileType.Bolt, TileType.Plate
    };

    private void DrawRandomPool(LevelData level)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Random Pool (Level Override)", EditorStyles.boldLabel);

        if (level.randomPool == null)
            level.randomPool = System.Array.Empty<TileType>();

        var selected = new System.Collections.Generic.HashSet<TileType>(level.randomPool);

        bool changed = false;
        EditorGUILayout.BeginHorizontal();
        foreach (var type in RandomPoolCandidates)
        {
            bool was = selected.Contains(type);
            bool now = GUILayout.Toggle(was, type.ToString());
            if (now != was)
            {
                changed = true;
                if (now) selected.Add(type);
                else selected.Remove(type);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (changed)
        {
            // Aday sırasını koruyarak diziye yaz (deterministik asset diff'i için).
            var list = new System.Collections.Generic.List<TileType>();
            foreach (var type in RandomPoolCandidates)
                if (selected.Contains(type))
                    list.Add(type);
            level.randomPool = list.ToArray();
        }

        if (level.randomPool.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "Hiçbiri seçili değil: GridSpawner'daki varsayılan havuz kullanılır. " +
                "Seçim yapılırsa bu levelda YALNIZCA seçilenler üretilir.",
                MessageType.None);
        }
        else if (level.randomPool.Length < 3)
        {
            EditorGUILayout.HelpBox(
                $"Sadece {level.randomPool.Length} tip seçili — match-3 akışı için en az 3 tip önerilir " +
                "(az tip = sürekli istemsiz match riski).",
                MessageType.Warning);
        }
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
            switch (goal.targetType)
            {
                case LevelGoalTargetType.Tile:
                    goal.tileType = (TileType)EditorGUILayout.EnumPopup("Tile", goal.tileType);
                    break;
                case LevelGoalTargetType.Obstacle:
                    goal.obstacleId = (ObstacleId)EditorGUILayout.EnumPopup("Obstacle", goal.obstacleId);
                    break;
                case LevelGoalTargetType.Collectible:
                    goal.collectibleId = (CollectibleId)EditorGUILayout.EnumPopup("Collectible", goal.collectibleId);
                    break;
            }

            goal.iconOverride = (Sprite)EditorGUILayout.ObjectField("Icon Override", goal.iconOverride, typeof(Sprite), false);
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
            var list = new System.Collections.Generic.List<LevelGoalDefinition>(level.goals) { new LevelGoalDefinition() };
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
        if (level.tubes == null)
            level.tubes = System.Array.Empty<TubeEntry>();

        if (level.magnets == null)
            level.magnets = System.Array.Empty<MagnetEntry>();

        if (level.safes == null)
            level.safes = System.Array.Empty<SafeEntry>();

        if (level.stackedObstacles == null)
            level.stackedObstacles = System.Array.Empty<StackedObstacleEntry>();

        if (level.pinnedTileTypes == null || level.pinnedTileTypes.Length != size)
        {
            var old = level.pinnedTileTypes;
            level.pinnedTileTypes = new int[size];
            if (old != null) System.Array.Copy(old, level.pinnedTileTypes, Mathf.Min(size, old.Length));
        }

        if (level.pinnedSpecialTypes == null || level.pinnedSpecialTypes.Length != size)
        {
            var old = level.pinnedSpecialTypes;
            level.pinnedSpecialTypes = new int[size];
            if (old != null) System.Array.Copy(old, level.pinnedSpecialTypes, Mathf.Min(size, old.Length));
        }
    }

    private void ValidateUnknownObstacles(LevelData level)
    {
        var library = level.obstacleLibrary;
        if (library == null || level.obstacles == null)
            return;
        for (int i = 0; i < level.obstacles.Length; i++)
        {
            var id = (ObstacleId)level.obstacles[i];
            if (id == ObstacleId.None) continue;
            if (library.Get(id) == null)
                Debug.LogWarning($"{level.name}: Unknown obstacle id {id} ({(int)id}) at index {i}. It will not render in editor and may not block cascade correctly.", level);
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
            if (!seenIds.Add(def.id)) { hasDuplicateIds = true; break; }
        }
        if (hasDuplicateIds)
            EditorGUILayout.HelpBox("ObstacleLibrary içinde duplicate ObstacleId var. Aynı Id birden fazla tanımlıysa ilk kayıt kullanılır; icon/BlocksCells beklenmedik görünebilir.", MessageType.Warning);

        int perRow = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 40) / (paletteIcon + 8)));
        int i = 0;
        while (i < library.obstacles.Count)
        {
            EditorGUILayout.BeginHorizontal();
            for (int k = 0; k < perRow && i < library.obstacles.Count; k++, i++)
            {
                var def = library.obstacles[i];
                if (def == null) continue;
                bool isSel = def.id == selectedObstacle;
                Rect r = GUILayoutUtility.GetRect(paletteIcon, paletteIcon, GUILayout.ExpandWidth(false));
                GUI.backgroundColor = isSel ? new Color(0.2f, 0.9f, 1f, 1f) : Color.white;
                if (GUI.Button(r, GUIContent.none)) selectedObstacle = def.id;
                GUI.backgroundColor = Color.white;
                DrawSpriteInRect(def.GetPreviewSprite(), r, 4);
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
            string stageInfo = stage0 == null ? "-" : $"BlocksCells: {stage0.blocksCells}  |  Behavior: {stage0.behavior}  |  AllowDiagonal: {stage0.allowDiagonal}";
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
            if (stage == null) { stage = new StageRule(); selDef.stages[i] = stage; }
            string label = i == 0 ? $"Stage {i} (Full HP)" : $"Stage {i} (After {i} hit{(i > 1 ? "s" : "")})";
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

        bool IsNormal(int x, int y)
        {
            if (x < 0 || x >= level.width || y < 0 || y >= level.height) return false;
            int idx = level.Index(x, y);
            return level.cells[idx] == (int)CellType.Normal;
        }

        for (int y = 0; y < level.height; y++)
        for (int x = 0; x < level.width; x++)
        {
            int idx = level.Index(x, y);
            Rect r = new Rect(ox + x * cellPx, oy + y * cellPx, cellPx - 1, cellPx - 1);
            bool isNormal = level.cells[idx] == (int)CellType.Normal;
            EditorGUI.DrawRect(r, isNormal ? normalCell : holeCell);
            var obs = (ObstacleId)level.obstacles[idx];
            if (isNormal && obs != ObstacleId.None && level.obstacleOrigins[idx] != idx)
                EditorGUI.DrawRect(r, occupiedOverlay);
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                Undo.RecordObject(level, $"Paint Level {mode}");
                ApplyPaint(level, x, y);
                EditorUtility.SetDirty(level);
                GUI.changed = true;
                Event.current.Use();
            }
        }

        var library = level.obstacleLibrary;
        for (int y = 0; y < level.height; y++)
        for (int x = 0; x < level.width; x++)
        {
            int idx = level.Index(x, y);
            if (level.cells[idx] != (int)CellType.Normal) continue;
            var obs = (ObstacleId)level.obstacles[idx];
            if (obs == ObstacleId.None || level.obstacleOrigins[idx] != idx) continue;
            var def = library != null ? library.Get(obs) : null;
            if (def == null || def.GetPreviewSprite() == null) continue;
            int w = Mathf.Max(1, def.size.x);
            int h = Mathf.Max(1, def.size.y);
            Rect big = new Rect(ox + x * cellPx, oy + y * cellPx, w * cellPx - 1, h * cellPx - 1);
            DrawSpriteInRect(def.GetPreviewSprite(), big, 2);
        }

        // Draw pinned tile overlays
        if (level.pinnedTileTypes != null || level.pinnedSpecialTypes != null)
        {
            for (int y = 0; y < level.height; y++)
            for (int x = 0; x < level.width; x++)
            {
                int idx = level.Index(x, y);
                Rect r = new Rect(ox + x * cellPx, oy + y * cellPx, cellPx - 1, cellPx - 1);

                bool hasPin     = level.pinnedTileTypes   != null && idx < level.pinnedTileTypes.Length   && level.pinnedTileTypes[idx]   > 0;
                bool hasSpecial = level.pinnedSpecialTypes != null && idx < level.pinnedSpecialTypes.Length && level.pinnedSpecialTypes[idx] > 0;

                if (hasSpecial)
                    EditorGUI.DrawRect(r, pinnedSpecialColor);
                else if (hasPin)
                    EditorGUI.DrawRect(r, pinnedTileColor);

                if (hasPin || hasSpecial)
                {
                    var iconLib = GetIconLibrary();
                    float half = r.width * 0.5f;
                    if (hasPin && hasSpecial)
                    {
                        // Sol yarı = tile tipi, sağ yarı = special
                        Rect leftR  = new Rect(r.x,          r.y, half, r.height);
                        Rect rightR = new Rect(r.x + half,   r.y, half, r.height);
                        var t = (TileType)(level.pinnedTileTypes[idx] - 1);
                        var s = (TileSpecial)level.pinnedSpecialTypes[idx];
                        Sprite tSprite = iconLib != null ? iconLib.Get(t)             : null;
                        Sprite sSprite = iconLib != null ? iconLib.GetSpecialIcon(s)  : null;
                        if (tSprite != null) DrawSpriteInRect(tSprite, leftR,  2);
                        else GUI.Label(leftR,  t.ToString().Substring(0, 2), new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 7, normal = { textColor = Color.black } });
                        if (sSprite != null) DrawSpriteInRect(sSprite, rightR, 2);
                        else GUI.Label(rightR, s.ToString().Substring(0, 2), new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 7, normal = { textColor = Color.black } });
                    }
                    else if (hasPin)
                    {
                        var t = (TileType)(level.pinnedTileTypes[idx] - 1);
                        Sprite tSprite = iconLib != null ? iconLib.Get(t) : null;
                        if (tSprite != null) DrawSpriteInRect(tSprite, r, 3);
                        else GUI.Label(r, t.ToString().Substring(0, 2), new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 8, normal = { textColor = Color.black } });
                    }
                    else
                    {
                        var s = (TileSpecial)level.pinnedSpecialTypes[idx];
                        Sprite sSprite = iconLib != null ? iconLib.GetSpecialIcon(s) : null;
                        if (sSprite != null) DrawSpriteInRect(sSprite, r, 3);
                        else GUI.Label(r, s.ToString().Substring(0, 2), new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 8, normal = { textColor = Color.black } });
                    }
                }
            }
        }

        // Draw magnet overlays
        if (level.magnets != null)
        {
            foreach (var entry in level.magnets)
            {
                if (entry.pathCellIndices == null || entry.pathCellIndices.Length < 2) continue;
                for (int mi = 0; mi < entry.pathCellIndices.Length; mi++)
                {
                    int ci = entry.pathCellIndices[mi];
                    if (ci < 0 || ci >= level.obstacles.Length) continue;
                    int cx = ci % level.width;
                    int cy = ci / level.width;
                    Rect mr = new Rect(ox + cx * cellPx, oy + cy * cellPx, cellPx - 1, cellPx - 1);
                    bool isEndpoint = mi == 0 || mi == entry.pathCellIndices.Length - 1;
                    EditorGUI.DrawRect(mr, isEndpoint ? magnetEndpointColor : magnetPathColor);
                    if (isEndpoint)
                        GUI.Label(mr, mi == 0 ? "A" : "B", new GUIStyle(EditorStyles.boldLabel)
                            { alignment = TextAnchor.MiddleCenter, fontSize = 9, normal = { textColor = Color.white } });
                }
            }
        }

        // Draw building magnet path
        for (int bi = 0; bi < magnetPathBuilding.Count; bi++)
        {
            int ci = magnetPathBuilding[bi];
            if (ci < 0 || ci >= level.obstacles.Length) continue;
            int cx = ci % level.width;
            int cy = ci / level.width;
            Rect mr = new Rect(ox + cx * cellPx, oy + cy * cellPx, cellPx - 1, cellPx - 1);
            EditorGUI.DrawRect(mr, magnetBuildingColor);
            GUI.Label(mr, bi == 0 ? "A" : bi.ToString(), new GUIStyle(EditorStyles.boldLabel)
                { alignment = TextAnchor.MiddleCenter, fontSize = 9, normal = { textColor = Color.black } });
        }

        // Draw tube overlays
        if (level.tubes != null)
        {
            for (int t = 0; t < level.tubes.Length; t++)
            {
                var entry = level.tubes[t];
                int[] cells = TubeObstacleService.GetCellIndices(entry, level.width, level.height);
                if (cells == null) continue;

                for (int ci = 0; ci < cells.Length; ci++)
                {
                    int cx = cells[ci] % level.width;
                    int cy = cells[ci] / level.width;
                    Rect tr = new Rect(ox + cx * cellPx, oy + cy * cellPx, cellPx - 1, cellPx - 1);
                    Color col = (ci == 0) ? tubeBaseColor : tubeBodyColor;
                    EditorGUI.DrawRect(tr, col);

                    // Direction arrow on base cell
                    if (ci == 0)
                    {
                        string arrow = entry.direction switch
                        {
                            TubeDirection.Up    => "▲",
                            TubeDirection.Down  => "▼",
                            TubeDirection.Left  => "◄",
                            TubeDirection.Right => "►",
                            _                   => "?"
                        };
                        GUI.Label(tr, arrow + $"{entry.length}", new GUIStyle(EditorStyles.boldLabel)
                        {
                            alignment  = TextAnchor.MiddleCenter,
                            fontSize   = 9,
                            normal     = { textColor = Color.white }
                        });
                    }
                }
            }
        }

        // Draw safe (kasa) region overlays
        if (level.safes != null)
        {
            for (int s = 0; s < level.safes.Length; s++)
            {
                var e = level.safes[s];
                int sox = e.originCellIndex % level.width;
                int soy = e.originCellIndex / level.width;
                int sw = Mathf.Max(1, e.width), sh = Mathf.Max(1, e.height);

                for (int r = 0; r < sh; r++)
                for (int c = 0; c < sw; c++)
                {
                    int cx = sox + c, cy = soy + r;
                    if (cx >= level.width || cy >= level.height) continue;
                    Rect sr = new Rect(ox + cx * cellPx, oy + cy * cellPx, cellPx - 1, cellPx - 1);
                    EditorGUI.DrawRect(sr, (c == 0 && r == 0) ? safeOriginColor : safeFillColor);
                }

                Rect lr = new Rect(ox + sox * cellPx, oy + soy * cellPx, cellPx - 1, cellPx - 1);
                GUI.Label(lr, $"🔒{e.redHits}/{e.yellowHits}/{e.greenHits}", new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize  = 8,
                    normal    = { textColor = Color.white }
                });
            }
        }

        // Draw stacked-obstacle (overlay) markers. ÖNEMLİ: hücrenin TAMAMINI kaplamayız —
        // altındaki authored obstacle (Mud, Oil...) görünür kalsın diye sadece ÜST-SAĞ köşeye
        // bir rozet + origin köşesine küçük obstacle ikonu çizeriz. Veride alttaki içerik EZİLMEZ.
        if (level.stackedObstacles != null)
        {
            var lib = level.obstacleLibrary;
            for (int s = 0; s < level.stackedObstacles.Length; s++)
            {
                var e = level.stackedObstacles[s];
                if (e.obstacleId == ObstacleId.None) continue;
                var def = lib != null ? lib.Get(e.obstacleId) : null;
                int sox = e.originCellIndex % level.width;
                int soy = e.originCellIndex / level.width;
                int sw = def != null ? Mathf.Max(1, def.size.x) : 1;
                int sh = def != null ? Mathf.Max(1, def.size.y) : 1;

                float band = cellPx * 0.40f;
                for (int r = 0; r < sh; r++)
                for (int c = 0; c < sw; c++)
                {
                    int cx = sox + c, cy = soy + r;
                    if (cx >= level.width || cy >= level.height) continue;
                    float bx = ox + cx * cellPx;
                    float by = oy + cy * cellPx;
                    Rect badge = new Rect(bx + cellPx - 1 - band, by, band, band);
                    EditorGUI.DrawRect(badge, (c == 0 && r == 0) ? stackedOriginColor : stackedFillColor);
                }

                // Origin'in üst-sol köşesine üstteki obstacle'ın mini ikonu.
                Rect lr = new Rect(ox + sox * cellPx, oy + soy * cellPx, cellPx * 0.55f, cellPx * 0.55f);
                if (def != null) DrawSpriteInRect(def.GetPreviewSprite(), lr, 1);
            }
        }

        Handles.BeginGUI();
        Handles.color = gridLine;
        for (int y = 0; y < level.height; y++)
        for (int x = 0; x < level.width; x++)
        {
            if (!IsNormal(x, y)) continue;
            float x0 = ox + x * cellPx;
            float y0 = oy + y * cellPx;
            float x1 = x0 + cellPx;
            float y1 = y0 + cellPx;
            Handles.DrawLine(new Vector3(x0, y0), new Vector3(x1, y0));
            Handles.DrawLine(new Vector3(x0, y0), new Vector3(x0, y1));
            if (!IsNormal(x + 1, y)) Handles.DrawLine(new Vector3(x1, y0), new Vector3(x1, y1));
            if (!IsNormal(x, y + 1)) Handles.DrawLine(new Vector3(x0, y1), new Vector3(x1, y1));
        }
        Handles.EndGUI();
    }

    private void ApplyPaint(LevelData level, int x, int y)
    {
        int idx = level.Index(x, y);
        switch (mode)
        {
            case PaintMode.Mask:
                level.cells[idx] = level.cells[idx] == (int)CellType.Normal ? (int)CellType.Empty : (int)CellType.Normal;
                if (level.cells[idx] == (int)CellType.Empty) ClearCell(level, idx);
                break;
            case PaintMode.Erase:
                ClearCell(level, idx);
                RemoveTubeAtCell(level, idx);
                RemoveMagnetAtCell(level, idx);
                RemoveSafeAtCell(level, idx);
                RemoveStackedAtCell(level, idx);
                magnetPathBuilding.Clear();
                ClearPinnedTile(level, idx);
                level.cells[idx] = (int)CellType.Normal;
                break;
            case PaintMode.Obstacle:
                StampObstacle(level, x, y, selectedObstacle);
                break;
            case PaintMode.Tube:
                PlaceTube(level, x, y);
                break;
            case PaintMode.Magnet:
                AddMagnetPathCell(level, idx);
                break;
            case PaintMode.Tiles:
                PaintPinnedTile(level, idx);
                break;
            case PaintMode.Safe:
                PlaceSafe(level, x, y);
                break;
            case PaintMode.Overlay:
                PlaceStackedObstacle(level, x, y);
                break;
        }
    }

    // Model (a): içerik AYRI yerleştirilir (Obstacle/Tiles/Mask modu); Safe sadece bir overlay
    // bölgesi (origin + boyut + kilit hit'leri) safes[]'e eklenir. Altındaki içerik EZİLMEZ —
    // runtime'da GridSpawner.StampSafeCellsIntoLevel kaydedip kaplar, kırılınca geri yükler.
    private void PlaceSafe(LevelData level, int bx, int by)
    {
        if (!level.InBounds(bx, by)) return;
        int originIdx = level.Index(bx, by);

        int w = Mathf.Max(1, selectedSafeW);
        int h = Mathf.Max(1, selectedSafeH);
        if (bx + w > level.width || by + h > level.height)
        {
            Debug.LogWarning($"[SafeEditor] Safe at ({bx},{by}) {w}x{h} grid dışına taşıyor.");
            return;
        }

        // Aynı origin'de varsa değiştir.
        RemoveSafeAtCell(level, originIdx);

        var entry = new SafeEntry
        {
            originCellIndex = originIdx,
            width           = w,
            height          = h,
            redHits         = Mathf.Max(1, selectedSafeRed),
            yellowHits      = Mathf.Max(1, selectedSafeYellow),
            greenHits       = Mathf.Max(1, selectedSafeGreen),
            lockHitMode     = selectedSafeHitMode,
            firstLock       = selectedSafeFirstLock,
            secondLock      = selectedSafeSecondLock,
            thirdLock       = selectedSafeThirdLock
        };

        var list = new System.Collections.Generic.List<SafeEntry>(level.safes ?? System.Array.Empty<SafeEntry>()) { entry };
        level.safes = list.ToArray();
    }

    private void RemoveSafeAtCell(LevelData level, int cellIndex)
    {
        if (level.safes == null || level.safes.Length == 0) return;

        int W = level.width;
        var list = new System.Collections.Generic.List<SafeEntry>(level.safes);
        for (int s = list.Count - 1; s >= 0; s--)
        {
            var e = list[s];
            int ox = e.originCellIndex % W, oy = e.originCellIndex / W;
            int cx = cellIndex % W, cy = cellIndex / W;
            if (cx >= ox && cx < ox + Mathf.Max(1, e.width) &&
                cy >= oy && cy < oy + Mathf.Max(1, e.height))
                list.RemoveAt(s);
        }
        level.safes = list.ToArray();
    }

    private void DrawOverlayPalette(LevelData level)
    {
        EditorGUILayout.HelpBox(
            "Overlay (stacked obstacle): seçili obstacle, altındaki AUTHORED içeriğin (Obstacle modunda " +
            "boyadığın Mud/Stone vb.) ÜSTÜNE konur. Üstteki kırılınca alttaki geri açılır. Safe ile aynı " +
            "beneath mekanizması — örn. Chest'i bir Mud'ın üstüne koymak için: önce Obstacle modunda Mud " +
            "boya, sonra burada Chest seçip aynı hücreye tıkla. Boyut obstacle'ın kendi def.size'ından gelir.",
            MessageType.Info);
        // Üste konacak obstacle, normal obstacle paleti ile seçilir (selectedObstacle paylaşılır).
        DrawPalette(level);
    }

    // Overlay modu: stackedObstacles[]'a bir entry ekler (origin + obstacleId). Altındaki içerik
    // EZİLMEZ — runtime'da GridSpawner.StampStackedObstaclesIntoLevel kaydedip kaplar, kırılınca
    // ObstacleStateService geri yükler. Safe'in generic karşılığı.
    private void PlaceStackedObstacle(LevelData level, int bx, int by)
    {
        if (!level.InBounds(bx, by)) return;
        if (selectedObstacle == ObstacleId.None) return;

        int originIdx = level.Index(bx, by);
        var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(selectedObstacle) : null;
        int w = def != null ? Mathf.Max(1, def.size.x) : 1;
        int h = def != null ? Mathf.Max(1, def.size.y) : 1;
        if (bx + w > level.width || by + h > level.height)
        {
            Debug.LogWarning($"[OverlayEditor] Stacked {selectedObstacle} at ({bx},{by}) {w}x{h} grid dışına taşıyor.");
            return;
        }

        // Aynı origin'de varsa değiştir.
        RemoveStackedAtCell(level, originIdx);

        var entry = new StackedObstacleEntry
        {
            originCellIndex = originIdx,
            obstacleId      = selectedObstacle
        };

        var list = new System.Collections.Generic.List<StackedObstacleEntry>(
            level.stackedObstacles ?? System.Array.Empty<StackedObstacleEntry>()) { entry };
        level.stackedObstacles = list.ToArray();
    }

    private void RemoveStackedAtCell(LevelData level, int cellIndex)
    {
        if (level.stackedObstacles == null || level.stackedObstacles.Length == 0) return;

        int W = level.width;
        var lib = level.obstacleLibrary;
        var list = new System.Collections.Generic.List<StackedObstacleEntry>(level.stackedObstacles);
        for (int s = list.Count - 1; s >= 0; s--)
        {
            var e = list[s];
            var def = lib != null ? lib.Get(e.obstacleId) : null;
            int ox = e.originCellIndex % W, oy = e.originCellIndex / W;
            int w = def != null ? Mathf.Max(1, def.size.x) : 1;
            int h = def != null ? Mathf.Max(1, def.size.y) : 1;
            int cx = cellIndex % W, cy = cellIndex / W;
            if (cx >= ox && cx < ox + w && cy >= oy && cy < oy + h)
                list.RemoveAt(s);
        }
        level.stackedObstacles = list.ToArray();
    }

    private void PlaceTube(LevelData level, int bx, int by)
    {
        int originIdx = level.Index(bx, by);
        if (!level.InBounds(bx, by)) return;
        if (level.cells[originIdx] != (int)CellType.Normal) return;

        // Remove existing tube at this cell if any
        RemoveTubeAtCell(level, originIdx);

        var entry = new TubeEntry
        {
            originCellIndex = originIdx,
            direction       = selectedTubeDir,
            length          = Mathf.Max(2, selectedTubeLength)
        };

        // Validate all cells in bounds and not occupied by other obstacles
        int[] cells = TubeObstacleService.GetCellIndices(entry, level.width, level.height);
        if (cells == null)
        {
            Debug.LogWarning($"[TubeEditor] Tube at ({bx},{by}) dir={selectedTubeDir} len={selectedTubeLength} goes out of bounds.");
            return;
        }
        foreach (int ci in cells)
        {
            if (level.cells[ci] != (int)CellType.Normal)
            {
                Debug.LogWarning($"[TubeEditor] Tube cell {ci} is not a normal cell.");
                return;
            }
            // Check for conflicting obstacles (not tubes – those were already removed)
            var existingObs = (ObstacleId)level.obstacles[ci];
            if (existingObs != ObstacleId.None && existingObs != ObstacleId.Tube)
            {
                Debug.LogWarning($"[TubeEditor] Tube cell {ci} already has obstacle {existingObs}.");
                return;
            }
        }

        var list = new System.Collections.Generic.List<TubeEntry>(level.tubes) { entry };
        level.tubes = list.ToArray();
    }

    private void RemoveTubeAtCell(LevelData level, int cellIndex)
    {
        if (level.tubes == null || level.tubes.Length == 0) return;

        var list = new System.Collections.Generic.List<TubeEntry>(level.tubes);
        for (int t = list.Count - 1; t >= 0; t--)
        {
            int[] cells = TubeObstacleService.GetCellIndices(list[t], level.width, level.height);
            if (cells == null) continue;
            bool found = false;
            foreach (int ci in cells) if (ci == cellIndex) { found = true; break; }
            if (found) list.RemoveAt(t);
        }
        level.tubes = list.ToArray();
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
        if (!level.InBounds(ax, ay) || !level.InBounds(ax + w - 1, ay + h - 1)) return;
        for (int y = ay; y < ay + h; y++)
        for (int x = ax; x < ax + w; x++)
        {
            int idx = level.Index(x, y);
            if (level.cells[idx] != (int)CellType.Normal) return;
        }
        int originIdx = level.Index(ax, ay);
        for (int y = ay; y < ay + h; y++)
        for (int x = ax; x < ax + w; x++)
        {
            int idx = level.Index(x, y);
            level.obstacles[idx] = (int)id;
            level.obstacleOrigins[idx] = originIdx;
        }
        level.obstacleOrigins[originIdx] = originIdx;
    }

    private void DrawTilePinPalette(LevelData level)
    {
        EditorGUILayout.LabelField("Tile Sabitleme", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Taş tipi + special seç → grid'de hücreye tıkla.\n" +
            "Sarı = sabit normal taş  |  Yeşil = sabit special  |  Erase → temizle",
            MessageType.Info);

        var lib = GetIconLibrary();

        // ── Normal Taş ───────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Normal Taş", EditorStyles.boldLabel);
        var tileTypes = new[] { TileType.Gear, TileType.Core, TileType.Bolt, TileType.Plate };
        EditorGUILayout.BeginHorizontal();
        foreach (var t in tileTypes)
        {
            bool isSel = selectedPinTileType == (int)t && selectedPinSpecial == TileSpecial.None;
            GUI.backgroundColor = isSel ? new Color(1f, 0.85f, 0.1f) : Color.white;
            Rect r = GUILayoutUtility.GetRect(paletteIcon, paletteIcon, GUILayout.ExpandWidth(false));
            if (GUI.Button(r, GUIContent.none))
            {
                selectedPinTileType = (int)t;
                selectedPinSpecial  = TileSpecial.None;
            }
            GUI.backgroundColor = Color.white;
            Sprite icon = lib != null ? lib.Get(t) : null;
            if (icon != null) DrawSpriteInRect(icon, r, 3);
            else              GUI.Label(r, t.ToString().Substring(0, 2), new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── Special ──────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Special", EditorStyles.boldLabel);
        var specials = new[] { TileSpecial.LineH, TileSpecial.LineV, TileSpecial.PulseCore, TileSpecial.PatchBot, TileSpecial.SystemOverride };
        EditorGUILayout.BeginHorizontal();
        foreach (var s in specials)
        {
            bool isSel = selectedPinSpecial == s;
            GUI.backgroundColor = isSel ? new Color(0.2f, 1f, 0.6f) : Color.white;
            Rect r = GUILayoutUtility.GetRect(paletteIcon, paletteIcon, GUILayout.ExpandWidth(false));
            if (GUI.Button(r, GUIContent.none))
            {
                selectedPinSpecial  = s;
                selectedPinTileType = -1;
            }
            GUI.backgroundColor = Color.white;
            Sprite icon = lib != null ? lib.GetSpecialIcon(s) : null;
            if (icon != null) DrawSpriteInRect(icon, r, 3);
            else GUI.Label(r, s.ToString().Substring(0, Mathf.Min(3, s.ToString().Length)), new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 8 });
        }
        EditorGUILayout.EndHorizontal();

        // ── SystemOverride renk seçimi ────────────────────────────────────────
        if (selectedPinSpecial == TileSpecial.SystemOverride)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Override Rengi", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (var t in tileTypes)
            {
                bool isSel = selectedPinTileType == (int)t;
                GUI.backgroundColor = isSel ? new Color(1f, 0.85f, 0.1f) : Color.white;
                Rect r = GUILayoutUtility.GetRect(paletteIcon, paletteIcon, GUILayout.ExpandWidth(false));
                if (GUI.Button(r, GUIContent.none)) selectedPinTileType = (int)t;
                GUI.backgroundColor = Color.white;
                Sprite icon = lib != null ? lib.Get(t) : null;
                if (icon != null) DrawSpriteInRect(icon, r, 3);
                else              GUI.Label(r, t.ToString().Substring(0, 2), new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            }
            EditorGUILayout.EndHorizontal();
            if (selectedPinTileType < 0)
                EditorGUILayout.HelpBox("Override rengi seçilmeli.", MessageType.Warning);
        }

        EditorGUILayout.Space(6);

        // ── İstatistik & Temizle ─────────────────────────────────────────────
        int pinCount = 0;
        if (level.pinnedTileTypes != null)
            foreach (var v in level.pinnedTileTypes) if (v > 0) pinCount++;
        EditorGUILayout.LabelField($"Sabitlenmiş hücre: {pinCount}", EditorStyles.miniLabel);

        if (pinCount > 0 && GUILayout.Button("Tüm pinleri temizle"))
        {
            Undo.RecordObject(level, "Clear All Pinned Tiles");
            if (level.pinnedTileTypes != null)   System.Array.Clear(level.pinnedTileTypes,   0, level.pinnedTileTypes.Length);
            if (level.pinnedSpecialTypes != null) System.Array.Clear(level.pinnedSpecialTypes, 0, level.pinnedSpecialTypes.Length);
            EditorUtility.SetDirty(level);
        }
    }

    private void PaintPinnedTile(LevelData level, int idx)
    {
        if (idx < 0) return;
        Undo.RecordObject(level, "Pin Tile");

        if (selectedPinSpecial == TileSpecial.SystemOverride)
        {
            // Override: hem special hem renk (tile tipi) birlikte gerekli
            if (selectedPinTileType < 0) return; // renk seçilmeden yerleştirme
            if (level.pinnedSpecialTypes != null && idx < level.pinnedSpecialTypes.Length)
                level.pinnedSpecialTypes[idx] = (int)selectedPinSpecial;
            if (level.pinnedTileTypes != null && idx < level.pinnedTileTypes.Length)
                level.pinnedTileTypes[idx] = selectedPinTileType + 1;
        }
        else if (selectedPinSpecial != TileSpecial.None)
        {
            // Diğer special'lar: sadece special, tile tipini temizle
            if (level.pinnedSpecialTypes != null && idx < level.pinnedSpecialTypes.Length)
                level.pinnedSpecialTypes[idx] = (int)selectedPinSpecial;
            if (level.pinnedTileTypes != null && idx < level.pinnedTileTypes.Length)
                level.pinnedTileTypes[idx] = 0;
        }
        else if (selectedPinTileType >= 0)
        {
            // Normal taş: sadece tile tipi, special'ı temizle
            if (level.pinnedTileTypes != null && idx < level.pinnedTileTypes.Length)
                level.pinnedTileTypes[idx] = selectedPinTileType + 1;
            if (level.pinnedSpecialTypes != null && idx < level.pinnedSpecialTypes.Length)
                level.pinnedSpecialTypes[idx] = 0;
        }

        EditorUtility.SetDirty(level);
    }

    private void ClearPinnedTile(LevelData level, int idx)
    {
        if (idx < 0) return;
        if (level.pinnedTileTypes != null && idx < level.pinnedTileTypes.Length)
            level.pinnedTileTypes[idx] = 0;
        if (level.pinnedSpecialTypes != null && idx < level.pinnedSpecialTypes.Length)
            level.pinnedSpecialTypes[idx] = 0;
    }

    private void DrawMagnetPalette(LevelData level)
    {
        EditorGUILayout.LabelField("Magnet Obstacle", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Grid'de hücrelere sırayla tıkla → mıknatıs yolu oluşturuluyor.\n" +
            "İlk hücre = Magnet A, son hücre = Magnet B.\n" +
            "En az 2 hücre seçince 'Magnet Kaydet' butonu aktif olur.\n" +
            "Erase modu: mevcut mıknatısı siler.",
            MessageType.Info);

        EditorGUILayout.LabelField($"Oluşturulan yol: {magnetPathBuilding.Count} hücre", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(magnetPathBuilding.Count < 2);
        if (GUILayout.Button("Magnet Kaydet"))
            FinalizeMagnet(level);
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Yolu Temizle"))
            magnetPathBuilding.Clear();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Mevcut mıknatıslar: {(level.magnets != null ? level.magnets.Length : 0)}", EditorStyles.miniLabel);

        if (level.magnets != null && level.magnets.Length > 0 && GUILayout.Button("Tüm mıknatısları temizle"))
        {
            Undo.RecordObject(level, "Clear All Magnets");
            level.magnets = System.Array.Empty<MagnetEntry>();
            EditorUtility.SetDirty(level);
        }
    }

    private void AddMagnetPathCell(LevelData level, int cellIndex)
    {
        if (cellIndex < 0 || cellIndex >= level.cells.Length) return;
        if (level.cells[cellIndex] != (int)CellType.Normal) return;
        if (magnetPathBuilding.Contains(cellIndex)) return;

        magnetPathBuilding.Add(cellIndex);
    }

    private void FinalizeMagnet(LevelData level)
    {
        if (magnetPathBuilding.Count < 2) return;

        Undo.RecordObject(level, "Add Magnet");

        var entry = new MagnetEntry { pathCellIndices = magnetPathBuilding.ToArray() };
        var list = new System.Collections.Generic.List<MagnetEntry>(level.magnets) { entry };
        level.magnets = list.ToArray();

        magnetPathBuilding.Clear();
        EditorUtility.SetDirty(level);
    }

    private void RemoveMagnetAtCell(LevelData level, int cellIndex)
    {
        if (level.magnets == null || level.magnets.Length == 0) return;

        var list = new System.Collections.Generic.List<MagnetEntry>(level.magnets);
        for (int m = list.Count - 1; m >= 0; m--)
        {
            var entry = list[m];
            if (entry.pathCellIndices == null) continue;
            bool found = false;
            foreach (int ci in entry.pathCellIndices) if (ci == cellIndex) { found = true; break; }
            if (found) list.RemoveAt(m);
        }
        level.magnets = list.ToArray();
    }

    private void DrawTubePalette(LevelData level)
    {
        EditorGUILayout.LabelField("Tube Obstacle", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Grid'de bir hücreye tıkla → o hücreden başlayan tüp eklenir.\n" +
            "Erase modu veya mevcut tüp hücresine tekrar tıklamak tüpü siler.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        selectedTubeDir    = (TubeDirection)EditorGUILayout.EnumPopup("Yön (base→open end)", selectedTubeDir);
        selectedTubeLength = EditorGUILayout.IntSlider("Uzunluk (hücre)", selectedTubeLength, 2, 9);
        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(level);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Mevcut tüpler: {(level.tubes != null ? level.tubes.Length : 0)}", EditorStyles.miniLabel);

        if (level.tubes != null && level.tubes.Length > 0 && GUILayout.Button("Tüm tüpleri temizle"))
        {
            Undo.RecordObject(level, "Clear All Tubes");
            level.tubes = System.Array.Empty<TubeEntry>();
            EditorUtility.SetDirty(level);
        }
    }

    private void DrawSafePalette(LevelData level)
    {
        EditorGUILayout.LabelField("Safe (Kasa) Obstacle", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Önce içeriği (mud/para/taş/obstacle) NORMAL şekilde yerleştir, SONRA Safe'i üstüne koy.\n" +
            "Grid'de bir hücreye tıkla → o hücre sol-ÜST origin olur, NxN bölgeyi kaplar.\n" +
            "Erase modu veya bölgeye tekrar tıklamak kasayı siler. (İçerik silinmez, sadece overlay.)",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        selectedSafeW = EditorGUILayout.IntSlider("Genişlik (hücre)", selectedSafeW, 1, 6);
        selectedSafeH = EditorGUILayout.IntSlider("Yükseklik (hücre)", selectedSafeH, 1, 6);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Kilit hit sayıları (kırılma sırası)", EditorStyles.miniBoldLabel);
        selectedSafeRed    = EditorGUILayout.IntSlider("1) Kırmızı", selectedSafeRed, 1, 20);
        selectedSafeYellow = EditorGUILayout.IntSlider("2) Sarı", selectedSafeYellow, 1, 20);
        selectedSafeGreen  = EditorGUILayout.IntSlider("3) Yeşil", selectedSafeGreen, 1, 20);
        EditorGUILayout.Space(2);
        selectedSafeHitMode = (SafeLockHitMode)EditorGUILayout.EnumPopup("Hit Modu", selectedSafeHitMode);
        EditorGUILayout.LabelField(
            selectedSafeHitMode == SafeLockHitMode.Ordered
                ? "Ordered: sadece sıradaki kilit kendi renginden hit alır."
                : "AnyColor: hangi renk hit geldiyse o renkteki kilit düşer.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("Sıra / Öncelik", EditorStyles.miniBoldLabel);
        selectedSafeFirstLock  = (SafeLockColor)EditorGUILayout.EnumPopup("1", selectedSafeFirstLock);
        selectedSafeSecondLock = (SafeLockColor)EditorGUILayout.EnumPopup("2", selectedSafeSecondLock);
        selectedSafeThirdLock  = (SafeLockColor)EditorGUILayout.EnumPopup("3", selectedSafeThirdLock);
        NormalizeSelectedSafeOrder();
        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(level);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Mevcut kasalar: {(level.safes != null ? level.safes.Length : 0)}", EditorStyles.miniLabel);

        if (level.safes != null && level.safes.Length > 0 && GUILayout.Button("Tüm kasaları temizle"))
        {
            Undo.RecordObject(level, "Clear All Safes");
            level.safes = System.Array.Empty<SafeEntry>();
            EditorUtility.SetDirty(level);
        }
    }

    private void NormalizeSelectedSafeOrder()
    {
        var used = new System.Collections.Generic.HashSet<SafeLockColor>();
        selectedSafeFirstLock = NormalizeLockSlot(selectedSafeFirstLock, used);
        selectedSafeSecondLock = NormalizeLockSlot(selectedSafeSecondLock, used);
        selectedSafeThirdLock = NormalizeLockSlot(selectedSafeThirdLock, used);
    }

    private static SafeLockColor NormalizeLockSlot(SafeLockColor value, System.Collections.Generic.HashSet<SafeLockColor> used)
    {
        if (!used.Contains(value))
        {
            used.Add(value);
            return value;
        }

        for (int i = 0; i < 3; i++)
        {
            var candidate = (SafeLockColor)i;
            if (used.Contains(candidate)) continue;
            used.Add(candidate);
            return candidate;
        }

        return SafeLockColor.Red;
    }

    private void DrawSpriteInRect(Sprite sprite, Rect r, float padding)
    {
        if (sprite == null) return;
        Rect rr = new Rect(r.x + padding, r.y + padding, r.width - padding * 2, r.height - padding * 2);
        Texture2D tex = sprite.texture;
        Rect tr = sprite.textureRect;
        Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        GUI.DrawTextureWithTexCoords(rr, tex, uv, true);
    }
}
