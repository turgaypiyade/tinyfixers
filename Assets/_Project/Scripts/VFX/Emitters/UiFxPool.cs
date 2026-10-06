using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LineV/LineH roket izi (after-image ghost) ve çarpma efekti için havuz SÜRÜCÜSÜ.
/// Depolama tek yerde: UiVfxPool. Bu sınıf yalnız prefab'tan üretimi, ghost sönmesini (tek Update
/// döngüsü) ve süreli efektin geri dönüşünü yönetir.
///
/// Neden: roket her hücre adımında 12 ghost + 2 impact üretiyordu; her biri yeni GameObject +
/// AddComponent + kendi coroutine'i ile doğup ~0.3 sn sonra Destroy ediliyordu. Bonus turunda
/// 15-20 roket AYNI ANDA uçunca ~0.4 sn'de binlerce obje oluşturulup siliniyordu → iPhone'da
/// GC + UI rebuild takılması. Görünüm birebir aynı: aynı alfa eğrisi, aynı büyüme, aynı ömür.
///
/// Roket örneği (LineTravelSplitSwapTestUI) iş bitince yok ediliyor; o anda hâlâ sönen ghost'lar
/// onunla birlikte gider (eskisi gibi) — havuz eksilen objeyi gerektiğinde yeniden üretir.
/// </summary>
public sealed class UiFxPool : MonoBehaviour
{
    private const int MaxGhostsPerKey = 512;   // bonus turunda aynı anda yüzlerce ghost sönüyor
    private const int MaxTimedPerKey = 128;
    private const string DefaultGhostKey = "LineTravelGhost:default";

    private struct Ghost
    {
        public GameObject go;
        public string key;
        public Image img;
        public RectTransform rt;
        public float t, life, alpha0, power;
        public Vector3 scale0, scale1;
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
        rt = img.rectTransform;
        return go;
    }

    /// <summary>Ghost'u söndürme döngüsüne alır (eski FadeOnly ile aynı eğri); bitince havuza döner.</summary>
    public static void TrackGhost(GameObject go, GameObject prefab, Image img, RectTransform rt,
        float life, float scaleUp, float fadePower)
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
        });
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

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear(true);
            ps.Play(true);
        }

        Instance.timed.Add(new Timed { go = go, key = key, t = 0f, life = life });
        return go;
    }

    // ── Döngü ──────────────────────────────────────────────────────────────

    private void Update()
    {
        float dt = Time.deltaTime;   // eski FadeOnly ile aynı zaman kaynağı

        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            var g = ghosts[i];
            // Roket örneğiyle birlikte yok olduysa havuza dönemez (eskisi gibi) → listeden düş.
            if (g.go == null || g.img == null) { RemoveAtSwap(ghosts, i); continue; }

            g.t += dt;
            float u = Mathf.Clamp01(g.t / g.life);
            float k = Mathf.Pow(1f - u, g.power);
            var c = g.img.color;
            c.a = g.alpha0 * k;
            g.img.color = c;
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

    // Sıra önemsiz → son elemanla yer değiştirip sil (O(1)).
    private static void RemoveAtSwap<T>(List<T> list, int i)
    {
        int last = list.Count - 1;
        if (i != last) list[i] = list[last];
        list.RemoveAt(last);
    }
}
