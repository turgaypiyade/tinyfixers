using System.Collections;
using TMPro;
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
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text prizeText;

    private SafariEventController controller;
    private Coroutine entrance;
    private bool showRequested;   // Show() ile mi uyandık, yoksa editörde aktif mi bırakıldık?
    private Vector3 popupBaseScale = Vector3.one;   // editörde authorlanan ölçek (animasyon bunu ezmez)
    private bool baseScaleCaptured;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
        if (cancelButton != null)   cancelButton.onClick.AddListener(OnCancel);
        // Popup yalnız Show() ile açılır: ayrı bir root varsa onu, root == bu obje ise kendimizi kapat.
        // showRequested, Show() içinde SetActive'ten ÖNCE set edilir; lazy-Awake yolunda kendimizi kapatmayız.
        if (root != null && root != gameObject) root.SetActive(false);
        else if (!showRequested) { gameObject.SetActive(false); return; }
        RefreshImages();
    }

    public void Show(SafariEventController owner)
    {
        controller = owner;
        showRequested = true;
        gameObject.SetActive(true);
        RefreshImages();
        if (root != null) root.SetActive(true);
        RefreshCopy();
        if (entrance != null) StopCoroutine(entrance);
        entrance = StartCoroutine(Reveal());
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    private void OnDisable()
    {
        if (entrance != null) { StopCoroutine(entrance); entrance = null; }
    }

    private void RefreshCopy()
    {
        var config = controller != null ? controller.Config : null;
        if (titleText != null) titleText.text = "SAFARİ";
        if (prizeText != null) prizeText.text = $"{(config != null ? config.prizePoolGold : 2000):N0} ALTIN";
        if (bodyText != null)
            bodyText.text = $"<b>{(config != null ? config.pitstopCount : 7)} KAT · BÜYÜK ÖDÜL</b>\nBölümleri ilk denemede geç,\nzirvedeki ödülü paylaş!";
    }

    private IEnumerator Reveal()
    {
        if (popupRoot == null) yield break;
        Canvas.ForceUpdateCanvases();

        if (!baseScaleCaptured)
        {
            popupBaseScale = popupRoot.localScale;   // editörde ne verildiyse o (genelde 1)
            baseScaleCaptured = true;
        }

        // Sığdırma yalnızca GERÇEK taşma için: eskiden 80/100px pay eklendiği için ekrana tam
        // oturan (stretch-anchor'lı) popup bile boşuna 0.92'ye küçülüyordu.
        var viewport = popupRoot.parent as RectTransform;
        float fit = 1f;
        if (viewport != null)
        {
            float w = popupRoot.rect.width, h = popupRoot.rect.height;
            if (w > viewport.rect.width || h > viewport.rect.height)
                fit = Mathf.Min(viewport.rect.width / Mathf.Max(1f, w), viewport.rect.height / Mathf.Max(1f, h));
        }
        // NOT: `??` Unity'nin sahte-null kontrolünü atlar; TryGetComponent şart.
        if (!popupRoot.TryGetComponent(out CanvasGroup group))
            group = popupRoot.gameObject.AddComponent<CanvasGroup>();
        float elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / 0.4f);
            group.alpha = Mathf.SmoothStep(0f, 1f, k);
            popupRoot.localScale = popupBaseScale * fit * (1f - 0.14f * Mathf.Pow(1f - k, 3f) + Mathf.Sin(k * Mathf.PI) * 0.025f);
            yield return null;
        }
        group.alpha = 1f;
        popupRoot.localScale = popupBaseScale * fit;
        entrance = null;
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
