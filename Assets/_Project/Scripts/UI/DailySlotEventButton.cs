using UnityEngine;
using UnityEngine.UI;

public class DailySlotEventButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject notifBadge;
    [SerializeField] private DailySlotMachineController slotMachine;

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(OnClicked);
    }

    private void OnEnable()
    {
        RefreshBadge();
    }

    private void OnClicked()
    {
        if (slotMachine != null) slotMachine.Open();
        RefreshBadge();
    }

    private void RefreshBadge()
    {
        if (notifBadge != null)
            notifBadge.SetActive(DailySlotMachineController.HasAvailableSpin());
    }
}
