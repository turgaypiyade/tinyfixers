using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class TileView : MonoBehaviour,
    IPointerClickHandler, IPointerDownHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 IconReferenceSize = new Vector2(100f, 100f);
    private const float IconReferenceTileSize = 100f;

    [SerializeField] private Image iconImage;
    [SerializeField] private PatchBotPropellerView propellerView;
    [SerializeField] private OverrideSpecialView overrideSpecialView;
    [SerializeField] private PulseFuseSparkleView fuseSparkleView;
    private TileModel model;

    public int X { get; private set; }
    public int Y { get; private set; }

    private BoardController board;
    private RectTransform rt;
    private RectTransform parentRt;

    // Drag state
    private Vector2 dragStartAnchored;
    private Vector2 dragStartLocalPointer;
    private Vector2 dragStartScreen;
    private bool dragConsumedSwap;
    private bool wasDragging;
    private bool boosterFiredOnDown;

    [SerializeField, Range(0.5f, 1f)]
    [FormerlySerializedAs("runtimeIconScale")]
    private float iconScale = 0.89f;

    [SerializeField] private Vector2 iconSize = new Vector2(100f, 100f);
    [SerializeField] private bool useFullCellIcon = false;

    private Coroutine specialCreationRevealRoutine;
    private GameObject specialCreationRevealRoot;
    private static Sprite specialRevealHaloSprite;

    private bool specialRevealVisualCaptured;
    private Vector3 specialRevealIconBaseScale = Vector3.one;
    private Quaternion specialRevealIconBaseRotation = Quaternion.identity;
    private Color specialRevealIconBaseColor = Color.white;

    public enum TileVisualLayout
    {
        BottomAligned,
        Centered,
        FillCell
    }

    [SerializeField] private TileVisualLayout visualLayout = TileVisualLayout.Centered;

    private bool isMovableObstacleTile = false;
    private bool isFullCellMovableSprite = false;
    // Movable obstacle (plastik vb.) sprite'ı model.type DIŞINDA tutulur. Saklanmazsa
    // RefreshIcon/SetType ikonu model.type'a (dummy normal tip) döndürüp obstacle'ı
    // ekranda normal taş gibi gösterir (görsel/mantık desync).
    private Sprite movableObstacleSprite;
    private int lastAppliedTileSize;

    // Bu taşın düştüğü CollapseAndSpawnAnimated nesil ID'si. -1 = hiç düşmedi.
    private int lastFallGeneration = -1;

    public RectTransform RectTransform => rt != null ? rt : (RectTransform)transform;
    public Image IconImage => iconImage;
    public PatchBotPropellerView PropellerView => propellerView;
    public OverrideSpecialView OverrideSpecialView => overrideSpecialView;

    public bool IsPlannedToMoveThisFallPass { get; private set; }

    public void MarkPlannedToMoveThisFallPass(bool value)
    {
        IsPlannedToMoveThisFallPass = value;
    }

    public void SetCoords(int x, int y)
    {
        X = x;
        Y = y;
    }

    public void ApplySortingOrder() => UpdateSiblingOrder();

    private void UpdateSiblingOrder()
    {
        if (transform.parent == null || board == null)
            return;

        int tilesPerRow = board.Width;
        int totalTiles = board.Width * board.Height;
        int idx = totalTiles - 1 - (Y * tilesPerRow + X);

        transform.SetSiblingIndex(Mathf.Clamp(idx, 0, totalTiles - 1));
    }

    private void Awake()
    {
        model = GetComponent<TileModel>();
        rt = GetComponent<RectTransform>();
        parentRt = rt.parent as RectTransform;

        if (iconImage == null)
        {
            var imgs = GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                if (img.gameObject.name == "Icon")
                {
                    iconImage = img;
                    break;
                }
            }
        }

        ResetVisualState();
    }

    private void OnEnable()
    {
        ResetVisualState();
    }

    private void OnDisable()
    {
        StopSpecialCreationReveal();
        ResetVisualState();
    }

    public void Init(BoardController board, int x, int y)
    {
        this.board = board;
        X = x;
        Y = y;
        IsPlannedToMoveThisFallPass = false;

        ResetVisualState();
        dragConsumedSwap = false;
        wasDragging = false;

    }

    private void ResetVisualState()
    {
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;

        if (TryGetComponent<CanvasGroup>(out var canvasGroup))
            canvasGroup.alpha = 1f;

        if (iconImage != null)
        {
            iconImage.color = Color.white;
            iconImage.transform.localScale = Vector3.one;
            iconImage.transform.localRotation = Quaternion.identity;
        }

        propellerView?.Stop();
        overrideSpecialView?.Stop();
        fuseSparkleView?.Stop();

        if (lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);
    }

    public bool IsSpecialCreationRevealPlaying =>
        specialCreationRevealRoutine != null || specialCreationRevealRoot != null;

    public bool TryGetCellState(out BoardCellStateSnapshot state)
    {
        state = default;
        return board != null && board.TryGetCellState(X, Y, out state);
    }

    public void RefreshIcon()
    {
        if (model == null || board == null)
            return;

        // Movable obstacle: ikon model.type'a göre değil, saklanan obstacle sprite'ına göre.
        // Aksi hâlde refresh, plastik/movable'ı dummy normal taşa çevirir (görsel/mantık desync).
        if (isMovableObstacleTile && movableObstacleSprite != null)
        {
            SetIcon(movableObstacleSprite);

            if (propellerView != null)
                propellerView.gameObject.SetActive(false);
            if (fuseSparkleView != null)
                fuseSparkleView.gameObject.SetActive(false);
            return;
        }

        if (model.special == TileSpecial.SystemOverride)
        {
            Sprite sp = null;

            if (model.hasOverrideBaseType)
                sp = board.GetOverrideIcon(model.overrideBaseType);

            if (sp == null)
                sp = board.GetSpecialIcon(model.special);

            if (sp == null)
                sp = board.GetIcon(model.type);

            if (sp != null) SetIcon(sp);
        }
        else if (model.special != TileSpecial.None)
        {
            Sprite sp = board.GetSpecialIcon(model.special);

            if (sp == null)
                sp = board.GetIcon(model.type);

            if (sp != null) SetIcon(sp);
        }
        else
        {
            Sprite sp = board.GetIcon(model.type);
            if (sp != null) SetIcon(sp);
        }

        if (propellerView != null)
            propellerView.gameObject.SetActive(model.special == TileSpecial.PatchBot);

        if (fuseSparkleView != null)
            fuseSparkleView.gameObject.SetActive(model.special == TileSpecial.PulseCore);
    }

    public void SnapToGrid(int tileSize)
    {
        if (rt == null)
            rt = GetComponent<RectTransform>();

        if (parentRt == null)
            parentRt = rt.parent as RectTransform;

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);

        rt.anchoredPosition = new Vector2(X * tileSize, -Y * tileSize);

        ApplyTileSize(tileSize);
    }

    public IEnumerator MoveToGrid(
        int tileSize,
        float duration,
        AnimationCurve easingCurve = null,
        bool enableSettle = false,
        float settleDuration = 0.06f,
        float settleStrength = 0.04f,
        float settleStretchX = 0f,
        float settleOvershoot = 0f)
    {
        lastFallGeneration = (board != null) ? board.FallGeneration : 0;

        if (rt == null || !rt)
            yield break;

        RectTransform visualRt = iconImage != null ? iconImage.rectTransform : null;
        // Ev scale CANLI OKUNMAZ (shake-drift dersinin skala hali): önceki esneme/squash
        // yarıda kesildiyse mevcut scale esnik kalmış olabilir — onu "ev" sanmak taşı
        // kalıcı esnik bırakıyordu. İkonun dinlenme scale'i bu kod tabanında HER ZAMAN 1.
        Vector3 visualBaseScale = Vector3.one;
        if (visualRt != null) visualRt.localScale = Vector3.one;   // uçuşa temiz başla

        Vector2 start = rt.anchoredPosition;
        Vector2 end;

        bool isSpecial = model != null && model.special != TileSpecial.None;
        bool isMovable = isMovableObstacleTile;

        if (isMovable)
        {
            end = new Vector2(X * tileSize, -Y * tileSize);
        }
        else if (isSpecial)
        {
            float cellH;

            if (useFullCellIcon)
            {
                cellH = tileSize;
            }
            else
            {
                float ratioY = iconSize.y / Mathf.Max(1f, IconReferenceSize.y);
                cellH = tileSize * Mathf.Max(0.1f, ratioY);
            }

            float elemH = cellH * (110f / 115f);
            float yOffset = (cellH - elemH) * 0.5f;

            end = new Vector2(X * tileSize, -Y * tileSize - yOffset);
        }
        else
        {
            end = new Vector2(X * tileSize, -Y * tileSize);
        }

        float totalPixels = Mathf.Abs(end.y - start.y);

        if (totalPixels < 0.5f)
        {
            if (rt != null && rt)
            {
                rt.anchoredPosition = end;
                SnapToGrid(tileSize);
            }

            ResetVisualScale(visualRt, visualBaseScale);
            yield break;
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.0001f, duration);

        while (elapsed < safeDuration)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / safeDuration);
            if (easingCurve != null)
                t = easingCurve.Evaluate(t);

            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, t);

            yield return null;
        }

        if (rt == null || !rt)
        {
            ResetVisualScale(visualRt, visualBaseScale);
            yield break;
        }

        rt.anchoredPosition = end;
        SnapToGrid(tileSize);

        if (!enableSettle || settleDuration <= 0f)
        {
            ResetVisualScale(visualRt, visualBaseScale);
            yield break;
        }

        // Pozisyon dalışı settleOvershoot ile (KÜÇÜK, %2 gibi) — strength SKALA squash'ı sürer.
        // Eskiden strength (%11+) pozisyonu sürüyordu: inen taş alttakinin içine gömülüp
        // çıkıyordu (ağır çekimde "üst üste binip ayrılma").
        Vector2 overshoot = end + new Vector2(0f, -settleOvershoot * tileSize);
        Vector2 overUp = end + new Vector2(0f, settleOvershoot * tileSize * 0.3f);

        float bounceDur = settleDuration;

        float b1 = 0f;
        while (b1 < bounceDur * 0.35f)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            b1 += Time.deltaTime;

            float k = Mathf.Clamp01(b1 / Mathf.Max(0.0001f, bounceDur * 0.35f));
            float eased = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(end, overshoot, eased);

            if (visualRt != null && (settleStretchX > 0f || settleStrength > 0f))
            {
                float sx = 1f + settleStretchX * eased;
                float sy = 1f - settleStrength * eased;   // impact ezilmesi skala ile (pozisyon değil)

                visualRt.localScale = new Vector3(
                    visualBaseScale.x * sx,
                    visualBaseScale.y * sy,
                    visualBaseScale.z);
            }

            yield return null;
        }

        float b2 = 0f;
        while (b2 < bounceDur * 0.35f)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            b2 += Time.deltaTime;

            float k = Mathf.Clamp01(b2 / Mathf.Max(0.0001f, bounceDur * 0.35f));
            float eased = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(overshoot, overUp, eased);

            if (visualRt != null && (settleStretchX > 0f || settleStrength > 0f))
            {
                float revK = 1f - k;
                float sx = 1f + settleStretchX * revK;
                float sy = 1f - settleStrength * revK;

                visualRt.localScale = new Vector3(
                    visualBaseScale.x * sx,
                    visualBaseScale.y * sy,
                    visualBaseScale.z);
            }

            yield return null;
        }

        float b3 = 0f;
        while (b3 < bounceDur * 0.3f)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            b3 += Time.deltaTime;

            float k = Mathf.Clamp01(b3 / Mathf.Max(0.0001f, bounceDur * 0.3f));
            float eased = k * k;

            rt.anchoredPosition = Vector2.LerpUnclamped(overUp, end, eased);

            yield return null;
        }

        ResetVisualScale(visualRt, visualBaseScale);

        if (rt != null && rt)
            SnapToGrid(tileSize);
    }

    private Vector2 GetFallCellAnchoredPosition(int cellX, int cellY, int tileSize)
    {
        bool isSpecial = model != null && model.special != TileSpecial.None;
        bool isMovable = isMovableObstacleTile;

        if (isMovable)
            return new Vector2(cellX * tileSize, -cellY * tileSize);

        if (isSpecial)
        {
            float cellH;

            if (useFullCellIcon)
            {
                cellH = tileSize;
            }
            else
            {
                float ratioY = iconSize.y / Mathf.Max(1f, IconReferenceSize.y);
                cellH = tileSize * Mathf.Max(0.1f, ratioY);
            }

            float elemH = cellH * (110f / 115f);
            float yOffset = (cellH - elemH) * 0.5f;

            return new Vector2(cellX * tileSize, -cellY * tileSize - yOffset);
        }

        return new Vector2(cellX * tileSize, -cellY * tileSize);
    }

    // ── Düşüş esnemesi (squash&stretch) yardımcıları ─────────────────────────

    // Zarf MUTLAK zamanlı: yüksek düşüş hızında (26 hücre/sn → 1 hücre ~0.04sn)
    // yüzde bazlı zarf hiç görünmüyordu. Gerilme ~0.03sn'de tavana çıkar; varıştan
    // kısa süre önce (recover oranı, en çok 0.08sn) toparlar.
    private static float FallStretchEnv(float elapsed, float duration, float recoverPortion)
    {
        const float rampInTime = 0.03f;
        float recoverTime = Mathf.Min(0.08f, duration * recoverPortion);

        float rampIn = Mathf.Clamp01(elapsed / rampInTime);
        float remain = duration - elapsed;
        float rampOut = recoverTime > 0.0001f ? Mathf.Clamp01(remain / recoverTime) : 1f;
        return Mathf.SmoothStep(0f, 1f, Mathf.Min(rampIn, rampOut));
    }

    private static void ApplyFallStretchScale(RectTransform visualRt, Vector3 baseScale, float amount, float env)
    {
        float sy = 1f + amount * env;
        float sx = 1f - amount * 0.55f * env;   // hacim korunuyormuş gibi incelt
        visualRt.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);
    }

    private static void ResetVisualScale(RectTransform visualRt, Vector3 baseScale)
    {
        if (visualRt != null && visualRt)
            visualRt.localScale = baseScale;
    }

    public IEnumerator MoveToGridCell(
        int tileSize,
        int fromX,
        int fromY,
        int toX,
        int toY,
        float duration,
        AnimationCurve easingCurve = null,
        bool enableSettle = false,
        float settleDuration = 0.06f,
        float settleStrength = 0.04f,
        float settleStretchX = 0f,
        float settleOvershoot = 0f)
    {
        lastFallGeneration = (board != null) ? board.FallGeneration : 0;

        if (this == null || !this)
            yield break;

        if (rt == null)
            rt = GetComponent<RectTransform>();

        if (rt == null || !rt)
            yield break;

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);

        RectTransform visualRt = iconImage != null ? iconImage.rectTransform : null;
        // Ev scale CANLI OKUNMAZ (shake-drift dersinin skala hali): önceki esneme/squash
        // yarıda kesildiyse mevcut scale esnik kalmış olabilir — onu "ev" sanmak taşı
        // kalıcı esnik bırakıyordu. İkonun dinlenme scale'i bu kod tabanında HER ZAMAN 1.
        Vector3 visualBaseScale = Vector3.one;
        if (visualRt != null) visualRt.localScale = Vector3.one;   // uçuşa temiz başla

        Vector2 start = GetFallCellAnchoredPosition(fromX, fromY, tileSize);
        Vector2 end = GetFallCellAnchoredPosition(toX, toY, tileSize);

        rt.anchoredPosition = start;

        float totalPixels = Vector2.Distance(start, end);

        if (totalPixels < 0.5f)
        {
            rt.anchoredPosition = end;
            SnapToGrid(tileSize);
            ResetVisualScale(visualRt, visualBaseScale);
            yield break;
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.0001f, duration);

        // Düşüş esnemesi: dikey düşüşte taş dikeyde uzar ("altından sandalye çekilmiş"),
        // varışa yakın normale döner; inişte settle squash devralır.
        float stretchAmt = board != null ? board.FallStretchAmount : 0f;
        float stretchRecover = board != null ? board.FallStretchRecover : 0.35f;
        bool doStretch = stretchAmt > 0.001f && visualRt != null
                         && end.y < start.y - 0.5f
                         && Mathf.Abs(end.x - start.x) < 0.5f;

        while (elapsed < safeDuration)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / safeDuration);
            if (easingCurve != null)
                t = easingCurve.Evaluate(t);

            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, t);

            if (doStretch && visualRt != null)
                ApplyFallStretchScale(visualRt, visualBaseScale, stretchAmt,
                    FallStretchEnv(elapsed, safeDuration, stretchRecover));

            yield return null;
        }

        if (rt == null || !rt)
        {
            ResetVisualScale(visualRt, visualBaseScale);
            yield break;
        }

        if (doStretch && visualRt != null)
            ResetVisualScale(visualRt, visualBaseScale);   // settle squash temiz tabandan başlasın

        rt.anchoredPosition = end;
        SnapToGrid(tileSize);

        if (!enableSettle || settleDuration <= 0f)
        {
            ResetVisualScale(visualRt, visualBaseScale);
            yield break;
        }

        Vector2 basePos = rt.anchoredPosition;
        // Pozisyon dalışı settleOvershoot ile (küçük) — strength skala squash'ı sürer (gömülme fix'i).
        Vector2 overshoot = basePos + new Vector2(0f, -settleOvershoot * tileSize);
        Vector2 overUp = basePos + new Vector2(0f, settleOvershoot * tileSize * 0.3f);

        float bounceDur = settleDuration;

        float b1 = 0f;
        while (b1 < bounceDur * 0.35f)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            b1 += Time.deltaTime;

            float k = Mathf.Clamp01(b1 / Mathf.Max(0.0001f, bounceDur * 0.35f));
            float eased = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(basePos, overshoot, eased);

            if (visualRt != null && (settleStretchX > 0f || settleStrength > 0f))
            {
                float sx = 1f + settleStretchX * eased;
                float sy = 1f - settleStrength * eased;   // impact ezilmesi skala ile (pozisyon değil)

                visualRt.localScale = new Vector3(
                    visualBaseScale.x * sx,
                    visualBaseScale.y * sy,
                    visualBaseScale.z);
            }

            yield return null;
        }

        float b2 = 0f;
        while (b2 < bounceDur * 0.35f)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            b2 += Time.deltaTime;

            float k = Mathf.Clamp01(b2 / Mathf.Max(0.0001f, bounceDur * 0.35f));
            float eased = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(overshoot, overUp, eased);

            if (visualRt != null && (settleStretchX > 0f || settleStrength > 0f))
            {
                float revK = 1f - k;
                float sx = 1f + settleStretchX * revK;
                float sy = 1f - settleStrength * revK;

                visualRt.localScale = new Vector3(
                    visualBaseScale.x * sx,
                    visualBaseScale.y * sy,
                    visualBaseScale.z);
            }

            yield return null;
        }

        float b3 = 0f;
        while (b3 < bounceDur * 0.3f)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            b3 += Time.deltaTime;

            float k = Mathf.Clamp01(b3 / Mathf.Max(0.0001f, bounceDur * 0.3f));
            float eased = k * k;

            rt.anchoredPosition = Vector2.LerpUnclamped(overUp, basePos, eased);

            yield return null;
        }

        ResetVisualScale(visualRt, visualBaseScale);

        if (rt != null && rt)
            SnapToGrid(tileSize);
    }

    /// <summary>
    /// Multi-segment yörünge ile hareket. Diagonal slide gibi durumlarda kullanılır:
    /// Taş kaynak sütunda corner hizasına kadar düz iner, sonra diagonal step yapar,
    /// sonra hedef sütunda final pozisyona iner. Tek smooth path olarak çalışır.
    ///
    /// waypoints[0]   = başlangıç hücresi
    /// waypoints[N]   = son hücre (tile.X/Y'ye snap edilir)
    /// segmentDurations[i] = waypoints[i] -> waypoints[i+1] arası süre
    ///
    /// Segment'ler arası SnapToGrid YOK — flicker olmaz.
    /// Sadece son waypoint sonrası SnapToGrid + opsiyonel settle.
    /// </summary>
    public IEnumerator MoveToGridPath(
        int tileSize,
        Vector2Int[] waypoints,
        float[] segmentDurations,
        AnimationCurve easingCurve = null,
        bool enableSettle = false,
        float settleDuration = 0.06f,
        float settleStrength = 0.04f,
        float settleStretchX = 0f,
        float settleOvershoot = 0f)
    {
        lastFallGeneration = (board != null) ? board.FallGeneration : 0;

        if (this == null || !this)
            yield break;

        if (waypoints == null || waypoints.Length < 2)
            yield break;

        if (segmentDurations == null || segmentDurations.Length < waypoints.Length - 1)
            yield break;

        if (rt == null)
            rt = GetComponent<RectTransform>();

        if (rt == null || !rt)
            yield break;

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);

        RectTransform visualRt = iconImage != null ? iconImage.rectTransform : null;
        // Ev scale CANLI OKUNMAZ (shake-drift dersinin skala hali): önceki esneme/squash
        // yarıda kesildiyse mevcut scale esnik kalmış olabilir — onu "ev" sanmak taşı
        // kalıcı esnik bırakıyordu. İkonun dinlenme scale'i bu kod tabanında HER ZAMAN 1.
        Vector3 visualBaseScale = Vector3.one;
        if (visualRt != null) visualRt.localScale = Vector3.one;   // uçuşa temiz başla

        // İlk waypoint'e konumlan
        Vector2 startPos = GetFallCellAnchoredPosition(waypoints[0].x, waypoints[0].y, tileSize);
        rt.anchoredPosition = startPos;

        // Her segment'i sırayla lerp'le. Aralarda snap YOK.
        int segmentCount = waypoints.Length - 1;

        // Düşüş esnemesi: yalnızca DİKEY inen segmentlerde gerilir; diagonal/yatay
        // adımlarda yumuşakça toparlar (envCurrent sürekli — segment geçişinde pop yok).
        float stretchAmt = board != null ? board.FallStretchAmount : 0f;
        float stretchRecover = board != null ? board.FallStretchRecover : 0.35f;
        bool stretchEnabled = stretchAmt > 0.001f && visualRt != null;
        float totalDur = 0f;
        for (int i = 0; i < segmentCount; i++) totalDur += Mathf.Max(0.0001f, segmentDurations[i]);
        float globalElapsed = 0f;
        float envCurrent = 0f;

        for (int seg = 0; seg < segmentCount; seg++)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            Vector2 segStart = GetFallCellAnchoredPosition(waypoints[seg].x, waypoints[seg].y, tileSize);
            Vector2 segEnd = GetFallCellAnchoredPosition(waypoints[seg + 1].x, waypoints[seg + 1].y, tileSize);

            float segDur = Mathf.Max(0.0001f, segmentDurations[seg]);
            float pixelDist = Vector2.Distance(segStart, segEnd);

            if (pixelDist < 0.5f)
            {
                rt.anchoredPosition = segEnd;
                globalElapsed += segDur;
                continue;
            }

            bool segVerticalDown = segEnd.y < segStart.y - 0.5f
                                   && Mathf.Abs(segEnd.x - segStart.x) < 0.5f;

            float elapsed = 0f;
            while (elapsed < segDur)
            {
                if (rt == null || !rt)
                {
                    ResetVisualScale(visualRt, visualBaseScale);
                    yield break;
                }

                elapsed += Time.deltaTime;
                globalElapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / segDur);
                if (easingCurve != null)
                    t = easingCurve.Evaluate(t);

                rt.anchoredPosition = Vector2.LerpUnclamped(segStart, segEnd, t);

                if (stretchEnabled && visualRt != null)
                {
                    float targetEnv = segVerticalDown
                        ? FallStretchEnv(globalElapsed, totalDur, stretchRecover)
                        : 0f;
                    envCurrent = Mathf.MoveTowards(envCurrent, targetEnv, Time.deltaTime / 0.04f);
                    ApplyFallStretchScale(visualRt, visualBaseScale, stretchAmt, envCurrent);
                }

                yield return null;
            }

            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            rt.anchoredPosition = segEnd;
            // SnapToGrid YOK — bir sonraki segment kaldığı yerden başlar
        }

        if (stretchEnabled && visualRt != null)
            ResetVisualScale(visualRt, visualBaseScale);

        // Son waypoint sonrası snap
        SnapToGrid(tileSize);

        if (!enableSettle || settleDuration <= 0f)
        {
            ResetVisualScale(visualRt, visualBaseScale);
            yield break;
        }

        Vector2 basePos = rt.anchoredPosition;
        // Pozisyon dalışı settleOvershoot ile (küçük) — strength skala squash'ı sürer (gömülme fix'i).
        Vector2 overshootPos = basePos + new Vector2(0f, -settleOvershoot * tileSize);
        Vector2 overUpPos = basePos + new Vector2(0f, settleOvershoot * tileSize * 0.3f);

        float bounceDur = settleDuration;

        float b1 = 0f;
        while (b1 < bounceDur * 0.35f)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            b1 += Time.deltaTime;

            float k = Mathf.Clamp01(b1 / Mathf.Max(0.0001f, bounceDur * 0.35f));
            float eased = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(basePos, overshootPos, eased);

            if (visualRt != null && (settleStretchX > 0f || settleStrength > 0f))
            {
                float sx = 1f + settleStretchX * eased;
                float sy = 1f - settleStrength * eased;   // impact ezilmesi skala ile (pozisyon değil)

                visualRt.localScale = new Vector3(
                    visualBaseScale.x * sx,
                    visualBaseScale.y * sy,
                    visualBaseScale.z);
            }

            yield return null;
        }

        float b2 = 0f;
        while (b2 < bounceDur * 0.35f)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            b2 += Time.deltaTime;

            float k = Mathf.Clamp01(b2 / Mathf.Max(0.0001f, bounceDur * 0.35f));
            float eased = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(overshootPos, overUpPos, eased);

            if (visualRt != null && (settleStretchX > 0f || settleStrength > 0f))
            {
                float revK = 1f - k;
                float sx = 1f + settleStretchX * revK;
                float sy = 1f - settleStrength * revK;

                visualRt.localScale = new Vector3(
                    visualBaseScale.x * sx,
                    visualBaseScale.y * sy,
                    visualBaseScale.z);
            }

            yield return null;
        }

        float b3 = 0f;
        while (b3 < bounceDur * 0.3f)
        {
            if (rt == null || !rt)
            {
                ResetVisualScale(visualRt, visualBaseScale);
                yield break;
            }

            b3 += Time.deltaTime;

            float k = Mathf.Clamp01(b3 / Mathf.Max(0.0001f, bounceDur * 0.3f));
            float eased = k * k;

            rt.anchoredPosition = Vector2.LerpUnclamped(overUpPos, basePos, eased);

            yield return null;
        }

        ResetVisualScale(visualRt, visualBaseScale);

        if (rt != null && rt)
            SnapToGrid(tileSize);
    }

    public void PlaySpecialCreationReveal(TileSpecial special, int tileSize)
    {
        if (special == TileSpecial.None)
            return;

        if (!gameObject.activeInHierarchy)
            return;

        if (iconImage == null)
            return;

        StopSpecialCreationReveal();

        specialCreationRevealRoutine = StartCoroutine(CoSpecialCreationRevealSimple(special, tileSize));

        if (special == TileSpecial.PatchBot)
            propellerView?.PlayCreationAnimation();

        if (special == TileSpecial.SystemOverride)
            overrideSpecialView?.PlayCreation();
    }

    private void StopSpecialCreationReveal()
    {
        if (specialCreationRevealRoutine != null)
        {
            StopCoroutine(specialCreationRevealRoutine);
            specialCreationRevealRoutine = null;
        }

        CleanupSpecialCreationReveal();
    }

    private void CleanupSpecialCreationReveal()
    {
        if (iconImage != null)
        {
            RectTransform iconRt = iconImage.rectTransform;

            if (iconRt != null)
            {
                iconRt.localScale = Vector3.one;
                if (specialRevealVisualCaptured)
                    iconRt.localRotation = specialRevealIconBaseRotation;
            }

            if (specialRevealVisualCaptured)
                iconImage.color = specialRevealIconBaseColor;
        }

        specialRevealVisualCaptured = false;

        if (specialCreationRevealRoot != null)
        {
            Destroy(specialCreationRevealRoot);
            specialCreationRevealRoot = null;
        }

        if (this != null)
        {
            Transform oldHalo = transform.Find("__SpecialCreationHalo");
            if (oldHalo != null)
                Destroy(oldHalo.gameObject);
        }

        if (this != null && lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);

        specialCreationRevealRoutine = null;
    }

    private IEnumerator CoSpecialCreationRevealSimple(TileSpecial special, int tileSize)
    {
        // PendingCreationApplicator already calls this after RefreshIcon and obstacle refresh.
        // Wait one frame so final icon sprite/layout is definitely ready.
        yield return null;

        if (this == null || iconImage == null || iconImage.sprite == null)
        {
            CleanupSpecialCreationReveal();
            yield break;
        }

        // Always re-enforce correct layout after 1-frame wait. Catches preserveAspect
        // and sizeDelta resets from Canvas layout rebuilds that the anchor check missed.
        if (lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);

        RectTransform iconRt = iconImage.rectTransform;

        if (iconRt == null)
        {
            CleanupSpecialCreationReveal();
            yield break;
        }

        // Special tile icon localScale should always be Vector3.one at rest.
        // Capturing iconRt.localScale here can pick up a mid-animation value from
        // a concurrent PlaySpecialCreationMerge (which shrinks/overshoots localScale).
        iconRt.localScale = Vector3.one;
        specialRevealIconBaseScale = Vector3.one;
        specialRevealIconBaseRotation = iconRt.localRotation;
        specialRevealIconBaseColor = iconImage.color;
        specialRevealIconBaseColor.a = 1f;
        specialRevealVisualCaptured = true;

        iconImage.color = specialRevealIconBaseColor;

        specialCreationRevealRoot = new GameObject(
            "__SpecialCreationHalo",
            typeof(RectTransform),
            typeof(Image));

        specialCreationRevealRoot.transform.SetParent(transform, false);
        specialCreationRevealRoot.transform.SetAsLastSibling();

        RectTransform haloRt = specialCreationRevealRoot.GetComponent<RectTransform>();
        haloRt.anchorMin = new Vector2(0.5f, 0.5f);
        haloRt.anchorMax = new Vector2(0.5f, 0.5f);
        haloRt.pivot = new Vector2(0.5f, 0.5f);
        haloRt.anchoredPosition = iconRt.anchoredPosition;
        haloRt.sizeDelta = new Vector2(tileSize * 2.6f, tileSize * 2.6f);
        haloRt.localScale = Vector3.zero;
        haloRt.localRotation = Quaternion.identity;

        Image haloImage = specialCreationRevealRoot.GetComponent<Image>();
        haloImage.sprite = GetOrCreateSpecialRevealHaloSprite();
        haloImage.raycastTarget = false;
        haloImage.color = new Color(1f, 1f, 1f, 0f);

        // Faz sırası (rotasyon yok, sadece büyüme):
        // 1) grow    : imaj + halo birlikte küçükten büyüğe büyür
        // 2) settle  : halo söner + imaj normal boyutuna oturur
        // Süreler board metronomuna (CellTime) oranlı: doğuş ~3 hücre + ~1.75 hücre vuruşu —
        // fall velocity değişince doğuş da aynı oranda hızlanır (senkron kayması olmaz).
        float cellTime = board != null ? board.CellTime : 0.075f;
        float growDuration = cellTime * 3f;
        float settleDuration = cellTime * 1.75f;

        const float maxScale = 1.25f;
        const float haloMaxScale = 1.35f;
        const float haloStartScale = 0.40f;
        const float haloEndScale = 0.45f;
        const float haloPeakAlpha = 0.55f;

        float totalDuration = growDuration + settleDuration;
        float elapsed = 0f;

        haloRt.localScale = Vector3.one * haloStartScale;
        if (haloImage != null)
            haloImage.color = new Color(1f, 1f, 1f, haloPeakAlpha);

        while (elapsed < totalDuration)
        {
            if (this == null || iconImage == null || iconRt == null || specialCreationRevealRoot == null)
            {
                CleanupSpecialCreationReveal();
                yield break;
            }

            elapsed += Time.deltaTime;

            float baseScale;
            float haloScale;
            float haloAlpha;

            if (elapsed < growDuration)
            {
                // 1) İmaj + halo birlikte büyür, rotasyon yok
                float t = Mathf.Clamp01(elapsed / growDuration);
                float e = EaseOutBackLight(t);

                baseScale = Mathf.LerpUnclamped(0.10f, maxScale, e);
                haloScale = Mathf.LerpUnclamped(haloStartScale, haloMaxScale, e);
                haloAlpha = haloPeakAlpha;
            }
            else
            {
                // 2) Halo söner + imaj normale oturur
                float t = Mathf.Clamp01((elapsed - growDuration) / settleDuration);
                float e = EaseOut(t);

                baseScale = Mathf.LerpUnclamped(maxScale, 1f, e);
                haloScale = Mathf.LerpUnclamped(haloMaxScale, haloEndScale, e);
                haloAlpha = haloPeakAlpha * (1f - t);
            }

            iconRt.localScale = new Vector3(
                specialRevealIconBaseScale.x * baseScale,
                specialRevealIconBaseScale.y * baseScale,
                specialRevealIconBaseScale.z);
            iconRt.localRotation = specialRevealIconBaseRotation;

            haloRt.localScale = Vector3.one * haloScale;
            haloRt.localRotation = Quaternion.identity;

            if (haloImage != null)
                haloImage.color = new Color(1f, 1f, 1f, haloAlpha);

            yield return null;
        }

        CleanupSpecialCreationReveal();
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    private static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float EaseOutBackLight(float t)
    {
        t = Mathf.Clamp01(t);

        const float c1 = 1.10f;
        const float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private static Sprite GetOrCreateSpecialRevealHaloSprite()
    {
        if (specialRevealHaloSprite != null)
            return specialRevealHaloSprite;

        const int size = 128;
        const float outerRadius = 0.50f;
        const float ringRadius = 0.34f;
        const float ringThickness = 0.085f;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "GeneratedSpecialCreationHalo";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxR = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                Vector2 dir = p - center;
                float d = dir.magnitude / maxR;

                float ring = 1f - Mathf.Clamp01(Mathf.Abs(d - ringRadius) / ringThickness);
                ring *= ring;

                float glow = 1f - Mathf.Clamp01(d / outerRadius);
                glow = glow * glow * 0.55f;

                float inner = 1f - Mathf.Clamp01(d / 0.24f);
                inner = inner * inner * 0.14f;

                float alpha = Mathf.Clamp01(ring * 1.15f + glow + inner);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply(false, true);

        specialRevealHaloSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f);

        return specialRevealHaloSprite;
    }


    private IEnumerator CoFallSettleImpact(
        int tileSize,
        float duration,
        float strength,
        float stretchX,
        float overshootRatio)
    {
        if (rt == null || !rt)
            yield break;

        RectTransform iconRt = iconImage != null ? iconImage.rectTransform : null;

        Vector2 basePos = rt.anchoredPosition;
        Vector3 baseScale = Vector3.one;   // canlı okuma tuzağı: dinlenme scale'i her zaman 1

        float dur = Mathf.Max(0.01f, duration);

        float squashY = Mathf.Clamp(strength, 0.02f, 0.28f);
        float squashX = Mathf.Clamp(stretchX, 0.00f, 0.16f);
        float overshootPx = tileSize * Mathf.Clamp(overshootRatio, 0.00f, 0.10f);

        Vector2 downPos = basePos + new Vector2(0f, -overshootPx);
        Vector2 reboundPos = basePos + new Vector2(0f, overshootPx * 0.30f);

        Vector3 impactScale = new Vector3(1f + squashX, 1f - squashY, 1f);
        Vector3 reboundScale = new Vector3(1f - squashX * 0.35f, 1f + squashY * 0.20f, 1f);

        float p1Dur = dur * 0.22f;
        float p2Dur = dur * 0.30f;
        float p3Dur = dur * 0.48f;

        float t = 0f;
        while (t < p1Dur)
        {
            if (rt == null || !rt)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, p1Dur));
            float e = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(basePos, downPos, e);

            if (iconRt != null)
                iconRt.localScale = Vector3.LerpUnclamped(baseScale, impactScale, e);

            yield return null;
        }

        t = 0f;
        while (t < p2Dur)
        {
            if (rt == null || !rt)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, p2Dur));
            float e = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(downPos, reboundPos, e);

            if (iconRt != null)
                iconRt.localScale = Vector3.LerpUnclamped(impactScale, reboundScale, e);

            yield return null;
        }

        t = 0f;
        while (t < p3Dur)
        {
            if (rt == null || !rt)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, p3Dur));
            float e = k * k * (3f - 2f * k);

            rt.anchoredPosition = Vector2.LerpUnclamped(reboundPos, basePos, e);

            if (iconRt != null)
                iconRt.localScale = Vector3.LerpUnclamped(reboundScale, baseScale, e);

            yield return null;
        }

        if (rt != null && rt)
            rt.anchoredPosition = basePos;

        if (iconRt != null)
            iconRt.localScale = baseScale;
    }

    private IEnumerator CoSubtleImpact(float duration, float strength)
    {
        if (iconImage == null)
            yield break;

        var iconRt = iconImage.rectTransform;

        if (iconRt == null)
            yield break;

        float s = Mathf.Clamp(strength, 0.02f, 0.2f);
        float dur = Mathf.Max(0.04f, duration);

        Vector3 normal = Vector3.one;
        Vector3 squashed = new Vector3(1f, 1f - s, 1f);

        float down = dur * 0.30f;
        float up = dur * 0.70f;

        float t = 0f;
        while (t < down)
        {
            if (this == null || iconImage == null || iconRt == null)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, down));
            iconRt.localScale = Vector3.LerpUnclamped(normal, squashed, k);

            yield return null;
        }

        t = 0f;
        while (t < up)
        {
            if (this == null || iconImage == null || iconRt == null)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, up));
            float e = 1f - (1f - k) * (1f - k);

            iconRt.localScale = Vector3.LerpUnclamped(squashed, normal, e);

            yield return null;
        }

        if (this != null && iconImage != null && iconRt != null)
            iconRt.localScale = normal;
    }

    private void SetPivotWithoutVisualJump(Vector2 newPivot)
    {
        if (rt == null)
            return;

        Vector2 size = rt.rect.size;
        Vector2 pivotDelta = rt.pivot - newPivot;
        Vector2 anchoredOffset = new Vector2(pivotDelta.x * size.x, pivotDelta.y * size.y);

        rt.pivot = newPivot;
        rt.anchoredPosition += anchoredOffset;
    }

    public TileType GetTileType() => model.type;

    public Sprite GetIconSprite() => iconImage != null ? iconImage.sprite : null;

    public void SetType(TileType type)
    {
        model.type = type;

        // Movable obstacle ikonu model.type'tan bağımsızdır; dummy tip ataması sprite'ı ezmesin.
        if (isMovableObstacleTile && movableObstacleSprite != null)
        {
            SetIcon(movableObstacleSprite);
            return;
        }

        if (board != null)
        {
            var sprite = board.GetIcon(type);
            SetIcon(sprite);
        }
    }

    public void SetIcon(Sprite sprite)
    {
        if (sprite == null)
        {
            Debug.LogError("TileView: icon set to NULL");
            return;
        }

        iconImage.sprite = sprite;
        iconImage.color = Color.white;

        if (lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);
    }

    public void SetIconAlpha(float alpha)
    {
        if (iconImage == null)
            return;

        var c = iconImage.color;
        c.a = Mathf.Clamp01(alpha);
        iconImage.color = c;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (board == null || board.IsBusy)
            return;

        if (board.ActiveBooster != BoardController.BoosterMode.None)
            return;

        transform.localScale = Vector3.one;

        wasDragging = true;
        dragConsumedSwap = false;

        dragStartAnchored = rt.anchoredPosition;
        dragStartScreen = eventData.position;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt,
            eventData.position,
            eventData.pressEventCamera,
            out dragStartLocalPointer);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (board == null || board.IsBusy)
            return;

        if (board.ActiveBooster != BoardController.BoosterMode.None)
            return;

        if (dragConsumedSwap)
            return;

        // Görsel takip (kozmetik): taş parmağı board'un kendi düzleminde takip etsin.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt,
            eventData.position,
            eventData.pressEventCamera,
            out var curLocal);

        var localDelta = curLocal - dragStartLocalPointer;

        float max = board.TileSize * 0.45f;
        localDelta.x = Mathf.Clamp(localDelta.x, -max, max);
        localDelta.y = Mathf.Clamp(localDelta.y, -max, max);
        rt.anchoredPosition = dragStartAnchored + localDelta;

        // Swap YÖNÜ ekran hareketinden hesaplanır — board'un eğimi/perspektifi
        // local düzlemi büküyor; ekran-uzayı "yukarı çektim = yukarı swap" garantisi verir.
        Vector2 screenDelta = eventData.position - dragStartScreen;
        float threshold = Screen.height * 0.022f;

        if (Mathf.Abs(screenDelta.x) < threshold && Mathf.Abs(screenDelta.y) < threshold)
            return;

        int dirX = 0;
        int dirY = 0;

        if (Mathf.Abs(screenDelta.x) > Mathf.Abs(screenDelta.y))
            dirX = screenDelta.x > 0 ? 1 : -1;
        else
            dirY = screenDelta.y > 0 ? -1 : 1; // ekranda yukarı = grid yukarı (-1)

        dragConsumedSwap = true;

        SnapToGrid(board.TileSize);
        board.RequestSwapFromDrag(this, dirX, dirY);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragConsumedSwap && board != null)
            SnapToGrid(board.TileSize);

        StartCoroutine(ResetWasDragging());
    }

    private IEnumerator ResetWasDragging()
    {
        yield return null;
        wasDragging = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        boosterFiredOnDown = false;
        if (board != null && board.ActiveBooster != BoardController.BoosterMode.None)
        {
            boosterFiredOnDown = true;
            board.OnTileClicked(this);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (wasDragging || boosterFiredOnDown)
            return;

        board?.OnTileClicked(this);
    }

    public TileSpecial GetSpecial() => model.special;

    public void SetOverrideBaseType(TileType baseType, bool deferVisualUpdate = false)
    {
        model?.SetOverrideBaseType(baseType);
        if (!deferVisualUpdate)
            RefreshIcon();
    }

    public void SetSpecial(TileSpecial sp, bool deferVisualUpdate = false)
    {
        model.SetSpecial(sp);

        if (!deferVisualUpdate)
        {
            RefreshIcon();

            if (lastAppliedTileSize > 0)
                ApplyTileSize(lastAppliedTileSize);
        }
    }

    public void SetIconScale(float scale)
    {
        iconScale = Mathf.Clamp(scale, 0.5f, 1f);

        if (lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);
    }

    public void SetUseFullCellIcon(bool useFullCell)
    {
        useFullCellIcon = useFullCell;

        if (lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);
    }

    public void SetMovableObstacleTile(bool value)
    {
        isMovableObstacleTile = value;

        if (lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);
    }

    public void SetFullCellMovableSprite(bool value)
    {
        isFullCellMovableSprite = value;

        if (lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);
    }

    // Movable obstacle sprite'ını sakla + uygula. Saklandığı için RefreshIcon/SetType
    // çağrılsa bile ikon obstacle sprite'ında kalır (model.type'a dönmez).
    public void SetMovableObstacleSprite(Sprite sprite)
    {
        movableObstacleSprite = sprite;
        if (sprite != null)
            SetIcon(sprite);
    }

    public void SetVisualLayout(TileVisualLayout layout)
    {
        visualLayout = layout;

        if (lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);
    }

    public void SetIconSize(Vector2 size)
    {
        iconSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));

        if (lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);
    }

    public void ApplyTileSize(int tileSize)
    {
        lastAppliedTileSize = tileSize;

        if (rt == null)
            rt = GetComponent<RectTransform>();

        rt.sizeDelta = new Vector2(tileSize, tileSize);

        if (iconImage == null)
            return;

        var irt = iconImage.rectTransform;

        bool isSpecial = model != null && model.special != TileSpecial.None;
        bool isMovable = isMovableObstacleTile;

        if (isMovable && isFullCellMovableSprite)
        {
            irt.anchorMin        = Vector2.zero;
            irt.anchorMax        = Vector2.one;
            irt.pivot            = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta        = Vector2.zero;
            iconImage.preserveAspect = false;
            return;
        }

        bool shouldFillCell =
            !isMovable && !isSpecial &&
            (useFullCellIcon || visualLayout == TileVisualLayout.FillCell);

        bool shouldCenter =
            visualLayout == TileVisualLayout.Centered ||
            isSpecial ||
            isMovable;

        if (shouldFillCell)
        {
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = Vector2.zero;

            iconImage.preserveAspect = false;
            return;
        }

        float tileRatio = Mathf.Max(0.01f, tileSize / IconReferenceTileSize);
        Vector2 scaledIconSize = iconSize * (tileRatio * iconScale);

        if (isMovable)
            scaledIconSize *= 0.95f;

        irt.anchorMin = new Vector2(0.5f, 0.5f);
        irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = scaledIconSize;

        iconImage.preserveAspect = true;

        if (shouldCenter)
        {
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
        }
        else
        {
            irt.pivot = new Vector2(0.5f, 0f);
            irt.anchoredPosition = new Vector2(0f, -scaledIconSize.y * 0.5f);
        }

        if (isSpecial)
        {
            irt.localScale = Vector3.one;

            if (board != null)
            {
                if (board.SpecialFillCell)
                {
                    irt.sizeDelta = new Vector2(tileSize, tileSize);
                    iconImage.preserveAspect = true;
                }

                // Always apply anchoredPosition so elevation (including negative = shift down)
                // overrides any stale offset. With elevation=0 this is a no-op vs Vector2.zero.
                irt.anchoredPosition = new Vector2(0f, tileSize * board.SpecialElevation);
            }
        }

        if (propellerView != null)
        {
            var prt = (RectTransform)propellerView.transform;
            prt.anchorMin        = irt.anchorMin;
            prt.anchorMax        = irt.anchorMax;
            prt.pivot            = irt.pivot;
            prt.sizeDelta        = irt.sizeDelta;
            prt.anchoredPosition = irt.anchoredPosition;
        }

        if (overrideSpecialView != null)
        {
            var ort = (RectTransform)overrideSpecialView.transform;
            ort.anchorMin        = irt.anchorMin;
            ort.anchorMax        = irt.anchorMax;
            ort.pivot            = irt.pivot;
            ort.sizeDelta        = irt.sizeDelta;
            ort.anchoredPosition = irt.anchoredPosition;
        }
    }


    public void PlayBeingLandedOnSquash(float duration = 0.22f, float strength = 0.46f)
    {
        if (this == null || !isActiveAndEnabled)
            return;

        StartCoroutine(CoFallSettleSquash(duration, strength));
    }

    private IEnumerator CoImpactSquash(float duration, float strength, float stretchX, float overshoot, int tileSize)
    {
        if (iconImage == null)
            yield break;

        var iconRt = iconImage.rectTransform;

        if (iconRt == null)
            yield break;

        float sy = Mathf.Clamp(strength, 0.05f, 0.7f);
        float sx = Mathf.Clamp(stretchX, 0f, 0.5f);
        float dur = Mathf.Max(0.05f, duration);

        Vector3 normal = Vector3.one;
        Vector3 squashed = new Vector3(1f + sx, 1f - sy, 1f);
        Vector3 stretched = new Vector3(1f - sx * 0.3f, 1f + sy * 0.15f, 1f);

        float p1 = dur * 0.25f;
        float p2 = dur * 0.35f;
        float p3 = dur * 0.40f;

        float t = 0f;
        while (t < p1)
        {
            if (this == null || iconImage == null || iconRt == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, p1));
            float e = 1f - (1f - k) * (1f - k);

            iconRt.localScale = Vector3.LerpUnclamped(normal, squashed, e);

            yield return null;
        }

        t = 0f;
        while (t < p2)
        {
            if (this == null || iconImage == null || iconRt == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, p2));
            float e = 1f - (1f - k) * (1f - k);

            iconRt.localScale = Vector3.LerpUnclamped(squashed, stretched, e);

            yield return null;
        }

        t = 0f;
        while (t < p3)
        {
            if (this == null || iconImage == null || iconRt == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, p3));
            float e = 1f - (1f - k) * (1f - k);

            iconRt.localScale = Vector3.LerpUnclamped(stretched, normal, e);

            yield return null;
        }

        if (this != null && iconImage != null && iconRt != null)
            iconRt.localScale = normal;
    }

    // Legacy — PlayBeingLandedOnSquash için
    private IEnumerator CoFallSettleSquash(float duration, float strength)
    {
        if (iconImage == null)
            yield break;

        var iconRt = iconImage.rectTransform;

        if (iconRt == null)
            yield break;

        float s = Mathf.Clamp(strength, 0f, 0.9f);

        Vector3 normal = Vector3.one;
        Vector3 squashed = new Vector3(1f, Mathf.Max(0.1f, 1f - s), 1f);

        float downTime = Mathf.Max(0.001f, duration * 0.20f);
        float upTime = Mathf.Max(0.001f, duration * 0.80f);

        float t = 0f;
        while (t < downTime)
        {
            if (iconRt == null)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / downTime);
            iconRt.localScale = Vector3.LerpUnclamped(normal, squashed, k * k);

            yield return null;
        }

        t = 0f;
        while (t < upTime)
        {
            if (iconRt == null)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / upTime);
            float e = 1f - (1f - k) * (1f - k);

            iconRt.localScale = Vector3.LerpUnclamped(squashed, normal, e);

            yield return null;
        }

        if (iconRt != null)
            iconRt.localScale = normal;
    }

    private void RestorePivot(Vector2 originalPivot)
    {
        if (rt == null || !rt)
            return;

        if (rt.pivot != originalPivot)
            SetPivotWithoutVisualJump(originalPivot);
    }

    public bool GetOverrideBaseType(out TileType type)
    {
        return model.TryGetOverrideBaseType(out type);
    }
}
