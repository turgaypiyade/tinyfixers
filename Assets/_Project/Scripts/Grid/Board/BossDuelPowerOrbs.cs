using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kırılan taşların enerjisi iki perdede güç göstergesine taşınır:
/// 1) TOPLANMA — her orb kendi hücresinden board ortasındaki buluşma noktasına süzülür ve
///    orada halka olup döner. Hamle boyunca kırılan her taş bu halkaya katılır.
/// 2) FIRLAMA — kırılma durunca halkadaki orb'lar sırayla, uzun izler bırakarak göstergeye akar.
///
/// Renk taşın KENDİ rengidir (BoardBreakFxService.ColorForTileType — kırılma FX'iyle tek kaynak).
/// Gücün kendisi burada sayılmaz: BossDuelController taş kırılır kırılmaz senkron ekler
/// (level-end kapısı buna bağlı). Bu bileşen yalnız sunumdur, varış anında geri çağırır.
/// </summary>
public sealed class BossDuelPowerOrbs : MonoBehaviour
{
    [Serializable]
    public struct Tuning
    {
        public float gatherDuration;    // hücreden buluşma noktasına
        public float holdBeforeLaunch;  // son taştan sonra fırlamaya kadar beklenen süre
        public float launchDuration;    // buluşma noktasından göstergeye
        public float launchStagger;     // orb'lar arası fırlama gecikmesi
        public float rallyRadius;       // buluşma halkasının yarıçapı (px)
        public float headSize;          // baş parıltısının çapı (px)
        public int trailDots;           // kuyruk nokta sayısı
        public int trailStride;         // kuyruk seyrekliği (kaç frame'de bir örnek)
        public int maxConcurrent;       // aynı anda taşınabilecek orb sayısı

        public static Tuning Default => new Tuning
        {
            gatherDuration = 0.30f,
            holdBeforeLaunch = 0.12f,
            launchDuration = 0.34f,
            launchStagger = 0.022f,
            rallyRadius = 46f,
            headSize = 58f,
            trailDots = 9,
            trailStride = 2,
            maxConcurrent = 18,
        };
    }

    private enum Phase { Gather, Hold, Launch }

    private RectTransform overlay;
    private RectTransform target;
    private Func<Vector3> rallyWorld;
    private Action onArrived;
    private Func<bool> canEmit;
    private Tuning tuning = Tuning.Default;

    private readonly Stack<Orb> pool = new();
    private readonly List<Orb> live = new();
    private float sinceLastSpawn;
    private int slotSeed;

    /// Taşınmakta olan orb sayısı — darbe, son enerji varmadan başlamasın diye controller okur.
    public int InFlight => live.Count;

    private sealed class Orb
    {
        public RectTransform head;
        public Image headImage, coreImage;
        public RectTransform[] trail;
        public Image[] trailImages;
        public Vector2[] history;
        public Vector2 start, control, rally;
        public float elapsed, delay, spin;
        public Phase phase;
        public Color color;
    }

    public void Initialize(RectTransform vfxRoot, RectTransform meter, Func<Vector3> rally,
                           Action arrived, Func<bool> emitGate, Tuning settings)
    {
        overlay = vfxRoot;
        target = meter;
        rallyWorld = rally;
        onArrived = arrived;
        canEmit = emitGate;
        if (settings.gatherDuration > 0f) tuning = settings;
    }

    private void OnEnable() => GameEventBus.OnTileClearedAt += HandleTileClearedAt;

    private void OnDisable()
    {
        GameEventBus.OnTileClearedAt -= HandleTileClearedAt;
        for (int i = live.Count - 1; i >= 0; i--)
            Recycle(live[i]);
        live.Clear();
    }

    private void HandleTileClearedAt(TileType type, Vector3 worldPos)
    {
        if (overlay == null || target == null) return;
        if (canEmit != null && !canEmit()) return;

        sinceLastSpawn = 0f;

        // Halka doluysa görseli atla ama krediyi ANINDA ver — gösterge gerçeğin gerisinde kalmasın.
        if (live.Count >= Mathf.Max(1, tuning.maxConcurrent))
        {
            onArrived?.Invoke();
            return;
        }

        var orb = pool.Count > 0 ? pool.Pop() : CreateOrb();
        orb.color = BoardBreakFxService.ColorForTileType(type);
        orb.start = WorldToAnchoredIn(overlay, worldPos);

        // Buluşma noktası etrafında kendine ait bir yuva — orb'lar üst üste binmesin.
        float angle = slotSeed++ * 2.39996f;   // altın açı: az sayıda orbda bile dağınık durur
        orb.rally = RallyCenter() + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.62f) * tuning.rallyRadius;
        orb.spin = angle;

        // Toplanırken hafif yay: taş önce yukarı fırlar, sonra merkeze düşer.
        Vector2 mid = (orb.start + orb.rally) * 0.5f;
        orb.control = mid + new Vector2(0f, 70f);

        orb.phase = Phase.Gather;
        orb.elapsed = 0f;
        orb.delay = 0f;

        Paint(orb);
        orb.head.anchoredPosition = orb.start;
        orb.head.localScale = Vector3.one * 0.4f;
        orb.head.gameObject.SetActive(true);
        for (int i = 0; i < orb.history.Length; i++) orb.history[i] = orb.start;
        live.Add(orb);
    }

    private Vector2 RallyCenter()
    {
        Vector3 world = rallyWorld != null ? rallyWorld() : target.position;
        return WorldToAnchoredIn(overlay, world);
    }

    private void Update()
    {
        if (live.Count == 0) return;

        sinceLastSpawn += Time.deltaTime;
        // Kırılma durdu: halkadaki herkes sırayla göstergeye fırlar.
        bool release = sinceLastSpawn >= tuning.holdBeforeLaunch;
        int launchIndex = 0;

        Vector2 meterPos = WorldToAnchoredIn(overlay, target.position);

        for (int i = live.Count - 1; i >= 0; i--)
        {
            var orb = live[i];
            orb.elapsed += Time.deltaTime;

            switch (orb.phase)
            {
                case Phase.Gather:
                {
                    float k = Mathf.Clamp01(orb.elapsed / Mathf.Max(0.02f, tuning.gatherDuration));
                    float e = 1f - (1f - k) * (1f - k);
                    Push(orb, Bezier(orb.start, orb.control, orb.rally, e));
                    orb.head.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, e);
                    if (k >= 1f) { orb.phase = Phase.Hold; orb.elapsed = 0f; }
                    break;
                }

                case Phase.Hold:
                {
                    // Yerinde nefes alarak döner; halka toplandıkça enerji birikiyor hissi.
                    orb.spin += Time.deltaTime * 1.8f;
                    Vector2 ring = RallyCenter() + new Vector2(Mathf.Cos(orb.spin), Mathf.Sin(orb.spin) * 0.62f) * tuning.rallyRadius;
                    Push(orb, Vector2.Lerp(orb.head.anchoredPosition, ring, 1f - Mathf.Exp(-12f * Time.deltaTime)));
                    orb.head.localScale = Vector3.one * (1f + Mathf.Sin(orb.spin * 2.4f) * 0.08f);
                    if (release)
                    {
                        orb.phase = Phase.Launch;
                        orb.delay = launchIndex++ * Mathf.Max(0f, tuning.launchStagger);
                        orb.elapsed = 0f;
                        orb.start = orb.head.anchoredPosition;
                        // Göstergeye doğru dışa taşan yay: düz çizgi yerine savrulan bir hamle.
                        Vector2 mid = (orb.start + meterPos) * 0.5f;
                        orb.control = mid + new Vector2(0f, 120f);
                    }
                    break;
                }

                case Phase.Launch:
                {
                    if (orb.elapsed < orb.delay) { Push(orb, orb.head.anchoredPosition); break; }
                    float k = Mathf.Clamp01((orb.elapsed - orb.delay) / Mathf.Max(0.02f, tuning.launchDuration));
                    float e = k * k * (3f - 2f * k);   // yavaş kopar, ortada hızlanır, hedefte yumuşar
                    Push(orb, Bezier(orb.start, orb.control, meterPos, e));
                    orb.head.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.35f, k * k);
                    if (k < 1f) break;

                    live.RemoveAt(i);
                    Recycle(orb);
                    onArrived?.Invoke();
                    continue;
                }
            }

            DrawTrail(orb);
        }
    }

    // Konum geçmişi: kuyruk noktaları bu tamponu seyrek örnekleyerek uzun bir iz çizer.
    private void Push(Orb orb, Vector2 pos)
    {
        for (int t = orb.history.Length - 1; t > 0; t--)
            orb.history[t] = orb.history[t - 1];
        orb.history[0] = pos;
        orb.head.anchoredPosition = pos;
    }

    private void DrawTrail(Orb orb)
    {
        int stride = Mathf.Max(1, tuning.trailStride);
        for (int t = 0; t < orb.trail.Length; t++)
        {
            int sample = Mathf.Min(orb.history.Length - 1, (t + 1) * stride);
            orb.trail[t].anchoredPosition = orb.history[sample];
            float fade = 1f - (float)t / orb.trail.Length;
            var c = orb.color;
            orb.trailImages[t].color = new Color(c.r, c.g, c.b, fade * fade * 0.8f);
            orb.trail[t].localScale = Vector3.one * (0.78f * fade + 0.12f);
        }
    }

    private void Paint(Orb orb)
    {
        // Baş: taşın rengi; çekirdek: beyaza yakın sıcak nokta — küçük boyutta da "enerji" okunur.
        orb.headImage.color = orb.color;
        orb.coreImage.color = Color.Lerp(orb.color, Color.white, 0.75f);
        for (int i = 0; i < orb.trailImages.Length; i++)
            orb.trailImages[i].color = new Color(orb.color.r, orb.color.g, orb.color.b, 0f);
    }

    private Orb CreateOrb()
    {
        int dots = Mathf.Max(1, tuning.trailDots);
        var orb = new Orb
        {
            trail = new RectTransform[dots],
            trailImages = new Image[dots],
            history = new Vector2[dots * Mathf.Max(1, tuning.trailStride) + 1],
        };

        // Kuyruk önce doğar ki başın ARKASINDA kalsın (UGUI'de sonraki kardeş üstte çizilir).
        for (int i = dots - 1; i >= 0; i--)
        {
            orb.trail[i] = CreateDot("PowerOrbTrail", tuning.headSize * 0.62f, overlay);
            orb.trailImages[i] = orb.trail[i].GetComponent<Image>();
        }
        orb.head = CreateDot("PowerOrb", tuning.headSize, overlay);
        orb.headImage = orb.head.GetComponent<Image>();
        var core = CreateDot("Core", tuning.headSize * 0.44f, orb.head);
        orb.coreImage = core.GetComponent<Image>();
        return orb;
    }

    private RectTransform CreateDot(string name, float size, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        BossDuelController.MatchParentLayer(go.transform);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = GetGlowSprite();
        img.raycastTarget = false;
        return rt;
    }

    private void Recycle(Orb orb)
    {
        if (orb.head != null) orb.head.gameObject.SetActive(false);
        for (int i = 0; i < orb.trailImages.Length; i++)
            if (orb.trailImages[i] != null)
            {
                var c = orb.trailImages[i].color;
                orb.trailImages[i].color = new Color(c.r, c.g, c.b, 0f);
            }
        pool.Push(orb);
    }

    private void OnDestroy()
    {
        foreach (var orb in live) DestroyOrb(orb);
        foreach (var orb in pool) DestroyOrb(orb);
        live.Clear();
        pool.Clear();
    }

    private static void DestroyOrb(Orb orb)
    {
        if (orb.head != null) Destroy(orb.head.gameObject);
        foreach (var t in orb.trail)
            if (t != null) Destroy(t.gameObject);
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static Vector2 WorldToAnchoredIn(RectTransform space, Vector3 worldPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            space, RectTransformUtility.WorldToScreenPoint(null, worldPos), null, out var local);
        return local;
    }

    // Yumuşak radyal parıltı — sprite bağımlılığı olmasın diye üretilir.
    private static Sprite glowSprite;

    private static Sprite GetGlowSprite()
    {
        if (glowSprite != null) return glowSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
            float t = Mathf.Clamp01(d / c);
            // Dolgun çekirdek + yumuşak hale: küçük boyutta bile net bir "top" okunur.
            float a = Mathf.Clamp01(1f - t * t * t) * (1f - t);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();

        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return glowSprite;
    }
}
