using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lamba şeridi (ObstacleId.LampStrip) — kurallar + görünüm. Duvar ailesinden: tek origin'li, serbest şekilli
/// (düz / L / zikzak / halka; DALLANMASIZ) parça, her hücre ayrı vuruş alır (ObstacleStateService.TakesPerCellHits).
///  - Her hücrede bir lamba (LevelData.lampColorByCell). Renkli lamba: yanındaki AYNI RENK normal eşleşme ya da
///    special/joker kırar. Mor lamba: yalnız special/joker. Kırılan lambanın yeri boş yuva kalır.
///  - Parça ayarı (LevelData.lampStripSettings) açıksa her oyuncu hamlesi oturunca bütün dizi (boşluklar dahil)
///    yol boyunca 1 hücre kayar; uçtan çıkan diğer uçtan girer. İleri = yolun sol-üst ucundan diğer uca.
///  - Son lamba kırılınca çerçeve temizlenir (hedef = çerçeve sayısı) ve 2-3 büyük parçaya ayrılıp düşer.
/// Görsel: hücre başına çerçeve parçası (komşuluğa göre 11 sprite'tan biri) + hale + lamba. Sprite'lar
/// ObstacleDef.auxiliarySprites sırasıyla (bkz. Sprite* sabitleri). Tüm parçalar tek Update döngüsünde oynar; oynayan
/// iş yokken servis kapalıdır (Update çağrılmaz). Kırılma parçacığı + sesleri genel engel yolundan (BoardBreakFx).
/// Sahne kurulumu yok: GridSpawner level çizerken Ensure(board).Build çağırır.
/// </summary>
public sealed class LampStripService : MonoBehaviour
{
    // ObstacleDef.auxiliarySprites sırası
    private const int SpriteHLeft = 0, SpriteHMid = 1, SpriteHRight = 2, SpriteHSingle = 3;
    private const int SpriteVTop = 4, SpriteVMid = 5, SpriteVBottom = 6;
    private const int SpriteCRightDown = 7, SpriteCLeftDown = 8, SpriteCRightUp = 9, SpriteCLeftUp = 10;
    private const int SpriteLampYellow = 11;   // 11..14 = sarı, kırmızı, mavi, yeşil (LampStripColor 1..4 sırası)
    private const int SpriteLampPurple = 15;
    private const int SpriteHalo = 16;
    private const int SpriteCount = 17;

    private const float ShiftSeconds = 0.34f;      // tek lambanın sön → kay → yan süresi
    private const float ShiftWaveSeconds = 0.30f;  // dalga başından sonuna toplam gecikme (uzun şeritte sabit)
    private const float ShiftWaveStepMax = 0.03f;  // kısa şeritte lambalar arası gecikme tavanı
    private const float ShiftDim = 0.38f;          // kayarken parlaklık (1 = tam yanık)
    private const float ShiftDimAlpha = 0.70f;     // kayarken opaklık
    private const float DimEnd = 0.28f, MoveStart = 0.18f, MoveEnd = 0.74f, LightStart = 0.62f;   // faz sınırları (0-1)
    private const float PopSeconds = 0.20f;
    private const float CrackSeconds = 0.14f;
    private const float FallSeconds = 0.75f;
    // Hale yalnız krom yuva halkasına renk verir (lamba 250/512, halka 300/512); hücreyi kaplarsa grafit kanalı
    // ve çelik kenarı renkli sise boğuyor, lambanın canlılığı kayboluyordu.
    private const float HaloAlpha = 0.35f;
    private const float HaloScale = 0.68f;
    private const bool ShowHalo = false;   // deneme: hale kapalı (geri almak için true)


    private static readonly Color[] HaloColors =
    {
        Color.clear,
        new Color(1f, 0.78f, 0.16f),   // sarı
        new Color(1f, 0.27f, 0.24f),   // kırmızı
        new Color(0.27f, 0.59f, 1f),   // mavi
        new Color(0.31f, 0.86f, 0.39f),// yeşil
        new Color(0.75f, 0.39f, 1f),   // mor
    };

    private sealed class Piece
    {
        public int origin;
        public readonly List<int> path = new();          // yol sırası (hücre index'leri)
        public bool shift, reverse;
        public bool closed;                              // halka: yolun iki ucu komşu → sarmalayan lamba da kayarak geçer
        public RectTransform root;
        public RectTransform frameLayer, haloLayer, lampLayer;
        public Image[] frames;
        public RectTransform[] slots;                    // hale + lamba taşıyıcısı (kaymada hareket eder)
        public Image[] halos, lamps;
        public Vector2[] slotPos;
        public int[] shownColor;                         // her taşıyıcının gösterdiği renk (kayma sırasında eski renk)
        public float shiftTime = -1f;
        public int shiftDir;                             // +1 ileri, -1 geri
        public int waveLead;                             // dalgada ilk kalkan taşıyıcı
        public float popTime = -1f;
        public int popSlot = -1;
        public bool collapsing;
        public float collapseTime;
        public RectTransform[] chunks;
        public Vector2[] chunkVel;
        public float[] chunkSpin;
        public CellHold hold;
        public System.IDisposable job;
    }

    private BoardController board;
    private ObstacleStateService boundState;
    private LevelData level;
    private RectTransform root;
    private int width;
    private float tileSize;
    private Sprite[] sprites;
    private readonly Dictionary<int, Piece> pieces = new();       // origin → parça
    private readonly Dictionary<int, int> pieceOfCell = new();     // hücre → origin
    private readonly Dictionary<int, int> colorByCell = new();     // hücre → LampStripColor (mantıksal)
    private readonly List<Piece> active = new();                   // Update'te oynayan parçalar
    private readonly List<(ObstacleId id, int origin)> layerBuffer = new();
    private readonly List<int> cellBuffer = new();
    private System.Func<int, int, ObstacleHitContext, TileType?, WallHitOutcome> hitHandler;
    private System.Func<int, bool> litHandler;

    public static LampStripService Ensure(BoardController board)
    {
        if (board == null) return null;
        if (!board.TryGetComponent<LampStripService>(out var service))
        {
            service = board.gameObject.AddComponent<LampStripService>();
            board.OnPlayerMoveResolved += service.HandleMoveResolved;
            board.OnObstacleViewRestored += service.HandleViewRestored;
        }
        service.board = board;
        return service;
    }

    /// Level çizilirken çağrılır: görünür şeritleri kaydeder, renkleri yükler, görünümleri kurar.
    public void Build(LevelData levelData, RectTransform parent, float cellSize)
    {
        Clear();
        level = levelData;
        if (board == null || level?.obstacles == null || level.obstacleOrigins == null || parent == null) return;

        root = parent;
        width = level.width;
        tileSize = cellSize;
        sprites = ResolveSprites(level);
        Bind(board.ObstacleStateService);

        for (int i = 0; i < level.obstacles.Length; i++)
        {
            // Örtü altındaki (sonradan açılacak) şerit hücrelerinin rengi de baştan yüklenir.
            int c = level.lampColorByCell != null && i < level.lampColorByCell.Length ? level.lampColorByCell[i] : 0;
            if (c > LampStripColor.None && c < LampStripColor.Count) colorByCell[i] = c;
        }

        var origins = new HashSet<int>();
        for (int i = 0; i < level.obstacles.Length; i++)
            if (TryGetVisibleLampLayer(i, out int origin)) origins.Add(origin);
        foreach (int origin in origins) BuildPiece(origin);
        enabled = false;
    }

    public void Clear()
    {
        foreach (var p in pieces.Values) ReleasePiece(p);
        foreach (var p in active) if (p.collapsing) ReleasePiece(p);
        pieces.Clear();
        pieceOfCell.Clear();
        colorByCell.Clear();
        active.Clear();
    }

    private void OnDestroy()
    {
        Clear();
        Bind(null);
        if (board != null)
        {
            board.OnPlayerMoveResolved -= HandleMoveResolved;
            board.OnObstacleViewRestored -= HandleViewRestored;
        }
    }

    private void Bind(ObstacleStateService state)
    {
        if (boundState == state) return;
        if (boundState != null)
        {
            if (boundState.LampStripHitInterceptor == hitHandler) boundState.LampStripHitInterceptor = null;
            if (boundState.LampStripCellLitQuery == litHandler) boundState.LampStripCellLitQuery = null;
            boundState.OnObstacleDestroyed -= HandleObstacleDestroyed;
        }
        boundState = state;
        if (boundState == null) return;
        hitHandler ??= HandleHit;
        litHandler ??= IsLitAt;
        boundState.LampStripHitInterceptor = hitHandler;
        boundState.LampStripCellLitQuery = litHandler;
        boundState.OnObstacleDestroyed += HandleObstacleDestroyed;
    }

    // ── Sorgular ────────────────────────────────────────────────────

    /// Hücrede kırılabilir (yanan) lamba var mı? Hedef seçiciler (PatchBot) boş yuvaya gitmesin.
    public bool IsLitAt(int cell) =>
        pieceOfCell.TryGetValue(cell, out int origin) && pieces.TryGetValue(origin, out var p) && !p.collapsing
        && colorByCell.TryGetValue(cell, out int c) && c != LampStripColor.None;

    /// Engel ipucu vurgusu için canlı parça kökleri.
    public void CollectViewRoots(List<Transform> result)
    {
        foreach (var p in pieces.Values)
            if (p.root != null) result.Add(p.root);
    }

    // ── Kural ───────────────────────────────────────────────────────

    private WallHitOutcome HandleHit(int origin, int cell, ObstacleHitContext context, TileType? source)
    {
        if (!pieces.TryGetValue(origin, out var p) || p.collapsing) return WallHitOutcome.Ignored;
        int slot = p.path.IndexOf(cell);
        if (slot < 0 || !colorByCell.TryGetValue(cell, out int color) || color == LampStripColor.None)
            return WallHitOutcome.Ignored;

        if (context == ObstacleHitContext.NormalMatch)
        {
            // Normal eşleşme yalnız kendi rengindeki lambayı kırar; mor lamba yalnız special/joker.
            if (!source.HasValue || !LampStripColor.TryGetTileType(color, out var lampType) || lampType != source.Value)
                return WallHitOutcome.Ignored;
        }

        colorByCell[cell] = LampStripColor.None;
        if (boundState != null) boundState.LampStripLastHitColor = ToMask(color);
        ShowBroken(p, slot);

        int lit = LitCount(p);
        if (lit == 0) return WallHitOutcome.Collapse;   // temizlik ObstacleStateService'te → HandleObstacleDestroyed
        boundState?.SetWallRemainingHits(origin, ObstacleId.LampStrip, lit);
        return WallHitOutcome.Advanced;
    }

    // Oyuncu hamlesi oturdu: kayan parçalarda dizi (boşluklar dahil) yol boyunca bir hücre ilerler.
    private void HandleMoveResolved()
    {
        foreach (var p in pieces.Values)
        {
            if (!p.shift || p.collapsing || p.path.Count < 2 || LitCount(p) == 0) continue;
            int n = p.path.Count;
            int dir = p.reverse ? -1 : 1;
            // Mantıksal kayma hemen (sonraki hamle yeni yerleri görür); görünüm ShiftSeconds içinde yetişir.
            var next = new int[n];
            for (int i = 0; i < n; i++) next[(i + dir + n) % n] = ColorAt(p.path[i]);
            // Dalga başı: açık şeritte uçtaki; halkada önü boş yuva olan dolu lamba (yoksa herhangi biri — üstten atlar).
            int lead = dir > 0 ? n - 1 : 0;
            if (p.closed)
                for (int i = 0; i < n; i++)
                    if (ColorAt(p.path[i]) != LampStripColor.None && next[(i + dir + n) % n] != LampStripColor.None
                        && ColorAt(p.path[(i + dir + n) % n]) == LampStripColor.None) { lead = i; break; }
            for (int i = 0; i < n; i++) colorByCell[p.path[i]] = next[i];
            if (p.slots == null) continue;
            p.waveLead = lead;
            p.slots[lead].SetAsLastSibling();   // dolu halkada arkadakinin üstünden atlıyormuş gibi üstte çizilir
            p.shiftDir = dir;
            p.shiftTime = 0f;
            Wake(p);
        }
    }

    private void HandleObstacleDestroyed(int origin, ObstacleId id)
    {
        if (id != ObstacleId.LampStrip) return;
        if (!pieces.TryGetValue(origin, out var p)) return;
        pieces.Remove(origin);
        foreach (int c in p.path) { pieceOfCell.Remove(c); colorByCell.Remove(c); }
        if (p.root == null) return;
        StartCollapse(p);
    }

    // Opak örtünün (kasa vb.) altından çıkan hücreler parçaya eklenir; renkler korunur.
    private void HandleViewRestored(int x, int y)
    {
        if (boundState == null || root == null || width <= 0) return;
        int cell = y * width + x;
        if (!TryGetVisibleLampLayer(cell, out int origin)) return;
        if (pieces.TryGetValue(origin, out var old))
        {
            pieces.Remove(origin);
            foreach (int c in old.path) pieceOfCell.Remove(c);
            ReleasePiece(old);
        }
        BuildPiece(origin);
    }

    // ── Kurulum ─────────────────────────────────────────────────────

    private bool TryGetVisibleLampLayer(int cell, out int origin)
    {
        origin = -1;
        if (level?.obstacles == null || cell < 0 || cell >= level.obstacles.Length) return false;
        if (boundState == null)
        {
            origin = level.obstacleOrigins[cell];
            return (ObstacleId)level.obstacles[cell] == ObstacleId.LampStrip && origin >= 0;
        }
        boundState.GetLayersTopDown(cell, layerBuffer);
        foreach (var (id, layerOrigin) in layerBuffer)
        {
            if (id == ObstacleId.LampStrip) { origin = layerOrigin; return origin >= 0; }
            if (!ObstacleStateService.IsSeeThroughLayer(id)) return false;   // opak örtü altında: açılınca çizilir
        }
        return false;
    }

    private void BuildPiece(int origin)
    {
        cellBuffer.Clear();
        for (int i = 0; i < level.obstacles.Length; i++)
            if (TryGetVisibleLampLayer(i, out int o) && o == origin) cellBuffer.Add(i);
        if (cellBuffer.Count == 0) return;

        var p = new Piece { origin = origin };
        OrderPath(cellBuffer, p.path);
        p.closed = p.path.Count >= 4 && IsAdjacent(p.path[0], p.path[p.path.Count - 1]);
        ResolveSettings(origin, out p.shift, out p.reverse);
        foreach (int c in p.path)
        {
            pieceOfCell[c] = origin;
            if (!colorByCell.ContainsKey(c)) colorByCell[c] = LampStripColor.None;
        }
        pieces[origin] = p;
        if (sprites != null) CreateView(p);
        boundState?.SetWallRemainingHits(origin, ObstacleId.LampStrip, Mathf.Max(1, LitCount(p)));
    }

    // Yol sırası: zincirde sol-üst uçtan (en küçük y, sonra x) diğer uca; halkada en küçük index'ten. Dallanma
    // editörde engellenir; olursa kalan hücreler sona eklenir (kayma yine çalışır, yön belirsizleşir).
    private void OrderPath(List<int> cells, List<int> path)
    {
        var set = new HashSet<int>(cells);
        int start = -1, bestKey = int.MaxValue;
        foreach (int c in cells)
        {
            if (Neighbors(set, c) != 1) continue;
            int key = (c / width) * 100000 + c % width;
            if (key < bestKey) { bestKey = key; start = c; }
        }
        if (start < 0) { start = int.MaxValue; foreach (int c in cells) start = Mathf.Min(start, c); }

        // Yürüyüş (BFS değil): kapalı halkada BFS iki yöne birden açılıp sırayı zikzak yapıyordu → kayma karşı
        // kenara atlıyordu. Her adımda görülmemiş tek komşuya geçilir; zincirde sonuç BFS ile aynı.
        var seen = new HashSet<int>();
        int cur = start;
        while (cur >= 0 && seen.Add(cur))
        {
            path.Add(cur);
            int x = cur % width, next = -1;
            if (set.Contains(cur - width) && !seen.Contains(cur - width)) next = cur - width;
            else if (x < width - 1 && set.Contains(cur + 1) && !seen.Contains(cur + 1)) next = cur + 1;
            else if (set.Contains(cur + width) && !seen.Contains(cur + width)) next = cur + width;
            else if (x > 0 && set.Contains(cur - 1) && !seen.Contains(cur - 1)) next = cur - 1;
            cur = next;
        }
        // Dallanma (editör engeller) olursa yürüyüşün kaçırdığı hücreler sona eklenir; parça eksik çizilmez.
        foreach (int c in cells) if (!seen.Contains(c)) path.Add(c);
    }

    private bool IsAdjacent(int a, int b)
    {
        int ax = a % width, bx = b % width;
        return (ax == bx && Mathf.Abs(a - b) == width) || (a / width == b / width && Mathf.Abs(ax - bx) == 1);
    }

    private int Neighbors(HashSet<int> set, int c)
    {
        int x = c % width, n = 0;
        if (set.Contains(c - width)) n++;
        if (set.Contains(c + width)) n++;
        if (x > 0 && set.Contains(c - 1)) n++;
        if (x < width - 1 && set.Contains(c + 1)) n++;
        return n;
    }

    private void ResolveSettings(int origin, out bool shift, out bool reverse)
    {
        shift = reverse = false;
        if (level?.lampStripSettings == null || level.wallPieceIds == null || origin >= level.wallPieceIds.Length) return;
        int pieceId = level.wallPieceIds[origin];
        foreach (var s in level.lampStripSettings)
        {
            if (s.pieceId != pieceId) continue;
            shift = s.shift;
            reverse = s.reverse;
            return;
        }
    }

    private void CreateView(Piece p)
    {
        int layer = root.gameObject.layer;
        p.root = MakeRect($"LampStrip_{p.origin}", root, layer);
        p.root.anchorMin = p.root.anchorMax = p.root.pivot = new Vector2(0f, 1f);
        p.root.anchoredPosition = Vector2.zero;
        p.root.sizeDelta = Vector2.zero;
        p.frameLayer = MakeLayer("Frames", p.root, layer);
        p.haloLayer = MakeLayer("Halos", p.root, layer);
        p.lampLayer = MakeLayer("Lamps", p.root, layer);

        int n = p.path.Count;
        p.frames = new Image[n];
        p.slots = new RectTransform[n];
        p.halos = new Image[n];
        p.lamps = new Image[n];
        p.slotPos = new Vector2[n];
        p.shownColor = new int[n];
        var set = new HashSet<int>(p.path);
        for (int i = 0; i < n; i++)
        {
            int c = p.path[i];
            int x = c % width, y = c / width;
            p.slotPos[i] = new Vector2(x * tileSize + tileSize * 0.5f, -(y * tileSize + tileSize * 0.5f));
            p.frames[i] = MakeImage("Frame", p.frameLayer, layer, sprites[FrameSprite(set, c)], p.slotPos[i]);

            // Hale ve lamba aynı taşıyıcıda (kaymada birlikte hareket eder); haleler komşu çerçeveye taşabilsin diye
            // ayrı katmanda, lambaların altında.
            p.slots[i] = MakeRect("Slot", p.lampLayer, layer);
            p.slots[i].anchorMin = p.slots[i].anchorMax = new Vector2(0f, 1f);
            p.slots[i].sizeDelta = new Vector2(tileSize, tileSize);
            p.slots[i].anchoredPosition = p.slotPos[i];
            p.halos[i] = MakeImage("Halo", p.haloLayer, layer, sprites[SpriteHalo], p.slotPos[i]);
            p.halos[i].rectTransform.sizeDelta = new Vector2(tileSize * HaloScale, tileSize * HaloScale);
            p.lamps[i] = MakeImage("Lamp", p.slots[i], layer, null, Vector2.zero);
            p.lamps[i].rectTransform.anchorMin = p.lamps[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ApplySlot(p, i, ColorAt(c));
        }
    }

    private int FrameSprite(HashSet<int> set, int c)
    {
        int x = c % width;
        bool l = x > 0 && set.Contains(c - 1), r = x < width - 1 && set.Contains(c + 1);
        bool u = set.Contains(c - width), d = set.Contains(c + width);
        if (l && r) return SpriteHMid;
        if (u && d) return SpriteVMid;
        if (r && d) return SpriteCRightDown;
        if (l && d) return SpriteCLeftDown;
        if (r && u) return SpriteCRightUp;
        if (l && u) return SpriteCLeftUp;
        if (r) return SpriteHLeft;
        if (l) return SpriteHRight;
        if (d) return SpriteVTop;
        if (u) return SpriteVBottom;
        return SpriteHSingle;
    }

    // Taşıyıcı i'nin gösterdiği rengi uygula (lamba sprite'ı + hale rengi; boş yuva → ikisi gizli).
    private void ApplySlot(Piece p, int i, int color)
    {
        p.shownColor[i] = color;
        bool on = color != LampStripColor.None && sprites != null;
        p.lamps[i].enabled = on;
        p.halos[i].enabled = on && ShowHalo;
        if (!on) return;
        p.lamps[i].sprite = sprites[color == LampStripColor.Purple ? SpriteLampPurple : SpriteLampYellow + color - 1];
        var h = HaloColors[color];
        p.halos[i].color = new Color(h.r, h.g, h.b, HaloAlpha);
    }

    private void ShowBroken(Piece p, int slot)
    {
        if (p.slots == null) return;
        // Kayma sürerken bu hücrenin rengini taşıyan, bir önceki konumdaki taşıyıcıdır.
        int n = p.path.Count;
        int carrier = p.shiftTime >= 0f ? (slot - p.shiftDir + n) % n : slot;
        ApplySlot(p, carrier, LampStripColor.None);
    }

    // ── Döngü ───────────────────────────────────────────────────────

    private void Wake(Piece p)
    {
        if (!active.Contains(p)) active.Add(p);
        if (!enabled) enabled = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var p = active[i];
            bool busy;
            try
            {
                busy = p.collapsing ? TickCollapse(p, dt) : TickShift(p, dt);
            }
            catch (System.Exception ex)
            {
                // Bir parçanın animasyon hatası oyunu kilitlemesin: görüntü temizlenir, hücre tutma + board işi
                // kapanır (açık kalan iş level sonunu sonsuza dek bekletirdi). Hata tam yığınla BİR KEZ loglanır.
                Debug.LogException(ex);
                ReleasePiece(p);
                busy = false;
            }
            if (!busy) active.RemoveAt(i);
        }
        if (active.Count == 0) enabled = false;
    }

    // Kayma = ışık tabelası: taşıyıcılar hareket yönünde ÖNDEKİNDEN başlayıp çok kısa aralıklarla sırayla, önce
    // sönükleşir, sönük haliyle yeni yuvaya kayar, orada yeniden yanar — "kapanıp yeni yerinde yanıyor" hissi.
    // Açık şeritte uçtaki lamba sönerek dışarı süzülür; döngüden giren lamba başında sönükten yanarak belirir
    // (halkada sarmalayan da komşuya kayar). Mantıksal renkler HandleMoveResolved'da anında değişti; girdi beklemez.
    private bool TickShift(Piece p, float dt)
    {
        if (p.slots == null || p.root == null) return false;   // parça yeniden kuruldu / yok edildi
        int n = p.path.Count;
        if (p.shiftTime >= 0f)
        {
            p.shiftTime += dt;
            float step = Mathf.Min(ShiftWaveStepMax, ShiftWaveSeconds / Mathf.Max(1, n - 1));
            float total = ShiftSeconds + step * (n - 1);
            int wrapFrom = p.shiftDir > 0 ? n - 1 : 0;
            for (int i = 0; i < n; i++)
            {
                int order = ((p.waveLead - i) * p.shiftDir + n) % n;   // dalga başından geriye doğru sırayla
                float k = Mathf.Clamp01((p.shiftTime - order * step) / ShiftSeconds);
                float m = Smooth01(Mathf.InverseLerp(MoveStart, MoveEnd, k));
                bool exiting = i == wrapFrom && !p.closed;
                Vector2 to = exiting
                    ? p.slotPos[i] + (p.slotPos[i] - p.slotPos[(i - p.shiftDir + n) % n]) * 0.5f
                    : p.slotPos[(i + p.shiftDir + n) % n];
                p.slots[i].anchoredPosition = Vector2.Lerp(p.slotPos[i], to, m);
                p.halos[i].rectTransform.anchoredPosition = p.slots[i].anchoredPosition;

                float dim = Smooth01(Mathf.Clamp01(k / DimEnd));                       // 0 → 1: sönükleşme
                float lit = exiting ? 0f : Smooth01(Mathf.InverseLerp(LightStart, 1f, k));   // 0 → 1: yeniden yanma
                float b = Mathf.Lerp(1f, ShiftDim, dim * (1f - lit));
                float a = exiting ? 1f - m : Mathf.Lerp(1f, ShiftDimAlpha, dim * (1f - lit));
                p.lamps[i].color = new Color(b, b, b, a);
            }
            if (p.shiftTime < total) return true;

            p.shiftTime = -1f;
            ResetSlots(p);
            for (int i = 0; i < n; i++) ApplySlot(p, i, ColorAt(p.path[i]));
            if (p.closed) return false;   // halkada uçtan çıkan lamba komşu hücreye zaten kaydı
            p.popSlot = p.shiftDir > 0 ? 0 : n - 1;
            p.popTime = 0f;
        }

        // Döngüden giren lamba: başında sönükten tam parlaklığa yanar.
        if (p.popTime < 0f) return false;
        p.popTime += dt;
        float pk = Smooth01(Mathf.Clamp01(p.popTime / PopSeconds));
        float pb = Mathf.Lerp(ShiftDim, 1f, pk);
        p.lamps[p.popSlot].color = new Color(pb, pb, pb, pk);
        if (pk < 1f) return true;
        p.lamps[p.popSlot].color = Color.white;
        p.popTime = -1f;
        return false;
    }

    private static float Smooth01(float t) => t * t * (3f - 2f * t);

    private static void ResetSlots(Piece p)
    {
        for (int i = 0; i < p.slots.Length; i++)
        {
            p.slots[i].anchoredPosition = p.slotPos[i];
            p.slots[i].localScale = Vector3.one;
            p.lamps[i].color = Color.white;
            p.halos[i].rectTransform.anchoredPosition = p.slotPos[i];
        }
    }

    // Son lamba kırıldı: kısa titreme → çerçeve yol boyunca 2-3 parçaya ayrılır, savrulup düşerek söner.
    private void StartCollapse(Piece p)
    {
        p.collapseTime = 0f;
        bool midShift = p.shiftTime >= 0f || p.popTime >= 0f;
        p.shiftTime = p.popTime = -1f;
        if (midShift && p.slots != null) ResetSlots(p);   // yarım kalan dalga: yuvalar yerine, dönüş sıfır

        var held = new List<Vector2Int>(p.path.Count);
        foreach (int c in p.path) held.Add(new Vector2Int(c % width, c / width));
        p.hold = board != null ? board.HoldCells(held) : null;
        p.job = board != null ? board.BeginJob(BoardController.BoardJobKind.ObstacleCollapse) : null;

        int n = p.path.Count;
        int chunkCount = n >= 6 ? 3 : n >= 2 ? 2 : 1;
        p.chunks = new RectTransform[chunkCount];
        p.chunkVel = new Vector2[chunkCount];
        p.chunkSpin = new float[chunkCount];
        int layer = root.gameObject.layer;
        for (int k = 0; k < chunkCount; k++)
        {
            int a = k * n / chunkCount, b = (k + 1) * n / chunkCount;
            Vector2 center = Vector2.zero;
            for (int i = a; i < b; i++) center += p.slotPos[i];
            center /= Mathf.Max(1, b - a);

            var chunk = MakeRect($"Chunk{k}", p.root, layer);
            p.chunks[k] = chunk;
            chunk.anchorMin = chunk.anchorMax = new Vector2(0f, 1f);
            chunk.anchoredPosition = center;
            for (int i = a; i < b; i++)
            {
                p.frames[i].rectTransform.SetParent(chunk, true);
                p.slots[i].SetParent(chunk, true);
                p.halos[i].enabled = false;
            }
            float side = chunkCount == 1 ? 0f : Mathf.Lerp(-1f, 1f, k / (float)(chunkCount - 1));
            p.chunkVel[k] = new Vector2(side * tileSize * 2.2f + Random.Range(-0.4f, 0.4f) * tileSize,
                                        tileSize * Random.Range(2.6f, 3.4f));
            p.chunkSpin[k] = Random.Range(-140f, 140f) + side * 90f;
        }

        // Parça ancak çöküş tamamen kurulduktan sonra "çöküyor" sayılır: kurulum yarıda kalırsa Update yarım
        // veriyle (chunks yok) TickCollapse'a girmesin.
        p.collapsing = true;
        var animator = board != null ? board.boardAnimatorRef : null;
        if (animator != null) board.StartCoroutine(animator.MicroShake(0.16f, 8f));
        Wake(p);
    }

    private bool TickCollapse(Piece p, float dt)
    {
        if (p.root == null || p.chunks == null) { FinishCollapse(p); return false; }
        p.collapseTime += dt;
        if (p.collapseTime < CrackSeconds)
        {
            // Çatlama: parçalar yerinde titrer.
            for (int k = 0; k < p.chunks.Length; k++)
                p.chunks[k].localRotation = Quaternion.Euler(0f, 0f, Random.Range(-2.5f, 2.5f));
            return true;
        }

        if (p.hold != null) { p.hold.Dispose(); p.hold = null; }   // hücreler boşalır, taşlar akabilir
        float t = p.collapseTime - CrackSeconds;
        float gravity = tileSize * 14f;
        float fade = 1f - Mathf.Clamp01((t - FallSeconds * 0.55f) / (FallSeconds * 0.45f));
        for (int k = 0; k < p.chunks.Length; k++)
        {
            var c = p.chunks[k];
            p.chunkVel[k].y -= gravity * dt;
            c.anchoredPosition += p.chunkVel[k] * dt;
            c.localRotation = Quaternion.Euler(0f, 0f, c.localEulerAngles.z + p.chunkSpin[k] * dt);
            float s = Mathf.Lerp(1f, 0.85f, t / FallSeconds);
            c.localScale = new Vector3(s, s, 1f);
        }
        SetAlpha(p, fade);
        if (t < FallSeconds) return true;
        FinishCollapse(p);
        return false;
    }

    private void FinishCollapse(Piece p)
    {
        p.hold?.Dispose();
        p.hold = null;
        p.job?.Dispose();
        p.job = null;
        if (p.root != null) Destroy(p.root.gameObject);
        p.root = null;
    }

    private static void SetAlpha(Piece p, float a)
    {
        for (int i = 0; i < p.frames.Length; i++)
        {
            p.frames[i].canvasRenderer.SetAlpha(a);
            p.lamps[i].canvasRenderer.SetAlpha(a);
        }
    }

    private void ReleasePiece(Piece p)
    {
        p.hold?.Dispose();
        p.hold = null;
        p.job?.Dispose();
        p.job = null;
        if (p.root != null) Destroy(p.root.gameObject);
        p.root = null;
    }

    // ── Yardımcılar ─────────────────────────────────────────────────

    private int ColorAt(int cell) => colorByCell.TryGetValue(cell, out int c) ? c : LampStripColor.None;

    private int LitCount(Piece p)
    {
        int lit = 0;
        foreach (int c in p.path) if (ColorAt(c) != LampStripColor.None) lit++;
        return lit;
    }

    private static ChestColorMask ToMask(int color) => color switch
    {
        LampStripColor.Yellow => ChestColorMask.Gear,
        LampStripColor.Red => ChestColorMask.Core,
        LampStripColor.Blue => ChestColorMask.Bolt,
        LampStripColor.Green => ChestColorMask.Plate,
        _ => ChestColorMask.None,
    };

    private RectTransform MakeLayer(string name, Transform parent, int layer)
    {
        var rt = MakeRect(name, parent, layer);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private Image MakeImage(string name, Transform parent, int layer, Sprite sprite, Vector2 pos)
    {
        var rt = MakeRect(name, parent, layer);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(tileSize, tileSize);
        rt.anchoredPosition = pos;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static RectTransform MakeRect(string name, Transform parent, int layer)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static Sprite[] ResolveSprites(LevelData level)
    {
        var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(ObstacleId.LampStrip) : null;
        if (def?.auxiliarySprites == null || def.auxiliarySprites.Count < SpriteCount)
        {
            if (GameDiagnostics.CriticalErrors)
                Debug.LogWarning("[LampStrip] ObstacleDef.auxiliarySprites eksik (17 sprite gerekli): görsel kapalı.");
            return null;
        }
        var result = new Sprite[SpriteCount];
        for (int i = 0; i < SpriteCount; i++) result[i] = def.auxiliarySprites[i];
        return result;
    }
}
