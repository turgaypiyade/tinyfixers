using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class PulseFuseSparkleView : MonoBehaviour
{
    [Header("Sprite")]
    [SerializeField] private Sprite sparkleSprite;

    [Header("Position")]
    // Fuse ucunun tile merkezine göre local offset (pixel). Inspector'dan fine-tune et.
    [SerializeField] private Vector2 fuseLocalOffset = new Vector2(5f, 28f);

    [Header("Emit")]
    [SerializeField] private float emitInterval = 0.02f; // Çok yoğun alev
    [SerializeField] private float sparkLifetime = 0.65f; // Çok daha uzun ömürlü
    [SerializeField] private float spreadRadius  = 12f; // Çok daha geniş yayılım

    [Header("Spark appearance")]
    [SerializeField] private float sparkSizeMin  = 12f; // Devasa kıvılcımlar
    [SerializeField] private float sparkSizeMax  = 22f;
    [SerializeField] private Color sparkColorA   = new Color(1.00f, 0.65f, 0.15f, 1f); // Daha turuncu/kızıl
    [SerializeField] private Color sparkColorB   = new Color(1.00f, 0.20f, 0.05f, 1f); // Koyu kırmızı

    [Header("Breath Idle (nefes)")]
    [Tooltip("Tile'ın Icon RectTransform'unu ata.")]
    [SerializeField] private RectTransform breathTarget;
    [SerializeField] private float breathIdleDelay   = 2f;
    [SerializeField] private float breathInDuration  = 0.55f;
    [SerializeField] private float breathOutDuration = 0.70f;
    [SerializeField] private float breathAmplitude   = 0.05f;  // 1.0 → 1.05 → 1.0
    [SerializeField] private float breathPauseMin    = 1.5f;
    [SerializeField] private float breathPauseMax    = 3.5f;

    [Header("Y-Ekseni Dönme (oluşumda, 6 kare flipbook)")]
    [Tooltip("Kürenin farklı açılardan görünümü — sırayla oynatılınca Y ekseninde döner (kağıt gibi düz durmaz).")]
    [SerializeField] private Sprite[] spinFrames;
    [Tooltip("Saniyede kaç kare (dönme hızı). Yüksek = hızlı döner.")]
    [Range(1f, 90f)]
    [SerializeField] private float spinFrameFps = 24f;
    [Tooltip("Oluşumda başlangıç ölçeği (orijinalin katı). İstenen: 0.75.")]
    [SerializeField] private float spinStartScale = 0.75f;
    [Tooltip("Oluşumda ulaşılan tepe ölçeği. İstenen: 2.5.")]
    [SerializeField] private float spinPeakScale = 2.5f;
    [Tooltip("0.75 → 2.5 büyüme süresi (sn).")]
    [SerializeField] private float spinGrowDuration = 0.22f;
    [Tooltip("2.5 → 1.0 (orijinal) küçülme süresi (sn).")]
    [SerializeField] private float spinShrinkDuration = 0.28f;
    [Tooltip("Açık: oluşumdan sonra da sürekli döner (orijinal boyutta). Kapalı: durur, statik ikon görünür.")]
    [SerializeField] private bool spinContinuous = false;

    [Header("Oluşum Halesi (normal match ring'i gibi)")]
    [Tooltip("Açık: oluşumda core'un arkasında yumuşak yuvarlak bir hale çıkar (match burst ring'iyle aynı sprite).")]
    [SerializeField] private bool spinHaloEnabled = true;
    [Tooltip("Hale, core ölçeğinin kaç katı olsun (core'u çevrelesin diye biraz büyük).")]
    [SerializeField] private float haloScaleFactor = 1.25f;
    [Tooltip("Hale rengi/opaklığı.")]
    [SerializeField] private Color haloColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("Spin/hale render sırası — tüm taş & obstacle'ların ÜSTÜNde çizilmesi için yüksek tut.")]
    [SerializeField] private int spinSortingOrder = 100;

    private RectTransform rt;
    private Coroutine emitRoutine;
    private Coroutine breathRoutine;
    private Coroutine spinRoutine;
    private Coroutine idleSpinRoutine;
    private Image spinImage;
    private Image haloImage;

    private void Awake()
    {
        emitInterval = 0.02f;
        sparkLifetime = 0.65f;
        spreadRadius = 12f;
        sparkSizeMin = 12f;
        sparkSizeMax = 22f;
        sparkColorA = new Color(1.00f, 0.65f, 0.15f, 1f);
        sparkColorB = new Color(1.00f, 0.20f, 0.05f, 1f);

        rt = GetComponent<RectTransform>();
        // Prefab'da aktif olsa bile başlangıçta kapat.
        // TileView.RefreshIcon yalnızca PulseCore için açar.
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        transform.SetAsLastSibling();
        Play();
        if (breathTarget != null)
            breathRoutine = StartCoroutine(CoBreath());
        idleSpinRoutine = StartCoroutine(CoIdleSpinWatch());
    }

    private void OnDisable() => StopAndClear();

    public void Play()
    {
        if (!isActiveAndEnabled) return;
        StopAndClear();
        emitRoutine = StartCoroutine(CoEmit());
    }

    public void Stop()
    {
        StopAndClear();
    }

    private void StopAndClear()
    {
        if (emitRoutine   != null) { StopCoroutine(emitRoutine);   emitRoutine   = null; }
        if (breathRoutine != null) { StopCoroutine(breathRoutine); breathRoutine = null; }
        if (spinRoutine   != null) { StopCoroutine(spinRoutine);   spinRoutine   = null; }
        if (idleSpinRoutine != null) { StopCoroutine(idleSpinRoutine); idleSpinRoutine = null; }
        if (externalFuseRoutine != null) { StopCoroutine(externalFuseRoutine); externalFuseRoutine = null; }
        emitIntensity = 1f;
        if (breathTarget  != null) breathTarget.localScale = Vector3.one;
        SetIconVisible(true);   // spin yarıda kesildiyse statik ikon gizli kalmasın
        spinImage = null;   // aşağıdaki child yıkımıyla yok olacak; referansı temizle
        haloImage = null;
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    // ── Y-ekseni flipbook dönme (PulseCore oluşumu) ─────────────────────────────
    // TileView.PlaySpecialCreationReveal → PulseCore için çağrılır. Kürenin 6 karesini sırayla
    // göstererek Y ekseninde dönme illüzyonu verir (düz/kağıt görünmesin diye).
    public void PlayCreationSpin(int tileSize)
    {
        if (!isActiveAndEnabled || !HasSpinFrames()) return;
        if (spinRoutine != null) { StopCoroutine(spinRoutine); spinRoutine = null; }
        if (spinHaloEnabled) EnsureHalo(tileSize);   // ÖNCE hale (arkada)
        EnsureSpinImage(tileSize);                   // SONRA core (önde)
        spinRoutine = StartCoroutine(CoSpin());
    }

    /// <summary>Idle sırasında LineV/LineH ile birebir aynı ölçekte (1.12x) hafif pop ile küre dönüşü yapar.</summary>
    public void PlayIdleSpin(int tileSize, float peakScale = 1.12f, float duration = 0.38f)
    {
        if (!isActiveAndEnabled || !HasSpinFrames()) return;
        if (spinRoutine != null) { StopCoroutine(spinRoutine); spinRoutine = null; }
        EnsureSpinImage(tileSize);
        spinRoutine = StartCoroutine(CoIdleSpin(peakScale, duration));
    }

    private IEnumerator CoIdleSpin(float peakScale, float duration)
    {
        int frameCount = spinFrames.Length;
        float frameTime = 1f / Mathf.Max(1f, spinFrameFps);
        float frameAcc = 0f;
        int idx = 0;
        SetFrame(idx);

        SetIconVisible(false);

        if (spinImage != null)
            StartExternalFuse(spinImage.rectTransform, 1f, emitIntensity, duration + 0.05f, this);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float k = Mathf.Clamp01(elapsed / duration);

            // LineV/H ile BİREBİR AYNI büyüme oranı (1.0 -> 1.12 -> 1.0)
            float cs = 1f + (peakScale - 1f) * Mathf.Sin(k * Mathf.PI);
            if (spinImage != null)
                spinImage.rectTransform.localScale = Vector3.one * cs;

            AdvanceFrame(ref frameAcc, ref idx, frameCount, frameTime, dt);
            yield return null;
        }

        if (spinImage != null)
        {
            spinImage.rectTransform.localScale = Vector3.one;
            spinImage.gameObject.SetActive(false);
        }

        SetIconVisible(true);
        spinRoutine = null;
    }

    private void EnsureHalo(int tileSize)
    {
        if (haloImage == null)
        {
            var go = new GameObject("_CoreHalo",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            var irt = go.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.localScale = Vector3.one;

            haloImage = go.GetComponent<Image>();
            haloImage.raycastTarget  = false;
            haloImage.preserveAspect = true;
            haloImage.sprite = TileClearBurstVfx.SoftCircleHaloSprite;   // match ring'iyle aynı
            ConfigureTopOverlay(go, spinSortingOrder);                   // tüm taşların üstünde
        }

        haloImage.rectTransform.sizeDelta = Vector2.one * tileSize;
        haloImage.transform.SetAsLastSibling();   // core'dan önce çağrıldığı için arkada kalır
        haloImage.gameObject.SetActive(true);
        var c = haloColor; c.a = 0f; haloImage.color = c;   // baştan görünmez, CoSpin fade eder
    }

    private bool HasSpinFrames()
    {
        if (spinFrames == null) return false;
        for (int i = 0; i < spinFrames.Length; i++)
            if (spinFrames[i] != null) return true;
        return false;
    }

    private void EnsureSpinImage(int tileSize)
    {
        if (spinImage == null)
        {
            var go = new GameObject("_CoreSpin",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            var irt = go.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.localScale = Vector3.one;

            spinImage = go.GetComponent<Image>();
            spinImage.raycastTarget  = false;
            spinImage.preserveAspect = true;
            ConfigureTopOverlay(go, spinSortingOrder + 1);   // hale'nin de üstünde (core en önde)
        }

        spinImage.rectTransform.sizeDelta = Vector2.one * tileSize;
        spinImage.rectTransform.localScale = Vector3.one * spinStartScale;
        spinImage.transform.SetAsLastSibling();
        spinImage.gameObject.SetActive(true);
        spinImage.color = Color.white;
    }

    private IEnumerator CoSpin()
    {
        int frameCount = spinFrames.Length;
        float frameTime = 1f / Mathf.Max(1f, spinFrameFps);
        float frameAcc = 0f;
        int idx = 0;
        SetFrame(idx);

        // Oluşum boyunca statik PulseCore ikonu gizlenir (büyüyen dönen küreyle çakışmasın).
        SetIconVisible(false);
        
        // Spin sürerken dönen küre (spinImage) üzerinde alev çıkması için external fuse başlat:
        // spinImage üstte çizildiği için alev de üstte çizilir ve görünür olur.
        if (spinImage != null)
            StartExternalFuse(spinImage.rectTransform, 1f, emitIntensity, spinGrowDuration + spinShrinkDuration + 0.1f, this);

        // ── Faz 1: Büyüme 0.75 → 2.5 (ease-out) — bu sırada sürekli hızlı dönme ──
        float t = 0f;
        while (t < spinGrowDuration)
        {
            float dt = Time.deltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, spinGrowDuration));
            float e = 1f - (1f - k) * (1f - k);
            float cs = Mathf.Lerp(spinStartScale, spinPeakScale, e);
            if (spinImage != null)
                spinImage.rectTransform.localScale = Vector3.one * cs;
            SetHalo(cs, Mathf.Clamp01(k * 2f) * haloColor.a);   // hızlı fade-in, core'u çevreler
            AdvanceFrame(ref frameAcc, ref idx, frameCount, frameTime, dt);
            yield return null;
        }

        // ── Faz 2: Küçülme 2.5 → 1.0 (orijinal, smoothstep) — dönme devam ──
        t = 0f;
        while (t < spinShrinkDuration)
        {
            float dt = Time.deltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, spinShrinkDuration));
            float e = k * k * (3f - 2f * k);
            float cs = Mathf.Lerp(spinPeakScale, 1f, e);
            if (spinImage != null)
                spinImage.rectTransform.localScale = Vector3.one * cs;
            SetHalo(cs, (1f - k) * haloColor.a);   // küçülürken sönerek kaybol
            AdvanceFrame(ref frameAcc, ref idx, frameCount, frameTime, dt);
            yield return null;
        }

        if (spinImage != null)
            spinImage.rectTransform.localScale = Vector3.one;

        HideHalo();

        // ── Sürekli dönme (opsiyonel): orijinal boyutta dönmeye devam, ikon gizli kalır ──
        if (spinContinuous)
        {
            while (true)
            {
                AdvanceFrame(ref frameAcc, ref idx, frameCount, frameTime, Time.deltaTime);
                yield return null;
            }
        }

        // Creation-only: overlay'i gizle, statik PulseCore ikonunu geri getir.
        if (spinImage != null)
            spinImage.gameObject.SetActive(false);
        SetIconVisible(true);
        spinRoutine = null;
    }

    // Flipbook'u dt kadar ilerlet (hız spinFrameFps'e bağlı, ölçek fazından bağımsız).
    private void AdvanceFrame(ref float acc, ref int idx, int frameCount, float frameTime, float dt)
    {
        acc += dt;
        while (acc >= frameTime)
        {
            acc -= frameTime;
            idx = (idx + 1) % Mathf.Max(1, frameCount);
            SetFrame(idx);
        }
    }

    private void SetFrame(int idx)
    {
        if (spinImage == null || spinFrames == null || spinFrames.Length == 0) return;
        var sp = spinFrames[Mathf.Clamp(idx, 0, spinFrames.Length - 1)];
        if (sp != null) spinImage.sprite = sp;
    }

    private void SetHalo(float coreScale, float alpha)
    {
        if (haloImage == null) return;
        haloImage.rectTransform.localScale = Vector3.one * (coreScale * Mathf.Max(0.01f, haloScaleFactor));
        var c = haloColor;
        c.a = Mathf.Clamp01(alpha);
        haloImage.color = c;
    }

    private void HideHalo()
    {
        if (haloImage != null)
            haloImage.gameObject.SetActive(false);
    }

    // Overlay'i tüm taş/obstacle'ların ÜSTÜNDE çizdir: kendi Canvas'ı (overrideSorting) + yüksek
    // sortingOrder. Taşlar sibling-index ile sıralandığı için tile'ın çocuğu olan overlay normalde
    // sonraki tile'ların ALTINDA kalıyordu. Ayrıca layer'ı bu obje ile aynı yap (dinamik UI layer
    // 0'da doğar → Screen Space Camera culling'i; project_board_vfx_rectmask_clip).
    private void ConfigureTopOverlay(GameObject go, int order)
    {
        go.layer = gameObject.layer;
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
    }

    // Statik PulseCore ikonunu (breathTarget üstündeki Image) gizle/göster.
    private void SetIconVisible(bool visible)
    {
        if (breathTarget == null) return;
        var img = breathTarget.GetComponent<Image>();
        if (img == null) return;
        var c = img.color;
        c.a = visible ? 1f : 0f;
        img.color = c;
    }

    // Fitil yoğunluğu (combolarda "daha yoğun yansın" için). 1 = normal idle. Büyük = daha çok/
    // hızlı kıvılcım + biraz daha büyük alev.
    private float emitIntensity = 1f;

    /// <summary>Fitil yanma yoğunluğunu ayarlar (1 = normal). Combolarda pulse "şarj oluyormuş"
    /// gibi daha yoğun yakmak için kullanılır.</summary>
    public void SetFuseIntensity(float multiplier)
    {
        emitIntensity = Mathf.Clamp(multiplier, 0.25f, 6f);
    }

    private IEnumerator CoEmit()
    {
        while (true)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(emitIntensity));
            for (int i = 0; i < count; i++)
                SpawnSpark();
            yield return new WaitForSeconds(emitInterval / Mathf.Max(0.5f, emitIntensity));
        }
    }

    private void SpawnSpark() => SpawnSparkAt(transform as RectTransform, fuseLocalOffset, 1f, emitIntensity);

    // Fitil kıvılcımını verilen parent'a, ölçekli offset/boyutla basar. Idle örneği (fuseLocalOffset,
    // tileSize) baz alınır; combo pulse görselinde sizeScale = comboIkonBoyu / TileSize ile aynı
    // ORAN korunur → doğru koordinat kendiliğinden gelir.
    private void SpawnSparkAt(RectTransform parent, Vector2 baseOffset, float sizeScale, float intensity, MonoBehaviour coroutineOwner = null)
    {
        if (sparkleSprite == null || parent == null) 
            return;

        var sparkGO = new GameObject("_Spark");
        sparkGO.transform.SetParent(parent, false);
        sparkGO.transform.SetAsLastSibling();

        var sparkRt = sparkGO.AddComponent<RectTransform>();
        // Yoğunlukta kıvılcım biraz büyür + biraz daha yayılır → dolgun alev.
        float intensityBoost = Mathf.Lerp(1f, 1.35f, Mathf.Clamp01(intensity - 1f));
        float size = Random.Range(sparkSizeMin, sparkSizeMax) * intensityBoost * sizeScale;
        sparkRt.sizeDelta = Vector2.one * size;
        sparkRt.anchoredPosition = baseOffset + Random.insideUnitCircle * spreadRadius * intensityBoost * sizeScale;
        sparkRt.localScale = Vector3.one;

        var img = sparkGO.AddComponent<Image>();
        img.sprite = sparkleSprite;
        img.color = Color.Lerp(sparkColorA, sparkColorB, Random.value);
        img.raycastTarget = false;

        (coroutineOwner != null ? coroutineOwner : this).StartCoroutine(CoAnimateSpark(sparkRt, img, size));
    }

    private Coroutine externalFuseRoutine;

    /// <summary>Combo görselleri için: verilen parent'a (combo pulse Image'ı) idle fitille AYNI
    /// kıvılcımları basar. sizeScale = comboPulseBoyu / TileSize (doğru koordinat için ölçek).
    /// duration &lt;= 0 → parent yok olana kadar. coroutineOwner verilirse tile silinse de combo
    /// görseli kendi parent'ı yaşadığı sürece yanmaya devam eder.</summary>
    public void StartExternalFuse(RectTransform target, float sizeScale, float intensity, float duration, MonoBehaviour coroutineOwner = null)
    {
        if (target == null) return;
        var owner = coroutineOwner != null ? coroutineOwner : this;
        if (owner == this && externalFuseRoutine != null) StopCoroutine(externalFuseRoutine);
        var routine = owner.StartCoroutine(CoExternalFuse(target, Mathf.Max(0.1f, sizeScale),
            Mathf.Clamp(intensity, 0.25f, 6f), duration, owner));
        if (owner == this)
            externalFuseRoutine = routine;
    }

    private IEnumerator CoExternalFuse(RectTransform target, float sizeScale, float intensity, float duration, MonoBehaviour coroutineOwner)
    {
        Vector2 offset = fuseLocalOffset * sizeScale;
        float elapsed = 0f;
        while (target != null && (duration <= 0f || elapsed < duration))
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(intensity));
            for (int i = 0; i < count; i++)
                SpawnSparkAt(target, offset, sizeScale, intensity, coroutineOwner);
            float wait = emitInterval / Mathf.Max(0.5f, intensity);
            elapsed += wait;
            yield return new WaitForSeconds(wait);
        }
        if (coroutineOwner == this)
            externalFuseRoutine = null;
    }

    private IEnumerator CoAnimateSpark(RectTransform sparkRt, Image img, float baseSize)
    {
        if (sparkRt == null || img == null) yield break;

        Vector2 startPos = sparkRt.anchoredPosition;
        Vector2 drift    = Random.insideUnitCircle.normalized * Random.Range(6f, 14f);
        float   elapsed  = 0f;

        while (elapsed < sparkLifetime)
        {
            if (sparkRt == null) yield break;

            elapsed += Time.deltaTime;
            float k = elapsed / sparkLifetime;

            sparkRt.anchoredPosition = startPos + drift * k;

            // Fade: hızlı fade-in, yavaş fade-out
            float alpha = k < 0.25f
                ? k / 0.25f
                : 1f - (k - 0.25f) / 0.75f;

            var c = img.color;
            c.a = alpha;
            img.color = c;

            // Scale: orta noktada en büyük
            float scale = Mathf.Sin(k * Mathf.PI);
            sparkRt.sizeDelta = Vector2.one * (baseSize * Mathf.Lerp(0.4f, 1f, scale));

            yield return null;
        }

        if (sparkRt != null)
            Destroy(sparkRt.gameObject);
    }

    // ── Breath idle ────────────────────────────────────────────────────────────

    private IEnumerator CoBreath()
    {
        yield return new WaitForSeconds(breathIdleDelay);

        while (true)
        {
            // Nefes al: 1.0 → 1.0 + amplitude
            float t = 0f;
            while (t < breathInDuration)
            {
                t += Time.deltaTime;
                float k = t / breathInDuration;
                float e = 1f - (1f - k) * (1f - k); // ease-out
                breathTarget.localScale = Vector3.one * (1f + breathAmplitude * e);
                yield return null;
            }

            // Nefes ver: 1.0 + amplitude → 1.0
            t = 0f;
            while (t < breathOutDuration)
            {
                t += Time.deltaTime;
                float k = t / breathOutDuration;
                float e = k * k * (3f - 2f * k); // smoothstep
                breathTarget.localScale = Vector3.one * (1f + breathAmplitude * (1f - e));
                yield return null;
            }

            breathTarget.localScale = Vector3.one;
            yield return new WaitForSeconds(Random.Range(breathPauseMin, breathPauseMax));
        }
    }

    // ── 3sn Periyodik Idle Spin ────────────────────────────────────────────────
    private IEnumerator CoIdleSpinWatch()
    {
        yield return new WaitForSeconds(3.0f);

        while (true)
        {
            if (spinRoutine == null && isActiveAndEnabled && HasSpinFrames())
            {
                var tile = GetComponentInParent<TileView>();
                bool isTileBusy = tile != null && (tile.WasDragging || (tile.Board != null && (tile.Board.IsBusy || tile.Board.InputLocked)));
                if (!isTileBusy)
                {
                    int s = tile != null ? tile.LastAppliedTileSize : 96;
                    PlayIdleSpin(s, 1.12f, 0.38f);
                }
            }

            yield return new WaitForSeconds(3.0f);
        }
    }
}
