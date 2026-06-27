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
    [Tooltip("Zincir baklası sprite'ı: dikey oval RING (ortası boş). Yön'e göre döndürülür.")]
    [SerializeField] private Sprite glowCircleSprite;

    [Header("Chain Link")]
    [Tooltip("Bakla rengi (tint). Sprite zaten renkliyse beyaz bırak.")]
    [SerializeField] private Color glowColor = Color.white;
    [Tooltip("Baklanın KISA ekseni (kalınlık) / hücre.")]
    [SerializeField, Range(0.3f, 1.2f)] private float chainLinkWidthRatio = 0.72f;
    [Tooltip("Baklanın UZUN ekseni (boy) / hücre. >1 → bakla hücreden BÜYÜK olur, komşularla içiçe geçer.")]
    [SerializeField, Range(1f, 2.2f)] private float chainLinkLengthRatio = 1.55f;
    [Tooltip("Kose baglanti baklasinin capi / duz bakla kalinligi.")]
    [SerializeField, Range(0.6f, 1.6f)] private float chainCornerScale = 1.08f;
    [Tooltip("Duz baklalari dugumlerin otesine tasiran ek bindirme / hucre.")]
    [SerializeField, Range(0f, 0.45f)] private float chainCornerOffset = 0.14f;

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
    private Image[] glowCircles;        // zincir baklaları (isim korundu: Pulse + visibility kullanır)
    private float[] linkPathPos;        // her baklanın path-index konumu (görünürlük için)

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
        {
            OrientMagnet(magnetAImage, newAIdx, newAIdx + 1);   // köşeyi geçince yön güncellenir
            StartCoroutine(MoveImageRoutine(magnetAImage, aFrom, aTo));
        }
        else
        {
            OrientMagnet(magnetBImage, newBIdx, newBIdx - 1);
            StartCoroutine(MoveImageRoutine(magnetBImage, bFrom, bTo));
        }
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
        BuildChainLinks();

        // Magnet A — yönelim path yönüne göre döndürülür (sabit flip yerine rotation).
        magnetAImage = CreateMagnetImage("MagnetA", flip: false);
        magnetAImage.rectTransform.anchoredPosition = CellCenter(path[0]);
        OrientMagnet(magnetAImage, 0, 1);

        // Magnet B.
        magnetBImage = CreateMagnetImage("MagnetB", flip: false);
        magnetBImage.rectTransform.anchoredPosition = CellCenter(path[path.Length - 1]);
        OrientMagnet(magnetBImage, path.Length - 1, path.Length - 2);
    }

    // Chain links are drawn on the edges between path cells. A separate round
    // junction is added on turns so L-shaped corners do not depend on a diagonal
    // link to hide the join.
    private void BuildChainLinks()
    {
        int n = path.Length;
        float linkW = cellSize * chainLinkWidthRatio;
        float linkL = cellSize * (chainLinkLengthRatio + chainCornerOffset);

        var imgs = new System.Collections.Generic.List<Image>();
        var poss = new System.Collections.Generic.List<float>();

        for (int i = 0; i < n - 1; i++)
        {
            Vector2 from = CellCenter(path[i]);
            Vector2 to = CellCenter(path[i + 1]);
            Vector2 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f) continue;

            float distance = dir.magnitude;
            dir /= distance;

            float angle = AngleForDirection(dir);
            float segmentLength = Mathf.Max(linkL, distance + cellSize * chainCornerOffset);
            imgs.Add(CreateLink((from + to) * 0.5f, angle, linkW, segmentLength));
            poss.Add(i + 0.5f);
        }

        float cornerSize = linkW * chainCornerScale;
        for (int i = 1; i < n - 1; i++)
        {
            Vector2 inD = ScreenDir(path[i - 1], path[i]);
            Vector2 outD = ScreenDir(path[i], path[i + 1]);
            if (inD.sqrMagnitude < 0.0001f || outD.sqrMagnitude < 0.0001f) continue;
            if (Vector2.Dot(inD, outD) > 0.99f) continue;

            imgs.Add(CreateLink(CellCenter(path[i]), 0f, cornerSize, cornerSize));
            poss.Add(i);
        }

        glowCircles = imgs.ToArray();
        linkPathPos = poss.ToArray();
    }

    private Image CreateLink(Vector2 anchoredPos, float angleZ, float w, float h)
    {
        var go = new GameObject("ChainLink",
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
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = anchoredPos;
        rt.localRotation = Quaternion.Euler(0f, 0f, angleZ);
        return img;
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
        // Bakla SADECE güncel uçlar (aIdx,bIdx) ARASINDA görünür; uçlarda magnet sprite var.
        // Küçüldükçe (aIdx↑ / bIdx↓) dışarıda kalan baklalar gizlenir. linkPathPos = path-index konumu.
        for (int i = 0; i < glowCircles.Length; i++)
        {
            float p = linkPathPos[i];
            glowCircles[i].gameObject.SetActive(p > aIdx && p < bIdx);
        }
    }

    // Uç magnet'i, bağlandığı komşu hücreye (içeri) doğru yönlendirir: U'nun ağzı path yönüne bakar.
    // Base sprite ağzı YUKARI bakar (∪). endpointIdx/neighborIdx = path[] içindeki indexler.
    private void OrientMagnet(Image img, int endpointIdx, int neighborIdx)
    {
        if (img == null) return;
        if (endpointIdx < 0 || endpointIdx >= path.Length) return;
        if (neighborIdx < 0 || neighborIdx >= path.Length) return;

        int eCell = path[endpointIdx];
        int nCell = path[neighborIdx];
        int dx = (nCell % gridWidth) - (eCell % gridWidth);
        int dy = (nCell / gridWidth) - (eCell / gridWidth);   // grid y aşağı artar

        // UP(0,1)'i hedef ekran yönüne (dx, -dy) çeviren Z dönüşü: Atan2(-dx, -dy).
        // down → 180° (∩), sol → 90°, sağ → -90°, up → 0° (∪).
        float angle = Mathf.Atan2(-dx, -dy) * Mathf.Rad2Deg;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // İki hücre arası birim EKRAN yönü (grid y aşağı artar → ekran y = -dy).
    private Vector2 ScreenDir(int fromCell, int toCell)
    {
        int dx = (toCell % gridWidth) - (fromCell % gridWidth);
        int dy = (toCell / gridWidth) - (fromCell / gridWidth);
        var v = new Vector2(dx, -dy);
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.zero;
    }

    private float AngleForDirection(Vector2 dir)
    {
        return Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
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
