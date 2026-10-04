using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Duvar çatlak2 aşamasında fitil ucundaki alev: geniş parlama + yukarı uzanıp yalpalayan alev dili +
/// sarı çekirdek + beyaz sıcak nokta; sık sık yukarı fışkıran kıvılcımlar. Bağımsız (sahne/prefab kurulumu yok); fitille birlikte yok olur.
/// </summary>
public sealed class WallFuseFlame : MonoBehaviour
{
    private const float SparkInterval = 0.05f;
    private const int MaxSparks = 12;

    // Katmanlar (dıştan içe): geniş parlama → turuncu alev dili → sarı çekirdek → beyaz sıcak nokta.
    private RectTransform glow, tongue, core, hot;
    private float ts;
    private float phase;
    private float sparkTimer;

    private readonly RectTransform[] sparks = new RectTransform[MaxSparks];
    private readonly Image[] sparkImages = new Image[MaxSparks];
    private readonly Vector2[] sparkVel = new Vector2[MaxSparks];
    private readonly float[] sparkAge = new float[MaxSparks];
    private readonly float[] sparkLife = new float[MaxSparks];

    public static WallFuseFlame Create(RectTransform parent, Vector2 localPos, float tileSize, float phaseOffset)
    {
        var go = new GameObject("FuseFlame", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localPos;
        rt.sizeDelta = Vector2.zero;

        var flame = go.AddComponent<WallFuseFlame>();
        flame.ts = tileSize;
        flame.phase = phaseOffset;
        flame.glow = flame.MakeDot(new Color(1f, 0.5f, 0.1f, 0.6f), tileSize * 0.38f).rectTransform;
        flame.tongue = flame.MakeDot(new Color(1f, 0.45f, 0.08f, 0.95f), tileSize * 0.2f).rectTransform;
        flame.tongue.sizeDelta = new Vector2(tileSize * 0.17f, tileSize * 0.34f);
        flame.tongue.pivot = new Vector2(0.5f, 0.2f);          // alt tarafı fitilde, yukarı uzanır
        flame.core = flame.MakeDot(new Color(1f, 0.85f, 0.3f, 1f), tileSize * 0.1f).rectTransform;
        flame.core.sizeDelta = new Vector2(tileSize * 0.1f, tileSize * 0.19f);
        flame.core.pivot = new Vector2(0.5f, 0.25f);
        flame.hot = flame.MakeDot(new Color(1f, 1f, 0.9f, 1f), tileSize * 0.065f).rectTransform;
        return flame;
    }

    private Image MakeDot(Color color, float size)
    {
        var go = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = WallSprites.SoftCircle;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime * 9f + phase * 10f;

        // Titreme: iki frekanslı gürültü → düzensiz ama yumuşak; alev dili sağa sola yalpalar.
        float flicker = 0.85f + 0.15f * Mathf.Sin(t) + 0.1f * Mathf.Sin(t * 2.3f + 1.7f);
        float sway = 9f * Mathf.Sin(t * 0.7f + phase) + 5f * Mathf.Sin(t * 1.9f);
        if (glow != null)
        {
            float g = 0.9f + 0.15f * Mathf.Sin(t * 0.8f + 0.5f);
            glow.localScale = new Vector3(g, g, 1f);
            glow.anchoredPosition = new Vector2(0f, ts * 0.06f);
        }
        if (tongue != null)
        {
            tongue.localScale = new Vector3(0.9f + 0.12f * Mathf.Sin(t * 1.3f), flicker * 1.1f, 1f);
            tongue.localEulerAngles = new Vector3(0f, 0f, sway);
        }
        if (core != null)
        {
            float c = 0.9f + 0.2f * Mathf.Sin(t * 1.6f + 0.4f);
            core.localScale = new Vector3(c, c * flicker, 1f);
            core.localEulerAngles = new Vector3(0f, 0f, sway * 0.7f);
        }
        if (hot != null)
        {
            float h = 0.85f + 0.25f * Mathf.Sin(t * 2.7f + 1.1f);
            hot.localScale = new Vector3(h, h, 1f);
        }

        sparkTimer -= dt;
        if (sparkTimer <= 0f)
        {
            sparkTimer = SparkInterval * Random.Range(0.6f, 1.4f);
            EmitSpark();
        }

        for (int i = 0; i < MaxSparks; i++)
        {
            if (sparks[i] == null || !sparks[i].gameObject.activeSelf) continue;
            sparkAge[i] += dt;
            float k = sparkAge[i] / sparkLife[i];
            if (k >= 1f) { sparks[i].gameObject.SetActive(false); continue; }
            sparkVel[i] += new Vector2(0f, ts * 0.6f) * dt;   // sıcak hava: hafif yukarı ivme
            sparks[i].anchoredPosition += sparkVel[i] * dt;
            var col = sparkImages[i].color;
            sparkImages[i].color = new Color(col.r, col.g, col.b, 1f - k);
            float s = Mathf.Lerp(1f, 0.3f, k);
            sparks[i].localScale = new Vector3(s, s, 1f);
        }
    }

    private void EmitSpark()
    {
        for (int i = 0; i < MaxSparks; i++)
        {
            if (sparks[i] != null && sparks[i].gameObject.activeSelf) continue;
            if (sparks[i] == null)
            {
                sparkImages[i] = MakeDot(new Color(1f, 0.75f, 0.25f, 1f), ts * 0.06f);
                sparks[i] = sparkImages[i].rectTransform;
            }
            sparks[i].gameObject.SetActive(true);
            sparks[i].anchoredPosition = new Vector2(0f, ts * 0.08f);
            sparkAge[i] = 0f;
            sparkLife[i] = Random.Range(0.4f, 0.75f);
            sparkVel[i] = new Vector2(Random.Range(-0.55f, 0.55f), Random.Range(0.6f, 1.3f)) * ts;
            sparkImages[i].color = Color.Lerp(new Color(1f, 0.85f, 0.35f, 1f), new Color(1f, 0.45f, 0.1f, 1f), Random.value);
            return;
        }
    }
}
