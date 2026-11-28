using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Çatlak su deposundan (WaterTank / WaterTankSmall, ara vuruştan sonra) KIRILANA KADAR sürekli su sızıntısı
/// (kullanıcı 2026-11-25). Vuruş anında her sızıntı noktasından bir damla dışa fışkırır ve kısa süre yoğun damlar;
/// sonra seyrek, sürekli damlar. Damla çatlaktan büyüyerek çıkar, gövdenin üstünden yavaş akar, alt kenardan düşüp söner.
/// Noktalar/aralıklar/boy: WaterTankConfig ("Çatlak sızıntısı").
/// PERFORMANS: tüm depolar + damlalar TEK Update'te; damla Image'ları ORTAK UiVfxPool'dan kiralanır, sönünce iade
/// edilir; struct listeleri yeniden kullanılır (karede tahsis yok). Kaynak yokken bileşen kapanır (boşta güncelleme yok).
/// Kaynak durur: Stop(origin) (depo kırıldı), görsel yok olunca/pasifleşince (level değişimi) kendiliğinden, StopAll (teardown).
/// </summary>
public sealed class WaterTankLeakSystem : MonoBehaviour
{
    private const string PoolKey = "WaterTank.Leak";
    private const int PoolKeep = 64;
    private const int MaxPoints = 6;
    private const float MaxDropAge = 2.5f;

    private struct Source
    {
        public int origin;
        public Image target;
        public float burstUntil;
        public int pointCount;
        public float t0, t1, t2, t3, t4, t5;   // nokta başına sonraki damla zamanı (sabit alan → dizi tahsisi yok)
    }

    private struct Drop
    {
        public Image img;
        public Vector2 pos, vel;
        public float age, bottomY, fadeFromY, bodyHeight;
        public bool squirt;
    }

    private readonly List<Source> sources = new(4);
    private readonly List<Drop> drops = new(48);
    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform root;
    private WaterTankConfig config;
    private float clock;

    public static WaterTankLeakSystem Ensure(GameObject host)
    {
        if (host == null) return null;
        if (!host.TryGetComponent(out WaterTankLeakSystem sys))
        {
            sys = host.AddComponent<WaterTankLeakSystem>();
            sys.enabled = false;
        }
        return sys;
    }

    /// <summary>Depo çatladı: sızıntıyı başlatır (aynı depo için tekrar çağrı yalnız yoğun sızıntıyı tazeler).</summary>
    public void Begin(int origin, Image target, RectTransform vfxRoot)
    {
        if (target == null || vfxRoot == null) return;
        config = WaterTankConfig.Load();
        if (config == null || config.leakPoints == null || config.leakPoints.Count == 0
            || config.dropletSprites == null || config.dropletSprites.Count == 0)
            return;
        root = vfxRoot;

        var src = new Source
        {
            origin = origin,
            target = target,
            burstUntil = clock + Mathf.Max(0f, config.leakBurstDuration),
            pointCount = Mathf.Min(MaxPoints, config.leakPoints.Count),
        };
        for (int p = 0; p < src.pointCount; p++)
            SetNext(ref src, p, clock + p * 0.05f);

        int existing = IndexOf(origin);
        if (existing >= 0) sources[existing] = src;
        else sources.Add(src);

        // Vuruş anı: her noktadan bir damla dışa fışkırır.
        if (TryGetRect(target, out var center, out var size))
            for (int p = 0; p < src.pointCount; p++)
                SpawnDrop(center, size, config.leakPoints[p], squirt: true, p);

        enabled = true;
    }

    /// <summary>Depo kırıldı: yeni damla çıkmaz; havadaki damlalar akıp söner.</summary>
    public void Stop(int origin)
    {
        int i = IndexOf(origin);
        if (i >= 0) sources.RemoveAt(i);
    }

    /// <summary>Teardown/level değişimi: kaynaklar silinir, tüm damlalar havuza döner.</summary>
    public void StopAll()
    {
        sources.Clear();
        for (int i = 0; i < drops.Count; i++)
            if (drops[i].img != null) UiVfxPool.Return(PoolKey, drops[i].img.gameObject, PoolKeep);
        drops.Clear();
        enabled = false;
    }

    // Boşta kapanırken liste zaten boş; GO kapanırken/yok edilirken (teardown) damlalar havuza döner.
    private void OnDisable() => StopAll();

    private void OnDestroy() => StopAll();

    private void Update()
    {
        if (root == null) { StopAll(); return; }
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        clock += dt;
        Emit();
        StepDrops(dt);
        if (sources.Count == 0 && drops.Count == 0) enabled = false;
    }

    private void Emit()
    {
        if (config == null) return;
        float steady = Mathf.Max(0.05f, config.leakInterval);
        float burst = Mathf.Max(0.03f, config.leakBurstInterval);
        for (int s = sources.Count - 1; s >= 0; s--)
        {
            var src = sources[s];
            // Görsel yok edildi/pasif (kırılma, level değişimi) → kaynak düşer.
            if (src.target == null || !src.target.isActiveAndEnabled) { sources.RemoveAt(s); continue; }
            bool rectOk = false;
            Vector2 center = default, size = default;
            float interval = clock < src.burstUntil ? burst : steady;
            for (int p = 0; p < src.pointCount; p++)
            {
                if (clock < GetNext(in src, p)) continue;
                if (!rectOk) rectOk = TryGetRect(src.target, out center, out size);
                if (rectOk && p < config.leakPoints.Count) SpawnDrop(center, size, config.leakPoints[p], squirt: false, p);
                SetNext(ref src, p, clock + interval * Random.Range(0.75f, 1.25f));
            }
            sources[s] = src;
        }
    }

    private void SpawnDrop(Vector2 center, Vector2 size, Vector2 uv, bool squirt, int spriteIndex)
    {
        var img = UiVfxPool.RentImage(PoolKey, root, "WaterTankLeak");
        if (img == null) return;
        img.gameObject.layer = root.gameObject.layer;
        img.transform.SetAsLastSibling();
        var sprites = config.dropletSprites;
        img.sprite = sprites[spriteIndex % sprites.Count];
        img.preserveAspect = true;
        img.color = config.dropletTint;
        float w = size.x * config.leakSizeRatio * Random.Range(0.8f, 1.1f);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.zero;
        rt.sizeDelta = new Vector2(w, w * 1.3f);

        Vector2 at = center + new Vector2((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y);
        float bottomY = center.y - size.y * 0.44f;   // gövdenin alt kenarı: buradan sonra serbest düşüş
        float side = uv.x < 0.5f ? -1f : 1f;
        rt.anchoredPosition = at;
        drops.Add(new Drop
        {
            img = img,
            pos = at,
            vel = squirt ? new Vector2(side * size.x * 0.55f, size.y * 0.35f) : Vector2.zero,
            bottomY = bottomY,
            fadeFromY = squirt ? at.y - size.y * 0.1f : bottomY,
            bodyHeight = size.y,
            squirt = squirt,
        });
    }

    private void StepDrops(float dt)
    {
        Color tint = config != null ? config.dropletTint : Color.white;
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i];
            if (d.img == null) { RemoveDropAt(i); continue; }
            float h = d.bodyHeight;
            d.age += dt;
            bool onBody = !d.squirt && d.pos.y > d.bottomY;
            d.vel.y -= (onBody ? h * 1.4f : h * 7f) * dt;          // gövdede yavaş akış, kenardan sonra yerçekimi
            if (onBody) d.vel.y = Mathf.Max(d.vel.y, -h * 0.55f);
            d.pos += d.vel * dt;

            float below = Mathf.Max(0f, d.fadeFromY - d.pos.y);
            float alpha = 1f - Mathf.Clamp01(below / (h * 0.35f));
            if (alpha <= 0f || d.age > MaxDropAge)
            {
                UiVfxPool.Return(PoolKey, d.img.gameObject, PoolKeep);
                RemoveDropAt(i);
                continue;
            }

            // Belirme (çatlaktan çıkar), düşüşte dikey uzama, hız yönüne bakış.
            float grow = Mathf.Clamp01(d.age / 0.12f);
            float stretch = onBody ? 1f : Mathf.Lerp(1f, 1.5f, Mathf.Clamp01(-d.vel.y / (h * 2f)));
            var rt = d.img.rectTransform;
            rt.anchoredPosition = d.pos;
            if (d.vel.sqrMagnitude > 1f)
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.vel.y, d.vel.x) * Mathf.Rad2Deg + 90f);
            rt.localScale = new Vector3(grow / Mathf.Sqrt(stretch), grow * stretch, 1f);
            d.img.color = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
            drops[i] = d;
        }
    }

    private void RemoveDropAt(int i)
    {
        int last = drops.Count - 1;
        drops[i] = drops[last];   // sıra önemsiz: swap-remove
        drops.RemoveAt(last);
    }

    private int IndexOf(int origin)
    {
        for (int i = 0; i < sources.Count; i++) if (sources[i].origin == origin) return i;
        return -1;
    }

    // Deponun root uzayındaki merkezi + boyu (görsel kare → boy = genişlik).
    private bool TryGetRect(Image target, out Vector2 center, out Vector2 size)
    {
        center = default; size = default;
        if (target == null || root == null) return false;
        target.rectTransform.GetWorldCorners(corners);
        Vector2 a = root.InverseTransformPoint(corners[0]);
        Vector2 b = root.InverseTransformPoint(corners[2]);
        center = (a + b) * 0.5f;
        float w = Mathf.Abs(b.x - a.x);
        if (w <= 0f) return false;
        size = new Vector2(w, w);
        return true;
    }

    private static float GetNext(in Source s, int p) => p switch
    {
        0 => s.t0, 1 => s.t1, 2 => s.t2, 3 => s.t3, 4 => s.t4, _ => s.t5,
    };

    private static void SetNext(ref Source s, int p, float t)
    {
        switch (p)
        {
            case 0: s.t0 = t; break;
            case 1: s.t1 = t; break;
            case 2: s.t2 = t; break;
            case 3: s.t3 = t; break;
            case 4: s.t4 = t; break;
            default: s.t5 = t; break;
        }
    }
}
