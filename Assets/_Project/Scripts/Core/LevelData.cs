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
    EnergyOrb = 1,

    // Boss Duel modunda boss'a verilen hasar. Goal amount = boss HP;
    // BossDuelController her temizlenen taş için bu goal'ü ilerletir.
    BossDamage = 2
}

public enum LevelKind : int
{
    Normal = 0,

    // Robot düellosu mini-oyunu (her 5 levelde bir). Boss HP'si
    // Collectible/BossDamage goal'üyle tanımlanır; boss her N hamlede
    // board'a oil fırlatır. BossDuelController bu modda devreye girer.
    BossDuel = 1
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

    // Cabinet-style obstacle. First hit opens the door; each subsequent normal
    // hit removes one item inside (front-to-back depth order).
    Wardrobe = 27,

    // Multi-stage sculpting obstacle. Looks like a solid stone, reveals a carved
    // shape progressively with each hit. Standard OverTileBlocker behavior.
    SculptingStone = 28,

    // Paired magnet obstacle. Two magnets connected by a glowing energy path.
    // Hit a magnet to move it toward the other. They vanish when they meet.
    // Cells are stamped into obstacles[] at runtime from LevelData.magnets[].
    Magnet = 29,

    // Single-hit movable obstacle. Falls with gravity like plastic_orange.
    HelmetPorcelain = 30,

    // Hat-shaped dispenser. Each hit launches one energy orb toward the goal.
    // Managed by HatLauncherService. Stays on board until manually cleared.
    HatLauncher = 31,

    // Bonus coin. Single-hit movable (falls like plastic_orange / HelmetPorcelain).
    // Collected when an adjacent match clears it → +1 coin to PlayerWallet (GoldCoinCollectService).
    // Not a level goal; pure bonus. Often hidden under a Safe.
    GoldMoney = 32,

    // Multi-cell vault that COVERS an NxN region (its cells keep their authored content
    // underneath, frozen/locked). 3 sequential locks (red→yellow→green), each with its own
    // hit count; each hit drops the active lock's knob a step. When all 3 are emptied the
    // safe breaks and the underlying content joins the board. Authored via LevelData.safes[].
    // Managed by SafeObstacleService; usually a level GOAL.
    Safe = 33,

    // "Drop-to-bottom" collectible. Falls like a MovableObstacle (gravity), but is UNBREAKABLE
    // (no source damages it). Collected when it reaches the bottom row and drops off the board —
    // that's when the goal counts (+1). Straight-down only (no diagonal slide). Authored by the
    // designer (placed via the obstacle palette). Requires ObstacleDef.exitAtBottom = true.
    Cargo = 34,

    // Single-hit movable shield pickups (fall with gravity like HelmetPorcelain). In a
    // BossDuel level, breaking one grants timed damage-immunity to the matching robot:
    //   PlayerShieldPickup → shields the PLAYER robot (absorbs enemy bolts for a few seconds)
    //   EnemyShieldPickup  → shields the ENEMY robot (player's bolts are absorbed)
    // Outside BossDuel they behave as plain breakable movable blocks (BossDuelController gates
    // the shield effect on bossModeActive). See BossDuelController.HandleObstacleVisualChanged.
    PlayerShieldPickup = 35,
    EnemyShieldPickup = 36,

    // Single-hit blocker barrel. Breaking it splatters Mud across a ~4x4 region
    // centered on the barrel (droplets scatter outward like a burst). Standard
    // OverTileBlocker behavior for damage; the spread + mud-spawn is orchestrated
    // by BoardController (BarrelSpreadAction) after the board settles. The resulting
    // Mud is usually the level GOAL (dynamic count grows as barrels break).
    Barrel = 37,

    // Color-keyed rocket battery. Holds 3 rockets (default Core=red, Gear=yellow,
    // Bolt=blue). An adjacent normal match of a loaded color launches that rocket;
    // the rocket uses the SAME targeting/impact as PatchBot (RocketBasketService +
    // RocketBasketLaunchAction). Each color fires once; when empty the basket clears.
    // Interceptor-managed — never breaks via generic damage. Not a goal.
    RocketBasket = 38,

    // SculptingStone'un ikizi: birebir aynı çok-stage OverTileBlocker davranışı, yalnızca
    // farklı sprite seti kullanılır. Tek fark: yalnızca SPECIAL ile hasar alır (ObstacleDef
    // stage'lerinde damageRule = SpecialOnly). Koda gömülü özel mantığı yoktur — tamamen
    // ObstacleLibrary asset'inden konfigüre edilir (SculptingStone gibi).
    SculptingSpecial = 39,

    // İki-stage movable blocker. Plastic/HelmetPorcelain gibi gravity ile düşer/swap olur,
    // ama hits=2: ilk vuruş sprite'ı 2. stage'e çevirir, ikinci vuruş kırar. Koda gömülü
    // mantığı yok — ObstacleDef'te behavior=MovableObstacle, hits=2, iki stage sprite ile
    // konfigüre edilir. (İsim serbestçe değiştirilebilir; değer 40 sabit kalmalı.)
    PlasticTwoStage = 40,

    // Barrel'in 4-stage versiyonu. İlk 3 hit sadece ObstacleDef stage sprite'ını değiştirir;
    // son hitte Barrel ile aynı şekilde Mud saçar. ObstacleDef: hits=4,
    // stages[0..3] OverTileBlocker olarak konfigüre edilmeli.
    Barrell_v2 = 41,

    // Dekoratif bitki örtüsü (grass). Gameplay'e KARIŞMAZ: hücreyi bloke etmez, swap/match
    // kilitlemez, gravity geçer — altındaki taş tamamen normal oynanır. obstacles[]'te yalnızca
    // işaretçi olarak durur; görselini (2 sprite geçmeli/seamless, hit'te sallanan) ve komşu-match
    // hasarını GrassOverlayService yönetir. Grass kendi hücresindeki match'ten HASAR ALMAZ; yalnızca
    // KOMŞU (görünür yan) hücrelerde bir taş temizlenince aşınır, N hit sonra kalkar.
    Grass = 42,

    // 2x2 renk şarj kutusu. BatteryBox gibi Gear/Core/Bolt/Plate hit'lerini ayrı tutar;
    // her geçerli hit orta progress'i ilerletir. Dört renk tamamen boşalınca patlar ve
    // board'u bir kez override tarzı temizler.
    OverrideBatteryBox = 43,

    // Static generator obstacle. Each valid hit produces a matchable Key tile on
    // the board until the level's Tile/Key goal capacity is reached.
    KeyGenerator = 44,
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

[System.Serializable]
public struct MagnetEntry
{
    [Tooltip("Sıralı yol hücreleri: ilk eleman MagnetA başlangıcı, son eleman MagnetB başlangıcı.\n" +
             "Flat index (y*width+x). En az 2 hücre gereklidir.")]
    public int[] pathCellIndices;
}

public enum SafeLockHitMode : int
{
    Ordered = 0,
    AnyColor = 1
}

public enum SafeLockColor : int
{
    Red = 0,
    Yellow = 1,
    Green = 2
}

[System.Serializable]
public struct SafeEntry
{
    [Tooltip("Kasanın SOL-ÜST hücresinin flat index'i (y*width+x).")]
    public int originCellIndex;
    [Tooltip("Kasanın kapladığı hücre boyutu (2x2, 3x3, 4x4...).")]
    [Min(1)] public int width;
    [Min(1)] public int height;
    [Tooltip("Kilit hit sayıları — sıra: KIRMIZI, SARI, YEŞİL. Aktif kilit sırayla düşer; " +
             "her kilit bitince knob en alta iner ve sıradakine geçilir.")]
    [Min(1)] public int redHits;
    [Min(1)] public int yellowHits;
    [Min(1)] public int greenHits;
    [Tooltip("Ordered: sadece sıradaki kilit hasar alır. AnyColor: hangi renk tile vurursa o kilit hasar alır.")]
    public SafeLockHitMode lockHitMode;
    [Tooltip("Ordered modda kilit sırası. AnyColor modda special/booster için öncelik sırası.")]
    public SafeLockColor firstLock;
    public SafeLockColor secondLock;
    public SafeLockColor thirdLock;
}

[System.Serializable]
public struct StackedObstacleEntry
{
    [Tooltip("Üstteki obstacle'ın SOL-ÜST (origin) hücresinin flat index'i (y*width+x).")]
    public int originCellIndex;
    [Tooltip("Altındaki AUTHORED içeriğin (Mud, Stone...) üstüne konacak obstacle. " +
             "Kapladığı NxN boyut obstacle'ın kendi def.size'ından gelir.")]
    public ObstacleId obstacleId;
}

/// <summary>
/// BossDuel dalga tanımı. LevelData.bossWaves DOLUYSA her eleman bir düşman dalgasıdır;
/// 0/-1 bırakılan sayısal alanlar level'daki Battlefield alanlarından devralınır.
/// Liste BOŞSA dalgalar BossDifficulty formülünden üretilir (Docs/BossDuel_Plan.md).
/// </summary>
[System.Serializable]
public class BossWaveDef
{
    [Tooltip("Bu dalganın toplam boss HP'sinden (BossDamage goal amount) aldığı pay. " +
             "Dalgalar arasında normalize edilir; hepsi eşitse HP eşit bölünür.")]
    [Min(0f)] public float hpWeight = 1f;

    [Header("Saldırı")]
    [Tooltip("Düşman kaç saniyede bir ateş eder. 0 = level'daki enemyAttackInterval.")]
    [Min(0f)] public float attackInterval = 0f;
    [Tooltip("İlk saldırı hasarı. 0 = level'daki enemyAttackBaseDamage.")]
    [Min(0)] public int attackDamageBase = 0;
    [Tooltip("Saldırı başına hasar artışı. -1 = level'daki enemyAttackDamageGrowth.")]
    [Min(-1)] public int attackDamageGrowth = -1;

    [Header("Oil Baskısı")]
    [Tooltip("Oil saldırısı başına fırlatılan oil. 0 = bu dalgada oil yok, -1 = level'daki bossAttackOilCount.")]
    [Min(-1)] public int oilCount = -1;
    [Tooltip("Kaç hamlede bir oil. -1/0 = level'daki bossAttackEveryMoves.")]
    [Min(-1)] public int oilEveryMoves = -1;

    [Header("Görsel Varyant")]
    [Tooltip("Bu dalganın robot gövde sprite'ı. Manual bossWaves içinde boşsa EnemyShieldPickup obstacle sprite'ına, o da boşsa sahnedeki mevcut gövdeye düşer.")]
    public Sprite bodySprite;
    [Tooltip("Bu dalga yenilince kullanılacak yığın sprite'ı. Boşsa controller'daki default.")]
    public Sprite defeatedSprite;
    [Tooltip("Gövdeye uygulanacak tint (beyaz = değişiklik yok). Sprite çizmeden dalga varyantı sağlar.")]
    public Color bodyTint = Color.white;
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

    [Header("Level Kind")]
    [Tooltip("BossDuel: robot düellosu mini-oyunu. Boss HP'si için Collectible/BossDamage " +
             "goal'ü ekleyin (amount = HP, iconOverride = boss ikonu).")]
    public LevelKind levelKind = LevelKind.Normal;

    [Tooltip("Bu level'e girerken DEFAULT chapter loading screen yerine, aşağıdaki iki parçanın " +
             "soldan/sağdan gelip ortada birleştiği özel intro 'load' olarak gösterilir. Intro " +
             "kalıcı katmanda, sahne ASENKRON yüklenirken oynar → board hiç flash etmez. " +
             "İşaret kapalıysa VEYA iki sprite'tan biri boşsa default load çalışır.")]
    public bool usesCustomIntro = false;
    [Tooltip("Soldan gelen parça (tam-ekran, transparan padding'li yarı).")]
    public Sprite introLeftSprite;
    [Tooltip("Sağdan gelen parça (tam-ekran, transparan padding'li yarı).")]
    public Sprite introRightSprite;
    [Tooltip("Parçaların ortada birleşme süresi (sn).")]
    [Min(0.05f)] public float introSlideInDuration = 0.6f;
    [Tooltip("Birleştikten sonra bekleme süresi (sn). Sahne bu süre içinde yüklenmezse intro hazır olana kadar bekler.")]
    [Min(0f)] public float introHoldDuration = 0.8f;

    [Header("Boss Duel")]
    [Tooltip("Boss kaç oyuncu hamlesinde bir EKSTRA oil saldırısı yapar (lazer her tur vurur, bu sadece oil sıklığı).")]
    [Min(1)] public int bossAttackEveryMoves = 3;
    [Tooltip("Her oil saldırısında board'a fırlatılan oil sayısı. 0 = oil yok, sadece lazer.")]
    [Min(0)] public int bossAttackOilCount = 2;

    [Header("Battlefield (BossDuel)")]
    [Tooltip("Oyuncu (sol robot) başlangıç/maks canı. 0 olunca level kaybedilir. Düşman HP'si BossDamage goal amount'tan gelir.")]
    [Min(1)] public int playerMaxHp = 560;
    [Tooltip("Temizlenen taş başına düşmana verilen hasar (her taş = 1 vuruş).")]
    [Min(0)] public int damagePerClearedTile = 10;
    [Tooltip("Düşmanın ilk saldırısındaki temel hasar.")]
    [Min(0)] public int enemyAttackBaseDamage = 20;
    [Tooltip("Düşman saldırı hasarının her saldırıda artış miktarı (telgraflı yükseliş).")]
    [Min(0)] public int enemyAttackDamageGrowth = 6;
    [Tooltip("Düşman kaç saniyede bir ateş eder (idle dahil). 0 = controller'daki default kullanılır.")]
    [Min(0f)] public float enemyAttackInterval = 2f;
    [Tooltip("Battlefield arena arka planı. ATANIRSA bu kullanılır; BOŞSA sahnedeki mevcut arka plan kalır.")]
    public Sprite battlefieldBackground;

    [Header("Boss Waves (BossDuel)")]
    [Tooltip("Dalga listesi. BOŞSA dalga sayısı bossWaveCount/formülden gelir ve parametreler " +
             "yukarıdaki Battlefield alanlarından BossDifficulty eskalasyonuyla türetilir " +
             "(eski tek-dalga bosslar hiç dokunmadan çalışır). DOLUYSA tam manuel kontrol.")]
    public BossWaveDef[] bossWaves;
    [Tooltip("bossWaves boşken dalga sayısı. 0 = otomatik: boss index (current_level/5) " +
             "BossDifficulty.AutoWaveCount ile belirler (erken bosslar 1, sonra 2, sonra 3).")]
    [Min(0)] public int bossWaveCount = 0;

    [Header("Random Pool")]
    [Tooltip("Bu levelda random üretilecek taş tipleri. DOLUYSA GridSpawner'daki varsayılan " +
             "havuz hiç kullanılmaz, yalnızca buradakiler üretilir. Boşsa GridSpawner'daki geçerlidir.")]
    public TileType[] randomPool;

    [Header("Energy Container")]
    [Tooltip("How many EnergyOrb collectibles each EnergyContainer releases in this level. EnergyContainerRuntime can still provide a fallback, but level data owns the tuning.")]
    [Min(1)] public int energyPerContainer = 10;

    [Header("Rocket Basket")]
    [Tooltip("AÇIKSA: bu leveldeki TÜM RocketBasket sepetleri tek vuruşta yüklü kalan bütün " +
             "roketleri aynı anda fırlatır (renk aranmaz, sepet o vuruşta tamamen boşalıp kalkar). " +
             "KAPALI (varsayılan): her renk kendi eşleşmesinde ayrı ayrı fırlar (Core→Gear→Bolt).")]
    public bool rocketBasketFireAllOnHit = false;

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

    [Tooltip("Magnet pair obstacles. Cells are stamped into obstacles[] at runtime by GridSpawner.")]
    public MagnetEntry[] magnets;

    [Tooltip("Safe (kasa) kapakları. Kapladıkları NxN bölgenin altındaki içerik dokunulmaz kalır; " +
             "kasa kırılınca açılır. Cells runtime'da SafeObstacleService ile işlenir.")]
    public SafeEntry[] safes;

    [Tooltip("Üst üste bindirilmiş obstacle'lar (generic stacking). Her entry, kapladığı hücrelerdeki " +
             "AUTHORED içeriği (Mud, Stone vb.) 'beneath' olarak saklayıp üstüne obstacleId'yi stamp eder; " +
             "üstteki obstacle kırılınca alttaki geri açılır. Safe ile aynı beneath mekanizmasının her " +
             "obstacle için çalışan generic hâli. Runtime'da GridSpawner + ObstacleStateService işler.")]
    public StackedObstacleEntry[] stackedObstacles;

    [Tooltip("Sabitlenmiş taş tipleri. 0 = rastgele (None), diğerleri TileType+1 değeri.\n" +
             "size = width*height. GridSpawner spawn sırasında simulation yerine bu değeri kullanır.")]
    public int[] pinnedTileTypes;

    [Tooltip("Sabitlenmiş special'lar. 0 = yok (None), diğerleri TileSpecial enum değeri.\n" +
             "size = width*height.")]
    public int[] pinnedSpecialTypes;

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

        if (magnets == null)
            magnets = System.Array.Empty<MagnetEntry>();

        if (safes == null)
            safes = System.Array.Empty<SafeEntry>();

        if (stackedObstacles == null)
            stackedObstacles = System.Array.Empty<StackedObstacleEntry>();

        if (pinnedTileTypes == null || pinnedTileTypes.Length != size)
            pinnedTileTypes = new int[size];

        if (pinnedSpecialTypes == null || pinnedSpecialTypes.Length != size)
            pinnedSpecialTypes = new int[size];

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
