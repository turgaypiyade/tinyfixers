using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private enum PaintMode { Mask, Obstacle, Tube, Magnet, Tiles, Safe, Overlay, Erase, AncientSeal, Channel }

    private PaintMode mode = PaintMode.Obstacle;
    private ObstacleId selectedObstacle = ObstacleId.Stone;
    private int selectedInitialCrateHits = 3;
    private bool showSharedCrateStageRules;
    private int selectedHamsterSatiety = 7;   // yerleştirilecek hamsterin lokma sayısı
    private TarpStatueColor selectedTarpColor = TarpStatueColor.Red;          // yerleştirilecek heykelin etiket rengi
    private int selectedTarpCount = LevelData.TarpStatueDefaultCount;         // brandanın kalkması için toplanacak taş
    private int selectedTarpSize = TarpStatueEntry.TarpStatueMinSize;         // kare boyut (2/3/4)
    // Duvar (Wall) numaralı fırça: aynı numaralı BİTİŞİK duvar hücreleri tek parça (tek origin).
    private int selectedWallPiece = 1;
    private bool showBossOilSettings;

    // Safe (kasa) settings — tıklanan hücre sol-üst origin; WxH bölgeyi kaplar.
    private AncientSealStartColor selectedSealStartColor = AncientSealStartColor.Auto;
    private int selectedSealW = 3, selectedSealH = 3;
    private int selectedSealRed = 4, selectedSealYellow = 4, selectedSealGreen = 4;
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
    // Son tüp yerleştirme denemesi neden başarısız oldu (palette görünür; yalnız console'a yazmak
    // "tıklıyorum, hiçbir şey olmuyor" hissi veriyordu).
    private string tubeStatus;

    // Magnet settings
    private readonly System.Collections.Generic.List<int> magnetPathBuilding = new();
    private static readonly Color magnetEndpointColor = new Color(0.15f, 0.45f, 1f, 0.90f);
    private static readonly Color magnetPathColor     = new Color(0.30f, 0.65f, 1f, 0.55f);
    private static readonly Color magnetBuildingColor = new Color(0.80f, 0.95f, 0.40f, 0.75f);

    // Su kanalı (yol: depo üst kenarda → çıkış alt kenarda)
    private readonly System.Collections.Generic.List<int> channelPathBuilding = new();
    private string channelStatus;
    private static readonly Color channelPathColor     = new Color(0.20f, 0.78f, 0.86f, 0.55f);
    private static readonly Color channelEndColor      = new Color(0.05f, 0.55f, 0.70f, 0.90f);
    private static readonly Color channelBuildingColor = new Color(0.55f, 0.95f, 1.00f, 0.80f);

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

    private static readonly Color boardBg       = new Color(0.43f, 0.59f, 0.82f, 1f);
    private static readonly Color normalCell    = new Color(0.36f, 0.50f, 0.78f, 0.62f);
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

        mode = (PaintMode)GUILayout.Toolbar((int)mode, new[] { "Mask", "Obstacle", "Tube", "Magnet", "Tiles", "Safe", "Overlay", "Erase", "Ancient Seal", "Kanal" });

        if (mode == PaintMode.Obstacle)
            DrawPalette(level);
        else if (mode == PaintMode.Tube)
            DrawTubePalette(level);
        else if (mode == PaintMode.Magnet)
            DrawMagnetPalette(level);
        else if (mode == PaintMode.Channel)
            DrawChannelPalette(level);
        else if (mode == PaintMode.Tiles)
            DrawTilePinPalette(level);
        else if (mode == PaintMode.Safe)
            DrawSafePalette(level);
        else if (mode == PaintMode.AncientSeal)
            DrawAncientSealPalette(level);
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
        DrawRocketBasketSettings(level);
        DrawAudio(level);
    }

    private void DrawRocketBasketSettings(LevelData level)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Rocket Basket", EditorStyles.boldLabel);
        level.rocketBasketFireAllOnHit = EditorGUILayout.Toggle(
            "Fire All On Hit", level.rocketBasketFireAllOnHit);
        EditorGUILayout.HelpBox(
            "Açık: bu leveldeki tüm sepetler TEK vuruşta yüklü tüm roketleri fırlatır ve anında kalkar.\n" +
            "Kapalı: her renk kendi eşleşmesinde ayrı ayrı fırlar (Core→Gear→Bolt).",
            MessageType.None);
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

        // Aç Hamster: level'da hamster varsa doyma eşiği (0 = ObstacleLibrary varsayılanı).
        if (level.obstacles != null && System.Array.IndexOf(level.obstacles, (int)ObstacleId.Hamster) >= 0)
            level.hamsterSatietyOverride = Mathf.Max(0, EditorGUILayout.IntField(
                new GUIContent("Hamster Varsayılan Lokma", "Hücresinde kendi sayısı olmayan hamsterler için. 0 = ObstacleLibrary'deki varsayılan (hamsterSatiety)."),
                level.hamsterSatietyOverride));

        // Boya kutusu kasaları: her kasanın kutu rengi (Auto = satır bandına göre).
        if (level.obstacles != null && level.obstacleOrigins != null && level.width > 0)
        {
            for (int i = 0; i < level.obstacles.Length && i < level.obstacleOrigins.Length; i++)
            {
                if (level.obstacles[i] != (int)ObstacleId.PaintCanBox || level.obstacleOrigins[i] != i) continue;
                var current = level.GetPaintCanBoxColor(i);
                var next = (PaintCanColor)EditorGUILayout.EnumPopup(
                    new GUIContent($"Boya Kasası ({i % level.width},{i / level.width})",
                        "Kutu rengi. Auto = kasanın satırına göre otomatik."), current);
                if (next != current)
                {
                    Undo.RecordObject(level, "Boya Kasası Rengi");
                    level.SetPaintCanBoxColor(i, next);
                    EditorUtility.SetDirty(level);
                }
            }
        }

        // Boss düellosu kendi hayvan intro'sunu (BossDuelIntroArtwork) oynatır; sol/sağ sprite'lı
        // özel intro orada hiç okunmaz. Yalnız giriş süreleri kullanılır.
        if (level.levelKind == LevelKind.BossDuel)
        {
            EditorGUILayout.LabelField("Boss Intro (hayvan görselleri otomatik)", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            level.introSlideInDuration = Mathf.Max(0.05f,
                EditorGUILayout.FloatField("Slide In Duration (sn)", level.introSlideInDuration));
            level.introHoldDuration = Mathf.Max(0f,
                EditorGUILayout.FloatField("Hold Duration (sn)", level.introHoldDuration));
            EditorGUI.indentLevel--;
        }
        else
        {
            level.usesCustomIntro = EditorGUILayout.Toggle(
                new GUIContent("Uses Custom Intro",
                    "Açık: bu level'e girerken default loading screen yerine, iki parçanın soldan/sağdan " +
                    "gelip ortada birleştiği özel intro 'load' olur (sahne async yüklenirken oynar). " +
                    "Kapalı VEYA iki sprite'tan biri boşsa default load çalışır."),
                level.usesCustomIntro);
        }

        if (level.usesCustomIntro && level.levelKind != LevelKind.BossDuel)
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

        EditorGUILayout.LabelField("Boss Duel", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox("Yukarıdaki Moves tüm karşılaşmanın hamle sınırıdır. Yaşayan rakip hamle sonunda " +
            "bir kez karşılık verir. Oyuncu canı ve kalan koruması sonraki rakibe taşınır; otomatik iyileşme yoktur.", MessageType.Info);
        level.playerMaxHp = Mathf.Max(1, EditorGUILayout.IntField("Player HP", level.playerMaxHp));
        level.damagePerClearedTile = Mathf.Max(0, EditorGUILayout.IntField("Power Per Tile", level.damagePerClearedTile));
        level.shieldProtectionPerPickup = Mathf.Max(1, EditorGUILayout.IntField(
            new GUIContent("Protection Per Shield", "Bir kalkanın eklediği koruma; oyuncu ve düşman için aynı. Varsayılan: 2."),
            level.shieldProtectionPerPickup));
        level.enemyAttackBaseDamage = Mathf.Max(0, EditorGUILayout.IntField(
            new GUIContent("Default Counter Damage", "Manuel rakipte Counter Damage 0 ise bu değer kullanılır."), level.enemyAttackBaseDamage));
        level.battlefieldBackground = (Sprite)EditorGUILayout.ObjectField("Arena Background (boş=mevcut)", level.battlefieldBackground, typeof(Sprite), false);

        EditorGUILayout.Space(2);
        showBossOilSettings = EditorGUILayout.Foldout(showBossOilSettings, "Thrown Obstacle Pressure", true);
        if (showBossOilSettings)
        {
            level.bossAttackOilCount = Mathf.Max(0, EditorGUILayout.IntField("Obstacles Per Volley (0 = off)", level.bossAttackOilCount));
            if (level.bossAttackOilCount > 0)
                level.bossAttackEveryMoves = Mathf.Max(1, EditorGUILayout.IntField("Volley Every N Counters", level.bossAttackEveryMoves));
            level.bossMaxPressureObstacles = Mathf.Max(1, EditorGUILayout.IntField("Max Pressure Obstacles On Board", level.bossMaxPressureObstacles));
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bossThrownObstacles"), new GUIContent("Previously Introduced Obstacles"), true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Havuzu önceki levellarda kullanılan engellerden seçin. Boş havuz = Oil. Sandık/üretici/ödül atılamaz. Jel desteklenir ancak taşları kilitlemez; zorluğu artırmaz.", MessageType.Info);
            if (level.bossThrownObstacles != null)
                foreach (var id in level.bossThrownObstacles)
                    if (!BossDuelObstaclePressure.IsThrowable(level, id))
                        EditorGUILayout.HelpBox($"{id}: fırlatmaya uygun değil; oyun bu girdiyi atlayacak.", MessageType.Warning);
        }

        EditorGUILayout.Space(2);
        level.bossToolThreatEnabled = EditorGUILayout.ToggleLeft(
            new GUIContent("Alet Tehdidi (geri sayımlı engel)",
                "Düşman board'a geri sayımlı ToolThreat engeli koyar. Süresinde kırılmazsa aletini ekrana fırlatır: " +
                "ekran çatlar, tahta kilitlenir, düşman ek saldırı yapar."),
            level.bossToolThreatEnabled);
        if (level.bossToolThreatEnabled)
        {
            EditorGUI.indentLevel++;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bossToolThreatTypes"),
                new GUIContent("Tehdit Türleri (boş = gri)", "Gri ToolThreat YALNIZ special ile kırılır; renkliler " +
                    "(Yellow/Red/Blue/Green) kendi rengindeki eşleşme veya special ile. Her seferinde listeden biri."), true);
            serializedObject.ApplyModifiedProperties();
            level.bossToolThreatEveryCounters = Mathf.Max(1, EditorGUILayout.IntField("Yeni Tehdit: Her N Karşı Saldırıda", level.bossToolThreatEveryCounters));
            level.bossToolThreatCountdown = Mathf.Max(1, EditorGUILayout.IntField("Geri Sayım (oyuncu turu)", level.bossToolThreatCountdown));
            level.bossToolThreatExtraAttacks = Mathf.Max(0, EditorGUILayout.IntField("Çatlakta Ek Saldırı", level.bossToolThreatExtraAttacks));
            level.bossToolThreatLockSeconds = Mathf.Max(0.5f, EditorGUILayout.FloatField("Kilit Süresi (sn)", level.bossToolThreatLockSeconds));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.HelpBox("ToolThreat engelini başlangıçta board'a da koyabilirsin; düelloda o da aynı geri sayımla sayar.", MessageType.None);

        DrawBossOpponents(level);

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
                "BossDamage goal'ü tüm rakiplerin toplam canıdır. Örn. toplam 270 ve HP Weight 60/90/120 → " +
                "rakip canları 60/90/120. Her rakibin görsellerini Character Profile belirler.",
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

    private void DrawBossOpponents(LevelData level)
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Opponents", EditorStyles.miniBoldLabel);
        serializedObject.Update();
        var waves = serializedObject.FindProperty("bossWaves");
        EditorGUI.BeginChangeCheck();
        int count = EditorGUILayout.IntSlider(new GUIContent("Manual Opponents", "0 = otomatik rakipler; 1–3 = her rakibi ayrı düzenle."),
            waves.arraySize, 0, 3);
        if (EditorGUI.EndChangeCheck())
        {
            int previousCount = waves.arraySize;
            waves.arraySize = count;
            for (int i = previousCount; i < count; i++)
            {
                // Unity duplicates the last array element when growing; give new opponents defaults.
                var wave = waves.GetArrayElementAtIndex(i);
                wave.FindPropertyRelative("hpWeight").floatValue = 1f;
                wave.FindPropertyRelative("attackDamageBase").intValue = 0;
                wave.FindPropertyRelative("oilCount").intValue = -1;
                wave.FindPropertyRelative("oilEveryMoves").intValue = -1;
                wave.FindPropertyRelative("characterProfile").objectReferenceValue = null;
                wave.FindPropertyRelative("bodyTint").colorValue = Color.white;
            }
        }

        if (waves.arraySize == 0)
        {
            var waveCount = serializedObject.FindProperty("bossWaveCount");
            waveCount.intValue = EditorGUILayout.IntSlider(new GUIContent("Opponent Count", "0 = ilerlemeye göre otomatik; 1–3 = sabit rakip sayısı."),
                waveCount.intValue, 0, 3);
        }
        else
        {
            if (waves.arraySize > 3)
                EditorGUILayout.HelpBox("Yalnız ilk 3 rakip kullanılır. Listeyi azaltmak için Manual Opponents değerini değiştir.", MessageType.Warning);
            for (int i = 0; i < Mathf.Min(3, waves.arraySize); i++)
            {
                var wave = waves.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Opponent {i + 1}", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(wave.FindPropertyRelative("hpWeight"), new GUIContent("HP Weight", "Toplam BossDamage canından aldığı pay."));
                EditorGUILayout.PropertyField(wave.FindPropertyRelative("attackDamageBase"), new GUIContent("Counter Damage (0 = default)"));
                EditorGUILayout.PropertyField(wave.FindPropertyRelative("characterProfile"), new GUIContent("Character Profile (empty = scene)"));
                EditorGUILayout.PropertyField(wave.FindPropertyRelative("bodyTint"), new GUIContent("Character Tint"));
                if (showBossOilSettings)
                {
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("oilCount"), new GUIContent("Volley Count (−1 = default, 0 = off)"));
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("oilEveryMoves"), new GUIContent("Volley Every N Counters (−1 = default)"));
                }
                EditorGUILayout.EndVertical();
            }
        }
        serializedObject.ApplyModifiedProperties();
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

        bool auto = level.refillSpecialGuard < 0f;
        bool newAuto = EditorGUILayout.Toggle("Refill Special Koruması: Otomatik", auto);
        if (newAuto != auto)
            level.refillSpecialGuard = newAuto ? RefillSpecialGuard.Auto : 0.8f;
        if (!newAuto)
            level.refillSpecialGuard = EditorGUILayout.Slider("Engelleme Oranı", level.refillSpecialGuard, 0f, 1f);
        EditorGUILayout.HelpBox(
            "Yeni düşen taşın kendiliğinden Rocket/PulseCore/PatchBot şekli tamamlamasının engellenme oranı " +
            "(0 = kapalı, 1 = hepsi). Otomatik: seviye 1-50 → 0.8, 51-100 → 0.4-0.9, 101+ → 0.3-0.7. " +
            "Değişken oran seviyeye göre belirlenir; aynı levelda her denemede korunur. " +
            "Düz 3'lü cascade ve gizli yardım etkilenmez.",
            MessageType.None);
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

            var prevTargetType = goal.targetType;
            var prevObstacleId = goal.obstacleId;
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

            // Obstacle seçilince Amount'a tahtadaki sayı önerilir (sonra elle değiştirilebilir).
            int boardCount = goal.targetType == LevelGoalTargetType.Obstacle
                ? CountGoalObstacles(level, goal.obstacleId)
                : -1;
            bool obstaclePicked = goal.targetType == LevelGoalTargetType.Obstacle
                                  && (prevTargetType != goal.targetType || prevObstacleId != goal.obstacleId);
            if (obstaclePicked && boardCount > 0)
                goal.amount = boardCount;

            goal.iconOverride = (Sprite)EditorGUILayout.ObjectField("Icon Override", goal.iconOverride, typeof(Sprite), false);
            goal.amount = Mathf.Max(1, EditorGUILayout.IntField("Amount", goal.amount));

            if (boardCount >= 0)
            {
                EditorGUILayout.BeginHorizontal();
                string hint = goal.obstacleId == ObstacleId.SpreadingGel
                    ? $"Oynanır hücre: {boardCount}"
                    : $"Tahtada: {boardCount}";
                var style = boardCount != goal.amount ? EditorStyles.boldLabel : EditorStyles.miniLabel;
                var prevColor = GUI.color;
                if (boardCount != goal.amount && goal.obstacleId != ObstacleId.SpreadingGel)
                    GUI.color = new Color(1f, 0.55f, 0.45f);
                EditorGUILayout.LabelField(" ", hint, style);
                GUI.color = prevColor;
                using (new EditorGUI.DisabledScope(boardCount <= 0 || boardCount == goal.amount))
                {
                    if (GUILayout.Button("Eşitle", GUILayout.Width(60)))
                        goal.amount = boardCount;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (goal.targetType == LevelGoalTargetType.Obstacle && goal.obstacleId == ObstacleId.BilliardBall)
                DrawBilliardReachWarning(level, goal.amount);
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

    private static readonly string[] TarpSizeLabels = { "2x2", "3x3", "4x4" };

    // Yeni heykel ayarları + tahtadaki her heykelin rengi/sayısı/boyutu (sonradan da değişir; aynı engel, boyut ayarlı).
    private void DrawTarpStatuePalette(LevelData level)
    {
        EditorGUILayout.LabelField("Yeni heykel", EditorStyles.boldLabel);
        selectedTarpSize = TarpStatueEntry.TarpStatueMinSize + GUILayout.Toolbar(
            Mathf.Clamp(selectedTarpSize - TarpStatueEntry.TarpStatueMinSize, 0, 2), TarpSizeLabels);
        selectedTarpColor = (TarpStatueColor)EditorGUILayout.EnumPopup(
            new GUIContent("Etiket rengi", "Brandanın kalkması için tahtanın herhangi bir yerinde toplanacak taş rengi."),
            selectedTarpColor);
        selectedTarpCount = Mathf.Max(1, EditorGUILayout.IntField(
            new GUIContent("Toplanacak taş", "Etikette geri sayan sayı."), selectedTarpCount));
        EditorGUILayout.HelpBox("Brandalı heykel: önce renk toplanır → branda kalkar; sonra 3 taş vuruşu (yalnız " +
                                "special) → heykel hedefe uçar. Hedef = heykel sayısı (boyuttan bağımsız).", MessageType.None);

        // Tahtadaki heykeller (taban katman + yığın) — düzenlenebilir.
        var origins = new System.Collections.Generic.List<int>();
        if (level.obstacles != null && level.obstacleOrigins != null)
            for (int i = 0; i < level.obstacles.Length; i++)
                if (level.obstacles[i] == (int)ObstacleId.TarpStatue && level.obstacleOrigins[i] == i) origins.Add(i);
        if (level.stackedObstacles != null)
            foreach (var e in level.stackedObstacles)
                if (e.obstacleId == ObstacleId.TarpStatue && !origins.Contains(e.originCellIndex)) origins.Add(e.originCellIndex);
        if (origins.Count == 0) return;

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Tahtadaki heykeller", EditorStyles.boldLabel);
        foreach (int o in origins)
        {
            var t = level.GetTarpStatue(o);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"({o % level.width},{o / level.width})", GUILayout.Width(52));
            int size = TarpStatueEntry.TarpStatueMinSize + EditorGUILayout.Popup(t.Size - TarpStatueEntry.TarpStatueMinSize,
                TarpSizeLabels, GUILayout.Width(52));
            var color = (TarpStatueColor)EditorGUILayout.EnumPopup(t.color, GUILayout.Width(70));
            int count = Mathf.Max(1, EditorGUILayout.IntField(t.count, GUILayout.Width(44)));
            EditorGUILayout.EndHorizontal();
            if (color == t.color && count == t.count && size == t.Size) continue;

            Undo.RecordObject(level, "Brandalı heykel düzenle");
            if (size != t.Size && !TryResizeTarpStatue(level, o, t.Size, size))
                size = t.Size;
            level.SetTarpStatue(o, color, count, size);
            EditorUtility.SetDirty(level);
        }
    }

    // Taban katmandaki heykeli yeni kare boyuta basar. Yeni alan tahta dışına taşar ya da başka engel/hole'a değerse vazgeçer.
    private static bool TryResizeTarpStatue(LevelData level, int origin, int oldSize, int newSize)
    {
        int ox = origin % level.width, oy = origin / level.width;
        if (level.obstacles[origin] != (int)ObstacleId.TarpStatue)
            return true;   // yığındaki heykel: hücre basılmaz, boyut yalnız girişte
        if (!level.InBounds(ox + newSize - 1, oy + newSize - 1))
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning($"[LevelEditor] Heykel {newSize}x{newSize} tahtaya sığmıyor.");
            return false;
        }
        for (int y = oy; y < oy + newSize; y++)
            for (int x = ox; x < ox + newSize; x++)
            {
                int i = level.Index(x, y);
                bool ownCell = level.obstacles[i] == (int)ObstacleId.TarpStatue && level.obstacleOrigins[i] == origin;
                if (level.cells[i] != (int)CellType.Normal || (!ownCell && level.obstacles[i] != (int)ObstacleId.None))
                {
                    if (GameDiagnostics.EditorTools)
                        Debug.LogWarning($"[LevelEditor] Heykel {newSize}x{newSize}: ({x},{y}) dolu ya da hole.");
                    return false;
                }
            }
        for (int y = oy; y < oy + oldSize; y++)
            for (int x = ox; x < ox + oldSize; x++)
            {
                int i = level.Index(x, y);
                if (level.obstacles[i] != (int)ObstacleId.TarpStatue || level.obstacleOrigins[i] != origin) continue;
                level.obstacles[i] = (int)ObstacleId.None;
                level.obstacleOrigins[i] = -1;
            }
        for (int y = oy; y < oy + newSize; y++)
            for (int x = ox; x < ox + newSize; x++)
            {
                int i = level.Index(x, y);
                level.obstacles[i] = (int)ObstacleId.TarpStatue;
                level.obstacleOrigins[i] = origin;
            }
        return true;
    }

    // Yerleştirilecek engelin ayak izi: brandalı heykelde seçili kare boyut, diğerlerinde def.size.
    private Vector2Int PlacementFootprint(ObstacleId id, ObstacleDef def)
    {
        if (id == ObstacleId.TarpStatue) return new Vector2Int(selectedTarpSize, selectedTarpSize);
        return def != null ? new Vector2Int(Mathf.Max(1, def.size.x), Mathf.Max(1, def.size.y)) : Vector2Int.one;
    }

    private static Color TarpEditorColor(TarpStatueColor c) => c switch
    {
        TarpStatueColor.Yellow => new Color(0.85f, 0.65f, 0.05f, 0.9f),
        TarpStatueColor.Blue => new Color(0.15f, 0.35f, 0.9f, 0.9f),
        TarpStatueColor.Green => new Color(0.1f, 0.6f, 0.2f, 0.9f),
        _ => new Color(0.8f, 0.1f, 0.12f, 0.9f),
    };

    // Bilardo: tasarım anında, cebe hiçbir zaman ulaşamayacak topları uyar (runtime takılma kuralıyla aynı hesap).
    private static void DrawBilliardReachWarning(LevelData level, int goalAmount)
    {
        if (level.obstacles == null || level.cells == null || level.width <= 0 || level.height <= 0) return;
        int w = level.width, h = level.height;
        bool IsHole(int x, int y) => level.cells[y * w + x] == (int)CellType.Empty;
        bool IsPocket(int x, int y) => level.obstacles[y * w + x] == (int)ObstacleId.BilliardPocket;

        var dist = new int[w, h];
        BilliardReach.ComputePocketDistances(w, h, IsHole, IsPocket, dist, new System.Collections.Generic.Queue<(int x, int y)>());

        int balls = 0, reachable = 0, pockets = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (IsPocket(x, y)) pockets++;
                if (level.obstacles[y * w + x] != (int)ObstacleId.BilliardBall) continue;
                balls++;
                if (dist[x, y] != BilliardReach.Unreachable) reachable++;
            }

        if (pockets == 0)
            EditorGUILayout.HelpBox("Tahtada bilardo cebi yok — top hedefi tamamlanamaz.", MessageType.Error);
        else if (reachable < goalAmount)
            EditorGUILayout.HelpBox($"{balls - reachable} top hiçbir cebe ulaşamıyor (yalnız aşağı/çapraz/yana yuvarlanır). " +
                                    $"Ulaşabilen {reachable} top < hedef {goalAmount}: level başta takılı sayılır.", MessageType.Error);
        else if (reachable < balls)
            EditorGUILayout.HelpBox($"{balls - reachable} top hiçbir cebe ulaşamıyor (süs olarak kalır).", MessageType.Warning);
    }

    /// <summary>
    /// Bir obstacle hedefinin tahtadaki karşılığı — TopHUD sayımıyla aynı kural: her ORIGIN bir kez
    /// (duvar parçası, 2x2 kasa = 1), yığılmış (stackedObstacles) ve kasa (safes) girişleri dahil.
    /// Çiçekli çim aynı zamanda çimdir. BatteryBox pil vuruşu başına sayılır. Jel için oynanır hücre
    /// sayısı döner (kaplama hedefi). Hamster = tahtadaki hamster sayısı. -1 = öneri yok: KeyGenerator (anahtar) ve
    /// movable engeller (hedef sayısına göre üretilir).
    /// </summary>
    private static int CountGoalObstacles(LevelData level, ObstacleId id)
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null) return -1;
        if (id == ObstacleId.KeyGenerator) return -1;
        // Hamster hedefi = tahtadaki hamster sayısı (her biri doyup ayrılınca 1 sayılır). Bilardo topu da yalnız
        // level'da konur (üstten gelmez) → hedef = tahtadaki top sayısı.
        if (id == ObstacleId.Hamster || id == ObstacleId.BilliardBall)
        {
            int placed = 0;
            for (int i = 0; i < level.obstacles.Length && i < level.obstacleOrigins.Length; i++)
                if (level.obstacles[i] == (int)id && level.obstacleOrigins[i] == i) placed++;
            return placed;
        }
        // Movable engeller (kargo, kask, plastik...) hedef sayısına göre üretilir: Amount tasarımcınındır.
        var lib = level.obstacleLibrary;
        var goalDef = lib != null ? lib.Get(id) : null;
        if (goalDef != null && goalDef.IsMovableObstacle) return -1;

        if (id == ObstacleId.SpreadingGel)
        {
            int playable = 0;
            if (level.cells != null)
                foreach (var c in level.cells) if (c != 0) playable++;
            return playable;
        }

        bool Matches(int raw) => raw == (int)id || (id == ObstacleId.Grass &&
            (raw == (int)ObstacleId.GrassFlower || raw == (int)ObstacleId.GrassWaterLily));

        int count = 0;
        for (int i = 0; i < level.obstacles.Length && i < level.obstacleOrigins.Length; i++)
            if (Matches(level.obstacles[i]) && level.obstacleOrigins[i] == i) count++;

        if (level.stackedObstacles != null)
            foreach (var e in level.stackedObstacles)
                if (Matches((int)e.obstacleId)) count++;

        if (id == ObstacleId.Safe && level.safes != null)
            count += level.safes.Length;
        if (id == ObstacleId.AncientSeal && level.ancientSeals != null)
            count += level.ancientSeals.Length;

        if (id == ObstacleId.BatteryBox)
            count *= Mathf.Max(1, goalDef != null ? goalDef.hits : 1) * 4;
        return count;
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
            level.colorCrateInitialHits = System.Array.Empty<ColorCrateInitialHitsEntry>();
            for (int i = 0; i < size; i++) level.obstacles[i] = (int)ObstacleId.None;
        }
        if (level.obstacleOrigins == null || level.obstacleOrigins.Length != size)
        {
            level.obstacleOrigins = new int[size];
            for (int i = 0; i < size; i++) level.obstacleOrigins[i] = -1;
        }
        if (level.wallPieceIds == null || level.wallPieceIds.Length != size)
        {
            var old = level.wallPieceIds;
            level.wallPieceIds = new int[size];
            if (old != null) System.Array.Copy(old, level.wallPieceIds, Mathf.Min(size, old.Length));
        }
        if (level.tubes == null)
            level.tubes = System.Array.Empty<TubeEntry>();

        if (level.magnets == null)
            level.magnets = System.Array.Empty<MagnetEntry>();

        if (level.waterChannels == null)
            level.waterChannels = System.Array.Empty<WaterChannelEntry>();

        if (level.safes == null)
            level.safes = System.Array.Empty<SafeEntry>();
        if (level.ancientSeals == null)
            level.ancientSeals = System.Array.Empty<AncientSealEntry>();

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
                {
                    if (GameDiagnostics.EditorTools)
                        Debug.LogWarning($"{level.name}: Unknown obstacle id {id} ({(int)id}) at index {i}. It will not render in editor and may not block cascade correctly.", level);
                }
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
                DrawObstaclePreview(library, def, r, 4);
                var mini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerRight };
                GUI.Label(r, $"{def.size.x}x{def.size.y}", mini);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);
        if (selectedObstacle == ObstacleId.GrassWaterLily)
            EditorGUILayout.HelpBox("GrassWaterLily tek parça yerleştirilir: çim + nilüfer birlikte gelir. Ayrı Grass ekleme.", MessageType.Info);
        if (selectedObstacle == ObstacleId.RhodesStatue)
            EditorGUILayout.HelpBox("RhodesStatue: tek seçimle 2×2 yerleşir. Yalnız special hasarı alır; dört vuruşta tamamen kalkar.", MessageType.Info);
        if (selectedObstacle == ObstacleId.TarpStatue)
            DrawTarpStatuePalette(level);
        if (selectedObstacle == ObstacleId.Hamster)
        {
            selectedHamsterSatiety = Mathf.Max(1, EditorGUILayout.IntField(
                new GUIContent("Lokma (bu hamster)", "Yerleştirilen hamsterin doyması için gereken lokma. Hücrede sayı olarak görünür. " +
                    "Var olan hamsterin sayısını değiştirmek için aynı hücreye yeniden yerleştir."),
                selectedHamsterSatiety));
            EditorGUILayout.HelpBox("Hamster hedefi = tahtadaki hamster sayısı. Her hamster kendi lokmasıyla doyar, bir kez zıplar (3x3) ve ayrılır.", MessageType.None);
        }
        var selDef = library.Get(selectedObstacle);
        if (selDef != null)
        {
            bool isColorCrate = StackedObstacleEntry.SupportsInitialHits(selectedObstacle);
            if (isColorCrate)
            {
                selectedInitialCrateHits = EditorGUILayout.IntSlider(
                    new GUIContent("Başlangıç kalan vuruş", "Bu sandığın başlangıç aşaması. Aynı sandığa yeniden tıklayarak güncelle."),
                    selectedInitialCrateHits, 1, Mathf.Max(1, selDef.hits));
                Rect stagePreview = GUILayoutUtility.GetRect(paletteIcon, paletteIcon, GUILayout.ExpandWidth(false));
                DrawObstaclePreview(library, selDef, stagePreview, 2, selectedInitialCrateHits);
                EditorGUILayout.HelpBox("Seçtiğin vuruş sayısıyla yerleştir. Mevcut aynı renk sandığa tıklamak aşamasını günceller.", MessageType.None);
            }
            var stage0 = selDef.GetStageRuleForRemainingHits(selDef.hits);
            string stageInfo = stage0 == null ? "-" : $"BlocksCells: {stage0.blocksCells}  |  Behavior: {stage0.behavior}  |  AllowDiagonal: {stage0.allowDiagonal}";
            EditorGUILayout.HelpBox($"Selected: {selectedObstacle}  |  Size: {selDef.size.x}x{selDef.size.y}  |  Library Max Hits: {Mathf.Max(1, selDef.hits)}  |  {stageInfo}", MessageType.None);
            if (IsWallId(selectedObstacle))
                DrawWallPieceBrush(level);
            if (isColorCrate)
                showSharedCrateStageRules = EditorGUILayout.Foldout(showSharedCrateStageRules, "Ortak kütüphane kuralları (tüm levellar)", true);
            if (!isColorCrate || showSharedCrateStageRules)
                DrawSelectedObstacleStageEditor(library, selDef);
        }
    }

    private void DrawWallPieceBrush(LevelData level)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Duvar Parçası (numaralı fırça)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Aynı numarayla boyanan BİTİŞİK duvar hücreleri tek parça olur (parça birlikte yıkılır, hedef parça " +
            "başına sayılır). Yan yana ayrı parça için numarayı değiştir. Çapraz değen hücreler ve farklı " +
            "duvar türleri (Wall / MetalWall) bağlanmaz.",
            MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        selectedWallPiece = Mathf.Max(1, EditorGUILayout.IntField("Parça No", selectedWallPiece));
        GUI.backgroundColor = WallPieceColor(selectedWallPiece);
        GUILayout.Box(GUIContent.none, GUILayout.Width(18), GUILayout.Height(18));
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Yeni Parça", GUILayout.Width(90)))
        {
            int max = 0;
            if (level.wallPieceIds != null)
                foreach (int p in level.wallPieceIds) max = Mathf.Max(max, p);
            selectedWallPiece = max + 1;
        }
        EditorGUILayout.EndHorizontal();
    }

    // Numaralı fırçayla parçalanan duvar türleri (aynı numara + aynı tür + bitişik = tek parça).
    private static bool IsWallId(ObstacleId id) => id == ObstacleId.Wall || id == ObstacleId.MetalWall;

    private static Color WallPieceColor(int piece)
    {
        if (piece <= 0) return Color.white;
        float hue = Mathf.Repeat(piece * 0.618034f, 1f);
        return Color.HSVToRGB(hue, 0.55f, 1f);
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
        DrawWallPieces(level, ox, oy);
        for (int y = 0; y < level.height; y++)
        for (int x = 0; x < level.width; x++)
        {
            int idx = level.Index(x, y);
            if (level.cells[idx] != (int)CellType.Normal) continue;
            var obs = (ObstacleId)level.obstacles[idx];
            if (obs == ObstacleId.None || level.obstacleOrigins[idx] != idx) continue;
            if (IsWallId(obs)) continue;   // DrawWallPieces hücre hücre çizer
            var def = library != null ? library.Get(obs) : null;
            if (def == null || def.GetPreviewSprite() == null) continue;
            var foot = level.GetObstacleFootprint(obs, idx, def);
            int w = foot.x, h = foot.y;
            Rect big = new Rect(ox + x * cellPx, oy + y * cellPx, w * cellPx - 1, h * cellPx - 1);
            DrawObstaclePreview(library, def, big, 2, level.GetColorCrateInitialHits(idx, obs));
            if (obs == ObstacleId.TarpStatue)
            {
                var tarp = level.GetTarpStatue(idx);
                var badge = new Rect(big.center.x - cellPx * 0.4f, big.center.y - cellPx * 0.25f, cellPx * 0.8f, cellPx * 0.5f);
                EditorGUI.DrawRect(badge, TarpEditorColor(tarp.color));
                GUI.Label(badge, tarp.count.ToString(), new GUIStyle(EditorStyles.boldLabel)
                    { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = Color.white } });
            }
            if (obs == ObstacleId.Hamster)
            {
                int bites = level.hamsterSatietyByCell != null && idx < level.hamsterSatietyByCell.Length
                    ? level.hamsterSatietyByCell[idx] : 0;
                string label = bites > 0 ? bites.ToString() : "~";   // ~ = level/kütüphane varsayılanı
                var badge = new Rect(big.xMax - cellPx * 0.42f, big.yMax - cellPx * 0.36f, cellPx * 0.42f, cellPx * 0.36f);
                EditorGUI.DrawRect(badge, new Color(0.15f, 0.1f, 0.05f, 0.85f));
                GUI.Label(badge, label, new GUIStyle(EditorStyles.boldLabel)
                    { alignment = TextAnchor.MiddleCenter, fontSize = 10, normal = { textColor = Color.white } });
            }
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

                // Hücrede taban engel (çim, yağ…) varsa sabitleme hücreyi kaplamasın: sol-alt köşede
                // küçük rozet olur, engel görünür kalır (126'da çimler pinli taşların altında kayboluyordu).
                if ((hasPin || hasSpecial) && idx < level.obstacles.Length && level.obstacles[idx] != (int)ObstacleId.None)
                {
                    float badge = r.width * 0.45f;
                    r = new Rect(r.x, r.yMax - badge, badge, badge);
                }

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

        // Su kanalları: yol turkuaz, depo hücresi "D", çıkış suyun çıktığı yönde ok
        if (level.waterChannels != null)
        {
            var endStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10, normal = { textColor = Color.white } };
            foreach (var entry in level.waterChannels)
            {
                if (entry.pathCellIndices == null) continue;
                for (int ki = 0; ki < entry.pathCellIndices.Length; ki++)
                {
                    int ci = entry.pathCellIndices[ki];
                    if (ci < 0 || ci >= level.obstacles.Length) continue;
                    Rect kr = new Rect(ox + (ci % level.width) * cellPx, oy + (ci / level.width) * cellPx, cellPx - 1, cellPx - 1);
                    bool first = ki == 0, last = ki == entry.pathCellIndices.Length - 1;
                    EditorGUI.DrawRect(kr, first || last ? channelEndColor : channelPathColor);
                    if (first) GUI.Label(kr, "D", endStyle);   // depo
                    else if (last)
                    {
                        char exit = WaterChannelEntry.ExitSide(level, entry.pathCellIndices);
                        GUI.Label(kr, exit == 'L' ? "←" : exit == 'R' ? "→" : "↓", endStyle);
                    }
                }
            }
        }
        for (int bi = 0; bi < channelPathBuilding.Count; bi++)
        {
            int ci = channelPathBuilding[bi];
            if (ci < 0 || ci >= level.obstacles.Length) continue;
            Rect kr = new Rect(ox + (ci % level.width) * cellPx, oy + (ci / level.width) * cellPx, cellPx - 1, cellPx - 1);
            EditorGUI.DrawRect(kr, channelBuildingColor);
            GUI.Label(kr, (bi + 1).ToString(), new GUIStyle(EditorStyles.boldLabel)
                { alignment = TextAnchor.MiddleCenter, fontSize = 9, normal = { textColor = Color.black } });
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

                // Engel ikonu bölgenin üstüne; altındaki içerik seçilebilsin diye hafif saydam.
                var safeDef = level.obstacleLibrary != null ? level.obstacleLibrary.Get(ObstacleId.Safe) : null;
                if (safeDef != null && safeDef.GetPreviewSprite() != null)
                {
                    var prevColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.85f);
                    DrawSpriteInRect(safeDef.GetPreviewSprite(),
                        new Rect(ox + sox * cellPx, oy + soy * cellPx, sw * cellPx - 1, sh * cellPx - 1), 2);
                    GUI.color = prevColor;
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

        // Ancient Seal collection counts and its own icon.
        if (level.ancientSeals != null)
        {
            for (int s = 0; s < level.ancientSeals.Length; s++)
            {
                var e = level.ancientSeals[s];
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

                // Engel ikonu bölgenin üstüne; altındaki içerik seçilebilsin diye hafif saydam.
                var safeDef = level.obstacleLibrary != null ? level.obstacleLibrary.Get(ObstacleId.AncientSeal) : null;
                if (safeDef != null && safeDef.GetPreviewSprite() != null)
                {
                    var prevColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.85f);
                    DrawSpriteInRect(safeDef.GetPreviewSprite(),
                        new Rect(ox + sox * cellPx, oy + soy * cellPx, sw * cellPx - 1, sh * cellPx - 1), 2);
                    GUI.color = prevColor;
                }

                Rect lr = new Rect(ox + sox * cellPx, oy + soy * cellPx, cellPx - 1, cellPx - 1);
                GUI.Label(lr, $"🔒{e.redCount}/{e.yellowCount}/{e.greenCount}", new GUIStyle(EditorStyles.boldLabel)
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
                var sfoot = level.GetObstacleFootprint(e.obstacleId, e.originCellIndex, def);
                int sw = sfoot.x, sh = sfoot.y;

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
                if (def != null)
                {
                    int remaining = e.initialRemainingHits > 0 && StackedObstacleEntry.SupportsInitialHits(e.obstacleId)
                        ? e.ResolveInitialRemainingHits(def) : 0;
                    DrawObstaclePreview(lib, def, lr, 1, remaining);
                }
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

    // Duvar hücreleri: kiremit + parça rengi + numara; farklı parçaya/boşluğa bakan kenarlar kalın çizgi.
    private void DrawWallPieces(LevelData level, float ox, float oy)
    {
        var label = new GUIStyle(EditorStyles.boldLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 10, normal = { textColor = Color.black } };

        bool SameOrigin(int idx, int nx, int ny)
        {
            if (!level.InBounds(nx, ny)) return false;
            int n = level.Index(nx, ny);
            return level.obstacles[n] == level.obstacles[idx] && level.obstacleOrigins[n] == level.obstacleOrigins[idx];
        }

        for (int y = 0; y < level.height; y++)
        for (int x = 0; x < level.width; x++)
        {
            int idx = level.Index(x, y);
            var wallId = (ObstacleId)level.obstacles[idx];
            if (!IsWallId(wallId)) continue;
            Rect r = new Rect(ox + x * cellPx, oy + y * cellPx, cellPx - 1, cellPx - 1);
            var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(wallId) : null;
            var sprite = def != null ? def.GetPreviewSprite() : null;
            if (sprite != null) DrawSpriteInRect(sprite, r, 1);
            int piece = level.wallPieceIds != null && idx < level.wallPieceIds.Length ? level.wallPieceIds[idx] : 0;
            var c = WallPieceColor(piece);
            EditorGUI.DrawRect(r, new Color(c.r, c.g, c.b, 0.45f));
            GUI.Label(r, piece.ToString(), label);

            const float t = 2f;
            var edge = new Color(0.1f, 0.05f, 0.02f, 1f);
            if (!SameOrigin(idx, x, y - 1)) EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), edge);
            if (!SameOrigin(idx, x, y + 1)) EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), edge);
            if (!SameOrigin(idx, x - 1, y)) EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), edge);
            if (!SameOrigin(idx, x + 1, y)) EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), edge);
        }
    }

    // Duvar parçalarını yeniden kur: aynı parça numaralı ve 4-komşulukla bağlı hücreler tek origin
    // (bileşenin en küçük indeksi). Numara aynı ama ayrık kalan gruplar ayrı parça olur.
    private static void RecomputeWallOrigins(LevelData level)
    {
        int n = level.width * level.height;
        if (level.wallPieceIds == null || level.wallPieceIds.Length != n)
        {
            var old = level.wallPieceIds;
            level.wallPieceIds = new int[n];
            if (old != null) System.Array.Copy(old, level.wallPieceIds, Mathf.Min(n, old.Length));
        }

        var visited = new bool[n];
        var queue = new System.Collections.Generic.Queue<int>();
        var comp = new System.Collections.Generic.List<int>();
        for (int i = 0; i < n; i++)
        {
            if (!IsWallId((ObstacleId)level.obstacles[i]))
            {
                level.wallPieceIds[i] = 0;
                continue;
            }
            if (level.wallPieceIds[i] <= 0) level.wallPieceIds[i] = 1;
        }

        for (int i = 0; i < n; i++)
        {
            if (visited[i] || !IsWallId((ObstacleId)level.obstacles[i])) continue;
            int piece = level.wallPieceIds[i];
            int wallType = level.obstacles[i];
            comp.Clear();
            queue.Enqueue(i);
            visited[i] = true;
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                comp.Add(c);
                int cx = c % level.width, cy = c / level.width;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? -1 : d == 1 ? 1 : 0);
                    int ny = cy + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (!level.InBounds(nx, ny)) continue;
                    int nIdx = level.Index(nx, ny);
                    if (visited[nIdx] || level.obstacles[nIdx] != wallType) continue;
                    if (level.wallPieceIds[nIdx] != piece) continue;
                    visited[nIdx] = true;
                    queue.Enqueue(nIdx);
                }
            }
            int origin = int.MaxValue;
            foreach (int c in comp) origin = Mathf.Min(origin, c);
            foreach (int c in comp) level.obstacleOrigins[c] = origin;
        }
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
                RemoveChannelAtCell(level, idx);
                RemoveSafeAtCell(level, idx);
                RemoveAncientSealAtCell(level, idx);
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
            case PaintMode.Channel:
                AddChannelPathCell(level, idx);
                break;
            case PaintMode.Tiles:
                PaintPinnedTile(level, idx);
                break;
            case PaintMode.Safe:
                PlaceSafe(level, x, y);
                break;
            case PaintMode.AncientSeal:
                PlaceAncientSeal(level, x, y);
                break;
            case PaintMode.Overlay:
                PlaceStackedObstacle(level, x, y);
                break;
        }
        level.PruneTarpStatues();
    }

    // Model (a): içerik AYRI yerleştirilir (Obstacle/Tiles/Mask modu); Safe sadece bir overlay
    // bölgesi (origin + boyut + kilit hit'leri) safes[]'e eklenir. Altındaki içerik EZİLMEZ —
    // runtime'da GridSpawner.StampLayeredEntriesIntoLevel yığın sırasıyla kurar, kırılınca geri yükler.
    private void PlaceSafe(LevelData level, int bx, int by)
    {
        if (!level.InBounds(bx, by)) return;
        int originIdx = level.Index(bx, by);

        int w = Mathf.Max(1, selectedSafeW);
        int h = Mathf.Max(1, selectedSafeH);
        if (bx + w > level.width || by + h > level.height)
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning($"[SafeEditor] Safe at ({bx},{by}) {w}x{h} grid dışına taşıyor.");
            return;
        }

        // Yığın (Docs/ObstacleStack_Plan.md): aynı köşedeki kasa yerinde güncellenir (sırası korunur);
        // başka engellerin üstüne/altına konan kasalar SİLİNMEZ — yeni kasa yığının en üstüne eklenir.
        int existing = System.Array.FindIndex(level.safes ?? System.Array.Empty<SafeEntry>(),
            e => e.originCellIndex == originIdx);
        if (!ValidateFullCover(level, RectCells(level, originIdx, w, h), ObstacleId.Safe, existing))
            return;
        int stackOrder = existing >= 0 ? level.safes[existing].stackOrder : NextStackOrder(level);
        if (existing >= 0)
        {
            var kept = new System.Collections.Generic.List<SafeEntry>(level.safes);
            kept.RemoveAt(existing);
            level.safes = kept.ToArray();
        }

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
            thirdLock       = selectedSafeThirdLock,
            stackOrder      = stackOrder
        };

        var list = new System.Collections.Generic.List<SafeEntry>(level.safes ?? System.Array.Empty<SafeEntry>()) { entry };
        level.safes = list.ToArray();
    }

    private void PlaceAncientSeal(LevelData level, int bx, int by)
    {
        if (!level.InBounds(bx, by)) return;
        int originIdx = level.Index(bx, by);

        int w = Mathf.Max(2, selectedSealW);
        int h = Mathf.Max(2, selectedSealH);
        if (bx + w > level.width || by + h > level.height)
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning($"[SealEditor] Ancient Seal at ({bx},{by}) {w}x{h} grid dışına taşıyor.");
            return;
        }

        // Aynı köşedeki mührü güncelle; Safe ve diğer katmanları koru.
        int existing = System.Array.FindIndex(level.ancientSeals ?? System.Array.Empty<AncientSealEntry>(),
            e => e.originCellIndex == originIdx);
        if (!ValidateFullCover(level, RectCells(level, originIdx, w, h), ObstacleId.AncientSeal, -1, existing))
            return;
        int stackOrder = existing >= 0 ? level.ancientSeals[existing].stackOrder : NextStackOrder(level);
        if (existing >= 0)
        {
            var kept = new System.Collections.Generic.List<AncientSealEntry>(level.ancientSeals);
            kept.RemoveAt(existing);
            level.ancientSeals = kept.ToArray();
        }

        var entry = new AncientSealEntry
        {
            originCellIndex = originIdx,
            width           = w,
            height          = h,
            startColor      = selectedSealStartColor,
            redCount        = Mathf.Max(1, selectedSealRed),
            yellowCount     = Mathf.Max(1, selectedSealYellow),
            greenCount      = Mathf.Max(1, selectedSealGreen),
            stackOrder      = stackOrder
        };

        var list = new System.Collections.Generic.List<AncientSealEntry>(level.ancientSeals ?? System.Array.Empty<AncientSealEntry>()) { entry };
        level.ancientSeals = list.ToArray();
    }

    // ── Yığın kuralı: üstteki katman alttaki engeli TAMAMEN örtmeli (aynı ya da daha çok hücre) ──
    // Yarım örtme (ör. 1x1 sandık 2x2 kasanın tek köşesinde) kurulmaz: görsel sıra ve açılma bozulur.
    // Muaf: mıknatıs/tüp (yol engeli) ve saydam örtüler (Grass/Oil — kısmi örtebilir, altı görünür).

    private static System.Collections.Generic.HashSet<int> RectCells(LevelData level, int origin, int w, int h)
    {
        var cells = new System.Collections.Generic.HashSet<int>();
        int ox = origin % level.width, oy = origin / level.width;
        for (int y = oy; y < oy + h && y < level.height; y++)
            for (int x = ox; x < ox + w && x < level.width; x++)
                cells.Add(y * level.width + x);
        return cells;
    }

    // Runtime kurulumuyla (GridSpawner.StampLayeredEntriesIntoLevel) aynı sırayla her hücrenin EN ÜST
    // katmanını ve o katmanın kapladığı hücreleri simüle eder. isPath = mıknatıs/tüp (kural dışı).
    private static System.Collections.Generic.Dictionary<int, (ObstacleId id, System.Collections.Generic.HashSet<int> cells, bool isPath)>
        SimulateTopLayers(LevelData level, int excludeSafeIndex, int excludeSealIndex)
    {
        var top = new System.Collections.Generic.Dictionary<int, (ObstacleId, System.Collections.Generic.HashSet<int>, bool)>();
        int n = level.width * level.height;

        // Taban katman: aynı origin'li hücreler tek engel.
        if (level.obstacles != null && level.obstacleOrigins != null)
        {
            var byOrigin = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>>();
            for (int i = 0; i < n && i < level.obstacles.Length; i++)
            {
                if ((ObstacleId)level.obstacles[i] == ObstacleId.None) continue;
                int o = i < level.obstacleOrigins.Length && level.obstacleOrigins[i] >= 0 ? level.obstacleOrigins[i] : i;
                if (!byOrigin.TryGetValue(o, out var set)) byOrigin[o] = set = new System.Collections.Generic.HashSet<int>();
                set.Add(i);
            }
            foreach (var kv in byOrigin)
                foreach (var c in kv.Value)
                    top[c] = ((ObstacleId)level.obstacles[c], kv.Value, false);
        }

        void MarkPath(int[] cells, ObstacleId id)
        {
            if (cells == null) return;
            var set = new System.Collections.Generic.HashSet<int>(cells);
            foreach (var c in cells) if (c >= 0 && c < n) top[c] = (id, set, true);
        }
        if (level.tubes != null)
            foreach (var t in level.tubes) MarkPath(TubeObstacleService.GetCellIndices(t, level.width, level.height), ObstacleId.Tube);
        if (level.magnets != null)
            foreach (var m in level.magnets) MarkPath(m.pathCellIndices, ObstacleId.Magnet);

        var order = new System.Collections.Generic.List<(int stackOrder, int kind, int index)>();
        if (level.stackedObstacles != null)
            for (int i = 0; i < level.stackedObstacles.Length; i++) order.Add((level.stackedObstacles[i].stackOrder, 0, i));
        if (level.safes != null)
            for (int i = 0; i < level.safes.Length; i++)
                if (i != excludeSafeIndex) order.Add((level.safes[i].stackOrder, 1, i));
        if (level.ancientSeals != null)
            for (int i = 0; i < level.ancientSeals.Length; i++)
                if (i != excludeSealIndex) order.Add((level.ancientSeals[i].stackOrder, 2, i));
        order.Sort((a, b) => a.stackOrder != b.stackOrder ? a.stackOrder.CompareTo(b.stackOrder)
                           : a.kind != b.kind ? a.kind.CompareTo(b.kind) : a.index.CompareTo(b.index));

        foreach (var o in order)
        {
            ObstacleId id; System.Collections.Generic.HashSet<int> cells;
            if (o.kind == 0)
            {
                var e = level.stackedObstacles[o.index];
                var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(e.obstacleId) : null;
                id = e.obstacleId;
                var efoot = level.GetObstacleFootprint(e.obstacleId, e.originCellIndex, def);
                cells = RectCells(level, e.originCellIndex, efoot.x, efoot.y);
            }
            else if (o.kind == 2)
            {
                var e = level.ancientSeals[o.index];
                id = ObstacleId.AncientSeal;
                cells = RectCells(level, e.originCellIndex, Mathf.Max(1, e.width), Mathf.Max(1, e.height));
            }
            else
            {
                var e = level.safes[o.index];
                id = ObstacleId.Safe;
                cells = RectCells(level, e.originCellIndex, Mathf.Max(1, e.width), Mathf.Max(1, e.height));
            }
            foreach (var c in cells) top[c] = (id, cells, false);
        }
        return top;
    }

    private static bool ValidateFullCover(LevelData level, System.Collections.Generic.HashSet<int> newCells,
        ObstacleId newId, int excludeSafeIndex, int excludeSealIndex = -1)
    {
        // Saydam örtüler (Grass/Oil) kısmen örtebilir: altı görünür kalır; çok-hücreli engel, üstündeki
        // tüm örtüler gidene dek vuruş almaz (ObstacleStateService.IsBuriedAnywhere). Örn. 4x4 kasa + 4 grass.
        if (ObstacleStateService.IsSeeThroughLayer(newId)) return true;

        var top = SimulateTopLayers(level, excludeSafeIndex, excludeSealIndex);
        foreach (var c in newCells)
        {
            if (!top.TryGetValue(c, out var under) || under.isPath || under.id == ObstacleId.None) continue;
            if (under.cells.IsSubsetOf(newCells)) continue;
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning($"[OverlayEditor] {newId} konamaz: ({c % level.width},{c / level.width}) hücresindeki " + $"{under.id} ({under.cells.Count} hücre) tamamen örtülmüyor. Üstteki katman alttaki engeli " + "aynı ya da daha çok hücreyle TAMAMEN kaplamalı.");
            return false;
        }
        return true;
    }

    // Yeni üst katman yığının en üstüne: kasalar + stacked girişler arasındaki en büyük sıra + 1.
    private static int NextStackOrder(LevelData level)
    {
        int max = 0;
        if (level.ancientSeals != null)
            foreach (var e in level.ancientSeals) max = Mathf.Max(max, e.stackOrder);
        if (level.safes != null)
            foreach (var e in level.safes) max = Mathf.Max(max, e.stackOrder);
        if (level.stackedObstacles != null)
            foreach (var e in level.stackedObstacles) max = Mathf.Max(max, e.stackOrder);
        return max + 1;
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

    private void RemoveAncientSealAtCell(LevelData level, int cellIndex)
    {
        if (level.ancientSeals == null || level.ancientSeals.Length == 0) return;

        int W = level.width;
        var list = new System.Collections.Generic.List<AncientSealEntry>(level.ancientSeals);
        for (int s = list.Count - 1; s >= 0; s--)
        {
            var e = list[s];
            int ox = e.originCellIndex % W, oy = e.originCellIndex / W;
            int cx = cellIndex % W, cy = cellIndex / W;
            if (cx >= ox && cx < ox + Mathf.Max(1, e.width) &&
                cy >= oy && cy < oy + Mathf.Max(1, e.height))
                list.RemoveAt(s);
        }
        level.ancientSeals = list.ToArray();
    }

    private void DrawOverlayPalette(LevelData level)
    {
        EditorGUILayout.HelpBox(
            "Overlay (yığın): seçili engel, hücrede ne varsa onun ÜSTÜNE eklenir; aynı hücreye istediğin kadar " +
            "katman koyabilirsin (Safe modu da aynı yığına ekler). Oyunda önce en üstteki kırılır, sonra " +
            "alttaki açılır; alttakiler o zamana dek yok hükmündedir. Mud/Jel yalnız en altta olur (Obstacle " +
            "modunda boya). Boyut engelin kendi def.size'ından gelir. Silgi hücredeki tüm katmanları siler.",
            MessageType.Info);
        // Üste konacak obstacle, normal obstacle paleti ile seçilir (selectedObstacle paylaşılır).
        DrawPalette(level);
    }

    // Overlay modu: stackedObstacles[]'a bir entry ekler (origin + obstacleId). Altındaki içerik
    // EZİLMEZ — runtime'da GridSpawner.StampLayeredEntriesIntoLevel yığın sırasıyla kurar, kırılınca
    // ObstacleStateService geri yükler. Safe'in generic karşılığı.
    private void PlaceStackedObstacle(LevelData level, int bx, int by)
    {
        if (!level.InBounds(bx, by)) return;
        if (selectedObstacle == ObstacleId.None) return;
        if (selectedObstacle == ObstacleId.AncientSeal)
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning("Ancient Seal için kendi sekmesini kullan; toplama sayıları orada ayarlanır.");
            return;
        }

        // Zemin altı engeller daima en altta. Lazer ise örn. 167'de tek jel hücresini
        // örtebilir; generic beneath yolu lazer kırılınca bu hücreyi açar.
        if (selectedObstacle == ObstacleId.Mud || selectedObstacle == ObstacleId.SpreadingGel
            || selectedObstacle == ObstacleId.WaterPuddle
            || selectedObstacle == ObstacleId.BilliardPocket)
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning($"[OverlayEditor] {selectedObstacle} üst katman olamaz (daima en altta). " + "Obstacle modunda taban katmana boya.");
            return;
        }

        int originIdx = level.Index(bx, by);
        // Bilardo cebi yığına girmez: üstüne de bir şey konmaz (top cebe düşebilmeli).
        if (level.obstacles != null && level.obstacles[originIdx] == (int)ObstacleId.BilliardPocket)
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning("[OverlayEditor] Bilardo cebinin üstüne katman konamaz.");
            return;
        }
        var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(selectedObstacle) : null;
        var pfoot = PlacementFootprint(selectedObstacle, def);
        int w = pfoot.x, h = pfoot.y;
        if (bx + w > level.width || by + h > level.height)
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning($"[OverlayEditor] Stacked {selectedObstacle} at ({bx},{by}) {w}x{h} grid dışına taşıyor.");
            return;
        }

        // Yığın: aynı köşeye AYNI engel tekrar konursa yinelenmez; farklı engel mevcutların ÜSTÜNE eklenir
        // (eskiden hücreyi kaplayan tüm üst katmanlar silinip tek katmana düşülüyordu).
        var current = level.stackedObstacles ?? System.Array.Empty<StackedObstacleEntry>();
        for (int i = 0; i < current.Length; i++)
            if (current[i].originCellIndex == originIdx && current[i].obstacleId == selectedObstacle)
            {
                if (StackedObstacleEntry.SupportsInitialHits(selectedObstacle))
                {
                    var existing = current[i];
                    existing.initialRemainingHits = selectedInitialCrateHits;
                    current[i] = existing;
                }
                return;
            }
        // Aynı normal sandığın aşamasını değiştirmek ikinci bir sandık katmanı oluşturmaz.
        if (StackedObstacleEntry.SupportsInitialHits(selectedObstacle)
            && level.obstacles[originIdx] == (int)selectedObstacle)
        {
            level.SetColorCrateInitialHits(originIdx, selectedObstacle, selectedInitialCrateHits);
            return;
        }
        if (!ValidateFullCover(level, RectCells(level, originIdx, w, h), selectedObstacle, -1))
            return;

        var entry = new StackedObstacleEntry
        {
            originCellIndex = originIdx,
            obstacleId      = selectedObstacle,
            stackOrder      = NextStackOrder(level),
            initialRemainingHits = StackedObstacleEntry.SupportsInitialHits(selectedObstacle) ? selectedInitialCrateHits : 0
        };

        var list = new System.Collections.Generic.List<StackedObstacleEntry>(
            level.stackedObstacles ?? System.Array.Empty<StackedObstacleEntry>()) { entry };
        level.stackedObstacles = list.ToArray();
        if (selectedObstacle == ObstacleId.TarpStatue)
            level.SetTarpStatue(originIdx, selectedTarpColor, selectedTarpCount, selectedTarpSize);
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
            var rfoot = level.GetObstacleFootprint(e.obstacleId, e.originCellIndex, def);
            int w = rfoot.x, h = rfoot.y;
            int cx = cellIndex % W, cy = cellIndex / W;
            if (cx >= ox && cx < ox + w && cy >= oy && cy < oy + h)
                list.RemoveAt(s);
        }
        level.stackedObstacles = list.ToArray();
    }

    private void PlaceTube(LevelData level, int bx, int by)
    {
        if (!level.InBounds(bx, by)) return;
        int originIdx = level.Index(bx, by);
        tubeStatus = null;

        // Mevcut bir tüpün hücresine tıklamak o tüpü siler (palet yardım metnindeki davranış).
        if (RemoveTubeAtCell(level, originIdx))
            return;

        if (level.cells[originIdx] != (int)CellType.Normal)
        {
            tubeStatus = $"({bx},{by}) boş (mask) hücre — tüp normal hücreden başlamalı.";
            return;
        }

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
            tubeStatus = $"({bx},{by}) → {selectedTubeDir} yönünde {entry.length} hücre board dışına taşıyor. " +
                         "Yönü veya uzunluğu değiştir (Up = tıklanan hücreden YUKARI uzanır).";
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning($"[TubeEditor] {tubeStatus}");
            return;
        }
        foreach (int ci in cells)
        {
            int cx = ci % level.width, cy = ci / level.width;
            if (level.cells[ci] != (int)CellType.Normal)
            {
                tubeStatus = $"Tüp ({cx},{cy}) hücresinden geçiyor ama orası boş (mask) hücre.";
                if (GameDiagnostics.EditorTools)
                    Debug.LogWarning($"[TubeEditor] {tubeStatus}");
                return;
            }
            var existingObs = (ObstacleId)level.obstacles[ci];
            if (existingObs != ObstacleId.None && existingObs != ObstacleId.Tube)
            {
                tubeStatus = $"Tüp ({cx},{cy}) hücresinden geçiyor ama orada {existingObs} var. Önce Erase ile temizle.";
                if (GameDiagnostics.EditorTools)
                    Debug.LogWarning($"[TubeEditor] {tubeStatus}");
                return;
            }
            if (TubeIndexAtCell(level, ci) >= 0)
            {
                tubeStatus = $"Tüp ({cx},{cy}) hücresinde başka bir tüple çakışıyor.";
                if (GameDiagnostics.EditorTools)
                    Debug.LogWarning($"[TubeEditor] {tubeStatus}");
                return;
            }
        }

        var list = new System.Collections.Generic.List<TubeEntry>(level.tubes) { entry };
        level.tubes = list.ToArray();
    }

    // Hücreyi kaplayan tüp(ler)i siler; en az biri silindiyse true.
    private bool RemoveTubeAtCell(LevelData level, int cellIndex)
    {
        if (level.tubes == null || level.tubes.Length == 0) return false;

        var list = new System.Collections.Generic.List<TubeEntry>(level.tubes);
        bool removed = false;
        for (int t = list.Count - 1; t >= 0; t--)
        {
            if (!TubeCovers(level, list[t], cellIndex)) continue;
            list.RemoveAt(t);
            removed = true;
        }
        if (removed) level.tubes = list.ToArray();
        return removed;
    }

    private static int TubeIndexAtCell(LevelData level, int cellIndex)
    {
        if (level.tubes == null) return -1;
        for (int t = 0; t < level.tubes.Length; t++)
            if (TubeCovers(level, level.tubes[t], cellIndex)) return t;
        return -1;
    }

    private static bool TubeCovers(LevelData level, TubeEntry entry, int cellIndex)
    {
        int[] cells = TubeObstacleService.GetCellIndices(entry, level.width, level.height);
        if (cells == null) return false;
        foreach (int ci in cells) if (ci == cellIndex) return true;
        return false;
    }

    private static void EnsureHamsterSatietyArray(LevelData level)
    {
        int size = level.width * level.height;
        if (level.hamsterSatietyByCell == null || level.hamsterSatietyByCell.Length != size)
        {
            var old = level.hamsterSatietyByCell;
            level.hamsterSatietyByCell = new int[size];
            if (old != null) System.Array.Copy(old, level.hamsterSatietyByCell, Mathf.Min(size, old.Length));
        }
    }

    private void ClearCell(LevelData level, int idx)
    {
        level.ClearColorCrateInitialHitsAt(idx);
        if (level.hamsterSatietyByCell != null && idx < level.hamsterSatietyByCell.Length)
            level.hamsterSatietyByCell[idx] = 0;
        bool wasWall = IsWallId((ObstacleId)level.obstacles[idx]);
        level.obstacles[idx] = (int)ObstacleId.None;
        level.obstacleOrigins[idx] = -1;
        if (wasWall) RecomputeWallOrigins(level);   // silinen hücre parçayı ikiye bölmüş olabilir
    }

    private void StampObstacle(LevelData level, int ax, int ay, ObstacleId id)
    {
        // Tube/Magnet/Safe ham obstacle olarak basılamaz: runtime yalnız LevelData.tubes/magnets/safes
        // girişlerinden kurar (GridSpawner generic görsel de çizmez) → görünmez, bozuk hücre olur.
        if (id == ObstacleId.Tube || id == ObstacleId.Magnet || id == ObstacleId.Safe || id == ObstacleId.AncientSeal
            || id == ObstacleId.WaterChannel)
        {
            if (GameDiagnostics.EditorTools)
                Debug.LogWarning($"[LevelEditor] {id} Obstacle modunda yerleştirilemez — kendi sekmesini ({id}) kullan.");
            return;
        }

        if (IsWallId(id))
        {
            if (!level.InBounds(ax, ay)) return;
            int wIdx = level.Index(ax, ay);
            if (level.cells[wIdx] != (int)CellType.Normal) return;
            level.ClearColorCrateInitialHitsAt(wIdx);
            level.obstacles[wIdx] = (int)id;
            if (level.wallPieceIds == null || level.wallPieceIds.Length != level.width * level.height)
                RecomputeWallOrigins(level);   // diziyi boyutlandırır
            level.wallPieceIds[wIdx] = Mathf.Max(1, selectedWallPiece);
            RecomputeWallOrigins(level);
            return;
        }

        var library = level.obstacleLibrary;
        var def = library != null ? library.Get(id) : null;
        Vector2Int size = PlacementFootprint(id, def);
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
        if (StackedObstacleEntry.SupportsInitialHits(id)
            && UpdateExistingStackedCrate(level, originIdx, id)) return;
        for (int y = ay; y < ay + h; y++)
        for (int x = ax; x < ax + w; x++)
        {
            int idx = level.Index(x, y);
            level.ClearColorCrateInitialHitsAt(idx);
            level.obstacles[idx] = (int)id;
            level.obstacleOrigins[idx] = originIdx;
        }
        level.obstacleOrigins[originIdx] = originIdx;
        if (StackedObstacleEntry.SupportsInitialHits(id))
            level.SetColorCrateInitialHits(originIdx, id, selectedInitialCrateHits);
        EnsureHamsterSatietyArray(level);
        for (int y = ay; y < ay + h; y++)
        for (int x = ax; x < ax + w; x++)
            level.hamsterSatietyByCell[level.Index(x, y)] = 0;
        if (id == ObstacleId.Hamster)
            level.hamsterSatietyByCell[originIdx] = Mathf.Max(1, selectedHamsterSatiety);
        if (id == ObstacleId.TarpStatue)
            level.SetTarpStatue(originIdx, selectedTarpColor, selectedTarpCount, selectedTarpSize);
        // Başka engel duvar hücrelerinin üstüne basıldıysa kalan duvar parçalarını yeniden kur.
        if (level.wallPieceIds != null && System.Array.Exists(level.wallPieceIds, p => p > 0))
            RecomputeWallOrigins(level);
    }

    // Normal Obstacle fırçası, çamur vb. üzerinde zaten duran sandığın yalnız aşamasını değiştirir.
    private bool UpdateExistingStackedCrate(LevelData level, int origin, ObstacleId id)
    {
        var entries = level.stackedObstacles;
        if (entries == null) return false;
        int top = -1;
        for (int i = 0; i < entries.Length; i++)
            if (entries[i].originCellIndex == origin
                && (top < 0 || entries[i].stackOrder >= entries[top].stackOrder))
                top = i;
        if (top < 0 || entries[top].obstacleId != id) return false;
        var entry = entries[top];
        entry.initialRemainingHits = selectedInitialCrateHits;
        entries[top] = entry;
        return true;
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

    private void DrawChannelPalette(LevelData level)
    {
        EditorGUILayout.LabelField("Su Kanalı", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1) İlk tıklama = DEPO: herhangi bir oynanır hücre (oyunda taş içinden geçer, üstünde çeşme durur).\n" +
            "2) Sonraki hücreler: bir öncekinin dik komşusu; YUKARI çıkamaz (aşağı ya da yana).\n" +
            "3) Son hücre: tahtanın sol, sağ ya da alt kenarına (veya bir boşluğa) değmeli; su oradan çıkar.\n" +
            "Son eklenen hücreye tekrar tıklamak onu geri alır. Erase modu: kanalı siler.",
            MessageType.Info);

        EditorGUILayout.LabelField($"Oluşturulan yol: {channelPathBuilding.Count} hücre", EditorStyles.miniLabel);
        if (!string.IsNullOrEmpty(channelStatus))
            EditorGUILayout.HelpBox(channelStatus, MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(channelPathBuilding.Count < 2);
        if (GUILayout.Button("Kanalı Kaydet"))
            FinalizeChannel(level);
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("Yolu Temizle"))
        {
            channelPathBuilding.Clear();
            channelStatus = null;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        int count = level.waterChannels != null ? level.waterChannels.Length : 0;
        EditorGUILayout.LabelField($"Mevcut kanallar: {count}", EditorStyles.miniLabel);
        if (count > 0)
        {
            for (int i = 0; i < count; i++)
                if (!WaterChannelEntry.Validate(level, level.waterChannels[i].pathCellIndices, out string err))
                    EditorGUILayout.HelpBox($"Kanal {i + 1}: {err}", MessageType.Error);
            if (GUILayout.Button("Tüm kanalları temizle"))
            {
                Undo.RecordObject(level, "Clear All Water Channels");
                level.waterChannels = System.Array.Empty<WaterChannelEntry>();
                EditorUtility.SetDirty(level);
            }
        }
    }

    private void AddChannelPathCell(LevelData level, int cellIndex)
    {
        if (cellIndex < 0 || cellIndex >= level.cells.Length) return;
        channelStatus = null;
        int n = channelPathBuilding.Count;
        if (n > 0 && channelPathBuilding[n - 1] == cellIndex) { channelPathBuilding.RemoveAt(n - 1); return; }   // geri al
        if (level.cells[cellIndex] != (int)CellType.Normal) { channelStatus = "Bu hücre oynanır değil."; return; }
        if (channelPathBuilding.Contains(cellIndex)) { channelStatus = "Bu hücre yolda zaten var."; return; }
        if (IsChannelCell(level, cellIndex)) { channelStatus = "Bu hücre başka bir kanalda."; return; }
        int w = level.width;
        if (n > 0)
        {
            int p = channelPathBuilding[n - 1];
            if (Mathf.Abs(cellIndex % w - p % w) + Mathf.Abs(cellIndex / w - p / w) != 1)
            { channelStatus = "Yeni hücre bir öncekinin dik komşusu olmalı."; return; }
            if (cellIndex / w < p / w)
            { channelStatus = "Kanal yukarı çıkamaz (su yalnız aşağı ya da yana akar)."; return; }
        }
        channelPathBuilding.Add(cellIndex);
    }

    private static bool IsChannelCell(LevelData level, int cellIndex)
    {
        if (level.waterChannels == null) return false;
        foreach (var e in level.waterChannels)
            if (e.pathCellIndices != null && System.Array.IndexOf(e.pathCellIndices, cellIndex) >= 0) return true;
        return false;
    }

    private void FinalizeChannel(LevelData level)
    {
        var path = channelPathBuilding.ToArray();
        if (!WaterChannelEntry.Validate(level, path, out string err)) { channelStatus = err; return; }
        Undo.RecordObject(level, "Add Water Channel");
        var list = new System.Collections.Generic.List<WaterChannelEntry>(level.waterChannels ?? System.Array.Empty<WaterChannelEntry>())
            { new WaterChannelEntry { pathCellIndices = path } };
        level.waterChannels = list.ToArray();
        channelPathBuilding.Clear();
        channelStatus = null;
        EditorUtility.SetDirty(level);
    }

    private void RemoveChannelAtCell(LevelData level, int cellIndex)
    {
        if (level.waterChannels == null || level.waterChannels.Length == 0) return;
        var list = new System.Collections.Generic.List<WaterChannelEntry>(level.waterChannels);
        list.RemoveAll(e => e.pathCellIndices != null && System.Array.IndexOf(e.pathCellIndices, cellIndex) >= 0);
        level.waterChannels = list.ToArray();
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
        selectedTubeLength = EditorGUILayout.IntSlider("Uzunluk (hücre)", selectedTubeLength, 2, LevelData.MaxHeight);
        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(level);

        if (!string.IsNullOrEmpty(tubeStatus))
            EditorGUILayout.HelpBox(tubeStatus, MessageType.Warning);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Mevcut tüpler: {(level.tubes != null ? level.tubes.Length : 0)}", EditorStyles.miniLabel);

        if (level.tubes != null && level.tubes.Length > 0 && GUILayout.Button("Tüm tüpleri temizle"))
        {
            Undo.RecordObject(level, "Clear All Tubes");
            level.tubes = System.Array.Empty<TubeEntry>();
            EditorUtility.SetDirty(level);
        }
    }

    private void DrawAncientSealPalette(LevelData level)
    {
        EditorGUILayout.LabelField("Ancient Seal (Antik Mühür)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Grid'e tıkla: sol üst hücreden başlayan mühür eklenir. Altındaki içerik korunur. " +
            "Tahtanın herhangi bir yerinde, sayaçtaki renkten belirtilen sayıda taş topla. " +
            "Auto başlangıçları kırmızı, yeşil, sarı olarak dağıtır; sonra renk döngüsü sürer. " +
            "Her taş yalnız bir mühre gider; aynı rengi isteyenler taşları sırayla paylaşır. " +
            "Kenardan veya special ile doğrudan vurmak sayılmaz; " +
            "special'ın topladığı uygun renk taşlar sayılır. Erase mührü kaldırır.", MessageType.Info);
        selectedSealStartColor = (AncientSealStartColor)EditorGUILayout.EnumPopup("Yeni mühür başlangıcı", selectedSealStartColor);
        selectedSealW = EditorGUILayout.IntSlider("Genişlik (hücre)", selectedSealW, 2, 9);
        selectedSealH = EditorGUILayout.IntSlider("Yükseklik (hücre)", selectedSealH, 2, 11);
        selectedSealRed = EditorGUILayout.IntSlider("Kırmızı taş sayısı", selectedSealRed, 1, 100);
        selectedSealYellow = EditorGUILayout.IntSlider("Sarı taş sayısı", selectedSealYellow, 1, 100);
        selectedSealGreen = EditorGUILayout.IntSlider("Yeşil taş sayısı", selectedSealGreen, 1, 100);
        EditorGUILayout.LabelField($"Mevcut mühürler: {level.ancientSeals?.Length ?? 0}");
        if (level.ancientSeals == null || level.ancientSeals.Length == 0) return;
        for (int i = 0; i < level.ancientSeals.Length; i++)
        {
            var entry = level.ancientSeals[i];
            EditorGUI.BeginChangeCheck();
            var startColor = (AncientSealStartColor)EditorGUILayout.EnumPopup(
                $"Mühür ({entry.originCellIndex % level.width}, {entry.originCellIndex / level.width}) başlangıcı", entry.startColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(level, "Change Ancient Seal Start Color");
                entry.startColor = startColor;
                level.ancientSeals[i] = entry;
                EditorUtility.SetDirty(level);
            }
        }
        if (GUILayout.Button("Seçili taş sayılarını mevcut mühürlere uygula"))
        {
            Undo.RecordObject(level, "Update Ancient Seal Counts");
            for (int i = 0; i < level.ancientSeals.Length; i++)
            {
                var entry = level.ancientSeals[i];
                entry.redCount = selectedSealRed;
                entry.yellowCount = selectedSealYellow;
                entry.greenCount = selectedSealGreen;
                level.ancientSeals[i] = entry;
            }
            EditorUtility.SetDirty(level);
        }
        if (GUILayout.Button("Tüm mühürleri temizle"))
        {
            Undo.RecordObject(level, "Clear Ancient Seals");
            level.ancientSeals = System.Array.Empty<AncientSealEntry>();
            EditorUtility.SetDirty(level);
        }
    }

    private void DrawSafePalette(LevelData level)
    {
        EditorGUILayout.LabelField("Safe (Kasa) Obstacle", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Önce içeriği (mud/para/taş/obstacle) NORMAL şekilde yerleştir, SONRA Safe'i üstüne koy.\n" +
            "Grid'de bir hücreye tıkla → o hücre sol-ÜST origin olur, WxH bölgeyi kaplar.\n" +
            "Erase modu veya bölgeye tekrar tıklamak kasayı siler. (İçerik silinmez, sadece overlay.)",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        selectedSafeW = EditorGUILayout.IntSlider("Genişlik (hücre)", selectedSafeW, 1, 9);
        selectedSafeH = EditorGUILayout.IntSlider("Yükseklik (hücre)", selectedSafeH, 1, 11);
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

        if (level.safes != null && level.safes.Length > 0
            && GUILayout.Button("Seçili kilit ayarlarını mevcut kasalara uygula"))
        {
            ApplySelectedSafeLockSettingsToExistingSafes(level);
        }

        if (level.safes != null && level.safes.Length > 0 && GUILayout.Button("Tüm kasaları temizle"))
        {
            Undo.RecordObject(level, "Clear All Safes");
            level.safes = System.Array.Empty<SafeEntry>();
            EditorUtility.SetDirty(level);
        }
    }

    private void ApplySelectedSafeLockSettingsToExistingSafes(LevelData level)
    {
        if (level == null || level.safes == null || level.safes.Length == 0)
            return;

        Undo.RecordObject(level, "Apply Safe Lock Settings");

        for (int i = 0; i < level.safes.Length; i++)
        {
            var entry = level.safes[i];
            entry.redHits = Mathf.Max(1, selectedSafeRed);
            entry.yellowHits = Mathf.Max(1, selectedSafeYellow);
            entry.greenHits = Mathf.Max(1, selectedSafeGreen);
            entry.lockHitMode = selectedSafeHitMode;
            entry.firstLock = selectedSafeFirstLock;
            entry.secondLock = selectedSafeSecondLock;
            entry.thirdLock = selectedSafeThirdLock;
            level.safes[i] = entry;
        }

        EditorUtility.SetDirty(level);
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

    private void DrawObstaclePreview(ObstacleLibrary library, ObstacleDef def, Rect rect, float padding, int remainingHits = 0)
    {
        if (def == null) return;
        if (GelLauncherPressPreview.TryDraw(def.id, rect, padding, remainingHits)) return;
        if (def.id == ObstacleId.GrassWaterLily)
        {
            DrawSpriteInRect(library != null ? library.Get(ObstacleId.Grass)?.GetPreviewSprite() : null, rect, padding);
            float grow = (GrassWaterLilyService.FlowerSizeRatio - 1) * (rect.width - padding * 2) * 0.5f;
            rect = new Rect(rect.x - grow, rect.y - grow, rect.width + grow * 2, rect.height + grow * 2);
        }
        // Mud's stage sprite is the underlay; palette/grid previews use its bordered display icon.
        var sprite = remainingHits > 0 ? def.GetSpriteForRemainingHits(remainingHits)
            : def.id == ObstacleId.Mud ? def.GetGoalIconSprite() : def.GetPreviewSprite();
        DrawSpriteInRect(sprite, rect, padding);
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
