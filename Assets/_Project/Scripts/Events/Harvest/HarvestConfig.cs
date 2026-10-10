using System;
using UnityEngine;

/// <summary>
/// Bostan Hasadı (kazı event'i) ayarları — kodsuz ayar için tek kaynak (Docs/HarikaKazisi_Plan.md).
/// Kürekle bostan yatağındaki kareler kazılır; altta gizli ürünler var, bir ürünün tüm kareleri
/// açılınca toplanır; kattaki bütün ürünler bulununca kat ödülü verilir ve sonraki kata geçilir.
/// Izgara izometrik: u ekseni yatağın üst köşesinden SAĞ köşeye, v ekseni üst köşeden SOL köşeye.
/// </summary>
[CreateAssetMenu(fileName = "HarvestConfig", menuName = "TinyFixers/Events/Harvest Config")]
public sealed class HarvestConfig : ScriptableObject
{
    public const string ResourcePath = "Events/HarvestConfig";

    private static HarvestConfig s_shared;
    public static HarvestConfig Shared =>
        s_shared != null ? s_shared : (s_shared = Resources.Load<HarvestConfig>(ResourcePath));

    [Header("Takvim & kapı")]
    [Tooltip("Oynanacak level bu sayıya ulaşınca açılır (25. seviye bitince = 26).")]
    [Min(1)] public int minLevelGate = 26;
    [Tooltip("Döngü uzunluğu (gün). İlk activeDays gün event açık, kalanı ara.")]
    [Min(1)] public int cycleDays = 7;
    [Min(1)] public int activeDays = 5;
    [Tooltip("Döngülerin başladığı UTC gün (yyyy-MM-dd). Tüm cihazlarda aynı takvim.")]
    public string epochUtc = "2026-01-05";

    [Header("Debug (yalnız Editor — PRODUCTION'DA KAPAT)")]
    [Tooltip("Açıkken takvim ve level kapısı yok sayılır (yalnız UNITY_EDITOR).")]
    public bool debugForceAvailable = false;

    [Header("Büyük sandık")]
    [Tooltip("Son hasadın sandığı oyuncunun harikasının sandığı olur (WonderProgress.ActiveWonder).")]
    public WonderCatalog wonderCatalog;

    [Header("Izgara")]
    [Tooltip("Bostan yatağı kare ızgara: N x N hücre (yatak kare olduğu için tüm katlarda aynı).")]
    [Range(3, 8)] public int gridSize = 5;

    [Header("Kürek kazanma")]
    [Min(0)] public int trowelsPerWin = 1;
    [Tooltip("İlk denemede kazanınca EK kürek.")]
    [Min(0)] public int firstTryBonus = 1;
    [Tooltip("Boss düellosu kazanınca verilen kürek (normal + ilk deneme yerine).")]
    [Min(0)] public int bossWinTrowels = 3;

    [Header("Ürünler ve katlar")]
    public HarvestCropDef[] crops = Array.Empty<HarvestCropDef>();
    public HarvestFloorDef[] floors = Array.Empty<HarvestFloorDef>();

    [Header("Sahne (arka plan pikseli, sol-üst köşe orijin)")]
    public Sprite sceneBackground;
    [Tooltip("Bostan yatağının iç toprak köşeleri (arka plan pikseli).")]
    public Vector2 bedTop = new(567, 714);
    public Vector2 bedRight = new(1125, 1050);
    public Vector2 bedBottom = new(600, 1395);
    public Vector2 bedLeft = new(15, 1026);

    [Header("Toprak kareleri")]
    public Sprite[] soilTiles = Array.Empty<Sprite>();
    public Sprite dugTile;
    [Tooltip("İpucu ışıltısı: yarısı kazılmış ürünün kalan karelerinde yanıp sönen yumuşak ışık.")]
    public Sprite hintGlow;
    public Color hintGlowColor = new(1f, 0.93f, 0.55f, 0.75f);
    [Tooltip("Karenin hücre enine oranı. <1 → kareler arasında ince derz kalır, hücreler okunur.")]
    [Range(0.6f, 1.1f)] public float tileFill = 0.92f;
    [Tooltip("Kare görselinde üst yüzün (elmas) merkezi — sol-üst köşeden, 0..1.")]
    public Vector2 tileTopFaceCenter = new(0.511f, 0.479f);
    [Tooltip("Üst yüzün genişliğinin görsel genişliğine oranı.")]
    [Range(0.5f, 1f)] public float tileTopFaceWidth = 0.956f;

    [Header("Karakter / ikon")]
    public Sprite bearIdle;
    public Sprite bearDig;
    public Sprite bearCheer;
    public Sprite trowelIcon;

    public int CellCount => gridSize * gridSize;
    public int FloorCount => floors != null ? floors.Length : 0;

    public HarvestFloorDef GetFloor(int index) =>
        floors != null && index >= 0 && index < floors.Length ? floors[index] : null;

    public HarvestCropDef GetCrop(int index) =>
        crops != null && index >= 0 && index < crops.Length ? crops[index] : null;

    /// Hücre indeksi (u + v*N) bu kattaki hangi yerleşime ait? Yoksa -1.
    public int PlacementAt(HarvestFloorDef floor, int cell)
    {
        if (floor?.placements == null) return -1;
        int u = cell % gridSize, v = cell / gridSize;
        for (int i = 0; i < floor.placements.Length; i++)
            if (floor.placements[i].Covers(u, v, this)) return i;
        return -1;
    }

    /// Yerleşimin kapladığı hücrelerin bit maskesi.
    public int PlacementMask(HarvestPlacement p)
    {
        var crop = GetCrop(p.crop);
        if (crop == null) return 0;
        int mask = 0;
        for (int du = 0; du < crop.size.x; du++)
            for (int dv = 0; dv < crop.size.y; dv++)
            {
                int u = p.origin.x + du, v = p.origin.y + dv;
                if (u >= 0 && u < gridSize && v >= 0 && v < gridSize) mask |= 1 << (u + v * gridSize);
            }
        return mask;
    }

    private void OnValidate()
    {
        // Bit maskeleri int'te tutulur → en fazla 31 hücre; 8x8 izin verilmez ama koruma kalsın.
        if (gridSize * gridSize > 31) gridSize = 5;
    }
}

[Serializable]
public sealed class HarvestCropDef
{
    public string id = "carrot";
    public Sprite sprite;
    [Tooltip("Izgarada kapladığı alan: x = u ekseni (sağa), y = v ekseni (sola).")]
    public Vector2Int size = Vector2Int.one;
}

[Serializable]
public sealed class HarvestPlacement
{
    [Tooltip("HarvestConfig.crops içindeki indeks.")]
    public int crop;
    [Tooltip("Sol-üst hücre (u, v).")]
    public Vector2Int origin;

    public bool Covers(int u, int v, HarvestConfig cfg)
    {
        var c = cfg.GetCrop(crop);
        if (c == null) return false;
        return u >= origin.x && u < origin.x + c.size.x && v >= origin.y && v < origin.y + c.size.y;
    }
}

[Serializable]
public sealed class HarvestFloorDef
{
    public HarvestPlacement[] placements = Array.Empty<HarvestPlacement>();
    [Tooltip("Kat bitince verilen ödüller.")]
    public DailySlotReward[] rewards = Array.Empty<DailySlotReward>();
}
