using System;
using System.Collections.Generic;
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

    // Her tab'ın sahnedeki "seçili değil" hâli (default) — Awake'te yakalanır.
    private readonly List<Vector2> iconBasePos = new();
    private readonly List<Vector3> iconBaseScale = new();
    private readonly List<Vector3> labelBaseScale = new();

    private int currentIndex = -1;

    private void Awake()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            var t = tabs[i];

            iconBasePos.Add(t.icon != null ? t.icon.anchoredPosition : Vector2.zero);
            iconBaseScale.Add(t.icon != null ? t.icon.localScale : Vector3.one);
            labelBaseScale.Add(t.label != null ? t.label.localScale : Vector3.one);

            int captured = i;
            if (t.button != null)
                t.button.onClick.AddListener(() => Select(captured));
        }
    }

    private void Start()
    {
        Select(Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, tabs.Count - 1)), force: true);

        // Başka sahneden "marketi aç" istendiyse (ör. 01_Game fail popup → Satın Al),
        // default sekmeden sonra market sekmesine geç.
        MarketNavigator.ConsumePendingIfAny(this);
    }

    public void Select(int index) => Select(index, force: false);

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
