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
        Refresh();
    }

    private void OnDisable()
    {
        PlayerWallet.OnTotalStarsChanged -= HandleStarsChanged;
        if (worldMap != null) worldMap.OnRegionUnlocked -= HandleRegionUnlocked;
    }

    private void HandleStarsChanged(int _) => Refresh();
    private void HandleRegionUnlocked(WorldMapRegion _) => Refresh();

    private void OpenPanel()
    {
        if (panel != null) panel.Open();
    }

    private void Refresh()
    {
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
