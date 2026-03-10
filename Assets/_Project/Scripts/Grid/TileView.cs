using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TileView : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);

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

    private float runtimeIconScale = 0.98f;
    private int lastAppliedTileSize;

    // Bu taşın düştüğü CollapseAndSpawnAnimated nesil ID'si. -1 = hiç düşmedi.
    private int lastFallGeneration = -1;

    public RectTransform RectTransform => rt != null ? rt : (RectTransform)transform;
    public Image IconImage => iconImage;

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

        if (model.special != TileSpecial.None)
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

    public IEnumerator MoveToGrid(
        int tileSize,
        float duration,
        AnimationCurve easingCurve = null,
        bool enableSettle = false,
        float settleDuration = 0.06f,
        float settleStrength = 0.04f)
    {
        lastFallGeneration = (board != null) ? board.FallGeneration : 0;

        Vector2 start = rt.anchoredPosition;
        Vector2 end = new Vector2(X * tileSize, -Y * tileSize);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float normalizedT = Mathf.Clamp01(t);
            float s;

            if (easingCurve != null && easingCurve.length > 0)
                s = Mathf.Clamp01(easingCurve.Evaluate(normalizedT));
            else
                s = normalizedT * normalizedT * normalizedT * (normalizedT * (6f * normalizedT - 15f) + 10f);

            rt.anchoredPosition = Vector2.Lerp(start, end, s);
            yield return null;
        }

        rt.anchoredPosition = end;
        SnapToGrid(tileSize);

        bool movedDown = end.y < start.y - 0.5f;
        if (movedDown && board != null && enableSettle)
        {
            TileView tileBelow = board.GetTileViewAt(X, Y + 1);
            if (tileBelow != null && tileBelow != this)
            {
                tileBelow.PlayBeingLandedOnSquash(settleDuration, settleStrength);
            }
        }

        SnapToGrid(tileSize);
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
        float currentAlpha = iconImage != null ? iconImage.color.a : 1f;
        iconImage.color = new Color(1f, 1f, 1f, currentAlpha);
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
        }
    }

    public void SetIconScale(float scale)
    {
        runtimeIconScale = Mathf.Clamp(scale, 0.5f, 1f);
        if (lastAppliedTileSize > 0)
            ApplyTileSize(lastAppliedTileSize);
    }

    public void ApplyTileSize(int tileSize)
    {
        lastAppliedTileSize = tileSize;

        if (rt == null) rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tileSize, tileSize);

        if (iconImage != null)
        {
            var irt = iconImage.rectTransform;
            float s = tileSize * runtimeIconScale;

            irt.sizeDelta = new Vector2(s, s);
            irt.anchorMin = new Vector2(0.5f, 0.5f);
            irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0f);
            irt.anchoredPosition = new Vector2(0f, -s * 0.5f);
        }
    }

    public void PlayBeingLandedOnSquash(float duration = 0.22f, float strength = 0.20f)
    {
        if (this == null) return;
        StartCoroutine(CoLandedOnSquash(duration, strength));
    }

    private IEnumerator CoLandedOnSquash(float duration, float strength)
    {
        Transform tr = (iconImage != null) ? (Transform)iconImage.rectTransform : transform;
        if (tr == null) yield break;

        float s = Mathf.Clamp(strength, 0f, 0.9f);
        float squashY = Mathf.Max(0.6f, 1f - s * 0.9f);
        float stretchX = 1f + s * 0.35f;

        Vector3 normal = Vector3.one;
        Vector3 squashed = new Vector3(stretchX, squashY, 1f);

        float half = Mathf.Max(0.001f, duration * 0.32f);
        float back = Mathf.Max(0.001f, duration * 0.68f);

        float t = 0f;
        while (t < half)
        {
            if (tr == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            float e = 1f - (1f - k) * (1f - k);
            tr.localScale = Vector3.LerpUnclamped(normal, squashed, e);
            yield return null;
        }

        t = 0f;
        while (t < back)
        {
            if (tr == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / back);
            float e = 1f - (1f - k) * (1f - k);
            tr.localScale = Vector3.LerpUnclamped(squashed, normal, e);
            yield return null;
        }

        if (tr != null)
            tr.localScale = normal;
    }

    public void SetOverrideBaseType(TileType type) => model.SetOverrideBaseType(type);

    public bool GetOverrideBaseType(out TileType type) => model.TryGetOverrideBaseType(out type);
}