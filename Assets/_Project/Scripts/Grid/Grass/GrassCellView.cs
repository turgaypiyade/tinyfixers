using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Tek bir grass hücresinin görseli. Taşların ÜSTÜNDE (overTiles root) çizilir; hücreden
/// biraz TAŞAR (overhang) ki komşu grass hücreleriyle yaprakları üst üste binip dikişsiz,
/// tek bütün bir bitki örtüsü gibi görünsün. Her hit'te yapraklar hafifçe sallanır; HP 0'a
/// inince fade-out ile kalkar.
///
/// İki farklı sprite (A/B) GrassOverlayService tarafından hücre konumuna göre atanır
/// (dama-tahtası: (x+y) tek/çift). Böylece yan yana aynı desen tekrar etmez, organik durur.
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class GrassCellView : MonoBehaviour
{
    private RectTransform rt;
    private Image image;

    private int gridX, gridY;
    private Coroutine swayRoutine;
    private Coroutine fadeRoutine;
    private bool isClearing;

    public int GridX => gridX;
    public int GridY => gridY;
    public bool IsClearing => isClearing;

    public void Init(Sprite sprite, int x, int y)
    {
        rt = GetComponent<RectTransform>();
        image = GetComponent<Image>();

        gridX = x;
        gridY = y;
        isClearing = false;

        image.raycastTarget = false;
        image.preserveAspect = false;   // kare hücreye tam otursun; taşırma sizeDelta ile verilir
        image.sprite = sprite;

        var c = image.color; c.a = 1f; image.color = c;
    }

    /// Hücreye yerleştir + her yöne toplam expandPixels kadar BÜYÜT (105px hücre → 107px sprite = 2px
    /// bindirme). Anchor top-left; grid Y aşağı artar. Merkezlenir, her kenardan expandPixels/2 taşar.
    public void PlaceInCell(int tileSize, float expandPixels)
    {
        float e = Mathf.Max(0f, expandPixels);
        float side = e * 0.5f;
        PlaceInCell(tileSize, side, side, side, side);
    }

    public void PlaceInCell(int tileSize, float leftPixels, float rightPixels, float topPixels, float bottomPixels)
    {
        if (rt == null) return;

        float left = Mathf.Max(0f, leftPixels);
        float right = Mathf.Max(0f, rightPixels);
        float top = Mathf.Max(0f, topPixels);
        float bottom = Mathf.Max(0f, bottomPixels);

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        float centerX = gridX * tileSize + tileSize * 0.5f + (right - left) * 0.5f;
        float centerY = -(gridY * tileSize + tileSize * 0.5f) + (top - bottom) * 0.5f;
        rt.anchoredPosition = new Vector2(centerX, centerY);
        rt.sizeDelta = new Vector2(tileSize + left + right, tileSize + top + bottom);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    /// Doğal shingle: doğum sırasında (y,x artan) en üste alınır → her karo yalnız SAĞ ve ALT
    /// komşusuyla örtüşür, dört yandan kesilmez. Boy farkı (B daha büyük) grid hissini yine kırar.
    public void SetSortingHint()
    {
        transform.SetAsLastSibling();
    }

    // ── Hit tepkisi: yaprak sallanması ─────────────────────────────────────────
    public void PlaySway(float amplitudeDeg, float duration, float cycles)
    {
        if (isClearing) return;
        if (!isActiveAndEnabled) return;
        if (swayRoutine != null) StopCoroutine(swayRoutine);
        swayRoutine = StartCoroutine(SwayRoutine(amplitudeDeg, duration, cycles));
    }

    private IEnumerator SwayRoutine(float amplitudeDeg, float duration, float cycles)
    {
        float t = 0f;
        duration = Mathf.Max(0.05f, duration);

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float damp = 1f - k;                                   // sönümlenerek dur
            float angle = Mathf.Sin(k * Mathf.PI * 2f * cycles) * amplitudeDeg * damp;
            if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        if (rt != null) rt.localRotation = Quaternion.identity;
        swayRoutine = null;
    }

    // ── Temizlenme ──────────────────────────────────────────────────────────────
    public void PlayClear(float fadeDuration)
    {
        if (isClearing) return;
        isClearing = true;

        if (swayRoutine != null) { StopCoroutine(swayRoutine); swayRoutine = null; }
        if (rt != null) rt.localRotation = Quaternion.identity;
        if (image != null) image.raycastTarget = false;

        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        Destroy(gameObject, fadeDuration + 0.05f);

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (!isActiveAndEnabled) { HardClear(); return; }
        fadeRoutine = StartCoroutine(ClearRoutine(fadeDuration));
    }

    private IEnumerator ClearRoutine(float fadeDuration)
    {
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        float t = 0f;
        Color start = image != null ? image.color : Color.white;
        Vector3 startScale = rt != null ? rt.localScale : Vector3.one;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            if (image != null) { var c = start; c.a = 1f - k; image.color = c; }
            if (rt != null) rt.localScale = Vector3.LerpUnclamped(startScale, startScale * 1.12f, k);
            yield return null;
        }

        HardClear();
    }

    public void HardClear()
    {
        isClearing = false;
        if (swayRoutine != null) { StopCoroutine(swayRoutine); swayRoutine = null; }
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
        if (image != null)
        {
            var c = image.color;
            c.a = 0f;
            image.color = c;
            image.raycastTarget = false;
        }
        if (rt != null)
        {
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }
        gameObject.SetActive(false);
    }
}
