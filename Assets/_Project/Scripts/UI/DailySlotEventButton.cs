using UnityEngine;
using UnityEngine.UI;

public class DailySlotEventButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject notifBadge;
    [SerializeField] private FortuneWheelController fortuneWheel;

    [Header("Bildirim rozeti (ortak UiBadge görünümü)")]
    [SerializeField, Min(8f)] private float badgeSize = 40f;
    [Tooltip("Butonun sağ üst köşesine göre rozet merkezi (yuvarlak ikonun kenarına oturur).")]
    [SerializeField] private Vector2 badgeCornerOffset = new Vector2(-26f, -26f);

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(OnClicked);
        if (notifBadge != null && notifBadge.TryGetComponent(out Image badgeImage))
            UiBadge.StyleDot(badgeImage, badgeSize, badgeCornerOffset);
    }

    private void OnEnable()
    {
        RefreshBadge();
    }

    private void OnClicked()
    {
        if (fortuneWheel != null) fortuneWheel.Open();
        RefreshBadge();
    }

    private void RefreshBadge()
    {
        if (notifBadge != null)
            notifBadge.SetActive(FortuneWheelController.HasAvailableSpin());
    }
}
