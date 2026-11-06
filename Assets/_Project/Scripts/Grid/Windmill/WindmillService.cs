using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yel değirmeni (ObstacleId.Windmill, 2×2) + un çuvalı (FlourSack / FlourSackV) + un (Flour) — kurallar + görünüm.
///  - Dört renkli kanat (mavi, sarı, kırmızı, yeşil). Kanatlar YALNIZ special/joker ile kırılır (kullanıcı kararı
///    2026-11-01: sıra önemli değil); normal eşleşme vurmaz. Special değirmenin hangi hücresine çarparsa o köşeye o an
///    en yakın duran kanat kırılır (duvar ailesi: hücre başına vuruş; sözleşme LampStrip ile aynı).
///  - Değirmen beklerken kanatlar yavaşça sürekli döner (yalnız görünüm). Maliyeti küçük tutmak için her değirmen kendi
///    alt Canvas'ında: her kare yalnız o 6 görsel yeniden çizilir, tahta katmanı değil.
///  - Son kanat kırılınca değirmen yıkılır ve rastgele, normal taş olan bir 1×2 alana çuval fırlatır. Çuval VERİSİ
///    yıkıldığı anda yazılır (hedef sayacı arada boşa düşmesin); görünüm uçuş bitince çizilir.
///  - Çuval tek vuruşta patlar → 5×5 alanda alt katmanı boş, engelsiz hücrelere un (taşın altında). Hedef = un.
/// Görsel: gövde + 4 kanat (göbek noktası etrafında döner) + göbek; sprite'lar ObstacleDef.auxiliarySprites
/// (gövde, mavi, sarı, kırmızı, yeşil, göbek). Tek Update döngüsü; oynayan iş yokken servis kapalı.
/// Sesler merkezi (board.Audio): kanat kırılma/yıkılma genel engel yolundan (hitSound/breakSound); dönme =
/// Windmill.releaseSound, çuval fırlatma = FlourSack.flightLoopSound (tek sefer), iniş = FlourSack.impactSound,
/// un patlaması = FlourSack.breakSound (genel yol). Boş klip = sessiz.
/// </summary>
public sealed class WindmillService : MonoBehaviour
{
    private const int SpriteBody = 0, SpriteBlue = 1, SpriteYellow = 2, SpriteRed = 3, SpriteGreen = 4, SpriteHub = 5;
    private const int SpriteCount = 6;
    // Göbek noktası, 1254'lük kaynak tuvalinde (628, 593) → normalize (sol-alt orijinli pivot).
    private static readonly Vector2 HubPivot = new Vector2(628f / 1254f, 1f - 593f / 1254f);

    // Köşe sırası saat yönü: 0 sol-üst, 1 sağ-üst, 2 sağ-alt, 3 sol-alt. color[] indeksi = renk−1
    // (LampStripColor: 1 sarı, 2 kırmızı, 3 mavi, 4 yeşil); 0 = o kanat kırık.

    private const float SpinDegreesPerSecond = 28f;   // bekleme dönüşü (saat yönü)
    private const float GustSeconds = 0.7f;            // hamle sonu rüzgâr esintisi: kısa hızlanma + gıcırtı sesi
    private const float GustBoost = 9f;                // esinti başında hız çarpanı (sönerek 1'e iner)
    private static readonly bool UseSubCanvas = false;           // değirmen başına alt Canvas (performans); görünürlük testi sonrası açılacak
    private const float FlingSeconds = 0.65f;
    private const float FlightSeconds = 0.85f;
    private const float FlightArcTiles = 3.2f;
    private const float SquashSeconds = 0.3f;
    private static readonly Color FlourColor = new Color(0.98f, 0.95f, 0.88f, 1f);
    private const int DefaultFlourPerSack = 25; // hedefte Un yoksa: tam 5×5
    /// Çuval başına dökülecek un = hedefteki Un ÷ başlangıçtaki değirmen sayısı (tasarımcı hedeften belirler).
    /// Çuval tam bu kadar hücreye döker (merkeze en yakın boş hücrelerden: önce 5×5, gerekirse bir halka dışarı),
    /// böylece hedef sayacı baştan toplamı gösterir ve yalnız temizledikçe azalır.
    public int FlourPerSack { get; private set; } = DefaultFlourPerSack;
    private readonly List<Vector2Int> spreadBuffer = new();

    private sealed class Mill
    {
        public int origin;
        public readonly int[] cells = new int[4];        // köşe → hücre index
        public readonly int[] color = new int[4];        // köşe → kanat rengi (0 = kırık)
        public RectTransform root;
        public Image body, hub;
        public readonly Image[] sails = new Image[5];    // renk → kanat Image (1..4)
        public readonly float[] angle = new float[5];    // renk → anlık açı (savrulan kanat için)
        public float spin;                               // ortak dönüş açısı (derece, saat yönü negatif)
        public float gust = -1f;                         // esinti zamanı (−1 = yok)
        public readonly float[] flingTime = { -1f, -1f, -1f, -1f, -1f };
        public readonly Vector2[] flingVel = new Vector2[5];
    }

    private sealed class Flight
    {
        public Image image, shadow;
        public Vector2 from, to;          // fx kökü yerel koordinatı
        public float time;
        public int originX, originY;
        public System.IDisposable job;
    }

    // Tek seferlik süsler (toz/un bulutu, iniş esnemesi): değirmen başına birkaç kez → havuz yerine oluştur/yok et.
    private sealed class Puff
    {
        public Image image;
        public Vector2 from, to;
        public float s0, s1, a0, a1, time, duration, spin;
    }

    private sealed class Squash
    {
        public RectTransform target;
        public Vector3 baseScale;
        public float time;
    }

    private BoardController board;
    private ObstacleStateService boundState;
    private LevelData level;
    private RectTransform root;
    private int width, height;
    private float tileSize;
    private Sprite[] sprites;
    private readonly Dictionary<int, Mill> mills = new();
    private readonly Dictionary<int, int> millOfCell = new();
    private readonly List<Mill> active = new();
    private readonly List<Flight> flights = new();
    private readonly List<Puff> puffs = new();
    private readonly List<Squash> squashes = new();
    // Çuval görünümü (iniş anında yakalanır): GridSpawner yok-edildi olayında kendi kaydını bizden önce silebilir;
    // parçalanma bu referansla oynar (Destroy kare sonunda uygulanır, sprite+konum o ana kadar okunur).
    private readonly Dictionary<int, Image> sackViews = new();
    private readonly List<Vector2Int> candidateBuffer = new();
    private System.Func<int, int, ObstacleHitContext, TileType?, WallHitOutcome> hitHandler;
    private System.Func<int, bool> litHandler;

    public static WindmillService Ensure(BoardController board)
    {
        if (board == null) return null;
        if (!board.TryGetComponent<WindmillService>(out var service))
        {
            service = board.gameObject.AddComponent<WindmillService>();
            board.OnPlayerMoveResolved += service.HandleMoveResolved;
        }
        service.board = board;
        return service;
    }

    public void Build(LevelData levelData, RectTransform parent, float cellSize)
    {
        Clear();
        level = levelData;
        if (board == null || level?.obstacles == null || level.obstacleOrigins == null || parent == null) return;
        root = parent;
        width = level.width;
        height = level.height;
        tileSize = cellSize;
        sprites = ResolveSprites(level);
        Bind(board.ObstacleStateService);

        for (int i = 0; i < level.obstacles.Length; i++)
            if ((ObstacleId)level.obstacles[i] == ObstacleId.Windmill && level.obstacleOrigins[i] == i)
                BuildMill(i);
        FlourPerSack = DefaultFlourPerSack;
        if (mills.Count > 0 && level.goals != null)
            foreach (var g in level.goals)
                if (g != null && g.targetType == LevelGoalTargetType.Obstacle && g.obstacleId == ObstacleId.Flour && g.amount > 0)
                    FlourPerSack = Mathf.Max(1, g.amount / mills.Count);
        enabled = active.Count > 0;
    }

    public void Clear()
    {
        foreach (var m in mills.Values) if (m.root != null) Destroy(m.root.gameObject);
        foreach (var f in flights)
        {
            f.job?.Dispose();
            if (f.image != null) Destroy(f.image.gameObject);
            if (f.shadow != null) Destroy(f.shadow.gameObject);
        }
        foreach (var p in puffs) if (p.image != null) Destroy(p.image.gameObject);
        foreach (var q in squashes) if (q.target != null) q.target.localScale = q.baseScale;
        puffs.Clear();
        squashes.Clear();
        sackViews.Clear();
        mills.Clear();
        millOfCell.Clear();
        active.Clear();
        flights.Clear();
    }

    private void OnDestroy()
    {
        Clear();
        Bind(null);
        if (board != null) board.OnPlayerMoveResolved -= HandleMoveResolved;
    }

    private void Bind(ObstacleStateService state)
    {
        if (boundState == state) return;
        if (boundState != null)
        {
            if (boundState.WindmillHitInterceptor == hitHandler) boundState.WindmillHitInterceptor = null;
            if (boundState.WindmillCellLitQuery == litHandler) boundState.WindmillCellLitQuery = null;
            boundState.OnObstacleDestroyed -= HandleObstacleDestroyed;
        }
        boundState = state;
        if (boundState == null) return;
        hitHandler ??= HandleHit;
        litHandler ??= IsLitAt;
        boundState.WindmillHitInterceptor = hitHandler;
        boundState.WindmillCellLitQuery = litHandler;
        boundState.OnObstacleDestroyed += HandleObstacleDestroyed;
    }

    // ── Sorgular ────────────────────────────────────────────────────

    public bool IsLitAt(int cell) =>
        millOfCell.TryGetValue(cell, out int origin) && mills.TryGetValue(origin, out var m) && SailsLeft(m) > 0;

    public void CollectViewRoots(List<Transform> result)
    {
        foreach (var m in mills.Values) if (m.root != null) result.Add(m.root);
    }

    // ── Kural ───────────────────────────────────────────────────────

    private WallHitOutcome HandleHit(int origin, int cell, ObstacleHitContext context, TileType? source)
    {
        if (!mills.TryGetValue(origin, out var m)) return WallHitOutcome.Ignored;
        if (context == ObstacleHitContext.NormalMatch) return WallHitOutcome.Ignored;   // yalnız special/joker
        int corner = CornerOf(m, cell);
        if (corner < 0 || SailsLeft(m) == 0) return WallHitOutcome.Ignored;

        int color = NearestSail(m, corner);
        m.color[color - 1] = 0;
        if (boundState != null) boundState.LampStripLastHitColor = ToMask(color);
        StartFling(m, color);

        int left = SailsLeft(m);
        if (left == 0) return WallHitOutcome.Collapse;   // temizlik ObstacleStateService'te → HandleObstacleDestroyed
        boundState?.SetWallRemainingHits(origin, ObstacleId.Windmill, left);
        return WallHitOutcome.Advanced;
    }

    // Vurulan köşenin yönüne o an en yakın duran kanat (kanatlar sürekli döndüğü için köşe sabit değil).
    private static int NearestSail(Mill m, int corner)
    {
        float target = CornerAngle(corner), best = float.MaxValue;
        int pick = 0;
        for (int c = 1; c <= 4; c++)
        {
            if (m.color[c - 1] == 0) continue;
            float d = Mathf.Abs(Mathf.DeltaAngle(HomeAngle(c) + m.spin, target));
            if (d < best) { best = d; pick = c; }
        }
        return pick;
    }

    // UI açıları (y yukarı): sol-üst 135°, sağ-üst 45°, sağ-alt −45°, sol-alt −135°.
    private static float CornerAngle(int corner) => corner switch { 0 => 135f, 1 => 45f, 2 => -45f, _ => -135f };
    private static float HomeAngle(int color) => color switch
    {
        LampStripColor.Blue => 135f, LampStripColor.Yellow => 45f, LampStripColor.Red => -45f, _ => -135f,
    };

    private static int SailsLeft(Mill m)
    {
        int left = 0;
        for (int k = 0; k < 4; k++) if (m.color[k] != 0) left++;
        return left;
    }

    // Oyuncu hamlesi oturdu: değirmene rüzgâr vurur — kanatlar kısa süre hızlanır, gıcırtı sesi bir kez çalar.
    private void HandleMoveResolved()
    {
        bool any = false;
        foreach (var m in mills.Values)
        {
            if (m.root == null || SailsLeft(m) == 0) continue;
            m.gust = 0f;
            Wake(m);
            any = true;
        }
        if (any) PlayClip(ObstacleId.Windmill, d => (d.releaseSound, d.releaseSoundVolume));
    }

    private void HandleObstacleDestroyed(int origin, ObstacleId id)
    {
        if (id == ObstacleId.Windmill && mills.TryGetValue(origin, out var m))
        {
            mills.Remove(origin);
            foreach (int c in m.cells) millOfCell.Remove(c);
            CollapseView(m);
            ThrowSack(origin);
            return;
        }
        if (id == ObstacleId.FlourSack || id == ObstacleId.FlourSackV)
        {
            bool vertical = id == ObstacleId.FlourSackV;
            int x = origin % width, y = origin / width;
            // Çuval kendi görselinden 2-3 büyük parçaya ayrılıp savrulur (ortak ObstacleShatterFx havuzu) + un bulutu.
            if (!sackViews.TryGetValue(origin, out var view) || view == null) view = board.ObstacleViewByOriginLookup?.Invoke(origin);
            sackViews.Remove(origin);
            var vfx = board.BoardVfxPlayer != null ? board.BoardVfxPlayer.VfxRoot : null;
            if (view != null && vfx != null) ObstacleShatterFx.Play(board, board, vfx, view, 2);
            FlourCloud(vertical ? CellCenter(x, y + 0.5f) : CellCenter(x + 0.5f, y));
            SpreadFlour(x, y, vertical ? 1 : 2, vertical ? 2 : 1);
        }
    }

    // ── Çuval + un ──────────────────────────────────────────────────

    private void ThrowSack(int millOrigin)
    {
        if (boundState == null || level == null) return;
        int mx = millOrigin % width, my = millOrigin / width;
        candidateBuffer.Clear();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (CanHoldSack(x, y) && CanHoldSack(x + 1, y)) candidateBuffer.Add(new Vector2Int(x * 2, y));      // yatay
                if (CanHoldSack(x, y) && CanHoldSack(x, y + 1)) candidateBuffer.Add(new Vector2Int(x * 2 + 1, y));  // dikey
            }

        Vector2 from = CellCenter(mx + 0.5f, my + 0.5f);
        while (candidateBuffer.Count > 0)
        {
            int pick = Random.Range(0, candidateBuffer.Count);
            var c = candidateBuffer[pick];
            candidateBuffer.RemoveAt(pick);
            bool vertical = (c.x & 1) == 1;
            int x = c.x >> 1, y = c.y;
            if (!boundState.TrySpawnFlourSack(x, y, vertical)) continue;
            var to = vertical ? CellCenter(x, y + 0.5f) : CellCenter(x + 0.5f, y);
            StartFlight(from, to, x, y, vertical);
            return;
        }
        // Uygun 1×2 yoksa çuval değirmenin yerinde patlamış sayılır: un doğrudan değirmen alanına yayılır.
        SpreadFlour(mx, my, 2, 2);
    }

    private bool CanHoldSack(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return false;
        int i = y * width + x;
        if (level.cells[i] != (int)CellType.Normal || (ObstacleId)level.obstacles[i] != ObstacleId.None) return false;
        var tile = board.Tiles[x, y];
        if (tile == null || board.GridData[x, y] == null) return false;
        if (!tile.IsRuntimeIdle || tile.WasDragging) return false;
        if (tile.GetSpecial() != TileSpecial.None || tile.GetTileType() == TileType.Key
            || DimerdalRules.IsDiamond(tile.GetTileType())) return false;
        if (board.IsPendingTriggeredSpecialCell(x, y) || board.IsReservedTileTargetCell(x, y)) return false;
        return true;
    }

    /// Çuval SABİT bir alana döker: merkezine en yakın FlourPerSack konum (25 → 5×5). Alan tahta sınırı/delik/engel/
    /// zaten unlu/ıslak kanal hücresine denk gelirse o un kaybolur ve hedef sayacı o kadar azalır (kullanıcı kuralı
    /// 2026-11-01: "unlu bir kareye dağılıyorsa bir tane olsun hedef azalsın.. kenardaysa boşluğa denk geliyorsa yine
    /// goals'teki azalmalı"). Eskiden yer bulamayan un dışa doğru taşıp uzak hücrelere yayılıyordu.
    private void SpreadFlour(int x0, int y0, int w, int h)
    {
        if (boundState == null) return;
        float cx = x0 + (w - 1) * 0.5f, cy = y0 + (h - 1) * 0.5f;
        int r = Mathf.CeilToInt(Mathf.Sqrt(FlourPerSack) * 0.5f) + 1;
        int ix = Mathf.FloorToInt(cx), iy = Mathf.FloorToInt(cy);
        spreadBuffer.Clear();
        for (int y = iy - r; y <= iy + r + 1; y++)
            for (int x = ix - r; x <= ix + r + 1; x++)
                spreadBuffer.Add(new Vector2Int(x, y));   // tahta dışı dahil: alan konumdur, hücre değil
        // Halka mesafesi (Chebyshev) önce, eşitlikte gerçek uzaklık: 25 konum = tam 5×5.
        spreadBuffer.Sort((p, q) =>
        {
            float rp = Mathf.Max(Mathf.Abs(p.x - cx), Mathf.Abs(p.y - cy)), rq = Mathf.Max(Mathf.Abs(q.x - cx), Mathf.Abs(q.y - cy));
            if (!Mathf.Approximately(rp, rq)) return rp.CompareTo(rq);
            float dp = (p.x - cx) * (p.x - cx) + (p.y - cy) * (p.y - cy), dq = (q.x - cx) * (q.x - cx) + (q.y - cy) * (q.y - cy);
            return dp.CompareTo(dq);
        });
        int count = Mathf.Min(FlourPerSack, spreadBuffer.Count);
        for (int k = 0; k < count; k++)
        {
            var c = spreadBuffer[k];
            if (c.x < 0 || c.y < 0 || c.x >= width || c.y >= height) continue;
            int i = c.y * width + c.x;
            if (level.cells[i] != (int)CellType.Normal || (ObstacleId)level.obstacles[i] != ObstacleId.None) continue;
            // Oyun anında delik olan hücre (su kanalı çeşmesi/deposu: MarkRuntimePassThroughHole) taş tutmaz → un orada
            // görünmez ve temizlenemez (hedef bitmezdi). O konuma düşen un kaybolur, hedef azalır (kullanıcı 2026-11-02).
            if (board.IsMaskHoleCell(c.x, c.y)) continue;
            if (board.WaterChannelWetQuery != null && board.WaterChannelWetQuery(i)) continue;   // un suyun altına girmez
            if (!boundState.TrySpawnSingleCellObstacleAt(c.x, c.y, ObstacleId.Flour)) continue;
            board.RaiseObstacleCreatedDynamic(c.x, c.y);
        }
        board.RequestResolveAfterActionSequence();
    }

    // Uçuş en üst efekt katmanında (taş/engellerin üstünde) oynar; koordinatlar değirmen kökünden çevrilir.
    private RectTransform FxRoot => board != null && board.BoardVfxPlayer != null && board.BoardVfxPlayer.VfxRoot != null
        ? board.BoardVfxPlayer.VfxRoot : root;

    private Vector2 ToFx(Vector2 anchored)
    {
        var fx = FxRoot;
        if (fx == root) return anchored;
        Rect r = root.rect;
        Vector3 world = root.TransformPoint(new Vector3(r.xMin + anchored.x, r.yMax + anchored.y, 0f));
        return fx.InverseTransformPoint(world);
    }

    private float FxScale()
    {
        var fx = FxRoot;
        return fx == root || fx.lossyScale.x == 0f ? 1f : root.lossyScale.x / fx.lossyScale.x;
    }

    private Image MakeFx(string name, Sprite sprite, Vector2 pos, Vector2 size, Color color)
    {
        var fx = FxRoot;
        var rt = MakeRect(name, fx, fx.gameObject.layer);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.localPosition = pos;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private void StartFlight(Vector2 from, Vector2 to, int ox, int oy, bool vertical)
    {
        var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(vertical ? ObstacleId.FlourSackV : ObstacleId.FlourSack) : null;
        float k = FxScale(), cell = tileSize * k;
        Vector2 size = vertical ? new Vector2(cell, cell * 2f) : new Vector2(cell * 2f, cell);
        Vector2 a = ToFx(from), b = ToFx(to);
        // İneceği yerde gölge: çuval yaklaştıkça koyulaşıp büyür → oyuncu düşeceği yeri görür.
        var shadow = MakeFx("FlourSackShadow", FlightTrailFx.GlowSprite(), b, size * 1.15f, new Color(0f, 0f, 0f, 0f));
        var img = MakeFx("FlourSackFlight", def != null ? def.GetPreviewSprite() : null, a, size, Color.white);
        img.rectTransform.localScale = Vector3.zero;
        PlayClip(vertical ? ObstacleId.FlourSackV : ObstacleId.FlourSack, d => (d.flightLoopSound, d.flightLoopSoundVolume));   // fırlatma (tek sefer)
        flights.Add(new Flight
        {
            image = img, shadow = shadow, from = a, to = b, time = 0f, originX = ox, originY = oy,
            job = board.BeginJob(BoardController.BoardJobKind.ObstacleSpread),
        });
        enabled = true;
    }

    private bool TickFlight(Flight f, float dt)
    {
        f.time += dt;
        float k = Mathf.Clamp01(f.time / FlightSeconds);
        float cell = tileSize * FxScale();
        if (f.image != null)
        {
            var rt = f.image.rectTransform;
            // Yatay ilerleme yumuşak, dikey yay yüksek (yerçekimi hissi: inişte hızlanır).
            float e = k * k * (3f - 2f * k);
            Vector2 p = Vector2.Lerp(f.from, f.to, e);
            p.y += 4f * k * (1f - k) * cell * FlightArcTiles;
            rt.localPosition = p;
            // Değirmenden fırlarken büyüyerek çıkar, havada iri, inerken gerçek boyuna döner.
            float s = k < 0.18f ? Mathf.Lerp(0f, 1.3f, k / 0.18f) : Mathf.Lerp(1.3f, 1f, (k - 0.18f) / 0.82f);
            rt.localScale = new Vector3(s, s, 1f);
            rt.localRotation = Quaternion.Euler(0f, 0f, (1f - e) * 360f);
        }
        if (f.shadow != null)
        {
            f.shadow.color = new Color(0f, 0f, 0f, 0.45f * k);
            float ss = Mathf.Lerp(0.35f, 1f, k);
            f.shadow.rectTransform.localScale = new Vector3(ss, ss * 0.6f, 1f);
        }
        if (k < 1f) return true;
        if (f.image != null) Destroy(f.image.gameObject);
        if (f.shadow != null) Destroy(f.shadow.gameObject);
        f.job?.Dispose();
        f.job = null;
        // Veri çoktan yazıldı; çuval o arada vurulmadıysa görünümünü çiz ve yere oturtma esnemesi ver.
        int i = f.originY * width + f.originX;
        var id = (ObstacleId)level.obstacles[i];
        if ((id == ObstacleId.FlourSack || id == ObstacleId.FlourSackV) && level.obstacleOrigins[i] == i)
        {
            board.RaiseObstacleCreatedDynamic(f.originX, f.originY);
            var view = board.ObstacleViewByOriginLookup?.Invoke(i);
            if (view != null)
            {
                sackViews[i] = view;
                squashes.Add(new Squash { target = view.rectTransform, baseScale = view.rectTransform.localScale });
            }
        }
        // Toz halkası: inişte çevreye kısa süren krem bulutçuklar.
        for (int n = 0; n < 8; n++)
        {
            float ang = n * Mathf.PI * 2f / 8f + Random.Range(-0.2f, 0.2f);
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f);
            AddPuff(f.to, f.to + dir * cell * Random.Range(0.9f, 1.3f), cell * 0.7f, 0.4f, 1.1f, 0.85f, 0.45f);
        }
        PlayClip(ObstacleId.FlourSack, d => (d.impactSound, d.impactSoundVolume));
        var animator = board.boardAnimatorRef;
        if (animator != null) board.StartCoroutine(animator.MicroShake(0.16f, 10f));
        board.RequestResolveAfterActionSequence();
        return false;
    }

    // Çuval patlayınca 5×5 alana yayılan un bulutu (un hücreleri aynı anda altta belirir).
    private void FlourCloud(Vector2 anchoredCenter)
    {
        Vector2 c = ToFx(anchoredCenter);
        float cell = tileSize * FxScale();
        AddPuff(c, c, cell * 2.2f, 0.3f, 2.6f, 0.95f, 0.6f);
        for (int n = 0; n < 12; n++)
        {
            Vector2 to = c + new Vector2(Random.Range(-2.2f, 2.2f), Random.Range(-2.2f, 2.2f)) * cell;
            AddPuff(c, to, cell * Random.Range(0.8f, 1.3f), 0.4f, Random.Range(1.2f, 1.8f), 0.9f, Random.Range(0.5f, 0.75f));
        }
        enabled = true;
    }

    private void AddPuff(Vector2 from, Vector2 to, float size, float s0, float s1, float alpha, float duration)
    {
        var img = MakeFx("FlourPuff", FlightTrailFx.GlowSprite(), from, Vector2.one * size, FlourColor);
        img.rectTransform.localScale = Vector3.one * s0;
        puffs.Add(new Puff { image = img, from = from, to = to, s0 = s0, s1 = s1, a0 = alpha, a1 = 0f, duration = duration,
                             spin = Random.Range(-90f, 90f) });
        enabled = true;
    }

    private bool TickPuff(Puff p, float dt)
    {
        if (p.image == null) return false;
        p.time += dt;
        float k = Mathf.Clamp01(p.time / p.duration);
        float e = 1f - (1f - k) * (1f - k);
        var rt = p.image.rectTransform;
        rt.localPosition = Vector2.Lerp(p.from, p.to, e);
        float s = Mathf.Lerp(p.s0, p.s1, e);
        rt.localScale = new Vector3(s, s, 1f);
        rt.localRotation = Quaternion.Euler(0f, 0f, p.spin * k);
        var col = FlourColor; col.a = Mathf.Lerp(p.a0, p.a1, k);
        p.image.color = col;
        if (k < 1f) return true;
        Destroy(p.image.gameObject);
        return false;
    }

    // İniş esnemesi: yassılaş → hafif uzayıp toparlan.
    private static bool TickSquash(Squash q, float dt)
    {
        if (q.target == null) return false;
        q.time += dt;
        float k = Mathf.Clamp01(q.time / SquashSeconds);
        float sx, sy;
        if (k < 0.35f) { float t = k / 0.35f; sx = Mathf.Lerp(1.25f, 0.92f, t); sy = Mathf.Lerp(0.7f, 1.1f, t); }
        else { float t = (k - 0.35f) / 0.65f; sx = Mathf.Lerp(0.92f, 1f, t); sy = Mathf.Lerp(1.1f, 1f, t); }
        q.target.localScale = new Vector3(q.baseScale.x * sx, q.baseScale.y * sy, q.baseScale.z);
        if (k < 1f) return true;
        q.target.localScale = q.baseScale;
        return false;
    }

    // ── Görünüm ─────────────────────────────────────────────────────

    private void BuildMill(int origin)
    {
        int x = origin % width, y = origin / width;
        var m = new Mill { origin = origin };
        m.cells[0] = origin; m.cells[1] = origin + 1; m.cells[2] = origin + width + 1; m.cells[3] = origin + width;
        for (int k = 0; k < 4; k++) { m.color[k] = k + 1; millOfCell[m.cells[k]] = origin; }   // renk k+1 duruyor
        mills[origin] = m;
        boundState?.SetWallRemainingHits(origin, ObstacleId.Windmill, 4);

        int layer = root.gameObject.layer;
        m.root = MakeRect($"Windmill_{origin}", root, layer);
        m.root.anchorMin = m.root.anchorMax = new Vector2(0f, 1f);
        m.root.pivot = new Vector2(0.5f, 0.5f);
        m.root.sizeDelta = new Vector2(tileSize * 2f, tileSize * 2f);
        m.root.anchoredPosition = CellCenter(x + 0.5f, y + 0.5f);
        // Sürekli dönen kanatlar tahta Canvas'ını her kare yeniden kurdurmasın: değirmen kendi alt Canvas'ında.
        // Şimdilik KAPALI: ilk cihaz testinde değirmen görünmedi; görünürlük doğrulanınca Canvas katman çalışmasıyla açılacak.
        if (UseSubCanvas) m.root.gameObject.AddComponent<Canvas>();
        if (sprites == null)
        {
            // Parça görselleri okunamadıysa en azından düz değirmen çizilsin (görünmez engel kalmasın).
            var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(ObstacleId.Windmill) : null;
            m.body = MakeFill("Body", m.root, layer, def != null ? def.GetPreviewSprite() : null, new Vector2(0.5f, 0.5f));
            return;
        }
        m.body = MakeFill("Body", m.root, layer, sprites[SpriteBody], new Vector2(0.5f, 0.5f));
        for (int c = 1; c <= 4; c++)
            m.sails[c] = MakeFill($"Sail{c}", m.root, layer, sprites[SailSprite(c)], HubPivot);
        m.hub = MakeFill("Hub", m.root, layer, sprites[SpriteHub], new Vector2(0.5f, 0.5f));
        m.spin = Random.Range(0f, 90f);   // değirmenler aynı fazda dönmesin
        Wake(m);
    }

    private void StartFling(Mill m, int color)
    {
        if (m.sails[color] == null) return;
        float a = (HomeAngle(color) + m.spin) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        m.angle[color] = m.spin;
        m.flingVel[color] = dir * tileSize * 3.2f + Vector2.up * tileSize * 2.4f;
        m.flingTime[color] = 0f;
        Wake(m);
    }

    private void CollapseView(Mill m)
    {
        if (m.root == null) return;
        // Gövde kendi görselinden 2-3 büyük parçaya ayrılır (ortak ObstacleShatterFx havuzu); göbek de gider.
        if (m.body != null && board.BoardVfxPlayer != null && board.BoardVfxPlayer.VfxRoot != null)
            ObstacleShatterFx.Play(board, board, board.BoardVfxPlayer.VfxRoot, m.body, 4);
        active.Remove(m);
        Destroy(m.root.gameObject);
        m.root = null;
    }

    private void Wake(Mill m)
    {
        if (!active.Contains(m)) active.Add(m);
        enabled = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var m = active[i];
            if (m.root == null || !TickMill(m, dt)) active.RemoveAt(i);
        }
        for (int i = flights.Count - 1; i >= 0; i--)
            if (!TickFlight(flights[i], dt)) flights.RemoveAt(i);
        for (int i = puffs.Count - 1; i >= 0; i--)
            if (!TickPuff(puffs[i], dt)) puffs.RemoveAt(i);
        for (int i = squashes.Count - 1; i >= 0; i--)
            if (!TickSquash(squashes[i], dt)) squashes.RemoveAt(i);
        if (active.Count == 0 && flights.Count == 0 && puffs.Count == 0 && squashes.Count == 0) enabled = false;
    }

    private bool TickMill(Mill m, float dt)
    {
        // Bekleme dönüşü: kalan kanatlar ortak açıyla döner (sprite'lar kendi yönlerinde çizili, göbek pivotu).
        float speed = SpinDegreesPerSecond;
        if (m.gust >= 0f)
        {
            m.gust += dt;
            float g = Mathf.Clamp01(m.gust / GustSeconds);
            speed *= 1f + (GustBoost - 1f) * (1f - g) * (1f - g);   // hızlı başla, yumuşakça yavaşla
            if (g >= 1f) m.gust = -1f;
        }
        m.spin = Mathf.Repeat(m.spin - speed * dt, 360f);
        var rot = Quaternion.Euler(0f, 0f, m.spin);
        for (int c = 1; c <= 4; c++)
            if (m.sails[c] != null && m.color[c - 1] != 0) m.sails[c].rectTransform.localRotation = rot;

        for (int c = 1; c <= 4; c++)
        {
            if (m.flingTime[c] < 0f || m.sails[c] == null) continue;
            m.flingTime[c] += dt;
            var rt = m.sails[c].rectTransform;
            m.flingVel[c].y -= tileSize * 12f * dt;
            rt.anchoredPosition += m.flingVel[c] * dt;
            m.angle[c] += (c % 2 == 0 ? -1f : 1f) * 260f * dt;
            rt.localRotation = Quaternion.Euler(0f, 0f, m.angle[c]);
            float t = m.flingTime[c] / FlingSeconds;
            m.sails[c].canvasRenderer.SetAlpha(1f - Mathf.Clamp01((t - 0.45f) / 0.55f));
            if (t >= 1f)
            {
                m.sails[c].gameObject.SetActive(false);
                m.flingTime[c] = -2f;   // bitti
            }
        }
        return true;   // değirmen ayakta olduğu sürece döner (yıkılınca CollapseView listeden çıkarır)
    }

    // ── Yardımcılar ─────────────────────────────────────────────────

    private static int CornerOf(Mill m, int cell)
    {
        for (int k = 0; k < 4; k++) if (m.cells[k] == cell) return k;
        return -1;
    }

    private static int SailSprite(int color) => color switch
    {
        LampStripColor.Blue => SpriteBlue,
        LampStripColor.Yellow => SpriteYellow,
        LampStripColor.Red => SpriteRed,
        _ => SpriteGreen,
    };

    private static ChestColorMask ToMask(int color) => color switch
    {
        LampStripColor.Yellow => ChestColorMask.Gear,
        LampStripColor.Red => ChestColorMask.Core,
        LampStripColor.Blue => ChestColorMask.Bolt,
        LampStripColor.Green => ChestColorMask.Plate,
        _ => ChestColorMask.None,
    };

    private Vector2 CellCenter(float x, float y) => new Vector2(x * tileSize + tileSize * 0.5f, -(y * tileSize + tileSize * 0.5f));

    private void PlayClip(ObstacleId id, System.Func<ObstacleDef, (AudioClip, float)> pick)
    {
        var def = level?.obstacleLibrary != null ? level.obstacleLibrary.Get(id) : null;
        if (def == null || board == null || board.Audio == null) return;
        var (clip, vol) = pick(def);
        if (clip != null) board.Audio.PlayOneShotClip(clip, vol);
    }

    private static Image MakeFill(string name, RectTransform parent, int layer, Sprite sprite, Vector2 pivot)
    {
        var rt = MakeRect(name, parent, layer);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = pivot;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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
        var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(ObstacleId.Windmill) : null;
        if (def?.auxiliarySprites == null || def.auxiliarySprites.Count < SpriteCount)
        {
            Debug.LogError($"[Windmill] ObstacleDef bulunamadı ya da auxiliarySprites eksik (6 gerekli, def={(def != null)}, " +
                           $"aux={(def?.auxiliarySprites != null ? def.auxiliarySprites.Count : -1)}): düz önizleme çiziliyor.");
            return null;
        }
        var result = new Sprite[SpriteCount];
        for (int i = 0; i < SpriteCount; i++) result[i] = def.auxiliarySprites[i];
        return result;
    }
}
