using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aç Hamster (ObstacleId.Hamster) — TEK HÜCRE, hareketli engel (kargo/kask altyapısı: tahtada TileView olarak
/// yaşar, altı boşalınca düşer, oyuncu yanındaki taşla yer değiştirebilir). Bu servis yalnız BESLENME + KARE +
/// ZIPLAMA'yı yönetir.
///  - Her vuruş (4-komşu eşleşme / special; ObstacleStateService.HamsterHitInterceptor) = 1 lokma, hasar yok.
///  - Yanak kademeleri: Idle → Cheeks1 → Cheeks2 → Cheeks3. Kalıcı sayaç YOK (karakteri küçültüyordu):
///    her lokmada kalan sayı hamsterin üstünden havaya uçarak söner (eşik 10 → 10, 9, ... 1).
///  - Doyunca: hedef sayacı +1 (TopHud) → PatchBot önceliğiyle hedef seçilir → hamster hedefin yakınındaki bir
///    taşla yer değiştirerek oraya zıplar → indiği hücrenin çevresi (3x3) vurulur. Hedef tamamlandıysa ayrılır.
///  - Level'da hamster HEDEFİ YOKSA: ilk doyumda bir kez zıplar (3x3), inişten sonra ayrılır (yardımcı karakter).
/// Sahne kurulumu yok: GridSpawner.DrawHamsterObstacles → Ensure(board).Build.
/// </summary>
public sealed class HamsterObstacleService : MonoBehaviour
{
    private const string SpritePath = "Hamster/Hamster_";
    private const float ChompSeconds = 0.12f;
    private const float PopSeconds = 0.18f;
    private const float CrouchSeconds = 0.16f;
    private const float JumpSeconds = 0.42f;
    private const float LandSeconds = 0.14f;
    private const float LandHoldSeconds = 0.25f;
    private const float JumpHeightCells = 1.2f;
    private const float LeaveSeconds = 0.75f;
    // Uçuşta hamster yükselirken bu kata kadar büyür, inerken aynı oranda küçülüp normale döner.
    private const float FlightPeakScale = 3f;
    // İniş tozu: yere çarpınca ayak hizasından iki yana saçılan küçük toz bulutları.
    private const int LandingDustPuffs = 6;
    private const float LandingDustSeconds = 0.5f;

    private sealed class HamsterState
    {
        public int food;
        public bool jumping;
        public bool leaveRequested;   // hedefsiz level: zıplama bitti, sıradaki "vuruş" ayrılıştır
        public Coroutine biteRoutine;
    }

    private BoardController board;
    private ObstacleStateService boundState;
    private int satiety = 7;
    private int feedsCompleted;
    private bool decorated;
    private readonly Dictionary<TileView, HamsterState> hamsters = new();
    private readonly Dictionary<string, Sprite> sprites = new();
    private System.Func<int, int, ObstacleHitContext, bool> hitHandler;

    public static HamsterObstacleService Ensure(BoardController board)
    {
        if (board == null) return null;
        if (!board.TryGetComponent<HamsterObstacleService>(out var service))
            service = board.gameObject.AddComponent<HamsterObstacleService>();
        service.board = board;
        return service;
    }

    /// Level kurulurken çağrılır. Hamster taşları (TileView) spawn sonrası ilk karede bulunup giydirilir.
    public void Build(LevelData level)
    {
        Clear();
        if (board == null || level == null) return;
        var def = level.obstacleLibrary != null ? level.obstacleLibrary.Get(ObstacleId.Hamster) : null;
        satiety = Mathf.Max(1, level.hamsterSatietyOverride > 0 ? level.hamsterSatietyOverride
            : def != null ? def.hamsterSatiety : 7);
        feedsCompleted = 0;
        decorated = false;
        Bind(board.ObstacleStateService);
    }

    public void Clear()
    {
        StopAllCoroutines();
        hamsters.Clear();
        decorated = true;
    }

    private void OnDestroy() => Bind(null);

    private void Bind(ObstacleStateService state)
    {
        if (boundState == state) return;
        if (boundState != null && boundState.HamsterHitInterceptor == hitHandler)
            boundState.HamsterHitInterceptor = null;
        boundState = state;
        if (boundState != null)
        {
            hitHandler ??= HandleHit;
            boundState.HamsterHitInterceptor = hitHandler;
        }
    }

    // Taşlar spawn edildikten sonra hamster taşlarını bul ve giydir (kare + sayaç).
    private void LateUpdate()
    {
        if (decorated || board == null || board.Tiles == null || board.ObstacleStateService == null) return;
        var obs = board.ObstacleStateService;
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            if (obs.GetObstacleIdAt(x, y) != ObstacleId.Hamster) continue;
            var tile = board.Tiles[x, y];
            if (tile == null) return;   // henüz spawn olmadı → sonraki kare
            Ensure(tile);
        }
        decorated = true;
    }

    private HamsterState Ensure(TileView tile)
    {
        if (hamsters.TryGetValue(tile, out var h)) return h;
        h = new HamsterState();
        hamsters[tile] = h;
        SetFrame(tile, "Idle");
        return h;
    }

    // ── Beslenme ────────────────────────────────────────────────────

    // true → hedef tamamlandı, hamster ayrılıyor (ObstacleStateService engeli temizler, board taşı kaldırır).
    private bool HandleHit(int origin, int cellIndex, ObstacleHitContext context)
    {
        if (board == null || board.Width <= 0) return false;
        var tile = board.Tiles[cellIndex % board.Width, cellIndex / board.Width];
        if (tile == null) return false;
        var h = Ensure(tile);

        // Hedefsiz level'da zıplama sonrası servis kendi vuruşuyla ayrılışı tetikler (RequestLeave).
        if (h.leaveRequested)
        {
            StartLeave(tile, h);
            hamsters.Remove(tile);
            return true;
        }
        if (h.jumping) return false;   // uçarken lokma sayılmaz

        SpawnFloatingNumber(tile, satiety - h.food);   // bu lokmadan ÖNCE kalan (10, 9, ... 1)
        h.food++;
        if (h.food < satiety)
        {
            PlayBite(tile, h);
            return false;
        }

        h.food = 0;
        feedsCompleted++;
        board.TopHud?.SetHamsterFeedsCompleted(feedsCompleted);

        int goal = board.TopHud != null ? board.TopHud.GetHamsterGoalAmount() : 0;
        if (goal > 0 && feedsCompleted >= goal)
        {
            StartLeave(tile, h);
            hamsters.Remove(tile);
            return true;
        }

        // Hedef yoksa: tek zıplama, sonra ayrılış (goal == 0). Hedef varsa: zıpla, devam et.
        if (!h.jumping)
            StartCoroutine(CoJump(tile, h, leaveAfter: goal <= 0));
        return false;
    }

    private void PlayBite(TileView tile, HamsterState h)
    {
        if (h.jumping) return;
        if (h.biteRoutine != null) StopCoroutine(h.biteRoutine);
        h.biteRoutine = StartCoroutine(CoBite(tile, h));
    }

    private IEnumerator CoBite(TileView tile, HamsterState h)
    {
        var rt = Icon(tile);
        SetFrame(tile, "Chomp");
        yield return Pop(rt, 1.08f, ChompSeconds);
        if (h.jumping || tile == null) yield break;
        SetFrame(tile, CheekFrame(h.food));
        yield return Pop(rt, 1.1f, PopSeconds);
        h.biteRoutine = null;
    }

    private string CheekFrame(int food)
    {
        if (food <= 0) return "Idle";
        int level = 1 + Mathf.Min(2, (food - 1) * 3 / Mathf.Max(1, satiety));
        return "Cheeks" + level;
    }

    // ── Zıplama: hedef seç → yer değiştir → uç → in (3x3 vur) ─────────

    private IEnumerator CoJump(TileView tile, HamsterState h, bool leaveAfter = false)
    {
        h.jumping = true;
        if (h.biteRoutine != null) { StopCoroutine(h.biteRoutine); h.biteRoutine = null; }
        using var job = board.BeginJob(BoardController.BoardJobKind.ObstacleSpread);

        var icon = Icon(tile);
        SetFrame(tile, "Cheeks3");
        yield return Pop(icon, 1.12f, PopSeconds);
        if (tile == null) yield break;

        var from = new Vector2Int(tile.X, tile.Y);
        var land = PickLanding(from);
        float cell = board.TileSize;
        var tileRt = (RectTransform)tile.transform;

        RectTransform swappedRt = null;
        Vector2 delta = new Vector2((land.x - from.x) * cell, -(land.y - from.y) * cell);
        if (land != from)
        {
            var swapped = board.Tiles[land.x, land.y];
            if (board.TeleportSwapTiles(from.x, from.y, land.x, land.y))
                swappedRt = swapped != null ? (RectTransform)swapped.transform : null;
            else
            {
                land = from;
                delta = Vector2.zero;
            }
        }

        // Uçuş boyunca iki hücre akışa kapalı (yerçekimi görselle yarışmasın).
        using (board.HoldCells(new[] { from, land }))
        {
            SetFrame(tile, "Crouch");
            yield return Squash(icon, 1.12f, 0.86f, CrouchSeconds);

            SetFrame(tile, "Jump");
            Vector2 start = tileRt.anchoredPosition;
            Vector2 otherStart = swappedRt != null ? swappedRt.anchoredPosition : Vector2.zero;
            tileRt.SetAsLastSibling();
            float height = Mathf.Max(JumpHeightCells * cell, delta.magnitude * 0.35f);
            float seconds = JumpSeconds + Mathf.Min(0.25f, delta.magnitude / Mathf.Max(1f, cell) * 0.04f);

            // 3 katına büyüyen hamster çim/engel katmanlarının ALTINDA kalmasın: uçuş boyunca VFX
            // köküne (PatchBot uçuşuyla aynı kök) taşınır; hareket o kökün yerel birimlerine çevrilir.
            var flight = BeginFlight(tileRt, delta, height);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (tile == null) yield break;
                float k = t / seconds;
                float arc = Mathf.Sin(k * Mathf.PI);
                float grow = 1f + (FlightPeakScale - 1f) * arc;   // tepeye kadar büyür, inişte aynı oranda küçülür
                if (flight.lifted)
                {
                    tileRt.localPosition = flight.localStart + flight.localDelta * k + flight.localUp * arc;
                    tileRt.localScale = flight.baseScale * grow;
                }
                else
                {
                    tileRt.anchoredPosition = start + delta * k + Vector2.up * (arc * height);
                    tileRt.localScale = Vector3.one * grow;
                }
                if (swappedRt != null)
                {
                    swappedRt.anchoredPosition = otherStart - delta * k;
                    swappedRt.localScale = Vector3.one * (1f - 0.35f * arc);
                }
                yield return null;
            }
            if (tile != null)
            {
                EndFlight(tileRt, flight);
                tileRt.anchoredPosition = start + delta;
                tileRt.localScale = Vector3.one;
            }
            if (swappedRt != null)
            {
                swappedRt.anchoredPosition = otherStart - delta;
                swappedRt.localScale = Vector3.one;
            }

            if (tile != null) SpawnLandingDust(tileRt, cell);
            SetFrame(tile, "Land");
            board.StartCoroutine(board.boardAnimatorRef.MicroShake(0.14f, board.ShakeStrength * 0.8f));
            EnqueueImpact(land);
            yield return Squash(icon, 1.16f, 0.84f, LandSeconds);
        }
        board.RequestResolveAfterActionSequence();

        yield return new WaitForSeconds(LandHoldSeconds);
        if (leaveAfter && tile != null)
        {
            RequestLeave(tile, h);
            yield break;
        }
        if (tile != null) SetFrame(tile, CheekFrame(h.food));
        h.jumping = false;
    }

    private struct FlightState
    {
        public bool lifted;
        public Transform homeParent;
        public Vector3 localStart, localDelta, localUp, baseScale;
        public Transform[] nodes;
        public int[] layers;
    }

    private FlightState BeginFlight(RectTransform tileRt, Vector2 delta, float height)
    {
        var state = new FlightState { homeParent = tileRt.parent };
        var root = FlightRoot();
        if (root == null || state.homeParent == null || root == state.homeParent) return state;

        // Tiles kökündeki hareket (anchored birimleri) → dünya → uçuş kökü yereli.
        state.localDelta = root.InverseTransformVector(state.homeParent.TransformVector(delta));
        state.localUp = root.InverseTransformVector(state.homeParent.TransformVector(Vector3.up * height));

        state.nodes = tileRt.GetComponentsInChildren<Transform>(true);
        state.layers = new int[state.nodes.Length];
        for (int i = 0; i < state.nodes.Length; i++)
        {
            state.layers[i] = state.nodes[i].gameObject.layer;
            state.nodes[i].gameObject.layer = root.gameObject.layer;   // Screen Space Camera culling'i önle
        }

        tileRt.SetParent(root, worldPositionStays: true);
        tileRt.SetAsLastSibling();
        state.localStart = tileRt.localPosition;
        state.baseScale = tileRt.localScale;
        state.lifted = true;
        return state;
    }

    private static void EndFlight(RectTransform tileRt, FlightState state)
    {
        if (!state.lifted || tileRt == null || state.homeParent == null) return;
        tileRt.SetParent(state.homeParent, worldPositionStays: false);
        for (int i = 0; i < state.nodes.Length; i++)
            if (state.nodes[i] != null) state.nodes[i].gameObject.layer = state.layers[i];
    }

    private RectTransform FlightRoot() =>
        board != null && board.BoardVfxPlayer != null ? board.BoardVfxPlayer.VfxRoot : null;

    // Yere çarpma: ayak hizasından iki yana yayılan, büyüyüp sönen küçük toz bulutları.
    private void SpawnLandingDust(RectTransform tileRt, float cell)
    {
        var sprite = Frame("Dust");
        if (sprite == null || tileRt == null) return;
        var root = FlightRoot();
        if (root == null) root = tileRt.parent as RectTransform;
        if (root == null) return;

        // Hücrenin alt kenarına yakın nokta (dünya) → toz kökü yereli; boyutlar da o köke ölçeklenir.
        Vector3 feetWorld = tileRt.TransformPoint(new Vector3(0f, -cell * 0.3f, 0f) - (Vector3)RectCenterOffset(tileRt));
        Vector3 feet = root.InverseTransformPoint(feetWorld);
        float unit = root.InverseTransformVector(tileRt.parent.TransformVector(Vector3.right * cell)).magnitude;

        for (int i = 0; i < LandingDustPuffs; i++)
        {
            var go = new GameObject("HamsterDust", typeof(RectTransform), typeof(Image));
            go.layer = root.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;

            float side = i % 2 == 0 ? -1f : 1f;
            float spread = Random.Range(0.35f, 0.75f);
            var velocity = new Vector3(side * spread, Random.Range(0.05f, 0.3f), 0f) * unit;
            float size = Random.Range(0.32f, 0.5f) * unit;
            rt.sizeDelta = new Vector2(size, size);
            rt.localPosition = feet + new Vector3(side * Random.Range(0f, 0.15f) * unit, 0f, 0f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            StartCoroutine(CoDust(img, velocity));
        }
    }

    // RectTransform pivot'u merkezde değilse (tile pivot'u) ayak noktasını merkeze göre hesapla.
    private static Vector2 RectCenterOffset(RectTransform rt) =>
        new Vector2((rt.pivot.x - 0.5f) * rt.rect.width, (rt.pivot.y - 0.5f) * rt.rect.height);

    private static IEnumerator CoDust(Image img, Vector3 velocity)
    {
        var rt = img.rectTransform;
        var start = rt.localPosition;
        for (float t = 0f; t < LandingDustSeconds; t += Time.deltaTime)
        {
            if (img == null) yield break;
            float k = t / LandingDustSeconds;
            float ease = 1f - (1f - k) * (1f - k);              // hızlı çıkış, yavaşlayarak süzülme
            rt.localPosition = start + velocity * ease;
            rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.4f, ease);
            var c = img.color; c.a = 0.85f * (1f - k); img.color = c;
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }

    // Engel verisini standart vuruş yolundan temizler (interceptor true döner → OnObstacleDestroyed →
    // board hamster taşını kaldırır, hücre dolar). Hamster vurulamaz olduğu için vuruş yalnız buradan gelir.
    private void RequestLeave(TileView tile, HamsterState h)
    {
        h.leaveRequested = true;
        var obs = board != null ? board.ObstacleStateService : null;
        if (obs == null || obs.TryDamageAt(tile.X, tile.Y, ObstacleHitContext.Scripted, null).didHit == false)
        {
            h.leaveRequested = false;
            h.jumping = false;
            return;
        }
        board.RequestResolveAfterActionSequence();
    }

    // İniş: inilen hücrenin çevresi (3x3, hamster hücresi hariç).
    private void EnqueueImpact(Vector2Int land)
    {
        if (board == null || board.LevelData == null) return;
        var targets = new List<Vector2Int>();
        for (int y = land.y - 1; y <= land.y + 1; y++)
        for (int x = land.x - 1; x <= land.x + 1; x++)
        {
            if (x < 0 || y < 0 || x >= board.Width || y >= board.Height) continue;
            if (x == land.x && y == land.y) continue;
            targets.Add(new Vector2Int(x, y));
        }
        if (targets.Count == 0) return;
        board.EnqueueBoardAction(new CellsImpactAction(board, board.LevelData, targets));
        board.RequestResolveAfterActionSequence();
    }

    // ── İniş hücresi (PatchBot önceliği) ────────────────────────────

    // PatchBot hedefi T seçilir. İniş: T düz bir taşsa T; değilse T'nin 3x3'ündeki (iniş vuruşu T'ye değsin)
    // T'ye en yakın düz taş; o da yoksa tahtadaki T'ye en yakın düz taş. Hiç yoksa yerinde.
    private Vector2Int PickLanding(Vector2Int from)
    {
        var target = PickPatchBotTarget(from);
        if (!target.HasValue) return from;
        var t = target.Value;
        if (IsSwappableTile(t, from)) return t;

        Vector2Int best = from; int bestDist = int.MaxValue;
        for (int pass = 0; pass < 2 && best == from; pass++)
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            var c = new Vector2Int(x, y);
            bool inRing = Mathf.Abs(x - t.x) <= 1 && Mathf.Abs(y - t.y) <= 1;
            if (pass == 0 && !inRing) continue;
            if (!IsSwappableTile(c, from)) continue;
            int d = Mathf.Abs(x - t.x) + Mathf.Abs(y - t.y);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    // Yer değiştirilebilir düz taş: engelsiz, special değil, oturmuş, tutulmamış.
    private bool IsSwappableTile(Vector2Int c, Vector2Int from)
    {
        if (c == from || c.x < 0 || c.y < 0 || c.x >= board.Width || c.y >= board.Height) return false;
        var tile = board.Tiles[c.x, c.y];
        if (tile == null || !tile.IsRuntimeIdle || tile.GetSpecial() != TileSpecial.None) return false;
        if (board.IsCellHeld(c.x, c.y)) return false;
        var obs = board.ObstacleStateService;
        return obs == null || obs.GetObstacleIdAt(c.x, c.y) == ObstacleId.None;
    }

    private Vector2Int? PickPatchBotTarget(Vector2Int from)
    {
        try
        {
            var coordinator = new PatchBotTargetCoordinator(board, new PatchbotComboService(board));
            // Aynı öncelikteki adaylardan hamstera EN UZAK olan: yakına değil, uzağa uçsun (görsel şölen).
            var (intent, ok) = coordinator.PickFarthestIntentFrom(from);
            if (!ok) return null;
            var cell = intent.CurrentCell(board);
            coordinator.ReleaseIntent(intent);
            return cell.x >= 0 ? cell : (Vector2Int?)null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Hamster] hedef seçilemedi: {e.Message}");
            return null;
        }
    }

    // ── Ayrılış (hedef tamam) ───────────────────────────────────────

    // Board taşı bu kare yok eder (havuza döner); ayrılış bir kopyada oynar.
    private void StartLeave(TileView tile, HamsterState h)
    {
        var src = Icon(tile);
        if (src == null) return;
        var ghostGo = new GameObject("HamsterLeaving", typeof(RectTransform), typeof(Image));
        ghostGo.layer = src.gameObject.layer;
        var ghost = (RectTransform)ghostGo.transform;
        var parent = board.BreakFxParent != null ? board.BreakFxParent : (RectTransform)tile.transform.parent;
        ghost.SetParent(parent, false);
        ghost.position = src.position;
        ghost.sizeDelta = src.rect.size;
        ghost.SetAsLastSibling();
        var img = ghostGo.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.sprite = Frame("Cheeks3");
        StartCoroutine(CoLeave(img));
    }

    private IEnumerator CoLeave(Image img)
    {
        using var job = board.BeginJob(BoardController.BoardJobKind.ObstacleSpread);
        var rt = img.rectTransform;
        Vector2 home = rt.anchoredPosition;
        float cell = board.TileSize;

        yield return Pop(rt, 1.15f, PopSeconds);
        img.sprite = Frame("Crouch") ?? img.sprite;
        yield return Squash(rt, 1.12f, 0.86f, CrouchSeconds);
        img.sprite = Frame("Jump") ?? img.sprite;
        for (float t = 0f; t < LeaveSeconds; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float k = t / LeaveSeconds;
            rt.anchoredPosition = home + Vector2.up * (k * 3.5f * cell);
            rt.localScale = Vector3.one * (1f + 0.25f * k);
            img.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((k - 0.55f) / 0.45f));
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }

    // ── Görsel yardımcılar ─────────────────────────────────────────

    private Sprite Frame(string name)
    {
        if (!sprites.TryGetValue(name, out var s))
            sprites[name] = s = Resources.Load<Sprite>(SpritePath + name);
        return s;
    }

    private void SetFrame(TileView tile, string name)
    {
        var s = Frame(name);
        if (tile != null && s != null) tile.SetMovableObstacleSprite(s);
    }

    private static RectTransform Icon(TileView tile) =>
        tile != null && tile.IconImage != null ? tile.IconImage.rectTransform : null;

    private static IEnumerator Pop(RectTransform rt, float peak, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float s = 1f + (peak - 1f) * Mathf.Sin(t / seconds * Mathf.PI);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    private static IEnumerator Squash(RectTransform rt, float sx, float sy, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float k = Mathf.Sin(t / seconds * Mathf.PI);
            rt.localScale = new Vector3(1f + (sx - 1f) * k, 1f + (sy - 1f) * k, 1f);
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    // Lokma sayısı: hamsterin üstünden yukarı uçup söner (taşa bağlı değil → karakteri küçültmez/örtmez).
    private void SpawnFloatingNumber(TileView tile, int value)
    {
        var icon = Icon(tile);
        if (icon == null || value <= 0) return;
        var parent = board.BreakFxParent != null ? board.BreakFxParent : (RectTransform)tile.transform.parent;
        var go = new GameObject("HamsterBite", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        float cell = board.TileSize;
        rt.sizeDelta = new Vector2(cell * 1.2f, cell * 0.8f);
        rt.position = icon.position;
        rt.anchoredPosition += new Vector2(0f, cell * 0.25f);
        rt.SetAsLastSibling();
        var text = go.AddComponent<TextMeshProUGUI>();
        if (CommonPopupSkin.Shared != null && CommonPopupSkin.Shared.font != null)
            text.font = CommonPopupSkin.Shared.font;
        text.text = value.ToString();
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontSize = cell * 0.62f;
        text.color = new Color32(255, 46, 46, 255);   // canlı kırmızı: taşların üstünde okunur
        text.raycastTarget = false;
        // Fontun yumuşak/buğulu varsayılan materyali yerine net materyal + kalın koyu kontur
        // (doğrudan outlineWidth setter'ı yeni yaratılan yazıda materyal yokken uygulanmıyordu).
        CrispTextMaterial.Apply(text);
        TmpOutline.Apply(text, 0.32f, new Color32(60, 0, 8, 255));
        StartCoroutine(CoFloat(text, cell));
    }

    private static IEnumerator CoFloat(TMP_Text text, float cell)
    {
        const float seconds = 1.0f;
        var rt = text.rectTransform;
        Vector2 start = rt.anchoredPosition;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (text == null) yield break;
            float k = t / seconds;
            rt.anchoredPosition = start + Vector2.up * (cell * 0.9f * (1f - (1f - k) * (1f - k)));
            float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((k - 0.15f) / 0.2f));
            rt.localScale = Vector3.one * pop;
            text.alpha = 1f - Mathf.Clamp01((k - 0.65f) / 0.35f);
            yield return null;
        }
        if (text != null) Destroy(text.gameObject);
    }
}
