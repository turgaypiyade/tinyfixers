using UnityEngine;
using UnityEngine.UI;

/// One UI cell that samples its own region out of a single shared mud texture.
/// No per-cell texture clamping → adjacent cells look like one continuous mud surface.
public class MudCellView : MonoBehaviour
{
    private RawImage rawImage;
    private RectTransform rt;

    private int gridX;
    private int gridY;
    private int gridWidth;
    private int gridHeight;

    private Color darkTint = new Color(0.35f, 0.25f, 0.18f, 1f);
    private Color lightTint = new Color(0.65f, 0.50f, 0.35f, 1f);
    private float minAlphaAtLastStage = 0.7f;

    public int GridX => gridX;
    public int GridY => gridY;

    public void Init(
        Texture sharedMudTexture,
        int x,
        int y,
        int gridW,
        int gridH,
        Color dark,
        Color light,
        float minAlpha)
    {
        rt = GetComponent<RectTransform>();
        if (rt == null)
            rt = gameObject.AddComponent<RectTransform>();

        rawImage = GetComponent<RawImage>();
        if (rawImage == null)
            rawImage = gameObject.AddComponent<RawImage>();

        rawImage.raycastTarget = false;
        rawImage.texture = sharedMudTexture;

        gridX = x;
        gridY = y;
        gridWidth = Mathf.Max(1, gridW);
        gridHeight = Mathf.Max(1, gridH);

        darkTint = dark;
        lightTint = light;
        minAlphaAtLastStage = Mathf.Clamp01(minAlpha);

        // Per-cell UV: each cell shows exactly its slice of the global texture,
        // so seams disappear when cells sit edge-to-edge.
        // Note: UGUI uv origin = bottom-left, but our grid uses top-down y.
        // Map so cell (0,0) reads the top-left chunk of the texture.
        float uvW = 1f / gridWidth;
        float uvH = 1f / gridHeight;
        float u = gridX * uvW;
        float v = 1f - (gridY + 1) * uvH;
        rawImage.uvRect = new Rect(u, v, uvW, uvH);
    }

    public void PlaceInCell(int tileSize)
    {
        if (rt == null) return;

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(gridX * tileSize, -gridY * tileSize);
        rt.sizeDelta = new Vector2(tileSize, tileSize);
    }

    /// remaining=maxHits → full dirt, remaining=0 → cleared (invisible).
    /// Alfa, son hit'e kadar `minAlphaAtLastStage`'in altına düşmez —
    /// böylece arka plan (cellBg / grid çizgileri) sızmaz.
    public void SetDamageLevel(int remaining, int maxHits)
    {
        if (rawImage == null) return;

        int max = Mathf.Max(1, maxHits);
        int rem = Mathf.Clamp(remaining, 0, max);

        if (rem <= 0)
        {
            rawImage.color = new Color(lightTint.r, lightTint.g, lightTint.b, 0f);
            return;
        }

        float t = (float)rem / max; // 1.0 → (1/max)
        Color tint = Color.Lerp(lightTint, darkTint, t);
        // Alfa: full hit = 1.0, son aktif hit = minAlphaAtLastStage.
        tint.a = Mathf.Lerp(minAlphaAtLastStage, 1f, t);
        rawImage.color = tint;
    }

    public void Clear()
    {
        if (rawImage != null)
        {
            var c = rawImage.color;
            c.a = 0f;
            rawImage.color = c;
        }
        gameObject.SetActive(false);
    }

    /// Per-stage texture swap mode. Alfa hep 1.0 — arkaplan sızmaz.
    /// stageTexture: bu stage için RawImage'a basılacak texture.
    /// visible: false ise tamamen şeffaf (cleared).
    public void ApplyStage(Texture stageTexture, bool visible)
    {
        if (rawImage == null) return;

        if (!visible)
        {
            rawImage.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        if (stageTexture != null)
            rawImage.texture = stageTexture;

        rawImage.color = Color.white; // tint yok, opak
    }
}
