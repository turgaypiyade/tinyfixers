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
    BossDamage = 2,

    // Guguk kuşu evinden (ObstacleId.BirdHouse) uçan kuşlar. Goal amount = toplam kuş.
    Bird = 3,
    // Dimerdal produces colored board diamonds; counted only when collected.
    Diamond = 4
}

public static class CollectibleSources
{
    /// Collectible'ı tahtada üreten engel: hedef ikonu (ObstacleDef.goalIconSprite) ve PatchBot hedefi buradan
    /// çözülür. None = kendi ikon/hedef kuralı olan collectible (EnergyOrb, BossDamage).
    public static ObstacleId ProducerOf(CollectibleId id) => id switch
    {
        CollectibleId.Bird => ObstacleId.BirdHouse,
        CollectibleId.Diamond => ObstacleId.DimerdalDiamondBlue,
        _ => ObstacleId.None,
    };
}

[System.Serializable]
public struct DimerdalEntry
{
    public int originCellIndex;
    public ObstacleId obstacleId;
    [Min(1)] public int count;
}

[System.Serializable]
public struct FruitObstacleEntry
{
    public int originCellIndex;
    [Min(1)] public int width;
    [Min(1)] public int height;
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

    // Lazer topu (2x2, sabit, hücre kapatan; yalnız special ile vurulur). Kule serbestçe döner; hamle sonunda
    // bir special'a kilitlenip lazerle işaretler, oyuncu bir hamle daha kullanmazsa kafese alır. Kafesli special
    // başka bir special'ın etki alanına girerse serbest kalır (patlamaz). 4 vuruşta (4 cıvata) parçalanır.
    // Mantık + görsel: Grid/LaserCannon/LaserCannonService + LaserCannonView.
    LaserCannon = 65,

    // Bilardo topu (tek hücre, hareketli, KIRILMAZ, swap'lanamaz). Kargo gibi düşer; ayrıca boşalan komşu hücreye
    // yuvarlanır (aşağı → çapraz aşağı → yana, yukarı asla) ve boşluğu taşlardan önce alır. Cebe girince hedef +1.
    // Yalnız level'da konur (üstten yeni top gelmez). Kurallar: Docs/BilliardBall_Plan.md.
    // Mantık: Grid/Board/CascadeLogic.Billiard.cs + BilliardBallService; görsel: BilliardBallRoll.
    BilliardBall = 66,

    // Bilardo cebi (tek hücre, sabit, kırılmaz). Taşa kapalı engel: taşlar yanından çapraz akar; yalnız top girer.
    // Sınırsız top alır.
    BilliardPocket = 67,

    // Brandalı heykel (2x2, sabit, hücre kapatan). Önce branda: etiketteki renkten N taş tahtanın HERHANGİ bir
    // yerinde temizlenince branda soyulur (o sırada vuruş almaz). Sonra 3 taş aşaması (YALNIZ special — normal
    // eşleşme vurmaz, SculptingSpecial kuralı), her vuruşta parça düşer; son vuruşta bitmiş heykel hedefe uçar, çerçeve parçalanır.
    // Renk/sayı engel başına: LevelData.tarpStatues. Mantık + görsel: Grid/TarpStatue/. Docs/TarpStatue_Plan.md.
    TarpStatue = 68,

    // Grass-backed water lily. Eats adjacent specials; special hits return the stored special,
    // then remove four petals permanently. Empty flowers accept normal/special hits; a stored
    // special can only be released by another special. The goal completes after grass clears.
    GrassWaterLily = 69,

    // Fixed 2x2 colossus: four damage stages, special activation only. Shared origin/goal.
    RhodesStatue = 70,

    // Renkli sandık: sabit 1x1, 3 aşama (sağlam → çatlak → ahşabı görünen). YALNIZ kendi rengindeki komşu
    // eşleşme + special kırar (ObstacleDef.restrictNormalMatchTileType). Renk sırası ToolThreat ile aynı:
    // Sarı=Gear, Kırmızı=Core, Mavi=Bolt, Yeşil=Plate.
    ColorCrateYellow = 71,
    ColorCrateRed = 72,
    ColorCrateBlue = 73,
    ColorCrateGreen = 74,

    // Fener: PlasticTwoStage (40) ile birebir aynı davranış (hareketli, 2 vuruş, hedefse tepeden yenisi doğar);
    // yalnız görsel farklı — 1. aşama sönük, ilk vuruşta yanar, ikinci vuruşta hedefe uçar. Kod gerekmez (veri).
    Lantern = 75,

    // Su kanalı: depodan (herhangi bir hücre) tahtanın sol/sağ/alt kenarına serbest çizilen, hiç yukarı çıkmayan yol
    // (LevelData.waterChannels).
    // Kapalı hücre taşın altında durur; üstündeki taş eşleşince açılır, açık hücreden taş geçer ama durmaz,
    // su depoya kesintisiz bağlı açık hücrelere akar; tamamı açılınca su son hücrenin kenarından çıkar → hedef +1.
    // Ayrıntı: Docs/WaterChannel_Plan.md.
    WaterChannel = 76,
    // Altın (HEDEF): GoldMoney görselli, plastik gibi hareketli tek vuruşluk engel; toplanan her parça cüzdana
    // 1 altın. Seçili sütunlarda refill her boş hücreyi altınla doldurur (spawnFillsColumn) → altın sütunu akışı.
    // Bonus GoldMoney'den (32, hedef değil) ayrı. Ödül leveli için (V1 71, 2026-10-29).
    GoldCoin = 77,

    // Guguk kuşu evi (1x1, KIRILMAZ, BirdHouseService). 1. vuruş kapağı açar, kuş çıkar; kuş görünürken her vuruş
    // kuşu hedefe uçurur (Collectible Bird +1) ve yuvadan yenisi çıkar, kapak açık kalır. Bir hamle boyunca vuruş
    // gelmezse kuş içeri girer, kapak kapanır. Evler ortak kota (Bird hedefi) dolana kadar
    // sınırsız kuş verir; kota dolunca hepsi kapanır. Üstüne kasa/taş/AncientSeal konabilir; zemin örtüsü gelmez.
    BirdHouse = 78,

    // Lamba şeridi (LampStripService): duvar gibi serbest şekilli (düz/L/zikzak, DALLANMASIZ) tek parça çerçeve,
    // her hücresinde renkli lamba. Renkli lamba yanındaki AYNI RENK eşleşmeyle ya da special'la kırılır; mor lamba
    // yalnız special'la. Kırılan lambanın yeri boş kalır. Parça ayarı açıksa her oyuncu hamlesi bitince bütün dizi
    // (boşluklar dahil) yol boyunca 1 hücre kayar, uçtan çıkan diğer uçtan girer. Tüm lambalar kırılınca çerçeve
    // 2-3 büyük parçaya ayrılıp düşer; hedef = çerçeve sayısı. Üstüne yağ/çim gelebilir; altında çamur/jel olabilir.
    LampStrip = 79,

    // Yel değirmeni (WindmillService) 2×2: dört renkli kanat beklerken yavaşça döner; kanatlar YALNIZ special/joker ile
    // kırılır (normal eşleşme vurmaz, sıra önemsiz — kullanıcı 2026-11-01). Dört kanat bitince değirmen yıkılır ve
    // rastgele, normal taş olan bir 1×2 alana un çuvalı fırlatır. Hedef DEĞİL (hedef: Flour). Üstüne bir şey konmaz;
    // ileride altına zemin (çamur vb.) gelebilir.
    Windmill = 80,
    // Un çuvalı: değirmenin fırlattığı 2×1 (yatay) / 1×2 (dikey) örtü; altındaki taşları tutar, tek vuruşta patlar ve
    // 5×5 alana (alt katmanı boş hücrelere) un saçar. Hedef değil.
    FlourSack = 81,
    FlourSackV = 82,
    // Un: taşın altında en alt katman (su birikintisi / çamur gibi); üstündeki eşleşmeyle temizlenir. Hedef = hücre sayısı.
    Flour = 83,

    // Dimerdal: four-hit colored shutters with an individual production count.
    DimerdalBlue1x1 = 84, DimerdalBlue2x2 = 85, DimerdalBlue3x3 = 86,
    DimerdalGreen1x1 = 87, DimerdalGreen2x2 = 88, DimerdalGreen3x3 = 89,
    DimerdalYellow1x1 = 90, DimerdalYellow2x2 = 91, DimerdalYellow3x3 = 92,
    DimerdalRed1x1 = 93, DimerdalRed2x2 = 94, DimerdalRed3x3 = 95,
    // Legacy diamond authoring/icon IDs; promoted to normal Diamond tiles at spawn/reveal.
    DimerdalDiamondBlue = 96, DimerdalDiamondGreen = 97,
    DimerdalDiamondYellow = 98, DimerdalDiamondRed = 99,
    FlyEggBox = 100, // Four special-only releases; one movable egg per special.
    FlyEgg = 101,    // Movable1x1; a normal match/special hatches three fruit-eating flies.
    FlyFruit = 102,  // Per-origin width/height; six fly bites, one fruit goal.
    HotAirBalloon = 103, // 2x2; 3 special/joker hits inflate it, then it flies up clearing its two columns.
}

/// Lamba şeridi hücre rengi (LevelData.lampColorByCell). Renkler taş türleriyle eşleşir (renkli sandık sırası).
public static class LampStripColor
{
    public const int None = 0;      // boş yuva (kırılmış / lambasız)
    public const int Yellow = 1;    // Gear
    public const int Red = 2;       // Core
    public const int Blue = 3;      // Bolt
    public const int Green = 4;     // Plate
    public const int Purple = 5;    // yalnız special
    public const int Count = 6;

    public static bool TryGetTileType(int color, out TileType type)
    {
        switch (color)
        {
            case Yellow: type = TileType.Gear; return true;
            case Red: type = TileType.Core; return true;
            case Blue: type = TileType.Bolt; return true;
            case Green: type = TileType.Plate; return true;
            default: type = TileType.Gear; return false;
        }
    }
}

[System.Serializable]
public struct LampStripSettings
{
    [Tooltip("Editördeki duvar/şerit parça numarası (wallPieceIds). Runtime parçayı origin hücresinin numarasıyla bulur.")]
    public int pieceId;
    [Tooltip("Açık: her oyuncu hamlesi bitince lambalar yol boyunca 1 hücre kayar (döngü). Kapalı: tanıtım, sabit.")]
    public bool shift;
    [Tooltip("Kayma yönü. Kapalı = ileri: yolun sol-üst ucundan diğer ucuna (yatayda sağa, dikeyde aşağı). Açık = geri.")]
    public bool reverse;
}

public enum TubeDirection { Up, Down, Left, Right }

[System.Serializable]
public struct TutorialStep
{
    [Tooltip("Gösterilecek metin (öğretici türü).")]
    public TutorialId id;
    [Tooltip("Değiştirilecek ilk hücre (y*width+x). -1 = tahtadaki special'ı kendisi bulur (oluşan roketi patlatma adımı).")]
    public int fromCell;
    [Tooltip("Değiştirilecek ikinci hücre (y*width+x). fromCell -1 ise yok sayılır (komşu kendisi seçilir).")]
    public int toCell;
}

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
public struct WaterChannelEntry
{
    [Tooltip("Sıralı yol (flat index y*width+x). İLK hücre = DEPO (herhangi bir oynanır hücre; oyunda taşın geçtiği " +
             "geçirgen hücre, üstünde çeşme). Sonrakiler kanal: dik komşu, hiçbir adım YUKARI çıkmaz (aşağı ya da yana). " +
             "SON hücre tahtanın sol/sağ/alt kenarına ya da bir boşluğa değer; su o kenardan çıkar.")]
    public int[] pathCellIndices;

    /// Yol kurallara uyuyor mu? Hata varsa okunur açıklama döner (editör uyarısı + runtime koruması).
    public static bool Validate(LevelData level, int[] path, out string error)
    {
        error = null;
        if (level == null || level.cells == null) { error = "Level verisi yok."; return false; }
        if (path == null || path.Length < 2) { error = "Depo + en az 1 kanal hücresi gerekli."; return false; }
        int w = level.width, h = level.height;
        var seen = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < path.Length; i++)
        {
            int c = path[i];
            if (c < 0 || c >= w * h || level.cells[c] != (int)CellType.Normal) { error = $"{i + 1}. hücre oynanır değil."; return false; }
            if (!seen.Add(c)) { error = $"{i + 1}. hücre yolda iki kez geçiyor."; return false; }
            if (i > 0)
            {
                int p = path[i - 1], dx = c % w - p % w, dy = c / w - p / w;
                if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) { error = $"{i}. ve {i + 1}. hücre dik komşu değil."; return false; }
                if (dy < 0) { error = $"{i + 1}. hücre yukarı çıkıyor (su yalnız aşağı ya da yana akar)."; return false; }
            }
        }
        if (ExitSide(level, path) == '\0') { error = "Son hücre tahtanın sol, sağ ya da alt kenarına (veya bir boşluğa) değmeli."; return false; }
        return true;
    }

    /// Suyun çıktığı kenar: son hücrenin dışarıya (tahta dışı ya da boş hücre) açılan kenarı. Öncelik: son adımın
    /// yönünde devam eden kenar, sonra alt, sonra sol/sağ. Yukarı çıkış yok. Uygun kenar yoksa '\0'.
    public static char ExitSide(LevelData level, int[] path)
    {
        if (level == null || path == null || path.Length < 2) return '\0';
        int w = level.width, last = path[path.Length - 1], prev = path[path.Length - 2];
        int x = last % w, y = last / w;
        char forward = (last % w) > (prev % w) ? 'R' : (last % w) < (prev % w) ? 'L' : 'B';
        bool Out(char s)
        {
            int nx = x + (s == 'R' ? 1 : s == 'L' ? -1 : 0), ny = y + (s == 'B' ? 1 : 0);
            if (nx < 0 || nx >= w || ny >= level.height) return true;
            return level.cells[ny * w + nx] != (int)CellType.Normal;
        }
        if (Out(forward)) return forward;
        if (Out('B')) return 'B';
        if (Out('L') && forward != 'R') return 'L';
        if (Out('R') && forward != 'L') return 'R';
        return '\0';
    }
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

public enum TarpStatueColor
{
    Red = 0,      // TileType.Core
    Yellow = 1,   // TileType.Gear
    Blue = 2,     // TileType.Bolt
    Green = 3,    // TileType.Plate
}

[System.Serializable]
public struct TarpStatueEntry
{
    [Tooltip("Heykelin SOL-ÜST (origin) hücresinin flat index'i (y*width+x).")]
    public int originCellIndex;
    [Tooltip("Etiket rengi: brandanın kalkması için toplanacak taş rengi.")]
    public TarpStatueColor color;
    [Tooltip("Toplanacak taş sayısı (etikette geri sayar).")]
    [Min(1)] public int count;
    [Tooltip("Kare boyut: 2 = 2x2, 3 = 3x3, 4 = 4x4 (0 = 2x2).")]
    public int size;

    public int Size => size >= TarpStatueMinSize ? Mathf.Min(size, TarpStatueMaxSize) : TarpStatueMinSize;
    public const int TarpStatueMinSize = 2, TarpStatueMaxSize = 4;
}

[System.Serializable]
public struct PaintCanBoxColorEntry
{
    [Tooltip("Kasanın SOL-ÜST (origin) hücresinin flat index'i (y*width+x).")]
    public int originCellIndex;
    public PaintCanColor color;
}

[System.Serializable]
public struct ColorCrateInitialHitsEntry
{
    public int originCellIndex;
    public ObstacleId obstacleId;
    [Min(1)] public int remainingHits;
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

    [Tooltip("Renkli 1x1 sandıkların başlangıçta kalan vuruşu. 0 = kütüphane varsayılanı; " +
             "2/1 = hasarlı aşamadan başlar. Diğer engellerde kullanılmaz.")]
    [Min(0)] public int initialRemainingHits;

    [Tooltip("Yığın Wall/MetalWall hücrelerinin ortak parça origin'i + 1. 0 = eski tek-hücre davranışı. " +
             "Aynı bağlı parçanın bütün hücrelerinde aynı değer kullanılır; yerleştirme hücresi originCellIndex olarak kalır.")]
    [Min(0)] public int wallPieceOriginPlusOne;

    [Tooltip("Overlay numaralı duvar fırçasının parça numarası; yalnız editör kullanır. 0 = eski kayıt.")]
    [Min(0)] public int wallPieceId;

    public int ResolveRuntimeOrigin(int cellCount)
    {
        int sharedOrigin = wallPieceOriginPlusOne - 1;
        return (obstacleId == ObstacleId.Wall || obstacleId == ObstacleId.MetalWall)
            && sharedOrigin >= 0 && sharedOrigin <= originCellIndex && sharedOrigin < cellCount
            ? sharedOrigin : originCellIndex;
    }

    public static bool SupportsInitialHits(ObstacleId id)
        => id == ObstacleId.ColorCrateYellow || id == ObstacleId.ColorCrateRed
        || id == ObstacleId.ColorCrateBlue || id == ObstacleId.ColorCrateGreen;

    public int ResolveInitialRemainingHits(ObstacleDef def)
    {
        int maxHits = Mathf.Max(1, def != null ? def.hits : 1);
        return SupportsInitialHits(obstacleId) && initialRemainingHits > 0
            ? Mathf.Clamp(initialRemainingHits, 1, maxHits) : maxHits;
    }
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

    [Tooltip("Refill special koruması: yeni düşen taşın, durduğu yerde 4+ dizi / T-L / 2x2 (Rocket, " +
             "PulseCore, PatchBot) tamamlama olasılığından engellenen oran. 0 = kapalı, 1 = hepsi engellenir. " +
             "-1 = otomatik (seviye numarasına göre, RefillSpecialGuard). Normal 3'lü cascade serbest kalır.")]
    [Range(-1f, 1f)] public float refillSpecialGuard = RefillSpecialGuard.Auto;

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

    [Tooltip("Önceden tanımlı öğretici adımları (sırayla). Tahta pinli taşlarla bu hamlelere göre hazırlanır; " +
             "oyuncu yalnız gösterilen değişimi yapabilir. Şansa bağlı değildir (ScriptedTutorial).")]
    public TutorialStep[] tutorialSteps;

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

    [Tooltip("Normal Obstacle fırçasıyla konan renkli sandıkların başlangıç kalan vuruşu. " +
             "Listede olmayan sandık kütüphane varsayılanından başlar; katman eklemez.")]
    public ColorCrateInitialHitsEntry[] colorCrateInitialHits;

    public int GetColorCrateInitialHits(int origin, ObstacleId id)
    {
        if (colorCrateInitialHits != null)
            foreach (var entry in colorCrateInitialHits)
                if (entry.originCellIndex == origin && entry.obstacleId == id)
                    return entry.remainingHits;
        return 0;
    }

    // Authoring only; runtime builds its lookup once during initialization.
    public void SetColorCrateInitialHits(int origin, ObstacleId id, int remaining)
    {
        ClearColorCrateInitialHitsAt(origin);
        if (!StackedObstacleEntry.SupportsInitialHits(id) || remaining <= 0) return;
        var def = obstacleLibrary != null ? obstacleLibrary.Get(id) : null;
        int maxHits = Mathf.Max(1, def != null ? def.hits : 3);
        remaining = Mathf.Clamp(remaining, 1, maxHits);
        if (remaining == maxHits) return;  // Full-health/default needs no extra record.
        var list = new System.Collections.Generic.List<ColorCrateInitialHitsEntry>(
            colorCrateInitialHits ?? System.Array.Empty<ColorCrateInitialHitsEntry>());
        list.Add(new ColorCrateInitialHitsEntry { originCellIndex = origin, obstacleId = id, remainingHits = remaining });
        colorCrateInitialHits = list.ToArray();
    }

    public void ClearColorCrateInitialHitsAt(int origin)
    {
        if (colorCrateInitialHits == null) return;
        for (int i = 0; i < colorCrateInitialHits.Length; i++)
            if (colorCrateInitialHits[i].originCellIndex == origin)
            {
                var list = new System.Collections.Generic.List<ColorCrateInitialHitsEntry>(colorCrateInitialHits);
                list.RemoveAll(entry => entry.originCellIndex == origin);
                colorCrateInitialHits = list.ToArray();
                return;
            }
    }

    [Tooltip("Shrinking tube obstacles. Cells are stamped into obstacles[] at runtime by GridSpawner.")]
    public TubeEntry[] tubes;

    [Tooltip("Magnet pair obstacles. Cells are stamped into obstacles[] at runtime by GridSpawner.")]
    public MagnetEntry[] magnets;

    [Tooltip("Su kanalları (ilk hücre depo → son hücre kenardan çıkış). Seviye editörünün 'Kanal' modunda çizilir.")]
    public WaterChannelEntry[] waterChannels;

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

    [Tooltip("Lamba şeridi: hücre başına lamba rengi (LampStripColor). 0 = boş yuva. Editörde şerit boyanırken yazılır.")]
    public int[] lampColorByCell;
    [Tooltip("Lamba şeridi parça ayarları (kayma açık/kapalı + yön), parça numarasına göre. Listede olmayan parça kaymaz.")]
    public LampStripSettings[] lampStripSettings;

    [Tooltip("Aç Hamster doyma eşiği (lokma), level varsayılanı. 0 = ObstacleLibrary'deki hamsterSatiety. " +
             "Hamster başına değer (hamsterSatietyByCell) varsa o geçerlidir.")]
    [Min(0)] public int hamsterSatietyOverride = 0;
    [Tooltip("Hamster başına lokma (doyma) sayısı — hamsterin KONDUĞU hücrenin index'inde. 0 = level/kütüphane varsayılanı. " +
             "Editörde hamster yerleştirirken yazılır.")]
    public int[] hamsterSatietyByCell;

    [Tooltip("Boya kutusu kasalarının (PaintCanBox) kutu rengi, kasa origin'ine göre. Listede olmayan kasa " +
             "Auto'dur: rengi satır bandından gelir (PaintCanBoxView.AutoColorForRow).")]
    public PaintCanBoxColorEntry[] paintCanBoxColors;

    [Tooltip("Her Dimerdal kutusunun üreteceği elmas adedi. Ortak Diamond hedefi bu adetlerin toplamıdır.")]
    public DimerdalEntry[] dimerdals;
    public const int DimerdalDefaultCount = 6;

    public int GetDimerdalProductionCount(int origin, ObstacleId id)
    {
        if (dimerdals != null)
            foreach (var e in dimerdals)
                if (e.originCellIndex == origin && e.obstacleId == id) return Mathf.Max(1, e.count);
        return DimerdalDefaultCount;
    }

    public void SetDimerdalProductionCount(int origin, ObstacleId id, int count)
    {
        if (!DimerdalRules.IsShutter(id)) return;
        var list = new System.Collections.Generic.List<DimerdalEntry>(dimerdals ?? System.Array.Empty<DimerdalEntry>());
        list.RemoveAll(e => e.originCellIndex == origin && e.obstacleId == id);
        list.Add(new DimerdalEntry { originCellIndex = origin, obstacleId = id, count = Mathf.Max(1, count) });
        dimerdals = list.ToArray();
    }

    /// Authoring/initialization only. Include covered sources once, by origin + type.
    public void GetDimerdalBoxes(System.Collections.Generic.List<DimerdalEntry> boxes)
    {
        boxes.Clear();
        if (obstacles != null && obstacleOrigins != null)
            for (int i = 0; i < obstacles.Length && i < obstacleOrigins.Length; i++)
                if (obstacleOrigins[i] == i) AddDimerdalBox(boxes, i, (ObstacleId)obstacles[i]);
        if (stackedObstacles != null)
            foreach (var e in stackedObstacles) AddDimerdalBox(boxes, e.originCellIndex, e.obstacleId);
        boxes.Sort((a, b) => a.originCellIndex != b.originCellIndex
            ? a.originCellIndex.CompareTo(b.originCellIndex) : a.obstacleId.CompareTo(b.obstacleId));
    }

    private void AddDimerdalBox(System.Collections.Generic.List<DimerdalEntry> boxes, int origin, ObstacleId id)
    {
        if (!DimerdalRules.IsShutter(id) || origin < 0 || origin >= width * height
            || cells == null || origin >= cells.Length || cells[origin] != (int)CellType.Normal) return;
        foreach (var e in boxes) if (e.originCellIndex == origin && e.obstacleId == id) return;
        boxes.Add(new DimerdalEntry { originCellIndex = origin, obstacleId = id, count = GetDimerdalProductionCount(origin, id) });
    }

    private int CountAuthoredDimerdalDiamonds()
    {
        var seen = new System.Collections.Generic.HashSet<(int, ObstacleId)>();
        if (obstacles != null && obstacleOrigins != null)
            for (int i = 0; i < obstacles.Length && i < obstacleOrigins.Length; i++)
                if (obstacleOrigins[i] == i && DimerdalRules.IsDiamond((ObstacleId)obstacles[i]))
                    seen.Add((i, (ObstacleId)obstacles[i]));
        if (stackedObstacles != null)
            foreach (var e in stackedObstacles)
                if (DimerdalRules.IsDiamond(e.obstacleId)) seen.Add((e.originCellIndex, e.obstacleId));
        return seen.Count;
    }

    /// Editor and runtime-clone initialization, BEFORE layer stamping. Never call on
    /// the live board: its obstacle arrays and source registrations change during play.
    public bool SynchronizeDimerdalGoals()
    {
        var boxes = new System.Collections.Generic.List<DimerdalEntry>();
        GetDimerdalBoxes(boxes);
        int authored = CountAuthoredDimerdalDiamonds();
        int oldAmount = 0;
        if (goals != null)
            foreach (var g in goals)
                if (IsDimerdalGoal(g)) oldAmount += Mathf.Max(0, g.amount);
        // Old levels had only a shared goal. Materialize an even split once, keeping
        // their total. New placements always store the designer's explicit count.
        if ((dimerdals == null || dimerdals.Length == 0) && boxes.Count > 0 && oldAmount > 0)
        {
            int budget = Mathf.Max(boxes.Count, oldAmount - authored);
            for (int i = 0; i < boxes.Count; i++)
            {
                var e = boxes[i];
                e.count = budget / boxes.Count + (i < budget % boxes.Count ? 1 : 0);
                boxes[i] = e;
            }
        }
        bool changed = dimerdals == null ? boxes.Count > 0 : dimerdals.Length != boxes.Count;
        if (!changed && dimerdals != null)
            for (int i = 0; i < boxes.Count; i++)
                if (dimerdals[i].originCellIndex != boxes[i].originCellIndex
                    || dimerdals[i].obstacleId != boxes[i].obstacleId || dimerdals[i].count != boxes[i].count) { changed = true; break; }
        if (changed) dimerdals = boxes.ToArray();
        int total = authored;
        foreach (var e in boxes) total += e.count;
        var next = new System.Collections.Generic.List<LevelGoalDefinition>();
        LevelGoalDefinition diamondGoal = null;
        if (goals != null)
            foreach (var g in goals)
            {
                if (!IsDimerdalGoal(g)) { next.Add(g); continue; }
                if (diamondGoal != null || total <= 0) { changed = true; continue; }
                diamondGoal = g;
                next.Add(g);
            }
        if (total > 0 && diamondGoal == null)
        {
            diamondGoal = new LevelGoalDefinition { targetType = LevelGoalTargetType.Collectible, collectibleId = CollectibleId.Diamond };
            next.Add(diamondGoal);
            changed = true;
        }
        if (diamondGoal != null)
        {
            var icon = obstacleLibrary != null ? obstacleLibrary.Get(ObstacleId.DimerdalDiamondBlue)?.GetGoalIconSprite() : null;
            if (diamondGoal.amount != total) { diamondGoal.amount = total; changed = true; }
            if (icon != null && diamondGoal.iconOverride != icon) { diamondGoal.iconOverride = icon; changed = true; }
        }
        if (goals == null || next.Count != goals.Length) goals = next.ToArray();
        return changed;
    }

    private static bool IsDimerdalGoal(LevelGoalDefinition goal) => goal != null
        && goal.targetType == LevelGoalTargetType.Collectible && goal.collectibleId == CollectibleId.Diamond;

    [Tooltip("Brandalı heykellerin etiket rengi + toplama sayısı, heykel origin'ine göre. Listede olmayan heykel " +
             "varsayılanı kullanır (kırmızı, TarpStatueDefaultCount).")]
    public TarpStatueEntry[] tarpStatues;

    public const int TarpStatueDefaultCount = 30;

    public TarpStatueEntry GetTarpStatue(int originCellIndex)
    {
        if (tarpStatues != null)
            foreach (var e in tarpStatues)
                if (e.originCellIndex == originCellIndex) return e;
        return new TarpStatueEntry { originCellIndex = originCellIndex, color = TarpStatueColor.Red, count = TarpStatueDefaultCount };
    }

    public void SetTarpStatue(int originCellIndex, TarpStatueColor color, int count, int size)
    {
        var list = new System.Collections.Generic.List<TarpStatueEntry>(
            tarpStatues ?? System.Array.Empty<TarpStatueEntry>());
        list.RemoveAll(e => e.originCellIndex == originCellIndex);
        list.Add(new TarpStatueEntry { originCellIndex = originCellIndex, color = color, count = Mathf.Max(1, count), size = size });
        tarpStatues = list.ToArray();
    }

    /// Engelin kapladığı alan. Brandalı heykel tek engel, boyutu heykel başına (2x2/3x3/4x4); diğerleri def.size.
    /// Çok-hücreli engelin ayak izini okuyan her yer bunu kullanmalı (doğrudan def.size değil).
    public Vector2Int GetObstacleFootprint(ObstacleId id, int originCellIndex, ObstacleDef def)
    {
        if (id == ObstacleId.FlyFruit) return GetFruitDimensions(originCellIndex);
        if (id == ObstacleId.TarpStatue)
        {
            int n = GetTarpStatue(originCellIndex).Size;
            return new Vector2Int(n, n);
        }
        return def != null ? new Vector2Int(Mathf.Max(1, def.size.x), Mathf.Max(1, def.size.y)) : Vector2Int.one;
    }

    public FruitObstacleEntry[] fruits;

    public Vector2Int GetFruitDimensions(int origin)
    {
        if (fruits != null)
            foreach (var f in fruits)
                if (f.originCellIndex == origin) return new Vector2Int(Mathf.Clamp(f.width, 1, Mathf.Max(1, width)), Mathf.Clamp(f.height, 1, Mathf.Max(1, height)));
        return Vector2Int.one;
    }

    public void SetFruitDimensions(int origin, int fruitWidth, int fruitHeight)
    {
        var list = new System.Collections.Generic.List<FruitObstacleEntry>(fruits ?? System.Array.Empty<FruitObstacleEntry>());
        list.RemoveAll(f => f.originCellIndex == origin);
        list.Add(new FruitObstacleEntry { originCellIndex = origin, width = Mathf.Clamp(fruitWidth, 1, Mathf.Max(1, width)), height = Mathf.Clamp(fruitHeight, 1, Mathf.Max(1, height)) });
        fruits = list.ToArray();
    }

    public void GetFruitOrigins(System.Collections.Generic.List<int> result)
    {
        result.Clear();
        if (obstacles != null && obstacleOrigins != null)
            for (int i = 0; i < obstacles.Length && i < obstacleOrigins.Length; i++)
                if (obstacles[i] == (int)ObstacleId.FlyFruit && obstacleOrigins[i] == i
                    && cells != null && i < cells.Length && cells[i] == (int)CellType.Normal) result.Add(i);
        if (stackedObstacles != null)
            foreach (var e in stackedObstacles)
                if (e.obstacleId == ObstacleId.FlyFruit && e.originCellIndex >= 0 && e.originCellIndex < width * height
                    && cells != null && e.originCellIndex < cells.Length && cells[e.originCellIndex] == (int)CellType.Normal
                    && !result.Contains(e.originCellIndex)) result.Add(e.originCellIndex);
        result.Sort();
    }

    // Authoring/runtime-clone only, never recount the live mutable board.
    public bool SynchronizeFruitGoals()
    {
        var origins = new System.Collections.Generic.List<int>(); GetFruitOrigins(origins);
        bool changed = false;
        if (fruits != null)
        {
            var entries = new System.Collections.Generic.List<FruitObstacleEntry>(fruits);
            if (entries.RemoveAll(e => !origins.Contains(e.originCellIndex)) > 0) { fruits = entries.ToArray(); changed = true; }
        }
        var next = new System.Collections.Generic.List<LevelGoalDefinition>();
        LevelGoalDefinition fruitGoal = null;
        if (goals != null) foreach (var g in goals)
        {
            if (g == null || g.targetType != LevelGoalTargetType.Obstacle
                || (g.obstacleId != ObstacleId.FlyFruit && g.obstacleId != ObstacleId.FlyEggBox && g.obstacleId != ObstacleId.FlyEgg)) { next.Add(g); continue; }
            if (g.obstacleId == ObstacleId.FlyFruit && fruitGoal == null && origins.Count > 0) { fruitGoal = g; next.Add(g); }
            else changed = true;
        }
        if (origins.Count > 0 && fruitGoal == null)
        {
            fruitGoal = new LevelGoalDefinition { targetType = LevelGoalTargetType.Obstacle, obstacleId = ObstacleId.FlyFruit };
            next.Add(fruitGoal); changed = true;
        }
        if (fruitGoal != null)
        {
            var icon = obstacleLibrary != null ? obstacleLibrary.Get(ObstacleId.FlyFruit)?.GetGoalIconSprite() : null;
            if (fruitGoal.amount != origins.Count) { fruitGoal.amount = origins.Count; changed = true; }
            if (icon != null && fruitGoal.iconOverride != icon) { fruitGoal.iconOverride = icon; changed = true; }
        }
        if (changed) goals = next.ToArray();
        return changed;
    }

    public void RemoveTarpStatue(int originCellIndex)
    {
        if (tarpStatues == null) return;
        var list = new System.Collections.Generic.List<TarpStatueEntry>(tarpStatues);
        if (list.RemoveAll(e => e.originCellIndex == originCellIndex) > 0) tarpStatues = list.ToArray();
    }

    /// Tahtada (taban ya da yığın katmanı olarak) heykeli kalmayan girdileri siler — silgi, üstüne boyama, maske
    /// hangisi heykeli kaldırırsa kaldırsın ayar listesi tahtayla aynı kalır.
    public void PruneTarpStatues()
    {
        if (tarpStatues == null || tarpStatues.Length == 0) return;
        var list = new System.Collections.Generic.List<TarpStatueEntry>(tarpStatues);
        int removed = list.RemoveAll(e => !HasTarpStatueAt(e.originCellIndex));
        if (removed > 0) tarpStatues = list.ToArray();
    }

    private bool HasTarpStatueAt(int origin)
    {
        if (obstacles != null && origin >= 0 && origin < obstacles.Length &&
            obstacles[origin] == (int)ObstacleId.TarpStatue &&
            (obstacleOrigins == null || origin >= obstacleOrigins.Length || obstacleOrigins[origin] == origin))
            return true;
        if (stackedObstacles != null)
            foreach (var s in stackedObstacles)
                if (s.obstacleId == ObstacleId.TarpStatue && s.originCellIndex == origin) return true;
        return false;
    }

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

    [Tooltip("Hareketli engellerin (plastik vb.) hedef için yeniden üretileceği sütunlar. Hiçbiri seçili değilse " +
             "eskisi gibi her sütundan gelebilir. Seçiliyse yalnız bu sütunlardan düşer → ip gibi akış (kargo hariç).")]
    public bool[] movableSpawnColumns;

    [Tooltip("Seçili sütunlardan gerekli engel bu kadar hamle gelemezse başka sütundan da düşebilir (hedef tıkanmasın). " +
             "0 = katı: yalnız seçili sütunlar.")]
    [Min(0)] public int movableSpawnFallbackMoves = 3;

    [Tooltip("AÇIK: seçili sütunlar hep hareketli engelle dolu kalır (geçiş başına 1 sınırı yok) ve her sütun yalnız " +
             "başta kendisine konan engel türünü üretir (turuncu sütun → turuncu). KAPALI: geçiş başına 1, karışık akış.")]
    public bool movableSpawnFillColumns = true;

    /// Tasarımcı en az bir üretim sütunu seçmiş mi?
    public bool HasMovableSpawnColumns
    {
        get
        {
            if (movableSpawnColumns == null) return false;
            for (int i = 0; i < movableSpawnColumns.Length && i < width; i++) if (movableSpawnColumns[i]) return true;
            return false;
        }
    }

    /// x sütunu hareketli engel üretimine açık mı? Seçim yoksa her sütun açıktır.
    public bool IsMovableSpawnColumn(int x)
    {
        if (movableSpawnColumns == null) return true;
        bool any = false;
        for (int i = 0; i < movableSpawnColumns.Length && i < width; i++)
            if (movableSpawnColumns[i]) { any = true; if (i == x) return true; }
        return !any;
    }

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

        if (lampColorByCell == null || lampColorByCell.Length != size)
            lampColorByCell = new int[size];

        if (lampStripSettings == null)
            lampStripSettings = System.Array.Empty<LampStripSettings>();

        if (tubes == null)
            tubes = System.Array.Empty<TubeEntry>();

        if (magnets == null)
            magnets = System.Array.Empty<MagnetEntry>();

        if (waterChannels == null)
            waterChannels = System.Array.Empty<WaterChannelEntry>();

        if (safes == null)
            safes = System.Array.Empty<SafeEntry>();
        if (ancientSeals == null)
            ancientSeals = System.Array.Empty<AncientSealEntry>();

        if (stackedObstacles == null)
            stackedObstacles = System.Array.Empty<StackedObstacleEntry>();

        if (pinnedTileTypes == null || pinnedTileTypes.Length != size)
            pinnedTileTypes = new int[size];

        if (hamsterSatietyByCell == null || hamsterSatietyByCell.Length != size)
            hamsterSatietyByCell = new int[size];

        if (pinnedSpecialTypes == null || pinnedSpecialTypes.Length != size)
            pinnedSpecialTypes = new int[size];

        if (goals == null)
            goals = System.Array.Empty<LevelGoalDefinition>();

        SynchronizeDimerdalGoals();

        SynchronizeFruitGoals();

        for (int i = 0; i < goals.Length; i++)
        {
            if (goals[i] == null)
                goals[i] = new LevelGoalDefinition();

            goals[i].amount = Mathf.Max(1, goals[i].amount);
        }
    }
}
