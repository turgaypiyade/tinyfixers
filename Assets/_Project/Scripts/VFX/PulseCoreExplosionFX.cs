using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PulseCoreExplosionFX : MonoBehaviour
{
    [SerializeField] private Image sunburstRays;
    [SerializeField] private Image innerGlow;
    [SerializeField] private Image shockwaveRing;
    [SerializeField] private Image coreFlash;

    [SerializeField] private float baseSize = 300f;
    [SerializeField, Range(0.5f, 2.0f)] private float areaOvershoot = 1.0f;

    [Tooltip("Tüm katmanların ortak başlangıç çapı (hücre). Merkezde üst üste = tek core gibi başlar.")]
    [SerializeField, Range(0.5f, 3f)] private float startDiameterCells = 1.5f;
    private float tileSizePx;

    [SerializeField] private float totalDuration = 0.85f;
    [SerializeField] private bool destroyOnFinish = true;

    // ───────────────────────── FIREWORK RANDOMIZATION ─────────────────────────
    [Header("Firework Randomization")]
    [Tooltip("Her patlamada rastgele renk ve glow/core/ring kalınlığı seçilir. Çap random değişmez.")]
    [SerializeField] private bool randomizeOnPlay = true;

    [Header("Thickness Randomization")]
    [Tooltip("Glow outline kalınlığı min. Eski 1-2 aralığının üzerinde başlar. Çapı değiştirmez.")]
    [SerializeField, Range(2f, 6f)] private float glowThicknessMin = 2.2f;

    [Tooltip("Glow outline kalınlığı max. Çapı değiştirmez.")]
    [SerializeField, Range(2f, 6f)] private float glowThicknessMax = 3.6f;

    [Tooltip("Core/flash outline kalınlığı min. Eski 1-2 aralığının üzerinde başlar. Çapı değiştirmez.")]
    [SerializeField, Range(2f, 6f)] private float coreThicknessMin = 2.4f;

    [Tooltip("Core/flash outline kalınlığı max. Çapı değiştirmez.")]
    [SerializeField, Range(2f, 6f)] private float coreThicknessMax = 4.2f;

    [Tooltip("Ring outline kalınlığı min. Çapı değiştirmez.")]
    [SerializeField, Range(1f, 3f)] private float ringThicknessMin = 1.2f;

    [Tooltip("Ring outline kalınlığı max. Çapı değiştirmez.")]
    [SerializeField, Range(1f, 3f)] private float ringThicknessMax = 2.4f;

    [Tooltip("Outline renginin alpha çarpanı. Kalınlık random, ana glow/core/ring alpha random değildir.")]
    [SerializeField, Range(0f, 1f)] private float thicknessAlphaMultiplier = 0.65f;

    [Tooltip("Sunburst çizgilerinin blur yarıçapı (px). Keskin sprite'ı ilk andan itibaren yumuşatır. Çapı değiştirmez.")]
    [SerializeField, Range(2f, 24f)] private float raysBlurSpread = 9f;

    [Tooltip("Sunburst blur için 360° etrafa dağıtılacak offset kopya sayısı. Fazla = daha yumuşak ama daha çok draw.")]
    [SerializeField, Range(4, 16)] private int raysBlurLayers = 12;
    // ──────────────────────────────────────────────────────────────────────────

    [SerializeField, Range(0.1f, 2f)] private float flashPeakSizeRatio = 0.75f;
    [SerializeField] private float flashInTime = 0.05f;
    [SerializeField] private float flashOutTime = 0.22f;
    [SerializeField] private float flashStartRatio = 0.3f;
    [SerializeField] private float flashEndRatio = 0.95f;
    [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 1f);

    [SerializeField, Range(0.1f, 2f)] private float glowPeakSizeRatio = 0.90f;
    [SerializeField] private float glowInTime = 0.08f;
    [SerializeField] private float glowOutTime = 0.50f;
    [SerializeField] private float glowStartRatio = 0.3f;
    [SerializeField] private float glowEndRatio = 0.95f;
    [SerializeField] private Color glowColor = new Color(1f, 0.55f, 0.1f, 0.75f);

    [SerializeField, Range(0.1f, 2f)] private float raysPeakSizeRatio = 0.95f;
    [SerializeField] private float raysInTime = 0.10f;
    [SerializeField] private float raysOutTime = 0.60f;
    [SerializeField] private float raysStartRatio = 0.2f;
    [SerializeField] private float raysEndRatio = 0.98f;
    [SerializeField] private float raysRotateSpeed = 90f;
    [SerializeField] private Color raysColor = new Color(1f, 0.82f, 0.25f, 0.95f);

    [SerializeField, Range(0.1f, 2f)] private float ringPeakSizeRatio = 1.0f;
    [SerializeField] private float ringInTime = 0.03f;
    [SerializeField] private float ringOutTime = 0.50f;
    [SerializeField] private float ringStartRatio = 0.2f;
    [SerializeField] private float ringEndRatio = 1.0f;
    [SerializeField] private Color ringColor = new Color(1f, 0.75f, 0.2f, 1f);

    [Header("Procedural Rays (İnce Işınlar)")]
    [Tooltip("Sunburst sprite'ı yerine prosedürel ince ışın texture'ı üret. Kalınlık/incelik/yumuşaklık tam kontrol.")]
    [SerializeField] private bool useProceduralRays = true;
    [Tooltip("Işın (çizgi) sayısı.")]
    [SerializeField, Range(6, 32)] private int rayCount = 16;
    [Tooltip("0.3=kalın ışın, 0.97=çok ince ışın. Çizgileri inceltir.")]
    [SerializeField, Range(0.3f, 0.97f)] private float rayThinness = 0.82f;
    [Tooltip("Işın kenarı yumuşaklığı. Referans: KESKİN ince ışık ışını → düşük tut (0.05).")]
    [SerializeField, Range(0.02f, 0.4f)] private float raySoftness = 0.05f;
    [Tooltip("Işınların merkezden başlayıp dışa doğru bittiği yarıçap (0..1). Yüksek = daha uzun ışın, en dışta kalır.")]
    [SerializeField, Range(0.5f, 1.0f)] private float rayReach = 0.95f;
    [Tooltip("Işınların gecikmeli çıkışı (sn). Önce yumuşak bloom oturur, sonra keskin ışınlar fışkırır (referans).")]
    [SerializeField, Range(0f, 0.5f)] private float rayStartDelay = 0.16f;
    [Tooltip("Işın düzensizliği: 0=kusursuz simetrik, 1=çok düzensiz (uzunluk/parlaklık/kalınlık ışın-ışın değişir).")]
    [SerializeField, Range(0f, 1f)] private float rayIrregularity = 0.6f;
    [Tooltip("Düzensizlik gürültüsünün açısal frekansı. Yüksek = komşu ışınlar birbirinden daha farklı.")]
    [SerializeField, Range(2f, 12f)] private float rayIrregFreq = 5f;
    [Tooltip("Prosedürel ışın texture çözünürlüğü.")]
    [SerializeField] private int rayTexSize = 256;

    [Header("Noisy Ring (Halka Kenar Düzensizliği)")]
    [Tooltip("Shockwave ring kenarını Perlin noise ile boz")]
    [SerializeField] private bool useNoisyRing = true;
    [Tooltip("Noise genliği — kenar ne kadar düzensiz")]
    [SerializeField, Range(0.05f, 0.50f)] private float noiseAmplitude = 0.25f;
    [Tooltip("Noise frekansı — kaç tane çıkıntı/girinti")]
    [SerializeField, Range(2f, 10f)] private float noiseFrequency = 5f;
    [Tooltip("Kenar yumuşaklığı — blur genişliği")]
    [SerializeField, Range(0.03f, 0.25f)] private float edgeSoftness = 0.15f;
    [Tooltip("Halka kalınlığı (0=ince, 0.4=kalın)")]
    [SerializeField, Range(0.08f, 0.50f)] private float ringThickness = 0.22f;
    [Tooltip("Prosedürel texture çözünürlüğü")]
    [SerializeField] private int noiseTexSize = 128;

    [SerializeField]
    private AnimationCurve easeOut =
        new AnimationCurve(new Keyframe(0, 0, 3, 3), new Keyframe(1, 1, 0, 0));

    [SerializeField]
    private AnimationCurve easeIn =
        new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 3, 3));

    /// <summary>
    /// Dalga (shockwave ring) yarıçapını PX cinsinden her frame bildirir — SpecialChainRunner
    /// halka-bazlı taş kırma/special tetiklemeyi BU gerçek animasyona senkronlar (tek saat).
    /// Bitişte/iptalde float.MaxValue gönderilir (kalan halkalar serbest kalsın).
    /// </summary>
    public System.Action<float> OnRadiusPx;

    private bool ringAnimStarted;

    private Coroutine playRoutine;
    private bool isShuttingDown;

    private bool baselineCached;

    private float origFlashPeakSizeRatio;
    private float origGlowPeakSizeRatio;
    private float origRaysPeakSizeRatio;
    private float origRingPeakSizeRatio;

    private Color origFlashColor;
    private Color origGlowColor;
    private Color origRaysColor;
    private Color origRingColor;

    private Shadow[] glowShadows;
    private Shadow[] coreShadows;
    private Shadow[] ringShadows;
    private Shadow[] raysShadows;
    private const int BlurLayerCount = 4; // kaç katman shadow ile blur oluşturulacak

    public void SetRadiusCells(int radiusCells, float tileSize)
    {
        int sideCells = radiusCells * 2 + 1;
        SetAreaCells(sideCells, tileSize);
    }

    public void SetAreaCells(int sideCells, float tileSize)
    {
        baseSize = tileSize * sideCells * areaOvershoot;
        tileSizePx = tileSize;
    }

    public void SetBaseSize(float size)
    {
        baseSize = size;
    }

    // Tüm katmanların ortak başlangıç boyutu (px): ~startDiameterCells hücre çapında,
    // merkezde üst üste → tek "core" gibi başlar, sonra her katman kendi peak'ine büyür.
    private float StartSizePx()
    {
        float s = tileSizePx > 0f ? tileSizePx * startDiameterCells : baseSize * 0.2f;
        return Mathf.Max(1f, s);
    }

    public void SetLifetime(float lifetime)
    {
        totalDuration = Mathf.Max(0.01f, lifetime);
    }

    /// <summary>
    /// Dalganın (shockwave ring) merkezden tam yarıçapa YOL ALMA süresi. Oyun mantığı
    /// halka-varış event'lerini bu büyümeye senkronlar; prefab'daki anlık (0.03s) değer
    /// yerine board tempo'sundan gelen okunabilir bir cephe hızı verilir.
    /// </summary>
    public void SetWaveTravelTime(float seconds)
    {
        if (seconds > 0f)
        {
            ringInTime = Mathf.Max(0.05f, seconds);
            if (totalDuration < ringInTime + ringOutTime)
                totalDuration = ringInTime + ringOutTime;
        }
    }

    private void OnEnable()
    {
        isShuttingDown = false;

        CacheBaselineOnce();

        if (randomizeOnPlay)
            ApplyRandomization();
        else
            RestoreBaseline();

        HideAll();

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(DeferredStart());
    }

    // Dalga yarıçapını (ring sizeDelta/2) her frame bildirir; yalnız BÜYÜYEN değer
    // raporlanır (prefab'ın bayat başlangıç boyutu erken tetiklemesin).
    private IEnumerator CoReportRadius()
    {
        float maxSeen = 0f;
        while (!isShuttingDown)
        {
            if (ringAnimStarted)
            {
                if (shockwaveRing == null) { OnRadiusPx?.Invoke(float.MaxValue); yield break; }
                float r = shockwaveRing.rectTransform.sizeDelta.x * 0.5f;
                if (r > maxSeen) { maxSeen = r; OnRadiusPx?.Invoke(maxSeen); }
            }
            yield return null;
        }
    }

    private void OnDisable()
    {
        isShuttingDown = true;
        OnRadiusPx?.Invoke(float.MaxValue);   // FX kapanırken kalan halkalar serbest
        OnRadiusPx = null;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  RANDOMIZATION
    // ═════════════════════════════════════════════════════════════════════════

    private void CacheBaselineOnce()
    {
        if (baselineCached)
            return;

        origFlashPeakSizeRatio = flashPeakSizeRatio;
        origGlowPeakSizeRatio = glowPeakSizeRatio;
        origRaysPeakSizeRatio = raysPeakSizeRatio;
        origRingPeakSizeRatio = ringPeakSizeRatio;

        origFlashColor = flashColor;
        origGlowColor = glowColor;
        origRaysColor = raysColor;
        origRingColor = ringColor;

        // Eski Outline varsa kaldır, yeni Shadow tabanlı blur kullanacağız
        RemoveOldOutline(innerGlow);
        RemoveOldOutline(coreFlash);
        RemoveOldOutline(shockwaveRing);
        RemoveOldOutline(sunburstRays);

        baselineCached = true;
    }

    private void RestoreBaseline()
    {
        flashPeakSizeRatio = origFlashPeakSizeRatio;
        glowPeakSizeRatio = origGlowPeakSizeRatio;
        raysPeakSizeRatio = origRaysPeakSizeRatio;
        ringPeakSizeRatio = origRingPeakSizeRatio;

        flashColor = origFlashColor;
        glowColor = origGlowColor;
        raysColor = origRaysColor;
        ringColor = origRingColor;

        DisableShadows(glowShadows);
        DisableShadows(coreShadows);
        DisableShadows(ringShadows);
        DisableShadows(raysShadows);
    }

    private void ApplyRandomization()
    {
        var variant = PulseFireworkPalette.PickRandomVariant();

        // Renk random kalır ama alpha random değişmez.
        // Alpha random olursa glow gradient dışarı daha görünür ve çap büyümüş gibi durur.
        flashColor = WithAlpha(variant.flash, origFlashColor.a);
        glowColor = WithAlpha(variant.glow, origGlowColor.a);
        raysColor = WithAlpha(variant.rays, origRaysColor.a);
        ringColor = WithAlpha(variant.ring, origRingColor.a);

        // Radius / çap sistemi korunur.
        // Normal Pulse radius 1, Pulse+Pulse radius 2 (5x5) gibi değerler
        // dışarıdan SetRadiusCells ile gelmeye devam eder.
        //
        // Burada random olarak baseSize, radius veya peak ratio değiştirmiyoruz.
        flashPeakSizeRatio = origFlashPeakSizeRatio;
        glowPeakSizeRatio = origGlowPeakSizeRatio;
        raysPeakSizeRatio = origRaysPeakSizeRatio;
        ringPeakSizeRatio = origRingPeakSizeRatio;

        // Glow ve core kalınlığı eski 1-2 aralığının üzerinde random değişir.
        // Radius / çap değişmez; sadece Outline.effectDistance değişir.
        float glowThickness = Random.Range(glowThicknessMin, glowThicknessMax);
        float coreThickness = Random.Range(coreThicknessMin, coreThicknessMax);
        float ringThickness = Random.Range(ringThicknessMin, ringThicknessMax);

        ApplyBlurShadows(innerGlow, ref glowShadows, glowColor, glowThickness);
        ApplyBlurShadows(coreFlash, ref coreShadows, flashColor, coreThickness);
        ApplyBlurShadows(shockwaveRing, ref ringShadows, ringColor, ringThickness);

        // Prosedürel ışınlarda yumuşaklık texture'a gömülü → shadow blur'a gerek yok.
        // Hazır sprite kullanılırsa keskin kenarları 360° radyal sözde-blur ile yumuşat.
        if (useProceduralRays)
            DisableShadows(raysShadows);
        else
            ApplyRadialBlurShadows(sunburstRays, ref raysShadows, raysColor, raysBlurSpread);
    }

    private void RemoveOldOutline(Image img)
    {
        if (img == null) return;
        var old = img.GetComponent<Outline>();
        if (old != null) Destroy(old);
    }

    private void ApplyBlurShadows(Image img, ref Shadow[] shadows, Color color, float thickness)
    {
        if (img == null)
            return;

        // Eski shadow'ları temizle
        if (shadows != null)
        {
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null)
                    Destroy(shadows[i]);
            }
        }

        shadows = new Shadow[BlurLayerCount];
        Color shadowColor = WithAlpha(color, color.a * thicknessAlphaMultiplier);

        // Farklı açı ve mesafelerde shadow katmanları oluştur
        // Düzensiz offset'ler organik/pürüzlü bir glow hissi verir
        float[] angles = { 25f, 110f, 200f, 310f };
        float[] distMults = { 1.0f, 0.75f, 1.15f, 0.6f };

        for (int i = 0; i < BlurLayerCount; i++)
        {
            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.useGraphicAlpha = true;

            float angle = angles[i % angles.Length] * Mathf.Deg2Rad;
            float dist = thickness * distMults[i % distMults.Length];
            // Hafif jitter — her patlamada biraz farklı çıkıntılar
            float jitterX = Random.Range(-thickness * 0.25f, thickness * 0.25f);
            float jitterY = Random.Range(-thickness * 0.25f, thickness * 0.25f);

            shadow.effectDistance = new Vector2(
                Mathf.Cos(angle) * dist + jitterX,
                Mathf.Sin(angle) * dist + jitterY);

            // Dış katmanlar daha soluk — doğal blur gradyan
            float alphaFalloff = Mathf.Lerp(1.0f, 0.35f, (float)i / (BlurLayerCount - 1));
            shadow.effectColor = WithAlpha(shadowColor, shadowColor.a * alphaFalloff);

            shadows[i] = shadow;
        }
    }

    // 360° etrafa dağıtılmış, artan yarıçaplı çok-katmanlı offset kopya = sözde-blur.
    // ApplyBlurShadows yalnız 4 yön kullanır (organik outline); bu ise sunburst gibi
    // keskin çizgili sprite'ı gerçekten yumuşatmak için tüm yönlere yayar.
    private void ApplyRadialBlurShadows(Image img, ref Shadow[] shadows, Color color, float spread)
    {
        if (img == null)
            return;

        if (shadows != null)
        {
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null)
                    Destroy(shadows[i]);
            }
        }

        int layers = Mathf.Max(4, raysBlurLayers);
        shadows = new Shadow[layers];
        Color shadowColor = WithAlpha(color, color.a * thicknessAlphaMultiplier);

        for (int i = 0; i < layers; i++)
        {
            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.useGraphicAlpha = true;

            // Her katman farklı açı; yarıçap içten dışa artar → yumuşak degrade
            float angle = (360f / layers) * i * Mathf.Deg2Rad;
            float dist = spread * Mathf.Lerp(0.35f, 1f, (float)i / (layers - 1));

            shadow.effectDistance = new Vector2(
                Mathf.Cos(angle) * dist,
                Mathf.Sin(angle) * dist);

            // Dış katmanlar daha soluk — blur gradyanı
            float alphaFalloff = Mathf.Lerp(0.85f, 0.25f, (float)i / (layers - 1));
            shadow.effectColor = WithAlpha(shadowColor, shadowColor.a * alphaFalloff);

            shadows[i] = shadow;
        }
    }

    private void DisableShadows(Shadow[] shadows)
    {
        if (shadows == null) return;
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null)
                shadows[i].enabled = false;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator DeferredStart()
    {
        yield return null;

        if (isShuttingDown || this == null || !isActiveAndEnabled)
            yield break;

        yield return PlayExplosion();
        playRoutine = null;
    }

    private void HideAll()
    {
        if (sunburstRays != null) sunburstRays.color = WithAlpha(raysColor, 0f);
        if (innerGlow != null) innerGlow.color = WithAlpha(glowColor, 0f);
        if (shockwaveRing != null) shockwaveRing.color = WithAlpha(ringColor, 0f);
        if (coreFlash != null) coreFlash.color = WithAlpha(flashColor, 0f);
    }

    private IEnumerator PlayExplosion()
    {
        yield return StartCoroutine(CoPlayExplosion());

        if (!isShuttingDown && destroyOnFinish && gameObject != null)
            Destroy(gameObject);
    }

    private Texture2D noisyGlowTexture; // cleanup için referans
    private Texture2D proceduralRaysTexture;

    private IEnumerator CoPlayExplosion()
    {
        // Ring sprite'ını prosedürel noisy halka texture ile değiştir
        if (useNoisyRing && shockwaveRing != null)
            ApplyNoisyRingSprite();

        // Sunburst sprite'ını prosedürel ince ışın texture ile değiştir
        if (useProceduralRays && sunburstRays != null)
            ApplySunburstSprite();

        Coroutine flash = StartCoroutine(AnimFlash());
        Coroutine glow = StartCoroutine(AnimGlow());
        Coroutine rays = StartCoroutine(AnimRays());
        Coroutine ring = StartCoroutine(AnimRing());
        Coroutine radius = OnRadiusPx != null ? StartCoroutine(CoReportRadius()) : null;

        yield return new WaitForSeconds(totalDuration);

        if (radius != null) StopCoroutine(radius);
        OnRadiusPx?.Invoke(float.MaxValue);   // kalan halkalar serbest

        if (isShuttingDown)
            yield break;

        if (flash != null) StopCoroutine(flash);
        if (glow != null) StopCoroutine(glow);
        if (rays != null) StopCoroutine(rays);
        if (ring != null) StopCoroutine(ring);

        // Prosedürel texture temizliği
        if (noisyGlowTexture != null)
        {
            Destroy(noisyGlowTexture);
            noisyGlowTexture = null;
        }
        if (proceduralRaysTexture != null)
        {
            Destroy(proceduralRaysTexture);
            proceduralRaysTexture = null;
        }
    }

    private void ApplySunburstSprite()
    {
        if (sunburstRays == null) return;

        int size = Mathf.Max(64, rayTexSize);
        proceduralRaysTexture = GenerateSunburstTexture(size);

        var sprite = Sprite.Create(
            proceduralRaysTexture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f);

        sunburstRays.sprite = sprite;
        sunburstRays.type = Image.Type.Simple;
        sunburstRays.preserveAspect = false;
    }

    /// <summary>
    /// Merkezden dışa uzanan ince ışınlardan oluşan sunburst texture üretir.
    /// rayThinness çizgileri inceltir, raySoftness kenarları yumuşatır (baked blur),
    /// rayReach ışınların ne kadar dışa gittiğini (en dıştaki katman) belirler.
    /// </summary>
    private Texture2D GenerateSunburstTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float center = size * 0.5f;
        float maxRadius = center;
        float seed = Random.Range(0f, 1000f);

        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / maxRadius; // 0..1+
                float angle = Mathf.Atan2(dy, dx);

                // Işınları düzensizleştir: her yön için 3 bağımsız Perlin gürültüsü —
                // uzunluk, parlaklık ve kalınlık ışın-ışın değişir (muntazamlığı kırar).
                float ca = Mathf.Cos(angle) * rayIrregFreq;
                float sa = Mathf.Sin(angle) * rayIrregFreq;
                float vLen = Mathf.PerlinNoise(ca + seed, sa + seed);
                float vInt = Mathf.PerlinNoise(ca + seed + 37.2f, sa + seed + 37.2f);
                float vThk = Mathf.PerlinNoise(ca + seed + 91.7f, sa + seed + 91.7f);

                // Kalınlık varyasyonu — bazı ışınlar daha kalın/ince
                float thin = Mathf.Clamp01(rayThinness + (vThk - 0.5f) * rayIrregularity * 0.4f);
                float edge0 = Mathf.Clamp01(thin - raySoftness);
                float edge1 = Mathf.Clamp01(thin + raySoftness);
                if (edge1 <= edge0) edge1 = edge0 + 0.001f;

                // Açısal ışın deseni: kosinüs tepe noktaları = ışın merkezleri
                float a = 0.5f * (Mathf.Cos(angle * rayCount) + 1f); // 0..1
                float t = Mathf.Clamp01((a - edge0) / (edge1 - edge0));
                float rayMask = t * t * (3f - 2f * t);

                // Uzunluk varyasyonu — ışınlar farklı mesafelere ulaşır
                float reach = rayReach * Mathf.Lerp(1f - rayIrregularity * 0.55f, 1f, vLen);
                float inFade = Mathf.Clamp01(r / 0.12f);
                float outFade = 1f - Mathf.Clamp01((r - (reach - 0.18f)) / 0.18f);
                float radial = inFade * Mathf.Clamp01(outFade);
                radial = radial * radial * (3f - 2f * radial); // smoothstep

                // Parlaklık varyasyonu — bazı ışınlar daha sönük
                float intensity = Mathf.Lerp(1f - rayIrregularity * 0.6f, 1f, vInt);

                float alpha = rayMask * radial * intensity;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void ApplyNoisyRingSprite()
    {
        if (shockwaveRing == null) return;

        int size = Mathf.Max(32, noiseTexSize);
        float seed = Random.Range(0f, 1000f);

        noisyGlowTexture = GenerateNoisyRingTexture(size, seed);

        var sprite = Sprite.Create(
            noisyGlowTexture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f);

        shockwaveRing.sprite = sprite;
        shockwaveRing.type = Image.Type.Simple;
        shockwaveRing.preserveAspect = false;
    }

    private IEnumerator AnimFlash()
    {
        if (coreFlash == null)
            yield break;

        float peak = baseSize * flashPeakSizeRatio;
        float start = Mathf.Min(StartSizePx(), peak);

        yield return AnimateLayer(coreFlash, flashColor, start, peak, 0f, flashColor.a, flashInTime, easeOut);
        yield return AnimateLayer(coreFlash, flashColor, peak, peak * flashEndRatio, flashColor.a, 0f, flashOutTime, easeIn);
    }

    private IEnumerator AnimGlow()
    {
        if (innerGlow == null)
            yield break;

        float peak = baseSize * glowPeakSizeRatio;
        float start = Mathf.Min(StartSizePx(), peak);

        yield return AnimateLayer(innerGlow, glowColor, start, peak, 0f, glowColor.a, glowInTime, easeOut);
        yield return AnimateLayer(innerGlow, glowColor, peak, peak * glowEndRatio, glowColor.a, 0f, glowOutTime, easeIn);
    }

    private IEnumerator AnimRays()
    {
        if (sunburstRays == null)
            yield break;

        float peak = baseSize * raysPeakSizeRatio;

        if (sunburstRays.rectTransform == null)
            yield break;

        sunburstRays.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        // Referans: ışınlar bloom oturduktan SONRA fışkırır. Gecikme boyunca gizli kalır
        // (HideAll ile alpha 0), sonra çekirdekten dışa keskin/hızlı açılır.
        if (rayStartDelay > 0f)
            yield return new WaitForSeconds(rayStartDelay);

        if (isShuttingDown || sunburstRays == null)
            yield break;

        float start = Mathf.Min(StartSizePx(), peak);

        yield return AnimateLayerWithRotation(sunburstRays, raysColor, start, peak, 0f, raysColor.a, raysInTime, easeOut);
        yield return AnimateLayerWithRotation(sunburstRays, raysColor, peak, peak * raysEndRatio, raysColor.a, 0f, raysOutTime, easeOut);
    }

    private IEnumerator AnimRing()
    {
        if (shockwaveRing == null)
        {
            ringAnimStarted = true;   // ring yoksa reporter beklemede kalmasın
            yield break;
        }

        float peak = baseSize * ringPeakSizeRatio;
        float start = Mathf.Min(StartSizePx(), peak);
        ringAnimStarted = true;

        yield return AnimateLayer(shockwaveRing, ringColor, start, peak, 0f, ringColor.a, ringInTime, easeOut);
        yield return AnimateLayer(shockwaveRing, ringColor, peak, peak * ringEndRatio, ringColor.a, 0f, ringOutTime, easeOut);
    }

    private IEnumerator AnimateLayer(
        Image img,
        Color baseCol,
        float fromSize,
        float toSize,
        float fromA,
        float toA,
        float duration,
        AnimationCurve curve)
    {
        if (img == null || duration <= 0f)
            yield break;

        RectTransform rt = img.rectTransform;
        if (rt == null)
            yield break;

        rt.localScale = Vector3.one;

        float t = 0f;

        while (t < duration)
        {
            if (isShuttingDown || img == null || rt == null)
                yield break;

            t += Time.deltaTime;

            float u = Mathf.Clamp01(t / duration);
            float eased = curve.Evaluate(u);
            float size = Mathf.Lerp(fromSize, toSize, eased);

            rt.sizeDelta = new Vector2(size, size);
            img.color = WithAlpha(baseCol, Mathf.Lerp(fromA, toA, eased));

            yield return null;
        }

        if (isShuttingDown || img == null || rt == null)
            yield break;

        rt.sizeDelta = new Vector2(toSize, toSize);
        img.color = WithAlpha(baseCol, toA);
    }

    private IEnumerator AnimateLayerWithRotation(
        Image img,
        Color baseCol,
        float fromSize,
        float toSize,
        float fromA,
        float toA,
        float duration,
        AnimationCurve curve)
    {
        if (img == null || duration <= 0f)
            yield break;

        RectTransform rt = img.rectTransform;
        if (rt == null)
            yield break;

        rt.localScale = Vector3.one;

        float t = 0f;

        while (t < duration)
        {
            if (isShuttingDown || img == null || rt == null)
                yield break;

            t += Time.deltaTime;

            float u = Mathf.Clamp01(t / duration);
            float eased = curve.Evaluate(u);
            float size = Mathf.Lerp(fromSize, toSize, eased);

            rt.sizeDelta = new Vector2(size, size);
            img.color = WithAlpha(baseCol, Mathf.Lerp(fromA, toA, eased));
            rt.localRotation *= Quaternion.Euler(0, 0, raysRotateSpeed * Time.deltaTime);

            yield return null;
        }

        if (isShuttingDown || img == null || rt == null)
            yield break;

        rt.sizeDelta = new Vector2(toSize, toSize);
        img.color = WithAlpha(baseCol, toA);
    }

    private static Color WithAlpha(Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  NOISY RING — prosedürel düzensiz kenarlı halka (simit) texture
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Simit (halka) şeklinde texture üretir.
    /// Dış ve iç kenar Perlin noise ile düzensizleştirilir.
    /// </summary>
    private Texture2D GenerateNoisyRingTexture(int size, float seed)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float center = size * 0.5f;
        float maxRadius = center;

        // Halka parametreleri (normalize: 0..1 aralığında)
        float outerR = 1.0f;
        float innerR = Mathf.Max(0.1f, outerR - ringThickness);
        float soft = Mathf.Max(0.02f, edgeSoftness);

        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxRadius; // 0..1+

                float angle = Mathf.Atan2(dy, dx);

                // Dış kenar noise
                float outerNX = Mathf.Cos(angle * noiseFrequency) * 1.5f + seed;
                float outerNY = Mathf.Sin(angle * noiseFrequency) * 1.5f + seed;
                float outerNoise = Mathf.PerlinNoise(outerNX, outerNY); // 0..1
                float noisyOuter = outerR + (outerNoise - 0.5f) * noiseAmplitude * 2f;
                noisyOuter = Mathf.Clamp(noisyOuter, outerR - noiseAmplitude, outerR + noiseAmplitude * 0.3f);

                // İç kenar noise (farklı seed offset ile farklı pattern)
                float innerNX = Mathf.Cos(angle * noiseFrequency * 0.8f) * 1.5f + seed + 500f;
                float innerNY = Mathf.Sin(angle * noiseFrequency * 0.8f) * 1.5f + seed + 500f;
                float innerNoise = Mathf.PerlinNoise(innerNX, innerNY);
                float noisyInner = innerR + (innerNoise - 0.5f) * noiseAmplitude * 1.2f;
                noisyInner = Mathf.Clamp(noisyInner, 0.05f, noisyOuter - 0.05f);

                // Alpha hesapla
                float alpha = 0f;

                if (dist >= noisyInner && dist <= noisyOuter)
                {
                    // Dış kenara doğru fade out
                    float outerFade = Mathf.Clamp01((noisyOuter - dist) / (soft * 0.5f));
                    // İç kenara doğru fade in
                    float innerFade = Mathf.Clamp01((dist - noisyInner) / (soft * 0.5f));

                    alpha = Mathf.Min(outerFade, innerFade);
                    // Smoothstep
                    alpha = alpha * alpha * (3f - 2f * alpha);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
// ═════════════════════════════════════════════════════════════════════════════
//  FIREWORK COLOR PALETTE
//  4 aile (mavi / kırmızı / sarı / yeşil) × 3 ton = 12 varyant
//  Her varyant 4 katman için RGB üretir. Alpha değerleri prefab'dan korunur.
// ═════════════════════════════════════════════════════════════════════════════
public static class PulseFireworkPalette
{
    public struct Variant
    {
        public Color flash;  // merkez — en parlak
        public Color glow;   // yumuşak halka
        public Color rays;   // ışın accent
        public Color ring;   // shockwave
    }

    private static readonly Variant[] Variants =
    {
        // ─── 🔵 MAVİ AİLESİ ──────────────────────────────────────────────
        // Klasik mavi — kraliyet mavisi → açık sky
        new Variant
        {
            flash = new Color(0.85f, 0.95f, 1.00f),  // buzlu beyaz
            glow  = new Color(0.30f, 0.55f, 1.00f),  // kraliyet mavisi
            rays  = new Color(0.55f, 0.80f, 1.00f),  // açık sky
            ring  = new Color(0.20f, 0.45f, 1.00f),  // doygun mavi
        },
        // Açık mavi / cyan — buzul havai fişek
        new Variant
        {
            flash = new Color(0.92f, 1.00f, 1.00f),
            glow  = new Color(0.50f, 0.85f, 1.00f),
            rays  = new Color(0.70f, 0.95f, 1.00f),
            ring  = new Color(0.35f, 0.75f, 1.00f),
        },
        // Turkuaz / aqua
        new Variant
        {
            flash = new Color(0.85f, 1.00f, 0.98f),
            glow  = new Color(0.20f, 0.80f, 0.75f),
            rays  = new Color(0.45f, 0.95f, 0.85f),
            ring  = new Color(0.10f, 0.70f, 0.70f),
        },

        // ─── 🔴 KIRMIZI AİLESİ ───────────────────────────────────────────
        // Saf kırmızı
        new Variant
        {
            flash = new Color(1.00f, 0.90f, 0.90f),
            glow  = new Color(1.00f, 0.25f, 0.20f),
            rays  = new Color(1.00f, 0.50f, 0.40f),
            ring  = new Color(0.95f, 0.15f, 0.15f),
        },
        // Kızıl-turuncu
        new Variant
        {
            flash = new Color(1.00f, 0.95f, 0.85f),
            glow  = new Color(1.00f, 0.45f, 0.15f),
            rays  = new Color(1.00f, 0.65f, 0.30f),
            ring  = new Color(1.00f, 0.35f, 0.10f),
        },
        // Fuşya / hot pink
        new Variant
        {
            flash = new Color(1.00f, 0.92f, 0.96f),
            glow  = new Color(1.00f, 0.30f, 0.65f),
            rays  = new Color(1.00f, 0.55f, 0.80f),
            ring  = new Color(0.95f, 0.20f, 0.60f),
        },

        // ─── 🟡 SARI AİLESİ ──────────────────────────────────────────────
        // Altın sarısı
        new Variant
        {
            flash = new Color(1.00f, 0.98f, 0.85f),
            glow  = new Color(1.00f, 0.75f, 0.15f),
            rays  = new Color(1.00f, 0.88f, 0.40f),
            ring  = new Color(1.00f, 0.70f, 0.10f),
        },
        // Limon sarısı — parlak
        new Variant
        {
            flash = new Color(1.00f, 1.00f, 0.90f),
            glow  = new Color(0.98f, 0.95f, 0.30f),
            rays  = new Color(1.00f, 1.00f, 0.55f),
            ring  = new Color(0.95f, 0.90f, 0.20f),
        },
        // Amber / sıcak altın
        new Variant
        {
            flash = new Color(1.00f, 0.94f, 0.80f),
            glow  = new Color(1.00f, 0.65f, 0.10f),
            rays  = new Color(1.00f, 0.82f, 0.35f),
            ring  = new Color(0.95f, 0.60f, 0.08f),
        },

        // ─── 🟢 YEŞİL AİLESİ ─────────────────────────────────────────────
        // Saf yeşil
        new Variant
        {
            flash = new Color(0.90f, 1.00f, 0.90f),
            glow  = new Color(0.20f, 0.85f, 0.30f),
            rays  = new Color(0.50f, 1.00f, 0.55f),
            ring  = new Color(0.10f, 0.75f, 0.25f),
        },
        // Lime / neon yeşil
        new Variant
        {
            flash = new Color(0.95f, 1.00f, 0.88f),
            glow  = new Color(0.60f, 0.95f, 0.20f),
            rays  = new Color(0.80f, 1.00f, 0.45f),
            ring  = new Color(0.50f, 0.90f, 0.15f),
        },
        // Zümrüt / koyu yeşil
        new Variant
        {
            flash = new Color(0.88f, 1.00f, 0.92f),
            glow  = new Color(0.10f, 0.65f, 0.40f),
            rays  = new Color(0.35f, 0.85f, 0.55f),
            ring  = new Color(0.05f, 0.55f, 0.35f),
        },
    };

    public static Variant PickRandomVariant()
    {
        int i = Random.Range(0, Variants.Length);
        return Variants[i];
    }
}