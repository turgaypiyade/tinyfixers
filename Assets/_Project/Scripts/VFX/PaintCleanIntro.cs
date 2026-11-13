using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Level başlangıcı eğlencesi #2 — cephe temizlik asansörü (kullanıcı 2026-11-13, v2):
///   1) Boya Kovası tahtanın üstünde 4 aşamada dökülür (Bucket_Pour_0..3); akışın ucundan 3 leke cama uçar, yapışır.
///   2) Cam silici asansörü iki ipiyle top HUD'un ALTINDAN iner (HUD alt kenarında kırpılan alan → üstünden geçmez),
///      içinde yüzü bize dönük Tinko (domuz) ve Pemi (tavşan), belden yukarı; ön panel bellerini kapatır.
///   3) Silecekle sol/orta/sağ hamleler: her hamlede lekeler parça parça kısalır (dolgu), tek seferde yok olmaz.
///   4) İkisi "tamam" işareti yapar, asansör yukarı çekilip gider. Dokununca anında atlanır.
/// v1 (yürüyen Pemi, tek geçişte silme) beğenilmedi; görselleri Art/Characters/PemiWipe_v1'de.
/// Ne zaman oynanacağı: LevelIntroFun. Görseller: Resources/PaintCleanIntro/(Paint_Splat_*, Gondola/*). Sesler: mevcutlar + PaintClean_Squeak.
/// </summary>
public sealed class PaintCleanIntro : MonoBehaviour, IPointerDownHandler
{
    private const int SplatCount = 3;           // kullanıcı: 3 leke
    private const float SplatSize = 280f;
    private const float SplatBandOfBoard = 0.45f;        // lekelerin yüksekliği (tahta altından oran; kullanıcı: biraz aşağı)
    private const float BucketBelowTopOfBoard = 0.14f;   // kovanın döküldüğü yer (tahta üstünden aşağı oran)

    private static readonly Color[] Palette =
    {
        new Color(0.90f, 0.24f, 0.20f), new Color(0.98f, 0.80f, 0.16f),
        new Color(0.20f, 0.44f, 0.90f), new Color(0.24f, 0.75f, 0.28f),
    };

    // Zamanlama (sn)
    private const float BucketDrop = 0.32f, PourStep = 0.13f, SplatFly = 0.26f, BucketAway = 0.3f;
    private const float GondolaDown = 0.55f, Stroke = 0.17f, OkHold = 0.65f, GondolaUp = 0.45f;
    private const int Strokes = 4;              // sol, orta, sağ, orta

    // Ölçüler (board ebeveyninin UI birimi)
    private const float BucketSize = 230f, GondolaWidthOfBoard = 0.82f, RopeThickness = 16f;
    // Gondola_Front'ta kancaların tepesi (normalize x) ve karakterlerin panele göre yeri — görsellerden ölçüldü.
    private const float HookLeftX = 0.054f, HookRightX = 0.906f;
    private const float CharHeightOfGondolaWidth = 0.46f;   // karakter yüksekliği / panel genişliği
    private const float CharBottomBelowPanelTop = 0.38f;    // karakter alt kenarı, panel yüksekliğinin bu oranı kadar içeride
    private const float TinkoX = 0.27f, PemiX = 0.70f;       // panel genişliğine göre
    private static readonly Vector2 PourTip = new Vector2(0.749f, 0.045f);   // Bucket_Pour_2 akış ucu (x, y-alttan)

    private static Sprite gondola, splat0, splat1, splat2;
    private static readonly Sprite[] bucketFrames = new Sprite[4];
    private static readonly Sprite[] tinko = new Sprite[4], pemi = new Sprite[4];   // L, C, R, OK

    private RectTransform root, clip, rig;
    // Sesler (opsiyonel; dosya yoksa sessiz): Resources/PaintCleanIntro/Audio/*. Merkezi GameEventSfx.PlayUiClip.
    private static AudioClip sfxPour, sfxSplat, sfxGondola, sfxSqueak;
    private bool skip;

    public static bool CanPlay()
    {
        if (gondola != null) return true;
        const string P = "PaintCleanIntro/", G = "PaintCleanIntro/Gondola/";
        splat0 = Resources.Load<Sprite>(P + "Paint_Splat_0");
        splat1 = Resources.Load<Sprite>(P + "Paint_Splat_1");
        splat2 = Resources.Load<Sprite>(P + "Paint_Splat_2");
        for (int i = 0; i < 4; i++) bucketFrames[i] = Resources.Load<Sprite>(G + "Bucket_Pour_" + i);
        string[] pose = { "Wipe_L", "Wipe_C", "Wipe_R", "OK" };
        for (int i = 0; i < 4; i++)
        {
            tinko[i] = Resources.Load<Sprite>(G + "Tinko_" + pose[i]);
            pemi[i] = Resources.Load<Sprite>(G + "Pemi_" + pose[i]);
        }
        var g = Resources.Load<Sprite>(G + "Gondola_Front");
        // Mevcut sesler yeniden kullanılır (kullanıcı 2026-11-13: tutarlılık): Boya Kovası jokerinin dökülme/sıçrama
        // sesleri, Brumo'nun tahta makara sesi; "tamam" = hedef tamamlanma (GameEventSfx.PlayGoalComplete).
        // Yalnız silecek gıcırtısı yeni (dosya yoksa sessiz).
        sfxPour = Resources.Load<AudioClip>("Audio/Jokers/PaintPour");
        sfxSplat = Resources.Load<AudioClip>("Audio/Jokers/PaintSplat");
        sfxGondola = Resources.Load<AudioClip>("BoardFixIntro/Audio/BoardFix_PulleyDrop");
        sfxSqueak = Resources.Load<AudioClip>("PaintCleanIntro/Audio/PaintClean_Squeak");
        bool ok = g && splat0 && splat1 && splat2;
        for (int i = 0; i < 4; i++) ok &= bucketFrames[i] && tinko[i] && pemi[i];
        if (ok) gondola = g;
        return ok;
    }

    public static IEnumerator Play(RectTransform boardRoot, BoardController board = null)
    {
        if (boardRoot == null || boardRoot.parent == null || !CanPlay()) yield break;
        var go = new GameObject("PaintCleanIntro", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var intro = go.AddComponent<PaintCleanIntro>();
        LevelIntroFun.IsPlaying = true;
        try { yield return intro.Run(boardRoot, board); }
        finally
        {
            LevelIntroFun.IsPlaying = false;
            if (intro != null) Destroy(intro.gameObject);
        }
    }

    public void OnPointerDown(PointerEventData eventData) => skip = true;

    private IEnumerator Run(RectTransform boardRoot, BoardController board)
    {
        int layer = boardRoot.gameObject.layer;
        root = (RectTransform)transform;
        root.SetParent(boardRoot.parent, false);
        root.SetSiblingIndex(boardRoot.GetSiblingIndex() + 1);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        gameObject.layer = layer;
        GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // görünmez; dokunuşu yakalar (atla)

        var r = boardRoot.rect;
        Vector2 min = Local(boardRoot.TransformPoint(new Vector3(r.xMin, r.yMin)));
        Vector2 max = Local(boardRoot.TransformPoint(new Vector3(r.xMax, r.yMax)));
        float boardW = max.x - min.x, boardH = max.y - min.y;

        // HUD alt kenarı: asansör bunun altından çıkar (kırpma alanının üst sınırı).
        float hudBottom = root.rect.yMax;
        var hudBg = board != null && board.TopHud != null ? board.TopHud.BackgroundRect : null;
        if (hudBg != null)
        {
            var hc = new Vector3[4];
            hudBg.GetWorldCorners(hc);
            hudBottom = Local(hc[0]).y + 140f;   // TopBG dikdörtgeni görünen kenardan aşağı uzanıyor (Brumo ile aynı ölçüm)
        }

        // Lekeler: tahtanın üst-orta bandında soldan sağa üç yer.
        float bandY = min.y + boardH * SplatBandOfBoard;
        var splatTargets = new Vector2[SplatCount];
        float[] xs = { 0.22f, 0.5f, 0.78f };
        for (int i = 0; i < SplatCount; i++)
            splatTargets[i] = new Vector2(min.x + boardW * xs[i % xs.Length], bandY + Random.Range(-50f, 50f));

        // ── 1) Kova iner, 4 aşamada dökülür; akış ucundan lekeler fırlar. ──
        var bucket = NewImage("Bucket", bucketFrames[0], layer, root, new Vector2(0.5f, 0.5f), Vector2.one * BucketSize);
        Vector2 hover = new Vector2((min.x + max.x) * 0.5f - 60f, max.y - boardH * BucketBelowTopOfBoard);
        Vector2 dropFrom = new Vector2(hover.x, root.rect.yMax + BucketSize);
        for (float t = 0f; t < BucketDrop && !skip; t += Time.deltaTime)
        {
            bucket.anchoredPosition = Vector2.LerpUnclamped(dropFrom, hover, EaseOutBack(t / BucketDrop));
            yield return null;
        }
        bucket.anchoredPosition = hover;

        var splats = new Image[SplatCount];
        var splatRts = new RectTransform[SplatCount];
        Sprite[] splatSprites = { splat0, splat1, splat2 };
        int colorOffset = Random.Range(0, Palette.Length);
        for (int i = 0; i < SplatCount; i++)
        {
            float size = SplatSize * Random.Range(0.85f, 1.1f);
            splatRts[i] = NewImage("Splat" + i, splatSprites[i % 3], layer, root, new Vector2(0.5f, 0.5f), Vector2.one * size);
            splatRts[i].SetSiblingIndex(0);
            splatRts[i].localRotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
            splatRts[i].gameObject.SetActive(false);
            splats[i] = splatRts[i].GetComponent<Image>();
            splats[i].color = Palette[(colorOffset + i) % Palette.Length];
            splats[i].type = Image.Type.Filled;          // silerken parça parça kısalır
            splats[i].fillMethod = Image.FillMethod.Horizontal;
        }

        var bucketImg = bucket.GetComponent<Image>();
        GameEventSfx.PlayUiClip(sfxPour);
        Vector2 tip = Vector2.zero;
        var launched = new float[SplatCount];
        for (int i = 0; i < SplatCount; i++) launched[i] = -1f;
        var landed = new bool[SplatCount];
        float pourTotal = PourStep * 4f + SplatFly + 0.1f;
        for (float t = 0f; t < pourTotal && !skip; t += Time.deltaTime)
        {
            int frame = Mathf.Min(3, (int)(t / PourStep));
            if (bucketImg.sprite != bucketFrames[frame]) SetSprite(bucket, bucketFrames[frame], BucketSize);
            if (frame >= 2)
            {
                tip = Local(bucket.TransformPoint(new Vector3((PourTip.x - 0.5f) * bucket.sizeDelta.x,
                                                              (PourTip.y - 0.5f) * bucket.sizeDelta.y)));
                for (int i = 0; i < SplatCount; i++)
                {
                    float start = PourStep * 2f + i * 0.06f;
                    if (t < start) continue;
                    if (launched[i] < 0f) { launched[i] = t; splatRts[i].gameObject.SetActive(true); }
                    float k = Mathf.Clamp01((t - start) / SplatFly), e = 1f - (1f - k) * (1f - k);
                    if (k >= 1f && landed[i] == false) { landed[i] = true; GameEventSfx.PlayUiClip(sfxSplat); }
                    splatRts[i].anchoredPosition = Vector2.Lerp(tip, splatTargets[i], e);
                    float grow = Mathf.Lerp(0.15f, 1f, e);
                    float squash = k > 0.85f && k < 1f ? Mathf.Lerp(1f, 0.86f, (k - 0.85f) / 0.15f) : 1f;
                    splatRts[i].localScale = new Vector3(grow / squash, grow * squash, 1f);
                }
            }
            yield return null;
        }
        for (int i = 0; i < SplatCount; i++)
        {
            splatRts[i].gameObject.SetActive(true);
            splatRts[i].anchoredPosition = splatTargets[i];
            splatRts[i].localScale = Vector3.one;
        }
        Vector2 bAt = bucket.anchoredPosition;
        for (float t = 0f; t < BucketAway && !skip; t += Time.deltaTime)
        {
            bucket.anchoredPosition = bAt + new Vector2(t * 400f, -t * t * 4000f);
            bucket.localRotation = Quaternion.Euler(0f, 0f, -t * 300f);
            yield return null;
        }
        bucket.gameObject.SetActive(false);

        // ── 2) Asansör: HUD alt kenarında kırpılan alanın içinden iner. ──
        clip = NewRect("GondolaClip", layer, root);
        clip.anchorMin = clip.anchorMax = new Vector2(0.5f, 0.5f);
        float clipBottom = root.rect.yMin;
        clip.sizeDelta = new Vector2(root.rect.width, hudBottom - clipBottom);
        clip.anchoredPosition = new Vector2(root.rect.center.x, (hudBottom + clipBottom) * 0.5f);
        clip.gameObject.AddComponent<RectMask2D>();
        // Lekeler camın üstünde: asansörün ÖNÜNDE çizilir; silindikçe Tinko ile Pemi'nin yüzleri ortaya çıkar.
        for (int i = 0; i < SplatCount; i++) splatRts[i].SetAsLastSibling();

        float gw = boardW * GondolaWidthOfBoard;
        float gh = gw * gondola.rect.height / gondola.rect.width;
        float charH = gw * CharHeightOfGondolaWidth;
        rig = NewRect("GondolaRig", layer, clip);   // pivot = panel üst kenarı ortası
        rig.anchorMin = rig.anchorMax = new Vector2(0.5f, 0.5f);
        rig.sizeDelta = Vector2.zero;

        var ropeL = NewRope("RopeL", layer, rig); var ropeR = NewRope("RopeR", layer, rig);
        var tk = NewImage("Tinko", tinko[1], layer, rig, new Vector2(0.5f, 0f), Vector2.one * charH);
        var pm = NewImage("Pemi", pemi[1], layer, rig, new Vector2(0.5f, 0f), Vector2.one * charH);
        var front = NewImage("Gondola", gondola, layer, rig, new Vector2(0.5f, 1f), new Vector2(gw, gh));
        front.anchoredPosition = Vector2.zero;
        tk.anchoredPosition = new Vector2((TinkoX - 0.5f) * gw, -gh * CharBottomBelowPanelTop);
        pm.anchoredPosition = new Vector2((PemiX - 0.5f) * gw, -gh * CharBottomBelowPanelTop);
        SetSprite(tk, tinko[1], charH); SetSprite(pm, pemi[1], charH);
        Vector2 hookL = new Vector2((HookLeftX - 0.5f) * gw, 0f), hookR = new Vector2((HookRightX - 0.5f) * gw, 0f);

        // Durma yeri: silecek yüksekliği (karakterin üst kısmı) leke bandına gelsin.
        float stopTopY = bandY - (charH * 0.78f - gh * CharBottomBelowPanelTop);
        Vector2 stopPos = new Vector2((min.x + max.x) * 0.5f, stopTopY);
        Vector2 abovePos = new Vector2(stopPos.x, hudBottom + charH + 40f);   // tamamen HUD'un altında gizli
        float ropeTop = hudBottom + 60f;
        void SetRig(Vector2 p)
        {
            rig.anchoredPosition = p - clip.anchoredPosition;
            float up = ropeTop - p.y;
            Stretch(ropeL, hookL, hookL + new Vector2(0f, up));
            Stretch(ropeR, hookR, hookR + new Vector2(0f, up));
        }
        GameEventSfx.PlayUiClip(sfxGondola);
        for (float t = 0f; t < GondolaDown && !skip; t += Time.deltaTime)
        {
            float k = t / GondolaDown;
            SetRig(Vector2.LerpUnclamped(abovePos, stopPos, EaseOutBack(k)));
            yield return null;
        }
        SetRig(stopPos);

        // ── 3) Silme: sol/orta/sağ/orta hamleler; Tinko sol+orta, Pemi sağ lekeyi siler; her hamlede dolgu azalır. ──
        int[] seq = { 0, 1, 2, 1 };
        for (int s = 0; s < Strokes && !skip; s++)
        {
            SetSprite(tk, tinko[seq[s]], charH);
            SetSprite(pm, pemi[seq[(s + 1) % seq.Length]], charH);   // ikisi aynı anda aynı pozda olmasın
            float from = 1f - s / (float)Strokes, to = 1f - (s + 1) / (float)Strokes;
            if (s % 2 == 0) GameEventSfx.PlayUiClip(sfxSqueak);   // gıcırtı ~0.5 sn: iki hamlede bir (üst üste binmesin)
            for (float t = 0f; t < Stroke && !skip; t += Time.deltaTime)
            {
                float fill = Mathf.Lerp(from, to, t / Stroke);
                for (int i = 0; i < SplatCount; i++)
                {
                    splats[i].fillOrigin = s % 2;          // hamle yönü değiştikçe karşı kenardan silinir
                    splats[i].fillAmount = fill;
                }
                rig.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((s * Stroke + t) * 18f) * 1.2f);   // hafif sallantı
                yield return null;
            }
        }
        for (int i = 0; i < SplatCount; i++) splatRts[i].gameObject.SetActive(false);
        rig.localRotation = Quaternion.identity;

        // ── 4) "Tamam" işareti, sonra asansör yukarı çekilir. ──
        SetSprite(tk, tinko[3], charH); SetSprite(pm, pemi[3], charH);
        if (!skip) GameEventSfx.PlayGoalComplete();
        for (float t = 0f; t < OkHold && !skip; t += Time.deltaTime) yield return null;
        if (!skip) GameEventSfx.PlayUiClip(sfxGondola);
        for (float t = 0f; t < GondolaUp && !skip; t += Time.deltaTime)
        {
            float k = t / GondolaUp;
            SetRig(Vector2.Lerp(stopPos, abovePos, k * k));
            yield return null;
        }
    }

    private Vector2 Local(Vector3 world) => root.InverseTransformPoint(world);

    private static RectTransform NewRect(string name, int layer, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static RectTransform NewImage(string name, Sprite sprite, int layer, Transform parent, Vector2 pivot, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return rt;
    }

    private static RectTransform NewRope(string name, int layer, Transform parent)
    {
        var sprite = Resources.Load<Sprite>("BoardFixIntro/Rope_Tile");   // Brumo'nun ipi
        var rt = NewImage(name, sprite, layer, parent, new Vector2(0f, 0.5f), new Vector2(1f, RopeThickness));
        var img = rt.GetComponent<Image>();
        img.preserveAspect = false;
        if (sprite != null)
        {
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = sprite.rect.height / RopeThickness;
        }
        return rt;
    }

    // Boyu sabit tutarak sprite değiştirir (genişlik oranı sprite'tan).
    private static void SetSprite(RectTransform rt, Sprite sprite, float height)
    {
        rt.GetComponent<Image>().sprite = sprite;
        rt.sizeDelta = new Vector2(height * sprite.rect.width / sprite.rect.height, height);
    }

    private static void Stretch(RectTransform rope, Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        rope.anchoredPosition = from;
        rope.sizeDelta = new Vector2(d.magnitude, RopeThickness);
        rope.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
