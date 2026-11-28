using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [Tooltip("Bölüm kapısı kapalıyken satırda görünecek metnin lokalizasyon anahtarı ({0} = bitirilecek SEVİYE no).")]
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

    [Header("Harika Başlıkları")]
    // Görev listesinde her harika bloğunun üstünde ara başlık (kullanıcı 2026-11-15: "Harika · Görev" satırı
    // yazıyı küçültüp sığmıyordu; Market'teki gibi ara başlık).
    [SerializeField, Min(20f)] private float headerHeight = 58f;
    [SerializeField, Min(10f)] private float headerFontSize = 46f;
    [Tooltip("Başlıklı listede satır arası (başlıklar yer kapladığı için normalden sıkı).")]
    [SerializeField, Min(0f)] private float headerItemSpacing = 10f;
    private readonly List<GameObject> headers = new();

    [Header("Yıldız Bakiyesi (panel açıkken sağ üst)")]
    // Panel açılınca üst bar gizleniyor; oyuncu kaç yıldızı olduğunu görsün (kullanıcı 2026-11-22, level sonu
    // altın kutusuyla aynı görünüm: ikon kutunun soluna biner, kutu sayıya göre uzar).
    [SerializeField] private Sprite starChipBackground;
    [SerializeField] private Sprite starChipIcon;
    [SerializeField] private TMP_FontAsset starChipFont;
    [SerializeField] private Material starChipMaterial;
    [SerializeField] private Vector2 starChipPosition = new Vector2(-40f, -170f);   // panel kökünün sağ üstüne göre
    [SerializeField, Min(1f)] private float starChipHeight = 76f;
    [SerializeField, Min(1f)] private float starChipIconSize = 96f;
    [SerializeField, Min(0f)] private float starChipIconOverlap = 34f;
    private RectTransform starChip;
    private RectTransform starChipBox;
    private TextMeshProUGUI starChipText;
    private bool starChipSubscribed;

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
        var active = WonderMode ? WonderProgress.ActiveWonder(wonderCatalog) : null;
        if (active != null && ProgressiveContent.GateEvent(this, "mission-" + active.wonderId, Open)) return;
        if (!WonderMode && ProgressiveContent.GateEvent(this, EventContentPolicy.MissionMap, Open)) return;
        OpenReady();
    }

    private void OpenReady()
    {
        if (!WonderMode && worldMap == null) { if (GameDiagnostics.CriticalErrors)
            Debug.LogWarning("[RegionPanel] worldMap atanmamış."); return; }

        if (!inViewMode)
        {
            inViewMode = true;
            SetMainMenuElementsVisible(false);
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);
        if (panelGroup != null) panelGroup.alpha = 0f;

        RebuildItemsInstant();
        ShowStarChip(true);
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
        ShowStarChip(false);
        SetMainMenuElementsVisible(true);
        inViewMode = false;
    }

    private void OnDisable() { ShowStarChip(false); ProgressiveContent.CancelEvent(this); }

    private void ShowStarChip(bool visible)
    {
        if (visible && starChip == null) BuildStarChip();
        if (starChip == null) return;
        starChip.gameObject.SetActive(visible);
        if (visible && !starChipSubscribed)
        {
            PlayerWallet.OnTotalStarsChanged += RefreshStarChip;
            starChipSubscribed = true;
        }
        else if (!visible && starChipSubscribed)
        {
            PlayerWallet.OnTotalStarsChanged -= RefreshStarChip;
            starChipSubscribed = false;
        }
        if (visible)
        {
            starChip.SetAsLastSibling();
            RefreshStarChip(PlayerWallet.TotalStars);
        }
    }

    private void BuildStarChip()
    {
        var parent = panelRoot != null ? panelRoot.transform : transform;
        starChip = NewChipRect("StarBalance", parent);
        starChip.anchorMin = starChip.anchorMax = new Vector2(1f, 1f);
        starChip.pivot = new Vector2(1f, 0.5f);
        starChip.anchoredPosition = starChipPosition;

        starChipBox = NewChipRect("Box", starChip);
        var box = starChipBox.gameObject.AddComponent<Image>();
        box.sprite = starChipBackground;
        box.enabled = starChipBackground != null;
        box.raycastTarget = false;
        box.type = starChipBackground != null && starChipBackground.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        starChipBox.anchorMin = starChipBox.anchorMax = starChipBox.pivot = new Vector2(1f, 0.5f);

        var textRt = NewChipRect("Amount", starChipBox);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(starChipIconOverlap + 8f, 8f);
        textRt.offsetMax = new Vector2(-20f, -8f);
        starChipText = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        if (starChipFont != null) starChipText.font = starChipFont;
        if (starChipMaterial != null) starChipText.fontSharedMaterial = starChipMaterial;
        starChipText.alignment = TextAlignmentOptions.Center;
        starChipText.enableAutoSizing = true;
        starChipText.fontSizeMax = 46f;
        starChipText.fontSizeMin = 22f;
        starChipText.fontStyle = FontStyles.Bold;
        starChipText.textWrappingMode = TextWrappingModes.NoWrap;
        starChipText.color = Color.white;
        starChipText.raycastTarget = false;

        var iconRt = NewChipRect("StarIcon", starChip);
        var icon = iconRt.gameObject.AddComponent<Image>();
        icon.sprite = starChipIcon;
        icon.enabled = starChipIcon != null;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.sizeDelta = Vector2.one * starChipIconSize;
    }

    private void RefreshStarChip(int amount)
    {
        if (starChipText == null) return;
        starChipText.text = amount.ToString();
        // Kutu sayının uzunluğuna göre uzar; ikon kutunun soluna biner.
        float h = starChipHeight;
        float minW = starChipBackground != null && starChipBackground.rect.height > 0f
            ? h * starChipBackground.rect.width / starChipBackground.rect.height : h * 2.4f;
        float w = Mathf.Max(minW, starChipIconOverlap + 8f + 20f
            + starChipText.GetPreferredValues(starChipText.text, float.PositiveInfinity, h).x);
        starChipBox.sizeDelta = new Vector2(w, h);
        starChipBox.anchoredPosition = Vector2.zero;
        starChip.sizeDelta = new Vector2(w + starChipIconSize - starChipIconOverlap, Mathf.Max(h, starChipIconSize));
    }

    private static RectTransform NewChipRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
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
            if (GameDiagnostics.CriticalErrors)
                Debug.LogWarning($"[RegionPanel] Açılamadı (yetersiz yıldız? zaten açık? reveal sürüyor?). " + $"cost={region.StarCost}, stars={PlayerWallet.TotalStars}");
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
        foreach (var h in headers) if (h != null) Destroy(h);
        headers.Clear();

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

        // Her harika bloğunun üstünde ara başlık; satırda yalnız görev adı (tam boy yazı).
        // Satırlar MERKEZDEN konumlanır (prefab pivot 0.5). Başlıklar yer kapladığı için toplam yükseklik
        // eski 3 satırlık alanı aşarsa liste bütünüyle küçültülür → çerçeveden taşmaz.
        // Satırlar TAM boy kalır (kullanıcı: küçülünce çirkin). Alan yetmezse önce önizleme satırları düşer;
        // her harikanın sıradaki görevi ve kilitli satır her zaman kalır. O da yetmezse son çare küçültme.
        float available = visibleSlotCount * itemHeight + Mathf.Max(0, visibleSlotCount - 1) * itemSpacing
                          + itemSpacing;   // satır arası bandın altına/üstüne pay
        float Needed(List<(int eventIndex, int stage, bool isEventNext)> list, bool teaser)
        {
            int hc = 0, rc = list.Count, pe = -1;
            foreach (var r in list) if (r.eventIndex != pe) { hc++; pe = r.eventIndex; }
            if (teaser) { hc++; rc++; }
            return rc * itemHeight + hc * headerHeight + Mathf.Max(0, rc + hc - 1) * headerItemSpacing;
        }
        var kept = new List<(int eventIndex, int stage, bool isEventNext)>();
        foreach (var r in rows) if (r.isEventNext) kept.Add(r);
        // Kilitli harika satırı da yalnız sığarsa (yoksa 2 görev kalır, satırlar küçülmez).
        if (showLockedTeaser && Needed(kept, true) > available) showLockedTeaser = false;
        foreach (var r in rows)
        {
            if (r.isEventNext) continue;
            kept.Add(r);
            kept.Sort((a, b) => a.eventIndex != b.eventIndex ? a.eventIndex.CompareTo(b.eventIndex) : a.stage.CompareTo(b.stage));
            if (Needed(kept, showLockedTeaser) > available) kept.Remove(r);
        }
        kept.Sort((a, b) => a.eventIndex != b.eventIndex ? a.eventIndex.CompareTo(b.eventIndex) : a.stage.CompareTo(b.stage));
        rows = kept;
        float needed = Needed(rows, showLockedTeaser);
        float scale = needed > available && needed > 0f ? available / needed : 1f;
        float top = firstSlotY + itemHeight * 0.5f;   // ilk slotun üst kenarı
        int lastEvent = -1;
        foreach (var row in rows)
        {
            var w = wonderCatalog.Get(row.eventIndex);
            if (row.eventIndex != lastEvent)
            {
                lastEvent = row.eventIndex;
                AddHeader(w.LocalizedName, ref top, scale);
            }
            var item = Instantiate(itemPrefab, itemsContainer);
            item.transform.localScale = Vector3.one * scale;
            item.SetInstantY(top - itemHeight * scale * 0.5f);
            top -= (itemHeight + headerItemSpacing) * scale;

            string title = w.GetTaskName(row.stage);
            int cost = wonderCatalog.GetStarCost(row.eventIndex, row.stage);
            bool affordable = PlayerWallet.HasEnoughStars(cost);   // yetmiyorsa sıradaki görevin butonu pasif görünür

            if (w.HasExplicitTaskIcon(row.stage))
                item.BindTask(title, w.GetTaskIcon(row.stage), cost,
                              this, isActive: row.isEventNext, wonderEventIndex: row.eventIndex, affordable: affordable);
            else
            {
                // İkon yok → harika imajının (stage+1)/toplam kadar kaynaklanmış mini önizlemesi
                float reveal = w.TaskCount > 0 ? (row.stage + 1f) / w.TaskCount : 1f;
                item.BindTaskRevealIcon(title, w.backgroundSprite, reveal, cost,
                                        this, isActive: row.isEventNext, wonderEventIndex: row.eventIndex, affordable: affordable);
            }

            items.Add(item);
        }

        if (showLockedTeaser)
        {
            var locked = wonderCatalog.Get(unlocked);
            if (locked != null)
            {
                AddHeader(locked.LocalizedName, ref top, scale);
                var item = Instantiate(itemPrefab, itemsContainer);
                item.transform.localScale = Vector3.one * scale;
                item.SetInstantY(top - itemHeight * scale * 0.5f);
                item.BindTaskRevealIcon(locked.GetTaskName(0),
                                        locked.backgroundSprite, 0f, wonderCatalog.GetStarCost(unlocked, 0),
                                        this, isActive: false, wonderEventIndex: unlocked);
                item.ApplyChapterLock(ChapterLockText());
                items.Add(item);
            }
        }

        if (progressBar != null) progressBar.ApplyInstant();
    }

    /// Harika bloğu ara başlığı: büyük beyaz yazı, bordo kontur (kullanıcı: bant gerekmez).
    /// Liste her görevde yeniden kurulur; başlıklar satırlarla birlikte silinir.
    private void AddHeader(string text, ref float top, float scale)
    {
        var src = itemPrefab != null ? (RectTransform)itemPrefab.transform : null;
        var go = new GameObject("WonderHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
        { layer = itemsContainer.gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(itemsContainer, false);
        if (src != null)
        {
            rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax;
            rt.sizeDelta = new Vector2(src.sizeDelta.x, headerHeight);
        }
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one * scale;
        rt.anchoredPosition = new Vector2(0f, top - headerHeight * scale * 0.5f);   // merkez = üst kenar - yarım
        var tmp = go.GetComponent<TextMeshProUGUI>();
        var style = itemPrefab != null ? itemPrefab.NameText : null;
        if (style != null) { tmp.font = style.font; tmp.fontStyle = style.fontStyle; }
        tmp.text = text;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = headerFontSize;
        tmp.fontSizeMin = headerFontSize * 0.6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        TextSurfaceStyle.Apply(tmp, TextSurface.Maroon);   // beyaz yüz + bordo kontur (yazı kuralı)
        headers.Add(go);
        top -= (headerHeight + headerItemSpacing) * scale;
    }

    /// <summary>"30. seviye bitince açılır" — kilit kuralı bölüm bazlı, ama oyuncu bölüm numarasını hiçbir
    /// yerde görmediği için metin, o bölümün SON seviyesini söyler (bitince sonraki bölüme geçilir).</summary>
    private string ChapterLockText()
    {
        int level = ChapterProgress.LastLevelOfChapter(WonderProgress.ChapterToFinishForNextEvent);
        string s = GameLocalization.GetFormat(chapterLockLocalizationKey, level);
        return (s == chapterLockLocalizationKey) ? $"{level}. seviye bitince açılır" : s;
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
            yield break;
        }

        if (!WonderProgress.CanAffordNextTask(wonderCatalog, eventIndex))
        {
            if (GameDiagnostics.CriticalErrors)
                Debug.LogWarning($"[WonderPanel] Yetersiz yıldız. " + $"cost={WonderProgress.NextTaskCost(wonderCatalog, eventIndex)}, " + $"stars={PlayerWallet.TotalStars}");
            // TODO: shop/reklam yönlendirmesi
            yield break;
        }

        var selected = wonderCatalog.Get(eventIndex);
        if (selected == null || ProgressiveContent.GateEvent(this, "mission-" + selected.wonderId,
            () => OnWonderTaskClicked(eventIndex), () => !isAnimating && inViewMode)) yield break;
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
