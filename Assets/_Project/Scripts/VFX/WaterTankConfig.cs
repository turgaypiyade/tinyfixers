using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WaterTank (2x2 su deposu) kırılma ayarları + görselleri (Resources/WaterTank/WaterTankConfig).
/// Sprite alanları boşsa prosedürel su damlası (WaterDropletSprites) kullanılır — asset hiç yoksa
/// da default değerlerle çalışır.
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Obstacles/Water Tank Config", fileName = "WaterTankConfig")]
public class WaterTankConfig : ScriptableObject
{
    public const string ResourcePath = "WaterTank/WaterTankConfig";

    private static WaterTankConfig cached;
    private static bool loaded;

    /// <summary>Resources'tan bir kez yüklenir; asset yoksa null (çağıranlar default'a düşer).</summary>
    public static WaterTankConfig Load()
    {
        if (!loaded)
        {
            cached = Resources.Load<WaterTankConfig>(ResourcePath);
            loaded = true;
        }
        return cached;
    }

    [Header("Gameplay")]
    [Tooltip("Depo kırılınca su fışkıran rastgele hücre sayısı (board'un tamamından seçilir).")]
    [Min(0)] public int puddleCount = 8;

    [Header("Çatlama (ara vuruş)")]
    [Tooltip("Ara vuruşta sırayla oynatılan çatlak kareleri (ör. Bidon → Bidon2 → Bidon3). Sonunda " +
             "ObstacleDef stage sprite'ında kalır. Boşsa yalnız stage sprite'ı değişir.")]
    public List<Sprite> crackFrames = new();
    [Tooltip("Çatlak karesi başına süre (sn).")]
    public float crackFrameDuration = 0.06f;
    [Tooltip("Çatlarken gövdeden sıçrayan parça sayısı. 0 = kapalı.")]
    [Min(0)] public int crackShardCount = 4;
    [Tooltip("Son vuruşta (dağılırken) saçılan parça sayısı. Parçalar crackFrames'in son karesinden kesilir.")]
    [Min(0)] public int breakShardCount = 8;

    [Header("Kırılma patlaması (dağılma)")]
    [Tooltip("Depo dağılırken merkezden her yöne saçılan, sadece görsel damla sayısı. 0 = kapalı.")]
    [Min(0)] public int burstDropletCount = 12;
    [Tooltip("Patlama damlalarının yana saçılma mesafesi (tile, depo boyutuyla ölçeklenir).")]
    public float burstRadiusTiles = 1.3f;
    [Tooltip("Patlama damlalarının yukarı sıçrama yüksekliği (tile).")]
    public float burstHeightTiles = 0.9f;
    [Tooltip("Patlama süresi (sn).")]
    public float burstDuration = 0.42f;

    [Header("Göktaşı damlaları (board)")]
    [Tooltip("Uçan su damlası sprite'ları (round-robin). Boşsa prosedürel damla.")]
    public List<Sprite> dropletSprites = new();
    [Tooltip("Uçan damla boyutu (tile oranı).")]
    [Range(0.2f, 1.5f)] public float dropletSizeRatio = 0.7f;
    [Tooltip("Uçan damla ve patlama damlalarına uygulanan renk (şeffaf beyaz cam damlasını board'da mavimsi yapar).")]
    public Color dropletTint = new Color(0.72f, 0.88f, 1f, 1f);
    [Tooltip("Depodan tepe noktasına yükselme süresi (sn).")]
    public float riseDuration = 0.34f;
    [Tooltip("Tepe noktasından hedef hücreye düşüş süresi (sn). Kısa = göktaşı gibi sert iner.")]
    public float fallDuration = 0.26f;
    [Tooltip("Tepe yüksekliği (tile), hedefle depodan yüksekte olanın üstüne eklenir. Min-max arası rastgele.")]
    public Vector2 apexHeightTiles = new Vector2(3f, 5f);
    [Tooltip("Damlaların ardışık fırlatılma aralığı (sn) — sırayla ateşlenir, hepsi aynı anda değil.")]
    public float launchInterval = 0.05f;
    [Tooltip("Düşüşte hız yönünde uzama (1 = yok).")]
    [Range(1f, 2.5f)] public float fallStretch = 1.6f;
    [Tooltip("Düşüşte arkada bırakılan iz parçacığı aralığı (sn). 0 = iz yok.")]
    public float trailInterval = 0.025f;

    [Header("Varış (sıçrama)")]
    [Tooltip("Varışta oluşan sıçrama sprite'ı. Boşsa damla sprite'ı.")]
    public Sprite splatSprite;
    [Range(0f, 2f)] public float splatSizeRatio = 0.95f;

    [Header("Ekran yağmur damlaları")]
    public bool playScreenDrops = true;
    [Tooltip("Telefon camına düşen damla sprite'ları (şeffaf PNG). Boşsa prosedürel cam damlası.")]
    public List<Sprite> screenDropSprites = new();
    [Tooltip("Ekrana düşen damla sayısı.")]
    [Min(0)] public int screenDropCount = 10;
    [Tooltip("Ekran damlalarının opaklık çarpanı (sprite zaten yarı şeffafsa 1 bırak).")]
    [Range(0.1f, 1f)] public float screenDropAlpha = 0.9f;
    [Tooltip("Damla boyutu, referans çözünürlükte (1080x1920) piksel. Min-max arası rastgele.")]
    public Vector2 screenDropSize = new Vector2(70f, 190f);
    [Tooltip("Bir damlanın ekranda kalma süresi (sn). Min-max arası rastgele.")]
    public Vector2 screenDropLifetime = new Vector2(1.6f, 2.6f);
}
