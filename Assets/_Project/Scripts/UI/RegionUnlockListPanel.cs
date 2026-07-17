using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bölge açma görev listesi paneli. Aynı anda N (varsayılan 3) açılabilir bölge gösterir.
/// Üstteki aktif (tıklanabilir), altındakiler preview. Aktif bölge açılınca: yıldızlar
/// uçar, sis kalkar, satır kaybolur, alttakiler yukarı kayar, en alta yeni bölge gelir.
///
/// (Workshop/RepairTaskListPanel'den uyarlandı — stage transition yerine bölge sisi kalkar.)
/// </summary>
public sealed class RegionUnlockListPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldMapController worldMap;
    [SerializeField] private RegionUnlockItem itemPrefab;
    [SerializeField] private RectTransform itemsContainer;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundDismissButton;
    [SerializeField] private Button[] extraCloseButtons;
    [Tooltip("Tüm bölgeler açıldığında gösterilecek mesaj/panel.")]
    [SerializeField] private GameObject allCompletedMessage;
    [Tooltip("Üstteki ilerleme barı (opsiyonel).")]
    [SerializeField] private RegionProgressBar progressBar;

    [Header("Layout")]
    [SerializeField, Min(1)] private int visibleSlotCount = 3;
    [SerializeField, Min(20f)] private float itemHeight = 160f;
    [SerializeField, Min(0f)]  private float itemSpacing = 12f;
    [SerializeField] private float firstSlotY = 0f;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float slideDuration     = 0.35f;
    [SerializeField, Min(0.05f)] private float completeDuration  = 0.25f;
    [SerializeField, Min(0.05f)] private float panelFadeDuration = 0.20f;
    [SerializeField, Min(0f)]    private float newItemEnterOffset = 100f;

    [Header("Star Fly Effect (Cinematic)")]
    [Tooltip("Tek bir yıldız UI prefab'ı (Image + star sprite). Satırdan bölgeye uçar.")]
    [SerializeField] private Image starUIPrefab;
    [SerializeField, Min(1)]    private int starFlyCount     = 6;
    [SerializeField, Min(0.1f)] private float starFlyDuration = 0.7f;
    [SerializeField, Min(0f)]   private float starFlyStagger  = 0.06f;

    [Header("View Mode")]
    [Tooltip("Panel açıkken gizlenecek MainMenu elementleri (opsiyonel).")]
    [SerializeField] private GameObject[] elementsToHideWhileOpen;
    [Tooltip("Açma sonrası paneli tekrar göster.")]
    [SerializeField] private bool reopenPanelAfterUnlock = true;
    [SerializeField, Min(0f)] private float postRevealPause = 0.3f;

    private readonly List<RegionUnlockItem> items = new();
    private bool isAnimating;
    private bool inViewMode;

    private float SlotY(int slotIndex) => firstSlotY - slotIndex * (itemHeight + itemSpacing);

    // ─── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backgroundDismissButton != null) backgroundDismissButton.onClick.AddListener(Close);
        if (extraCloseButtons != null)
            foreach (var b in extraCloseButtons)
                if (b != null) b.onClick.AddListener(Close);
    }

    // ─── Public API ─────────────────────────────────────────────────────────

    public void Open()
    {
        if (worldMap == null) { Debug.LogWarning("[RegionPanel] worldMap atanmamış."); return; }

        if (!inViewMode)
        {
            inViewMode = true;
            SetMainMenuElementsVisible(false);
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);
        if (panelGroup != null) panelGroup.alpha = 0f;

        RebuildItemsInstant();
        worldMap.FocusNextLocked(instant: true);   // boş ekranda açma — sıradaki pine konumlan
        StartCoroutine(FadePanel(0f, 1f, panelFadeDuration));
    }

    public void Close()
    {
        if (!inViewMode) return;
        StartCoroutine(ExitViewModeRoutine());
    }

    private IEnumerator ExitViewModeRoutine()
    {
        yield return FadePanel(panelGroup != null ? panelGroup.alpha : 1f, 0f, panelFadeDuration);
        if (panelRoot != null) panelRoot.SetActive(false);
        SetMainMenuElementsVisible(true);
        inViewMode = false;
    }

    private void SetMainMenuElementsVisible(bool visible)
    {
        if (elementsToHideWhileOpen == null) return;
        foreach (var go in elementsToHideWhileOpen)
            if (go != null) go.SetActive(visible);
    }

    /// Aktif (üstteki) satıra tıklanınca çağrılır.
    public void OnActiveItemClicked()
    {
        if (isAnimating) return;
        if (worldMap == null || items.Count == 0 || items[0] == null) return;

        var region = items[0].Region;
        Vector3 starSourceWorldPos = items[0].transform.position;

        if (!worldMap.TryUnlock(region))
        {
            Debug.LogWarning($"[RegionPanel] Açılamadı (yetersiz yıldız? zaten açık? reveal sürüyor?). " +
                             $"cost={region.StarCost}, stars={PlayerWallet.TotalStars}");
            // TODO: yetersiz yıldız → shop/reklam yönlendirmesi
            return;
        }

        StartCoroutine(CinematicUnlockFlow(starSourceWorldPos, region));
    }

    private IEnumerator CinematicUnlockFlow(Vector3 starSourceWorldPos, WorldMapRegion region)
    {
        isAnimating = true;

        // 1. Yıldızlar bölgenin PIN'ine uçar (bölgeye özel nokta; kamera odağı ada bazlı).
        StartCoroutine(FlyStarsFromTo(starSourceWorldPos, region.StarPoint));

        // 2. Kısa gecikme — yıldızlar yola çıksın.
        yield return new WaitForSeconds(0.1f);

        // 3. Paneli soldur (haritada sis kalkışı görünsün).
        yield return FadePanel(panelGroup != null ? panelGroup.alpha : 1f, 0f, panelFadeDuration);

        // 4. Sis kalkma sekansı bitene kadar bekle.
        yield return new WaitUntil(() => !worldMap.IsRevealing);

        if (postRevealPause > 0f) yield return new WaitForSeconds(postRevealPause);

        // 5. Paneli güncel listeyle tekrar aç.
        if (reopenPanelAfterUnlock && inViewMode)
        {
            RebuildItemsInstant();
            yield return FadePanel(0f, 1f, panelFadeDuration);
        }

        isAnimating = false;
    }

    // ─── Star Fly ─────────────────────────────────────────────────────────────

    private IEnumerator FlyStarsFromTo(Vector3 sourceWorldPos, RectTransform target)
    {
        if (starUIPrefab == null || target == null) yield break;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) yield break;
        var canvasRoot = canvas.transform as RectTransform;

        Vector3 targetWorldPos = target.position;
        for (int i = 0; i < starFlyCount; i++)
        {
            StartCoroutine(FlyOneStar(sourceWorldPos, targetWorldPos, canvasRoot));
            if (starFlyStagger > 0f) yield return new WaitForSeconds(starFlyStagger);
        }
    }

    private IEnumerator FlyOneStar(Vector3 fromWorld, Vector3 toWorld, RectTransform parent)
    {
        var star = Instantiate(starUIPrefab, parent);
        var rt = star.rectTransform;
        rt.position = fromWorld;
        rt.localScale = Vector3.one * 1.5f;

        Vector3 mid = (fromWorld + toWorld) * 0.5f;
        mid += new Vector3(Random.Range(-120f, 120f), Random.Range(40f, 180f), 0f);

        var img = star.GetComponent<Image>();
        Color baseColor = img != null ? img.color : Color.white;

        float t = 0f;
        while (t < starFlyDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / starFlyDuration);
            float kEased = Mathf.SmoothStep(0f, 1f, k);

            Vector3 a = Vector3.Lerp(fromWorld, mid, kEased);
            Vector3 b = Vector3.Lerp(mid, toWorld, kEased);
            rt.position = Vector3.Lerp(a, b, kEased);
            rt.localScale = Vector3.one * Mathf.Lerp(1.5f, 0.6f, k);

            if (img != null)
            {
                float a01 = (k > 0.8f) ? Mathf.Lerp(1f, 0f, (k - 0.8f) / 0.2f) : 1f;
                var c = baseColor; c.a = a01; img.color = c;
            }
            yield return null;
        }
        Destroy(star.gameObject);
    }

    // ─── Build ────────────────────────────────────────────────────────────────

    private void RebuildItemsInstant()
    {
        foreach (var it in items) if (it != null) Destroy(it.gameObject);
        items.Clear();

        var locked = worldMap.GetLockedRegions(visibleSlotCount);

        if (allCompletedMessage != null)
            allCompletedMessage.SetActive(locked.Count == 0);

        for (int slot = 0; slot < locked.Count; slot++)
        {
            var item = Instantiate(itemPrefab, itemsContainer);
            item.SetInstantY(SlotY(slot));
            item.Bind(locked[slot], this, isActive: slot == 0);
            items.Add(item);
        }

        if (progressBar != null) progressBar.ApplyInstant();
    }

    private IEnumerator FadePanel(float from, float to, float dur)
    {
        if (panelGroup == null) yield break;
        panelGroup.alpha = from;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        panelGroup.alpha = to;
    }
}
