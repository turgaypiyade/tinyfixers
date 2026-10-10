using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Bostan Hasadı kazı ekranı. Hiyerarşi editör kurulumundan gelir (TinyFixers ▸ Mockup ▸ Harvest Event).
/// Board = sahne arka planı, boyutu ARKA PLAN PİKSELİ; ekrana genişliğe sığdırılır (yatağın sol köşesi kenarda
/// olduğu için kırpılmaz), boşluk gök/çimen dolgusuyla kapanır. Izgara yatağın 4 köşesinden bilinear kurulur;
/// dokunuş ters-bilinear ile hücreye çevrilir. Kareler/ürün görselleri havuzdan (sabit sayıda) yeniden kullanılır.
/// </summary>
public sealed class HarvestScreen : MonoBehaviour, IPointerClickHandler
{
    [Header("Kök")]
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform board;
    [SerializeField] private RectTransform tilesLayer;
    [SerializeField] private RectTransform cropsLayer;
    [Tooltip("Toplanan ürünlerin konduğu tezgâh noktaları (Board içinde, sürüklenebilir).")]
    [SerializeField] private RectTransform[] standSlots;

    [Header("Karakter")]
    [SerializeField] private Image bear;

    [Header("HUD")]
    [Tooltip("Üst tabela kökü (açılışta yukarıdan düşer). Boşsa \"TopHud\" adlı çocuk aranır.")]
    [SerializeField] private RectTransform topHud;
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Tabelanın 2. satırı (\"Hasadı\"). Boşsa başlık tek satır harvest_title.")]
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text floorText;
    [Tooltip("Sezon bitişine kalan süre (tabela altındaki hap).")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text trowelText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button closeButton;
    [Tooltip("\"?\" — Nasıl oynanır overlay'ini açar.")]
    [SerializeField] private Button helpButton;

    [Header("Animasyon")]
    [SerializeField, Min(0.05f)] private float digDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float cropPopDuration = 0.35f;
    [SerializeField, Min(0.05f)] private float cropFlyDuration = 0.45f;
    [SerializeField, Min(0f)] private float cheerHold = 0.9f;

    [Header("Açılış (daire geçişi + sahne kurulumu)")]
    [Tooltip("Daire açılırken sahne kurulumu bu kadar sonra başlar (dairenin sonuna doğru).")]
    [SerializeField, Min(0f)] private float introDelay = 0.16f;
    [SerializeField, Min(1f)] private float introBoardScale = 1.06f;
    [Tooltip("Arkadan öne dalga: köşegen başına gecikme.")]
    [SerializeField, Min(0f)] private float introTileStagger = 0.025f;

    private HarvestConfig cfg;
    private Image[] tiles;
    private Image[] peeks;                       // kazılmış ama ürünü henüz tamamlanmamış hücrede "uç" görünür
    private Image[] glows;                       // ipucu ışıltısı: yarım ürünün kalan (kazılmamış) kareleri
    private int hintMask;
    private const float PeekFill = 0.4f;         // ucun görünen kısmı (ürünün üst %40'ı)
    private const int PileSize = 3;              // tezgâhta her ürün küçük bir yığın
    private readonly List<Image> cropImages = new();
    private readonly List<RectTransform> standPiles = new();
    private bool busy;
    private bool built;
    private EventScreenIris iris;
    private RectTransform origin;                // açılışın çıktığı ikon; kapanışta daire buraya döner
    private float boardIntroScale = 1f;          // FitBoard ölçeğinin çarpanı (açılışta 1.06 → 1)
    private Vector2 bearHome, hudHome;
    private long shownTimerSecond = long.MinValue;

    public bool IsOpen => root != null && root.activeInHierarchy;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (playButton != null) playButton.onClick.AddListener(OnPlay);
        if (helpButton != null) helpButton.onClick.AddListener(ShowHowToPlay);
        if (root != null && root != gameObject) root.SetActive(false);
        if (topHud == null) topHud = transform.Find("TopHud") as RectTransform;   // eski kurulum (iris öncesi)
        if (bear != null) bearHome = bear.rectTransform.anchoredPosition;
        if (topHud != null) hudHome = topHud.anchoredPosition;
    }

    private void OnEnable()  => HarvestState.OnChanged += RefreshHud;
    private void OnDisable() => HarvestState.OnChanged -= RefreshHud;

    public void Open() => Open(null);

    /// from: tıklanan ikon — ekran o noktadan büyüyen daireyle açılır (null → ekran ortası).
    public void Open(RectTransform from)
    {
        cfg = HarvestConfig.Shared;
        if (cfg == null) { Debug.LogWarning("[Harvest] HarvestConfig yok (Resources/Events)."); return; }
        HarvestState.SyncCycle(cfg, System.DateTime.UtcNow);
        gameObject.SetActive(true);
        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        if (transform.parent != null) transform.parent.SetAsLastSibling();
        StopAllCoroutines();
        busy = false;
        origin = from;
        BuildGrid();
        RebuildFloorVisuals();
        SetBear(cfg.bearIdle);
        RefreshHud();

        if (iris == null && root != null) iris = EventScreenIris.For((RectTransform)root.transform);
        busy = true;                             // açılış bitene kadar kazı/kapat yok
        iris?.PlayOpen(origin);
        EventSfx.Play(l => l.arrowWhoosh);
        StartCoroutine(Intro());
    }

    // Sahne kendini kurar: board hafif yakından oturur, tabela düşer, kareler arkadan öne filizlenir,
    // ayı zıplayarak girer, tezgâh yığınları en son pop. Tek döngü; kare başı ayırma yok.
    private IEnumerator Intro()
    {
        const float boardDur = 0.5f, hudDelay = 0.1f, hudDur = 0.4f, tileDelay = 0.12f, tileDur = 0.22f;
        const float bearDelay = 0.28f, bearDur = 0.36f, pileDelay = 0.42f, pileStagger = 0.04f, pileDur = 0.25f;
        int n = cfg.gridSize;
        float tilesEnd = tileDelay + introTileStagger * (2 * (n - 1)) + tileDur;
        int piles = 0;
        for (int i = 0; i < standPiles.Count; i++) if (standPiles[i].gameObject.activeSelf) piles++;
        float total = Mathf.Max(boardDur, hudDelay + hudDur, tilesEnd, bearDelay + bearDur,
            pileDelay + pileStagger * Mathf.Max(0, piles - 1) + pileDur);

        var bearRt = bear != null ? bear.rectTransform : null;
        float t = -introDelay;
        while (true)
        {
            boardIntroScale = Mathf.Lerp(introBoardScale, 1f, OutCubic(Mathf.Clamp01(t / boardDur)));
            if (topHud != null)
                topHud.anchoredPosition = hudHome + Vector2.up * (320f * (1f - OutBack(Mathf.Clamp01((t - hudDelay) / hudDur))));
            for (int cell = 0; cell < tiles.Length; cell++)
            {
                float k = Mathf.Clamp01((t - tileDelay - introTileStagger * (cell % n + cell / n)) / tileDur);
                float sc = OutBack(k);
                tiles[cell].rectTransform.localScale = new Vector3(sc, sc, 1f);
            }
            if (bearRt != null)
            {
                float k = Mathf.Clamp01((t - bearDelay) / bearDur);
                bearRt.anchoredPosition = bearHome + Vector2.down * (260f * (1f - OutBack(k)));
                bear.enabled = k > 0f;
            }
            for (int i = 0, p = 0; i < standPiles.Count; i++)
            {
                if (!standPiles[i].gameObject.activeSelf) continue;
                float sc = OutBack(Mathf.Clamp01((t - pileDelay - pileStagger * p++) / pileDur));
                standPiles[i].localScale = new Vector3(sc, sc, 1f);
            }
            if (t >= total) break;
            yield return null;
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        }

        boardIntroScale = 1f;
        busy = false;
        if (!HarvestHowToPlay.SeenThisCycle(cfg)) ShowHowToPlay();   // her sezonun ilk açılışında
    }

    private static float OutCubic(float k) { float x = 1f - k; return 1f - x * x * x; }

    private static float OutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    // Açılış yarıda kesilirse (OnPlay vb.) her şey son haline otursun.
    private void SnapIntro()
    {
        boardIntroScale = 1f;
        if (topHud != null) topHud.anchoredPosition = hudHome;
        if (bear != null) { bear.rectTransform.anchoredPosition = bearHome; bear.enabled = true; }
        if (tiles != null) foreach (var tile in tiles) tile.rectTransform.localScale = Vector3.one;
        foreach (var pile in standPiles) pile.localScale = Vector3.one;
    }

    private void ShowHowToPlay()
    {
        if (busy || cfg == null) return;
        busy = true;   // overlay açıkken kazı yok
        HarvestHowToPlay.Show(transform, cfg, () =>
        {
            HarvestHowToPlay.MarkSeen(cfg);
            busy = false;
        });
    }

    public void Hide()
    {
        if (busy || root == null || !root.activeSelf) return;
        if (iris == null) { root.SetActive(false); return; }
        busy = true;
        iris.PlayClose(origin, () => { busy = false; root.SetActive(false); });
    }

    // Seviyeye geçiş: animasyonsuz kapan (sahne değişiyor).
    private void HideImmediate()
    {
        StopAllCoroutines();
        SnapIntro();
        busy = false;
        if (root != null) root.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!IsOpen) return;
        FitBoard();
        PulseHints();
        RefreshTimer();
    }

    // Kalan süre: metin yalnız saniye değişince yazılır (kare başı string üretimi yok).
    private void RefreshTimer()
    {
        if (timerText == null || cfg == null) return;
        var now = System.DateTime.UtcNow;
        var remaining = HarvestState.WindowEnd(cfg, now) - now;
        long second = remaining > System.TimeSpan.Zero ? (long)System.Math.Ceiling(remaining.TotalSeconds) : 0;
        if (second == shownTimerSecond) return;
        shownTimerSecond = second;
        timerText.text = TimeFormat.Countdown(remaining);
    }

    // Yarısı kazılmış ama tamamlanmamış ürünlerin KAZILMAMIŞ kareleri ("burayı da kaz").
    private void RefreshHints()
    {
        hintMask = 0;
        var floor = cfg != null ? cfg.GetFloor(HarvestState.Floor) : null;
        int dug = HarvestState.DugMask;
        if (floor?.placements != null)
            for (int i = 0; i < floor.placements.Length; i++)
            {
                if (HarvestState.IsFound(i)) continue;
                int mask = cfg.PlacementMask(floor.placements[i]);
                if ((dug & mask) != 0) hintMask |= mask & ~dug;
            }
        if (glows == null || cfg.hintGlow == null) return;
        for (int c = 0; c < glows.Length; c++)
            glows[c].gameObject.SetActive((hintMask & (1 << c)) != 0);
    }

    private void PulseHints()
    {
        if (hintMask == 0 || glows == null) return;
        float a = cfg.hintGlowColor.a * (0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f)));
        var col = new Color(cfg.hintGlowColor.r, cfg.hintGlowColor.g, cfg.hintGlowColor.b, a);
        for (int c = 0; c < glows.Length; c++)
            if ((hintMask & (1 << c)) != 0) glows[c].color = col;
    }

    // Genişliğe sığdır (alt hizalı); taşarsa yüksekliğe. Board boyutu = arka plan pikseli.
    private void FitBoard()
    {
        if (board == null || viewport == null) return;
        Vector2 size = board.sizeDelta;
        if (size.x < 1f || size.y < 1f) return;
        Rect area = viewport.rect;
        float s = area.width / size.x;
        if (size.y * s > area.height) s = area.height / size.y;
        s *= boardIntroScale;
        if (Mathf.Abs(board.localScale.x - s) > 0.0001f)
            board.localScale = new Vector3(s, s, 1f);
    }

    // ── Izgara geometrisi (arka plan pikseli, sol-üst orijin) ───────────────────────

    private Vector2 Bed(float u, float v)
    {
        Vector2 t = cfg.bedTop, r = cfg.bedRight, l = cfg.bedLeft, b = cfg.bedBottom;
        return t + u * (r - t) + v * (l - t) + u * v * (b - r - l + t);
    }

    private Vector2 CellCenter(int u, int v)
    {
        float n = cfg.gridSize;
        return Bed((u + 0.5f) / n, (v + 0.5f) / n);
    }

    private float CellWidth(int u, int v)
    {
        float n = cfg.gridSize;
        return Mathf.Abs(Bed((u + 1) / n, v / n).x - Bed(u / n, (v + 1) / n).x);
    }

    // Ters bilinear (Newton): piksel → (u, v) 0..1. Yatak dörtgeninin dışında false.
    private bool PixelToBed(Vector2 p, out float u, out float v)
    {
        Vector2 t = cfg.bedTop, r = cfg.bedRight, l = cfg.bedLeft, b = cfg.bedBottom;
        Vector2 e1 = r - t, e2 = l - t, k = b - r - l + t;
        u = 0.5f; v = 0.5f;
        for (int i = 0; i < 8; i++)
        {
            Vector2 f = t + u * e1 + v * e2 + u * v * k - p;
            Vector2 du = e1 + v * k, dv = e2 + u * k;
            float det = du.x * dv.y - du.y * dv.x;
            if (Mathf.Abs(det) < 1e-5f) return false;
            u -= (f.x * dv.y - f.y * dv.x) / det;
            v -= (du.x * f.y - du.y * f.x) / det;
        }
        return u >= 0f && u < 1f && v >= 0f && v < 1f;
    }

    private static Vector2 ToBoard(Vector2 px) => new(px.x, -px.y);   // Board içinde (0,1) anchor'lı konum

    // ── Kurulum ──────────────────────────────────────────────────────────────────────

    private void BuildGrid()
    {
        int n = cfg.gridSize, count = n * n;
        if (built && tiles != null && tiles.Length == count) return;
        foreach (Transform c in tilesLayer) Destroy(c.gameObject);
        tiles = new Image[count];
        peeks = new Image[count];
        glows = new Image[count];

        // Arkadan öne çizim: u+v küçük olan (yatağın üst köşesine yakın) önce.
        var order = new List<int>(count);
        for (int i = 0; i < count; i++) order.Add(i);
        order.Sort((a, b) => (a % n + a / n).CompareTo(b % n + b / n));

        Sprite any = cfg.soilTiles != null && cfg.soilTiles.Length > 0 ? cfg.soilTiles[0] : cfg.dugTile;
        float aspect = any != null ? any.rect.height / any.rect.width : 0.75f;
        foreach (int cell in order)
        {
            int u = cell % n, v = cell / n;
            float w = CellWidth(u, v) * cfg.tileFill / cfg.tileTopFaceWidth;
            var img = NewImage($"Tile_{u}_{v}", tilesLayer);
            var rt = img.rectTransform;
            rt.pivot = new Vector2(cfg.tileTopFaceCenter.x, 1f - cfg.tileTopFaceCenter.y);
            rt.sizeDelta = new Vector2(w, w * aspect);
            rt.anchoredPosition = ToBoard(CellCenter(u, v));
            tiles[cell] = img;

            // Uç: ürünün YALNIZ üst kısmı (havuçta yapraklar) çukurdan çıkar → "burada bir şey gömülü,
            // komşu kareleri kaz" okunur; tamamlanmış ürünle karışmaz.
            var peek = NewImage($"Peek_{u}_{v}", rt);
            var prt = peek.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(cfg.tileTopFaceCenter.x, 1f - cfg.tileTopFaceCenter.y);   // üst yüz merkezi
            prt.pivot = new Vector2(0.5f, 1f - PeekFill);   // dolgu kenarı = toprak çizgisi, hücre merkezinde
            prt.sizeDelta = new Vector2(w * 0.55f, w * 0.55f);
            peek.preserveAspect = true;
            peek.type = Image.Type.Filled;
            peek.fillMethod = Image.FillMethod.Vertical;
            peek.fillOrigin = (int)Image.OriginVertical.Top;
            peek.fillAmount = PeekFill;
            peek.gameObject.SetActive(false);
            peeks[cell] = peek;

            // Üst yüzde elips ışık (izometrik elmasın oranında); ilk sırada → ucun arkasında kalır.
            var glow = NewImage($"Glow_{u}_{v}", rt);
            var grt = glow.rectTransform;
            grt.anchorMin = grt.anchorMax = prt.anchorMin;
            grt.pivot = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(w * 0.8f, w * 0.46f);
            glow.sprite = cfg.hintGlow;
            glow.color = cfg.hintGlowColor;
            glow.transform.SetAsFirstSibling();
            glow.gameObject.SetActive(false);
            glows[cell] = glow;
        }
        built = true;
    }

    private void RebuildFloorVisuals()
    {
        var floor = cfg.GetFloor(HarvestState.Floor);
        for (int cell = 0; cell < tiles.Length; cell++)
        {
            bool dug = HarvestState.IsDug(cell);
            tiles[cell].sprite = dug ? cfg.dugTile : SoilFor(cell);
            tiles[cell].rectTransform.localScale = Vector3.one;
            int p = floor != null ? cfg.PlacementAt(floor, cell) : -1;
            bool peek = dug && p >= 0 && !HarvestState.IsFound(p);
            peeks[cell].gameObject.SetActive(peek);
            if (peek) peeks[cell].sprite = cfg.GetCrop(floor.placements[p].crop)?.sprite;
        }

        // Ürün görselleri: yerleşim başına bir tane (havuz); bulunanlar tezgâhta durur.
        int placements = floor != null ? floor.placements.Length : 0;
        EnsurePool(cropImages, placements, cropsLayer, "Crop");
        EnsurePiles(placements);
        for (int i = 0; i < cropImages.Count; i++)
        {
            cropImages[i].gameObject.SetActive(false);
            bool onStand = i < placements && HarvestState.IsFound(i);
            standPiles[i].gameObject.SetActive(onStand);
            if (onStand) SetupPile(standPiles[i], floor.placements[i], i);
        }
        RefreshHints();
    }

    // Hücreye göre sabit (rastgele görünen ama her açılışta aynı) toprak varyantı.
    private Sprite SoilFor(int cell)
    {
        var set = cfg.soilTiles;
        if (set == null || set.Length == 0) return cfg.dugTile;
        uint h = (uint)(cell * 2654435761u + (uint)HarvestState.Floor * 40503u);
        return set[(int)(h % (uint)set.Length)];
    }

    private void EnsurePool(List<Image> pool, int count, RectTransform parent, string name)
    {
        while (pool.Count < count)
        {
            var img = NewImage($"{name}_{pool.Count}", parent);
            img.preserveAspect = true;
            img.gameObject.SetActive(false);
            pool.Add(img);
        }
    }

    private Vector2 PlacementCenter(HarvestPlacement p)
    {
        var crop = cfg.GetCrop(p.crop);
        Vector2 sum = Vector2.zero;
        int c = 0;
        for (int du = 0; du < crop.size.x; du++)
            for (int dv = 0; dv < crop.size.y; dv++) { sum += CellCenter(p.origin.x + du, p.origin.y + dv); c++; }
        return c > 0 ? sum / c : Vector2.zero;
    }

    private void SetupCropImage(Image img, HarvestPlacement p)
    {
        var crop = cfg.GetCrop(p.crop);
        img.sprite = crop?.sprite;
        int span = crop != null ? Mathf.Max(crop.size.x, crop.size.y) : 1;
        float cw = CellWidth(p.origin.x, p.origin.y);
        float target = cw * (0.85f + 0.45f * (span - 1));       // büyük ürün = büyük görsel
        Vector2 s = img.sprite != null ? img.sprite.rect.size : Vector2.one;
        float k = target / Mathf.Max(s.x, s.y);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.25f);                    // topraktan yükselir
        rt.sizeDelta = s * k;
        rt.anchoredPosition = ToBoard(PlacementCenter(p));
        rt.localScale = Vector3.one;
    }

    // Tezgâh yığını: aynı üründen birkaç tane üst üste (kasada/sepette hasat dolu görünsün).
    private static readonly Vector2[] PileOffsets = { new(-20f, 0f), new(20f, 2f), new(0f, 22f) };

    private void EnsurePiles(int count)
    {
        while (standPiles.Count < count)
        {
            var pile = new GameObject($"StandPile_{standPiles.Count}", typeof(RectTransform)).GetComponent<RectTransform>();
            pile.gameObject.layer = board.gameObject.layer;
            pile.SetParent(board, false);
            pile.anchorMin = pile.anchorMax = new Vector2(0f, 1f);
            pile.pivot = new Vector2(0.5f, 0f);          // konum = kabın tabanı; yığın oradan yükselir
            pile.sizeDelta = new Vector2(100f, 80f);
            for (int k = 0; k < PileSize; k++)
            {
                var img = NewImage($"Item{k}", pile);
                img.preserveAspect = true;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = PileOffsets[k];
            }
            pile.gameObject.SetActive(false);
            standPiles.Add(pile);
        }
    }

    private void SetupPile(RectTransform pile, HarvestPlacement p, int index)
    {
        var sprite = cfg.GetCrop(p.crop)?.sprite;
        pile.anchoredPosition = StandPoint(index);
        pile.localScale = Vector3.one;
        foreach (Transform c in pile)
        {
            var img = c.GetComponent<Image>();
            img.sprite = sprite;
            Vector2 s = sprite != null ? sprite.rect.size : Vector2.one;
            img.rectTransform.sizeDelta = s * (60f / Mathf.Max(s.x, s.y));
        }
    }

    // 4 kap (3 kasa + sepet); 4'ten fazla ürünlü hasatta 5. ürün ilk kabın üstüne yığılır.
    private Vector2 StandPoint(int index)
    {
        if (standSlots == null || standSlots.Length == 0) return Vector2.zero;
        int n = standSlots.Length;
        return standSlots[index % n].anchoredPosition + Vector2.up * 34f * (index / n);
    }

    // ── Dokunma ─────────────────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData e)
    {
        if (busy || cfg == null || board == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(board, e.position, e.pressEventCamera, out var local))
            return;
        // Board pivot (0.5, 0) → arka plan pikseli (sol-üst orijin).
        Vector2 size = board.sizeDelta;
        var px = new Vector2(local.x + size.x * 0.5f, size.y - local.y);
        if (!PixelToBed(px, out float u, out float v)) return;
        int n = cfg.gridSize;
        int cell = Mathf.Clamp((int)(u * n), 0, n - 1) + Mathf.Clamp((int)(v * n), 0, n - 1) * n;
        TryDigCell(cell);
    }

    private void TryDigCell(int cell)
    {
        if (HarvestState.IsDug(cell)) return;
        if (HarvestState.Trowels <= 0)
        {
            StartCoroutine(Nudge(trowelText != null ? trowelText.rectTransform : null));
            return;
        }
        var floor = cfg.GetFloor(HarvestState.Floor);
        if (!HarvestState.TryDig(cfg, cell, out var result)) return;
        StartCoroutine(PlayDig(result, floor));
    }

    private IEnumerator PlayDig(HarvestState.DigResult r, HarvestFloorDef floor)
    {
        busy = true;
        SetBear(cfg.bearDig);
        EventSfx.Play(l => l.stepPop);

        var tile = tiles[r.cell];
        var rt = tile.rectTransform;
        float t = 0f, half = digDuration * 0.5f;
        bool swapped = false;
        while (t < digDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = t < half ? t / half : 1f - (t - half) / half;
            rt.localScale = new Vector3(1f + 0.08f * k, 1f - 0.12f * k, 1f);
            if (!swapped && t >= half) { tile.sprite = cfg.dugTile; swapped = true; }
            yield return null;
        }
        rt.localScale = Vector3.one;
        tile.sprite = cfg.dugTile;

        if (r.placement >= 0 && !r.cropCompleted)
        {
            var peek = peeks[r.cell];
            peek.sprite = cfg.GetCrop(floor.placements[r.placement].crop)?.sprite;
            peek.gameObject.SetActive(true);
            yield return Pop(peek.rectTransform, 0.2f);
        }

        if (!r.floorCompleted) RefreshHints();   // kat bittiyse maske yeni kata ait; Rebuild yeniler

        if (r.cropCompleted)
            yield return RevealCrop(floor, r.placement);
        else
            SetBear(cfg.bearIdle);

        if (r.floorCompleted)
            yield return CompleteFloor(floor);

        busy = false;
    }

    private IEnumerator RevealCrop(HarvestFloorDef floor, int placement)
    {
        var p = floor.placements[placement];
        // Ürünün uçları kaybolur, tam görseli topraktan çıkar.
        int mask = cfg.PlacementMask(p);
        for (int c = 0; c < peeks.Length; c++)
            if ((mask & (1 << c)) != 0) peeks[c].gameObject.SetActive(false);

        var img = cropImages[placement];
        SetupCropImage(img, p);
        img.gameObject.SetActive(true);
        SetBear(cfg.bearCheer);
        EventSfx.Play(l => l.rankBadge);
        yield return Pop(img.rectTransform, cropPopDuration);
        yield return WaitUnscaled(cheerHold * 0.5f);

        // Tezgâha uç.
        var pile = standPiles[placement];
        SetupPile(pile, p, placement);
        Vector2 from = img.rectTransform.anchoredPosition, to = pile.anchoredPosition;
        Vector2 fromSize = img.rectTransform.sizeDelta;
        Vector2 sz = img.sprite != null ? img.sprite.rect.size : Vector2.one;
        Vector2 toSize = sz * (60f / Mathf.Max(sz.x, sz.y));
        float t = 0f;
        while (t < cropFlyDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / cropFlyDuration);
            float e = k * k * (3f - 2f * k);
            img.rectTransform.anchoredPosition = Vector2.LerpUnclamped(from, to, e) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 120f;
            img.rectTransform.sizeDelta = Vector2.LerpUnclamped(fromSize, toSize, e);
            yield return null;
        }
        img.gameObject.SetActive(false);
        pile.gameObject.SetActive(true);
        StartCoroutine(Pop(pile, 0.25f));
        EventSfx.Play(l => l.plankPlace);
        SetBear(cfg.bearIdle);
    }

    private IEnumerator CompleteFloor(HarvestFloorDef floor)
    {
        yield return WaitUnscaled(0.3f);
        bool done = false;
        // Son hasadın büyük sandığı oyuncunun harikasının sandığı (yoksa varsayılan sandık).
        Sprite closed = null, opened = null;
        if (HarvestState.IsFinished(cfg) && cfg.wonderCatalog != null)
        {
            var wonder = WonderProgress.ActiveWonder(cfg.wonderCatalog);
            if (wonder != null) { closed = wonder.chestClosedSprite; opened = wonder.chestOpenedSprite; }
        }
        if (floor.rewards != null && floor.rewards.Length > 0)
            RewardChestRevealOverlay.Show(floor.rewards, () => done = true, closed, opened);
        else done = true;
        while (!done) yield return null;

        // Sonraki kat: toprak yeniden kapanır, tezgâh boşalır.
        RebuildFloorVisuals();
        RefreshHud();
        if (HarvestState.IsFinished(cfg)) SetBear(cfg.bearCheer);
    }

    // ── Yardımcılar ─────────────────────────────────────────────────────────────────

    private void RefreshHud()
    {
        if (cfg == null) return;
        int floors = cfg.FloorCount;
        int floor = Mathf.Min(HarvestState.Floor + 1, floors);
        if (subtitleText != null)
        {
            if (titleText != null) titleText.text = L("harvest_sign_top", "Bostan");
            subtitleText.text = L("harvest_sign_bottom", "Hasadı");
        }
        else if (titleText != null) titleText.text = L("harvest_title", "BOSTAN HASADI");
        if (floorText != null) floorText.text = string.Format(L("harvest_floor", "Hasat {0}/{1}"), floor, floors);
        if (trowelText != null) trowelText.text = $"x{HarvestState.Trowels}";
        bool none = HarvestState.Trowels <= 0 && !HarvestState.IsFinished(cfg);
        if (hintText != null)
        {
            hintText.gameObject.SetActive(none || HarvestState.IsFinished(cfg));
            hintText.text = HarvestState.IsFinished(cfg)
                ? L("harvest_finished", "Hasat tamam! Harika iş çıkardın.")
                : L("harvest_no_trowel", "Seviye geç, kürek kazan!");
        }
        if (playButton != null)
        {
            playButton.gameObject.SetActive(none);
            var label = playButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = L("harvest_play", "OYNA");
        }
    }

    private void OnPlay()
    {
        var launcher = FindFirstObjectByType<MainMenuLevelButtonController>(FindObjectsInactive.Include);
        if (launcher == null) { Debug.LogWarning("[Harvest] MainMenuLevelButtonController yok — Oyna iptal."); return; }
        HideImmediate();
        if (launcher.StartLevel() == LevelStartResult.Suppressed) Open(origin);
    }

    private void SetBear(Sprite s)
    {
        if (bear != null && s != null) bear.sprite = s;
    }

    private static IEnumerator Pop(RectTransform rt, float duration)
    {
        if (rt == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = k < 0.6f ? Mathf.Lerp(0f, 1.18f, k / 0.6f) : Mathf.Lerp(1.18f, 1f, (k - 0.6f) / 0.4f);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private static IEnumerator Nudge(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector2 home = rt.anchoredPosition;
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.unscaledDeltaTime;
            rt.anchoredPosition = home + Vector2.right * Mathf.Sin(t * 60f) * 10f * (1f - t / 0.35f);
            yield return null;
        }
        rt.anchoredPosition = home;
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        return img;
    }

    private static string L(string key, string fallback)
    {
        string v = GameLocalization.Get(key);
        return string.IsNullOrEmpty(v) || v == key ? fallback : v;
    }
}
