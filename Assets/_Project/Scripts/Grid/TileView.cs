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
    private UnityEngine.Vector2 dragStartAnchored;
    private UnityEngine.Vector2 dragStartLocalPointer;
    private bool dragConsumedSwap;
    private bool wasDragging;

    private float runtimeIconScale = 0.98f;
    private int lastAppliedTileSize;

    private void Awake()
    {
        model = GetComponent<TileModel>();
        rt = GetComponent<RectTransform>();
        parentRt = rt.parent as RectTransform;

        if (iconImage == null)
        {
            var imgs = GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
                if (img.gameObject.name == "Icon")
                    iconImage = img;
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
        X = x; Y = y;
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
    }

    public void SetCoords(int x, int y)
    {
        X = x; Y = y;
    }
    void RefreshIcon()
    {
        if (model == null || board == null) return;

        if (model.special != TileSpecial.None)
        {
            // Special ikon
            var sp = board.GetSpecialIcon(model.special);
            if (sp != null) SetIcon(sp);
            else SetIcon(board.GetIcon(model.type));
        }
        else
        {
            // Normal ikon
            SetIcon(board.GetIcon(model.type));
        }
    }


    public void SnapToGrid(int tileSize)
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        if (parentRt == null) parentRt = rt.parent as RectTransform;

        rt.anchorMin = new UnityEngine.Vector2(0, 1);
        rt.anchorMax = new UnityEngine.Vector2(0, 1);
        rt.pivot     = new UnityEngine.Vector2(0, 1);
        rt.anchoredPosition = new UnityEngine.Vector2(X * tileSize, -Y * tileSize);
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
        UnityEngine.Vector2 start = rt.anchoredPosition;
        UnityEngine.Vector2 end = new UnityEngine.Vector2(X * tileSize, -Y * tileSize);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float normalizedT = Mathf.Clamp01(t);
            float s;

            if (easingCurve != null && easingCurve.length > 0)
                s = Mathf.Clamp01(easingCurve.Evaluate(normalizedT));
            else
                s = normalizedT * normalizedT * normalizedT * (normalizedT * (6f * normalizedT - 15f) + 10f); // smootherstep

            rt.anchoredPosition = UnityEngine.Vector2.Lerp(start, end, s);
            yield return null;
        }

        rt.anchoredPosition = end;
        SnapToGrid(tileSize);

        if (!enableSettle)
            yield break;


        // Drag/clear akışlarıyla çakışmaması için settle bitiminde net olarak resetlenir.
        transform.localScale = Vector3.one;

        float clampedStrength = Mathf.Clamp(settleStrength, 0f, 0.15f);
        Vector3 settleScale = new Vector3(1f + clampedStrength, 1f - clampedStrength, 1f);
        float settleHalf = Mathf.Max(0.0001f, settleDuration * 0.5f);

        t = 0f;
        while (t < settleHalf)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / settleHalf);
            transform.localScale = Vector3.Lerp(Vector3.one, settleScale, k);
            yield return null;
        }

        t = 0f;
        while (t < settleHalf)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / settleHalf);
            transform.localScale = Vector3.Lerp(settleScale, Vector3.one, k);
            yield return null;
        }

        transform.localScale = Vector3.one;
        SnapToGrid(tileSize);

    }

    public IEnumerator PopOut(float duration)
    {
        transform.localScale = Vector3.one;

        float popDuration = Mathf.Max(0.0001f, duration);
        float impactDuration = Mathf.Min(0.055f, popDuration * 0.40f);
        float t = 0f;

        Vector2 originalPivot = rt.pivot;
        var hasCanvasGroup = TryGetComponent<CanvasGroup>(out var canvasGroup);

        // Küçülme solda kayıyormuş gibi görünmesin diye merkezi pivot'tan animasyon yap.
        if (rt.pivot != CenterPivot)
            SetPivotWithoutVisualJump(CenterPivot);

        if (!hasCanvasGroup)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;

        // 1) "Kırılma" hissi için kısa bir impact punch.
        while (t < impactDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, impactDuration));
            float squish = 1f + Mathf.Lerp(0f, 0.12f, k);
            float stretch = 1f - Mathf.Lerp(0f, 0.08f, k);
            transform.localScale = new Vector3(squish, stretch, 1f);
            yield return null;
        }

        // 2) Punch sonrası hızlı parçalanma/kaybolma.
        t = 0f;
        Vector3 start = transform.localScale;
        Vector3 end = Vector3.zero;

        float shatterDuration = Mathf.Max(0.0001f, popDuration - impactDuration);
        while (t < shatterDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / shatterDuration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);

            transform.localScale = Vector3.Lerp(start, end, eased);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 16f, eased));
            canvasGroup.alpha = 1f - eased;
            yield return null;
        }

        transform.localScale = end;
        transform.localRotation = Quaternion.identity;
        canvasGroup.alpha = 0f;

        // Pool/re-enable durumları için eski pivot'u koru.
        if (rt != null && rt.pivot != originalPivot)
            SetPivotWithoutVisualJump(originalPivot);
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

    public IEnumerator PlayLightningStrikeAndShrink(float duration, Color lightningColor)
    {
        if (iconImage == null)
        {
            yield return PopOut(duration);
            yield break;
        }

        Color baseColor = iconImage.color;
        float flashTime = Mathf.Min(0.05f, duration * 0.30f);
        float impactTime = Mathf.Min(0.04f, duration * 0.25f);
        float t = 0f;

        // 1) Ani beyaz/elektrik flash
        while (t < flashTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, flashTime));
            iconImage.color = Color.Lerp(baseColor, lightningColor, k);
            yield return null;
        }

        // 2) Vurulma anında çok kısa sert punch
        t = 0f;
        while (t < impactTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, impactTime));
            float s = Mathf.Lerp(1f, 1.14f, k);
            transform.localScale = new Vector3(s, 1f - (s - 1f) * 0.65f, 1f);
            yield return null;
        }

        iconImage.color = baseColor;

        // 3) Kırılıp küçülerek yok olma
        float shrinkDuration = Mathf.Max(0.04f, duration - flashTime - impactTime);
        t = 0f;
        Vector3 start = transform.localScale;
        Vector3 end = Vector3.zero;

        while (t < shrinkDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, shrinkDuration));
            float eased = k * k;
            transform.localScale = Vector3.Lerp(start, end, eased);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 18f, eased));

            var c = iconImage.color;
            c.a = Mathf.Lerp(baseColor.a, 0f, eased);
            iconImage.color = c;
            yield return null;
        }

        transform.localScale = end;
        transform.localRotation = Quaternion.identity;
        var finalColor = iconImage.color;
        finalColor.a = 0f;
        iconImage.color = finalColor;
    }

    public TileType GetTileType() => model.type;

    public Sprite GetIconSprite() => iconImage != null ? iconImage.sprite : null;

    public RectTransform RectTransform => rt != null ? rt : (RectTransform)transform;

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

    // -------------------
    // DRAG SWAP
    // -------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (board == null || board.IsBusy) return;

        // ResetVisualState ile aynı baseline: drag başlangıcında ölçek temizlenir.
        transform.localScale = Vector3.one;

        wasDragging = true;
        dragConsumedSwap = false;

        dragStartAnchored = rt.anchoredPosition;

        // pointer'ı parent local space'e çevir
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt, eventData.position, eventData.pressEventCamera, out dragStartLocalPointer
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (board == null || board.IsBusy) return;
        if (dragConsumedSwap) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt, eventData.position, eventData.pressEventCamera, out var curLocal
        );

        var delta = curLocal - dragStartLocalPointer;

        // Tile, parmağı biraz takip etsin (çok kaçmasın)
        float max = board.TileSize * 0.45f;
        delta.x = Mathf.Clamp(delta.x, -max, max);
        delta.y = Mathf.Clamp(delta.y, -max, max);

        rt.anchoredPosition = dragStartAnchored + delta;

        // Threshold geçildiyse swap tetikle
        float threshold = board.TileSize * 0.25f;
        if (Mathf.Abs(delta.x) < threshold && Mathf.Abs(delta.y) < threshold) return;

        int dirX = 0, dirY = 0;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            dirX = delta.x > 0 ? 1 : -1;
        else
            dirY = delta.y > 0 ? -1 : 1; // UI'da yukarı negatif Y, dikkat

        dragConsumedSwap = true;

        // tile'ı hemen grid'e geri koy (swap animasyonunu Board yapacak)
        SnapToGrid(board.TileSize);

        board.RequestSwapFromDrag(this, dirX, dirY);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Swap tetiklenmediyse yerine dön
        if (!dragConsumedSwap && board != null)
            SnapToGrid(board.TileSize);

        // küçük gecikmeyle click’i engelle (drag sonrası tık sayılmasın)
        StartCoroutine(ResetWasDragging());
    }

    IEnumerator ResetWasDragging()
    {
        yield return null;
        wasDragging = false;
    }

    // Tap-tap swap hâlâ duruyor; drag varken click'i yeme
    public void OnPointerClick(PointerEventData eventData)
    {
        if (wasDragging) return;
        board?.OnTileClicked(this);
    }

    public TileSpecial GetSpecial() => model.special;

    public void SetSpecial(TileSpecial sp)
    {
        model.SetSpecial(sp);
        RefreshIcon();
    }
    public IEnumerator PlayPulseImpact(float delay, float totalTime)
    {
        yield return new WaitForSeconds(delay);
        if (this == null) yield break;

        // Küçük pop + fade
        var rt = (RectTransform)transform;
        var g = GetComponent<CanvasGroup>();
        if (g == null) g = gameObject.AddComponent<CanvasGroup>();

        Vector3 start = rt.localScale;
        Vector3 up = start * 1.08f;
        Vector3 down = start * 0.90f;

        float t = 0f;
        float half = totalTime * 0.45f;

        // Pop up
        while (t < half)
        {
            if (this == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            rt.localScale = Vector3.Lerp(start, up, k);
            yield return null;
        }

        // Pop down + fade out
        t = 0f;
        while (t < totalTime - half)
        {
            if (this == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / (totalTime - half));
            rt.localScale = Vector3.Lerp(up, down, k);
            g.alpha = Mathf.Lerp(1f, 0f, k);
            yield return null;
        }

        // (Logic silme ayrı yerde; bu sadece görüntü)
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
            irt.anchorMin = CenterPivot;
            irt.anchorMax = CenterPivot;
            irt.pivot = CenterPivot;
            irt.anchoredPosition = Vector2.zero;

            float s = tileSize * runtimeIconScale;
            irt.sizeDelta = new Vector2(s, s);
        }
    }


    public void SetOverrideBaseType(TileType type) => model.SetOverrideBaseType(type);

    public bool GetOverrideBaseType(out TileType type) => model.TryGetOverrideBaseType(out type);


}
