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

    [Header("Layout")]
    [Tooltip("Body'nin base sprite içine kaç pixel overlap yapacağı (grid çizgisini gizler).")]
    [SerializeField, Min(0f)] private float baseBodyOverlap = 2f;

    [Tooltip("TubeCap'ın üst hücreyi tam doldurmasına ek olarak alt komşu hücreyle kaç pixel overlap yapacağı.")]
    [SerializeField, Min(0f)] private float capCellOverlap = 8f;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float shrinkDuration = 0.2f;
    [SerializeField, Min(0f)] private float shakeAmplitude = 6f;
    [SerializeField, Min(0f)] private float shakeDuration  = 0.25f;

    private RectTransform maskContainer;
    [Header("Cutting")]
    [SerializeField] private Sprite sawSprite;
    [SerializeField] private Vector2 sawCenterInCell = new Vector2(0.5f, 0.36f);
    [SerializeField, Min(0.01f)] private float sawSizeInCells = 0.60f;
    [SerializeField, Min(0.05f)] private float sawSpinDuration = 0.4f;
    [SerializeField] private float sawDegreesPerSecond = 1800f;
    [SerializeField, Range(1, 80)] private int chipCount = 26;
    [Tooltip("Talas parcasinin hucre boyutuna orani (uzunluk). Kalinlik bunun yarisi kadar.")]
    [SerializeField, Min(0.01f)] private float chipSizeInCells = 0.10f;
    private RectTransform sawRt;
    private Coroutine shrinkRoutine, shakeRoutine, sawRoutine;
    private Vector2 restingPosition;
    private bool destroying;
    private Image baseImage;
    private RectTransform capRt;
    private float capOffsetY = 0f;

    private TubeDirection direction;
    private int totalLength;
    private float cellSize;
    private bool isVertical;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Init(TubeDirection dir, int length, float cs)
    {
        direction   = dir;
        totalLength = length;
        cellSize    = cs;
        isVertical  = dir == TubeDirection.Up || dir == TubeDirection.Down;

        BuildChildren();
        SetMaskSize(totalLength);
        if (capRt != null) capRt.gameObject.SetActive(totalLength > 1);
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
        if (direction == TubeDirection.Down)
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((topLeftX + 0.5f) * cellSize,
                -(topLeftY + totalLength * 0.5f) * cellSize);
            rt.localEulerAngles = new Vector3(0f, 0f, 180f);
        }
        restingPosition = rt.anchoredPosition;
    }

    public void AnimateShrink(int remainingCells)
    {
        if (destroying) return;
        if (shrinkRoutine != null) StopCoroutine(shrinkRoutine);
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        if (sawRoutine != null) StopCoroutine(sawRoutine);
        shrinkRoutine = StartCoroutine(ShrinkCoroutine(remainingCells));
        shakeRoutine = StartCoroutine(ShakeCoroutine());
        sawRoutine = StartCoroutine(SpinSaw());
        StartCoroutine(EmitChips());
    }

    public void DestroyView()
    {
        if (destroying) return;
        AnimateShrink(0);
        destroying = true;
        StartCoroutine(FinishDestruction());
    }

    private IEnumerator FinishDestruction()
    {
        yield return new WaitForSeconds(Mathf.Max(shrinkDuration, sawSpinDuration));
        baseImage.enabled = false;
        if (sawRt != null) sawRt.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.65f);
        Destroy(gameObject);
    }

    private IEnumerator SpinSaw()
    {
        float elapsed = 0f;
        while (sawRt != null && elapsed < sawSpinDuration)
        {
            elapsed += Time.deltaTime;
            float speed = sawDegreesPerSecond * (1f - Mathf.Clamp01(elapsed / sawSpinDuration));
            sawRt.Rotate(0f, 0f, -speed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator EmitChips()
    {
        const float life = 0.6f;
        var pieces = new RectTransform[chipCount];
        var images = new Image[chipCount];
        var velocities = new Vector2[chipCount];
        var spins = new float[chipCount];
        Vector2 source = new Vector2((sawCenterInCell.x - 0.5f) * cellSize,
            (sawCenterInCell.y + sawSizeInCells * 0.4f) * cellSize);
        // Gravity stays screen-down even on rotated horizontal/down-facing tubes.
        Vector3 gravity = transform.InverseTransformVector(
            transform.parent.TransformVector(Vector3.down * cellSize * 5f));
        for (int i = 0; i < pieces.Length; i++)
        {
            var go = new GameObject("BlueCuttingChip", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            images[i] = go.GetComponent<Image>();
            images[i].raycastTarget = false;
            images[i].color = Color.Lerp(new Color(0.02f, 0.25f, 1f), new Color(0.1f, 0.95f, 1f), Random.value);
            pieces[i] = images[i].rectTransform;
            pieces[i].anchorMin = pieces[i].anchorMax = new Vector2(0.5f, 0f);
            pieces[i].anchoredPosition = source + Random.insideUnitCircle * cellSize * 0.07f;
            float chipLength = chipSizeInCells * Random.Range(0.7f, 1.3f);
            pieces[i].sizeDelta = new Vector2(chipLength, chipLength * Random.Range(0.4f, 0.65f)) * cellSize;
            velocities[i] = new Vector2(Random.Range(-1.9f, 1.9f), Random.Range(0.3f, 1.7f)) * cellSize;
            spins[i] = Random.Range(-700f, 700f);
        }
        float elapsed = 0f;
        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < pieces.Length; i++)
            {
                velocities[i] += (Vector2)gravity * Time.deltaTime;
                pieces[i].anchoredPosition += velocities[i] * Time.deltaTime;
                pieces[i].Rotate(0f, 0f, spins[i] * Time.deltaTime);
                var color = images[i].color;
                color.a = 1f - Mathf.InverseLerp(life * 0.4f, life, elapsed);
                images[i].color = color;
            }
            yield return null;
        }
        foreach (var piece in pieces) Destroy(piece.gameObject);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void BuildChildren()
    {
        // ── BaseImage (drawn first = behind everything else) ───────────────
        var baseGo = new GameObject("BaseImage",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        baseGo.transform.SetParent(transform, false);
        baseImage = baseGo.GetComponent<Image>();
        baseGo.layer = gameObject.layer;
        baseImage.sprite = baseSprite;
        baseImage.raycastTarget = false;
        var baseRt = baseImage.rectTransform;
        SetAnchorForBaseEnd(baseRt, stretchCross: true);
        SetMainAxisSize(baseRt, cellSize);

        // ── EnergyMaskContainer (stencil Mask) ────────────────────────────
        // Body begins after the base cell; the mask crops it as the cap approaches.
        var maskGo = new GameObject("EnergyMask",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        maskGo.layer = gameObject.layer;
        maskGo.transform.SetParent(transform, false);
        maskContainer = maskGo.GetComponent<RectTransform>();
        var maskImage = maskGo.GetComponent<Image>();
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;
        var mask = maskGo.GetComponent<Mask>();
        mask.showMaskGraphic = false;
        SetAnchorForBaseEnd(maskContainer, stretchCross: true);
        ApplyOpenEndOffset(maskContainer, cellSize - baseBodyOverlap);

        // Repeat whole cell images instead of stretching one texture along the tube.
        for (int i = 0; i < Mathf.Max(0, totalLength - 2); i++)
        {
            var bodyGo = new GameObject($"BodyCell_{i}", typeof(RectTransform), typeof(Image));
            bodyGo.layer = gameObject.layer;
            bodyGo.transform.SetParent(maskContainer, false);
            var image = bodyGo.GetComponent<Image>();
            image.sprite = bodySprite;
            image.raycastTarget = false;
            SetBodyAnchor(image.rectTransform, cellSize);
            image.rectTransform.anchoredPosition = new Vector2(0f, i * cellSize);
        }

        if (sawSprite != null)
        {
            var sawGo = new GameObject("Saw", typeof(RectTransform), typeof(Image));
            sawGo.layer = gameObject.layer;
            sawGo.transform.SetParent(baseImage.transform, false);
            var image = sawGo.GetComponent<Image>();
            image.sprite = sawSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            sawRt = image.rectTransform;
            sawRt.anchorMin = sawRt.anchorMax = sawCenterInCell;
            sawRt.pivot = new Vector2(0.5f, 0.5f);
            sawRt.sizeDelta = Vector2.one * cellSize * sawSizeInCells;
        }

        // ── OpenEndCap (root'a bağlı, mask dışında — kendi animasyonuyla kayar) ──
        if (openEndCapSprite != null)
        {
            var capGo = new GameObject("OpenEndCap",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            capGo.layer = gameObject.layer;
            capGo.transform.SetParent(transform, false);
            var capImg = capGo.GetComponent<Image>();
            capImg.sprite = openEndCapSprite;
            capImg.raycastTarget = false;
            capRt = capImg.rectTransform;
            SetAnchorForOpenEnd(capRt, stretchCross: true);
            SetMainAxisSize(capRt, cellSize + capCellOverlap);
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

    // Body: sabit yükseklik, alta dayalı. Mask'ın yüksekliği değişince üstten kesilir (crop).
    private void SetBodyAnchor(RectTransform rt, float fullHeight)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, fullHeight);
    }

    private void ApplyOpenEndOffset(RectTransform rt, float offset)
    {
        // Layout is always Up: offset from the base (bottom) toward the open end (top).
        rt.anchoredPosition = new Vector2(0f, offset);
    }

    private void SetMaskSize(int cells)
    {
        if (maskContainer == null) return;
        float size = cells <= 2 ? 0f : (cells - 2) * cellSize + baseBodyOverlap;
        SetMainAxisSize(maskContainer, size);
    }

    private IEnumerator ShakeCoroutine()
    {
        var rt      = GetComponent<RectTransform>();
        var origin  = restingPosition;
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
        float targetMaskSize = remainingCells <= 2 ? 0f : (remainingCells - 2) * cellSize + baseBodyOverlap;
        float startMaskSize  = maskContainer.sizeDelta.y;

        float targetOffset = (totalLength - remainingCells) * cellSize;
        float startOffset  = capOffsetY;

        float elapsed = 0f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            SetMainAxisSize(maskContainer, Mathf.Lerp(startMaskSize, targetMaskSize, t));
            if (capRt != null)
            {
                capOffsetY = Mathf.Lerp(startOffset, targetOffset, t);
                capRt.anchoredPosition = new Vector2(0f, -capOffsetY);
            }
            yield return null;
        }

        SetMainAxisSize(maskContainer, targetMaskSize);
        if (capRt != null)
        {
            capOffsetY = targetOffset;
            capRt.anchoredPosition = new Vector2(0f, -capOffsetY);
            if (remainingCells <= 1)
                capRt.gameObject.SetActive(false);
        }
    }
}
