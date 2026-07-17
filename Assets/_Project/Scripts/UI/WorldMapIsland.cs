using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bir adacık: altındaki WorldMapRegion'ları (hiyerarşi sırasıyla) gruplar.
/// Tüm bölgeleri açılınca sandık töreni (RewardChestRevealOverlay) oynar ve
/// chestRewards bir KEZ verilir (PlayerPrefs island_chest_claimed_&lt;id&gt;).
///
/// Sahne kurulumu: Content altında Island1, Island2... objeleri yarat, Region'ları
/// altına taşı, bu component'i ekle ve WorldMapController.islands listesine SIRAYLA ata.
/// Bölgelerin ada içi açılış sırası = hiyerarşi sırası.
/// </summary>
public sealed class WorldMapIsland : MonoBehaviour
{
    [Tooltip("Sandık kalıcılık anahtarı — adalar arasında benzersiz olmalı (örn \"ada1\"). Boşsa obje adı kullanılır.")]
    [SerializeField] private string islandId;

    [Header("Sandık Ödülü (ada tamamlanınca)")]
    [Tooltip("Ada tamamlanınca sandıktan çıkacak ödüller (coin/yıldız/joker/booster).")]
    [SerializeField] private DailySlotReward[] chestRewards;

    [Tooltip("Sandık görselleri. Boşsa Resources/UI/RewardChestClosed|Opened kullanılır.")]
    [SerializeField] private Sprite chestClosedSprite;
    [SerializeField] private Sprite chestOpenedSprite;

    [Header("Kamera Odağı")]
    [Tooltip("Bu adadaki TÜM açılışlarda kameranın ortalayacağı nokta. " +
             "Boşsa adanın kendi transform'u (karo merkezi) kullanılır — çoğu ada için yeterli.")]
    [SerializeField] private RectTransform revealFocus;

    private WorldMapRegion[] cachedRegions;

    public string IslandId => string.IsNullOrEmpty(islandId) ? gameObject.name : islandId;

    /// <summary>Ada odak noktası: atanmışsa revealFocus, yoksa karonun kendisi (merkez).</summary>
    public RectTransform RevealFocus => revealFocus != null ? revealFocus : (RectTransform)transform;
    public IReadOnlyList<DailySlotReward> ChestRewards => chestRewards;
    public Sprite ChestClosedSprite => chestClosedSprite;
    public Sprite ChestOpenedSprite => chestOpenedSprite;

    /// <summary>Altındaki bölgeler, hiyerarşi sırasıyla (= ada içi açılış sırası).</summary>
    public IReadOnlyList<WorldMapRegion> Regions
    {
        get
        {
            if (cachedRegions == null || cachedRegions.Length == 0)
                cachedRegions = GetComponentsInChildren<WorldMapRegion>(true);
            return cachedRegions;
        }
    }

    public int TotalRegions => Regions.Count;

    public int UnlockedCount
    {
        get
        {
            int c = 0;
            for (int i = 0; i < Regions.Count; i++)
                if (Regions[i] != null && Regions[i].IsUnlocked) c++;
            return c;
        }
    }

    public bool AllRegionsUnlocked
    {
        get
        {
            for (int i = 0; i < Regions.Count; i++)
                if (Regions[i] != null && !Regions[i].IsUnlocked) return false;
            return Regions.Count > 0;
        }
    }

    private string ChestPrefKey => $"island_chest_claimed_{IslandId}";

    public bool ChestClaimed => PlayerPrefs.GetInt(ChestPrefKey, 0) == 1;

    public void MarkChestClaimed()
    {
        PlayerPrefs.SetInt(ChestPrefKey, 1);
        PlayerPrefs.Save();
    }
}
