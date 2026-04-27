using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class TileView : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 IconReferenceSize = new Vector2(100f, 100f);
    private const float IconReferenceTileSize = 100f;

    [SerializeField] private Image iconImage;
    private TileModel model;

    public int X { get; private set; }
    public int Y { get; private set; }

    private BoardController board;
    private RectTransform rt;
    private RectTransform parentRt;

    // Drag state
    private Vector2 dragStartAnchored;
    private Vector2 dragStartLocalPointer;
    private bool dragConsumedSwap;
    private bool wasDragging;

    [SerializeField, Range(0.5f, 1f)]
    [FormerlySerializedAs("runtimeIconScale")]
    private float iconScale = 0.98f;
    [SerializeField] private Vector2 iconSize = new Vector2(100f, 100f);

    [SerializeField] private bool useFullCellIcon = false;

    public enum TileVisualLayout
    {
        BottomAligned,
        Centered,
        FillCell
    }

    [SerializeField] private TileVisualLayout visualLayout = TileVisualLayout.Centered;

    private bool isMovableObstacleTile = false;

    private int lastAppliedTileSize;

    // Bu taşın düştüğü CollapseAndSpawnAnimated nesil ID'si. -1 = hiç düşmedi.
    private int lastFallGeneration = -1;

    public RectTransform RectTransform => rt != null ? rt : (RectTransform)transform;
    public Image IconImage => iconImage;

    public bool IsPlannedToMoveThisFallPass { get; private set; }

    // ============================================================
    // SABİT HIZ fall modeli (kullanıcı vizyonu — hızlandırıldı)
    //
    // Tüm taşlar V_MAX ile başlar ve V_MAX'ta devam eder. İvmelenme yok.
    // Düşüş süresi = mesafe / V_MAX (deterministik, basit).
    //
    // Akordiyon hissi FallAction içindeki adaptive start delay ile gelir:
    //   - Farklı taşlar farklı zamanlarda BAŞLAR
    //   - Ama hepsi AYNI hızda iner
    //   - Aralarındaki görsel mesafe = start delay farkı × V_MAX
    //
    // 35 → 42: %20 daha hızlı fall motion.
    // ============================================================
    private const float FALL_VELOCITY = 30.0f;

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
        if (transform.parent == null || board == null) return;
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

    public void Init(BoardController board, int x, int y)
    {
        this.board = board;
        X = x;
        Y = y;
        IsPlannedToMoveThisFallPass = false;

        ResetVisualState();
        dragConsumedSwap = false;
        wasDragging = false;

        // ── DEBUG (sadece ilk tile için logla) ──
        if (x == 1 && y == 1)
        {
            var cam = GetComponentInParent<Canvas>()?.worldCamera;
            if (cam != null)
            {
                Debug.Log($"[TileDebug] Camera clearFlags: {cam.clearFlags}");
                Debug.Log($"[TileDebug] Camera bgColor: {cam.backgroundColor}");
                Debug.Log($"[TileDebug] Camera HDR: {cam.allowHDR}");

                var vol = cam.GetComponent<UnityEngine.Rendering.Volume>();
                Debug.Log($"[TileDebug] PostProcess Volume: {(vol != null ? "VAR" : "YOK")}");
            }

            var parentImages = GetComponentsInParent<UnityEngine.UI.Image>(true);
            foreach (var img in parentImages)
                Debug.Log($"[TileDebug] Parent Image: {img.gameObject.name} color={img.color} raycast={img.raycastTarget}");
        }
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
    }

    public bool TryGetCellState(out BoardCellStateSnapshot state)
    {
        state = default;
        return board != null && board.TryGetCellState(X, Y, out state);
    }


    public void RefreshIcon()
    {
        if (model == null || board == null) return;

        if (model.special == TileSpecial.SystemOverride)
        {
            Sprite sp = null;
            if (model.hasOverrideBaseType)
                sp = board.GetOverrideIcon(model.overrideBaseType);
            if (sp == null)
                sp = board.GetSpecialIcon(model.special);
            if (sp != null) SetIcon(sp);
            else SetIcon(board.GetIcon(model.type));
        }
        else if (model.special != TileSpecial.None)
        {
            var sp = board.GetSpecialIcon(model.special);
            if (sp != null) SetIcon(sp);
            else SetIcon(board.GetIcon(model.type));
        }
        else
        {
            SetIcon(board.GetIcon(model.type));
        }
    }

    public void SnapToGrid(int tileSize)
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        if (parentRt == null) parentRt = rt.parent as RectTransform;

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);

        rt.anchoredPosition = new Vector2(X * tileSize, -Y * tileSize);

        ApplyTileSize(tileSize);
    }


    // ============================================================
    // MoveToGrid v4 — İVMELİ FİZİK + HIZLANDIRILMIŞ PARAMETRELER
    //
    // Model:
    //   v(t) = min(V_START + GRAVITY * t, V_MAX)
    //   traveled(t) = integral v(t) dt
    //   bitiş: traveled == distance
    //
    // duration parametresi YOK SAYILIR — süre fizik tarafından türetilir.
    // easingCurve YOK SAYILIR — curve yerine fizik denklemi.
    // ============================================================

    public IEnumerator MoveToGrid(
      int tileSize,
      float duration,                      // YOK SAYILIR (backward compat)
      AnimationCurve easingCurve = null,   // YOK SAYILIR (backward compat)
      bool enableSettle = false,
      float settleDuration = 0.06f,
      float settleStrength = 0.04f,
      float settleStretchX = 0f,
      float settleOvershoot = 0f)
    {
        lastFallGeneration = (board != null) ? board.FallGeneration : 0;
        if (rt == null || !rt) yield break;

        RectTransform visualRt = iconImage != null ? iconImage.rectTransform : null;
        Vector3 visualBaseScale = visualRt != null ? visualRt.localScale : Vector3.one;

        Vector2 start = rt.anchoredPosition;
        Vector2 end;

        bool isSpecial = model != null && model.special != TileSpecial.None;
        bool isMovable = isMovableObstacleTile;

        // Movable obstacle tile gibi hareket eder ama special offset almaz.
        // Böylece hücre merkezine/pozisyonuna tam oturur.
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

        // ======= SABİT HIZ FİZİĞİ =======
        float totalPixels = Mathf.Abs(end.y - start.y);

        if (totalPixels < 0.5f)
        {
            if (rt != null && rt)
            {
                rt.anchoredPosition = end;
                SnapToGrid(tileSize);
            }

            yield break;
        }

        float totalCells = totalPixels / Mathf.Max(1f, (float)tileSize);
        float direction = end.y < start.y ? -1f : 1f;

        // Sabit hız — ivmelenme yok, taş tam hızında başlar ve tam hızında biter
        float traveledCells = 0f;

        while (traveledCells < totalCells)
        {
            if (rt == null || !rt) yield break;

            float dt = Time.deltaTime;

            float deltaCells = FALL_VELOCITY * dt;
            traveledCells += deltaCells;

            if (traveledCells > totalCells)
                traveledCells = totalCells;

            float traveledPixels = traveledCells * tileSize * direction;
            rt.anchoredPosition = new Vector2(end.x, start.y + traveledPixels);

            yield return null;
        }

        if (rt == null || !rt) yield break;

        rt.anchoredPosition = end;
        SnapToGrid(tileSize);

        // ======= SETTLE / BOUNCE =======
        if (!enableSettle || settleDuration <= 0f)
            yield break;

        Vector2 overshoot = end + new Vector2(0f, -settleStrength * tileSize);
        Vector2 overUp = end + new Vector2(0f, settleStrength * tileSize * 0.3f);

        float bounceDur = settleDuration;

        float b1 = 0f;
        while (b1 < bounceDur * 0.35f)
        {
            if (rt == null || !rt) yield break;

            b1 += Time.deltaTime;

            float k = Mathf.Clamp01(b1 / (bounceDur * 0.35f));
            float eased = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(end, overshoot, eased);

            if (visualRt != null && settleStretchX > 0f)
            {
                float sx = 1f + settleStretchX * eased;
                float sy = 1f - settleStretchX * eased * 0.5f;

                visualRt.localScale = new Vector3(
                    visualBaseScale.x * sx,
                    visualBaseScale.y * sy,
                    visualBaseScale.z
                );
            }

            yield return null;
        }

        float b2 = 0f;
        while (b2 < bounceDur * 0.35f)
        {
            if (rt == null || !rt) yield break;

            b2 += Time.deltaTime;

            float k = Mathf.Clamp01(b2 / (bounceDur * 0.35f));
            float eased = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(overshoot, overUp, eased);

            if (visualRt != null && settleStretchX > 0f)
            {
                float revK = 1f - k;
                float sx = 1f + settleStretchX * revK;
                float sy = 1f - settleStretchX * revK * 0.5f;

                visualRt.localScale = new Vector3(
                    visualBaseScale.x * sx,
                    visualBaseScale.y * sy,
                    visualBaseScale.z
                );
            }

            yield return null;
        }

        float b3 = 0f;
        while (b3 < bounceDur * 0.3f)
        {
            if (rt == null || !rt) yield break;

            b3 += Time.deltaTime;

            float k = Mathf.Clamp01(b3 / (bounceDur * 0.3f));
            float eased = k * k;

            rt.anchoredPosition = Vector2.LerpUnclamped(overUp, end, eased);

            yield return null;
        }

        if (visualRt != null)
            visualRt.localScale = visualBaseScale;

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

        if (rt == null)
            rt = GetComponent<RectTransform>();

        if (rt == null || !rt)
            yield break;

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);

        RectTransform visualRt = iconImage != null ? iconImage.rectTransform : null;
        Vector3 visualBaseScale = visualRt != null ? visualRt.localScale : Vector3.one;

        Vector2 start = GetFallCellAnchoredPosition(fromX, fromY, tileSize);
        Vector2 end = GetFallCellAnchoredPosition(toX, toY, tileSize);

        // Kritik: TileView.X/Y zaten logical final state olabilir.
        // Bu nedenle visual baslangici record.from'dan zorla kuruyoruz.
        rt.anchoredPosition = start;

        float totalPixels = Vector2.Distance(start, end);

        if (totalPixels < 0.5f)
        {
            rt.anchoredPosition = end;
            SnapToGrid(tileSize);
            yield break;
        }

        float totalCells = totalPixels / Mathf.Max(1f, tileSize);
        float traveledCells = 0f;

        while (traveledCells < totalCells)
        {
            if (rt == null || !rt)
                yield break;

            float deltaCells = FALL_VELOCITY * Time.deltaTime;
            traveledCells += deltaCells;

            if (traveledCells > totalCells)
                traveledCells = totalCells;

            float t = Mathf.Clamp01(traveledCells / totalCells);

            // Easing kullanmiyoruz; referans videodaki gibi sabit hiz.
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, t);

            yield return null;
        }

        if (rt == null || !rt)
            yield break;

        rt.anchoredPosition = end;
        SnapToGrid(tileSize);

        if (!enableSettle || settleDuration <= 0f)
            yield break;

        Vector2 basePos = rt.anchoredPosition;
        Vector2 overshoot = basePos + new Vector2(0f, -settleStrength * tileSize);
        Vector2 overUp = basePos + new Vector2(0f, settleStrength * tileSize * 0.3f);

        float bounceDur = settleDuration;

        float b1 = 0f;
        while (b1 < bounceDur * 0.35f)
        {
            if (rt == null || !rt)
                yield break;

            b1 += Time.deltaTime;

            float k = Mathf.Clamp01(b1 / Mathf.Max(0.0001f, bounceDur * 0.35f));
            float eased = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(basePos, overshoot, eased);

            if (visualRt != null && settleStretchX > 0f)
            {
                float sx = 1f + settleStretchX * eased;
                float sy = 1f - settleStretchX * eased * 0.5f;

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
                yield break;

            b2 += Time.deltaTime;

            float k = Mathf.Clamp01(b2 / Mathf.Max(0.0001f, bounceDur * 0.35f));
            float eased = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.LerpUnclamped(overshoot, overUp, eased);

            if (visualRt != null && settleStretchX > 0f)
            {
                float revK = 1f - k;
                float sx = 1f + settleStretchX * revK;
                float sy = 1f - settleStretchX * revK * 0.5f;

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
                yield break;

            b3 += Time.deltaTime;

            float k = Mathf.Clamp01(b3 / Mathf.Max(0.0001f, bounceDur * 0.3f));
            float eased = k * k;

            rt.anchoredPosition = Vector2.LerpUnclamped(overUp, basePos, eased);

            yield return null;
        }

        if (visualRt != null)
            visualRt.localScale = visualBaseScale;

        if (rt != null && rt)
            SnapToGrid(tileSize);
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
        Vector3 baseScale = iconRt != null ? iconRt.localScale : Vector3.one;

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
        if (iconImage == null) yield break;
        var iconRt = iconImage.rectTransform;
        if (iconRt == null) yield break;

        float s = Mathf.Clamp(strength, 0.02f, 0.2f);
        float dur = Mathf.Max(0.04f, duration);
        Vector3 normal = Vector3.one;
        Vector3 squashed = new Vector3(1f, 1f - s, 1f);

        float down = dur * 0.30f;
        float up = dur * 0.70f;

        float t = 0f;
        while (t < down)
        {
            if (this == null || iconImage == null || iconRt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / down);
            iconRt.localScale = Vector3.LerpUnclamped(normal, squashed, k);
            yield return null;
        }

        t = 0f;
        while (t < up)
        {
            if (this == null || iconImage == null || iconRt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / up);
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
    }
    public void SetIconAlpha(float alpha)
    {
        if (iconImage == null) return;

        var c = iconImage.color;
        c.a = Mathf.Clamp01(alpha);
        iconImage.color = c;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (board == null || board.IsBusy) return;
        if (board.ActiveBooster != BoardController.BoosterMode.None) return;

        transform.localScale = Vector3.one;

        wasDragging = true;
        dragConsumedSwap = false;

        dragStartAnchored = rt.anchoredPosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt, eventData.position, eventData.pressEventCamera, out dragStartLocalPointer
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (board == null || board.IsBusy) return;
        if (board.ActiveBooster != BoardController.BoosterMode.None) return;
        if (dragConsumedSwap) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt, eventData.position, eventData.pressEventCamera, out var curLocal
        );

        var delta = curLocal - dragStartLocalPointer;

        float max = board.TileSize * 0.45f;
        delta.x = Mathf.Clamp(delta.x, -max, max);
        delta.y = Mathf.Clamp(delta.y, -max, max);

        rt.anchoredPosition = dragStartAnchored + delta;

        float threshold = board.TileSize * 0.25f;
        if (Mathf.Abs(delta.x) < threshold && Mathf.Abs(delta.y) < threshold) return;

        int dirX = 0, dirY = 0;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            dirX = delta.x > 0 ? 1 : -1;
        else
            dirY = delta.y > 0 ? -1 : 1;

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

    IEnumerator ResetWasDragging()
    {
        yield return null;
        wasDragging = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (wasDragging) return;
        board?.OnTileClicked(this);
    }

    public TileSpecial GetSpecial() => model.special;

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

        if (rt == null) rt = GetComponent<RectTransform>();

        rt.sizeDelta = new Vector2(tileSize, tileSize);

        if (iconImage == null)
            return;

        var irt = iconImage.rectTransform;

        bool isSpecial = model != null && model.special != TileSpecial.None;
        bool isMovable = isMovableObstacleTile;

        // Static obstacle zaten GridSpawner.DrawObstacleImage() ile ayrı çiziliyor
        // ve hücreyi tamamen kaplıyor.
        //
        // Movable obstacle ise TileView üzerinden geldiği için burada özellikle
        // FillCell/useFullCellIcon yoluna sokmuyoruz.
        bool shouldFillCell = !isMovable && (useFullCellIcon || visualLayout == TileVisualLayout.FillCell);

        // Movable obstacle her zaman hücre merkezinde dursun.
        bool shouldCenter = visualLayout == TileVisualLayout.Centered || isSpecial || isMovable;

        if (shouldFillCell)
        {
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = Vector2.zero;

            // Sadece static olmayan, normal full-cell tile/special görsellerde kullanılır.
            iconImage.preserveAspect = false;
            return;
        }

        float tileRatio = Mathf.Max(0.01f, tileSize / IconReferenceTileSize);
        Vector2 scaledIconSize = iconSize * (tileRatio * iconScale);

        // Movable obstacle hücreyi tam kaplamasın.
        // Static obstacle bu metoda girmediği için bu scale sadece movable için güvenli.
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
    }
    public void PlayBeingLandedOnSquash(float duration = 0.22f, float strength = 0.46f)
    {
        if (this == null || !isActiveAndEnabled) return;
        StartCoroutine(CoFallSettleSquash(duration, strength));
    }

    private IEnumerator CoImpactSquash(float duration, float strength, float stretchX, float overshoot, int tileSize)
    {
        if (iconImage == null) yield break;
        var iconRt = iconImage.rectTransform;
        if (iconRt == null) yield break;

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
            if (this == null || iconImage == null || iconRt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / p1);
            float e = 1f - (1f - k) * (1f - k);
            iconRt.localScale = Vector3.LerpUnclamped(normal, squashed, e);
            yield return null;
        }

        t = 0f;
        while (t < p2)
        {
            if (this == null || iconImage == null || iconRt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / p2);
            float e = 1f - (1f - k) * (1f - k);
            iconRt.localScale = Vector3.LerpUnclamped(squashed, stretched, e);
            yield return null;
        }

        t = 0f;
        while (t < p3)
        {
            if (this == null || iconImage == null || iconRt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / p3);
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
        if (iconImage == null) yield break;
        var iconRt = iconImage.rectTransform;
        if (iconRt == null) yield break;

        float s = Mathf.Clamp(strength, 0f, 0.9f);
        Vector3 normal = Vector3.one;
        Vector3 squashed = new Vector3(1f, Mathf.Max(0.1f, 1f - s), 1f);

        float downTime = Mathf.Max(0.001f, duration * 0.20f);
        float upTime = Mathf.Max(0.001f, duration * 0.80f);

        float t = 0f;
        while (t < downTime)
        {
            if (iconRt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / downTime);
            iconRt.localScale = Vector3.LerpUnclamped(normal, squashed, k * k);
            yield return null;
        }

        t = 0f;
        while (t < upTime)
        {
            if (iconRt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / upTime);
            float e = 1f - (1f - k) * (1f - k);
            iconRt.localScale = Vector3.LerpUnclamped(squashed, normal, e);
            yield return null;
        }

        if (iconRt != null) iconRt.localScale = normal;
    }

    private void RestorePivot(Vector2 originalPivot)
    {
        if (rt == null || !rt) return;
        if (rt.pivot != originalPivot)
            SetPivotWithoutVisualJump(originalPivot);
    }

    public void SetOverrideBaseType(TileType type) => model.SetOverrideBaseType(type);

    public bool GetOverrideBaseType(out TileType type) => model.TryGetOverrideBaseType(out type);
}