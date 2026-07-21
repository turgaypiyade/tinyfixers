using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime companion for ObstacleId.RocketBasket.
///
/// Renk-anahtarlı launcher: sepet 3 roket tutar (varsayılan Core=kırmızı, Gear=sarı, Bolt=mavi).
/// Sepete KOMŞU o rengin normal match'i olunca ilgili roket fırlar; roket PatchBot ile AYNI
/// hedefleme/impact'i kullanır (BoardController → RocketBasketLaunchAction). Her renk bir kez;
/// 3'ü de gidince sepet board'dan kalkar. Hit tamamen interceptor'da yönetilir (generic break yok).
///
/// HatLauncherService kalıbında MonoBehaviour; BoardController GameObject'ine eklenir.
/// </summary>
public sealed class RocketBasketService : MonoBehaviour
{
    [System.Serializable]
    public struct RocketConfig
    {
        [Tooltip("Bu roketi tetikleyen taş rengi/tipi (varsayılan: Core/Gear/Bolt).")]
        public TileType color;
        [Tooltip("Uçan roket sprite'ı (burun yukarı, alev kuyrukta). Uçuşta yöne kodla döndürülür.")]
        public Sprite flyingSprite;
        [Tooltip("Sepette bu roketi gösteren opsiyonel overlay sprite'ı. Boşsa sepet taban sprite'ı " +
                 "kullanılır (tüp boşalma efekti için ayrı overlay + boş-tüp taban gerekir).")]
        public Sprite basketOverlaySprite;
        [Tooltip("Overlay'in hücre içindeki konumu (-1..1, merkez 0). Örn sol tüp ≈ (-0.3, 0.15).")]
        public Vector2 overlayAnchor;
        [Tooltip("Overlay boyutu (tile oranı).")]
        [Range(0.1f, 1f)] public float overlaySizeRatio;
        [Tooltip("Sepetteki overlay eğimi (derece). Düz sprite'ları yatık göstermek için: " +
                 "soldaki + (üstü sola), sağdaki - (üstü sağa), ortadaki 0. + = CCW.")]
        [Range(-45f, 45f)] public float overlayTiltDegrees;
    }

    [Header("References")]
    [SerializeField] private BoardController board;

    [Header("Basket Visual")]
    [Tooltip("Sepet taban sprite'ı. Boşsa ObstacleLibrary'deki RocketBasket def sprite'ı kullanılır.")]
    [SerializeField] private Sprite basketBaseSprite;

    [Header("Rockets (default Core=red, Gear=yellow, Bolt=blue)")]
    [SerializeField]
    private RocketConfig[] rockets =
    {
        new RocketConfig { color = TileType.Core, overlayAnchor = new Vector2(-0.28f, 0.12f), overlaySizeRatio = 0.34f, overlayTiltDegrees =  9f },
        new RocketConfig { color = TileType.Gear, overlayAnchor = new Vector2( 0.00f, 0.16f), overlaySizeRatio = 0.34f, overlayTiltDegrees =  0f },
        new RocketConfig { color = TileType.Bolt, overlayAnchor = new Vector2( 0.28f, 0.12f), overlaySizeRatio = 0.34f, overlayTiltDegrees = -9f },
    };

    [Header("Debug")]
    [SerializeField] private bool logHits;

    // origin cell index → hâlâ yüklü renkler
    private readonly Dictionary<int, HashSet<TileType>> loadedByOrigin = new();
    // origin cell index → görsel
    private readonly Dictionary<int, RocketBasketView> viewByOrigin = new();

    public Sprite BasketBaseSprite => basketBaseSprite;
    public IReadOnlyList<RocketConfig> Rockets => rockets;

    private void Awake() => ResolveReferences();

    private void OnEnable() => StartCoroutine(BindWhenReady());

    private void OnDisable()
    {
        if (board?.ObstacleStateService != null)
            board.ObstacleStateService.RocketBasketHitInterceptor = null;
        if (board != null)
            board.OnObstacleViewRestored -= HandleObstacleRevealed;
    }

    private void ResolveReferences()
    {
        if (board == null)
            board = GetComponent<BoardController>()
                    ?? GetComponentInParent<BoardController>(true)
                    ?? FindFirstObjectByType<BoardController>();
    }

    private IEnumerator BindWhenReady()
    {
        while (board == null || board.ObstacleStateService == null || board.ActiveLevelData == null)
        {
            ResolveReferences();
            yield return null;
        }

        InitCharges();
        board.ObstacleStateService.RocketBasketHitInterceptor = HandleHit;
        board.OnObstacleViewRestored -= HandleObstacleRevealed;
        board.OnObstacleViewRestored += HandleObstacleRevealed;

        if (logHits)
            Debug.Log($"[RocketBasket] Bound. baskets={loadedByOrigin.Count}");
    }

    // Safe/Chest gibi bir cover'ın ALTINA konan basket, stamp sırasında authored obstacles[]'ta
    // cover id'siyle ezildiği için InitCharges onu GÖREMEZ (loadedByOrigin boş kalır → basket
    // hiç ateşlemezdi). Şarjlar reveal anında burada kurulur.
    private void HandleObstacleRevealed(int x, int y)
    {
        var svc = board != null ? board.ObstacleStateService : null;
        if (svc == null) return;
        if (svc.GetObstacleIdAt(x, y) != ObstacleId.RocketBasket) return;

        int origin = svc.GetObstacleOriginAt(x, y);
        if (origin < 0 || loadedByOrigin.ContainsKey(origin)) return;

        var set = new HashSet<TileType>();
        for (int r = 0; r < rockets.Length; r++)
            set.Add(rockets[r].color);
        loadedByOrigin[origin] = set;

        if (logHits)
            Debug.Log($"[RocketBasket] revealed basket charges origin={origin}");
    }

    private void InitCharges()
    {
        loadedByOrigin.Clear();

        var level = board.ActiveLevelData;
        if (level?.obstacles == null || level.obstacleOrigins == null)
            return;

        for (int i = 0; i < level.obstacles.Length; i++)
        {
            if ((ObstacleId)level.obstacles[i] != ObstacleId.RocketBasket) continue;
            if (level.obstacleOrigins[i] != i) continue;   // 1x1 → origin kendisi

            var set = new HashSet<TileType>();
            for (int r = 0; r < rockets.Length; r++)
                set.Add(rockets[r].color);
            loadedByOrigin[i] = set;
        }
    }

    /// <summary>GridSpawner tarafından çağrılır: origin → view kaydı.</summary>
    public void RegisterView(int origin, RocketBasketView view)
    {
        if (view != null) viewByOrigin[origin] = view;
    }

    // ObstacleStateService.RocketBasketHitInterceptor: (origin, context, sourceColor) → consumed?
    private bool HandleHit(int origin, ObstacleHitContext context, TileType? sourceColor)
    {
        if (!loadedByOrigin.TryGetValue(origin, out var loaded) || loaded.Count == 0)
            return false;

        // Level-wide "fire all on hit": herhangi bir vuruş (renk eşleşse de eşleşmese de),
        // yüklü kalan TÜM roketleri aynı anda fırlatır ve sepeti tek seferde boşaltır.
        if (board.ActiveLevelData != null && board.ActiveLevelData.rocketBasketFireAllOnHit)
        {
            if (context == ObstacleHitContext.NormalMatch &&
                (!sourceColor.HasValue || !loaded.Contains(sourceColor.Value)))
                return false;   // normal match'te en az bir yüklü renk eşleşmeli
            LaunchAll(origin, loaded);
            return true;
        }

        TileType color;
        if (context == ObstacleHitContext.NormalMatch)
        {
            // Normal match: SADECE eşleşen rengin roketi fırlar.
            if (!sourceColor.HasValue || !loaded.Contains(sourceColor.Value))
                return false;   // yanlış renk / şarj yok → no-op
            color = sourceColor.Value;
        }
        else
        {
            // Special / combo / booster / scripted: renk aranmaz, MUTLAKA kalan bir roket fırlar.
            // Eşleşen renk yüklüyse onu, değilse config sırasındaki ilk kalan rengi seç.
            if (sourceColor.HasValue && loaded.Contains(sourceColor.Value))
                color = sourceColor.Value;
            else if (!TryPickAnyLoaded(loaded, out color))
                return false;
        }

        LaunchColor(origin, color, loaded);
        return true;
    }

    private bool TryPickAnyLoaded(HashSet<TileType> loaded, out TileType color)
    {
        // Config sırasını koru (varsayılan Core→Gear→Bolt) — deterministik.
        for (int i = 0; i < rockets.Length; i++)
        {
            if (loaded.Contains(rockets[i].color))
            {
                color = rockets[i].color;
                return true;
            }
        }
        // Config dışı bir renk yüklüyse (beklenmez) ilkini al.
        foreach (var c in loaded) { color = c; return true; }
        color = default;
        return false;
    }

    // Tek vuruşta yüklü kalan tüm roketleri config sırasında (Core→Gear→Bolt) fırlatır.
    // Snapshot alınır çünkü LaunchColor `loaded`'ı değiştirir ve son roketle sepeti teardown eder.
    private void LaunchAll(int origin, HashSet<TileType> loaded)
    {
        var order = new List<TileType>();
        for (int i = 0; i < rockets.Length; i++)
            if (loaded.Contains(rockets[i].color))
                order.Add(rockets[i].color);
        // Config dışı yüklü renk (beklenmez) kalırsa onları da ekle.
        foreach (var c in loaded)
            if (!order.Contains(c))
                order.Add(c);

        for (int i = 0; i < order.Count; i++)
            LaunchColor(origin, order[i], loaded);
    }

    private void LaunchColor(int origin, TileType color, HashSet<TileType> loaded)
    {
        loaded.Remove(color);

        int w = board.Width;
        var originCell = new Vector2Int(origin % w, origin / w);
        Sprite flying = ResolveFlyingSprite(color);

        board.QueueRocketLaunch(originCell, color, flying);

        if (viewByOrigin.TryGetValue(origin, out var view) && view != null)
            view.LaunchRocket(color);

        if (logHits)
            Debug.Log($"[RocketBasket] launch origin={origin} color={color} remaining={loaded.Count}");

        if (loaded.Count == 0)
        {
            loadedByOrigin.Remove(origin);
            board.ObstacleStateService.ClearRocketBasket(origin);
            if (viewByOrigin.TryGetValue(origin, out var v) && v != null)
            {
                v.Teardown();
                viewByOrigin.Remove(origin);
            }
        }
    }

    private Sprite ResolveFlyingSprite(TileType color)
    {
        for (int i = 0; i < rockets.Length; i++)
            if (rockets[i].color == color)
                return rockets[i].flyingSprite;
        return null;
    }
}
