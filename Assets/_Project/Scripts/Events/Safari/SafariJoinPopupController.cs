using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tiny Safari giriş popup'ı. Event ilk aktif olduğunda otomatik, hiç katılmadıysa saatte bir çıkar
/// (cadans controller'da). "Devam" → yarışa gir + harita; "Vazgeç" → kapat, ikon aktif kalır.
///
/// PreLevel popup gibi ana popup görseli + opsiyonel overlay image + iki buton kullanır.
/// </summary>
public sealed class SafariJoinPopupController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private Image popupBackgroundImage;
    [SerializeField] private Image overlayImage;
    [SerializeField] private Button continueButton;
    [SerializeField] private Image continueButtonImage;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Image cancelButtonImage;

    private SafariEventController controller;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
        if (cancelButton != null)   cancelButton.onClick.AddListener(OnCancel);
        // root == bu obje ise burada kapatma (kendini kapatınca Show'da lazy-Awake ile görünmez kalır);
        // yalnız root ayrı child ise kapat. Kendi objesi editörde pasif author'lanır.
        if (root != null && root != gameObject) root.SetActive(false);
        RefreshImages();
    }

    public void Show(SafariEventController owner)
    {
        controller = owner;
        gameObject.SetActive(true);
        RefreshImages();
        if (root != null) root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    private void OnContinue()
    {
        Hide();
        if (controller != null) controller.OnJoinAccepted();
    }

    private void OnCancel()
    {
        Hide();
        if (controller != null) controller.OnJoinDeclined();
    }

    private void RefreshImages()
    {
        if (overlayImage != null)
        {
            overlayImage.gameObject.SetActive(overlayImage.sprite != null);
        }
    }
}
