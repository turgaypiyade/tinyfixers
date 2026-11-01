using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LineV/LineH roket izi (after-image ghost) ve çarpma efekti için havuz SÜRÜCÜSÜ.
/// Depolama tek yerde: UiVfxPool. Bu sınıf yalnız prefab'tan üretimi, ghost sönmesini (tek Update
/// döngüsü) ve süreli efektin geri dönüşünü yönetir.
/// PulseCore fitil kıvılcımları da aynı döngüde söner; taş/overlay kapanınca havuza döner.
///
/// Neden: roket her hücre adımında 12 ghost + 2 impact üretiyordu; her biri yeni GameObject +
/// AddComponent + kendi coroutine'i ile doğup ~0.3 sn sonra Destroy ediliyordu. Bonus turunda
/// 15-20 roket AYNI ANDA uçunca ~0.4 sn'de binlerce obje oluşturulup siliniyordu → iPhone'da
/// GC + UI rebuild takılması. Görünüm birebir aynı: aynı alfa eğrisi, aynı büyüme, aynı ömür.
///
/// Roket örneği kapanınca altındaki izler aynı anda kaybolur; bu parçalar root yok edilmeden
/// havuza döner. Dışarı parent edilmiş izler kendi sürelerini tamamlar.
/// </summary>
public sealed class UiFxPool : MonoBehaviour
{
    private const int MaxGhostsPerKey = 512;   // bonus turunda aynı anda yüzlerce ghost sönüyor
    private const int MaxTimedPerKey = 128;
    private const string DefaultGhostKey = "LineTravelGhost:default";
    internal const string PulseFuseSparkKey = "PulseCore:fuseSpark";
    private const int MaxPulseSparksPerKey = 512;

    private struct PulseFuseSpark
    {
        public Image image;
        public RectTransform rect;
        public Vector2 start, drift;
        public float age, life, size;
        public bool slot;          // PulseFuseSparkleView'in kalıcı yuvası: havuza dönmez, yalnız söner
        public float sizeScale;    // yuva: gerçek boy / yuva boyu (yalnız slot=true)
    }

    private struct Ghost
    {
        public GameObject go;
        public string key;
        public Image img;
        public RectTransform rt;
        public float t, life, alpha0, power;
        public Vector3 scale0, scale1;
        public Transform owner;
        public bool cancelWithOwner;
        public int skipFrame;
    }

    private struct ChargeSpark
    {
        public Image image;
        public RectTransform rect;
        public Transform owner;
        public Vector2 start, drift;
        public float age, life, size;
        public int lastFrame;
    }

    internal const string ChargeSparkKey = "PulseFuseSpark";
    private const int MaxChargeSparksPerKey = 256;

    private struct PatchbotSpark
    {
        public Image image;
        public RectTransform rect;
        public Transform owner;
        public string key;
        public Vector2 from, to;
        public Color color;
        public float age, life;
        public int lastFrame;
    }

    private struct Timed
    {
        public GameObject go;
        public string key;
        public float t, life;
    }

    private static UiFxPool _instance;
    private static readonly Dictionary<GameObject, string> prefabKeys = new();

    private readonly List<Ghost> ghosts = new(256);
    private readonly List<Timed> timed = new(64);
    private readonly List<PulseFuseSpark> pulseFuseSparks = new(128);
    private readonly List<ChargeSpark> chargeSparks = new(128);
    private readonly List<ParticleSystem> particleResetBuffer = new(16);
    private readonly List<PatchbotSpark> patchbotSparks = new(128);

    private static UiFxPool Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[UiFxPool]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UiFxPool>();
            return _instance;
        }
    }

    private static string KeyFor(string prefix, GameObject prefab)
    {
        if (prefab == null) return DefaultGhostKey;
        if (!prefabKeys.TryGetValue(prefab, out var key))
            prefabKeys[prefab] = key = prefix + prefab.GetInstanceID();
        return key;
    }

    // ── Ghost (iz) ─────────────────────────────────────────────────────────

    /// <summary>Havuzdan ghost alır (yoksa üretir) ve parent'a takar. prefab null → düz Image.</summary>
    public static GameObject RentGhost(GameObject prefab, RectTransform parent, out Image img, out RectTransform rt)
    {
        string key = KeyFor("LineTravelGhost:", prefab);
        var go = UiVfxPool.TryRent(key, parent, "LineTravelAfterImage");
        if (go == null)
        {
            go = prefab != null
                ? Instantiate(prefab, parent)
                : new GameObject("LineTravelAfterImage", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
        }
        go.transform.SetAsLastSibling();
        go.SetActive(true);

        img = go.GetComponentInChildren<Image>(true);
        if (img == null) img = go.AddComponent<Image>();
        img.gameObject.SetActive(true);
        img.canvasRenderer.SetAlpha(1f);   // sönüm CanvasRenderer'dan sürülür; havuzdan önceki kullanımın 0'ı taşınmasın
        rt = img.rectTransform;
        return go;
    }

    /// <summary>Ghost'u söndürme döngüsüne alır (eski FadeOnly ile aynı eğri); bitince havuza döner.</summary>
    public static void TrackGhost(GameObject go, GameObject prefab, Image img, RectTransform rt,
        float life, float scaleUp, float fadePower, Transform owner = null)
    {
        Instance.ghosts.Add(new Ghost
        {
            go = go,
            key = KeyFor("LineTravelGhost:", prefab),
            img = img,
            rt = rt,
            t = 0f,
            life = Mathf.Max(0.0001f, life),
            alpha0 = img != null ? img.color.a : 1f,
            power = Mathf.Max(0.5f, fadePower),
            scale0 = rt != null ? rt.localScale : Vector3.one,
            scale1 = (rt != null ? rt.localScale : Vector3.one) * scaleUp,
            owner = owner,
            skipFrame = -1,
        });
    }

    /// <summary>PatchBot's original linear fade, including its synchronous first frame.</summary>
    public static void TrackLinearGhost(GameObject go, string key, Image image, float life, Transform owner)
    {
        float duration = Mathf.Max(0.0001f, life);
        float alpha = image.color.a;
        image.canvasRenderer.SetAlpha(1f - Mathf.Clamp01(Time.deltaTime / duration));
        Instance.ghosts.Add(new Ghost
        {
            // Pure alpha fade: leave rt null so the driver never rewrites its unchanged scale.
            go = go, key = key, img = image, t = Time.deltaTime, life = duration,
            alpha0 = alpha, power = 1f,
            owner = owner, cancelWithOwner = true, skipFrame = Time.frameCount
        });
    }

    public static void ReleaseGhosts(Transform owner, bool childrenOnly = false)
    {
        if (_instance == null) return;
        var list = _instance.ghosts;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var ghost = list[i];
            if (ghost.owner != owner || (childrenOnly && ghost.go != null && !ghost.go.transform.IsChildOf(owner))) continue;
            if (ghost.go != null) UiVfxPool.Return(ghost.key, ghost.go, MaxGhostsPerKey);
            RemoveAtSwap(list, i);
        }
    }

    // ── Süreli efekt (impact) ──────────────────────────────────────────────

    /// <summary>Süreli efekt (ör. çarpma parçacığı) — havuzdan alınır, parçacıkları baştan oynar,
    /// 'life' sonra havuza döner (eski Destroy zamanlamasıyla aynı).</summary>
    public static GameObject RentTimed(GameObject prefab, RectTransform parent, float life)
    {
        if (prefab == null) return null;
        string key = KeyFor("Timed:", prefab);
        var go = UiVfxPool.TryRent(key, parent, prefab.name);
        if (go == null)
        {
            go = Instantiate(prefab, parent);
        }
        else if (go.transform is RectTransform rt && prefab.transform is RectTransform src)
        {
            // UiVfxPool.Prepare boyut/anchor'ı sıfırlar → prefab'ın kendi ölçülerini geri yükle.
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.sizeDelta = src.sizeDelta;
            rt.localScale = src.localScale;
            rt.localRotation = src.localRotation;
        }
        go.transform.SetAsLastSibling();
        go.SetActive(true);

        var driver = Instance;
        driver.particleResetBuffer.Clear();
        go.GetComponentsInChildren(true, driver.particleResetBuffer);
        for (int i = 0; i < driver.particleResetBuffer.Count; i++)
        {
            var ps = driver.particleResetBuffer[i];
            ps.Clear(true);
            ps.Play(true);
        }
        driver.particleResetBuffer.Clear();

        driver.timed.Add(new Timed { go = go, key = key, t = 0f, life = life });
        return go;
    }

    // ── Döngü ──────────────────────────────────────────────────────────────

    internal static void TrackPulseFuseSpark(Image image, float life, float size)
    {
        // Görünürlük ve boy her kare CanvasRenderer alpha + localScale ile sürülür: ikisi de Graphic'i yeniden
        // kurdurmaz. Eskiden her kare image.color + sizeDelta yazılıyordu; PulseCore başına ~33 canlı kıvılcım ×
        // tahtadaki her pulse = yüzlerce görsel her kare baştan kuruluyordu (L205 kombo karesinde Canvas 17 ms).
        var color = image.color;
        color.a = 1f;
        image.color = color;
        image.rectTransform.sizeDelta = Vector2.one * size;
        image.rectTransform.localScale = new Vector3(0.4f, 0.4f, 1f);
        image.canvasRenderer.SetAlpha(0f); // No full-opacity frame before the shared driver first advances it.
        Instance.pulseFuseSparks.Add(new PulseFuseSpark
        {
            image = image,
            rect = image.rectTransform,
            start = image.rectTransform.anchoredPosition,
            drift = Random.insideUnitCircle.normalized * Random.Range(6f, 14f),
            life = Mathf.Max(0.0001f, life),
            size = size
        });
    }

    /// <summary>Kalıcı yuva kıvılcımı: sprite/renk/boy sahibi tarafından bir kez yazılmıştır; burada yalnız
    /// konum + ölçek + CanvasRenderer alpha sürülür (Graphic yeniden kurulmaz). Bitince havuza dönmez.</summary>
    internal static void TrackPulseFuseSparkSlot(Image image, float life, float sizeScale)
    {
        var rect = image.rectTransform;
        image.canvasRenderer.SetAlpha(0f);
        float s0 = 0.4f * sizeScale;
        rect.localScale = new Vector3(s0, s0, 1f);
        Instance.pulseFuseSparks.Add(new PulseFuseSpark
        {
            image = image,
            rect = rect,
            start = rect.anchoredPosition,
            drift = Random.insideUnitCircle.normalized * Random.Range(6f, 14f),
            life = Mathf.Max(0.0001f, life),
            slot = true,
            sizeScale = sizeScale
        });
    }

    private void UpdatePulseFuseSparks(float dt)
    {
        for (int i = pulseFuseSparks.Count - 1; i >= 0; i--)
        {
            var spark = pulseFuseSparks[i];
            if (spark.image == null || spark.rect == null)
            {
                RemoveAtSwap(pulseFuseSparks, i);
                continue;
            }
            spark.age += dt;
            if (!spark.image.gameObject.activeInHierarchy || spark.age >= spark.life)
            {
                if (spark.slot)
                {
                    spark.image.canvasRenderer.SetAlpha(0f);
                    spark.rect.localScale = Vector3.zero;
                }
                else UiVfxPool.Return(PulseFuseSparkKey, spark.image.gameObject, MaxPulseSparksPerKey);
                RemoveAtSwap(pulseFuseSparks, i);
                continue;
            }
            float k = spark.age / spark.life;
            spark.rect.anchoredPosition = spark.start + spark.drift * k;
            spark.image.canvasRenderer.SetAlpha(k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f);
            float scale = Mathf.Lerp(0.4f, 1f, Mathf.Sin(k * Mathf.PI)) * (spark.slot ? spark.sizeScale : 1f);
            spark.rect.localScale = new Vector3(scale, scale, 1f);
            pulseFuseSparks[i] = spark;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;   // eski FadeOnly ile aynı zaman kaynağı
        UpdatePulseFuseSparks(dt);
        UpdateChargeSparks(dt);
        UpdatePatchbotSparks(dt);

        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            var g = ghosts[i];
            // Roket örneğiyle birlikte yok olduysa havuza dönemez (eskisi gibi) → listeden düş.
            if (g.go == null || g.img == null) { RemoveAtSwap(ghosts, i); continue; }

            if (g.cancelWithOwner && (g.owner == null || !g.owner.gameObject.activeInHierarchy))
            {
                UiVfxPool.Return(g.key, g.go, MaxGhostsPerKey);
                RemoveAtSwap(ghosts, i);
                continue;
            }
            if (g.skipFrame == Time.frameCount) continue;

            g.t += dt;
            float u = Mathf.Clamp01(g.t / g.life);
            float k = Mathf.Pow(1f - u, g.power);
            // image.color alpha0'da kalır; sönüm CanvasRenderer alpha'sıyla (çarpılır) → Graphic yeniden kurulmaz.
            g.img.canvasRenderer.SetAlpha(k);
            if (g.rt != null) g.rt.localScale = Vector3.LerpUnclamped(g.scale0, g.scale1, u);

            if (g.t >= g.life)
            {
                UiVfxPool.Return(g.key, g.go, MaxGhostsPerKey);
                RemoveAtSwap(ghosts, i);
                continue;
            }
            ghosts[i] = g;
        }

        float udt = Time.unscaledDeltaTime;   // impact eskiden AutoDestroyUnscaled ile siliniyordu
        for (int i = timed.Count - 1; i >= 0; i--)
        {
            var e = timed[i];
            if (e.go == null) { RemoveAtSwap(timed, i); continue; }
            e.t += udt;
            if (e.t >= e.life)
            {
                UiVfxPool.Return(e.key, e.go, MaxTimedPerKey);
                RemoveAtSwap(timed, i);
                continue;
            }
            timed[i] = e;
        }
    }

    internal static void TrackChargeSpark(Image image, Transform owner, float life, float size)
    {
        var spark = new ChargeSpark
        {
            image = image, rect = image.rectTransform, owner = owner,
            start = image.rectTransform.anchoredPosition,
            drift = (Vector2.up * 1.6f + Random.insideUnitCircle).normalized * Random.Range(12f, 30f),
            life = Mathf.Max(0.0001f, life), size = size, lastFrame = Time.frameCount
        };
        var color = image.color;
        color.a = 1f;
        image.color = color;
        image.rectTransform.sizeDelta = Vector2.one * size;
        // StartCoroutine previously applied its first animation step synchronously.
        AdvanceChargeSpark(ref spark, Time.deltaTime);
        Instance.chargeSparks.Add(spark);
    }

    private static void AdvanceChargeSpark(ref ChargeSpark spark, float dt)
    {
        spark.age += dt;
        float k = spark.age / spark.life;
        spark.rect.anchoredPosition = spark.start + spark.drift * k;
        // Alpha + boy CanvasRenderer/localScale ile: Graphic yeniden kurulmaz (fitil kıvılcımıyla aynı kural).
        spark.image.canvasRenderer.SetAlpha(Mathf.Clamp01(k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f));
        float scale = Mathf.Lerp(0.45f, 1.15f, Mathf.Sin(k * Mathf.PI));
        spark.rect.localScale = new Vector3(scale, scale, 1f);
    }

    private void UpdateChargeSparks(float dt)
    {
        for (int i = chargeSparks.Count - 1; i >= 0; i--)
        {
            var spark = chargeSparks[i];
            if (spark.image == null || spark.rect == null) { RemoveAtSwap(chargeSparks, i); continue; }
            if (spark.owner == null || !spark.owner.gameObject.activeInHierarchy || !spark.image.gameObject.activeInHierarchy
                || spark.age >= spark.life)
            {
                UiVfxPool.Return(ChargeSparkKey, spark.image.gameObject, MaxChargeSparksPerKey);
                RemoveAtSwap(chargeSparks, i);
                continue;
            }
            if (spark.lastFrame == Time.frameCount) continue;
            spark.lastFrame = Time.frameCount;
            AdvanceChargeSpark(ref spark, dt);
            chargeSparks[i] = spark;
        }
    }

    internal static void ReleaseChargeSparks(Transform owner)
    {
        // Teardown must not create the persistent driver. Cancel before the owner can be reused.
        if (_instance == null) return;
        var sparks = _instance.chargeSparks;
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var spark = sparks[i];
            if (spark.owner != owner) continue;
            if (spark.image != null)
                UiVfxPool.Return(ChargeSparkKey, spark.image.gameObject, MaxChargeSparksPerKey);
            RemoveAtSwap(sparks, i);
        }
    }

    internal static void TrackPatchbotSpark(Image image, Transform owner, Vector2 from, Vector2 to, float life, string key)
    {
        var spark = new PatchbotSpark
        {
            image = image, rect = image.rectTransform, owner = owner, key = key,
            from = from, to = to, color = image.color, life = life, lastFrame = Time.frameCount
        };
        AdvancePatchbotSpark(ref spark, Time.deltaTime);
        Instance.patchbotSparks.Add(spark);
    }

    private static void AdvancePatchbotSpark(ref PatchbotSpark spark, float dt)
    {
        spark.age += dt;
        float t = Mathf.Clamp01(spark.age / spark.life);
        float eased = 1f - (1f - t) * (1f - t);
        spark.rect.anchoredPosition = Vector2.LerpUnclamped(spark.from, spark.to, eased);
        float scale = t < 0.2f ? Mathf.Lerp(0.85f, 1.25f, t / 0.2f) : Mathf.Lerp(1.25f, 0.10f, (t - 0.2f) / 0.8f);
        spark.rect.localScale = Vector3.one * scale;
        float alpha = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
        spark.image.canvasRenderer.SetAlpha(alpha);   // image.color = spark.color sabit; çarpım aynı sonucu verir
    }

    private void UpdatePatchbotSparks(float dt)
    {
        for (int i = patchbotSparks.Count - 1; i >= 0; i--)
        {
            var spark = patchbotSparks[i];
            if (spark.image == null || spark.rect == null) { RemoveAtSwap(patchbotSparks, i); continue; }
            if (spark.owner == null || !spark.owner.gameObject.activeInHierarchy || !spark.image.gameObject.activeInHierarchy
                || spark.age >= spark.life)
            {
                UiVfxPool.Return(spark.key, spark.image.gameObject);
                RemoveAtSwap(patchbotSparks, i);
                continue;
            }
            if (spark.lastFrame == Time.frameCount) continue;
            spark.lastFrame = Time.frameCount;
            AdvancePatchbotSpark(ref spark, dt);
            patchbotSparks[i] = spark;
        }
    }

    internal static void ReleasePatchbotSparks(Transform owner)
    {
        if (_instance == null) return;
        var sparks = _instance.patchbotSparks;
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var spark = sparks[i];
            if (spark.owner != owner) continue;
            if (spark.image != null) UiVfxPool.Return(spark.key, spark.image.gameObject);
            RemoveAtSwap(sparks, i);
        }
    }

    // Sıra önemsiz → son elemanla yer değiştirip sil (O(1)).
    private static void RemoveAtSwap<T>(List<T> list, int i)
    {
        int last = list.Count - 1;
        if (i != last) list[i] = list[last];
        list.RemoveAt(last);
    }
}
