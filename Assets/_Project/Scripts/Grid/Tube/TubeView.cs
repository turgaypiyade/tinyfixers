using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// UI visual for one tube obstacle.
/// Consists of a base (socket) sprite that never changes, and an energy body
/// clipped by a UI Mask that shrinks from the open end on each hit.
///
/// Horizontal tubes (Left/Right) use the same Up-direction child layout but
/// have their root RectTransform rotated ±90°, so the mask and sprites work
/// correctly without fighting the clipping system.
///
/// Setup: call Init() after Instantiate, then PlaceOnGrid() to position.
public class TubeView : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite bodySprite;
    [SerializeField] private Sprite baseSprite;
    [Tooltip("Optional: open-end cap sprite pinned to the visible tip of the tube. " +
             "If assigned, it stays at the shrinking edge so the tip shape is never clipped.")]
    [SerializeField] private Sprite openEndCapSprite;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float shrinkDuration = 0.2f;
    [SerializeField, Min(0f)] private float shakeAmplitude = 6f;
    [SerializeField, Min(0f)] private float shakeDuration  = 0.25f;

    private RectTransform maskContainer;
    private Image energyBody;
    private Image baseImage;
    private RectTransform capRt;

    private TubeDirection direction;
    private int totalLength;
    private float cellSize;
    private bool isVertical;
    private bool isBaseAtLowEnd; // low = bottom (Up) or left (Right)

    // ── Public API ────────────────────────────────────────────────────────────

    public void Init(TubeDirection dir, int length, float cs)
    {
        direction   = dir;
        totalLength = length;
        cellSize    = cs;
        isVertical  = dir == TubeDirection.Up || dir == TubeDirection.Down;
        isBaseAtLowEnd = dir == TubeDirection.Up || dir == TubeDirection.Left;

        BuildChildren();
        SetMaskSize(totalLength);
    }

    /// Positions this view's root RectTransform within a canvas parent.
    /// topLeftX/Y are the grid coordinates of the top-left cell of the tube's bounding box.
    public void PlaceOnGrid(RectTransform parent, int topLeftX, int topLeftY)
    {
        var rt = GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);

        if (isVertical)
        {
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(topLeftX * cellSize, -topLeftY * cellSize);
            rt.sizeDelta = new Vector2(cellSize, totalLength * cellSize);
        }
        else
        {
            // Horizontal: root is a vertical rect rotated ±90° around its centre.
            // Children are laid out as Up so the mask clips correctly.
            float cx = (topLeftX + totalLength * 0.5f) * cellSize;
            float cy = -(topLeftY + 0.5f) * cellSize;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, cy);
            rt.sizeDelta = new Vector2(cellSize, totalLength * cellSize);
            rt.localEulerAngles = new Vector3(0f, 0f, direction == TubeDirection.Right ? -90f : 90f);
        }
    }

    public void AnimateShrink(int remainingCells)
    {
        StopAllCoroutines();
        StartCoroutine(ShrinkCoroutine(remainingCells));
        StartCoroutine(ShakeCoroutine());
    }

    public void DestroyView() => Destroy(gameObject);

    // ── Private ───────────────────────────────────────────────────────────────

    private void BuildChildren()
    {
        // ── BaseImage (drawn first = behind everything else) ───────────────
        var baseGo = new GameObject("BaseImage",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        baseGo.transform.SetParent(transform, false);
        baseImage = baseGo.GetComponent<Image>();
        baseImage.sprite = baseSprite;
        baseImage.raycastTarget = false;
        var baseRt = baseImage.rectTransform;
        SetAnchorForBaseEnd(baseRt, stretchCross: true);
        SetMainAxisSize(baseRt, cellSize);

        // ── EnergyMaskContainer (stencil Mask) ────────────────────────────
        // Starts 65% into the first cell so the body overlaps the top 35% of
        // the base sprite and renders on top of it.
        var maskGo = new GameObject("EnergyMask",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        maskGo.transform.SetParent(transform, false);
        maskContainer = maskGo.GetComponent<RectTransform>();
        var maskImage = maskGo.GetComponent<Image>();
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;
        var mask = maskGo.GetComponent<Mask>();
        mask.showMaskGraphic = false;
        SetAnchorForBaseEnd(maskContainer, stretchCross: true);
        ApplyOpenEndOffset(maskContainer, cellSize * 0.65f);

        // ── EnergyBody (Image inside mask, always full length) ─────────────
        var bodyGo = new GameObject("EnergyBody",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bodyGo.transform.SetParent(maskContainer, false);
        energyBody = bodyGo.GetComponent<Image>();
        energyBody.sprite = bodySprite;
        energyBody.type   = Image.Type.Sliced;
        energyBody.raycastTarget = false;
        var bodyRt = energyBody.rectTransform;
        float capInset = (openEndCapSprite != null) ? cellSize * 0.4f : 0f;
        SetBodyAnchor(bodyRt, capInset);

        // ── OpenEndCap (inside mask, pinned to the open end) ──────────────
        if (openEndCapSprite != null)
        {
            var capGo = new GameObject("OpenEndCap",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            capGo.transform.SetParent(maskContainer, false);
            var capImg = capGo.GetComponent<Image>();
            capImg.sprite = openEndCapSprite;
            capImg.raycastTarget = false;
            capRt = capImg.rectTransform;
            SetAnchorForOpenEnd(capRt, stretchCross: true);
            SetMainAxisSize(capRt, cellSize * 0.4f);
        }
    }

    // All layout helpers use Up-direction anchors.
    // Horizontal tubes rotate the root instead of individual sprites.

    private void SetAnchorForBaseEnd(RectTransform rt, bool stretchCross)
    {
        // Always treat layout as Up (base = bottom). Horizontal tubes rely on root rotation.
        rt.anchorMin = new Vector2(stretchCross ? 0f : 0.5f, 0f);
        rt.anchorMax = new Vector2(stretchCross ? 1f : 0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private void SetAnchorForOpenEnd(RectTransform rt, bool stretchCross)
    {
        // Always treat layout as Up (open = top). Horizontal tubes rely on root rotation.
        rt.anchorMin = new Vector2(stretchCross ? 0f : 0.5f, 1f);
        rt.anchorMax = new Vector2(stretchCross ? 1f : 0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    // Sets the main-axis sizeDelta. Layout is always vertical (Y is main axis).
    private void SetMainAxisSize(RectTransform rt, float size)
    {
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, size);
    }

    // Body stretches the full mask but leaves an inset gap at the open end for the cap.
    private void SetBodyAnchor(RectTransform rt, float openEndInset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);

        // Layout is always Up: inset from the top (open end).
        float half = openEndInset * 0.5f;
        rt.anchoredPosition = new Vector2(0f, -half);
        rt.sizeDelta = new Vector2(0f, -openEndInset);
    }

    private void ApplyOpenEndOffset(RectTransform rt, float offset)
    {
        // Layout is always Up: offset from the base (bottom) toward the open end (top).
        rt.anchoredPosition = new Vector2(0f, offset);
    }

    private void SetMaskSize(int cells)
    {
        if (maskContainer == null) return;
        float size = Mathf.Max(0f, cells * cellSize - cellSize * 0.65f);
        SetMainAxisSize(maskContainer, size);
    }

    private IEnumerator ShakeCoroutine()
    {
        var rt      = GetComponent<RectTransform>();
        var origin  = rt.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float t         = elapsed / shakeDuration;
            float amplitude = shakeAmplitude * (1f - t);
            float offset    = Mathf.Sin(t * Mathf.PI * 8f) * amplitude;

            // Vertical tubes shake on X; horizontal tubes shake on Y (perpendicular to tube).
            rt.anchoredPosition = isVertical
                ? new Vector2(origin.x + offset, origin.y)
                : new Vector2(origin.x, origin.y + offset);

            yield return null;
        }

        rt.anchoredPosition = origin;
    }

    private IEnumerator ShrinkCoroutine(int remainingCells)
    {
        float targetSize = Mathf.Max(0f, remainingCells * cellSize - cellSize * 0.65f);
        float startSize  = maskContainer.sizeDelta.y; // always Y: layout is always vertical

        float elapsed = 0f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t       = Mathf.Clamp01(elapsed / shrinkDuration);
            float current = Mathf.Lerp(startSize, targetSize, t);
            SetMainAxisSize(maskContainer, current);
            yield return null;
        }

        SetMainAxisSize(maskContainer, targetSize);
        if (targetSize <= 0f && capRt != null)
            capRt.gameObject.SetActive(false);
    }
}
