using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Brandalı heykel görünümü (ObstacleId.TarpStatue, 2x2). GridSpawner'ın çizdiği engel Image'ına takılır (LaserCannon
/// deseni): Image iç zemini (mavi kumaş) gösterir, katmanlar çocuktur — taş, branda, çerçeve, etiket + sayaç.
/// Mekanik TarpStatueService'te; burada yalnız görsel:
///   • sayaç geri sayar (toplanan taş etikete varınca),
///   • sayaç 0 → branda alttan yukarı soyulur, etiket ipten kopup düşer, taş blok görünür,
///   • taş vuruşu → aşama sprite'ı değişir, taştan parçalar kopup düşer,
///   • son vuruş → bitmiş heykel parlar, hedefe uçar (servis), çerçeve + zemin parçalanıp savrulur.
/// Sprite'lar Resources/TarpStatue/ (hepsi aynı kadraj, 512 px).
/// </summary>
public sealed class TarpStatueView : MonoBehaviour, ICollectionFxTarget
{
    private const string Dir = "TarpStatue/";
    private static readonly string[] TagNames = { "Tag_Red", "Tag_Yellow", "Tag_Blue", "Tag_Green" };
    private static readonly Color[] Tints =
    {
        new Color(1f, 0.25f, 0.3f), new Color(1f, 0.82f, 0.2f), new Color(0.3f, 0.6f, 1f), new Color(0.3f, 1f, 0.45f),
    };
    // Etiket plakasının merkezi (tuval oranı, UI y yukarı): sayı + toplama hedefi. Yükseklik plaka boyu.
    private static readonly Vector2 PlateCenter = new Vector2(0.5f, 1f - 0.4365f);
    private const float PlateHeight = 0.2f;
    private static readonly Color32 CounterDark = new Color32(70, 10, 10, 255);

    private const float PeelTime = 0.55f;
    private const int ChipsPerHit = 8;

    private Image baseImage;
    private RectTransform stone, tarp, frame, tagRoot, counterRoot;
    private Image stoneImage, tarpImage;
    private TextMeshProUGUI counter, counterShadow;
    private TarpStatueColor color;
    private bool covered;
    private bool peeling;
    private Coroutine punchCo;
    private Coroutine tagDropCo;
    private Graphic[] tagGraphics;
    private Vector2 tagStart;

    public TarpStatueColor TagColor => color;
    public bool IsCovered => covered;
    public Vector3 CenterWorld => baseImage.rectTransform.TransformPoint(baseImage.rectTransform.rect.center);

    /// GridSpawner çağırır: katmanları bir kez kurar, mevcut durumu çizer.
    public static TarpStatueView Ensure(Image image, TarpStatueColor color, int remainingCount, int remainingHits)
    {
        if (image == null) return null;
        if (!image.TryGetComponent<TarpStatueView>(out var view))
            view = image.gameObject.AddComponent<TarpStatueView>();
        view.Build(color);
        view.SetCountInstant(remainingCount);
        view.SetStoneStage(StageForRemaining(remainingHits));
        return view;
    }

    /// Kalan taş vuruşu → görünen taş: 3 kaba blok, 2 kaba aslan, 1 yarım oyulmuş, 0 bitmiş heykel.
    public static int StageForRemaining(int remainingHits) => Mathf.Clamp(4 - remainingHits, 1, 4);

    private void Build(TarpStatueColor tagColor)
    {
        color = tagColor;
        if (baseImage != null) return;
        baseImage = GetComponent<Image>();
        baseImage.sprite = Load("Background");
        baseImage.preserveAspect = false;

        stone = Layer("Stone", Load("Stone_1"));
        stoneImage = stone.GetComponent<Image>();
        tarp = Layer("Tarp", Load("Tarp"));
        tarpImage = tarp.GetComponent<Image>();
        tarpImage.type = Image.Type.Filled;
        tarpImage.fillMethod = Image.FillMethod.Vertical;
        tarpImage.fillOrigin = (int)Image.OriginVertical.Top;   // alt kenar yukarı kalkar
        frame = Layer("Frame", Load("Frame"));
        tagRoot = Layer("Tag", Load(TagNames[(int)tagColor]));

        // Sayaç: plaka merkezinde beyaz sayı + koyu kontur + ayrı gölge kopyası (mühür sayacıyla aynı okunurluk).
        var go = new GameObject("Counter", typeof(RectTransform));
        go.layer = gameObject.layer;
        counterRoot = (RectTransform)go.transform;
        counterRoot.SetParent(tagRoot, false);
        counterRoot.anchorMin = counterRoot.anchorMax = PlateCenter;
        counterRoot.pivot = new Vector2(0.5f, 0.5f);
        counterShadow = NewText("CounterShadow", CounterDark);
        counter = NewText("CounterText", new Color32(255, 255, 255, 255));
        LayoutCounter();
        tagGraphics = tagRoot.GetComponentsInChildren<Graphic>(true);
        tagStart = tagRoot.anchoredPosition;
        covered = true;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (counterRoot != null) LayoutCounter();
    }

    private void LayoutCounter()
    {
        var r = ((RectTransform)transform).rect;
        float h = r.height * PlateHeight;
        counterRoot.sizeDelta = new Vector2(h * 1.6f, h);
        counter.fontSize = counterShadow.fontSize = h * 0.62f;
        counterShadow.rectTransform.anchoredPosition = new Vector2(0f, -h * 0.05f);
    }

    // ── Branda evresi ────────────────────────────────────────────────

    private void SetCountInstant(int remaining)
    {
        if (tagDropCo != null) StopCoroutine(tagDropCo);
        tagDropCo = null;
        ResetTag();
        covered = remaining > 0;
        counter.text = counterShadow.text = Mathf.Max(0, remaining).ToString();
        tarp.gameObject.SetActive(covered);
        tagRoot.gameObject.SetActive(covered);
        if (covered)
        {
            tarpImage.fillAmount = 1f;
            tarp.localScale = Vector3.one;
            tarpImage.color = Color.white;
        }
    }

    /// Toplanan taş etikete vardı: sayı azalır + etiket nabız atar. 0 → branda soyulur.
    public void OnCollected(int remaining)
    {
        if (!covered) return;
        counter.text = counterShadow.text = Mathf.Max(0, remaining).ToString();
        if (punchCo != null) StopCoroutine(punchCo);
        punchCo = StartCoroutine(CoPunch(counterRoot, 1.25f, 0.16f));
        if (remaining <= 0)
            StartCoroutine(CoPeel());
    }

    private IEnumerator CoPeel()
    {
        covered = false;
        peeling = true;
        // Etiket ve sayacı birlikte yana sıçrayıp dönerek düşer.
        tagDropCo = StartCoroutine(CoDropTag());
        // Branda alt kenarından yukarı doğru toplanarak soyulur (dolgu kesilir + hafif yukarı çekilir).
        float lift = ((RectTransform)transform).rect.height * 0.06f;
        for (float t = 0f; t < PeelTime; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / PeelTime);
            float e = k * k * (3f - 2f * k);
            tarpImage.fillAmount = 1f - e;
            tarp.anchoredPosition = new Vector2(0f, lift * Mathf.Sin(k * Mathf.PI));
            tarp.localScale = new Vector3(1f + 0.04f * Mathf.Sin(k * Mathf.PI), 1f, 1f);
            yield return null;
        }
        tarp.gameObject.SetActive(false);
        peeling = false;
        StartCoroutine(CoPunch(stone, 1.06f, 0.2f));
    }

    private IEnumerator CoDropTag()
    {
        float h = ((RectTransform)transform).rect.height;
        float side = Random.value < 0.5f ? -1f : 1f;
        Vector2 velocity = new Vector2(side * h * Random.Range(1.25f, 1.65f), h * Random.Range(0.9f, 1.2f));
        float spin = -side * Random.Range(340f, 480f);
        const float life = 0.6f;
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            tagRoot.anchoredPosition = tagStart + velocity * t + Vector2.down * (0.5f * h * 4f * t * t);
            tagRoot.localRotation = Quaternion.Euler(0f, 0f, spin * t);
            float a = 1f - Mathf.Clamp01((t - life * 0.5f) / (life * 0.5f));
            foreach (var g in tagGraphics) { if (g == null) continue; var c = g.color; c.a = a; g.color = c; }
            yield return null;
        }
        tagRoot.gameObject.SetActive(false);
        ResetTag();
        tagDropCo = null;
    }

    private void ResetTag()
    {
        if (tagRoot == null) return;
        tagRoot.anchoredPosition = tagStart;
        tagRoot.localRotation = Quaternion.identity;
        if (tagGraphics == null) return;
        foreach (var g in tagGraphics)
        {
            if (g == null) continue;
            var c = g.color; c.a = 1f; g.color = c;
        }
    }

    private void OnDisable()
    {
        if (tagDropCo != null) StopCoroutine(tagDropCo);
        tagDropCo = null;
        ResetTag();
        if (tagRoot != null) tagRoot.gameObject.SetActive(covered);
    }

    // ── Taş evresi ───────────────────────────────────────────────────

    public void SetStoneStage(int stage)
    {
        var s = Load("Stone_" + Mathf.Clamp(stage, 1, 4));
        if (s != null && stoneImage.sprite != s) stoneImage.sprite = s;
    }

    /// Vuruş: aşama sprite'ı değişir, taştan parçalar kopar (vfxRoot'ta, tek döngü).
    public void OnStoneHit(int remainingHits, MonoBehaviour host, RectTransform vfxRoot)
    {
        SetStoneStage(StageForRemaining(remainingHits));
        StartCoroutine(CoPunch(stone, 1.05f, 0.14f));
        if (host != null && vfxRoot != null)
            host.StartCoroutine(CoChips(vfxRoot, ChipsPerHit, 1f));
    }

    private IEnumerator CoChips(RectTransform root, int count, float power)
    {
        var rt = (RectTransform)transform;
        float size = root.InverseTransformVector(rt.TransformVector(new Vector3(rt.rect.width, 0f, 0f))).magnitude;
        Vector2 center = root.InverseTransformPoint(stone.TransformPoint(stone.rect.center));
        var parts = new RectTransform[count];
        var imgs = new Image[count];
        var vel = new Vector2[count];
        var spin = new float[count];
        for (int i = 0; i < count; i++)
        {
            var chip = Load("Chip_" + Random.Range(0, 6));
            // Boy heykel kenarına oranlı (2x2'de ~0.45 hücre; 3x3/4x4'te kendiliğinden büyür).
            parts[i] = FlightTrailFx.CreateImage(root, "TarpStatueChip", chip, Color.white, size * Random.Range(0.17f, 0.27f));
            imgs[i] = parts[i].GetComponent<Image>();
            imgs[i].preserveAspect = true;
            Vector2 off = new Vector2(Random.Range(-0.28f, 0.28f), Random.Range(-0.05f, 0.25f)) * size;
            parts[i].localPosition = center + off;
            vel[i] = new Vector2(Mathf.Sign(off.x) * Random.Range(0.5f, 1.3f), Random.Range(0.9f, 1.7f)) * size * power;
            spin[i] = Random.Range(-540f, 540f);
        }
        const float life = 0.85f;
        float gravity = size * 4.5f;
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float dt = Time.deltaTime, fade = 1f - Mathf.Clamp01((t - life * 0.55f) / (life * 0.45f));
            for (int i = 0; i < count; i++)
            {
                if (parts[i] == null) continue;
                vel[i].y -= gravity * dt;
                parts[i].localPosition += (Vector3)(vel[i] * dt);
                parts[i].localRotation *= Quaternion.Euler(0f, 0f, spin[i] * dt);
                imgs[i].color = new Color(1f, 1f, 1f, fade);
            }
            yield return null;
        }
        for (int i = 0; i < count; i++)
            if (parts[i] != null) Destroy(parts[i].gameObject);
    }

    // ── Final ────────────────────────────────────────────────────────

    /// Son vuruş: bitmiş heykel görünür ve parlar. Heykel uçuşu servisin; bu yalnız ortaya çıkış (sn).
    public IEnumerator CoRevealStatue(float duration)
    {
        SetStoneStage(4);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / duration);
            stone.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        stone.localScale = Vector3.one;
    }

    public Sprite StatueSprite => stoneImage != null ? stoneImage.sprite : null;
    public RectTransform StoneRect => stone;

    /// Heykel uçmaya başladı: tahtadaki heykel gizlenir, çerçeve + zemin parçalanıp savrulur (vfxRoot).
    public IEnumerator CoShatter(RectTransform root)
    {
        stone.gameObject.SetActive(false);
        var rt = (RectTransform)transform;
        float size = root.InverseTransformVector(rt.TransformVector(new Vector3(rt.rect.width, 0f, 0f))).magnitude;
        Vector2 center = root.InverseTransformPoint(CenterWorld);
        var frameShards = ShardsFor(frame.GetComponent<Image>().sprite);
        var bgShards = ShardsFor(baseImage.sprite);
        frame.gameObject.SetActive(false);
        baseImage.enabled = false;

        int n = (frameShards?.Length ?? 0) + (bgShards?.Length ?? 0) + 8;
        var parts = new RectTransform[n];
        var imgs = new Image[n];
        var vel = new Vector2[n];
        var spin = new float[n];
        int idx = 0;
        void Add(Sprite s, Vector2 norm, float scale, float speed)
        {
            if (s == null) return;
            var p = FlightTrailFx.CreateImage(root, "TarpStatueShard", s, Color.white, size * scale);
            Vector2 off = (norm - new Vector2(0.5f, 0.5f)) * size;
            p.localPosition = center + off;
            var dir = (off.sqrMagnitude > 1f ? off.normalized : Random.insideUnitCircle.normalized) + Random.insideUnitCircle * 0.3f;
            vel[idx] = (dir.normalized + Vector2.up * 0.6f) * size * speed;
            spin[idx] = Random.Range(-500f, 500f);
            parts[idx] = p;
            imgs[idx] = p.GetComponent<Image>();
            imgs[idx].preserveAspect = true;
            idx++;
        }
        if (bgShards != null)
            for (int i = 0; i < bgShards.Length; i++) Add(bgShards[i], ShardCenters[i], 0.34f, Random.Range(0.8f, 1.3f));
        if (frameShards != null)
            for (int i = 0; i < frameShards.Length; i++) Add(frameShards[i], ShardCenters[i], 0.34f, Random.Range(1.3f, 2f));
        for (int i = 0; i < 8; i++)
            Add(Load("Chip_" + (i % 6)), new Vector2(0.5f, 0.45f) + Random.insideUnitCircle * 0.2f, Random.Range(0.17f, 0.26f), Random.Range(1.5f, 2.4f));

        const float life = 0.75f;
        float gravity = size * 4.5f;
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float dt = Time.deltaTime, k = t / life, fade = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            for (int i = 0; i < idx; i++)
            {
                if (parts[i] == null) continue;
                vel[i].y -= gravity * dt;
                vel[i] *= 1f - 0.5f * dt;
                parts[i].localPosition += (Vector3)(vel[i] * dt);
                parts[i].localRotation *= Quaternion.Euler(0f, 0f, spin[i] * dt);
                parts[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, k);
                imgs[i].color = new Color(1f, 1f, 1f, fade);
            }
            yield return null;
        }
        for (int i = 0; i < idx; i++)
            if (parts[i] != null) Destroy(parts[i].gameObject);
    }

    // Çerçeve/zemin parçaları: dört köşe + dört kenar ortası (normalize merkez, UI y yukarı). Sprite başına önbellek.
    private static readonly Vector2[] ShardCenters =
    {
        new Vector2(0.16f, 0.84f), new Vector2(0.84f, 0.84f), new Vector2(0.84f, 0.16f), new Vector2(0.16f, 0.16f),
        new Vector2(0.5f, 0.9f), new Vector2(0.9f, 0.5f), new Vector2(0.5f, 0.1f), new Vector2(0.1f, 0.5f),
    };
    private static readonly System.Collections.Generic.Dictionary<Sprite, Sprite[]> shardCache = new();

    private static Sprite[] ShardsFor(Sprite source)
    {
        if (source == null || source.texture == null) return null;
        if (shardCache.TryGetValue(source, out var cached)) return cached;
        var r = source.textureRect;
        float side = Mathf.Min(r.width, r.height) * 0.34f;
        var result = new Sprite[ShardCenters.Length];
        for (int i = 0; i < ShardCenters.Length; i++)
        {
            float x = Mathf.Clamp(r.x + ShardCenters[i].x * r.width - side * 0.5f, r.x, r.xMax - side);
            float y = Mathf.Clamp(r.y + ShardCenters[i].y * r.height - side * 0.5f, r.y, r.yMax - side);
            result[i] = Sprite.Create(source.texture, new Rect(x, y, side, side), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        }
        shardCache[source] = result;
        return result;
    }

    // ── Toplama hedefi (AncientSealCollectFx) ────────────────────────

    public bool IsCollectionAlive => this != null && isActiveAndEnabled && covered;

    public Vector3 GetCollectionTarget(int _) => counterRoot.TransformPoint(counterRoot.rect.center);

    public bool IsCollectionTargetReady(int _) => !peeling;

    public static Color TintFor(TarpStatueColor c) => Tints[(int)c];

    // ── Yardımcılar ──────────────────────────────────────────────────

    private RectTransform Layer(string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = false;
        return rt;
    }

    private TextMeshProUGUI NewText(string name, Color32 c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(counterRoot, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var text = go.AddComponent<TextMeshProUGUI>();
        if (CommonPopupSkin.Shared != null && CommonPopupSkin.Shared.font != null)
            text.font = CommonPopupSkin.Shared.font;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        CrispTextMaterial.Apply(text);
        text.color = c;
        TmpOutline.Apply(text, 0.35f, CounterDark);
        return text;
    }

    private static IEnumerator CoPunch(Transform target, float peak, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (target == null) yield break;
            target.localScale = Vector3.one * Mathf.Lerp(peak, 1f, Mathf.Clamp01(t / duration));
            yield return null;
        }
        if (target != null) target.localScale = Vector3.one;
    }

    private static readonly System.Collections.Generic.Dictionary<string, Sprite> spriteCache = new();

    private static Sprite Load(string name)
    {
        if (spriteCache.TryGetValue(name, out var s) && s != null) return s;
        s = Resources.Load<Sprite>(Dir + name);
        spriteCache[name] = s;
        return s;
    }
}
