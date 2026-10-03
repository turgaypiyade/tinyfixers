using UnityEngine;

/// <summary>
/// Art gelene kadar kullanılan prosedürel su damlası sprite'ları (bir kez üretilir, cache'lenir).
///   Droplet — board'da uçan dolu mavi damla.
///   Glass   — telefon camındaki şeffaf damla: açık iç, koyu kenar halkası, sol-üst parlama.
/// </summary>
public static class WaterDropletSprites
{
    private const int Size = 128;

    private static Sprite droplet;
    private static Sprite glass;

    public static Sprite Droplet => droplet != null ? droplet : (droplet = Build(false));
    public static Sprite Glass => glass != null ? glass : (glass = Build(true));

    private static Sprite Build(bool glassStyle)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = glassStyle ? "WaterGlassDrop" : "WaterDroplet"
        };

        var pixels = new Color32[Size * Size];
        float r = Size * 0.5f - 1.5f;
        var c = new Vector2(Size * 0.5f, Size * 0.5f);
        var highlight = c + new Vector2(-r * 0.36f, r * 0.36f);

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Vector2.Distance(p, c) / r;                 // 0 merkez, 1 kenar
            float edge = Mathf.Clamp01((1f - d) * r / 1.5f);     // ~1.5px antialias
            if (edge <= 0f) { pixels[y * Size + x] = new Color32(0, 0, 0, 0); continue; }

            float h = Mathf.Clamp01(1f - Vector2.Distance(p, highlight) / (r * 0.32f));
            h *= h;

            Color col;
            if (glassStyle)
            {
                // Cam damlası: içi neredeyse şeffaf, kenara doğru koyulaşan halka (kırılma hissi).
                float rim = Mathf.Pow(d, 4f);
                col = Color.Lerp(new Color(0.8f, 0.92f, 1f, 0.16f), new Color(0.22f, 0.42f, 0.6f, 0.62f), rim);
            }
            else
            {
                // Uçan damla: dolu mavi, kenar hafif koyu.
                col = Color.Lerp(new Color(0.42f, 0.78f, 1f, 0.95f), new Color(0.12f, 0.45f, 0.85f, 1f), Mathf.Pow(d, 3f));
            }

            col = Color.Lerp(col, new Color(1f, 1f, 1f, 0.95f), h);
            col.a *= edge;
            pixels[y * Size + x] = col;
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }
}
