using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class OverrideBatteryBoxView : MonoBehaviour
{
    [Header("Bars")]
    [SerializeField] private Image[] coreRedBars;
    [SerializeField] private Image[] gearYellowBars;
    [SerializeField] private Image[] boltBlueBars;
    [SerializeField] private Image[] plateGreenBars;

    [Header("Center")]
    [SerializeField] private Image centerImage;
    [SerializeField] private Image progressImage;
    [SerializeField] private Image pinImage;

    [Tooltip("Kapalıysa ApplyLayout hiçbir şeyi taşımaz; prefab'ta elle kurduğun yerleşim/pivot korunur. Kod fallback için açık kalmalı.")]
    [SerializeField] private bool autoLayout = true;

    [Header("Needle")]
    [Tooltip("İbre (pin) progress'e göre soldan sağa dönsün mü? Sprite pivotu tabanda olmalı.")]
    [SerializeField] private bool rotatePin = true;
    [Tooltip("progress=0 iken ibre açısı (Z). Sol/yeşil taraf.")]
    [SerializeField] private float needleStartAngle = 75f;
    [Tooltip("progress=total iken ibre açısı (Z). Sağ/kırmızı taraf.")]
    [SerializeField] private float needleEndAngle = -75f;
    [Tooltip("İbrenin döneceği merkez (kutu içinde fraksiyon). Gauge kadranının dönüş noktası.")]
    [SerializeField] private Vector2 gaugeCenter = new Vector2(0.5f, 0.47f);
    [Tooltip("İbre sprite'ının içindeki göbek (pivot) fraksiyonu. Dönüş bu noktadan olur.")]
    [SerializeField] private Vector2 needlePivot = new Vector2(0.5f, 0.42f);
    [Tooltip("İbre boyutu (kutunun kısa kenarına oran).")]
    [SerializeField, Range(0.1f, 1.2f)] private float needleSizeFraction = 0.34f;
    [Tooltip("Merkez gauge/dial sprite boyutu (kutunun kısa kenarına oran).")]
    [SerializeField, Range(0.1f, 1.2f)] private float gaugeSizeFraction = 0.34f;

    [Header("Bar boyutu (yuvaya oran)")]
    [Tooltip("Mavi/yeşil (üst/alt) bar genişlik oranı.")]
    [SerializeField, Range(0.2f, 1.2f)] private float horizontalBarWidthScale = 0.85f;
    [Tooltip("Mavi/yeşil (üst/alt) bar yükseklik oranı.")]
    [SerializeField, Range(0.2f, 1.2f)] private float horizontalBarHeightScale = 0.6f;
    [Tooltip("Kırmızı/sarı (sol/sağ, döndürülmüş) bar genişlik oranı.")]
    [SerializeField, Range(0.2f, 1.2f)] private float rotatedBarWidthScale = 0.62f;
    [Tooltip("Kırmızı/sarı (sol/sağ, döndürülmüş) bar yükseklik oranı.")]
    [SerializeField, Range(0.2f, 1.2f)] private float rotatedBarHeightScale = 1.0f;

    [Header("Motion")]
    [SerializeField, Min(0.1f)] private float detonationDuration = 2f;
    [SerializeField, Range(1f, 18f)] private float shakeMagnitude = 9f;
    [SerializeField, Range(10f, 80f)] private float shakeFrequency = 42f;
    [Tooltip("Patlama öncesi buhar puf sprite'ı (opsiyonel). Boşsa düz beyaz kare kullanılır.")]
    [SerializeField] private Sprite steamSprite;

    private Coroutine shakeRoutine;
    private int maxHitsPerColor = 3;

    public void SetBarImage(ChestColorMask color, int index, Image image)
    {
        if (index < 0 || image == null)
            return;

        var bars = EnsureBarArray(color, index + 1);
        bars[index] = image;
    }

    public void SetCenterImage(Image image) => centerImage = image;
    public void SetProgressImage(Image image) => progressImage = image;
    public void SetPinImage(Image image) => pinImage = image;
    public void SetSteamSprite(Sprite sprite) => steamSprite = sprite;

    public void Initialize(int hitsPerColor)
    {
        maxHitsPerColor = Mathf.Max(1, hitsPerColor);
        ApplyColorState(ChestColorMask.Core, maxHitsPerColor, maxHitsPerColor);
        ApplyColorState(ChestColorMask.Gear, maxHitsPerColor, maxHitsPerColor);
        ApplyColorState(ChestColorMask.Bolt, maxHitsPerColor, maxHitsPerColor);
        ApplyColorState(ChestColorMask.Plate, maxHitsPerColor, maxHitsPerColor);
        ApplyProgress(0, maxHitsPerColor * 4);
    }

    public void ApplyColorState(ChestColorMask color, int remaining, int maxHits)
    {
        var bars = GetBars(color);
        if (bars == null || bars.Length == 0)
            return;

        maxHits = Mathf.Max(1, maxHits);
        remaining = Mathf.Clamp(remaining, 0, maxHits);
        int visibleCount = Mathf.CeilToInt((remaining / (float)maxHits) * bars.Length);

        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] == null)
                continue;

            bars[i].gameObject.SetActive(i < visibleCount);
        }
    }

    public void ApplyProgress(int progress, int total)
    {
        float frac = total > 0 ? Mathf.Clamp01(progress / (float)total) : 0f;

        if (progressImage != null)
        {
            progressImage.type = Image.Type.Filled;
            progressImage.fillMethod = Image.FillMethod.Radial360;
            progressImage.fillClockwise = true;
            progressImage.fillAmount = frac;
        }

        // İbre: soldan (0) sağa (full) doğru döner. Radial fill ile birlikte çalışır.
        if (rotatePin && pinImage != null)
        {
            float angle = Mathf.Lerp(needleStartAngle, needleEndAngle, frac);
            pinImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    public void Shake()
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(0.28f, shakeMagnitude * 0.7f));
    }

    public void PlayDetonationAndDestroy()
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(DetonationRoutine());
    }

    public void ApplyLayout()
    {
        if (!autoLayout)
            return;

        LayoutBars(ChestColorMask.Bolt, 0.42f, 0.62f, 0.58f, 0.96f, vertical: true);
        LayoutBars(ChestColorMask.Plate, 0.42f, 0.04f, 0.58f, 0.38f, vertical: true);
        LayoutBars(ChestColorMask.Core, 0.04f, 0.42f, 0.38f, 0.58f, vertical: false);
        LayoutBars(ChestColorMask.Gear, 0.62f, 0.42f, 0.96f, 0.58f, vertical: false);
        LayoutCentered(progressImage, gaugeSizeFraction);
        LayoutCentered(centerImage, gaugeSizeFraction);
        LayoutNeedle();

        // Render sırası (alttan üste): barlar < radial fill < gauge < ibre.
        // Gauge barların ÜSTÜNDE olsun ki barlar onu ezmesin.
        if (progressImage != null)
            progressImage.transform.SetAsLastSibling();
        if (centerImage != null)
            centerImage.transform.SetAsLastSibling();
        if (pinImage != null)
            pinImage.transform.SetAsLastSibling();
    }

    // Düdüklü tencere: önce basınç birikir (büzülme + hızlanan titreme + buhar),
    // sonra jelly gibi esneyerek-titreyerek şişer, en sonda merkezli toz bulutu patlar.
    private IEnumerator DetonationRoutine()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null)
        {
            Destroy(gameObject);
            yield break;
        }

        // Pivot'u merkeze al (2x2'de sol-üst yerine gerçek merkez etrafında ölçek/titreme).
        RecenterPivot(rt);

        var rootImage = GetComponent<Image>();
        Color rootBase = rootImage != null ? rootImage.color : Color.white;
        Vector2 origin = rt.anchoredPosition;
        Vector3 baseScale = rt.localScale;

        const float buildPortion = 0.6f;   // ilk %60 basınç, sonrası jelly şişme
        float elapsed = 0f;
        float nextSteamAt = 0f;

        while (elapsed < detonationDuration && rt != null)
        {
            float t = elapsed / detonationDuration;

            if (t < buildPortion)
            {
                // BASINÇ: büzülme + hızlanan titreme + kızarma
                float p = t / buildPortion;
                float ease = p * p;
                float squeeze = Mathf.Lerp(1f, 0.82f, ease);
                float breathe = 1f + Mathf.Sin(elapsed * 26f) * 0.025f * p;
                rt.localScale = baseScale * (squeeze * breathe);

                float freq = Mathf.Lerp(28f, 72f, p);
                float mag = Mathf.Lerp(2f, shakeMagnitude, p);
                rt.anchoredPosition = origin + new Vector2(
                    Mathf.Sin(elapsed * freq) * mag,
                    Mathf.Cos(elapsed * freq * 1.27f) * mag * 0.6f);

                if (rootImage != null)
                    rootImage.color = Color.Lerp(rootBase, new Color(1f, 0.62f, 0.5f, rootBase.a), ease * 0.6f);

                float gap = Mathf.Lerp(0.10f, 0.035f, p);
                if (elapsed >= nextSteamAt) { SpawnSteam(p, burst: false); nextSteamAt = elapsed + gap; }
            }
            else
            {
                // JELLY ŞİŞME: büyürken yanlardan/üst-alttan esneyip titremeye devam eder.
                float s = (t - buildPortion) / (1f - buildPortion);   // 0..1
                float env = Mathf.Lerp(0.82f, 1.35f, 1f - (1f - s) * (1f - s));  // easeOut büyüme

                // İki eksen ters fazda salınır → squash & stretch (jöle) hissi
                float wob = Mathf.Lerp(0.16f, 0.08f, s);
                float sx = env * (1f + Mathf.Sin(elapsed * 24f) * wob);
                float sy = env * (1f + Mathf.Sin(elapsed * 24f + Mathf.PI) * wob);
                rt.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, 1f);

                // Şişerken de titreme sürsün (azalan)
                float tr = Mathf.Lerp(shakeMagnitude * 0.7f, 2f, s);
                rt.anchoredPosition = origin + new Vector2(
                    Mathf.Sin(elapsed * 58f) * tr,
                    Mathf.Cos(elapsed * 64f) * tr * 0.7f);

                if (elapsed >= nextSteamAt) { SpawnSteam(1f, burst: false); nextSteamAt = elapsed + 0.04f; }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // FİNAL: kutu son bir kez şişip saydamlaşarak kaybolur. Asıl patlama görseli,
        // board'ın buradan yayılan override radyal dalgası (OverrideBatteryBoxDetonationAction).
        var cg = gameObject.GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        for (int k = 0; k < 9; k++)
            SpawnSteam(1f, burst: true);

        const float fade = 0.2f;
        float fe = 0f;
        while (fe < fade && rt != null)
        {
            float u = fe / fade;
            rt.localScale = new Vector3(baseScale.x, baseScale.y, 1f) * Mathf.Lerp(1.35f, 1.7f, u);
            cg.alpha = 1f - u;
            fe += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
        shakeRoutine = null;
    }

    private static void RecenterPivot(RectTransform rt)
    {
        Vector2 oldPivot = rt.pivot;
        Vector2 newPivot = new Vector2(0.5f, 0.5f);
        Vector2 size = rt.rect.size;
        rt.pivot = newPivot;
        rt.anchoredPosition += new Vector2(
            (newPivot.x - oldPivot.x) * size.x,
            (newPivot.y - oldPivot.y) * size.y);
    }

    private void SpawnSteam(float intensity, bool burst)
    {
        var go = new GameObject("Steam", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;   // layer 0 culling'e karşı (Screen Space Camera)
        go.transform.SetParent(transform, false);

        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        // Yumuşak radyal puf (roket dumanıyla aynı yöntem). Kendi sprite'ın varsa onu kullan.
        image.sprite = steamSprite != null ? steamSprite : GetSoftSmokeSprite();
        image.preserveAspect = true;
        image.color = new Color(0.78f, 0.70f, 0.58f, burst ? 0.95f : 0.84f);

        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        float boxW = GetComponent<RectTransform>().rect.width;
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir == Vector2.zero) dir = Vector2.up;
        dir = (dir + Vector2.up * 0.6f).normalized;   // valften kaçan buhar: yukarı eğilim

        // Kutuya oranlı boyut/konum → obstacle boyutundan bağımsız tutarlı görünür.
        float size = boxW * (burst ? Random.Range(0.46f, 0.72f)
                                   : Random.Range(0.30f, 0.46f) * Mathf.Lerp(0.9f, 1.22f, intensity));
        rt.sizeDelta = Vector2.one * size;
        rt.anchoredPosition = dir * boxW * 0.38f * Random.Range(0.42f, 0.95f);
        rt.SetAsLastSibling();

        StartCoroutine(SteamRoutine(image, dir, burst));
    }

    private static IEnumerator SteamRoutine(Image image, Vector2 dir, bool burst)
    {
        if (image == null)
            yield break;

        var rt = image.rectTransform;
        Vector2 startPos = rt.anchoredPosition;
        float own = rt.sizeDelta.x;
        Vector2 target = startPos + dir * own * (burst ? 1.45f : 0.95f) + Vector2.up * own * 0.42f;
        float duration = burst ? 0.62f : 0.82f;
        Vector3 startScale = rt.localScale;
        Vector3 endScale = startScale * (burst ? 2.45f : 2.05f);
        float startA = image.color.a;
        float elapsed = 0f;

        while (elapsed < duration && image != null)
        {
            float u = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - u, 2f);   // easeOut: hızlı açılıp yavaş sönme
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, target, eased);
            rt.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            var c = image.color;
            c.a = startA * (1f - eased);
            image.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (image != null)
            Destroy(image.gameObject);
    }

    // Prosedürel yumuşak radyal puf (roket dumanıyla aynı). Bir kez üretilip cache'lenir.
    private static Sprite softSmokeSprite;

    private static Sprite GetSoftSmokeSprite()
    {
        if (softSmokeSprite != null)
            return softSmokeSprite;

        const int res = 64;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
        float radius = res * 0.48f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center) / radius;
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a);   // yumuşak kenar (smoothstep)
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply(false, true);
        softSmokeSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        softSmokeSprite.name = "GeneratedOBBSteam";
        return softSmokeSprite;
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null)
            yield break;

        Vector2 origin = rt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration && rt != null)
        {
            float damp = 1f - elapsed / duration;
            rt.anchoredPosition = origin + new Vector2(Mathf.Sin(elapsed * shakeFrequency) * magnitude * damp, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (rt != null)
            rt.anchoredPosition = origin;
        shakeRoutine = null;
    }

    private Image[] EnsureBarArray(ChestColorMask color, int size)
    {
        switch (color)
        {
            case ChestColorMask.Core:
                return coreRedBars = EnsureSize(coreRedBars, size);
            case ChestColorMask.Gear:
                return gearYellowBars = EnsureSize(gearYellowBars, size);
            case ChestColorMask.Bolt:
                return boltBlueBars = EnsureSize(boltBlueBars, size);
            case ChestColorMask.Plate:
                return plateGreenBars = EnsureSize(plateGreenBars, size);
            default:
                return null;
        }
    }

    private Image[] GetBars(ChestColorMask color) => color switch
    {
        ChestColorMask.Core  => coreRedBars,
        ChestColorMask.Gear  => gearYellowBars,
        ChestColorMask.Bolt  => boltBlueBars,
        ChestColorMask.Plate => plateGreenBars,
        _                    => null
    };

    private static Image[] EnsureSize(Image[] source, int size)
    {
        source ??= new Image[size];
        if (source.Length >= size)
            return source;

        var next = new Image[size];
        for (int i = 0; i < source.Length; i++)
            next[i] = source[i];
        return next;
    }

    private void LayoutBars(ChestColorMask color, float xMin, float yMin, float xMax, float yMax, bool vertical)
    {
        var bars = GetBars(color);
        if (bars == null || bars.Length == 0)
            return;

        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] == null)
                continue;

            float a0 = i / (float)bars.Length;
            float a1 = (i + 1) / (float)bars.Length;
            if (vertical)
            {
                // Üst/alt panel: yuva yatay, bar sprite'ı zaten yatay → döndürme yok.
                LayoutBarSized(bars[i], xMin, Mathf.Lerp(yMin, yMax, a0), xMax, Mathf.Lerp(yMin, yMax, a1),
                    horizontalBarWidthScale, horizontalBarHeightScale, rotate: false);
            }
            else
            {
                // Sol/sağ panel: yuva dikey. Yatay bar sprite'ını 90° döndürüp oturt.
                LayoutBarSized(bars[i], Mathf.Lerp(xMin, xMax, a0), yMin, Mathf.Lerp(xMin, xMax, a1), yMax,
                    rotatedBarWidthScale, rotatedBarHeightScale, rotate: true);
            }
        }
    }

    // Bir bar sprite'ını yuva merkezine, bağımsız genişlik/yükseklik oranıyla yerleştirir.
    // rotate=true ise 90° döndürür (sol/sağ dikey yuvalar için).
    private void LayoutBarSized(Image image, float xMin, float yMin, float xMax, float yMax,
        float widthScale, float heightScale, bool rotate)
    {
        if (image == null)
            return;

        Rect self = GetComponent<RectTransform>().rect;
        var rt = image.rectTransform;
        float cx = (xMin + xMax) * 0.5f;
        float cy = (yMin + yMax) * 0.5f;

        rt.anchorMin = rt.anchorMax = new Vector2(cx, cy);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        // Ekranda görünecek footprint (yatay × dikey)
        float onW = Mathf.Max(1f, (xMax - xMin) * self.width) * widthScale;
        float onH = Mathf.Max(1f, (yMax - yMin) * self.height) * heightScale;

        if (rotate)
        {
            // 90° döndürülünce genişlik↔yükseklik yer değişir; footprint için swap'la.
            rt.sizeDelta = new Vector2(onH, onW);
            rt.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        else
        {
            rt.sizeDelta = new Vector2(onW, onH);
            rt.localRotation = Quaternion.identity;
        }

        rt.localScale = Vector3.one;
        image.preserveAspect = false;
    }

    // GridSpawner fallback'ten canlı ayar için (view runtime'da AddComponent edildiğinde).
    public void ConfigureLayout(float hBarW, float hBarH, float rBarW, float rBarH,
        float needleSize, float gaugeSize, Vector2 gauge, Vector2 pivot,
        float startAngle, float endAngle)
    {
        horizontalBarWidthScale = hBarW;
        horizontalBarHeightScale = hBarH;
        rotatedBarWidthScale = rBarW;
        rotatedBarHeightScale = rBarH;
        needleSizeFraction = needleSize;
        gaugeSizeFraction = gaugeSize;
        gaugeCenter = gauge;
        needlePivot = pivot;
        needleStartAngle = startAngle;
        needleEndAngle = endAngle;
    }

    // Bir görseli gauge merkezine, kutunun kısa kenarına oranla kare olarak yerleştirir.
    private void LayoutCentered(Image image, float sizeFraction)
    {
        if (image == null)
            return;

        Rect self = GetComponent<RectTransform>().rect;
        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = gaugeCenter;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        float size = Mathf.Min(self.width, self.height) * sizeFraction;
        rt.sizeDelta = new Vector2(size, size);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        image.preserveAspect = true;
    }

    // İbre: göbeğinden (needlePivot) gauge merkezine sabitlenir; dönüş ApplyProgress'te.
    private void LayoutNeedle()
    {
        if (pinImage == null)
            return;

        Rect self = GetComponent<RectTransform>().rect;
        var rt = pinImage.rectTransform;
        rt.anchorMin = rt.anchorMax = gaugeCenter;
        rt.pivot = needlePivot;
        rt.anchoredPosition = Vector2.zero;

        float size = Mathf.Min(self.width, self.height) * needleSizeFraction;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one;
        pinImage.preserveAspect = true;
    }

    private void LayoutImage(Image image, float xMin, float yMin, float xMax, float yMax)
    {
        if (image == null)
            return;

        var rt = image.rectTransform;
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.one * 2f;
        rt.offsetMax = Vector2.one * -2f;
        rt.localScale = Vector3.one;
    }
}
