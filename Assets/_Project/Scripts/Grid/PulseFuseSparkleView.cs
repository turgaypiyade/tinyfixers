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

    private RectTransform rt;
    private Coroutine emitRoutine;
    private Coroutine breathRoutine;

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
        if (breathTarget  != null) breathTarget.localScale = Vector3.one;
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
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
