using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RocketBasket obstacle görseli: hücrede taban sprite + (opsiyonel) 3 roket overlay.
/// Bir renk fırlayınca o overlay küçülerek gizlenir; sepet boşalınca teardown.
/// Config RocketBasketService'ten gelir; GridSpawner tarafından spawn edilir.
/// </summary>
public sealed class RocketBasketView : MonoBehaviour
{
    private Image baseImage;
    private readonly Dictionary<TileType, Image> overlayByColor = new();

    /// <param name="rootImage">GridSpawner'ın oluşturduğu, hücreyi dolduran taban Image.</param>
    public void Init(RocketBasketService service, Image rootImage, float tileSize)
    {
        baseImage = rootImage;

        if (service == null || baseImage == null)
            return;

        if (service.BasketBaseSprite != null)
            baseImage.sprite = service.BasketBaseSprite;

        var cellRt = baseImage.rectTransform;
        var rockets = service.Rockets;
        if (rockets == null) return;

        for (int i = 0; i < rockets.Count; i++)
        {
            var cfg = rockets[i];
            if (cfg.basketOverlaySprite == null) continue;   // overlay opsiyonel

            var go = new GameObject($"Rocket_{cfg.color}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(cellRt, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float size = tileSize * Mathf.Clamp(cfg.overlaySizeRatio, 0.1f, 1f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(cfg.overlayAnchor.x * tileSize, cfg.overlayAnchor.y * tileSize);
            rt.localRotation = Quaternion.Euler(0f, 0f, cfg.overlayTiltDegrees);

            var img = go.GetComponent<Image>();
            img.sprite = cfg.basketOverlaySprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            overlayByColor[cfg.color] = img;
        }
    }

    /// <summary>Bu rengin roketi fırladı — overlay'i küçülterek gizle.</summary>
    public void LaunchRocket(TileType color)
    {
        if (overlayByColor.TryGetValue(color, out var img) && img != null)
            StartCoroutine(PopOut(img.rectTransform, img));
    }

    /// <summary>Son roket gitti — sepeti kısa bir gecikmeyle yok et.</summary>
    public void Teardown()
    {
        StartCoroutine(TeardownRoutine());
    }

    private IEnumerator TeardownRoutine()
    {
        // Son roketin fırlama animasyonunun başlaması için birkaç frame yaşat.
        yield return new WaitForSeconds(0.25f);
        if (this != null && gameObject != null)
            Destroy(gameObject);
    }

    private IEnumerator PopOut(RectTransform rt, Image img)
    {
        if (rt == null) yield break;

        Vector3 start = rt.localScale;
        float dur = 0.14f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            rt.localScale = Vector3.LerpUnclamped(start, start * 1.25f, k);
            if (img != null)
            {
                var c = img.color;
                c.a = 1f - k;
                img.color = c;
            }
            yield return null;
        }
        if (rt != null) rt.gameObject.SetActive(false);
    }
}
