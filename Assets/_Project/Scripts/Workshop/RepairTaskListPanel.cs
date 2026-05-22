using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Repair (Onar) butonundan açılan task list paneli.
/// Aynı anda sadece N (varsayılan 3) görevi gösterir.
/// Üstteki aktif, altındakiler preview/kilitli.
/// Aktif görev tamamlanınca: kaybolur, alttakiler yukarı kayar, en alta yeni bir tane gelir.
/// </summary>
public class RepairTaskListPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorkshopController workshop;
    [SerializeField] private RepairTaskItem itemPrefab;
    [SerializeField] private RectTransform itemsContainer;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundDismissButton; // boşluğa tıklayarak kapatmak için (opsiyonel)
    [Tooltip("Ek kapama butonları (Continue, Cancel vb.). Hepsi Close() çağırır.")]
    [SerializeField] private Button[] extraCloseButtons;
    [SerializeField] private GameObject allCompletedMessage;  // tüm görevler bittiğinde gösterilecek panel
    [SerializeField] private RepairProgressBar progressBar;   // üstteki ilerleme barı (opsiyonel)

    [Header("Layout")]
    [Tooltip("Aynı anda görünen task sayısı (genelde 3).")]
    [SerializeField, Min(1)] private int visibleSlotCount = 3;
    [Tooltip("Item RectTransform'unun yükseklik değeri (sizeDelta.y).")]
    [SerializeField, Min(20f)] private float itemHeight = 160f;
    [Tooltip("Itemler arası boşluk.")]
    [SerializeField, Min(0f)]  private float itemSpacing = 12f;
    [Tooltip("İlk slot'un (en üstteki) anchoredPosition.y değeri. " +
             "ItemsContainer'ın pivot/anchor ayarına göre 0 ya da başka bir değer olabilir.")]
    [SerializeField] private float firstSlotY = 0f;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float slideDuration     = 0.35f;
    [SerializeField, Min(0.05f)] private float completeDuration  = 0.25f;
    [SerializeField, Min(0.05f)] private float panelFadeDuration = 0.20f;
    [SerializeField, Min(0f)]    private float newItemEnterOffset = 100f;

    [Header("Star Fly Effect (Cinematic)")]
    [Tooltip("Tek bir yıldız UI prefab'ı (Image + star sprite). Spawn olup workshop'a uçacak.")]
    [SerializeField] private Image starUIPrefab;
    [Tooltip("Yıldızların hedef noktası — workshop'taki vfxParent veya CurrentImage'in ortası.")]
    [SerializeField] private RectTransform starFlyTarget;
    [SerializeField, Min(1)]    private int starFlyCount     = 6;
    [SerializeField, Min(0.1f)] private float starFlyDuration = 0.7f;
    [SerializeField, Min(0f)]   private float starFlyStagger  = 0.06f;

    [Header("Workshop View Mode")]
    [Tooltip("Popup açıkken (workshop görünümünde) gizlenecek MainMenu UI elementleri. " +
             "Örn: LevelButton, TopHud, vs. (Wallet/Star gösterimini görünür bırakmak istersen koyma.)")]
    [SerializeField] private GameObject[] elementsToHideWhileOpen;
    [Tooltip("ONAR sonrası workshop transition tamamlanınca paneli tekrar aç.")]
    [SerializeField] private bool reopenPanelAfterRepair = true;
    [SerializeField, Min(0f)] private float postTransitionPause = 0.3f;

    private readonly List<RepairTaskItem> items = new();
    private bool isAnimating;

    private float SlotY(int slotIndex) => firstSlotY - slotIndex * (itemHeight + itemSpacing);

    // ─── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backgroundDismissButton != null) backgroundDismissButton.onClick.AddListener(Close);

        if (extraCloseButtons != null)
        {
            for (int i = 0; i < extraCloseButtons.Length; i++)
            {
                if (extraCloseButtons[i] != null)
                    extraCloseButtons[i].onClick.AddListener(Close);
            }
        }

        // Not: Panel'in başlangıçta gizli olması için Inspector'da GameObject'i inactive yap.
        // Burada SetActive(false) çağırmıyoruz çünkü ilk Open() esnasında re-trigger eder.
    }

    // ─── Public API ─────────────────────────────────────────────────────────

    private bool inWorkshopMode;

    /// MainMenu RepairButton bunu çağırır. Workshop view mode'a girer:
    /// MainMenu UI gizlenir, popup açılır.
    public void Open()
    {
        if (workshop == null || workshop.StageData == null)
        {
            Debug.LogWarning("[RepairPanel] Workshop veya StageData atanmamış.");
            return;
        }

        if (!inWorkshopMode)
        {
            inWorkshopMode = true;
            SetMainMenuElementsVisible(false);
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);

        if (panelGroup != null) panelGroup.alpha = 0f;

        RebuildItemsInstant();
        if (progressBar != null) progressBar.ApplyInstant();
        StartCoroutine(FadePanel(0f, 1f, panelFadeDuration));
    }

    /// CloseButton / DimBackground bunu çağırır. Workshop view mode'dan çıkar:
    /// Popup kapanır, MainMenu UI geri gelir.
    public void Close()
    {
        if (!inWorkshopMode) return;
        StartCoroutine(ExitWorkshopModeRoutine());
    }

    private IEnumerator ExitWorkshopModeRoutine()
    {
        yield return FadePanel(panelGroup != null ? panelGroup.alpha : 1f, 0f, panelFadeDuration);
        if (panelRoot != null) panelRoot.SetActive(false);
        SetMainMenuElementsVisible(true);
        inWorkshopMode = false;
    }

    private void SetMainMenuElementsVisible(bool visible)
    {
        if (elementsToHideWhileOpen == null) return;
        for (int i = 0; i < elementsToHideWhileOpen.Length; i++)
        {
            var go = elementsToHideWhileOpen[i];
            if (go != null) go.SetActive(visible);
        }
    }

    /// Aktif (üstteki) task'a tıklanınca çağrılır. RepairTaskItem button'unun listener'ı.
    public void OnActiveTaskClicked()
    {
        Debug.Log($"[Cinematic] OnActiveTaskClicked. isAnimating={isAnimating}, workshop={(workshop != null)}, inWorkshopMode={inWorkshopMode}, items={items.Count}");

        if (isAnimating) { Debug.Log("[Cinematic] Skip: already animating"); return; }
        if (workshop == null) { Debug.LogWarning("[Cinematic] Skip: workshop null"); return; }

        // Tıklanan butonun world pozisyonunu yakala (yıldızlar buradan uçacak)
        Vector3 starSourceWorldPos = (items.Count > 0 && items[0] != null)
            ? items[0].transform.position
            : transform.position;

        Debug.Log($"[Cinematic] TryRepairCurrent: CurrentStage={workshop.CurrentStage}, Stars={PlayerWallet.TotalStars}");
        if (!workshop.TryRepairCurrent())
        {
            Debug.LogWarning("[Cinematic] TryRepairCurrent returned FALSE. (Yetersiz yıldız? Son task? Already transitioning?)");
            return;
        }
        Debug.Log("[Cinematic] TryRepairCurrent OK, starting cinematic flow.");

        // Cinematic akış: yıldızlar uç, panel kısa süreliğine fade out, workshop transition görünür,
        // sonra panel geri açılır. Workshop view mode'dan çıkmayız.
        StartCoroutine(CinematicRepairFlow(starSourceWorldPos));
    }

    private IEnumerator CinematicRepairFlow(Vector3 starSourceWorldPos)
    {
        isAnimating = true;

        // 1. Yıldızlar uç (paralel başlat)
        StartCoroutine(FlyStarsFromTo(starSourceWorldPos, starFlyTarget));

        // 2. Kısa gecikme — yıldızlar yola çıksın
        yield return new WaitForSeconds(0.1f);

        // 3. Panel fade out (gameObject hala aktif — workshop görünür kalır altta)
        yield return FadePanel(panelGroup != null ? panelGroup.alpha : 1f, 0f, panelFadeDuration);

        // 4. Workshop transition tamamlanana kadar bekle
        if (workshop != null)
            yield return new WaitUntil(() => !workshop.IsTransitioning);

        // 5. Kısa bir nefes — yeni state'i görsün
        if (postTransitionPause > 0f)
            yield return new WaitForSeconds(postTransitionPause);

        // 6. Panel tekrar aç (güncellenen task listesiyle)
        if (reopenPanelAfterRepair && inWorkshopMode)
        {
            RebuildItemsInstant();
            if (progressBar != null) progressBar.ApplyInstant();
            yield return FadePanel(0f, 1f, panelFadeDuration);
        }

        isAnimating = false;
    }

    /// Yıldızları kaynak noktadan hedefe uçur. Canvas içinde Image instance'ları yaratır.
    private IEnumerator FlyStarsFromTo(Vector3 sourceWorldPos, RectTransform target)
    {
        if (starUIPrefab == null || target == null)
            yield break;

        // Yıldızları Canvas'a parent'la ki panel kapanınca da görünür kalsınlar.
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) yield break;
        var canvasRoot = canvas.transform as RectTransform;

        Vector3 targetWorldPos = target.position;

        for (int i = 0; i < starFlyCount; i++)
        {
            StartCoroutine(FlyOneStar(sourceWorldPos, targetWorldPos, canvasRoot));
            if (starFlyStagger > 0f)
                yield return new WaitForSeconds(starFlyStagger);
        }
    }

    private IEnumerator FlyOneStar(Vector3 fromWorld, Vector3 toWorld, RectTransform parent)
    {
        var star = Instantiate(starUIPrefab, parent);
        var rt = star.rectTransform;
        rt.position = fromWorld;
        rt.localScale = Vector3.one * 1.5f;

        // Bezier control point (rastgele kavis için)
        Vector3 mid = (fromWorld + toWorld) * 0.5f;
        mid += new Vector3(
            Random.Range(-120f, 120f),
            Random.Range(40f, 180f),
            0f);

        var img = star.GetComponent<Image>();
        Color baseColor = img != null ? img.color : Color.white;

        float t = 0f;
        while (t < starFlyDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / starFlyDuration);
            float kEased = Mathf.SmoothStep(0f, 1f, k);

            // Quadratic bezier: from → mid → to
            Vector3 a = Vector3.Lerp(fromWorld, mid, kEased);
            Vector3 b = Vector3.Lerp(mid, toWorld, kEased);
            rt.position = Vector3.Lerp(a, b, kEased);

            // Scale: hafifçe küçül
            rt.localScale = Vector3.one * Mathf.Lerp(1.5f, 0.6f, k);

            // Hafif alpha fade son %20'de
            if (img != null)
            {
                float a01 = (k > 0.8f) ? Mathf.Lerp(1f, 0f, (k - 0.8f) / 0.2f) : 1f;
                var c = baseColor; c.a = a01; img.color = c;
            }

            yield return null;
        }

        Destroy(star.gameObject);
    }

    // ─── Build / Rebuild ────────────────────────────────────────────────────

    private void RebuildItemsInstant()
    {
        foreach (var it in items) if (it != null) Destroy(it.gameObject);
        items.Clear();

        var stages = workshop.StageData.stages;
        int currentStage = workshop.CurrentStage;

        if (allCompletedMessage != null)
            allCompletedMessage.SetActive(currentStage >= stages.Count);

        // stages[0] initial state'tir; gerçek tasklar stages[1..N-1].
        // Aktif task = stages[currentStage + 1].
        for (int slot = 0; slot < visibleSlotCount; slot++)
        {
            int stageIndex = currentStage + 1 + slot;
            if (stageIndex >= stages.Count) break;

            var item = Instantiate(itemPrefab, itemsContainer);
            item.SetInstantY(SlotY(slot));
            item.Bind(stageIndex, stages[stageIndex], this, isActive: slot == 0);
            items.Add(item);
        }
    }

    // ─── Animation ──────────────────────────────────────────────────────────

    private IEnumerator AnimateShift()
    {
        isAnimating = true;

        // 1. En üstteki görevi tamamlanma animasyonu ile yok et.
        if (items.Count > 0 && items[0] != null)
        {
            yield return items[0].PlayCompleteAnimation(completeDuration);
            Destroy(items[0].gameObject);
            items.RemoveAt(0);
        }

        // 2. Kalan itemleri bir slot yukarı kaydır (paralel).
        var slides = new List<Coroutine>();
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;
            slides.Add(StartCoroutine(items[i].SlideToY(SlotY(i), slideDuration)));
        }
        foreach (var co in slides) yield return co;

        // 3. Aktif state'leri yenile (yeni 0. slot artık aktif).
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                items[i].RefreshActiveState(i == 0);
        }

        // 4. En alta yeni görev ekle (varsa).
        var stages = workshop.StageData.stages;
        int nextNewStageIndex = workshop.CurrentStage + 1 + items.Count;
        if (nextNewStageIndex < stages.Count && items.Count < visibleSlotCount)
        {
            var newItem = Instantiate(itemPrefab, itemsContainer);
            newItem.Bind(nextNewStageIndex, stages[nextNewStageIndex], this, isActive: false);

            int targetSlot = items.Count;
            items.Add(newItem);

            yield return newItem.SlideInFromBelow(SlotY(targetSlot), newItemEnterOffset, slideDuration);
        }

        // 5. Tüm görevler bittiyse bilgilendirme paneli.
        if (allCompletedMessage != null && items.Count == 0)
            allCompletedMessage.SetActive(true);

        isAnimating = false;
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
