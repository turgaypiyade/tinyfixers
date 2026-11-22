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

    [Tooltip("Sarı (inşaat) buton kalır; etrafına popup butonlarındaki sarı çerçeve (ButtonFrameBacking, oransal). " +
             "Kullanıcı 2026-11-22.")]
    [SerializeField] private Sprite buttonFrameSprite;
    [Tooltip("Kenarsız sarı buton (GreenButonwoStroke'un görev sarısına boyanmışı, 9-slice) + ayrı anahtar ikonu.")]
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Sprite wrenchIcon;
    // Anahtar, eski RepairButtonNewUI'deki yerinde (görsel oranı): x %8.8–29.5, y %18–81 → buton büyüse de oranlı.
    private static readonly Vector2 WrenchMin = new Vector2(0.088f, 0.19f), WrenchMax = new Vector2(0.295f, 0.818f);
    private Image wrench;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OpenPanel);
    }

    private void Start() => RefreshSkin();
    private bool frameAttached;

    /// ChapterThemeApplier tema görselini atadıktan sonra da çağırır. Anahtar yalnız kenarsız sarı görselde görünür:
    /// Usta Ligi'nin kendi görselinde (veya eski anahtarlı görselde) ikinci bir ikon çıkmasın.
    // NOT: sahnede bu bileşen KAPALI (tıklamayı başka bileşen yönetiyor) → Start/Awake çalışmaz; tema bu metodu
    // doğrudan çağırır. Bu yüzden çerçeve de burada, bir kez eklenir.
    public void RefreshSkin()
    {
        if (button == null || !button.TryGetComponent(out Image image)) return;
        bool ours = buttonSprite != null && image.sprite == buttonSprite;
        if (ours)
        {
            image.type = buttonSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            // Seviye butonuyla aynı: kenar eğimi PreLevel'deki gibi yüksekliğe oranlı.
            var rt = (RectTransform)button.transform;
            if (image.type == Image.Type.Sliced && rt.rect.height > 1f)
                image.pixelsPerUnitMultiplier = buttonSprite.rect.height / rt.rect.height;
        }
        if (ours && !frameAttached && buttonFrameSprite != null)
        {
            frameAttached = true;
            ButtonFrameBacking.Attach(button, buttonFrameSprite);
        }
        if (wrenchIcon == null) return;
        if (wrench == null)
        {
            if (!ours) return;
            var rt = (RectTransform)new GameObject("WrenchIcon", typeof(RectTransform)) { layer = gameObject.layer }.transform;
            rt.SetParent(button.transform, false);
            rt.SetAsFirstSibling();   // yazının arkasında
            rt.anchorMin = WrenchMin;
            rt.anchorMax = WrenchMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            wrench = rt.gameObject.AddComponent<Image>();
            wrench.sprite = wrenchIcon;
            wrench.preserveAspect = true;
            wrench.raycastTarget = false;
        }
        wrench.gameObject.SetActive(ours);
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
                               && workshop.CurrentStage < workshop.TotalTasks
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
