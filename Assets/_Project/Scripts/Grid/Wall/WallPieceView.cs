using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bir duvar parçasının görünümü (WallObstacleService yönetir). Tür (<see cref="WallKind"/>) sprite önekini
/// ve aşama→detay eşlemesini belirler.
///
/// Çizim (Resources/Wall/, kiremit.png'den pişirildi — Tools/WallBake):
///  - Her hücre 4 çeyrek: komşularına göre dış köşe / düz kenar / iç dolgu. "Komşu" = AYNI parçanın
///    hücresi; yalnız köşeden değen hücre bağlanmaz (diyagonal birleşme yok). Farklı parçalar yan yana
///    gelince iki kenar çizilir → aradaki oluk.
///  - İçe dönük köşe (iki kenar komşusu var, çaprazı yok): köşe noktasına ortalanmış *Concave_* sprite'ı.
///  - Detay: kiremit'in iç düz alanının %85'i boyutunda, alanın merkezinde; aşama 0'da rastgele
///    kabartma (+ayna), sonraki aşamalarda türün çatlak dokuları; fitili dışarıda olan çatlaklarda alev.
/// Katman sırası: çeyrekler → iç köşeler → detaylar → efektler. Yıkılmada her hücrenin üç katmandaki
/// grubu hücrenin patlama anında gizlenir; o hücre taş düşüşüne hemen açılır.
/// </summary>
public sealed class WallPieceView : MonoBehaviour
{
    // kiremit.png (1212 px hücre) kenar bantları — detayın oturduğu iç düz alan bunlardan.
    private const float SrcCell = 1212f, RimL = 106f, RimR = 98f, RimT = 131f, RimB = 94f;
    private const float DetailScale = 0.85f;
    private const float BakedCellPx = 512f;     // pişmiş sprite'ların hücre çözünürlüğü (Concave boyutu buna göre)
    private const float QuadOverlapPx = 0.75f;  // çeyrekler arası kıl payı boşluk olmasın

    // Fitil uçları, çizimin sol-üst köşesine göre normalize.
    // KiremitCatlak2.png (1268x1240) ve KiremitCatlak3.png (1254x1254).
    private static readonly Vector2[] FuseTipsCrack2 =
    {
        new Vector2(1000f / 1268f, 185f / 1240f),
        new Vector2(1140f / 1268f, 300f / 1240f),
        new Vector2(1220f / 1268f, 490f / 1240f),
    };
    private static readonly Vector2[] FuseTipsCrack3 =
    {
        new Vector2(1030f / 1254f, 155f / 1254f),
        new Vector2(1190f / 1254f, 350f / 1254f),
        new Vector2(1220f / 1254f, 505f / 1254f),
    };
    // Kiremitcatlak4.png (1024x1024).
    private static readonly Vector2[] FuseTipsCrack4 =
    {
        new Vector2(835f / 1024f, 150f / 1024f),
        new Vector2(965f / 1024f, 305f / 1024f),
        new Vector2(990f / 1024f, 430f / 1024f),
    };
    // İleri aşamalarda alev büyür (son vuruşa yaklaşıldığını hissettirir).
    private const float Crack3FlameScale = 1.25f;
    private const float Crack4FlameScale = 1.4f;

    private sealed class CellParts
    {
        public int x, y;
        public RectTransform baseGroup, concaveGroup, detailGroup;
        public RectTransform detailRoot;     // detay dikdörtgeni (merkez + boyut); alevler bunun içinde
        public Image detail;                 // aşama 0'da kabartma, sonra çatlak dokuları
        public Sprite kabartma;
        public bool kabartmaFlipped;
        public string flameKey;
        public RectTransform flames;
        public Coroutine punch;
        public bool cellReleased;
    }

    private readonly Dictionary<int, CellParts> cells = new();
    private RectTransform baseLayer, concaveLayer, detailLayer, fxLayer;
    private WallKind kind;
    private int width;
    private float ts;
    private bool collapsing;

    private string P => kind.SpritePrefix;

    public static WallPieceView Create(RectTransform parent, int origin, List<int> cellIndices, int boardWidth, float tileSize, WallKind kind)
    {
        var go = new GameObject($"{kind.SpritePrefix}_{origin}", typeof(RectTransform));
        go.layer = parent.gameObject.layer;   // Screen Space Camera culling'i önle
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var view = go.AddComponent<WallPieceView>();
        view.kind = kind;
        view.Build(origin, cellIndices, boardWidth, tileSize);
        return view;
    }

    // ── Kurulum ─────────────────────────────────────────────────────────

    private void Build(int origin, List<int> cellIndices, int boardWidth, float tileSize)
    {
        width = boardWidth;
        ts = tileSize;
        baseLayer = MakeLayer("Base");
        concaveLayer = MakeLayer("Concave");
        detailLayer = MakeLayer("Detail");
        fxLayer = MakeLayer("Fx");

        var member = new HashSet<int>(cellIndices);
        bool Same(int cx, int cy) => cx >= 0 && cx < width && cy >= 0 && member.Contains(cy * width + cx);

        var rng = new System.Random(origin * 7919 + cellIndices.Count);

        foreach (int idx in cellIndices)
        {
            int x = idx % width, y = idx / width;
            var parts = new CellParts
            {
                x = x, y = y,
                baseGroup = MakeCellGroup(baseLayer, x, y),
                concaveGroup = MakeCellGroup(concaveLayer, x, y),
                detailGroup = MakeCellGroup(detailLayer, x, y),
            };
            cells[idx] = parts;

            // 4 çeyrek
            for (int q = 0; q < 4; q++)
            {
                int dx = (q & 1) == 0 ? -1 : 1;     // sol / sağ
                int dy = q < 2 ? -1 : 1;            // üst / alt (grid y aşağı artar)
                bool v = Same(x, y + dy);
                bool h = Same(x + dx, y);
                bool d = Same(x + dx, y + dy);

                Sprite sprite;
                if (!v && !h) sprite = WallSprites.Outer(P, dx, dy);
                else if (v && !h) sprite = WallSprites.Edge(P, dx < 0 ? "Left" : "Right");
                else if (h && !v) sprite = WallSprites.Edge(P, dy < 0 ? "Top" : "Bottom");
                else sprite = WallSprites.Inner(P);

                float half = ts * 0.5f + QuadOverlapPx;
                var img = MakeImage(parts.baseGroup, sprite, new Vector2(dx * ts * 0.25f, -dy * ts * 0.25f), new Vector2(half, half));
                img.name = "Q" + q;

                if (v && h && !d)
                {
                    var cs = WallSprites.Concave(P, dx, dy);
                    if (cs != null)
                    {
                        float size = cs.rect.width / BakedCellPx * ts;
                        MakeImage(parts.concaveGroup, cs, new Vector2(dx * ts * 0.5f, -dy * ts * 0.5f), new Vector2(size, size)).name = "Concave" + q;
                    }
                }
            }

            // Detay (kabartma)
            var (center, detailSize) = DetailRect();
            parts.detailRoot = MakeRect(parts.detailGroup, "DetailRoot", center, detailSize);
            bool pick1 = rng.NextDouble() < 0.5;          // rng sırası türden bağımsız kalsın
            parts.kabartmaFlipped = rng.NextDouble() < 0.5;
            parts.kabartma = kind.PlainBase ? null : WallSprites.Detail(P, pick1 ? "Kabartma1" : "Kabartma2");
            parts.detail = MakeImage(parts.detailRoot, parts.kabartma, Vector2.zero, detailSize);
            parts.detail.name = "Detail";
            parts.detail.enabled = parts.kabartma != null;   // düz duvar: aşama 0'da detay görünmez
            if (parts.kabartmaFlipped)
                parts.detail.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    // Detay dikdörtgeni (hücre merkezine göre): kiremit iç düz alanının %85'i, alanın merkezinde.
    private (Vector2 center, Vector2 size) DetailRect()
    {
        if (kind.DetailFullCell) return (Vector2.zero, new Vector2(ts, ts));
        float u0 = RimL / SrcCell, u1 = 1f - RimR / SrcCell;
        float v0 = RimT / SrcCell, v1 = 1f - RimB / SrcCell;
        var center = new Vector2(((u0 + u1) * 0.5f - 0.5f) * ts, (0.5f - (v0 + v1) * 0.5f) * ts);
        var size = new Vector2((u1 - u0) * DetailScale * ts, (v1 - v0) * DetailScale * ts);
        return (center, size);
    }

    // ── Aşama ───────────────────────────────────────────────────────────

    public void SetCellStage(int cellIndex, int stage)
    {
        if (collapsing || !cells.TryGetValue(cellIndex, out var parts) || parts.detail == null) return;

        string detailName = stage > 0 && stage < kind.StageDetails.Length ? kind.StageDetails[stage] : null;
        var crack = detailName != null ? WallSprites.Detail(P, detailName) : null;

        if (crack != null)
        {
            parts.detail.sprite = crack;
            parts.detail.enabled = true;
            parts.detail.rectTransform.localScale = Vector3.one;   // çatlaklar aynalanmaz (fitil uçları sabit)
        }
        else
        {
            // Aşama 0 (metal duvarda boş geçilen hamle sonrası): kabartmalı duvar geri gelir.
            parts.detail.sprite = parts.kabartma;
            parts.detail.enabled = parts.kabartma != null;
            parts.detail.rectTransform.localScale = new Vector3(parts.kabartmaFlipped ? -1f : 1f, 1f, 1f);
        }
        SetFlames(parts, detailName);

        if (stage > 0)
        {
            if (parts.punch != null) StopCoroutine(parts.punch);
            parts.punch = StartCoroutine(Punch(parts.detailGroup));
        }
    }

    // Fitili dışarıda olan çatlaklarda (Crack2/3/4) uçlarda alev; doku değişince uçlar yeniden kurulur.
    private void SetFlames(CellParts parts, string detailName)
    {
        Vector2[] tips = detailName != null && kind.FuseTips != null
            ? (kind.FuseTips.TryGetValue(detailName, out var own) ? own : null)
            : detailName switch
        {
            "Crack2" => FuseTipsCrack2,
            "Crack3" => FuseTipsCrack3,
            "Crack4" => FuseTipsCrack4,
            _ => null,
        };
        string key = tips != null ? detailName : null;
        if (key == parts.flameKey) return;
        if (parts.flames != null) Destroy(parts.flames.gameObject);
        float scale = detailName == "Crack4" ? Crack4FlameScale : detailName == "Crack3" ? Crack3FlameScale : 1f;
        parts.flames = tips == null ? null : SpawnFlames(parts.detailRoot, tips, scale);
        parts.flameKey = key;
    }

    private RectTransform SpawnFlames(RectTransform detail, Vector2[] tipsUv, float flameScale)
    {
        var holder = new GameObject("Flames", typeof(RectTransform));
        holder.layer = gameObject.layer;
        var hrt = (RectTransform)holder.transform;
        hrt.SetParent(detail, false);
        hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = Vector2.zero;

        Vector2 size = detail.sizeDelta;
        for (int i = 0; i < tipsUv.Length; i++)
        {
            var uv = tipsUv[i];
            var pos = new Vector2((uv.x - 0.5f) * size.x, (0.5f - uv.y) * size.y);
            WallFuseFlame.Create(hrt, pos, ts * flameScale, i * 0.37f);
        }
        return hrt;
    }

    private static IEnumerator Punch(RectTransform target)
    {
        const float dur = 0.2f;
        float t = 0f;
        while (t < dur && target != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float s = 1f + 0.14f * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.3f);
            target.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (target != null) target.localScale = Vector3.one;
    }

    // ── Yıkılma ─────────────────────────────────────────────────────────

    /// Tetik hücreden yayılan zincir: her hücre kendi patlamasında görünümünü ve kilidini bırakır.
    /// onCollapseStart ilk patlama anında, onFinished son hücre açıldığında çağrılır.
    /// Erken iptal de onFinished'i bir kez çağırır; dekoratif döküntü hücreleri tutmaz.
    public void PlayCollapse(int triggerCell, System.Action<int, bool> onBlast = null,
        System.Action onCollapseStart = null, System.Action onFinished = null,
        System.Action<int> onCellVacated = null)
    {
        if (collapsing) return;
        collapsing = true;
        finishCallback = onFinished;
        cellVacatedCallback = onCellVacated;
        effects.Capacity = 128; // Büyük parçacık tamponunu yalnız yıkılacak duvara ayır.
        StartCoroutine(CollapseRoutine(triggerCell, onBlast, onCollapseStart));
    }

    private System.Action finishCallback;
    private System.Action<int> cellVacatedCallback;

    private void Finish()
    {
        var cb = finishCallback;
        finishCallback = null;
        cellVacatedCallback = null;
        cb?.Invoke();
    }

    private void OnDestroy()
    {
        ReleaseAllEffects();
        Finish();
    }

    private void OnDisable()
    {
        ReleaseAllEffects();
        if (!collapsing) return;
        StopAllCoroutines();
        Finish();
        Destroy(gameObject);
    }

    private const float CollapseTimeScale = 0.8f;
    private const float BlastInterval = 0.12f * CollapseTimeScale;
    private const float WaveMax = 1.0f * CollapseTimeScale;
    private const float BlastIntervalMin = 0.05f * CollapseTimeScale;
    private const float TriggerBlastScale = 1.35f, CellBlastScale = 1f;

    private IEnumerator CollapseRoutine(int triggerCell, System.Action<int, bool> onBlast, System.Action onCollapseStart)
    {
        var ring = RingDistances(triggerCell);
        var order = new List<int>(cells.Keys);
        var tieBreak = new Dictionary<int, float>();
        foreach (int c in order) tieBreak[c] = Random.value;
        order.Sort((a, b) =>
        {
            if (a == b) return 0;
            int ra = ring.TryGetValue(a, out int x) ? x : int.MaxValue;
            int rb = ring.TryGetValue(b, out int y) ? y : int.MaxValue;
            if (a == triggerCell) return -1;
            if (b == triggerCell) return 1;
            return ra != rb ? ra.CompareTo(rb) : tieBreak[a].CompareTo(tieBreak[b]);
        });

        float interval = order.Count > 1
            ? Mathf.Max(BlastIntervalMin, Mathf.Min(BlastInterval, WaveMax / (order.Count - 1)))
            : 0f;
        float elapsed = 0f;
        onCollapseStart?.Invoke();
        // Tek zamanlayıcı: bekleyen her hücre için coroutine / kare başına sütun taraması yok.
        for (int n = 0; n < order.Count; n++)
        {
            while (elapsed < n * interval)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            int cell = order[n];
            BlastCell(cells[cell], cell, n == 0, onBlast);
        }
        Finish();
        // Pooled efektlerin kendi ömürleri bitsin; son duman sabit bir timeout ile kesilmesin.
        while (effects.Count > 0) yield return null;
        Destroy(gameObject);
    }

    private void BlastCell(CellParts parts, int cellIndex, bool isTrigger, System.Action<int, bool> onBlast)
    {
        if (parts.cellReleased) return;
        parts.cellReleased = true;
        if (parts.punch != null) { StopCoroutine(parts.punch); parts.punch = null; }
        // Önce gerçek gövdeyi kaldır, sonra kilidi bırak. Komşu hücreler patlayana kadar yerinde kalır.
        if (parts.baseGroup != null) parts.baseGroup.gameObject.SetActive(false);
        if (parts.concaveGroup != null) parts.concaveGroup.gameObject.SetActive(false);
        if (parts.detailGroup != null) parts.detailGroup.gameObject.SetActive(false);
        if (parts.flames != null) parts.flames.gameObject.SetActive(false);
        Vector2 center = CellCenter(parts.x, parts.y);
        SpawnBigBlast(center, isTrigger ? TriggerBlastScale : CellBlastScale);
        SpawnDebris(center, isTrigger ? 9 : 6);
        cellVacatedCallback?.Invoke(cellIndex);
        onBlast?.Invoke(cellIndex, isTrigger);
    }

    // Tetik hücreden parça içi BFS halka mesafesi (yalnız kenar komşuluğu).
    private Dictionary<int, int> RingDistances(int start)
    {
        var dist = new Dictionary<int, int>();
        if (!cells.ContainsKey(start))
            foreach (var k in cells.Keys) { start = k; break; }
        var queue = new Queue<int>();
        dist[start] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            int cx = c % width, cy = c / width;
            for (int i = 0; i < 4; i++)
            {
                int nx = cx + (i == 0 ? -1 : i == 1 ? 1 : 0);
                int ny = cy + (i == 2 ? -1 : i == 3 ? 1 : 0);
                if (nx < 0 || nx >= width || ny < 0) continue;
                int n = ny * width + nx;
                if (!cells.ContainsKey(n) || dist.ContainsKey(n)) continue;
                dist[n] = dist[c] + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    private const string FxPoolKey = "Wall.Collapse";
    private enum FxKind { Blast, Dust, Debris }
    private struct FxParticle
    {
        public Image image;
        public RectTransform rect;
        public Color tint;
        public Vector2 velocity;
        public float elapsed, life, from, to, spin;
        public FxKind kind;
    }
    private readonly List<FxParticle> effects = new();

    private void TrackFx(Image image, FxKind kind, float life, Vector2 velocity = default,
        float from = 1f, float to = 1f, float spin = 0f)
    {
        effects.Add(new FxParticle { image = image, rect = image.rectTransform, tint = image.color,
            kind = kind, life = life, velocity = velocity, from = from, to = to, spin = spin });
        if (kind == FxKind.Blast) image.rectTransform.localScale = new Vector3(from, from, 1f);
    }

    // Bir duvarın tüm pooled parçacıkları tek döngüde; parçacık başına coroutine yok.
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            var fx = effects[i];
            fx.elapsed += dt;
            if (fx.image == null || fx.elapsed >= fx.life)
            {
                ReleaseEffect(fx);
                int last = effects.Count - 1;
                effects[i] = effects[last];
                effects.RemoveAt(last);
                continue;
            }
            float k = fx.elapsed / fx.life;
            var color = fx.tint;
            if (fx.kind == FxKind.Blast)
            {
                float s = Mathf.Lerp(fx.from, fx.to, 1f - (1f - k) * (1f - k));
                fx.rect.localScale = new Vector3(s, s, 1f);
                color.a *= 1f - k;
            }
            else if (fx.kind == FxKind.Dust)
            {
                fx.rect.anchoredPosition += fx.velocity * dt * (1f - k);
                float s = 1f + 0.9f * k;
                fx.rect.localScale = new Vector3(s, s * 0.8f, 1f);
                color.a *= 1f - k;
            }
            else
            {
                fx.velocity.y -= ts * 9f * dt;
                fx.rect.anchoredPosition += fx.velocity * dt;
                fx.rect.localEulerAngles += new Vector3(0f, 0f, fx.spin * dt);
                color.a *= 1f - Mathf.Clamp01((k - 0.5f) / 0.5f);
            }
            fx.image.color = color;
            effects[i] = fx;
        }
    }

    private static void ReleaseEffect(FxParticle fx)
    {
        if (fx.image == null) return;
        fx.image.sprite = null;
        UiVfxPool.Return(FxPoolKey, fx.image.gameObject, 256);
    }

    private void ReleaseAllEffects()
    {
        foreach (var fx in effects) ReleaseEffect(fx);
        effects.Clear();
    }

    // Görünür patlama: şok halkası + ateş topu + beyaz çekirdek + kıvılcımlar + yükselen duman.
    private void SpawnBigBlast(Vector2 center, float scale)
    {
        var ringImg = MakeImage(fxLayer, WallSprites.ShockRing, center, Vector2.one * ts * scale);
        ringImg.color = new Color(1f, 0.95f, 0.75f, 0.95f);
        TrackFx(ringImg, FxKind.Blast, 0.36f, from: 0.3f, to: 2.6f);

        var fire = MakeImage(fxLayer, WallSprites.SoftCircle, center, Vector2.one * ts * scale);
        fire.color = new Color(1f, 0.55f, 0.12f, 1f);
        TrackFx(fire, FxKind.Blast, 0.42f, from: 0.35f, to: 1.9f);

        var core = MakeImage(fxLayer, WallSprites.SoftCircle, center, Vector2.one * ts * 0.75f * scale);
        core.color = new Color(1f, 1f, 0.85f, 1f);
        TrackFx(core, FxKind.Blast, 0.24f, from: 0.25f, to: 1.4f);

        for (int i = 0; i < 8; i++)
        {
            var spark = MakeImage(fxLayer, WallSprites.SoftCircle, center, Vector2.one * ts * 0.09f * scale);
            spark.color = new Color(1f, Random.Range(0.75f, 0.95f), 0.35f, 1f);
            float a = (i / 8f + Random.Range(-0.05f, 0.05f)) * Mathf.PI * 2f;
            var vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ts * Random.Range(3.5f, 5.5f) * scale;
            TrackFx(spark, FxKind.Debris, Random.Range(0.25f, 0.4f), vel);
        }

        for (int i = 0; i < 4; i++)
        {
            var smoke = MakeImage(fxLayer, WallSprites.SoftCircle,
                center + Random.insideUnitCircle * ts * 0.2f, Vector2.one * ts * Random.Range(0.55f, 0.8f) * scale);
            smoke.color = new Color(0.42f, 0.4f, 0.38f, 0.55f);
            TrackFx(smoke, FxKind.Dust, Random.Range(0.7f, 0.95f),
                new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.1f)) * ts);
        }
    }

    private void SpawnDebris(Vector2 center, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float size = ts * Random.Range(0.1f, 0.2f);
            var chip = MakeImage(fxLayer, WallSprites.Inner(P), center + Random.insideUnitCircle * ts * 0.25f, new Vector2(size, size * Random.Range(0.7f, 1.1f)));
            chip.color = Color.Lerp(Color.white, new Color(0.75f, 0.75f, 0.75f, 1f), Random.value);
            Vector2 vel = new Vector2(Random.Range(-1f, 1f), Random.Range(0.4f, 1.3f)) * ts * 2.4f;
            TrackFx(chip, FxKind.Debris, Random.Range(0.45f, 0.65f), vel, spin: Random.Range(-540f, 540f));
        }
    }

    // ── Yardımcılar ─────────────────────────────────────────────────────

    private Vector2 CellCenter(int x, int y) => new Vector2(x * ts + ts * 0.5f, -(y * ts + ts * 0.5f));

    private RectTransform MakeLayer(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    // Hücre grubu: pivot hücre merkezinde; patlamada üç katman birlikte gizlenir.
    private RectTransform MakeCellGroup(RectTransform layer, int x, int y)
    {
        var go = new GameObject($"C_{x}_{y}", typeof(RectTransform));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(layer, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = CellCenter(x, y);
        rt.sizeDelta = new Vector2(ts, ts);
        return rt;
    }

    private RectTransform MakeRect(RectTransform parent, string name, Vector2 localCenter, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localCenter;
        rt.sizeDelta = size;
        return rt;
    }

    private Image MakeImage(RectTransform parent, Sprite sprite, Vector2 localCenter, Vector2 size)
    {
        Image img;
        if (parent == fxLayer)
            img = UiVfxPool.RentImage(FxPoolKey, parent, "WallFx");
        else
        {
            var go = new GameObject("Img", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            img = go.GetComponent<Image>();
        }
        img.gameObject.layer = gameObject.layer;
        var rt = img.rectTransform;
        // Hücre grubunda merkez-ankrajlı; katman köküne doğrudan konanlar (fx) sol-üst ankrajlı.
        bool inCell = parent != fxLayer;
        rt.anchorMin = rt.anchorMax = inCell ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localCenter;
        rt.sizeDelta = size;
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = false;
        return img;
    }
}

/// Duvar sprite'ları (Resources/Wall, tür önekiyle) + prosedürel şekiller. Tembel yükleme, oturum boyu önbellek.
public static class WallSprites
{
    private static readonly Dictionary<string, Sprite> cache = new();
    private static Sprite softCircle, shockRing;

    private static Sprite Load(string name)
    {
        if (cache.TryGetValue(name, out var s)) return s;
        s = Resources.Load<Sprite>("Wall/" + name);
        if (s == null) Debug.LogWarning($"[Wall] Sprite bulunamadı: Resources/Wall/{name}");
        cache[name] = s;
        return s;
    }

    private static string Corner(int dx, int dy) => (dy < 0 ? "T" : "B") + (dx < 0 ? "L" : "R");

    public static Sprite Outer(string prefix, int dx, int dy) => Load(prefix + "Q_Outer" + Corner(dx, dy));
    public static Sprite Concave(string prefix, int dx, int dy) => Load(prefix + "Concave_" + Corner(dx, dy));
    public static Sprite Edge(string prefix, string side) => Load(prefix + "Q_Edge" + side);
    public static Sprite Inner(string prefix) => Load(prefix + "Q_Inner");
    public static Sprite Detail(string prefix, string name) => Load(prefix + "Detail_" + name);

    /// Şok dalgası halkası: yumuşak kenarlı ince halka.
    public static Sprite ShockRing
    {
        get
        {
            if (shockRing != null) return shockRing;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.14f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            shockRing = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return shockRing;
        }
    }

    public static Sprite SoftCircle
    {
        get
        {
            if (softCircle != null) return softCircle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            softCircle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return softCircle;
        }
    }
}
