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
/// grubu birlikte düşer (zincirleme patlama, sonra gövde yukarıdan aşağı çöker).
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
        public bool falling;                 // çöküş başladı → patlama sarsıntısı bırakır
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

    /// Parça veriden silindi: tetik hücrede büyük patlama → parça içinde halka halka zincirleme patlama →
    /// taban çöker, üstündekiler neredeyse aynı anda aşağı inip tabandaki yığına basılır; tabanda toz
    /// kalkar. Toplam ~0.5 sn (veri anında boşaldığı için taşlar hemen düşmeye başlar). Bitince yok olur.
    /// onCollapseStart: patlamadan sonra yığılma başladığı an (yığılma sesi için).
    /// onBlast(cellIndex, isTrigger): her patlama anı (ses/sarsıntı). onCollapseStart: zincir bitip çöküş
    /// başladığı an (yığılma sesi). onFinished: görünüm tamamen bitti (hücre tutma/iş serbest) — görünüm erken
    /// yok edilse de (level yeniden kurulumu) bir kez çağrılır.
    public void PlayCollapse(int triggerCell, System.Action<int, bool> onBlast = null,
        System.Action onCollapseStart = null, System.Action onFinished = null)
    {
        if (collapsing) return;
        collapsing = true;
        finishCallback = onFinished;
        StartCoroutine(CollapseRoutine(triggerCell, onBlast, onCollapseStart));
    }

    private System.Action finishCallback;

    private void Finish()
    {
        var cb = finishCallback;
        finishCallback = null;
        cb?.Invoke();
    }

    private void OnDestroy() => Finish();

    // Kontrollü yıkım (TV'deki bina yıkımı gibi): hücreler TEK TEK "pat pat" patlar, sonra gövde çöker.
    // Süre parça boyutuyla orantılı: her patlama arası sabit; çok büyük parçada zincir WaveMax'a sıkışır.
    private const float BlastInterval = 0.12f;
    private const float WaveMax = 1.0f;
    private const float BlastIntervalMin = 0.05f;
    private const float WaveToFall = 0.12f;                     // son patlamadan çöküşe kadar
    private const float RowDelay = 0.018f;                      // çöküşte sütunda bir üst hücrenin gecikmesi
    private const float SquashTime = 0.22f;
    private const float PileStep = 0.16f;                       // yığında her hücrenin bıraktığı yükseklik (hücre oranı)
    private const float TriggerBlastScale = 1.35f, CellBlastScale = 1f;

    private IEnumerator CollapseRoutine(int triggerCell, System.Action<int, bool> onBlast, System.Action onCollapseStart)
    {
        // 1) Patlama sırası: tetik hücre önce, sonra parça içinde yayılarak (halka sırası, halka içi rastgele).
        var ring = RingDistances(triggerCell);
        var order = new List<int>(cells.Keys);
        var tieBreak = new Dictionary<int, float>();
        foreach (int c in order) tieBreak[c] = Random.value;
        order.Sort((a, b) =>
        {
            int ra = ring.TryGetValue(a, out int x) ? x : 0, rb = ring.TryGetValue(b, out int y) ? y : 0;
            if (a == triggerCell) return -1;
            if (b == triggerCell) return 1;
            return ra != rb ? ra.CompareTo(rb) : tieBreak[a].CompareTo(tieBreak[b]);
        });

        float interval = order.Count > 1
            ? Mathf.Max(BlastIntervalMin, Mathf.Min(BlastInterval, WaveMax / (order.Count - 1)))
            : 0f;
        for (int n = 0; n < order.Count; n++)
        {
            bool isTrigger = n == 0;
            StartCoroutine(CellBlast(cells[order[n]], order[n], n * interval, isTrigger, onBlast));
        }
        float fallStart = (order.Count - 1) * interval + WaveToFall;

        // 2) Çöküş: taban çöker, üstündekiler neredeyse aynı anda aşağı iner ve tabandaki yığına basılır.
        var bottomByColumn = new Dictionary<int, int>();
        foreach (var p in cells.Values)
            if (!bottomByColumn.TryGetValue(p.x, out int b) || p.y > b) bottomByColumn[p.x] = p.y;

        float longest = 0f;
        foreach (var p in cells.Values)
        {
            int bottom = bottomByColumn[p.x];
            int k = bottom - p.y;                                  // tabandan kaçıncı hücre
            float delay = fallStart + k * RowDelay;
            float fallDist = k * ts * (1f - PileStep);
            float fallTime = k == 0 ? 0f : 0.09f + 0.07f * Mathf.Sqrt(k);
            bool isBase = k == 0;
            StartCoroutine(CollapseCell(p, delay, fallDist, fallTime, isBase, bottom));
            longest = Mathf.Max(longest, delay + fallTime + SquashTime);
        }

        float t = 0f;
        while (t < fallStart) { t += Time.unscaledDeltaTime; yield return null; }
        onCollapseStart?.Invoke();

        while (t < longest + 0.15f) { t += Time.unscaledDeltaTime; yield return null; }
        Finish();                                   // hücreler serbest: taşlar dolabilir
        while (t < longest + 0.8f) { t += Time.unscaledDeltaTime; yield return null; }   // duman/döküntü sönsün
        Destroy(gameObject);
    }

    private IEnumerator CellBlast(CellParts parts, int cellIndex, float delay, bool isTrigger, System.Action<int, bool> onBlast)
    {
        float t = 0f;
        while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
        Vector2 center = CellCenter(parts.x, parts.y);
        float scale = isTrigger ? TriggerBlastScale : CellBlastScale;
        SpawnBigBlast(center, scale);
        SpawnDebris(center, isTrigger ? 9 : 6);
        onBlast?.Invoke(cellIndex, isTrigger);
        if (parts.flames != null) { Destroy(parts.flames.gameObject); parts.flames = null; }
        Char(parts);
        yield return Kick(parts, isTrigger ? 0.18f : 0.12f);
    }

    // Patlayan hücre isle kararır (yanık), çöküşe kadar öyle kalır.
    private static void Char(CellParts parts)
    {
        var tint = new Color(0.62f, 0.58f, 0.55f, 1f);
        foreach (var g in new[] { parts.baseGroup, parts.concaveGroup, parts.detailGroup })
        {
            if (g == null) continue;
            foreach (var img in g.GetComponentsInChildren<Image>(true))
                if (img.GetComponent<WallFuseFlame>() == null) img.color = tint * new Color(1f, 1f, 1f, img.color.a);
        }
    }

    // Patlama darbesi: hücre grubu kısa bir an şişip titrer (çöküş başlayınca CollapseCell devralır).
    private IEnumerator Kick(CellParts parts, float amount)
    {
        const float dur = 0.12f;
        var groups = new[] { parts.baseGroup, parts.concaveGroup, parts.detailGroup };
        Vector2 home = CellCenter(parts.x, parts.y);
        float t = 0f;
        while (t < dur)
        {
            if (parts.falling) yield break;   // çöküş devraldı
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float s = 1f + amount * Mathf.Sin(k * Mathf.PI);
            var jitter = Random.insideUnitCircle * ts * 0.03f * (1f - k);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null) continue;
                groups[i].localScale = new Vector3(s, s, 1f);
                groups[i].anchoredPosition = home + jitter;
            }
            yield return null;
        }
        if (parts.falling) yield break;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null) continue;
            groups[i].localScale = Vector3.one;
            groups[i].anchoredPosition = home;
        }
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
            int[] nx = { cx - 1, cx + 1, cx, cx };
            int[] ny = { cy, cy, cy - 1, cy + 1 };
            for (int i = 0; i < 4; i++)
            {
                if (nx[i] < 0 || nx[i] >= width || ny[i] < 0) continue;
                int n = ny[i] * width + nx[i];
                if (!cells.ContainsKey(n) || dist.ContainsKey(n)) continue;
                dist[n] = dist[c] + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    private IEnumerator CollapseCell(CellParts parts, float delay, float fallDist, float fallTime, bool isBase, int bottomRow)
    {
        float t = 0f;
        while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }

        parts.falling = true;
        var groups = new[] { parts.baseGroup, parts.concaveGroup, parts.detailGroup };
        var fades = new CanvasGroup[groups.Length];
        var starts = new Vector2[groups.Length];
        Vector2 home = CellCenter(parts.x, parts.y);   // sarsıntı ofsetinden bağımsız gerçek merkez
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null) continue;
            fades[i] = groups[i].gameObject.AddComponent<CanvasGroup>();
            fades[i].interactable = false;
            fades[i].blocksRaycasts = false;
            starts[i] = home;
        }

        void Apply(Vector2 offset, float sx, float sy, float rot, float alpha)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null) continue;
                groups[i].anchoredPosition = starts[i] + offset;
                groups[i].localScale = new Vector3(sx, sy, 1f);
                groups[i].localEulerAngles = new Vector3(0f, 0f, rot);
                fades[i].alpha = alpha;
            }
        }

        // Düşüş (taban hücre düşmez, yerinde ezilir): yerçekimi gibi hızlanır, hafif yalpalar.
        float tilt = Random.Range(-7f, 7f);
        t = 0f;
        while (t < fallTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fallTime);
            Apply(new Vector2(0f, -fallDist * k * k), 1f, 1f, tilt * k, 1f);
            yield return null;
        }

        // Çarpma anı: taban hücrede toz bulutu, her hücrede döküntü.
        Vector2 land = CellCenter(parts.x, parts.y) + new Vector2(0f, -fallDist);
        if (isBase) SpawnDust(new Vector2(land.x, -(bottomRow * ts + ts)));
        SpawnDebris(land, isBase ? 5 : 3);

        // Ezilme: alt kenarı sabit kalarak basıklaşır, yanlara yayılır, söner.
        t = 0f;
        while (t < SquashTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / SquashTime);
            float e = 1f - (1f - k) * (1f - k);
            float sy = Mathf.Lerp(1f, 0.22f, e);
            float sx = Mathf.Lerp(1f, 1.22f, e);
            float sink = (1f - sy) * ts * 0.5f;                  // pivot merkezde → alt kenarı yerinde tut
            float alpha = 1f - Mathf.Clamp01((k - 0.35f) / 0.65f);
            Apply(new Vector2(0f, -fallDist - sink), sx, sy, tilt * (1f - e), alpha);
            yield return null;
        }
        foreach (var g in groups)
            if (g != null) g.gameObject.SetActive(false);
    }

    // Taban hizasında yanlara açılan toz bulutu.
    private void SpawnDust(Vector2 groundCenter)
    {
        for (int i = 0; i < 5; i++)
        {
            var puff = MakeImage(fxLayer, WallSprites.SoftCircle,
                groundCenter + new Vector2(Random.Range(-0.35f, 0.35f) * ts, Random.Range(0f, 0.12f) * ts),
                Vector2.one * ts * Random.Range(0.45f, 0.7f));
            puff.color = new Color(0.86f, 0.8f, 0.7f, 0.75f);
            float dir = i % 2 == 0 ? -1f : 1f;
            StartCoroutine(Dust(puff, new Vector2(dir * Random.Range(0.4f, 0.9f) * ts, Random.Range(0.15f, 0.35f) * ts),
                Random.Range(0.45f, 0.65f)));
        }
    }

    private static IEnumerator Dust(Image img, Vector2 vel, float life)
    {
        var rt = img.rectTransform;
        Color c = img.color;
        float t = 0f;
        while (t < life && img != null)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float k = t / life;
            rt.anchoredPosition += vel * dt * (1f - k);
            float s = 1f + 0.9f * k;
            rt.localScale = new Vector3(s, s * 0.8f, 1f);
            img.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }

    // Görünür patlama: şok halkası + ateş topu + beyaz çekirdek + kıvılcımlar + yükselen duman.
    private void SpawnBigBlast(Vector2 center, float scale)
    {
        var ringImg = MakeImage(fxLayer, WallSprites.ShockRing, center, Vector2.one * ts * scale);
        ringImg.color = new Color(1f, 0.95f, 0.75f, 0.95f);
        StartCoroutine(Blast(ringImg, 0.3f, 2.6f, 0.36f));

        var fire = MakeImage(fxLayer, WallSprites.SoftCircle, center, Vector2.one * ts * scale);
        fire.color = new Color(1f, 0.55f, 0.12f, 1f);
        StartCoroutine(Blast(fire, 0.35f, 1.9f, 0.42f));

        var core = MakeImage(fxLayer, WallSprites.SoftCircle, center, Vector2.one * ts * 0.75f * scale);
        core.color = new Color(1f, 1f, 0.85f, 1f);
        StartCoroutine(Blast(core, 0.25f, 1.4f, 0.24f));

        for (int i = 0; i < 8; i++)
        {
            var spark = MakeImage(fxLayer, WallSprites.SoftCircle, center, Vector2.one * ts * 0.09f * scale);
            spark.color = new Color(1f, Random.Range(0.75f, 0.95f), 0.35f, 1f);
            float a = (i / 8f + Random.Range(-0.05f, 0.05f)) * Mathf.PI * 2f;
            var vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ts * Random.Range(3.5f, 5.5f) * scale;
            StartCoroutine(Debris(spark, vel, 0f, Random.Range(0.25f, 0.4f)));
        }

        for (int i = 0; i < 4; i++)
        {
            var smoke = MakeImage(fxLayer, WallSprites.SoftCircle,
                center + Random.insideUnitCircle * ts * 0.2f, Vector2.one * ts * Random.Range(0.55f, 0.8f) * scale);
            smoke.color = new Color(0.42f, 0.4f, 0.38f, 0.55f);
            StartCoroutine(Dust(smoke, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.1f)) * ts,
                Random.Range(0.7f, 0.95f)));
        }
    }

    private static IEnumerator Blast(Image img, float from, float to, float dur)
    {
        var rt = img.rectTransform;
        Color c = img.color;
        float t = 0f;
        while (t < dur && img != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k));
            rt.localScale = new Vector3(s, s, 1f);
            img.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }

    private void SpawnDebris(Vector2 center, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float size = ts * Random.Range(0.1f, 0.2f);
            var chip = MakeImage(fxLayer, WallSprites.Inner(P), center + Random.insideUnitCircle * ts * 0.25f, new Vector2(size, size * Random.Range(0.7f, 1.1f)));
            chip.color = Color.Lerp(Color.white, new Color(0.75f, 0.75f, 0.75f, 1f), Random.value);
            Vector2 vel = new Vector2(Random.Range(-1f, 1f), Random.Range(0.4f, 1.3f)) * ts * 2.4f;
            StartCoroutine(Debris(chip, vel, Random.Range(-540f, 540f), Random.Range(0.45f, 0.65f)));
        }
    }

    private IEnumerator Debris(Image img, Vector2 vel, float spin, float life)
    {
        var rt = img.rectTransform;
        float gravity = ts * 9f;
        float t = 0f;
        while (t < life && img != null)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            vel.y -= gravity * dt;
            rt.anchoredPosition += vel * dt;
            rt.localEulerAngles += new Vector3(0f, 0f, spin * dt);
            var c = img.color;
            img.color = new Color(c.r, c.g, c.b, 1f - Mathf.Clamp01((t - life * 0.5f) / (life * 0.5f)));
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
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

    // Hücre grubu: pivot hücre merkezinde (yıkılmada kendi merkezi etrafında döner/küçülür).
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
        var go = new GameObject("Img", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        // Hücre grubunda merkez-ankrajlı; katman köküne doğrudan konanlar (fx) sol-üst ankrajlı.
        bool inCell = parent != fxLayer;
        rt.anchorMin = rt.anchorMax = inCell ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localCenter;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
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
