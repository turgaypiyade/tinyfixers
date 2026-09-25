using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MainMenu'deki "Görevler/Aç" butonu. Bölge açma görev listesini (RegionUnlockListPanel) açar.
/// Açılabilir bölge varsa (kilitli + yeterli yıldız) notification dot gösterir.
/// (Eski MainMenuRepairButton'ın bölge-açma karşılığı.)
/// </summary>
public sealed class MainMenuRegionUnlockButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private RegionUnlockListPanel panel;
    [SerializeField] private WorldMapController worldMap;

    [Tooltip("Atanırsa buton WONDER modunda çalışır: nokta, bölüm kapısı AÇIK ve yıldız yeterliyken yanar.")]
    [SerializeField] private WonderCatalog wonderCatalog;

    [Tooltip("\"Açılabilir bölge var\" işareti (kırmızı nokta vb). Opsiyonel.")]
    [SerializeField] private GameObject notificationDot;

    [Tooltip("Tüm bölgeler açıldıysa butonu gizle. Kapalıysa buton aktif kalır.")]
    [SerializeField] private bool hideWhenCompleted = false;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OpenPanel);
    }

    private void OnEnable()
    {
        PlayerWallet.OnTotalStarsChanged += HandleStarsChanged;
        if (worldMap != null) worldMap.OnRegionUnlocked += HandleRegionUnlocked;
        WonderProgress.OnTaskCompleted += HandleWonderChanged;
        WonderProgress.OnWonderCompleted += HandleWonderChanged;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerWallet.OnTotalStarsChanged -= HandleStarsChanged;
        if (worldMap != null) worldMap.OnRegionUnlocked -= HandleRegionUnlocked;
        WonderProgress.OnTaskCompleted -= HandleWonderChanged;
        WonderProgress.OnWonderCompleted -= HandleWonderChanged;
    }

    private void HandleStarsChanged(int _) => Refresh();
    private void HandleRegionUnlocked(WorldMapRegion _) => Refresh();
    private void HandleWonderChanged(int _) => Refresh();

    private void OpenPanel()
    {
        if (panel != null) panel.Open();
    }

    private void Refresh()
    {
        // Wonder modu: nokta yalnız görev GERÇEKTEN başlatılabiliyorsa yanar
        // (bölüm kapısı açık + yıldız yeterli) — kilitli göreve "hazır" işareti koyma.
        if (wonderCatalog != null)
        {
            bool anyTaskLeft = WonderProgress.ActiveEventIndex(wonderCatalog) >= 0
                               || WonderProgress.HasLockedEvent(wonderCatalog);
            if (hideWhenCompleted) gameObject.SetActive(anyTaskLeft);
            if (notificationDot != null)
                notificationDot.SetActive(WonderProgress.HasStartableTask(wonderCatalog));
            return;
        }

        if (worldMap == null) return;

        bool allDone = worldMap.AllUnlocked;

        if (hideWhenCompleted)
            gameObject.SetActive(!allDone);

        if (notificationDot != null)
        {
            var next = worldMap.GetLockedRegions(1);
            bool hasPlayable = next.Count > 0 && PlayerWallet.HasEnoughStars(next[0].StarCost);
            notificationDot.SetActive(hasPlayable);
        }
    }
}
