using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun sahnesinden ÇIKMADAN açılan market. Fail akışında oyuncu altın almaya gidince level
/// kaybolmasın diye: ana menü market'iyle aynı katalog + kart prefab'larını kendi overlay canvas'ına
/// basar, satın alma ShopPurchaseService'ten geçer (içerik anında verilir). Kapatınca onClosed çağrılır;
/// çağıran (LevelEndSimplePopupController) altın yetiyorsa oyunu kaldığı yerden sürdürür.
/// </summary>
public sealed class InGameShopOverlay : MonoBehaviour
{
    /// <summary>Market içeriği için referanslar (ana menüdeki ShopScreenController ile aynı varlıklar).</summary>
    [Serializable]
    public struct Refs
    {
        [Tooltip("Ana menü market ekranının prefab'ı (ShopScreenPanel) — varsa ekran BİREBİR aynı kurulur.")]
        public GameObject panelPrefab;
        public ShopCatalog catalog;
        public UITheme theme;
        public ShopSectionHeader sectionHeaderPrefab;
        public ShopOfferCard bundleCardPrefab;
        public ShopCoinRowCard coinRowPrefab;

        public bool IsValid => panelPrefab != null
            || (catalog != null && (bundleCardPrefab != null || coinRowPrefab != null));
    }

    private const float ContentWidth = 930f;   // ana menü market içerik genişliği

    private Refs refs;
    private Action onClosed;
    private RectTransform content;
    private TMP_Text balanceText;
    private TMP_Text toastText;
    private Coroutine toastRoutine;
    private readonly List<GameObject> spawned = new();

    public static InGameShopOverlay Open(Refs refs, Action onClosed)
    {
        var root = new GameObject("InGameShopOverlay");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // fail popup'ın üstünde, RuntimeChoicePopup'ın (32760) altında
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        var overlay = root.AddComponent<InGameShopOverlay>();
        overlay.refs = refs;
        overlay.onClosed = onClosed;
        if (refs.panelPrefab != null)
            overlay.BuildFromPanelPrefab();
        else
            overlay.Build();
        return overlay;
    }

    // Ana menüdeki market ekranının kendisi: arka plan, başlık bandı, cüzdan, kartlar, bildirim ve
    // satın alma (ShopScreenController → ShopPurchaseService) aynen. Ana menüde alttaki sekme çubuğu
    // için bırakılan boşluğa "Kapat" konur.
    private void BuildFromPanelPrefab()
    {
        var panel = Instantiate(refs.panelPrefab, transform, false);
        panel.name = "ShopPanel";
        var panelRt = (RectTransform)panel.transform;
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
        panel.SetActive(true);

        var close = CommonPopupView.Button(transform, "BtnClose", "Kapat", Close);
        CommonPopupView.Region((RectTransform)close.transform, new Rect(0.30f, 0.03f, 0.40f, 0.075f));
    }

    private void OnEnable() => PlayerWallet.OnCoinsChanged += RefreshBalance;
    private void OnDisable() => PlayerWallet.OnCoinsChanged -= RefreshBalance;

    private void Build()
    {
        // Arka plan (tahtayı tamamen örter, tıklamayı keser).
        var bg = CommonPopupView.NewRect(transform, "Background", Vector2.zero);
        CommonPopupView.Region(bg, new Rect(0, 0, 1, 1));
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.09f, 0.07f, 0.16f, 0.97f);
        bgImg.raycastTarget = true;

        // Üst: başlık + bakiye.
        var title = CommonPopupView.Text(transform, "Title", "Market", 72, Color.white);
        CommonPopupView.Region(title.rectTransform, new Rect(0.05f, 0.90f, 0.90f, 0.08f));
        balanceText = CommonPopupView.Text(transform, "Balance", "", 44, new Color(1f, 0.86f, 0.35f));
        CommonPopupView.Region(balanceText.rectTransform, new Rect(0.05f, 0.855f, 0.90f, 0.045f));
        RefreshBalance(PlayerWallet.Coins);

        // Orta: kaydırılabilir teklif listesi (ana menü market'iyle aynı dizilim).
        var viewport = CommonPopupView.NewRect(transform, "Viewport", Vector2.zero);
        CommonPopupView.Region(viewport, new Rect(0, 0.12f, 1, 0.725f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var viewportImg = viewport.gameObject.AddComponent<Image>();
        viewportImg.color = new Color(0, 0, 0, 0.001f);   // sürükleme yakalansın

        content = CommonPopupView.NewRect(viewport, "Content", Vector2.zero);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(ContentWidth, 0f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;

        // Alt: kapat.
        var close = CommonPopupView.Button(transform, "BtnClose", "Kapat", Close);
        CommonPopupView.Region((RectTransform)close.transform, new Rect(0.30f, 0.025f, 0.40f, 0.075f));

        // Satın alma bildirimi.
        toastText = CommonPopupView.Text(transform, "Toast", "", 42, new Color(0.6f, 1f, 0.6f));
        CommonPopupView.Region(toastText.rectTransform, new Rect(0.05f, 0.10f, 0.90f, 0.04f));
        toastText.gameObject.SetActive(false);

        Populate();
    }

    private void Populate()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
        ShopScreenController.PopulateOffers(refs.catalog, refs.theme, content, refs.sectionHeaderPrefab,
            refs.bundleCardPrefab, refs.coinRowPrefab, HandlePurchase, spawned);
    }

    private void HandlePurchase(ShopOffer offer)
    {
        if (!ShopPurchaseService.TryPurchase(offer)) return;

        ShowToast($"{offer.displayName} alındı!");
        Populate();   // bakiye + tek seferlik teklif durumu değişti
    }

    private void ShowToast(string message)
    {
        if (toastText == null) return;
        toastText.text = message;
        toastText.gameObject.SetActive(true);
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(HideToast());
    }

    private IEnumerator HideToast()
    {
        yield return new WaitForSecondsRealtime(1.4f);
        if (toastText != null) toastText.gameObject.SetActive(false);
        toastRoutine = null;
    }

    private void RefreshBalance(int coins)
    {
        if (balanceText != null) balanceText.text = $"Altın: {coins:N0}";
    }

    private void Close()
    {
        var callback = onClosed;
        onClosed = null;
        Destroy(gameObject);
        callback?.Invoke();
    }
}
