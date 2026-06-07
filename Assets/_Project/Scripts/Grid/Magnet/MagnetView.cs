using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Visual for one magnet pair obstacle.
/// Two magnet sprites sit at the endpoints; overlapping glow circles fill the
/// connecting path, naturally rounding every corner without needing corner sprites.
///
/// Setup: call Init() after Instantiate. The view manages its own children and
/// is destroyed via PlayDestroyAnimation() when the pair meets.
public class MagnetView : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("Mıknatıs uç sprite'ı. MagnetB yatay olarak çevrilir.")]
    [SerializeField] private Sprite magnetSprite;
    [Tooltip("Path hücreleri için glow dairesi sprite'ı (yumuşak radial gradient önerilir).")]
    [SerializeField] private Sprite glowCircleSprite;

    [Header("Glow")]
    [SerializeField] private Color glowColor = new Color(0.25f, 0.65f, 1f, 0.82f);
    [Tooltip("Glow dairesinin hücre boyutuna oranı. >1 = komşu dairelerle overlap.")]
    [SerializeField, Range(0.8f, 2f)] private float glowCircleScale = 1.25f;

    [Header("Pulse")]
    [SerializeField, Min(0.2f)] private float pulseDuration = 1.4f;
    [SerializeField, Range(0f, 1f)] private float pulseMinAlpha = 0.5f;
    [SerializeField, Range(0f, 1f)] private float pulseMaxAlpha = 0.88f;

    [Header("Move Animation")]
    [SerializeField, Min(0.05f)] private float moveDuration = 0.2f;

    [Header("Destroy Animation")]
    [SerializeField, Min(0.05f)] private float destroyDuration = 0.35f;

    private int[] path;
    private int gridWidth;
    private float cellSize;

    private Image magnetAImage;
    private Image magnetBImage;
    private Image[] glowCircles;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Init(int[] pathCellIndices, int gridWidth, float cellSize, RectTransform parent)
    {
        path = pathCellIndices;
        this.gridWidth = gridWidth;
        this.cellSize = cellSize;

        var rt = GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = Vector2.zero;

        BuildChildren();
        RefreshGlowVisibility(0, path.Length - 1);
        StartCoroutine(PulseRoutine());
    }

    /// Called by MagnetObstacleService after a hit moves one of the endpoints.
    public void UpdatePositions(int newAIdx, int newBIdx, int prevAIdx, int prevBIdx)
    {
        RefreshGlowVisibility(newAIdx, newBIdx);

        bool aChanged = newAIdx != prevAIdx;
        Vector2 aFrom = CellCenter(path[prevAIdx]);
        Vector2 aTo   = CellCenter(path[newAIdx]);
        Vector2 bFrom = CellCenter(path[prevBIdx]);
        Vector2 bTo   = CellCenter(path[newBIdx]);

        if (aChanged)
            StartCoroutine(MoveImageRoutine(magnetAImage, aFrom, aTo));
        else
            StartCoroutine(MoveImageRoutine(magnetBImage, bFrom, bTo));
    }

    /// Fade-out then destroy.
    public void PlayDestroyAnimation()
    {
        StopAllCoroutines();
        StartCoroutine(DestroyRoutine());
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void BuildChildren()
    {
        float circleSize = cellSize * glowCircleScale;

        // Glow circles — one per path cell, drawn first (behind magnets).
        glowCircles = new Image[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            var go = new GameObject($"Glow_{i}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            var img = go.GetComponent<Image>();
            img.sprite = glowCircleSprite;
            img.color  = glowColor;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(circleSize, circleSize);
            rt.anchoredPosition = CellCenter(path[i]);

            glowCircles[i] = img;
        }

        // Magnet A (on top of glow).
        magnetAImage = CreateMagnetImage("MagnetA", flip: false);
        magnetAImage.rectTransform.anchoredPosition = CellCenter(path[0]);

        // Magnet B (flipped horizontally).
        magnetBImage = CreateMagnetImage("MagnetB", flip: true);
        magnetBImage.rectTransform.anchoredPosition = CellCenter(path[path.Length - 1]);
    }

    private Image CreateMagnetImage(string goName, bool flip)
    {
        var go = new GameObject(goName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        if (flip)
            go.transform.localScale = new Vector3(-1f, 1f, 1f);

        var img = go.GetComponent<Image>();
        img.sprite = magnetSprite;
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(cellSize, cellSize);

        return img;
    }

    private void RefreshGlowVisibility(int aIdx, int bIdx)
    {
        for (int i = 0; i < glowCircles.Length; i++)
            glowCircles[i].gameObject.SetActive(i >= aIdx && i <= bIdx);
    }

    private Vector2 CellCenter(int cellIndex)
    {
        int cx = cellIndex % gridWidth;
        int cy = cellIndex / gridWidth;
        return new Vector2(cx * cellSize + cellSize * 0.5f, -(cy * cellSize + cellSize * 0.5f));
    }

    private IEnumerator MoveImageRoutine(Image img, Vector2 from, Vector2 to)
    {
        if (img == null) yield break;
        var rt = img.rectTransform;
        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / moveDuration));
            rt.anchoredPosition = Vector2.Lerp(from, to, k);
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    private IEnumerator PulseRoutine()
    {
        float half = pulseDuration * 0.5f;
        while (true)
        {
            float t = 0f;
            while (t < pulseDuration)
            {
                t += Time.deltaTime;
                float k = t < half
                    ? Mathf.Clamp01(t / half)
                    : 1f - Mathf.Clamp01((t - half) / half);
                float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, k);

                foreach (var circle in glowCircles)
                {
                    if (circle == null || !circle.gameObject.activeSelf) continue;
                    var c = circle.color;
                    c.a = alpha;
                    circle.color = c;
                }
                yield return null;
            }
        }
    }

    private IEnumerator DestroyRoutine()
    {
        // Collect all images to fade.
        var canvasGroup = gameObject.AddComponent<CanvasGroup>();
        float t = 0f;
        while (t < destroyDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / destroyDuration);
            yield return null;
        }
        Destroy(gameObject);
    }
}
