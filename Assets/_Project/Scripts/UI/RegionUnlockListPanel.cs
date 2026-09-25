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

    [Header("Wonder Mode (atanırsa region yerine harika görevleri)")]
    [SerializeField] private WonderCatalog wonderCatalog;
    [SerializeField] private WonderRevealOverlay wonderOverlay;
    [Tooltip("Bölüm kapısı kapalıyken satırda görünecek metnin lokalizasyon anahtarı ({0} = bölüm no).")]
    [SerializeField] private string chapterLockLocalizationKey = "wonder_task_locked_chapter";

    private bool WonderMode => wonderCatalog != null;

    [Header("Layout")]
    [Tooltip("Bölge modunda aynı anda gösterilen satır sayısı. Wonder modunda HER ZAMAN 1 satır gösterilir.")]
    [SerializeField, Min(1)] private int visibleSlotCount = 3;
    [SerializeField, Min(20f)] private float itemHeight = 160f;
    [SerializeField, Min(0f)]  private float itemSpacing = 12f;
    [SerializeField] private float firstSlotY = 0f;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float panelFadeDuration = 0.20f;

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
        if (!WonderMode && worldMap == null) { Debug.LogWarning("[RegionPanel] worldMap atanmamış."); return; }

        if (!inViewMode)
        {
            inViewMode = true;
            SetMainMenuElementsVisible(false);
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);
        if (panelGroup != null) panelGroup.alpha = 0f;

        RebuildItemsInstant();
        if (!WonderMode) worldMap.FocusNextLocked(instant: true);   // boş ekranda açma — sıradaki pine
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
        if (WonderMode)
        {
            StartCoroutine(WonderTaskFlow(WonderProgress.ActiveEventIndex(wonderCatalog)));
            return;
        }
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

        if (WonderMode) { RebuildWonderItems(); return; }

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

    // ─── Wonder Mode ────────────────────────────────────────────────────────────

    /// <summary>
    /// Wonder modu satırları: AÇIK event'lerin kalan görevleri, event sırasıyla, slot dolana
    /// kadar listelenir. Her event'in SIRADAKİ görevi aktif (tıklanabilir); aynı event'in
    /// ondan sonraki görevleri önizlemedir. Event kilidi BÖLÜM ile açılır — ilk event bitmese
    /// bile bölüm bitince sıradaki event'in görevleri listeye girer. Slot artarsa ve hâlâ
    /// kilitli event varsa en alta "Bölüm X bitince açılır" satırı konur.
    /// [[project_wonder_reveal_background]]
    /// </summary>
    private void RebuildWonderItems()
    {
        int unlocked = WonderProgress.UnlockedEventCount(wonderCatalog);

        var rows = new List<(int eventIndex, int stage, bool isEventNext)>();

        // 1) ÖNCE her açık & bitmemiş event'in SIRADAKİ görevi — hepsi tıklanabilir.
        //    Böylece yeni açılan event, öncekinin görevleri bitmemiş olsa da listeye girer.
        for (int e = 0; e < unlocked && rows.Count < visibleSlotCount; e++)
        {
            var we = wonderCatalog.Get(e);
            if (we == null) continue;
            if (WonderProgress.RemainingTasks(wonderCatalog, e) <= 0) continue;

            rows.Add((e, WonderProgress.StageOf(e), true));
        }

        // 2) Boş slot kaldıysa aynı event'lerin SONRAKİ görevleriyle doldur (önizleme).
        for (int e = 0; e < unlocked && rows.Count < visibleSlotCount; e++)
        {
            var we = wonderCatalog.Get(e);
            if (we == null) continue;

            for (int s = WonderProgress.StageOf(e) + 1; s < we.TaskCount && rows.Count < visibleSlotCount; s++)
                rows.Add((e, s, false));
        }

        // Görsel düzen: event'ler blok blok, her blokta görev sırası.
        rows.Sort((a, b) => a.eventIndex != b.eventIndex
            ? a.eventIndex.CompareTo(b.eventIndex)
            : a.stage.CompareTo(b.stage));

        bool showLockedTeaser = rows.Count < visibleSlotCount
                                && WonderProgress.HasLockedEvent(wonderCatalog);

        if (allCompletedMessage != null)
            allCompletedMessage.SetActive(rows.Count == 0 && !showLockedTeaser);

        // Birden fazla event listedeyse görev adının önüne event adını yaz (aynı görev
        // isimleri event'ler arasında tekrar ediyor → hangi event olduğu belli olsun).
        bool multiEvent = showLockedTeaser;
        for (int i = 1; i < rows.Count && !multiEvent; i++)
            multiEvent = rows[i].eventIndex != rows[0].eventIndex;

        int slot = 0;
        foreach (var row in rows)
        {
            var w = wonderCatalog.Get(row.eventIndex);
            var item = Instantiate(itemPrefab, itemsContainer);
            item.SetInstantY(SlotY(slot++));

            string title = RowTitle(w, w.GetTaskName(row.stage), multiEvent);

            if (w.HasExplicitTaskIcon(row.stage))
                item.BindTask(title, w.GetTaskIcon(row.stage), w.GetStarCost(row.stage),
                              this, isActive: row.isEventNext, wonderEventIndex: row.eventIndex);
            else
            {
                // İkon yok → harika imajının (stage+1)/toplam kadar kaynaklanmış mini önizlemesi
                float reveal = w.TaskCount > 0 ? (row.stage + 1f) / w.TaskCount : 1f;
                item.BindTaskRevealIcon(title, w.backgroundSprite, reveal, w.GetStarCost(row.stage),
                                        this, isActive: row.isEventNext, wonderEventIndex: row.eventIndex);
            }

            items.Add(item);
        }

        if (showLockedTeaser)
        {
            var locked = wonderCatalog.Get(unlocked);
            if (locked != null)
            {
                var item = Instantiate(itemPrefab, itemsContainer);
                item.SetInstantY(SlotY(slot));
                item.BindTaskRevealIcon(RowTitle(locked, locked.GetTaskName(0), multiEvent),
                                        locked.backgroundSprite, 0f, locked.GetStarCost(0),
                                        this, isActive: false, wonderEventIndex: unlocked);
                item.ApplyChapterLock(ChapterLockText());
                items.Add(item);
            }
        }

        if (progressBar != null) progressBar.ApplyInstant();
    }

    private static string RowTitle(WonderDefinition w, string taskName, bool prefixEventName)
    {
        if (!prefixEventName || w == null) return taskName;
        string eventName = !string.IsNullOrEmpty(w.displayName) ? w.displayName : w.wonderId;
        return string.IsNullOrEmpty(eventName) ? taskName : $"{eventName} · {taskName}";
    }

    /// <summary>"Bölüm X bitince açılır" — sıradaki event'in açılması için bitirilecek bölüm.</summary>
    private string ChapterLockText()
    {
        int chapter = WonderProgress.ChapterToFinishForNextEvent;
        string s = GameLocalization.GetFormat(chapterLockLocalizationKey, chapter);
        return (s == chapterLockLocalizationKey) ? $"Bölüm {chapter} bitince açılır" : s;
    }

    /// <summary>Bir satıra tıklandı: o satırın ait olduğu EVENT'in sıradaki görevi yapılır.</summary>
    public void OnWonderTaskClicked(int eventIndex)
    {
        if (isAnimating) return;
        StartCoroutine(WonderTaskFlow(eventIndex));
    }

    private IEnumerator WonderTaskFlow(int eventIndex)
    {
        if (eventIndex < 0) yield break;

        if (!WonderProgress.IsEventUnlocked(wonderCatalog, eventIndex))
        {
            Debug.Log($"[WonderPanel] Event {eventIndex} kilitli — bölüm " +
                      $"{WonderProgress.ChapterToFinishForNextEvent} bitmeli.");
            yield break;
        }

        if (!WonderProgress.CanAffordNextTask(wonderCatalog, eventIndex))
        {
            Debug.LogWarning($"[WonderPanel] Yetersiz yıldız. " +
                             $"cost={WonderProgress.NextTaskCost(wonderCatalog, eventIndex)}, " +
                             $"stars={PlayerWallet.TotalStars}");
            // TODO: shop/reklam yönlendirmesi
            yield break;
        }

        isAnimating = true;
        Vector3 starSource = ResolveStarSource(eventIndex);
        int fromStage = WonderProgress.StageOf(eventIndex);

        if (!WonderProgress.TrySpendForNextTask(wonderCatalog, eventIndex)) { isAnimating = false; yield break; }

        if (wonderOverlay != null)
            StartCoroutine(FlyStarsFromTo(starSource, (RectTransform)wonderOverlay.transform));
        yield return new WaitForSeconds(0.1f);

        yield return FadePanel(panelGroup != null ? panelGroup.alpha : 1f, 0f, panelFadeDuration);

        if (wonderOverlay != null)
            yield return wonderOverlay.PlayReveal(wonderCatalog, eventIndex, fromStage);

        if (postRevealPause > 0f) yield return new WaitForSeconds(postRevealPause);

        if (reopenPanelAfterUnlock && inViewMode)
        {
            RebuildItemsInstant();
            yield return FadePanel(0f, 1f, panelFadeDuration);
        }
        isAnimating = false;
    }

    /// <summary>Yıldızların uçacağı başlangıç noktası: tıklanan event'in satırı (yoksa panel).</summary>
    private Vector3 ResolveStarSource(int eventIndex)
    {
        foreach (var it in items)
            if (it != null && it.WonderEventIndex == eventIndex)
                return it.transform.position;
        return transform.position;
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
