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

    [Header("Resim üstü düzen (koddan kurulur)")]
    [Tooltip("Gökyüzüne DynaPuff başlık + kural satırı; ödül alttaki koyu şeride taşınır (cipin üstüne binmesin).")]
    [SerializeField, Range(0f, 1f)] private float headlineY = 0.9f;
    [SerializeField, Range(0f, 1f)] private float rulesY = 0.81f;
    [SerializeField, Range(0f, 1f)] private float prizeBandY = 0.09f;

    private TMP_Text headlineText, rulesText;
    private RectTransform prizeBand;
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
        EventSfx.Play(x => x.popupOpen);
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
        if (titleText != null) titleText.text = GameLocalization.Get("safari_join_title");
        if (prizeText != null) prizeText.text = GameLocalization.GetFormat("safari_join_prize", config != null ? config.prizePoolGold : 10000);
        if (bodyText != null)
            bodyText.text = GameLocalization.GetFormat("safari_join_body", config != null ? config.pitstopCount : 7);
        BuildImageTexts(config);

        // Buton: ortak event "Katıl" (lokalize, büyük harf değil — Köprü popup'ıyla aynı anahtar).
        var joinLabel = continueButton != null ? continueButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (joinLabel != null)
        {
            string value = GameLocalization.Get("event_join_button");
            joinLabel.text = string.IsNullOrEmpty(value) || value == "event_join_button" ? "Katıl" : value;
        }
    }

    // Gökyüzünde başlık (DynaPuff, Köprü popup'ıyla aynı dil) + kısa kural; ödül resmin altındaki yarı saydam
    // koyu şeritte (eskiden cipin ortasında okunmuyordu).
    private void BuildImageTexts(SafariConfig config)
    {
        if (overlayImage == null || !overlayImage.gameObject.activeInHierarchy) return;
        var host = overlayImage.rectTransform;
        var skin = CommonPopupSkin.Shared;

        if (headlineText == null)
        {
            headlineText = MakeText("Headline", host, headlineY, 0.86f, 0.11f);
            if (skin != null && skin.celebrationFont != null)
            {
                headlineText.font = skin.celebrationFont;
                if (skin.celebrationMaterial != null) headlineText.fontSharedMaterial = skin.celebrationMaterial;
            }
        }
        headlineText.text = GameLocalization.GetOr("safari_join_headline", "Zirveye ilk sen ulaş!");

        if (rulesText == null)
        {
            rulesText = MakeText("Rules", host, rulesY, 0.8f, 0.06f);
            if (skin != null && skin.font != null) rulesText.font = skin.font;
            rulesText.color = Color.white;
            rulesText.outlineWidth = 0.25f;
            rulesText.outlineColor = new Color32(40, 20, 10, 255);
        }
        rulesText.text = GameLocalization.GetFormat("safari_join_rules", config != null ? config.pitstopCount : 7);

        if (prizeText != null && prizeBand == null)
        {
            var bandGo = new GameObject("PrizeBand", typeof(RectTransform), typeof(Image));
            bandGo.layer = host.gameObject.layer;
            prizeBand = (RectTransform)bandGo.transform;
            prizeBand.SetParent(host, false);
            prizeBand.anchorMin = new Vector2(0f, prizeBandY - 0.07f);
            prizeBand.anchorMax = new Vector2(1f, prizeBandY + 0.07f);
            prizeBand.offsetMin = prizeBand.offsetMax = Vector2.zero;
            var band = bandGo.GetComponent<Image>();
            band.color = new Color(0.12f, 0.05f, 0.03f, 0.62f);
            band.raycastTarget = false;
            var prt = prizeText.rectTransform;
            prt.SetParent(prizeBand, false);
            prt.anchorMin = new Vector2(0.05f, 0.08f); prt.anchorMax = new Vector2(0.95f, 0.92f);
            prt.offsetMin = prt.offsetMax = Vector2.zero; prt.anchoredPosition = Vector2.zero;
            prizeText.enableAutoSizing = true;
            prizeText.fontSizeMin = 14f;
        }
    }

    private static TMP_Text MakeText(string name, RectTransform host, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = host.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(host, false);
        rt.anchorMin = new Vector2(0.5f - width * 0.5f, y - height * 0.5f);
        rt.anchorMax = new Vector2(0.5f + width * 0.5f, y + height * 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 90f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
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
        // Ortak popup girişi (yukarıdan kayarak + fade). Ölçek yalnız sığdırma için.
        popupRoot.localScale = popupBaseScale * fit;
        PopupEntranceAnimator.Enter(popupRoot);
        entrance = null;
    }

    private void OnContinue()
    {
        EventSfx.Play(x => x.uiTap);
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
