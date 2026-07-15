using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Robot kafasının üstünde duran HP barı. Battlefield/BossDuel'de hem oyuncu (yeşil)
/// hem düşman (mor) için kullanılır. fillImage tipi "Filled / Horizontal" olmalı.
/// Hasarda fill yumuşak iner + kısa shake.
/// </summary>
public sealed class HpBar : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Image Type = Filled, Fill Method = Horizontal olmalı.")]
    [SerializeField] private Image fillImage;
    [Tooltip("\"current/max\" yazısı (opsiyonel).")]
    [SerializeField] private TMP_Text label;
    [Tooltip("Hasarda sarsılacak hedef (boşsa bu transform).")]
    [SerializeField] private RectTransform shakeTarget;

    [Header("Feel")]
    [SerializeField, Min(0f)] private float fillLerpSpeed = 1.8f;   // saniyede fill oranı
    [SerializeField, Min(0f)] private float damageShakeDuration = 0.18f;
    [SerializeField, Min(0f)] private float damageShakeStrength = 7f;

    private int max = 1;
    private int current = 1;
    private float displayedFill = 1f;
    private Coroutine shakeCo;
    private Vector2 shakeBase;

    // Dalga pip'leri (BossDuel çok-dalga): bar üstünde küçük noktalar; sönük = yenilen dalga.
    private RectTransform pipsRoot;
    private Image[] pips;

    public int Current => current;
    public int Max => max;

    public void Init(int maxHp, int currentHp = -1)
    {
        max = Mathf.Max(1, maxHp);
        current = currentHp < 0 ? max : Mathf.Clamp(currentHp, 0, max);
        displayedFill = (float)current / max;
        if (fillImage != null) fillImage.fillAmount = displayedFill;
        UpdateLabel();
    }

    public void Set(int currentHp, bool flashDamage = true)
    {
        int clamped = Mathf.Clamp(currentHp, 0, max);
        bool tookDamage = clamped < current;
        current = clamped;
        UpdateLabel();
        if (tookDamage && flashDamage)
            PlayShake();
    }

    private void UpdateLabel()
    {
        if (label != null)
            label.text = $"{current}/{max}";
    }

    // ── Dalga pip'leri (çok-dalga BossDuel) ──

    /// <summary>
    /// Bar üstünde dalga noktalarını kurar. 1 dalga = pip çizilmez (tek boss görünümü aynı kalır).
    /// Sahne kurulumu gerektirmez; pip'ler prosedürel üretilir.
    /// </summary>
    public void InitWavePips(int totalWaves)
    {
        if (pipsRoot != null)
        {
            Destroy(pipsRoot.gameObject);
            pipsRoot = null;
            pips = null;
        }

        if (totalWaves <= 1)
            return;

        var go = new GameObject("WavePips", typeof(RectTransform));
        pipsRoot = (RectTransform)go.transform;
        pipsRoot.SetParent(transform, false);
        pipsRoot.anchorMin = pipsRoot.anchorMax = new Vector2(0.5f, 1f);
        pipsRoot.pivot = new Vector2(0.5f, 0f);
        pipsRoot.anchoredPosition = new Vector2(0f, 4f);

        const float pipSize = 12f;
        const float gap = 6f;
        float totalW = totalWaves * pipSize + (totalWaves - 1) * gap;

        pips = new Image[totalWaves];
        for (int i = 0; i < totalWaves; i++)
        {
            var pipGo = new GameObject($"Pip{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)pipGo.transform;
            rt.SetParent(pipsRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(pipSize, pipSize);
            rt.anchoredPosition = new Vector2(-totalW * 0.5f + pipSize * 0.5f + i * (pipSize + gap), 0f);

            var img = pipGo.GetComponent<Image>();
            img.raycastTarget = false;
            pips[i] = img;
        }

        SetWaveIndex(0);
    }

    /// <summary>Aktif dalga index'i (0 bazlı). Öncekiler sönük, aktif+sonrakiler parlak.</summary>
    public void SetWaveIndex(int waveIndex)
    {
        if (pips == null)
            return;

        for (int i = 0; i < pips.Length; i++)
        {
            if (pips[i] == null) continue;
            pips[i].color = i < waveIndex
                ? new Color(0.25f, 0.25f, 0.25f, 0.55f)   // yenilen dalga
                : new Color(1f, 0.85f, 0.25f, 0.95f);     // aktif/bekleyen dalga
        }
    }

    private void Update()
    {
        if (fillImage == null)
            return;

        float target = max > 0 ? (float)current / max : 0f;
        if (!Mathf.Approximately(displayedFill, target))
        {
            displayedFill = Mathf.MoveTowards(displayedFill, target, Mathf.Max(0.01f, fillLerpSpeed) * Time.deltaTime);
            fillImage.fillAmount = displayedFill;
        }
    }

    private void PlayShake()
    {
        if (damageShakeDuration <= 0f)
            return;

        var t = shakeTarget != null ? shakeTarget : (RectTransform)transform;

        if (shakeCo != null)
            StopCoroutine(shakeCo);
        else
            shakeBase = t.anchoredPosition;

        shakeCo = StartCoroutine(ShakeRoutine(t));
    }

    private IEnumerator ShakeRoutine(RectTransform t)
    {
        float e = 0f;
        while (e < damageShakeDuration)
        {
            e += Time.deltaTime;
            float falloff = 1f - Mathf.Clamp01(e / damageShakeDuration);
            t.anchoredPosition = shakeBase + Random.insideUnitCircle * (damageShakeStrength * falloff);
            yield return null;
        }
        t.anchoredPosition = shakeBase;
        shakeCo = null;
    }
}
