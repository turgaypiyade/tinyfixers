using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LineH için TEK dönen drill süpürmesi: satırın bir ucundan girer, DÖNEREK diğer uca süpürür ve
/// her hücreye vardığında onCellReached(index) ile o hücrenin kırılmasını tetikler (event-driven —
/// roket travel'ıyla aynı mekanik, farklı görsel). LineV roket kalır; yalnız LineH bunu kullanır.
///
/// Kurulum: bu component'i vfx canvas'ı altında board'u kaplayan bir RectTransform'a koy, drillSprite'ı
/// ata ve BoardController.drillSweepPlayer alanına bağla.
/// </summary>
public class DrillSweepPlayer : MonoBehaviour
{
    [Header("Drill")]
    [Tooltip("Ana/yedek drill görüntüsü (rotationFrames boşsa bu kullanılır).")]
    [SerializeField] private Sprite drillSprite;
    [Tooltip("Dönme için 5 kare (flipbook — sırayla oynatılınca döner). PulseCore spin gibi.")]
    [SerializeField] private Sprite[] rotationFrames;
    [Tooltip("Dönme flipbook hızı (kare/sn). Yüksek = hızlı döner.")]
    [Range(1f, 60f)]
    [SerializeField] private float rotationFps = 24f;
    [Tooltip("Süpürme hızı (hücre/sn).")]
    [SerializeField] private float cellsPerSecond = 14f;
    [Tooltip("Drill görsel boyutu (hücre oranı).")]
    [SerializeField, Range(0.5f, 2.5f)] private float sizeCells = 1.2f;
    [Tooltip("Girişte/çıkışta kaç hücre ekran dışından gelsin.")]
    [SerializeField] private float entryLeadCells = 1.0f;

    [Header("Duman (smoke)")]
    [Tooltip("Drill etrafında duman efekti spawn et.")]
    [SerializeField] private bool smokeEnabled = true;
    [Tooltip("Duman sprite'ı (yumuşak daire/puf). Boşsa TileClearBurst yumuşak-daire kullanılır.")]
    [SerializeField] private Sprite smokeSprite;
    [Tooltip("Saniyede kaç duman pufu.")]
    [SerializeField] private float smokePerSecond = 35f;
    [Tooltip("Puf ömrü (sn).")]
    [SerializeField] private float smokeLife = 0.65f;
    [Tooltip("Puf boyutu (hücre oranı).")]
    [SerializeField, Range(0.2f, 2.5f)] private float smokeSizeCells = 1.1f;
    [Tooltip("Duman rengi/opaklığı.")]
    [SerializeField] private Color smokeColor = new Color(0.9f, 0.9f, 0.92f, 0.9f);

    public RectTransform SweepSpace => transform as RectTransform;

    // Tahmini toplam süre (giriş + hücreler + çıkış).
    public float EstimateDuration(int cellCount) =>
        Mathf.Max(0.05f, (cellCount + 2f * entryLeadCells) / Mathf.Max(1f, cellsPerSecond));

    /// <summary>
    /// startAnchored (ilk hücre merkezi, anchored) → stepAnchored yönünde cellCount hücre süpürür.
    /// onCellReached(i): i. hücreye varınca (kırılma tetiklenir). onCompleted: süpürme bitince.
    /// </summary>
    public void PlaySweep(Vector2 startAnchored, Vector2 stepAnchored, int cellCount, float tileSize,
                          float delay, Action<int> onCellReached, Action onCompleted)
    {
        if (cellCount <= 0)
        {
            onCompleted?.Invoke();
            return;
        }
        StartCoroutine(CoSweep(startAnchored, stepAnchored, cellCount, tileSize, delay, onCellReached, onCompleted));
    }

    private IEnumerator CoSweep(Vector2 startAnchored, Vector2 stepAnchored, int cellCount, float tileSize,
                               float delay, Action<int> onCellReached, Action onCompleted)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        var space = SweepSpace;
        if (space == null)
        {
            // Fallback: sadece kırılmaları tetikle (görsel olmadan).
            for (int i = 0; i < cellCount; i++) onCellReached?.Invoke(i);
            onCompleted?.Invoke();
            yield break;
        }

        // Drill image'ını runtime oluştur.
        var go = new GameObject("DrillSweep", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(space, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        float size = tileSize * sizeCells;
        rt.sizeDelta = new Vector2(size, size);
        rt.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        int frameCount = (rotationFrames != null) ? rotationFrames.Length : 0;
        img.sprite = (frameCount > 0 && rotationFrames[0] != null) ? rotationFrames[0] : drillSprite;

        // Girişte entryLeadCells kadar dışarıdan başla, çıkışta entryLeadCells kadar dışarı git.
        Vector2 pos0 = startAnchored - stepAnchored * entryLeadCells;
        float totalCells = cellCount - 1f + 2f * entryLeadCells;  // ilk hücre..son hücre + giriş/çıkış
        float speed = Mathf.Max(1f, cellsPerSecond);
        float duration = totalCells / speed;

        float t = 0f;
        int nextCell = 0;
        float frameTime = 1f / Mathf.Max(1f, rotationFps);
        float frameAcc = 0f;
        int frameIdx = 0;
        float smokeAcc = 0f;
        float smokeInterval = smokePerSecond > 0f ? 1f / smokePerSecond : float.MaxValue;

        while (t < duration)
        {
            t += Time.deltaTime;
            float travelled = Mathf.Min(totalCells, t * speed);   // giriş dahil kat edilen hücre
            rt.anchoredPosition = pos0 + stepAnchored * travelled;

            // Dönme = flipbook (5 kare). Ekstra X/Y/Z dönme eklemiyoruz çünkü flipbook zaten 360 dereceyi simüle ediyor.
            if (frameCount > 0)
            {
                frameAcc += Time.deltaTime;
                while (frameAcc >= frameTime)
                {
                    frameAcc -= frameTime;
                    frameIdx = (frameIdx + 1) % frameCount;
                    if (rotationFrames[frameIdx] != null)
                        img.sprite = rotationFrames[frameIdx];
                }
            }

            // Duman pufları (drill konumunda, arkasına doğru dağılır).
            if (smokeEnabled)
            {
                smokeAcc += Time.deltaTime;
                while (smokeAcc >= smokeInterval)
                {
                    smokeAcc -= smokeInterval;
                    SpawnSmokePuff(space, rt.anchoredPosition, tileSize, stepAnchored);
                }
            }

            // Hücre i, travelled >= entryLeadCells + i konumunda (drill o hücrenin merkezine vardı).
            while (nextCell < cellCount && travelled >= entryLeadCells + nextCell)
            {
                onCellReached?.Invoke(nextCell);
                nextCell++;
            }

            yield return null;
        }

        while (nextCell < cellCount)
        {
            onCellReached?.Invoke(nextCell);
            nextCell++;
        }

        Destroy(go);
        onCompleted?.Invoke();
    }

    private void SpawnSmokePuff(RectTransform space, Vector2 drillPos, float tileSize, Vector2 stepDirection)
    {
        if (space == null) return;

        var go = new GameObject("DrillSmokePuff", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(space, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        float size = tileSize * smokeSizeCells;
        rt.sizeDelta = new Vector2(size, size);

        // Drill arkasında ve etrafında rastgele dağılım
        Vector2 stepNorm = stepDirection.sqrMagnitude > 0.001f ? stepDirection.normalized : Vector2.right;
        Vector2 behindOffset = -stepNorm * (tileSize * 0.3f);
        Vector2 randomOffset = new Vector2(
            UnityEngine.Random.Range(-tileSize * 0.15f, tileSize * 0.15f),
            UnityEngine.Random.Range(-tileSize * 0.15f, tileSize * 0.15f)
        );
        rt.anchoredPosition = drillPos + behindOffset + randomOffset;

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;

        if (smokeSprite != null)
        {
            img.sprite = smokeSprite;
        }
        else
        {
            img.sprite = GetFallbackSmokeSprite();
        }
        img.color = smokeColor;

        // Drill puff'ların üstünde görünsün
        rt.SetAsFirstSibling();

        StartCoroutine(CoAnimateSmokePuff(go, rt, img, smokeLife));
    }

    private IEnumerator CoAnimateSmokePuff(GameObject go, RectTransform rt, Image img, float life)
    {
        float t = 0f;
        Vector3 startScale = Vector3.one * 0.7f;
        Vector3 endScale = Vector3.one * 1.6f;
        Color startColor = img.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        while (t < life)
        {
            if (go == null || !go) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            rt.localScale = Vector3.Lerp(startScale, endScale, k);
            // Linear fade makes it stay visible a bit longer than k*k
            img.color = Color.Lerp(startColor, endColor, k);
            yield return null;
        }

        if (go != null && go) Destroy(go);
    }

    private static Sprite fallbackSmokeSprite;
    private static Sprite GetFallbackSmokeSprite()
    {
        if (fallbackSmokeSprite != null) return fallbackSmokeSprite;

        int res = 32;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
        float radius = res * 0.5f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float norm = Mathf.Clamp01(dist / radius);
                float alpha = Mathf.SmoothStep(1f, 0f, norm);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        fallbackSmokeSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
        return fallbackSmokeSprite;
    }
}

