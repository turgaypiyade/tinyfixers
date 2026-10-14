using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Brandalı heykel mekaniği (ObstacleId.TarpStatue; Docs/TarpStatue_Plan.md). Taş evresi sıradan kütüphane engelidir
/// (2x2, 3 vuruş — hasar/aşama/yığın/hedef ObstacleStateService'te); bu servis yalnız şunları ekler:
///   • Branda: etiket rengindeki her temizlenen taş (tahtanın herhangi bir yerinde) etikete uçar ve varınca sayaç düşer
///     (AncientSealCollectFx). Sayaç bitene dek ObstacleStateService.TarpStatueCoveredQuery heykeli vuruşa kapatır.
///   • Final: son vuruşta ayak izi tutulur (taş girmez, level-end bekler), heykel parlar, hedef slotuna uçar,
///     çerçeve parçalanır. Hedef kredisi kütüphane yolundan (OnObstacleDestroyed) anında düşer; uçuş görseldir.
/// Board üstünde tek bileşen (LaserCannonService deseni).
/// </summary>
public sealed class TarpStatueService : MonoBehaviour
{
    private const float RevealSeconds = 0.35f;
    private const float FlySeconds = 0.6f;

    private sealed class Statue
    {
        public TarpStatueColor color;
        public int remaining, total, inFlight;
        public TarpStatueView view;
    }

    private sealed class Flight
    {
        public int origin;
        public Statue statue;
        public IDisposable job;
        public AncientSealCollectFx fx;
        public bool arrived;
    }

    private BoardController board;
    private ObstacleStateService boundState;
    private bool subscribed;
    private readonly Dictionary<int, Statue> statues = new();
    private readonly List<int> eligible = new();
    private readonly HashSet<Flight> flights = new();
    private readonly Dictionary<long, AncientSealService.GatherBatch> gatherBatches = new();
    private readonly List<Vector2Int> cellBuffer = new(4);
    private int gatherFrame = -1;
    private int lastAssignedOrigin = -1;
    private RectTransform flightRoot;

    public static TarpStatueService Ensure(BoardController board)
    {
        if (board == null) return null;
        if (!board.TryGetComponent<TarpStatueService>(out var service))
            service = board.gameObject.AddComponent<TarpStatueService>();
        service.Bind(board);
        return service;
    }

    /// Level kurulumu başında: önceki level'ın durum + uçuşları bırakılır (görünümleri spawner temizler).
    public void Clear()
    {
        CancelFlights();
        statues.Clear();
        lastAssignedOrigin = -1;
    }

    /// GridSpawner, engel görselini çizerken çağırır.
    public void Register(int origin, Image image)
    {
        if (image == null || board == null) return;
        var s = GetOrCreate(origin);
        // Örtü (çim) altındayken hücrenin kalan vuruşu örtüye aittir → yalnız en üst katman heykelse oku.
        var state = board.ObstacleStateService;
        int ox = origin % board.Width, oy = origin / board.Width;
        int hits = state != null && state.GetObstacleIdAt(ox, oy) == ObstacleId.TarpStatue
            ? state.GetRemainingHitsAt(ox, oy) : 3;
        s.view = TarpStatueView.Ensure(image, s.color, s.remaining, hits > 0 ? hits : 3);
    }

    private Statue GetOrCreate(int origin)
    {
        if (statues.TryGetValue(origin, out var s)) return s;
        var entry = board != null && board.ActiveLevelData != null
            ? board.ActiveLevelData.GetTarpStatue(origin)
            : new TarpStatueEntry { color = TarpStatueColor.Red, count = LevelData.TarpStatueDefaultCount };
        s = new Statue { color = entry.color, remaining = Mathf.Max(1, entry.count), total = Mathf.Max(1, entry.count) };
        statues[origin] = s;
        return s;
    }

    private bool IsCovered(int origin) => GetOrCreate(origin).remaining > 0;

    private void Bind(BoardController target)
    {
        if (board != target) Unsubscribe();
        board = target;
        // ObstacleStateService level başına yeniden kurulabilir: sorguyu her bağlamada tazele.
        if (board != null && board.ObstacleStateService != null)
            board.ObstacleStateService.TarpStatueCoveredQuery = IsCovered;
        if (subscribed || board == null) return;
        board.OnTileCollectedAt += HandleTileCollectedAt;
        boundState = board.ObstacleStateService;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (board != null) board.OnTileCollectedAt -= HandleTileCollectedAt;
        if (boundState != null && boundState.TarpStatueCoveredQuery == (Func<int, bool>)IsCovered)
            boundState.TarpStatueCoveredQuery = null;
        boundState = null;
        subscribed = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        Clear();
    }

    // ── Branda: renk toplama ─────────────────────────────────────────

    private static TarpStatueColor? ColorOf(TileType type) => type switch
    {
        TileType.Core => TarpStatueColor.Red,
        TileType.Gear => TarpStatueColor.Yellow,
        TileType.Bolt => TarpStatueColor.Blue,
        TileType.Plate => TarpStatueColor.Green,
        _ => null,
    };

    private void HandleTileCollectedAt(TileType type, Vector3 worldPosition)
    {
        var color = ColorOf(type);
        var obstacles = board != null ? board.ObstacleStateService : null;
        if (color == null || obstacles == null || statues.Count == 0) return;

        // Yalnız eksik kalan kadar taş ayrılır; örtü (çim) altındaki heykel toplamaz (mühür kuralı).
        eligible.Clear();
        foreach (var pair in statues)
        {
            var s = pair.Value;
            if (s.color != color.Value || s.remaining <= s.inFlight) continue;
            if (obstacles.GetObstacleIdAt(pair.Key % board.Width, pair.Key / board.Width) != ObstacleId.TarpStatue) continue;
            if (obstacles.IsBuriedAnywhere(ObstacleId.TarpStatue, pair.Key)) continue;
            eligible.Add(pair.Key);
        }
        if (eligible.Count == 0) return;
        eligible.Sort();
        // Her taşın TEK hedefi var; aynı renkli heykeller sırayla alır.
        int i = eligible.FindIndex(o => o > lastAssignedOrigin);
        int origin = eligible[i >= 0 ? i : 0];
        lastAssignedOrigin = origin;
        Launch(origin, type, worldPosition);
    }

    private void Launch(int origin, TileType type, Vector3 worldPosition)
    {
        var s = statues[origin];
        s.inFlight++;
        var flight = new Flight { origin = origin, statue = s };
        if (s.view == null || !s.view.isActiveAndEnabled || !EnsureFlightRoot() || board.GetIcon(type) == null)
        {
            Arrive(flight);
            return;
        }
        // Uçuş yerçekimini durdurmaz ama kazanma/kaybetme değerlendirmesi varışı bekler.
        flight.job = board.BeginJob(BoardController.BoardJobKind.GoalOrbFlight);
        var batch = BatchFor(origin);
        int order = batch.count;
        batch.worldSum += worldPosition;
        batch.count++;
        flight.fx = new AncientSealCollectFx(this, flightRoot, board, s.view, type, (int)s.color, worldPosition, batch, order,
            TarpStatueView.TintFor(s.color));
        flights.Add(flight);
        StartCoroutine(Fly(flight));
    }

    private IEnumerator Fly(Flight flight)
    {
        try { yield return flight.fx.Play(() => Arrive(flight)); }
        finally { ReleaseFlight(flight); }
    }

    private void Arrive(Flight flight)
    {
        if (flight.arrived) return;
        flight.arrived = true;
        var s = flight.statue;
        s.inFlight = Mathf.Max(0, s.inFlight - 1);
        if (!statues.TryGetValue(flight.origin, out var current) || current != s || s.remaining <= 0) return;
        s.remaining = Mathf.Max(0, s.remaining - 1);
        if (s.view != null) s.view.OnCollected(s.remaining);
        if (s.remaining == 0) PlaySound(d => (d.coverRemovedSound, d.coverRemovedSoundVolume));
        // Branda kalktı: heykel artık vuruş alır. Çözümü uyandır (bekleyen bitişik eşleşme yok ama special zinciri olabilir).
        if (s.remaining == 0) board?.RequestResolveAfterActionSequence();
    }

    private AncientSealService.GatherBatch BatchFor(int origin)
    {
        if (gatherFrame != Time.frameCount)
        {
            gatherFrame = Time.frameCount;
            gatherBatches.Clear();
        }
        if (!gatherBatches.TryGetValue(origin, out var batch))
            gatherBatches[origin] = batch = new AncientSealService.GatherBatch();
        return batch;
    }

    // ── Taş evresi + final (GridSpawner'dan) ─────────────────────────

    /// Ara vuruş: aşama sprite'ı + taş parçaları.
    public static bool TryPlayHit(BoardController board, Image image, int remainingHits)
    {
        if (board == null || image == null || !image.TryGetComponent<TarpStatueView>(out var view)) return false;
        var service = Ensure(board);
        view.OnStoneHit(remainingHits, service, VfxRoot(board));
        return true;
    }

    /// Son vuruş: görünüm servise devredilir (heykel uçar, çerçeve parçalanır, görsel yok edilir).
    public static bool TryPlayFinale(BoardController board, int origin, Image image)
    {
        if (board == null || image == null || !image.TryGetComponent<TarpStatueView>(out var view)) return false;
        var service = Ensure(board);
        service.statues.Remove(origin);
        service.StartCoroutine(service.CoFinale(origin, image, view));
        return true;
    }

    private IEnumerator CoFinale(int origin, Image image, TarpStatueView view)
    {
        int ox = origin % board.Width, oy = origin / board.Width;
        int n = board.ActiveLevelData != null ? board.ActiveLevelData.GetTarpStatue(origin).Size : 2;
        cellBuffer.Clear();
        for (int dy = 0; dy < n; dy++)
            for (int dx = 0; dx < n; dx++)
                cellBuffer.Add(new Vector2Int(ox + dx, oy + dy));
        var hold = board.HoldCells(cellBuffer);
        var job = board.BeginJob(BoardController.BoardJobKind.ObstacleCollapse);
        var flyJob = board.BeginJob(BoardController.BoardJobKind.GoalOrbFlight);
        try
        {
            yield return view.CoRevealStatue(RevealSeconds);
            if (image == null) yield break;

            var root = VfxRoot(board);
            // Heykel daima HUD'a uçar: hedef slotu varsa ona (slot nabız atar), yoksa HUD'un ortasına.
            RectTransform goal = null;
            bool hasGoalSlot = board.TopHud != null
                && board.TopHud.TryGetGoalTargetRectForObstacle(ObstacleId.TarpStatue, out goal);
            if (!hasGoalSlot && board.TopHud != null)
                goal = board.TopHud.transform as RectTransform;
            if (EnsureFlightRoot())
                StartCoroutine(CoFlyStatue(view, goal, hasGoalSlot, flyJob));
            else
                flyJob.Dispose();
            flyJob = null;

            PlaySound(d => (d.followUpBreakSound, d.followUpBreakSoundVolume));
            if (root != null)
                yield return view.CoShatter(root);
        }
        finally
        {
            hold?.Dispose();
            job?.Dispose();
            flyJob?.Dispose();
            if (image != null) Destroy(image.gameObject);
        }
    }

    // Heykel tahtadan hedef slotuna yay çizerek uçar, küçülür, varınca slot nabız atar.
    private IEnumerator CoFlyStatue(TarpStatueView view, RectTransform goal, bool punchSlot, IDisposable job)
    {
        RectTransform ghost = null;
        try
        {
            var sprite = view.StatueSprite;
            var stone = view.StoneRect;
            if (sprite == null || stone == null) yield break;
            float size = flightRoot.InverseTransformVector(stone.TransformVector(new Vector3(stone.rect.width, 0f, 0f))).magnitude;
            ghost = FlightTrailFx.CreateImage(flightRoot, "TarpStatueFlight", sprite, Color.white, size);
            ghost.GetComponent<Image>().preserveAspect = true;
            Vector2 start = flightRoot.InverseTransformPoint(stone.TransformPoint(stone.rect.center));
            ghost.anchoredPosition = start;
            if (goal == null)
            {
                for (float t = 0f; t < 0.3f; t += Time.deltaTime)
                {
                    ghost.localScale = Vector3.one * (1f - t / 0.3f);
                    yield return null;
                }
                yield break;
            }

            for (float t = 0f; t < FlySeconds; t += Time.deltaTime)
            {
                if (goal == null) yield break;
                float k = Mathf.Clamp01(t / FlySeconds);
                float e = k * k * (3f - 2f * k);
                Vector2 end = flightRoot.InverseTransformPoint(goal.TransformPoint(goal.rect.center));
                Vector2 control = (start + end) * 0.5f + new Vector2(0f, size * 0.9f);
                ghost.anchoredPosition = (1f - e) * (1f - e) * start + 2f * (1f - e) * e * control + e * e * end;
                ghost.localScale = Vector3.one * Mathf.Lerp(1.05f, 0.18f, e);
                yield return null;
            }
            if (!punchSlot) yield break;
            var punch = goal.parent as RectTransform ?? goal;
            for (float t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                if (punch == null) break;
                punch.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, t / 0.14f);
                yield return null;
            }
            if (punch != null) punch.localScale = Vector3.one;
        }
        finally
        {
            if (ghost != null) Destroy(ghost.gameObject);
            job?.Dispose();
        }
    }

    // Sesler kütüphane tanımında (level'la önceden yüklenir; oyun ortasında Resources.Load yok). Vuruş + ortaya çıkış
    // sesleri (hitSound / breakSound) BoardBreakFxService'ten; branda ve parçalanma anını görünüm belirler → buradan.
    private void PlaySound(Func<ObstacleDef, (AudioClip clip, float volume)> pick)
    {
        if (board == null || !GameSettings.SoundEnabled) return;
        var def = board.ActiveLevelData?.obstacleLibrary?.Get(ObstacleId.TarpStatue);
        if (def == null) return;
        var (clip, volume) = pick(def);
        if (clip != null) board.SfxSource?.PlayOneShot(clip, volume);
    }

    private static RectTransform VfxRoot(BoardController board)
        => board.BoardVfxPlayer != null && board.BoardVfxPlayer.VfxRoot != null
            ? (RectTransform)board.BoardVfxPlayer.VfxRoot : board.BreakFxParent;

    // ── Yardımcılar ──────────────────────────────────────────────────

    private bool EnsureFlightRoot()
    {
        if (flightRoot != null) return true;
        var canvas = board.TilesRoot != null ? board.TilesRoot.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return false;
        var go = new GameObject("TarpStatueFlights", typeof(RectTransform));
        go.layer = canvas.rootCanvas.gameObject.layer;
        flightRoot = (RectTransform)go.transform;
        flightRoot.SetParent(canvas.rootCanvas.transform, false);
        flightRoot.anchorMin = Vector2.zero;
        flightRoot.anchorMax = Vector2.one;
        flightRoot.pivot = new Vector2(0.5f, 0.5f);
        flightRoot.offsetMin = flightRoot.offsetMax = Vector2.zero;
        return true;
    }

    private void ReleaseFlight(Flight flight)
    {
        if (!flights.Remove(flight)) return;
        if (!flight.arrived) flight.statue.inFlight = Mathf.Max(0, flight.statue.inFlight - 1);
        flight.fx?.Dispose();
        flight.job?.Dispose();
    }

    private void CancelFlights()
    {
        StopAllCoroutines();
        foreach (var flight in new List<Flight>(flights)) ReleaseFlight(flight);
        if (flightRoot != null) Destroy(flightRoot.gameObject);
        flightRoot = null;
    }
}
