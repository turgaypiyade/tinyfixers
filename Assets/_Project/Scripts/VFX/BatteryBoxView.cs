using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BatteryBox obstacle için görsel yönetici.
/// 4 pil (Gear/Core/Bolt/Plate) her biri kendi sprite dizisine sahip.
/// Sprite dizisi: index 0 = tam dolu (def.hits hit kaldı), son index = boşalmış.
/// ApplyBatteryState(color, remaining, max) çağrısıyla güncellenir.
/// </summary>
public class BatteryBoxView : MonoBehaviour
{
    [Header("Battery Images")]
    [SerializeField] private Image gearImage;
    [SerializeField] private Image coreImage;
    [SerializeField] private Image boltImage;
    [SerializeField] private Image plateImage;

    [Header("Battery Sprites (index 0=full, last=depleted)")]
    [SerializeField] private Sprite[] gearSprites;
    [SerializeField] private Sprite[] coreSprites;
    [SerializeField] private Sprite[] boltSprites;
    [SerializeField] private Sprite[] plateSprites;

    [Header("Shake")]
    [SerializeField, Range(0.05f, 0.8f)] private float shakeDuration  = 0.30f;
    [SerializeField, Range(1f, 20f)]     private float shakeMagnitude = 6f;
    [SerializeField, Range(10f, 60f)]    private float shakeFrequency = 35f;

    [Header("Layout")]
    [SerializeField, Range(0f, 40f)]   private float iconPadding      = 10f;
    [SerializeField, Range(0f, 20f)]   private float innerSpacing      = 4f;
    [SerializeField, Range(-0.3f, 0.3f)] private float verticalShift  = 0f;
    [SerializeField]                   private Vector2 batteryAspect   = new Vector2(1f, 2f);
    [SerializeField, Range(0f, 8f)]    private float horizontalNudge   = 4f;
    [SerializeField, Range(0.1f, 4f)]  private float iconScale          = 1f;

    // GridSpawner tarafından runtime'da atanan sprite dizileri (prefab yoksa)
    public void SetBatterySprites(ChestColorMask color, Sprite[] sprites)
    {
        switch (color)
        {
            case ChestColorMask.Gear:  gearSprites  = sprites; break;
            case ChestColorMask.Core:  coreSprites  = sprites; break;
            case ChestColorMask.Bolt:  boltSprites  = sprites; break;
            case ChestColorMask.Plate: plateSprites = sprites; break;
        }
    }

    public void SetBatteryImage(ChestColorMask color, Image img)
    {
        switch (color)
        {
            case ChestColorMask.Gear:  gearImage  = img; break;
            case ChestColorMask.Core:  coreImage  = img; break;
            case ChestColorMask.Bolt:  boltImage  = img; break;
            case ChestColorMask.Plate: plateImage = img; break;
        }
    }

    /// <summary>
    /// Pil görselini güncelle. remaining = kalan hit sayısı, max = toplam hit.
    /// remaining==0 → boşalmış sprite.
    /// </summary>
    public void ApplyBatteryState(ChestColorMask color, int remaining, int maxHits)
    {
        var (img, sprites) = ResolveForColor(color);
        if (img == null || sprites == null || sprites.Length == 0) return;

        int spriteIndex = Mathf.Clamp(maxHits - remaining, 0, sprites.Length - 1);
        img.sprite = sprites[spriteIndex];
        img.gameObject.SetActive(true);
    }

    public void ShowAll()
    {
        SetActive(gearImage,  true);
        SetActive(coreImage,  true);
        SetActive(boltImage,  true);
        SetActive(plateImage, true);
    }

    // GridSpawner parent boyutunu set ettikten sonra çağırır
    public void ApplyLayout()
    {
        float s = verticalShift;
        float h = innerSpacing * 0.5f;
        float n = horizontalNudge;

        PlaceIcon(gearImage,  0f, 0.5f+s, 0.5f, 1.0f+s, iconPadding, h, h, iconPadding, +n);
        PlaceIcon(coreImage,  0.5f, 0.5f+s, 1.0f, 1.0f+s, h, iconPadding, h, iconPadding, -n);
        PlaceIcon(boltImage,  0f, 0.0f+s, 0.5f, 0.5f+s, iconPadding, h, iconPadding, h, +n);
        PlaceIcon(plateImage, 0.5f, 0.0f+s, 1.0f, 0.5f+s, h, iconPadding, iconPadding, h, -n);
    }

    private Coroutine _shakeRoutine;

    public void Shake()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        var rt = GetComponent<RectTransform>();
        Vector2 origin = rt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float damp = 1f - elapsed / shakeDuration;
            rt.anchoredPosition = origin + new Vector2(Mathf.Sin(elapsed * shakeFrequency) * shakeMagnitude * damp, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        rt.anchoredPosition = origin;
        _shakeRoutine = null;
    }

    private (Image img, Sprite[] sprites) ResolveForColor(ChestColorMask color) => color switch
    {
        ChestColorMask.Gear  => (gearImage,  gearSprites),
        ChestColorMask.Core  => (coreImage,  coreSprites),
        ChestColorMask.Bolt  => (boltImage,  boltSprites),
        ChestColorMask.Plate => (plateImage, plateSprites),
        _                    => (null, null)
    };

    private void PlaceIcon(Image img, float xMin, float yMin, float xMax, float yMax,
        float left, float right, float bottom, float top, float xNudge)
    {
        if (img == null) return;
        var rt = img.GetComponent<RectTransform>();
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        Rect p = parent.rect;
        float ax0 = p.xMin + p.width * xMin + left;
        float ax1 = p.xMin + p.width * xMax - right;
        float ay0 = p.yMin + p.height * yMin + bottom;
        float ay1 = p.yMin + p.height * yMax - top;

        float avW = Mathf.Max(0f, ax1 - ax0);
        float avH = Mathf.Max(0f, ay1 - ay0);

        float aspect = batteryAspect.x / Mathf.Max(0.001f, batteryAspect.y);
        float fw = avW;
        float fh = fw / aspect;
        if (fh > avH) { fh = avH; fw = fh * aspect; }
        fw *= iconScale;
        fh *= iconScale;

        Vector2 center = new Vector2((ax0 + ax1) * 0.5f + xNudge, (ay0 + ay1) * 0.5f);
        Vector2 parentCenter = new Vector2(p.xMin + p.width * 0.5f, p.yMin + p.height * 0.5f);

        img.preserveAspect = true;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center - parentCenter;
        rt.sizeDelta = new Vector2(fw, fh);
        rt.localScale = Vector3.one;
    }

    private static void SetActive(Image img, bool active)
    {
        if (img != null) img.gameObject.SetActive(active);
    }
}
