using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kullanım Şartları / Gizlilik Politikası'nı OYUN İÇİNDE gösteren kaydırılabilir pencere (internet gerekmez).
/// Metin LegalConsent.GetDocument'tan (indirilmiş güncel kopya ya da oyunla gelen kopya). Kabul ekranının da üstünde
/// çizilir; kapatınca altındaki pencere olduğu gibi kalır. Altta "Web'de aç" bağlantısı.
/// </summary>
public sealed class LegalDocumentView : MonoBehaviour
{
    private static LegalDocumentView current;

    public static void Show(string slug)
    {
        if (current != null) Destroy(current.gameObject);

        var root = new GameObject("LegalDocumentView");
        DontDestroyOnLoad(root);
        current = root.AddComponent<LegalDocumentView>();
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32766;   // kabul ekranının (32765) üstünde
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        var dim = Stretch(new GameObject("Dim", typeof(RectTransform), typeof(Image)).transform as RectTransform, root.transform);
        var dimImg = dim.GetComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.6f);
        dimImg.raycastTarget = true;

        string title = GameLocalization.Get(slug == "privacy" ? "settings_terms_open_privacy" : "settings_terms_open_terms");
        var view = CommonPopupView.Create(root.transform, title, Close);

        // Kaydırma alanı: gövdenin tamamı; içerik TMP, yüksekliği metne göre.
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        var vrt = (RectTransform)viewport.transform;
        vrt.SetParent(view.Body, false);
        CommonPopupView.Region(vrt, new Rect(0f, 0f, 1f, 1f));
        var vimg = viewport.GetComponent<Image>();
        vimg.color = new Color(1f, 1f, 1f, 0.001f);   // sürükleme için raycast yüzeyi
        vimg.raycastTarget = true;

        var content = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        var crt = (RectTransform)content.transform;
        crt.SetParent(vrt, false);
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = new Vector2(8f, 0f); crt.offsetMax = new Vector2(-8f, 0f);
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var text = CommonPopupView.Text(crt, "Text", LegalConsent.GetDocument(slug), 30, CommonPopupSkin.Shared.bodyTextColor);
        var trt = text.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.richText = true;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        var fit = text.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false; layout.padding = new RectOffset(0, 0, 4, 24);

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.content = crt;
        scroll.viewport = vrt;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var web = CommonPopupView.Button(view.Actions, "BtnWeb", GameLocalization.Get("legal_open_web"),
            () => Application.OpenURL(LegalConsent.WebUrl(slug)));
        var close = CommonPopupView.Button(view.Actions, "BtnClose2", GameLocalization.Get("common_ok"), Close);
        LegalConsent.SetLabelSize(web, CommonPopupSkin.Shared.actionFontSize * 0.62f);
        LegalConsent.SetLabelSize(close, CommonPopupSkin.Shared.actionFontSize * 0.7f);
        CommonPopupView.Region((RectTransform)web.transform, new Rect(0f, 0f, 0.48f, 1f));
        CommonPopupView.Region((RectTransform)close.transform, new Rect(0.52f, 0f, 0.48f, 1f));
    }

    private static RectTransform Stretch(RectTransform rt, Transform parent)
    {
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static void Close()
    {
        if (current != null) Destroy(current.gameObject);
        current = null;
    }

    private void OnDestroy() { if (current == this) current = null; }
}
