using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MainMenu'deki "Onar" butonu. Repair task list panel'ini açar.
/// Yapılabilir görev varsa (yeterli yıldız + aktif aşama mevcut) bir notification dot gösterir.
/// </summary>
public class MainMenuRepairButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private RepairTaskListPanel panel;
    [SerializeField] private WorkshopController workshop;

    [Tooltip("\"Yapılabilir görev var\" işareti (kırmızı nokta vb). Opsiyonel.")]
    [SerializeField] private GameObject notificationDot;

    [Tooltip("Tüm aşamalar bittiyse butonu gizle. Kapalıysa buton aktif kalır.")]
    [SerializeField] private bool hideWhenCompleted = false;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OpenPanel);
    }

    private void OnEnable()
    {
        PlayerWallet.OnTotalStarsChanged += HandleStarsChanged;
        if (workshop != null) workshop.OnStageCompleted += HandleStageCompleted;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerWallet.OnTotalStarsChanged -= HandleStarsChanged;
        if (workshop != null) workshop.OnStageCompleted -= HandleStageCompleted;
    }

    private void HandleStarsChanged(int _) => Refresh();
    private void HandleStageCompleted(int _) => Refresh();

    private void OpenPanel()
    {
        if (panel != null) panel.Open();
    }

    private void Refresh()
    {
        if (workshop == null || workshop.StageData == null) return;

        bool allDone = workshop.IsAllCompleted;

        if (hideWhenCompleted)
            gameObject.SetActive(!allDone);

        if (notificationDot != null)
        {
            bool hasPlayable = !allDone
                               && workshop.CurrentStage < workshop.TotalStages
                               && PlayerWallet.HasEnoughStars(GetActiveStarCost());
            notificationDot.SetActive(hasPlayable);
        }
    }

    private int GetActiveStarCost()
    {
        var active = workshop.GetActiveStage();
        return active != null ? active.starCost : int.MaxValue;
    }
}
