using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pulse+Pulse combo patlama VFX'i.
/// Mevcut Animator animasyonunun üstüne prosedürel efektler ekler:
///   - Ring wobble (halkaya düzensizlik)
///   - Fill burst (halkanın içini dolduran parçacıklar)
///   - Inner glow (merkez parlama genişlemesi)
/// </summary>
public class PulsePulseExplosionVfx : MonoBehaviour
{
    [Header("Existing")]
    [SerializeField] private ParticleSystem streaks;

    [Header("Ring References (opsiyonel — otomatik bulur)")]
    [SerializeField] private RectTransform ring;
    [SerializeField] private RectTransform ring2;
    [SerializeField] private Image flashImage;

    // ── Ring Wobble ──
    [Header("Ring Wobble")]
    [Tooltip("Halkaya random scale düzensizliği ekle")]
    [SerializeField] private bool enableRingWobble = true;
    [SerializeField] private float wobbleSpeed = 25f;
    [SerializeField] private float wobbleAmount = 0.08f;

    // ── Fill Burst ──
    [Header("Fill Burst (halkanın içini dolduran parçacıklar)")]
    [SerializeField] private bool enableFillBurst = true;
    [SerializeField] private Sprite fillParticleSprite;
    [SerializeField] private int fillParticleCount = 16;
    [SerializeField] private float fillRadius = 80f;
    [SerializeField] private float fillDuration = 0.30f;
    [SerializeField] private float fillParticleSize = 20f;
    [SerializeField] private Color fillColorInner = new Color(1f, 1f, 0.85f, 0.9f);
    [SerializeField] private Color fillColorOuter = new Color(1f, 0.65f, 0.15f, 0.7f);

    // ── Secondary Burst (dışarı fırlayan parçalar) ──
    [Header("Secondary Burst")]
    [SerializeField] private bool enableSecondaryBurst = true;
    [SerializeField] private Sprite burstParticleSprite;
    [SerializeField] private int burstParticleCount = 10;
    [SerializeField] private float burstDistance = 160f;
    [SerializeField] private float burstDuration = 0.35f;
    [SerializeField] private float burstParticleSize = 14f;
    [SerializeField] private Color burstColor = new Color(1f, 0.8f, 0.3f, 0.85f);

    // ── Inner Glow ──
    [Header("Inner Glow")]
    [SerializeField] private bool enableInnerGlow = true;
    [SerializeField] private float glowMaxScale = 1.8f;
    [SerializeField] private float glowDuration = 0.25f;

    // Runtime
    private bool isPlaying;
    private float playTime;
    private Vector3 ring1BaseScale;
    private Vector3 ring2BaseScale;
    private float wobbleSeed;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        if (ring == null)
        {
            var t = transform.Find("Ring");
            if (t) ring = t as RectTransform;
        }

        if (ring2 == null)
        {
            var t = transform.Find("Ring2");
            if (t) ring2 = t as RectTransform;
        }

        if (flashImage == null)
        {
            var t = transform.Find("Flash");
            if (t) flashImage = t.GetComponent<Image>();
        }
    }

    public void PlayStreaks()
    {
        if (streaks != null)
            streaks.Play();

        // Prosedürel efektleri başlat
        isPlaying = true;
        playTime = 0f;
        wobbleSeed = Random.Range(0f, 100f);

        if (ring) ring1BaseScale = ring.localScale;
        if (ring2) ring2BaseScale = ring2.localScale;

        if (enableFillBurst)
            StartCoroutine(CoFillBurst());

        if (enableSecondaryBurst)
            StartCoroutine(CoSecondaryBurst());

        if (enableInnerGlow && flashImage)
            StartCoroutine(CoInnerGlow());
    }

    private void Update()
    {
        if (!isPlaying) return;

        playTime += Time.unscaledDeltaTime;

        if (enableRingWobble)
            ApplyRingWobble();
    }

    // ────────────────────────────────────────────────
    // Ring Wobble — halkaya düzensiz titreşim
    // ────────────────────────────────────────────────
    private void ApplyRingWobble()
    {
        float t = playTime * wobbleSpeed;

        if (ring)
        {
            float wx = 1f + Mathf.PerlinNoise(t + wobbleSeed, 0f) * wobbleAmount * 2f - wobbleAmount;
            float wy = 1f + Mathf.PerlinNoise(0f, t + wobbleSeed + 17f) * wobbleAmount * 2f - wobbleAmount;

            ring.localScale = new Vector3(
                ring1BaseScale.x * wx,
                ring1BaseScale.y * wy,
                ring1BaseScale.z
            );
        }

        if (ring2)
        {
            float wx = 1f + Mathf.PerlinNoise(t + wobbleSeed + 50f, 30f) * wobbleAmount * 2f - wobbleAmount;
            float wy = 1f + Mathf.PerlinNoise(30f, t + wobbleSeed + 67f) * wobbleAmount * 2f - wobbleAmount;

            ring2.localScale = new Vector3(
                ring2BaseScale.x * wx,
                ring2BaseScale.y * wy,
                ring2BaseScale.z
            );
        }
    }

    // ────────────────────────────────────────────────
    // Fill Burst — halkanın içini dolduran parçacıklar
    // ────────────────────────────────────────────────
    private IEnumerator CoFillBurst()
    {
        var parent = transform as RectTransform;
        if (parent == null) yield break;

        var particles = new List<(RectTransform rt, Image img, Vector2 velocity, float startDelay)>();

        for (int i = 0; i < fillParticleCount; i++)
        {
            var go = new GameObject($"FillP_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Rastgele pozisyon — merkezden dağılımlı
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Random.Range(0f, fillRadius * 0.3f);
            rt.anchoredPosition = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);

            float size = fillParticleSize * Random.Range(0.6f, 1.4f);
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            if (fillParticleSprite) img.sprite = fillParticleSprite;

            // İç parçacıklar daha parlak, dıştakiler daha turuncu
            float normalizedDist = dist / Mathf.Max(0.01f, fillRadius * 0.3f);
            img.color = Color.Lerp(fillColorInner, fillColorOuter, normalizedDist);

            // Dışa doğru yavaş hareket
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(fillRadius * 0.8f, fillRadius * 1.5f);
            float delay = Random.Range(0f, 0.04f);

            particles.Add((rt, img, vel, delay));
        }

        float elapsed = 0f;
        while (elapsed < fillDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / fillDuration);

            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                if (p.rt == null) continue;

                // Delay
                float localU = Mathf.Clamp01((elapsed - p.startDelay) / (fillDuration - p.startDelay));
                if (localU <= 0f) continue;

                float eased = 1f - (1f - localU) * (1f - localU);

                // Hareket
                p.rt.anchoredPosition += p.velocity * Time.unscaledDeltaTime * (1f - eased * 0.5f);

                // Scale: büyü → küçül
                float scale = localU < 0.3f
                    ? Mathf.Lerp(0.2f, 1.2f, localU / 0.3f)
                    : Mathf.Lerp(1.2f, 0f, (localU - 0.3f) / 0.7f);
                p.rt.localScale = Vector3.one * Mathf.Max(0f, scale);

                // Alpha fade
                float alpha = 1f - (localU * localU);
                var c = p.img.color;
                c.a = alpha * fillColorInner.a;
                p.img.color = c;
            }

            yield return null;
        }

        // Temizlik
        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i].rt != null)
                Destroy(particles[i].rt.gameObject);
        }
    }

    // ────────────────────────────────────────────────
    // Secondary Burst — dışarı fırlayan düzensiz parçalar
    // ────────────────────────────────────────────────
    private IEnumerator CoSecondaryBurst()
    {
        var parent = transform as RectTransform;
        if (parent == null) yield break;

        // Küçük gecikme — ring açılmaya başladıktan sonra
        yield return new WaitForSecondsRealtime(0.04f);

        var particles = new List<(RectTransform rt, Image img, Vector2 dir, float speed, float rotSpeed)>();

        for (int i = 0; i < burstParticleCount; i++)
        {
            var go = new GameObject($"BurstP_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Düzensiz açı dağılımı — eşit aralık + random offset
            float baseAngle = (360f / burstParticleCount) * i;
            float angle = (baseAngle + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            rt.anchoredPosition = dir * Random.Range(8f, 20f);

            float size = burstParticleSize * Random.Range(0.5f, 1.5f);
            rt.sizeDelta = new Vector2(size, size * Random.Range(0.6f, 1.0f)); // Hafif yamuk
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            if (burstParticleSprite) img.sprite = burstParticleSprite;
            img.color = burstColor;

            float speed = Random.Range(burstDistance * 0.7f, burstDistance * 1.3f);
            float rotSpeed = Random.Range(-400f, 400f);

            particles.Add((rt, img, dir, speed, rotSpeed));
        }

        float elapsed = 0f;
        while (elapsed < burstDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / burstDuration);

            float eased = 1f - (1f - u) * (1f - u); // ease out

            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                if (p.rt == null) continue;

                // Pozisyon
                float dist = p.speed * eased;
                p.rt.anchoredPosition = p.dir * (10f + dist);

                // Rotation
                float rot = p.rotSpeed * elapsed;
                p.rt.localRotation = Quaternion.Euler(0f, 0f, rot);

                // Scale + alpha
                float scale = u < 0.2f
                    ? Mathf.Lerp(0.3f, 1.0f, u / 0.2f)
                    : Mathf.Lerp(1.0f, 0f, (u - 0.2f) / 0.8f);
                p.rt.localScale = Vector3.one * Mathf.Max(0f, scale);

                float alpha = 1f - (u * u * u);
                var c = p.img.color;
                c.a = alpha * burstColor.a;
                p.img.color = c;
            }

            yield return null;
        }

        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i].rt != null)
                Destroy(particles[i].rt.gameObject);
        }
    }

    // ────────────────────────────────────────────────
    // Inner Glow — flash'ın ekstra parlama genişlemesi
    // ────────────────────────────────────────────────
    private IEnumerator CoInnerGlow()
    {
        if (flashImage == null) yield break;

        var rt = flashImage.rectTransform;
        Vector3 baseScale = rt.localScale;

        float elapsed = 0f;
        while (elapsed < glowDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / glowDuration);

            // Hızlı genişle, yavaş sönsün
            float scale = u < 0.25f
                ? Mathf.Lerp(1f, glowMaxScale, u / 0.25f)
                : Mathf.Lerp(glowMaxScale, 1f, (u - 0.25f) / 0.75f);

            rt.localScale = baseScale * scale;

            yield return null;
        }

        rt.localScale = baseScale;
    }
}