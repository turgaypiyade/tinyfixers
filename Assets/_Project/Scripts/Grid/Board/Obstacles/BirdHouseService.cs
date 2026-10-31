using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Guguk kuşu evi (ObstacleId.BirdHouse) — 1x1, KIRILMAZ üretici (KeyGenerator kalıbı).
///  - Kapalı ev vurulunca kapak açılır, kuş yuvanın derininden küçük başlayıp büyüyerek tüneğe oturur.
///  - Kuş görünürken vurulursa kuş hedefe (Collectible Bird) uçar ve kapak AÇIK kalır: yuvadan hemen yeni kuş çıkar
///    (kullanıcı kararı 2026-10-31: vuruş sürdükçe ev açık, her vuruş bir kuş). Bir hamle boyunca (zinciri dahil)
///    evin yanında vuruş olmazsa hamle oturduğunda kuş içeri girer, kapak kapanır, döngü baştan başlar.
///  - Ortak kota: evler hedef (Bird goal amount) dolana kadar sınırsız kuş verir; dolunca hepsi kapanır, kararır ve
///    vuruş almaz. Hedefte Bird yoksa ev etkisiz bir engeldir.
///  - Normal eşleşme/zincir vuruşlarında ev hamle başına en fazla BİR kuş verir (tek kombo hedefi boşaltmasın);
///    special/joker vuruşları bu sınıra takılmaz.
/// Görsel: ev görüntüsünün (GridSpawner'ın engel Image'ı) içine açık ev + kuş (gövde, iki kanat) + tünek katmanları
/// eklenir; tüm evler ve uçan kuşlar bu servisin TEK Update döngüsünde oynar, uçan kuşlar havuzdan gelir.
/// Performans (Android): ev katmanları ev başına bir kez kurulur; beklerken her kare transform yazılmaz (yalnız ara
/// sıra kanat karesi değişir → tuval yalnız o an yeniden toplanır); uçan kuşun kendi alt Canvas'ı var (HUD tuvali her
/// karede yeniden toplanmaz); iş kalmayınca servis kendini kapatır (Update çağrılmaz).
/// Sprite'lar ObstacleDef.auxiliarySprites sırasıyla: açık ev, tünek, gövde, sol kanat 1-3, sağ kanat 1-3.
/// Sahne kurulumu yok: GridSpawner level çizerken Ensure(board).Build çağırır.
/// </summary>
public sealed class BirdHouseService : MonoBehaviour
{
    private const int SpriteOpened = 0;
    private const int SpritePerch = 1;
    private const int SpriteBody = 2;
    private const int SpriteWingL = 3;   // 3,4,5 = sol kanat 1 (yukarı), 2 (orta), 3 (yatay)
    private const int SpriteWingR = 6;   // 6,7,8 = sağ kanat 1-3
    private const int SpriteCount = 9;

    // Kuş yerleşimi (ev hücresine oranla): genişlik 0.86 hücre, ayaklar tüneğin üst kenarına basar.
    private const float BirdLeft = 0.07f;
    private const float BirdRight = 0.93f;
    private const float BirdBottom = 0.205f;
    private const float BirdAspect = 624f / 812f;
    private const float HiddenDrop = 0.12f;       // yuvanın derini: küçükken bu kadar aşağıda
    private const float HiddenScale = 0.3f;
    private const float HiddenTint = 0.45f;

    private const float EmergeSeconds = 0.32f;
    private const float TaDaSeconds = 0.16f;      // çıkışın sonunda kanatlar yatay açık bekler
    private const float RetreatSeconds = 0.24f;
    private const float EmptyDoorSeconds = 0.14f; // son kuş uçtuktan sonra kapak kapanmadan önce
    private const float ReloadSeconds = 0.2f;     // kuş uçtuktan sonra yuvadan yenisi çıkmadan önce
    private const float PulseSeconds = 0.18f;
    private const float FastFlapFps = 22f;
    private const float IdleFlapFps = 16f;
    private const int IdleFlapFrames = 8;          // 1-2-3-2 iki tur
    private const float IdleFlapMinGap = 1.5f;
    private const float IdleFlapMaxGap = 3f;

    private const float CrouchSeconds = 0.08f;
    private const float FlySeconds = 0.5f;
    private const float ExhaustedTint = 0.62f;

    private static readonly int[] FlapCycle = { 0, 1, 2, 1 };
    private static readonly Vector3[] CornerBuffer = new Vector3[4];   // kanat karesi indeksleri: 1 → 2 → 3 → 2
    private const int RestWing = 1;
    private const int TaDaWing = 2;

    private enum Phase { Closed, Emerging, Waiting, Retreating, EmptyOpen }

    private sealed class House
    {
        public int origin;
        public bool open;               // mantık: kuş dışarıda (vuruş penceresi açık)
        public bool hitSinceMoveEnd;
        public bool releasedThisMove;

        // Görünüm
        public Image host;
        public Vector3 hostScale = Vector3.one;
        public RectTransform root;
        public Image opened;
        public Image perch;
        public RectTransform bird;
        public Image body;
        public Image wingL;
        public Image wingR;
        public Phase phase;
        public float time;
        public int pendingReleases;     // çıkış animasyonu bitince kalkacak kuş sayısı (special çoklu vuruş)
        public bool pendingRetreat;
        public float nextIdleFlap;
        public float idleFlapTime = -1f;
        public float pulseTime = -1f;
        public float pulseSign = 1f;
    }

    private sealed class Flyer
    {
        public RectTransform root;
        public Image body;
        public Image wingL;
        public Image wingR;
        public Vector2 start;
        public Vector2 control;
        public Vector2 end;
        public Vector2 size;
        public float endScale;
        public float time;
        public bool counted;            // kota hedefe yazılacak mı (goal varsa)
    }

    private BoardController board;
    private ObstacleStateService boundState;
    private LevelData level;
    private int width;
    private int capacity;
    private int released;
    private bool exhausted;
    private RectTransform cachedFlyRoot;
    private Sprite[] sprites;
    private readonly Dictionary<int, House> housesByOrigin = new();
    private readonly List<House> houses = new();
    private readonly List<Flyer> flyers = new();
    private readonly Stack<Flyer> flyerPool = new();
    private System.Func<int, ObstacleHitContext, bool> hitHandler;
    private System.Func<bool> canProduceHandler;
    private System.Action<int, ObstacleId> destroyedHandler;

    public static BirdHouseService Ensure(BoardController board)
    {
        if (board == null) return null;
        if (!board.TryGetComponent<BirdHouseService>(out var service))
        {
            service = board.gameObject.AddComponent<BirdHouseService>();
            board.OnPlayerMoveResolved += service.HandleMoveResolved;
            board.OnObstacleViewRestored += service.HandleViewRestored;
        }
        service.board = board;
        return service;
    }

    /// Level kurulurken çağrılır: kotayı hedeften okur, durumları sıfırlar, vuruş yönlendirmesini bağlar.
    public void Build(LevelData levelData)
    {
        Clear();
        level = levelData;
        if (board == null || level == null) return;

        width = level.width;
        capacity = ResolveCapacity(level);
        released = 0;
        exhausted = false;
        cachedFlyRoot = null;
        sprites = ResolveSprites(level);
        Bind(board.ObstacleStateService);
    }

    public void Clear()
    {
        for (int i = 0; i < houses.Count; i++)
            DestroyView(houses[i]);
        houses.Clear();
        housesByOrigin.Clear();

        // Uçuştaki kuşlar: level gidiyor → sayaç yazılmaz, yalnız board işi dengelenir.
        for (int i = flyers.Count - 1; i >= 0; i--)
            FinishFlyer(flyers[i], notifyGoal: false);
        flyers.Clear();
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
        foreach (var f in flyerPool)
            if (f.root != null) Destroy(f.root.gameObject);
        flyerPool.Clear();
    }

    private void Bind(ObstacleStateService state)
    {
        if (boundState != null)
        {
            if (boundState.BirdHouseHitInterceptor == hitHandler) boundState.BirdHouseHitInterceptor = null;
            if (boundState.BirdHouseCanProduceQuery == canProduceHandler) boundState.BirdHouseCanProduceQuery = null;
            if (destroyedHandler != null) boundState.OnObstacleDestroyed -= destroyedHandler;
        }
        boundState = state;
        if (boundState == null) return;

        hitHandler ??= HandleHit;
        canProduceHandler ??= CanProduce;
        destroyedHandler ??= HandleObstacleDestroyed;
        boundState.BirdHouseHitInterceptor = hitHandler;
        boundState.BirdHouseCanProduceQuery = canProduceHandler;
        boundState.OnObstacleDestroyed -= destroyedHandler;
        boundState.OnObstacleDestroyed += destroyedHandler;
    }

    // ── Kural ───────────────────────────────────────────────────────

    private bool CanProduce()
    {
        if (exhausted || released >= capacity) return false;
        var hud = board != null ? board.TopHud : null;
        return hud != null && hud.HasGoalForCollectible(CollectibleId.Bird);
    }

    private bool HandleHit(int origin, ObstacleHitContext context)
    {
        if (!CanProduce()) return false;

        var house = GetHouse(origin);
        bool capped = context == ObstacleHitContext.NormalMatch || context == ObstacleHitContext.Scripted;
        if (house.releasedThisMove && capped) return true;   // bu hamle zaten kuş verdi

        house.hitSinceMoveEnd = true;
        if (!house.open)
        {
            house.open = true;
            RequestEmerge(house);
            return true;
        }

        // Kuş uçar; kota kalıyorsa ev açık kalır ve yuvadan yeni kuş çıkar (RequestRelease → yeniden çıkış).
        house.releasedThisMove = true;
        released++;
        house.open = released < capacity;
        RequestRelease(house);
        if (released >= capacity) ExhaustAll();
        return true;
    }

    // Oyuncu hamlesi (zinciri dahil) oturdu: bu hamlede vurulmayan açık evin kuşu içeri girer.
    private void HandleMoveResolved()
    {
        for (int i = 0; i < houses.Count; i++)
        {
            var house = houses[i];
            if (house.open && !house.hitSinceMoveEnd)
            {
                house.open = false;
                RequestRetreat(house);
            }
            house.hitSinceMoveEnd = false;
            house.releasedThisMove = false;
        }
    }

    private void ExhaustAll()
    {
        exhausted = true;
        for (int i = 0; i < houses.Count; i++)
        {
            var house = houses[i];
            if (!house.open) continue;
            house.open = false;
            RequestRetreat(house);
        }

        if (level?.obstacles == null || level.obstacleOrigins == null || board?.ObstacleViewByOriginLookup == null)
            return;
        int size = Mathf.Min(level.obstacles.Length, level.obstacleOrigins.Length);
        for (int i = 0; i < size; i++)
        {
            if ((ObstacleId)level.obstacles[i] != ObstacleId.BirdHouse || level.obstacleOrigins[i] != i) continue;
            TintExhausted(board.ObstacleViewByOriginLookup(i));
        }
    }

    private static void TintExhausted(Image host)
    {
        if (host != null) host.color = new Color(ExhaustedTint, ExhaustedTint, ExhaustedTint, host.color.a);
    }

    // Üstündeki kasa/taş kırılıp ev açığa çıktı: eski görünüm geçersiz; kota bitmişse yeni görünüm kararık başlar.
    private void HandleViewRestored(int x, int y)
    {
        if (board?.ObstacleStateService == null) return;
        if (board.ObstacleStateService.GetObstacleIdAt(x, y) != ObstacleId.BirdHouse) return;
        int origin = board.ObstacleStateService.GetObstacleOriginAt(x, y);
        if (housesByOrigin.TryGetValue(origin, out var house)) DestroyView(house);
        if (exhausted && board.ObstacleViewByOriginLookup != null)
            TintExhausted(board.ObstacleViewByOriginLookup(origin));
    }

    private void HandleObstacleDestroyed(int origin, ObstacleId id)
    {
        if (id != ObstacleId.BirdHouse || !housesByOrigin.TryGetValue(origin, out var house)) return;
        DestroyView(house);
        houses.Remove(house);
        housesByOrigin.Remove(origin);
    }

    private House GetHouse(int origin)
    {
        if (housesByOrigin.TryGetValue(origin, out var house)) return house;
        house = new House { origin = origin };
        housesByOrigin[origin] = house;
        houses.Add(house);
        return house;
    }

    // ── Görünüm istekleri ───────────────────────────────────────────

    private void RequestEmerge(House house)
    {
        if (!EnsureView(house)) return;
        house.pendingRetreat = false;
        house.phase = Phase.Emerging;
        house.time = 0f;
        ShowOpen(house, true);
        StartPulse(house, 1f);
        PlaySound(d => d.birdAppearSound, d => d.birdAppearSoundVolume);
        PlaySound(d => d.birdWingSound, d => d.birdWingSoundVolume);
    }

    private void RequestRelease(House house)
    {
        if (house.root == null || house.phase == Phase.Closed)
        {
            // Görünüm yok (ör. lookup başarısız): kuş hücre merkezinden kalkar.
            LaunchFlyer(house);
            return;
        }
        if (house.phase == Phase.Emerging)
        {
            house.pendingReleases++;   // kuş yerine oturunca kalkar; her vuruş kendi kuşunu uçurur (sayaç kaybolmaz)
            return;
        }
        LaunchFlyer(house);
        house.bird.gameObject.SetActive(false);
        // Ev açık kaldıysa (kota var) kısa bekleyişten sonra yeni kuş çıkar; yoksa kapak kapanır.
        house.phase = Phase.EmptyOpen;
        house.time = 0f;
    }

    // Kapak açık kalır; yuvanın derininden yeni kuş aynı çıkış animasyonuyla gelir. Sessiz: guguk yalnız kapak ilk
    // açılırken, kanat+uçuş sesi kalkışta çalar (seri sürerken ses yığılmasın).
    private void StartReload(House house)
    {
        house.pendingRetreat = false;
        house.phase = Phase.Emerging;
        house.time = 0f;
        house.bird.gameObject.SetActive(true);
        ApplyEmerge(house, 0f);
    }

    private void RequestRetreat(House house)
    {
        if (house.root == null) return;
        if (house.phase == Phase.Emerging)
        {
            house.pendingRetreat = true;
            return;
        }
        if (house.phase == Phase.EmptyOpen) return;   // yuva boş: open=false olduğu için kapak kendiliğinden kapanır
        if (house.phase != Phase.Waiting) return;
        Wake();
        house.phase = Phase.Retreating;
        house.time = 0f;
        PlaySound(d => d.birdRetreatSound, d => d.birdRetreatSoundVolume);
    }

    private void StartPulse(House house, float sign)
    {
        Wake();
        house.pulseTime = 0f;
        house.pulseSign = sign;
    }

    private void ShowOpen(House house, bool open)
    {
        if (house.root == null) return;
        house.root.gameObject.SetActive(open);
        house.bird.gameObject.SetActive(open);
        if (house.host != null) house.host.canvasRenderer.SetAlpha(open ? 0f : 1f);
    }

    private void CloseDoor(House house)
    {
        house.phase = Phase.Closed;
        house.time = 0f;
        ShowOpen(house, false);
        StartPulse(house, -1f);
    }

    // ── Döngü ───────────────────────────────────────────────────────

    private void Update()
    {
        float dt = Time.deltaTime;
        bool busy = flyers.Count > 0;
        for (int i = 0; i < houses.Count; i++)
        {
            var house = houses[i];
            TickHouse(house, dt);
            busy |= house.root != null && (house.phase != Phase.Closed || house.pulseTime >= 0f);
        }
        for (int i = flyers.Count - 1; i >= 0; i--)
            TickFlyer(flyers[i], dt, i);

        // Oynayan ev / uçan kuş yoksa Update hiç çağrılmasın; her görünüm isteği Wake ile açar.
        if (!busy && flyers.Count == 0) enabled = false;
    }

    private void Wake()
    {
        if (!enabled) enabled = true;
    }

    private void TickHouse(House house, float dt)
    {
        if (house.root == null) return;
        if (house.host == null)
        {
            DestroyView(house);
            return;
        }

        TickPulse(house, dt);
        if (house.phase == Phase.Closed) return;
        house.time += dt;

        switch (house.phase)
        {
            case Phase.Emerging:
            {
                float k = Mathf.Clamp01(house.time / EmergeSeconds);
                ApplyEmerge(house, k);
                SetWings(house, FlapCycle[(int)(house.time * FastFlapFps) % FlapCycle.Length]);
                if (k < 1f) break;

                house.phase = Phase.Waiting;
                house.time = 0f;
                house.idleFlapTime = -1f;
                ApplyBird(house, 1f, 1f, 0f, 1f, 1f);   // bekleme pozu bir kez; beklerken transform yazılmaz
                house.nextIdleFlap = TaDaSeconds + Random.Range(IdleFlapMinGap, IdleFlapMaxGap);
                SetWings(house, TaDaWing);
                if (house.pendingReleases > 0)
                {
                    int count = house.pendingReleases;
                    house.pendingReleases = 0;
                    for (int r = 0; r < count; r++) RequestRelease(house);
                }
                else if (house.pendingRetreat) RequestRetreat(house);
                house.pendingReleases = 0; house.pendingRetreat = false;
                break;
            }
            case Phase.Waiting:
                TickWaiting(house, dt);
                break;
            case Phase.Retreating:
            {
                float k = Mathf.Clamp01(house.time / RetreatSeconds);
                ApplyRetreat(house, k);
                if (k >= 1f) CloseDoor(house);
                break;
            }
            case Phase.EmptyOpen:
                if (house.open)
                {
                    if (house.time >= ReloadSeconds) StartReload(house);
                }
                else if (house.time >= EmptyDoorSeconds)
                {
                    CloseDoor(house);
                }
                break;
        }
    }

    // Küçük ve koyu (yuvanın derininde) → büyüyerek yerine → %110 taşma + gövde uzar → %96 basılır → %100.
    private void ApplyEmerge(House house, float k)
    {
        float s, sx = 1f, sy = 1f, drop, tint;
        if (k < 0.56f)
        {
            float e = EaseOut(k / 0.56f);
            s = Mathf.Lerp(HiddenScale, 1.1f, e);
            drop = Mathf.Lerp(HiddenDrop, 0f, e);
            tint = Mathf.Lerp(HiddenTint, 1f, e);
            sx = 0.96f;
            sy = 1.05f;
        }
        else if (k < 0.75f)
        {
            float e = (k - 0.56f) / 0.19f;
            s = Mathf.Lerp(1.1f, 0.96f, e);
            drop = 0f;
            tint = 1f;
            sx = Mathf.Lerp(0.96f, 1.05f, e);
            sy = Mathf.Lerp(1.05f, 0.93f, e);
        }
        else
        {
            float e = EaseOut((k - 0.75f) / 0.25f);
            s = Mathf.Lerp(0.96f, 1f, e);
            drop = 0f;
            tint = 1f;
            sx = Mathf.Lerp(1.05f, 1f, e);
            sy = Mathf.Lerp(0.93f, 1f, e);
        }
        ApplyBird(house, s * sx, s * sy, drop, tint, 1f);
    }

    // Önce hafif sıkışır, sonra küçülüp kararak yuvanın derinine iner.
    private void ApplyRetreat(House house, float k)
    {
        if (k < 0.25f)
        {
            float e = Mathf.Sin(k / 0.25f * Mathf.PI * 0.5f);
            ApplyBird(house, Mathf.Lerp(1f, 1.06f, e), Mathf.Lerp(1f, 0.9f, e), 0f, 1f, 1f);
            SetWings(house, 0);
            return;
        }
        float t = (k - 0.25f) / 0.75f;
        float e2 = t * t;
        float s = Mathf.Lerp(1f, HiddenScale, e2);
        float alpha = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f;
        ApplyBird(house, s * 1.06f, s * 0.9f, Mathf.Lerp(0f, HiddenDrop, e2), Mathf.Lerp(1f, HiddenTint, e2), alpha);
        SetWings(house, 0);
    }

    // Beklerken yalnız kanat SPRITE'ı değişir (kare değişmedikçe hiçbir şey yazılmaz).
    private void TickWaiting(House house, float dt)
    {
        if (house.time < TaDaSeconds) return;

        if (house.idleFlapTime >= 0f)
        {
            house.idleFlapTime += dt;
            int frame = (int)(house.idleFlapTime * IdleFlapFps);
            if (frame >= IdleFlapFrames)
            {
                house.idleFlapTime = -1f;
                house.nextIdleFlap = house.time + Random.Range(IdleFlapMinGap, IdleFlapMaxGap);
                SetWings(house, RestWing);
            }
            else
            {
                SetWings(house, FlapCycle[frame % FlapCycle.Length]);
            }
            return;
        }

        SetWings(house, RestWing);
        if (house.time >= house.nextIdleFlap) house.idleFlapTime = 0f;   // sessiz çırpma
    }

    private void ApplyBird(House house, float sx, float sy, float drop, float tint, float alpha)
    {
        house.bird.localScale = new Vector3(sx, sy, 1f);
        float h = house.root.rect.height;
        house.bird.anchoredPosition = new Vector2(0f, -drop * h);
        var c = new Color(tint, tint, tint, alpha);
        house.body.color = c;
        house.wingL.color = c;
        house.wingR.color = c;
    }

    private void SetWings(House house, int frame) => SetWings(house.wingL, house.wingR, frame);

    private void SetWings(Image left, Image right, int frame)
    {
        if (sprites == null) return;
        var l = sprites[SpriteWingL + frame];
        var r = sprites[SpriteWingR + frame];
        if (left.sprite != l) left.sprite = l;
        if (right.sprite != r) right.sprite = r;
    }

    // Kapak açılırken ev hafifçe sıkışıp esner; kapanırken küçük bir geri sekme.
    private void TickPulse(House house, float dt)
    {
        if (house.pulseTime < 0f) return;
        house.pulseTime += dt;
        float k = Mathf.Clamp01(house.pulseTime / PulseSeconds);
        float wave = Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
        float amp = house.pulseSign > 0f ? 0.06f : 0.04f;
        var rt = house.host.rectTransform;
        rt.localScale = new Vector3(house.hostScale.x * (1f + amp * wave),
                                    house.hostScale.y * (1f - amp * 0.8f * wave), house.hostScale.z);
        if (k >= 1f)
        {
            rt.localScale = house.hostScale;
            house.pulseTime = -1f;
        }
    }

    // ── Görünüm kurulumu ────────────────────────────────────────────

    private bool EnsureView(House house)
    {
        if (house.root != null && house.host != null) return true;
        if (house.root != null) DestroyView(house);
        if (sprites == null || board?.ObstacleViewByOriginLookup == null) return false;

        var host = board.ObstacleViewByOriginLookup(house.origin);
        if (host == null) return false;

        house.host = host;
        house.hostScale = host.rectTransform.localScale;
        int layer = host.gameObject.layer;

        house.root = CreateRect("BirdHouseOpen", host.rectTransform, layer);
        Stretch(house.root);
        house.opened = CreateImage("Opened", house.root, layer, sprites[SpriteOpened]);
        Stretch(house.opened.rectTransform);

        house.bird = CreateRect("Bird", house.root, layer);
        house.bird.anchorMin = new Vector2(BirdLeft, BirdBottom);
        house.bird.anchorMax = new Vector2(BirdRight, BirdBottom + (BirdRight - BirdLeft) * BirdAspect);
        house.bird.pivot = new Vector2(0.5f, 0f);   // ayaklardan büyür/basılır
        house.bird.offsetMin = house.bird.offsetMax = Vector2.zero;
        BuildBird(house.bird, layer, out house.body, out house.wingL, out house.wingR);

        house.perch = CreateImage("Perch", house.root, layer, sprites[SpritePerch]);
        Stretch(house.perch.rectTransform);

        house.phase = Phase.Closed;
        house.root.gameObject.SetActive(false);
        return true;
    }

    private void BuildBird(RectTransform bird, int layer, out Image body, out Image wingL, out Image wingR)
    {
        // Kanatlar gövdenin ARKASINDA; üç kare aynı kanvasta hizalı.
        wingL = CreateImage("WingL", bird, layer, sprites[SpriteWingL + RestWing]);
        wingR = CreateImage("WingR", bird, layer, sprites[SpriteWingR + RestWing]);
        body = CreateImage("Body", bird, layer, sprites[SpriteBody]);
        Stretch(wingL.rectTransform);
        Stretch(wingR.rectTransform);
        Stretch(body.rectTransform);
    }

    private void DestroyView(House house)
    {
        if (house.host != null)
        {
            house.host.canvasRenderer.SetAlpha(1f);
            house.host.rectTransform.localScale = house.hostScale;
        }
        if (house.root != null) Destroy(house.root.gameObject);
        house.root = null;
        house.host = null;
        house.bird = null;
        house.phase = Phase.Closed;
        house.pulseTime = -1f;
        house.pendingReleases = 0; house.pendingRetreat = false;
    }

    private static RectTransform CreateRect(string name, Transform parent, int layer)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image CreateImage(string name, Transform parent, int layer, Sprite sprite)
    {
        var rt = CreateRect(name, parent, layer);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = false;
        return image;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    // ── Hedefe uçuş ─────────────────────────────────────────────────

    private void LaunchFlyer(House house)
    {
        var hud = board != null ? board.TopHud : null;
        RectTransform target = null;
        bool goal = hud != null && hud.HasGoalForCollectible(CollectibleId.Bird);
        if (goal) hud.TryGetGoalTargetRectForCollectible(CollectibleId.Bird, out target);
        RectTransform flyRoot = ResolveFlyRoot(target);
        if (flyRoot == null || sprites == null)
        {
            if (goal) hud.NotifyCollectibleCollected(CollectibleId.Bird, 1);
            return;
        }

        // Kuşun tahtadaki dünya dikdörtgeni → uçuş kökünün yerel koordinatı.
        Vector3 worldCenter;
        Vector2 worldSize;
        if (house.bird != null && house.bird.gameObject.activeInHierarchy)
        {
            var corners = CornerBuffer;
            house.bird.GetWorldCorners(corners);
            worldCenter = (corners[0] + corners[2]) * 0.5f;
            worldSize = new Vector2(Vector3.Distance(corners[0], corners[3]), Vector3.Distance(corners[0], corners[1]));
        }
        else
        {
            int x = house.origin % width, y = house.origin / width;
            worldCenter = board.GetCellWorldCenterPosition(x, y);
            float cell = board.TileSize * (board.ContentRoot != null ? board.ContentRoot.lossyScale.x : 1f);
            worldSize = new Vector2(cell * 0.86f, cell * 0.86f * BirdAspect);
        }

        float rootScale = Mathf.Max(0.0001f, flyRoot.lossyScale.x);
        var flyer = RentFlyer(flyRoot);
        flyer.size = worldSize / rootScale;
        flyer.root.sizeDelta = flyer.size;
        flyer.start = flyRoot.InverseTransformPoint(worldCenter);
        flyer.end = target != null
            ? (Vector2)flyRoot.InverseTransformPoint(target.TransformPoint(target.rect.center))
            : flyer.start + new Vector2(0f, flyRoot.rect.height * 0.5f);
        float targetHeight = target != null ? target.rect.height * target.lossyScale.y / rootScale : flyer.size.y * 0.5f;
        flyer.endScale = Mathf.Clamp(targetHeight / Mathf.Max(1f, flyer.size.y), 0.2f, 1f);

        Vector2 delta = flyer.end - flyer.start;
        float side = delta.x >= 0f ? -1f : 1f;   // önce ters yana hafif kavis, sonra hedefe
        flyer.control = (flyer.start + flyer.end) * 0.5f
                        + new Vector2(side * Mathf.Abs(delta.y) * 0.25f, delta.magnitude * 0.18f);
        flyer.time = 0f;
        flyer.counted = goal;
        flyer.root.anchoredPosition = flyer.start;
        flyer.root.localScale = Vector3.one;
        flyer.root.localRotation = Quaternion.identity;
        flyer.root.SetAsLastSibling();
        SetFlyerColor(flyer, 1f);
        flyers.Add(flyer);
        Wake();

        // Level sonu kuş varmadan sonuç göstermez; board akışı beklemez (goal-orb kalıbı).
        if (goal) board.BeginGoalOrbFlight();
        PlaySound(d => d.birdWingSound, d => d.birdWingSoundVolume);
        PlaySound(d => d.birdFlySound, d => d.birdFlySoundVolume);
    }

    private void TickFlyer(Flyer f, float dt, int index)
    {
        if (f.root == null)
        {
            flyers.RemoveAt(index);
            FinishFlyer(f, notifyGoal: true);
            return;
        }

        f.time += dt;
        SetWings(f.wingL, f.wingR, FlapCycle[(int)(f.time * FastFlapFps) % FlapCycle.Length]);

        if (f.time < CrouchSeconds)
        {
            // Kalkış öncesi çömelme.
            float c = Mathf.Sin(f.time / CrouchSeconds * Mathf.PI * 0.5f);
            f.root.localScale = new Vector3(1f + 0.08f * c, 1f - 0.14f * c, 1f);
            return;
        }

        float k = Mathf.Clamp01((f.time - CrouchSeconds) / FlySeconds);
        float e = k * k * (3f - 2f * k);
        Vector2 pos = Bezier(f.start, f.control, f.end, e);
        Vector2 ahead = Bezier(f.start, f.control, f.end, Mathf.Min(1f, e + 0.05f));
        f.root.anchoredPosition = pos;

        float stretch = Mathf.Sin(k * Mathf.PI) * 0.08f;
        float s = Mathf.Lerp(1f, f.endScale, k * k);
        f.root.localScale = new Vector3(s * (1f - stretch), s * (1f + stretch), 1f);
        Vector2 dir = ahead - pos;
        float tilt = dir.sqrMagnitude > 0.01f ? -Mathf.Clamp(dir.x / dir.magnitude, -1f, 1f) * 28f : 0f;
        f.root.localRotation = Quaternion.Euler(0f, 0f, tilt * (1f - k));
        SetFlyerColor(f, k < 0.85f ? 1f : 1f - (k - 0.85f) / 0.15f);

        if (k < 1f) return;
        flyers.RemoveAt(index);
        FinishFlyer(f, notifyGoal: true);
    }

    private void FinishFlyer(Flyer f, bool notifyGoal)
    {
        if (f.counted)
        {
            f.counted = false;
            if (notifyGoal) board?.TopHud?.NotifyCollectibleCollected(CollectibleId.Bird, 1);
            board?.EndGoalOrbFlight();
        }
        if (f.root == null) return;
        f.root.gameObject.SetActive(false);
        flyerPool.Push(f);
    }

    private Flyer RentFlyer(RectTransform parent)
    {
        Flyer f = null;
        while (flyerPool.Count > 0 && f == null)
        {
            f = flyerPool.Pop();
            if (f.root == null) f = null;   // sahne yıkımında yok olmuş
        }
        if (f == null)
        {
            f = new Flyer();
            f.root = CreateRect("BirdHouseFlyer", parent, parent.gameObject.layer);
            f.root.anchorMin = f.root.anchorMax = f.root.pivot = new Vector2(0.5f, 0.5f);
            // Kendi alt Canvas'ı: her karede hareket eden kuş yalnız bu küçük tuvali yeniden toplar, HUD'u değil.
            // Sıralama ezilmez (hiyerarşi sırasıyla en üstte çizilir). Havuzlu → bir kez kurulur.
            f.root.gameObject.AddComponent<Canvas>();
            BuildBird(f.root, parent.gameObject.layer, out f.body, out f.wingL, out f.wingR);
        }
        else if (f.root.parent != parent)
        {
            f.root.SetParent(parent, false);
        }
        SetLayer(f.root, parent.gameObject.layer);
        f.root.gameObject.SetActive(true);
        return f;
    }

    private static void SetLayer(RectTransform root, int layer)
    {
        if (root.gameObject.layer == layer) return;
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++) root.GetChild(i).gameObject.layer = layer;
    }

    private static void SetFlyerColor(Flyer f, float alpha)
    {
        var c = new Color(1f, 1f, 1f, alpha);
        f.body.color = c;
        f.wingL.color = c;
        f.wingR.color = c;
    }

    // Hedef ikonunun kanvası (HUD) en üstte çizilir; yoksa tahta içeriği.
    private RectTransform ResolveFlyRoot(RectTransform target)
    {
        if (cachedFlyRoot != null) return cachedFlyRoot;
        var root = FindFlyRoot(target);
        if (target != null) cachedFlyRoot = root;   // hedefsiz (yedek) kök önbelleğe alınmaz
        return root;
    }

    private RectTransform FindFlyRoot(RectTransform target)
    {
        Canvas canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
        if (canvas == null && board != null && board.ContentRoot != null)
            canvas = board.ContentRoot.GetComponentInParent<Canvas>();
        if (canvas != null) return canvas.rootCanvas.transform as RectTransform;
        return board != null ? board.ContentRoot : null;
    }

    // ── Yardımcılar ─────────────────────────────────────────────────

    private void PlaySound(System.Func<ObstacleDef, AudioClip> clip, System.Func<ObstacleDef, float> volume)
    {
        if (board == null || board.Audio == null) return;
        var def = level?.obstacleLibrary?.Get(ObstacleId.BirdHouse);
        if (def == null) return;
        var c = clip(def);
        if (c != null) board.Audio.PlayOneShotClip(c, volume(def));
    }

    private static int ResolveCapacity(LevelData level)
    {
        int total = 0;
        if (level.goals == null) return 0;
        foreach (var goal in level.goals)
            if (goal != null && goal.targetType == LevelGoalTargetType.Collectible && goal.collectibleId == CollectibleId.Bird)
                total += Mathf.Max(0, goal.amount);
        return total;
    }

    private static Sprite[] ResolveSprites(LevelData level)
    {
        var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(ObstacleId.BirdHouse) : null;
        if (def?.auxiliarySprites == null || def.auxiliarySprites.Count < SpriteCount)
        {
            if (GameDiagnostics.CriticalErrors)
                Debug.LogWarning("[BirdHouse] ObstacleDef.auxiliarySprites eksik (9 sprite gerekli): görsel animasyon kapalı.");
            return null;
        }
        var result = new Sprite[SpriteCount];
        for (int i = 0; i < SpriteCount; i++) result[i] = def.auxiliarySprites[i];
        return result;
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }
}
