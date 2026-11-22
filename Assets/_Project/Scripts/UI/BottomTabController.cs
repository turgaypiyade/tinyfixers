using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menü alt navigasyon çubuğu. Her sekme ayrı parça (ikon + yazı + opsiyonel
/// selected highlight) ve kendi ekranını (panel) açar.
///
/// Seçili sekme: ikon yukarı kalkar + büyür, yazı büyür, selected highlight görünür,
/// paneli açılır. Diğerleri Inspector'daki default boyut/konuma döner, panelleri kapanır.
/// Default boyut/konum runtime'da Awake'te yakalanır — yani sahnede ne ayarladıysan o
/// "seçili değil" hâli olur; büyütme/kaldırma onun ÜZERİNE uygulanır.
///
/// Görselleri (ikon, selected arka plan) sen Inspector'da bağlarsın; bu script sadece
/// durum geçişini (seç / büyüt / panel aç) yönetir.
/// </summary>
public sealed class BottomTabController : MonoBehaviour
{
    [Serializable]
    public sealed class Tab
    {
        public string name;                 // sadece Inspector'da okunurluk için
        public Button button;               // tıklama hedefi
        public RectTransform icon;          // seçilince yukarı kalkıp büyüyecek ikon
        public RectTransform label;         // seçilince büyüyecek yazı (opsiyonel)
        public GameObject selectedHighlight; // senin vereceğin selected arka plan/glow (opsiyonel)
        public GameObject panel;            // bu sekmenin açacağı ekran (opsiyonel)
    }

    [Header("Sekmeler (soldan sağa)")]
    [SerializeField] private List<Tab> tabs = new();

    [Header("Seçili durum çarpanları (default'un ÜZERİNE)")]
    [Tooltip("Seçili ikonun yukarı kalkma miktarı (px).")]
    [SerializeField] private float iconLift = 18f;
    [Tooltip("Seçili ikon büyütme çarpanı.")]
    [SerializeField] private float iconScale = 1.2f;
    [Tooltip("Seçili yazı büyütme çarpanı.")]
    [SerializeField] private float labelScale = 1.12f;

    [Header("Başlangıç")]
    [Tooltip("Açılışta seçili sekme index'i (örn HOME).")]
    [SerializeField] private int defaultIndex = 2;

    [Header("Sadece HOME'da görünür öğeler")]
    [Tooltip("HOME dışı sekmelerde gizlenecek main-menu overlay'leri: RightEventPanel, üst HUD, " +
             "event panelleri vb. HOME'a dönünce geri gelir.")]
    [SerializeField] private GameObject[] homeOnlyElements;

    [Header("Arka plan kenar taşması")]
    [Tooltip("Barın arka planı yanlardan (x, bar genişliğine oran) ve alttan (y, bar yüksekliğine oran) ekran dışına taşar: " +
             "yan/alt çerçeve çizgileri hiçbir ekranda görünmez (kullanıcı 2026-11-22). Oran olduğu için görsel barla birlikte " +
             "orantılı ölçeklenir → yivler sekme butonlarıyla hizalı kalır. Görsel bu taşmayı içerecek şekilde çizilir: " +
             "bar bölgesi tuvalin (x, 0) noktasında; tuval = bar × (1 + 2x) genişlik, × (1 + y) yükseklik.")]
    [SerializeField] private Vector2 backgroundBleed = new Vector2(0.12f, 0.48f);
    [Tooltip("Dilimli arka planın çerçeve ölçeği (Image.pixelsPerUnitMultiplier). 1.4 → altın şerit ≈10 birim, PreLevel'deki " +
             "yeşil butonun sarı çerçevesiyle aynı kalınlık (kullanıcı 2026-11-22). Küçük = kalın çerçeve.")]
    [SerializeField, Min(0.1f)] private float backgroundFrameScale = 1.4f;
    [Tooltip("Sekme aralarındaki yiv (Resources/UI/BottomBarDivider). Bar genişliğinin %20/%40/%60/%80'ine konur.")]
    [SerializeField] private string dividerResource = "UI/BottomBarDivider";
    [SerializeField, Min(0f)] private float dividerTopInset = 20f;   // altın çerçevenin altından başlasın

    // Her tab'ın sahnedeki "seçili değil" hâli (default) — Awake'te yakalanır.
    private readonly List<Vector2> iconBasePos = new();
    private readonly List<Vector3> iconBaseScale = new();
    private readonly List<Vector3> labelBaseScale = new();

    private int currentIndex = -1;

    // Türkçe "Ana Menü" sekmeye sığmıyor: TR'de yalnız HOME sekmesinin yazısı birkaç punto küçülür (kullanıcı 2026-11-10;
    // diğer Türkçe sekme adları oturuyor).
    private const float TurkishLabelSizeDelta = 3f;
    private readonly List<TMP_Text> labelTexts = new();
    private readonly List<float> labelBaseSizes = new();

    private void Awake()
    {
        ApplyBackgroundBleed();
        for (int i = 0; i < tabs.Count; i++)
        {
            var t = tabs[i];

            iconBasePos.Add(t.icon != null ? t.icon.anchoredPosition : Vector2.zero);
            iconBaseScale.Add(t.icon != null ? t.icon.localScale : Vector3.one);
            labelBaseScale.Add(t.label != null ? t.label.localScale : Vector3.one);
            if (t.label != null && i == defaultIndex)
                foreach (var text in t.label.GetComponentsInChildren<TMP_Text>(true))
                {
                    labelTexts.Add(text);
                    labelBaseSizes.Add(text.fontSize);
                }

            int captured = i;
            if (t.button != null)
                t.button.onClick.AddListener(() => Select(captured));
        }
    }

    // Barın kendi Image'ı kapatılır; aynı sprite, bar rect'inin oranla genişletilmiş hâlinde çizilir.
    private void ApplyBackgroundBleed()
    {
        var own = GetComponent<Image>();
        if (own == null || !own.enabled || own.sprite == null) return;
        var barRt = (RectTransform)transform;

        var rt = (RectTransform)new GameObject("BleedBackground", typeof(RectTransform)) { layer = gameObject.layer }.transform;
        rt.SetParent(barRt, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = new Vector2(-backgroundBleed.x, -backgroundBleed.y);
        rt.anchorMax = new Vector2(1f + backgroundBleed.x, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = own.sprite;
        img.color = own.color;
        img.material = own.material;
        img.raycastTarget = own.raycastTarget;   // bar alanı dokunuşu eskisi gibi yutsun
        // Yivler artık ayrı → arka plan dilimli: köşe ve çerçeve kalınlığı sabit, yalnız orta uzar.
        img.type = own.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = backgroundFrameScale;
        own.enabled = false;

        BuildDividers(barRt, rt.GetSiblingIndex() + 1);
    }

    // Yivler sekmelerin arasına (sekmeler bar genişliğinin %10..%90'ında): arka planın hemen önünde, butonların arkasında.
    private static readonly float[] DividerAnchors = { 0.2f, 0.4f, 0.6f, 0.8f };
    private void BuildDividers(RectTransform barRt, int siblingIndex)
    {
        var sprite = Resources.Load<Sprite>(dividerResource);
        if (sprite == null) return;
        float width = sprite.rect.width / backgroundFrameScale;
        for (int i = 0; i < DividerAnchors.Length; i++)
        {
            var d = (RectTransform)new GameObject("Divider" + i, typeof(RectTransform)) { layer = gameObject.layer }.transform;
            d.SetParent(barRt, false);
            d.SetSiblingIndex(siblingIndex + i);
            d.anchorMin = new Vector2(DividerAnchors[i], 0f);
            d.anchorMax = new Vector2(DividerAnchors[i], 1f);
            d.pivot = new Vector2(0.5f, 1f);
            d.sizeDelta = new Vector2(width, -dividerTopInset);
            d.anchoredPosition = new Vector2(0f, -dividerTopInset);
            var dimg = d.gameObject.AddComponent<Image>();
            dimg.sprite = sprite;
            dimg.raycastTarget = false;
        }
    }

    private void OnEnable()
    {
        GameLocalization.OnLanguageChanged += ApplyLanguageLabelSize;
        WonderProgress.OnTaskCompleted += HandleWonderTask;
        ApplyLanguageLabelSize();
    }

    private void OnDisable()
    {
        GameLocalization.OnLanguageChanged -= ApplyLanguageLabelSize;
        WonderProgress.OnTaskCompleted -= HandleWonderTask;
    }

    private void ApplyLanguageLabelSize()
    {
        float delta = GameLocalization.CurrentLanguage == "tr" ? TurkishLabelSizeDelta : 0f;
        for (int i = 0; i < labelTexts.Count; i++)
            if (labelTexts[i] != null) labelTexts[i].fontSize = labelBaseSizes[i] - delta;
    }

    private GameObject worldTourGlobe;
    private WonderCatalog worldTourCatalog;

    private bool GlobeVisible => worldTourGlobe != null && currentIndex == defaultIndex
                                 && WorldTourGlobeButton.IsUnlocked(worldTourCatalog);

    // İlk görev adımı yapılınca dünya butonu hemen belirir (öğretici onu gösterir).
    private void HandleWonderTask(int _) { if (worldTourGlobe != null) worldTourGlobe.SetActive(GlobeVisible); }

    private void Start()
    {
        // Sağ alt dünya butonu (yalnız HOME'da); harita görseli yoksa kurulmaz.
        var rootCanvas = GetComponentInParent<Canvas>();
        var background = FindAnyObjectByType<WonderBackgroundView>(FindObjectsInactive.Include);
        var repairTutorial = FindAnyObjectByType<WorkshopRepairButtonTutorial>(FindObjectsInactive.Include);
        var repairButton = repairTutorial != null && repairTutorial.RepairButton != null
            ? (RectTransform)repairTutorial.RepairButton.transform : null;
        worldTourCatalog = background != null ? background.Catalog : null;
        if (rootCanvas != null && background != null)
            worldTourGlobe = WorldTourGlobeButton.Create((RectTransform)rootCanvas.rootCanvas.transform,
                (RectTransform)transform, repairButton, homeOnlyElements != null && homeOnlyElements.Length > 0 && homeOnlyElements[0] != null
                    ? homeOnlyElements[0].transform : null, background.Catalog);

        Select(Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, tabs.Count - 1)), force: true);

        // Başka sahneden "marketi aç" istendiyse (ör. 01_Game fail popup → Satın Al),
        // default sekmeden sonra market sekmesine geç.
        MarketNavigator.ConsumePendingIfAny(this);
    }

    public void Select(int index) => Select(index, force: false);

    public int CurrentIndex => currentIndex;

    /// Paneli (ya da çocukları) T bileşenini taşıyan sekme — öğreticiler hedef butonu sahne aramadan bulsun.
    public int FindTabIndex<T>() where T : Component
    {
        for (int i = 0; i < tabs.Count; i++)
            if (tabs[i]?.panel != null && tabs[i].panel.GetComponentInChildren<T>(true) != null) return i;
        return -1;
    }

    public Button TabButton(int index) => index >= 0 && index < tabs.Count ? tabs[index].button : null;

    /// Ana ekran (HOME) sekmesine geçer.
    public void SelectHome() => Select(defaultIndex, force: false);

    private void Select(int index, bool force)
    {
        if (index < 0 || index >= tabs.Count) return;
        if (!force && index == currentIndex) return;

        currentIndex = index;

        for (int i = 0; i < tabs.Count; i++)
        {
            bool selected = i == index;
            ApplyState(i, selected);
        }

        // HOME dışı sekmelerde main-menu overlay'lerini (RightEventPanel vb.) gizle.
        bool onHome = index == defaultIndex;
        if (homeOnlyElements != null)
            foreach (var go in homeOnlyElements)
                if (go != null) go.SetActive(onHome);
        if (worldTourGlobe != null) worldTourGlobe.SetActive(GlobeVisible);
    }

    private void ApplyState(int i, bool selected)
    {
        var t = tabs[i];

        if (t.icon != null)
        {
            t.icon.anchoredPosition = selected
                ? iconBasePos[i] + Vector2.up * iconLift
                : iconBasePos[i];
            t.icon.localScale = selected
                ? iconBaseScale[i] * iconScale
                : iconBaseScale[i];
        }

        if (t.label != null)
            t.label.localScale = selected
                ? labelBaseScale[i] * labelScale
                : labelBaseScale[i];

        if (t.selectedHighlight != null)
            t.selectedHighlight.SetActive(selected);

        if (t.panel != null)
            t.panel.SetActive(selected);
    }
}
