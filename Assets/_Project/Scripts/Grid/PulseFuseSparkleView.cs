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
    [SerializeField] private float emitInterval = 0.10f;
    [SerializeField] private float sparkLifetime = 0.40f;
    [SerializeField] private float spreadRadius  = 5f;

    [Header("Spark appearance")]
    [SerializeField] private float sparkSizeMin  = 5f;
    [SerializeField] private float sparkSizeMax  = 11f;
    [SerializeField] private Color sparkColorA   = new Color(1.00f, 0.95f, 0.40f, 1f);
    [SerializeField] private Color sparkColorB   = new Color(1.00f, 0.60f, 0.10f, 1f);

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
    private Image spinImage;
    private Image haloImage;

    private void Awake()
    {
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

    private IEnumerator CoEmit()
    {
        while (true)
        {
            SpawnSpark();
            yield return new WaitForSeconds(emitInterval);
        }
    }

    private void SpawnSpark()
    {
        if (sparkleSprite == null) return;

        var sparkGO = new GameObject("_Spark");
        sparkGO.transform.SetParent(transform, false);
        sparkGO.transform.SetAsLastSibling();

        var sparkRt = sparkGO.AddComponent<RectTransform>();
        float size = Random.Range(sparkSizeMin, sparkSizeMax);
        sparkRt.sizeDelta = Vector2.one * size;
        sparkRt.anchoredPosition = fuseLocalOffset + Random.insideUnitCircle * spreadRadius;
        sparkRt.localScale = Vector3.one;

        var img = sparkGO.AddComponent<Image>();
        img.sprite = sparkleSprite;
        img.color = Color.Lerp(sparkColorA, sparkColorB, Random.value);
        img.raycastTarget = false;

        StartCoroutine(CoAnimateSpark(sparkRt, img, size));
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
}
