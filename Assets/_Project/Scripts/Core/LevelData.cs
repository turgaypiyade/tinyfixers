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
    // Animal BossDuel: each pickup adds 2 persistent protection points to its character.
    // Legacy BossDuel retains player hit-count shields and enemy timed immunity.
    //   PlayerShieldPickup → protects the PLAYER; EnemyShieldPickup → protects the ENEMY.
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

    // Two-hit movable egg: cracks, then hatches into a bird that splits into three dives.
    // Keep the former WolfEgg numeric ID so authored levels and goals migrate in place.
    EggBird = 45,

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

    // Yayılan jel (SpreadingGel). MUD gibi UNDER-TILE (behavior=UnderTileLayered, drawUnderTiles=1;
    // grass DEĞİL — o taşın üstünde), TEK-STAGE (mud'un hits=2 çift katmanı YOK, hits=1), gameplay'e
    // karışmaz (blocksCells=0, locksInteraction=0, holdsTile=0): taş üstünde normal oynanır VE hareket
    // eder. HASAR ALMAZ / temizlenmez (damageRule=FullyDisabled) — HEDEF = tüm board'u jele
    // kaplamak (artan coverage goal). YAYILMA yalnız (a) jel hücresindeki taş SWAP'la yeni
    // hücreye geçince hedef hücreye, (b) jel hücresindeki bir special/combo (LineV/H, PulseCore,
    // Override, PatchBot) etkilediği HER hücreye jeli taşır. Gravity düşüşü YAYMAZ. Bulaşma
    // konumsaldır (taş yalnız jel hücresindeyken bulaşık; traveling special tetiklenirken kaynağın
    // jelde olup olmadığı yakalanır). Görsel + köşe birleştirme mud'dan (SpreadingGelOverlayService),
    // yayılma mantığı SpreadingGelService'te. ID sabit (level verisi).
    SpreadingGel = 46,

    // Boss düellosu "alet tehdidi" (gri): tek-vuruş, düşen (movable) engel; YALNIZ special kırar (damageRule=SpecialOnly). BossDuel'de
    // üstünde geri sayım rozeti durur (BossDuelToolThreat); oyuncu turunda azalır, 0 olunca düşman
    // aletini ekrana fırlatır (çatlak + kilit + ek saldırı) ve engel "harcanır" (sayaçsız kalır).
    // BossDuel dışında sıradan tek-vuruş engeldir. Başlangıçta editörle de yerleştirilebilir.
    ToolThreat = 47,

    // ToolThreat'in renkli versiyonları: YALNIZ kendi rengindeki komşu eşleşme (+ special) kırar
    // (ObstacleDef.restrictNormalMatchTileType). Renk eşlemesi renkli plastiklerle aynı:
    // Sarı=Gear, Kırmızı=Core, Mavi=Bolt, Yeşil=Plate. Gri ToolThreat (47) YALNIZ special ile kırılır (en zor).
    ToolThreatYellow = 48,
    ToolThreatRed = 49,
    ToolThreatBlue = 50,
    ToolThreatGreen = 51,

    // 2x2 su deposu. Standart OverTileBlocker, 2 stage (hits=2: sağlam → çatlak bidon, ikinci
    // vuruşta dağılır; stage sprite'ları ObstacleDef'ten). Kırılınca board'un RASTGELE
    // oynanabilir hücrelerine su fışkırır (göktaşı yayı: yükselir, hedefe düşer) → her varışta
    // WaterPuddle; aynı anda telefon ekranına yağmur damlaları (ScreenRainDropsFx). Saçılım
    // BoardController → WaterTankSpreadAction; sayı/görseller Resources/WaterTank/WaterTankConfig.
    WaterTank = 52,

    // Su birikintisi. Mud gibi UNDER-TILE (behavior=UnderTileLayered, blocksCells=0): taş üstünde
    // normal oynanır, o hücredeki taş temizlenince 1 vuruşta kurur. WaterTank'ın dinamik goal'ü
    // (Barrel→Mud kalıbı); editörle de doğrudan yerleştirilebilir.
    WaterPuddle = 53,

    // WaterTank'ın 1x1 versiyonu: davranış birebir aynı (2 stage, kırılınca rastgele su +
    // ekran damlası); boyut ObstacleDef.size'tan okunur.
    WaterTankSmall = 54,

    // Duvar (kiremit). Serbest şekilli PARÇALAR: bir parçanın tüm hücreleri aynı obstacleOrigins
    // değerini paylaşır (çok-hücreli tek engel; hedef parça başına sayılır). Her hücre KENDİ aşamasını
    // taşır (WallObstacleService): normal → çatlak1 (dinamit görünür) → çatlak2 (fitil yanar) → çatlak3 →
    // çatlak4; son aşamadaki hücreye bir vuruş daha gelirse (5. vuruş) parçanın tamamı yıkılır. Taş gibi hücreyi kapatır (blocksCells).
    // Görsel: WallObstacleService/WallPieceView (kenar/köşe autotile + rastgele kabartma).
    Wall = 55,

    // Metal duvar: Wall ile aynı parça sistemi/görünüm (metalik gri). Farkı: hücre vuruşları ARDIŞIK
    // olmalı — bir hamle (zincirleri dahil) bitince o hamlede vurulmayan hücre aşama 0'a döner
    // (kabartmalı duvar geri gelir). Aşamalar: 1. vuruş çatlak1, 2. vuruş çatlak3, 3. vuruş parça yıkılır.
    MetalWall = 56,

    // Aç Hamster (tek hücre, hareketli engel — kargo gibi düşer, swap edilir), KIRILMAZ. 4-komşu eşleşme/special
    // vuruşu = 1 lokma. Doyunca PatchBot hedefine yakın bir taşla yer değiştirerek zıplar, çevresi (3x3) vurulur;
    // hedef "X kez doyur". Hedef bitince mutlu ayrılır (engel kalkar).
    // Görsel/mantık: Grid/Hamster/HamsterObstacleService. Doyma eşiği: ObstacleDef.hamsterSatiety.
    Hamster = 57,

    // Çiçekli çim: Grass + 1 vuruşluk süs katmanı. Yalnız editör/level verisinde bu id ile durur;
    // runtime'da GridSpawner hücreyi Grass'a çevirir ve ObstacleStateService'e çiçekli hücre olarak
    // kaydeder (çimin kalan vuruşu +1). İlk vuruş (çimle aynı kaynaklar) çiçekleri döker → hedef
    // GrassFlower +1; hücre bundan sonra tamamen normal çimdir. Görsel: GrassFlowerOverlayService.
    // Grass gibi başka engellerin üstüne yığılabilir (saydam örtü).
    GrassFlower = 58,

    // Jel fırlatıcı (kartuş): sabit, hücre kapatan, YALNIZ special hasarı alan 2 aşamalı engel. Ağzın baktığı
    // yöne göre 4 ayrı id: Up/Down = 1x2 (dik), Left/Right = 2x1 (yatay). 2. vuruşta kırılınca kapak ağızdan
    // tahta kenarına kadar uçar; yolundaki her hücrede engel kalmayana kadar vurur, taşı kırar/special'ı
    // tetikler; jel 2 hücre geriden gelip yolu (ve kartuşun kendi hücrelerini) boyar.
    // Görsel/mantık: BoardController.HandleObstacleDestroyed → GelLauncherFireAction.
    GelLauncherUp = 59,
    GelLauncherDown = 60,
    GelLauncherLeft = 61,
    GelLauncherRight = 62,

    // Boya kutusu piramidi (2x2, sabit, hücre kapatan). Kasada 4-3-2 dizili 9 boya kutusu; bitişik her
    // eşleşme bir, her special vuruşu iki kutu düşürür (ObstacleStateService). Son kutu düşünce kasa kalkar.
    // Görsel: Grid/PaintCanBoxView — vuruşta tüm kutular sallanır, düşenler takla atarak uçar.
    PaintCanBox = 63,

    // Collect the displayed color anywhere on the board; direct hits do not advance it.
    AncientSeal = 64,
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
    [Tooltip("Yığın sırası (Docs/ObstacleStack_Plan.md): üst üste konan engeller (stackedObstacles + safes) " +
             "bu sayıya göre alttan üste kurulur. Eşitse eski davranış: önce stackedObstacles, sonra kasalar.")]
    public int stackOrder;
}

public enum AncientSealStartColor
{
    Auto = 0,
    Red = 1,
    Yellow = 2,
    Green = 3
}

[System.Serializable]
public struct AncientSealEntry
{
    public int originCellIndex;
    [Min(2)] public int width;
    [Min(2)] public int height;
    [Tooltip("Başlangıç rengi; Auto mühürleri kırmızı, yeşil, sarı başlangıçlara dengeli dağıtır.")]
    public AncientSealStartColor startColor;
    [Tooltip("Her rengin toplama adedi. Başlangıçtan itibaren kırmızı → sarı → yeşil döngüsü izlenir.")]
    [Min(1)] public int redCount;
    [Min(1)] public int yellowCount;
    [Min(1)] public int greenCount;
    public int stackOrder;
}

public enum PaintCanColor
{
    Auto = 0,
    Red = 1,
    Yellow = 2,
    Green = 3,
    Blue = 4,
}

[System.Serializable]
public struct PaintCanBoxColorEntry
{
    [Tooltip("Kasanın SOL-ÜST (origin) hücresinin flat index'i (y*width+x).")]
    public int originCellIndex;
    public PaintCanColor color;
}

[System.Serializable]
public struct StackedObstacleEntry
{
    [Tooltip("Üstteki obstacle'ın SOL-ÜST (origin) hücresinin flat index'i (y*width+x).")]
    public int originCellIndex;
    [Tooltip("Altındaki AUTHORED içeriğin (Mud, Stone...) üstüne konacak obstacle. " +
             "Kapladığı NxN boyut obstacle'ın kendi def.size'ından gelir.")]
    public ObstacleId obstacleId;
    [Tooltip("Yığın sırası: kasalarla (safes) birlikte alttan üste bu sayıya göre kurulur (SafeEntry.stackOrder).")]
    public int stackOrder;
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
    [Tooltip("Bu rakibin karşılık hasarı. 0 = level'daki enemyAttackBaseDamage.")]
    [Min(0)] public int attackDamageBase = 0;

    [Header("Engel Baskısı")]
    [Tooltip("Saldırı başına fırlatılan engel. 0 = bu dalgada kapalı, -1 = level'daki bossAttackOilCount. Türler level havuzundan gelir.")]
    [Min(-1)] public int oilCount = -1;
    [Tooltip("Kaç karşı saldırıda bir engel. -1/0 = level'daki bossAttackEveryMoves.")]
    [Min(-1)] public int oilEveryMoves = -1;

    [Header("Görsel Varyant")]
    [Tooltip("Bu rakibin tüm pozlarını belirleyen profil. Boşsa BossDuelController'daki rakip sırası kullanılır.")]
    public BossDuelCharacterProfile characterProfile;
    [Tooltip("Gövdeye uygulanacak tint (beyaz = değişiklik yok). Aynı profilden varyant üretir.")]
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

    [Header("Boss Duel — Obstacle Pressure")]
    [Tooltip("Kaç karşı saldırıda bir engel fırlatılır.")]
    [Min(1)] public int bossAttackEveryMoves = 3;
    [Tooltip("Her baskı saldırısında fırlatılan engel sayısı. 0 = kapalı. Havuz boşsa eski Oil davranışı.")]
    [Min(0)] public int bossAttackOilCount = 2;
    [Tooltip("Yalnız önceki levellarda tanıtılan taşınabilir engeller/overlay'ler. Sandık, üretici, kargo ve ödül objeleri atılmaz. Boş = Oil.")]
    public ObstacleId[] bossThrownObstacles = System.Array.Empty<ObstacleId>();
    [Tooltip("Havuzdaki engellerin board üzerindeki toplam üst sınırı; başlangıç engelleri de sayılır. Yoğun board'da baskı bekler.")]
    [Min(1)] public int bossMaxPressureObstacles = 8;

    [Header("Boss Duel — Alet Tehdidi (geri sayımlı engel)")]
    [Tooltip("Açık: düşman belirli aralıkla board'a geri sayımlı ToolThreat engeli koyar. Başlangıçta editörle " +
             "konan ToolThreat'ler bu kapalıyken de sayar.")]
    public bool bossToolThreatEnabled = false;
    [Tooltip("Düşmanın koyacağı tehdit türleri (her seferinde aralarından biri). Boş = gri ToolThreat " +
             "(yalnız special kırar). Renkliler kendi rengindeki eşleşme veya special ile kırılır.")]
    public ObstacleId[] bossToolThreatTypes = System.Array.Empty<ObstacleId>();
    [Tooltip("Kaç düşman karşı saldırısında bir yeni tehdit konur (board'da aktif tehdit yoksa).")]
    [Min(1)] public int bossToolThreatEveryCounters = 4;
    [Tooltip("Geri sayım: tehdit kaç oyuncu turu sonra fırlatılır.")]
    [Min(1)] public int bossToolThreatCountdown = 3;
    [Tooltip("Ekran çatlakken (tahta kilitli) düşmanın yaptığı ek saldırı sayısı.")]
    [Min(0)] public int bossToolThreatExtraAttacks = 2;
    [Tooltip("Çatlak/tamir süresince tahta kilidi (sn).")]
    [Min(0.5f)] public float bossToolThreatLockSeconds = 3f;

    [Header("Battlefield (BossDuel)")]
    [Tooltip("Oyuncu (sol robot) başlangıç/maks canı. 0 olunca level kaybedilir. Düşman HP'si BossDamage goal amount'tan gelir.")]
    [Min(1)] public int playerMaxHp = 560;
    [Tooltip("Taş başına biriken güç. Animal Duel: hamlede kırılan toplam taş × bu değer, tek darbede uygulanır. Örn. 1 güç × 20 taş = 20 hasar.")]
    [Min(0)] public int damagePerClearedTile = 10;
    [Tooltip("Her kalkanın oyuncu veya düşmana eklediği koruma puanı. Kullanılmayan koruma saklanır.")]
    [Min(1)] public int shieldProtectionPerPickup = 2;
    [Tooltip("Rakibin hamle sonu karşılık hasarı (Opponent'ta 0 bırakılırsa bu kullanılır).")]
    [Min(0)] public int enemyAttackBaseDamage = 20;
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

    [Header("Oil")]
    [Tooltip("Oil yayılmasının tavanı: board'daki toplam oil bu sayıya ulaşınca yayılma durur (boss'un attığı oil de sayılır). " +
             "0 = otomatik: oynanabilir hücrelerin dörtte biri, ama level'ın başlangıç oil sayısından az değil.")]
    [Min(0)] public int oilSpreadMaxCells = 0;

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

    [Tooltip("Ancient Mühür: sıradaki renkten belirtilen sayıda taş toplanınca açılır. Kenar/special vuruşu sayılmaz.")]
    public AncientSealEntry[] ancientSeals;

    [Tooltip("Üst üste bindirilmiş obstacle'lar (generic stacking). Her entry, kapladığı hücrelerdeki " +
             "AUTHORED içeriği (Mud, Stone vb.) 'beneath' olarak saklayıp üstüne obstacleId'yi stamp eder; " +
             "üstteki obstacle kırılınca alttaki geri açılır. Safe ile aynı beneath mekanizmasının her " +
             "obstacle için çalışan generic hâli. Runtime'da GridSpawner + ObstacleStateService işler.")]
    public StackedObstacleEntry[] stackedObstacles;

    [Tooltip("Duvar (Wall) parça numarası, yalnız EDİTÖR içindir (numaralı fırça). Runtime parçayı " +
             "obstacleOrigins'ten tanır: aynı numaralı bitişik duvar hücreleri tek origin paylaşır. 0 = duvar yok.")]
    public int[] wallPieceIds;

    [Tooltip("Aç Hamster doyma eşiği (lokma). 0 = ObstacleLibrary'deki hamsterSatiety varsayılanı.")]
    [Min(0)] public int hamsterSatietyOverride = 0;

    [Tooltip("Boya kutusu kasalarının (PaintCanBox) kutu rengi, kasa origin'ine göre. Listede olmayan kasa " +
             "Auto'dur: rengi satır bandından gelir (PaintCanBoxView.AutoColorForRow).")]
    public PaintCanBoxColorEntry[] paintCanBoxColors;

    public PaintCanColor GetPaintCanBoxColor(int originCellIndex)
    {
        if (paintCanBoxColors != null)
            foreach (var e in paintCanBoxColors)
                if (e.originCellIndex == originCellIndex) return e.color;
        return PaintCanColor.Auto;
    }

    public void SetPaintCanBoxColor(int originCellIndex, PaintCanColor color)
    {
        var list = new System.Collections.Generic.List<PaintCanBoxColorEntry>(
            paintCanBoxColors ?? System.Array.Empty<PaintCanBoxColorEntry>());
        list.RemoveAll(e => e.originCellIndex == originCellIndex);
        if (color != PaintCanColor.Auto)
            list.Add(new PaintCanBoxColorEntry { originCellIndex = originCellIndex, color = color });
        paintCanBoxColors = list.ToArray();
    }

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

        if (wallPieceIds == null || wallPieceIds.Length != size)
            wallPieceIds = new int[size];

        if (tubes == null)
            tubes = System.Array.Empty<TubeEntry>();

        if (magnets == null)
            magnets = System.Array.Empty<MagnetEntry>();

        if (safes == null)
            safes = System.Array.Empty<SafeEntry>();
        if (ancientSeals == null)
            ancientSeals = System.Array.Empty<AncientSealEntry>();

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
