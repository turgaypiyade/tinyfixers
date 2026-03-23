using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pulse+Pulse combo — SADECE charge animasyonu.
/// Glow + bomba sprite: şişer, titrer, nefes alır.
///
/// Patlama bu component'te YOK — BoardController charge bittikten sonra
/// mevcut PulseCoreImpactService.PlayPulseCoreExplosionVfxAtCell() ile
/// daha geniş alanda (5x5) patlatır.
///
/// Start()'ta otomatik başlar.
/// </summary>
public class PulsePulseExplosionVfx : MonoBehaviour
{
    [Header("Charge — Bomb")]
    [Tooltip("PulseCore bomba sprite'ı. TileIconLibrary'deki PulseCore.")]
    [SerializeField] private Sprite bombSprite;
    [SerializeField] private float bombBaseSize = 130f;
    [SerializeField] private float chargeDuration = 2.0f;

    [Header("Charge — Scale")]
    [SerializeField] private float chargeStartScale = 0.9f;
    [SerializeField] private float chargeMaxScale = 2.5f;
    [SerializeField] private float breathSpeed = 6f;
    [SerializeField] private float breathAmount = 0.15f;

    [Header("Charge — Shake")]
    [SerializeField] private float shakeStartNormalized = 0.25f;
    [SerializeField] private float shakeIntensityStart = 0.5f;
    [SerializeField] private float shakeIntensityEnd = 6f;
    [SerializeField] private float shakeSpeed = 40f;

    [Header("Charge — Glow")]
    [Tooltip("Glow sprite (Knob). Bombanın arkasında yumuşak parlama.")]
    [SerializeField] private Sprite glowSprite;
    [SerializeField] private float glowSizeMultiplier = 2.8f;
    [SerializeField] private Color glowColorStart = new Color(0.4f, 0.6f, 1f, 0f);
    [SerializeField] private Color glowColorPeak = new Color(1f, 0.95f, 0.7f, 0.8f);

    private Animator animator;

    public float ChargeDuration => chargeDuration;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator) animator.enabled = false;

        // Mevcut ring/flash child'larını gizle — kullanmıyoruz
        string[] names = { "Ring", "Ring2", "Flash" };
        foreach (var name in names)
        {
            var t = transform.Find(name);
            if (t) t.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        Debug.Log("[PulsePulseExplosionVfx] Start — charge begin");
        StartCoroutine(CoCharge());
    }

    public void PlayStreaks()
    {
        // Geriye uyumluluk — Start() zaten başlatıyor
    }

    // ════════════════════════════════════════════════
    //  CHARGE: Glow + Bomba — şişer, titrer
    // ════════════════════════════════════════════════
    private IEnumerator CoCharge()
    {
        var parent = transform as RectTransform;
        if (!parent) yield break;

        var container = CreateContainer(parent);

        // Glow (arkada)
        Image glowImg = null;
        if (glowSprite)
        {
            float glowSize = bombBaseSize * glowSizeMultiplier;
            glowImg = CreateUIImage("ChargeGlow", container, glowSprite, glowSize);
            glowImg.color = glowColorStart;
        }

        // Bomba (önde)
        Image bombImg = null;
        if (bombSprite)
        {
            bombImg = CreateUIImage("ChargeBomb", container, bombSprite, bombBaseSize);
            bombImg.preserveAspect = true;
            bombImg.color = Color.white;
        }

        float elapsed = 0f;
        float shakeSeed = Random.Range(0f, 100f);

        while (elapsed < chargeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / chargeDuration);

            // Büyüme: ease-in
            float grow = u * u;
            float baseScale = Mathf.Lerp(chargeStartScale, chargeMaxScale, grow);

            // Nefes
            float breathU = Mathf.Clamp01((u - 0.15f) / 0.85f);
            float curBreathSpeed = Mathf.Lerp(breathSpeed * 0.6f, breathSpeed * 2f, breathU);
            float curBreathAmt = Mathf.Lerp(breathAmount * 0.3f, breathAmount, breathU * breathU);
            float breath = Mathf.Sin(elapsed * curBreathSpeed) * curBreathAmt;

            container.localScale = Vector3.one * (baseScale + breath);

            // Shake
            float shakeU = Mathf.Clamp01((u - shakeStartNormalized) / (1f - shakeStartNormalized));
            float intensity = Mathf.Lerp(shakeIntensityStart, shakeIntensityEnd, shakeU * shakeU);

            float sx = (Mathf.PerlinNoise(elapsed * shakeSpeed + shakeSeed, 0f) - 0.5f) * 2f * intensity;
            float sy = (Mathf.PerlinNoise(0f, elapsed * shakeSpeed + shakeSeed + 50f) - 0.5f) * 2f * intensity;
            container.anchoredPosition = new Vector2(sx, sy);

            // Glow: fade in
            if (glowImg)
            {
                Color gc = Color.Lerp(glowColorStart, glowColorPeak, u * u);
                if (u > 0.7f)
                {
                    float flashPulse = Mathf.Sin(elapsed * breathSpeed * 2f) * 0.15f;
                    gc.a = Mathf.Clamp01(gc.a + flashPulse);
                }
                glowImg.color = gc;
            }

            // Bomba: son %25'te flash
            if (bombImg)
            {
                float flashU = Mathf.Clamp01((u - 0.75f) / 0.25f);
                float flashBeat = Mathf.Sin(elapsed * breathSpeed * 3f) * 0.5f + 0.5f;
                bombImg.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.75f), flashU * flashBeat);
            }

            yield return null;
        }

        Destroy(container.gameObject);
        Debug.Log("[PulsePulseExplosionVfx] Charge done");
    }

    // ────────────────────────────────────────────────
    //  Utility
    // ────────────────────────────────────────────────
    private RectTransform CreateContainer(RectTransform parent)
    {
        var go = new GameObject("ChargeContainer", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private Image CreateUIImage(string name, RectTransform parent, Sprite sprite, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }
}