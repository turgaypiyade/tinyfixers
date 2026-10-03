using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Su telefon camına sıçradı": su deposu kırılınca ekranın önünde damlalar belirir, bir süre
/// asılı kalır, sonra camdan aşağı iz bırakarak süzülüp söner.
///
/// ScreenCrackFx kalıbı: kendi Screen Space Overlay canvas'ı, GraphicRaycaster yok, tüm Graphic'ler
/// raycastTarget=false → input'u (dynamic input) asla kesmez, oyunu bekletmez. Hepsi bitince
/// canvas'ı ile kendini yok eder.
/// </summary>
public class ScreenRainDropsFx : MonoBehaviour
{
    private const int SortingOrder = 890;                 // board/HUD üstü; ScreenCrackFx (900) altı
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

    private const float PopDuration = 0.14f;
    private const float SpawnWindow = 0.28f;              // damlalar bu pencereye serpiştirilir
    private const float FadeDuration = 0.45f;

    private RectTransform root;
    private int alive;
    private float maxAlpha = 1f;

    /// <summary>screenPoint: sıçramanın geldiği nokta (piksel). Damlaların bir kısmı onun etrafında
    /// kümelenir, kalanı ekrana dağılır.</summary>
    public static ScreenRainDropsFx Play(Vector2 screenPoint, WaterTankConfig config)
    {
        int count = config != null ? config.screenDropCount : 10;
        if (count <= 0)
            return null;

        var go = new GameObject("__ScreenRainDropsFx", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.layer = LayerMask.NameToLayer("UI");

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        var fx = go.AddComponent<ScreenRainDropsFx>();
        fx.Begin(screenPoint, config, count);
        return fx;
    }

    private void Begin(Vector2 screenPoint, WaterTankConfig config, int count)
    {
        root = (RectTransform)transform;
        Canvas.ForceUpdateCanvases();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out var focus);

        Vector2 half = root.rect.size * 0.5f;
        Vector2 sizeRange = config != null ? config.screenDropSize : new Vector2(70f, 190f);
        Vector2 lifeRange = config != null ? config.screenDropLifetime : new Vector2(1.6f, 2.6f);
        List<Sprite> sprites = config != null ? config.screenDropSprites : null;
        maxAlpha = config != null ? config.screenDropAlpha : 1f;

        for (int i = 0; i < count; i++)
        {
            // Yarısı sıçrama noktası çevresinde kümelenir (su oradan geldi), yarısı ekrana dağılır.
            Vector2 pos = i % 2 == 0
                ? focus + Random.insideUnitCircle * 360f
                : new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y * 0.85f, half.y * 0.85f));
            pos.x = Mathf.Clamp(pos.x, -half.x + 40f, half.x - 40f);
            pos.y = Mathf.Clamp(pos.y, -half.y + 80f, half.y - 80f);

            float size = Random.Range(sizeRange.x, sizeRange.y);
            float life = Random.Range(lifeRange.x, lifeRange.y);
            float delay = Random.Range(0f, SpawnWindow);
            var sprite = PickSprite(sprites, i);

            alive++;
            StartCoroutine(RunDrop(sprite, pos, size, life, delay));
        }

        StartCoroutine(DestroyWhenDone());
    }

    private static Sprite PickSprite(List<Sprite> sprites, int index)
    {
        if (sprites != null && sprites.Count > 0)
        {
            int n = sprites.Count;
            for (int j = 0; j < n; j++)
            {
                var s = sprites[(index + j) % n];
                if (s != null) return s;
            }
        }
        return WaterDropletSprites.Glass;
    }

    private IEnumerator RunDrop(Sprite sprite, Vector2 pos, float size, float life, float delay)
    {
        if (delay > 0f)
            yield return Wait(delay);

        // İz: damlanın başladığı noktadan aşağı uzayan ince ıslak çizgi (damlanın arkasında).
        var trail = CreateImage("Trail", sprite);
        trail.color = new Color(1f, 1f, 1f, 0f);
        var trt = trail.rectTransform;
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = pos;
        float trailWidth = size * 0.32f;
        trt.sizeDelta = new Vector2(trailWidth, 0f);

        var drop = CreateImage("Drop", sprite);
        var rt = drop.rectTransform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size * Random.Range(0.92f, 1.12f));
        rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(-12f, 12f));

        // 1) Çarpma: hafif taşarak belirir.
        float t = 0f;
        while (t < PopDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / PopDuration);
            float s = k < 0.7f ? Mathf.Lerp(0.2f, 1.18f, k / 0.7f) : Mathf.Lerp(1.18f, 1f, (k - 0.7f) / 0.3f);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;

        // 2) Asılı kalır, sonra camdan aşağı süzülür (büyük damla daha hızlı/uzun gider), son
        //    FadeDuration'da söner.
        float hold = life * Random.Range(0.25f, 0.45f);
        float slide = Mathf.Max(0.1f, life - hold);
        float travel = size * Random.Range(1.2f, 2.8f);
        float wobble = Random.Range(-18f, 18f);
        t = 0f;
        while (t < life)
        {
            t += Time.unscaledDeltaTime;

            float sk = t <= hold ? 0f : Mathf.Clamp01((t - hold) / slide);
            float eased = sk * sk;                         // yavaş başlar, hızlanır (yerçekimi)
            float dy = travel * eased;
            float dx = wobble * Mathf.Sin(sk * Mathf.PI);
            rt.anchoredPosition = pos + new Vector2(dx, -dy);

            // Süzülürken hafif incelir (damla suyunu ize bırakır).
            float shrink = Mathf.Lerp(1f, 0.72f, sk);
            rt.localScale = new Vector3(shrink, shrink, 1f);

            float fade = 1f - Mathf.Clamp01((t - (life - FadeDuration)) / FadeDuration);
            SetAlpha(drop, fade * maxAlpha);

            trt.sizeDelta = new Vector2(trailWidth * shrink, dy + size * 0.2f);
            SetAlpha(trail, 0.45f * maxAlpha * fade * Mathf.Clamp01(sk * 4f));
            yield return null;
        }

        Destroy(drop.gameObject);
        Destroy(trail.gameObject);
        alive--;
    }

    private IEnumerator DestroyWhenDone()
    {
        yield return null;
        while (alive > 0)
            yield return null;
        Destroy(gameObject);
    }

    private Image CreateImage(string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(root, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        c.a = a;
        g.color = c;
    }

    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
