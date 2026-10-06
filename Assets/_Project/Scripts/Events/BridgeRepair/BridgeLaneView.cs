using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bir köprünün yerleşim ölçüleri — tümü arka plan PİKSELİ cinsinden (Board ölçeksiz uzayı).</summary>
[System.Serializable]
public sealed class BridgeLaneMetrics
{
    [Tooltip("Güverte tahtasının kalınlığı (arka plan pikseli). Tüm parçaların ölçeği buradan çıkar\n" +
             "(V3 parçaları aynı tuvalden kesildi → ortak ölçek).")]
    public float deckHeight = 68f;   // eski segment 226px × 0.30 — görünür boyut korunur
    [Tooltip("Tüm köprünün (ayaklar + güverte + karakterler) dikey kaydırması (+ yukarı).")]
    public float pieceOffsetY = -10f;   // ayaklar çıkıntıya otursun (kullanıcı)
    [Tooltip("Güverte üst çizgisinin çıkıntı anchor'ının ne kadar ÜSTÜNDE olduğu (V2 uç parçaları taşsız,\n" +
             "arka plandaki çıkıntıya oturur). pieceOffsetY bunun üstüne eklenir. Lane0: anchor 439 + 160 - 10 ≈ 590 (kullanıcı).")]
    public float deckAboveLedge = 150f;   // kullanıcı: 150 iyi
    [Tooltip("Güverte başlangıcının (sol uç parçanın iç kenarı) sol anchor'a yatay uzaklığı; sağda simetrik.")]
    public float deckInsetX = 87f;
    [Tooltip("Uç parçaların segment altına girme payı (dikiş boşluğu kalmasın).")]
    public float endOverlap = 4f;
    [Tooltip("Karakter yüksekliği (arka plan pikseli).")]
    public float characterHeight = 118f;
    [Tooltip("Karakterin ayak hizası, güverte üst çizgisine göre (+ yukarı).")]
    public float characterFeetOffsetY = -42f;   // köprü -10 + karakter -10 = ekranda toplam 20 px aşağı (kullanıcı, 3 tur)
    [Tooltip("Karakter, inşa ucunun ne kadar gerisinde durur (karakter genişliğinin oranı).")]
    [Range(0f, 1f)] public float characterFrontGap = 0.3f;
    [Tooltip("Köprü bitince karakterin sağ uçta duracağı mesafe (güverte bitişinden sonra).")]
    public float finishStandOffset = 62f;
    [Tooltip("İsim etiketinin güverte başlangıcına göre konumu. Etiket bir ÜST şeridin güverte başlangıcına göre\n" +
             "yerleşir → karakterin üstünde durur (en üst şerit için şerit aralığı yukarı uzatılır).")]
    public Vector2 tagOffset = new Vector2(120f, -112f);
    [Tooltip("Yürüme hızı (arka plan pikseli / sn).")]
    public float walkSpeed = 150f;
}

/// <summary>
/// Tek köprü şeridi: sol/sağ uç parçaları çıkıntılara oturur (anchor = çıkıntının ön-üst kenar ortası),
/// aradaki boşluk BridgesegmentsV1 parçalarıyla kaplanır ve ilerlemeyle soldan sağa açılır (Filled).
/// Karakter köprünün önünde yürür, inşa ucunda eğilip parçayı oturtur; beklerken kendi başına hareket eder.
///
/// V3 parçaları Photoshop'ta TEK tuvalden kesildi → ortak ölçek. Sabitler ölçüldü (piksel, sol-ÜST köşeden):
/// sol 1353x1692 güverte y=1024..1652, iç kenar x=1338 · sağ 1353x1692 güverte y=1024, iç kenar x=0 ·
/// segment 1311x1641 güverte y=974..1601 (tuvali üstten 50px kısa kırpılmış; korkuluk da aynı hizada).
/// Segmentler her köprüde TAM SAYIDA dilim olacak şekilde yatayda hafif esnetilir (son dilim kesik kalmaz).
/// </summary>
public sealed class BridgeLaneView : MonoBehaviour
{
    private const float SegW = 1311f, SegH = 1641f, SegDeckY = 974f, DeckThickness = 627f;
    private static readonly EndPiece LeftEnd  = new(1353f, 1692f, 1024f, 1338f);
    private static readonly EndPiece RightEnd = new(1353f, 1692f, 1024f, 0f);

    private readonly struct EndPiece
    {
        public readonly Vector2 size;
        public readonly float deckTop, innerX;
        public EndPiece(float w, float h, float deckTop, float innerX)
        {
            size = new Vector2(w, h);
            this.deckTop = deckTop;
            this.innerX = innerX;
        }
        // Pivot = güverte üst çizgisi × iç kenar → parça doğrudan P0/P1'e oturur.
        public Vector2 Pivot => new(innerX / size.x, 1f - deckTop / size.y);
    }

    private BridgeRepairConfig config;
    private BridgeLaneMetrics metrics;
    private BridgeContestant contestant;
    private RectTransform deck, walkers, badgeRoot;
    private Image[] segments;
    private float segLen;           // bir dilimin (esnetilmiş) güverte uzunluğu
    private float length;           // güverte boşluğu uzunluğu (P0→P1)
    private float revealed;         // açılmış uzunluk
    private BridgeCharacterView character;
    private TMP_Text progressText;
    private int shownProgress;

    public BridgeContestant Contestant => contestant;
    public int ShownProgress => shownProgress;

    public static BridgeLaneView Create(RectTransform parent, RectTransform tagsLayer, BridgeRepairConfig config,
        BridgeLaneMetrics metrics, BridgeContestant contestant, Vector2 leftAnchor, Vector2 rightAnchor,
        Vector2 tagLeftAnchor, Sprite leftSprite, Sprite rightSprite, Sprite segmentSprite)
    {
        var root = BridgeRepairUI.Rect($"Lane{contestant.lane}", parent, Vector2.zero, Vector2.zero);
        root.localPosition = Vector3.zero;
        var lane = root.gameObject.AddComponent<BridgeLaneView>();
        lane.config = config;
        lane.metrics = metrics;
        lane.contestant = contestant;
        lane.Build(tagsLayer, leftAnchor, rightAnchor, tagLeftAnchor, leftSprite, rightSprite, segmentSprite);
        return lane;
    }

    private void Build(RectTransform tagsLayer, Vector2 leftAnchor, Vector2 rightAnchor, Vector2 tagLeftAnchor,
        Sprite leftSprite, Sprite rightSprite, Sprite segmentSprite)
    {
        float a = Mathf.Max(1f, metrics.deckHeight) / DeckThickness;   // parça pikseli → arka plan pikseli
        leftAnchor.y += metrics.pieceOffsetY;
        rightAnchor.y += metrics.pieceOffsetY;

        // Güverte uçları (P0 = sol parçanın iç kenarı, P1 = sağ parçanın iç kenarı; güverte üst çizgisi).
        Vector2 deckFromAnchor = new Vector2(metrics.deckInsetX, metrics.deckAboveLedge);
        Vector2 p0 = leftAnchor + deckFromAnchor;
        Vector2 p1 = rightAnchor + new Vector2(-metrics.deckInsetX, metrics.deckAboveLedge);
        Vector2 dir = p1 - p0;
        length = Mathf.Max(1f, dir.magnitude);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // Çizim sırası: önce köprü (sol uç → güverte → sağ uç), karakterler EN ÖNDE.
        // Uç parçalar güverte açısıyla döner; iç kenarları segmentin biraz altına girer.
        Vector2 along = dir / length;
        Piece("BridgeLeft", leftSprite, LeftEnd, p0 + along * metrics.endOverlap, angle, a);
        deck = Strip("Deck", p0, angle);
        Piece("BridgeRight", rightSprite, RightEnd, p1 - along * metrics.endOverlap, angle, a);
        walkers = Strip("Walkers", p0, angle);

        // Tam sayıda dilim: boşluk en yakın dilim sayısına bölünür, genişlik o kadar esner (en fazla ~%15).
        int count = Mathf.Max(1, Mathf.RoundToInt(length / (SegW * a)));
        segLen = length / count;
        segments = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var seg = BridgeRepairUI.Picture($"Segment{i}", deck, segmentSprite, new Vector2(segLen, SegH * a),
                Vector2.zero, preserveAspect: false);
            seg.rectTransform.pivot = new Vector2(0f, 1f - SegDeckY / SegH);
            seg.rectTransform.localPosition = new Vector3(i * segLen, 0f, 0f);
            seg.type = Image.Type.Filled;
            seg.fillMethod = Image.FillMethod.Horizontal;
            seg.fillOrigin = (int)Image.OriginHorizontal.Left;
            seg.fillAmount = 0f;
            segments[i] = seg;
        }

        var def = CharacterDef();
        character = BridgeCharacterView.Create(walkers, def, metrics.characterHeight);
        character.SetGround(metrics.characterFeetOffsetY);
        character.SfxScale = EventSfx.ScaleFor(contestant.isPlayer);
        character.Place(StandX(0));

        badgeRoot = BridgeRepairUI.Rect("Badge", walkers, Vector2.zero, Vector2.zero);

        // Etiket bir üst şeridin güverte başlangıcına göre: eski "alttaki" konumu = bu karakterin üstü.
        tagLeftAnchor.y += metrics.pieceOffsetY;
        BuildTag(tagsLayer, tagLeftAnchor + deckFromAnchor);
    }

    private BridgeCharacterDef CharacterDef()
    {
        var list = config.characters;
        if (list == null || list.Count == 0) return null;
        return list[Mathf.Clamp(contestant.characterIndex, 0, list.Count - 1)];
    }

    private RectTransform Strip(string name, Vector2 origin, float angle)
    {
        var rt = BridgeRepairUI.Rect(name, transform, Vector2.zero, Vector2.zero);
        rt.pivot = Vector2.zero;
        rt.localPosition = origin;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        return rt;
    }

    private Image Piece(string name, Sprite sprite, EndPiece piece, Vector2 position, float angle, float a)
    {
        var img = BridgeRepairUI.Picture(name, transform, sprite, piece.size * a, Vector2.zero,
            preserveAspect: false);
        img.rectTransform.pivot = piece.Pivot;
        img.rectTransform.localPosition = position;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        return img;
    }

    private void BuildTag(RectTransform tagsLayer, Vector2 p0)
    {
        var parent = tagsLayer != null ? tagsLayer : (RectTransform)transform;
        Vector2 local = tagsLayer != null
            ? (Vector2)tagsLayer.InverseTransformPoint(transform.TransformPoint(p0 + metrics.tagOffset))
            : p0 + metrics.tagOffset;

        bool me = contestant.isPlayer;
        // Etiket büyütüldü (230x62 → 270x76); SOL kenar aynı yerde kalsın diye merkez sağa kayar
        // (karakterin başıyla çakışmasın).
        const float tagW = 270f, tagH = 76f;
        var pill = BridgeRepairUI.Rect($"Tag{contestant.lane}", parent, new Vector2(tagW, tagH), Vector2.zero);
        pill.localPosition = local + new Vector2((tagW - 230f) * 0.5f, 0f);
        var bg = pill.gameObject.AddComponent<Image>();
        bg.sprite = BridgeRepairUI.Pill();
        bg.type = Image.Type.Sliced;
        // Oyuncunun balonu da botlarınkiyle AYNI (kullanıcı); kim olduğu "SEN" yazısından anlaşılır.
        bg.color = new Color(0.06f, 0.16f, 0.3f, 0.88f);
        bg.raycastTarget = false;

        BridgeRepairUI.Avatar("Avatar", pill, contestant.avatar, 70f, new Vector2(-tagW * 0.5f + 42f, 0f),
            new Color(0.85f, 0.9f, 1f));

        float textX = -tagW * 0.5f + 84f + 92f;   // avatarın sağından başlayan 184px'lik yazı kutusunun merkezi
        string name = me ? BridgeRepairUI.L("bridge_you", "SEN") : contestant.displayName;
        TagText("Name", pill, name, 29f, new Vector2(184f, 36f), new Vector2(textX, 15f), BridgeRepairUI.Cream);
        progressText = TagText("Progress", pill, "", 26f, new Vector2(184f, 30f), new Vector2(textX, -18f),
            BridgeRepairUI.Gold);
    }

    // Etiket yazısı: oyunun kalın UI fontu + net materyal; koyu zeminde ince koyu kontur, sarı (benim)
    // zeminde kontursuz. Yazı BAŞTAN doğru fontla kurulur: BridgeRepairUI.Label konturu hemen verdiği için
    // TMP varsayılan fontuyla materyal kopyası oluşuyordu; font sonradan değişince kontur o eski kopyaya
    // dönüp Inter harflerini yanlış atlastan çiziyordu (bozuk karakterler).
    private static TMP_Text TagText(string name, RectTransform parent, string value, float size, Vector2 box,
        Vector2 position, Color color)
    {
        var text = BridgeRepairUI.Rect(name, parent, box, position).gameObject.AddComponent<TextMeshProUGUI>();
        var style = RewardTextStyle.Shared;
        if (style != null && style.font != null) text.font = style.font;
        CrispTextMaterial.Apply(text);
        text.text = value;
        text.raycastTarget = false;
        text.color = color;
        text.fontSize = text.fontSizeMax = size;
        text.fontSizeMin = size * 0.6f;
        text.enableAutoSizing = true;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        TmpOutline.Apply(text, 0.16f, BridgeRepairUI.Ink);
        return text;
    }

    // ── İlerleme ─────────────────────────────────────────────────

    private float RevealFor(int progress) =>
        length * Mathf.Clamp01(progress / (float)Mathf.Max(1, config.levelsToFinish));

    private float CharacterWidth => character != null ? character.Rect.sizeDelta.x : 100f;

    private float StandX(int progress)
    {
        if (progress >= config.levelsToFinish) return length + metrics.finishStandOffset;
        return RevealFor(progress) - CharacterWidth * metrics.characterFrontGap;
    }

    private void ApplyReveal(float len)
    {
        revealed = len;
        for (int i = 0; i < segments.Length; i++)
            segments[i].fillAmount = Mathf.Clamp01((len - i * segLen) / segLen);
    }

    private void RefreshProgressText(int progress)
    {
        if (progressText != null) progressText.text = $"{progress}/{config.levelsToFinish}";
    }

    public void SetInstant(int progress, int rank)
    {
        shownProgress = Mathf.Clamp(progress, 0, config.levelsToFinish);
        ApplyReveal(RevealFor(shownProgress));
        character.Place(StandX(shownProgress));
        RefreshProgressText(shownProgress);
        SetRank(rank, animate: false);
    }

    /// from → to arası her adımı oynatır: eğil → parça otursun (toz) → kalk → yeni uca yürü.
    public IEnumerator Animate(int to, int rankWhenDone, float speedMul)
    {
        to = Mathf.Clamp(to, 0, config.levelsToFinish);
        speedMul = Mathf.Max(0.1f, speedMul);

        while (shownProgress < to)
        {
            int next = shownProgress + 1;
            // Eğil ve parçayı oturt.
            yield return character.BendDown(speedMul);
            float from = revealed, target = RevealFor(next);
            float dur = 0.42f / speedMul, t = 0f;
            EventSfx.Play(x => x.plankPlace, EventSfx.ScaleFor(contestant.isPlayer));
            StartCoroutine(character.Hammer(dur));
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                ApplyReveal(Mathf.Lerp(from, target, BridgeRepairUI.Smooth01(t / dur)));
                yield return null;
            }
            ApplyReveal(target);
            SpawnDust(target);
            shownProgress = next;
            RefreshProgressText(next);
            yield return character.StandUp(speedMul);

            // Yeni uca yürü (son adımda köprünün öbür ucuna geçer).
            yield return character.WalkTo(StandX(next), metrics.walkSpeed * speedMul);
        }

        if (shownProgress >= config.levelsToFinish && rankWhenDone > 0)
        {
            SetRank(rankWhenDone, animate: true);
            yield return character.Cheer();
        }
    }

    // ── Sıra rozeti (karakterin başında) ─────────────────────────

    public void SetRank(int rank, bool animate)
    {
        for (int i = badgeRoot.childCount - 1; i >= 0; i--) Destroy(badgeRoot.GetChild(i).gameObject);
        if (rank <= 0) return;

        var ch = character.Rect;
        badgeRoot.localPosition = ch.localPosition + new Vector3(0f, ch.sizeDelta.y + 28f, 0f);
        badgeRoot.localRotation = Quaternion.Inverse(walkers.localRotation);   // köprü eğimine rağmen dik dursun

        var color = rank switch
        {
            1 => new Color(1f, 0.8f, 0.18f),
            2 => new Color(0.82f, 0.86f, 0.92f),
            3 => new Color(0.86f, 0.53f, 0.27f),
            _ => new Color(0.45f, 0.55f, 0.7f)
        };
        var disc = BridgeRepairUI.Picture("RankDisc", badgeRoot, BridgeRepairUI.Circle(), Vector2.one * 64f, Vector2.zero);
        disc.color = color;
        var ring = BridgeRepairUI.Picture("RankRing", disc.rectTransform, BridgeRepairUI.Circle(), Vector2.one * 52f, Vector2.zero);
        ring.color = Color.Lerp(color, Color.white, 0.35f);
        BridgeRepairUI.Label("Rank", disc.rectTransform, rank.ToString(), 38f, Vector2.one * 60f, new Vector2(0f, 2f),
            BridgeRepairUI.Ink, rewardStyle: false).outlineWidth = 0f;
        if (animate)
        {
            StartCoroutine(Pop(disc.rectTransform));
            EventSfx.Play(x => x.rankBadge, EventSfx.ScaleFor(contestant.isPlayer));
        }
    }

    private static IEnumerator Pop(RectTransform rt)
    {
        float t = 0f;
        const float d = 0.4f;
        while (t < d && rt != null)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * BridgeRepairUI.EaseOutBack(Mathf.Clamp01(t / d));
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    // ── Toz efekti (parça oturunca) ──────────────────────────────

    private void SpawnDust(float x)
    {
        for (int i = 0; i < 6; i++)
        {
            var puff = BridgeRepairUI.Picture("Dust", deck, BridgeRepairUI.Glow(), Vector2.one * 34f, Vector2.zero);
            puff.rectTransform.localPosition = new Vector3(x - 6f, -18f, 0f);
            puff.color = new Color(1f, 0.93f, 0.78f, 0.9f);
            StartCoroutine(DustFly(puff, new Vector2(Random.Range(-40f, 26f), Random.Range(-10f, 40f))));
        }
    }

    private static IEnumerator DustFly(Image puff, Vector2 velocity)
    {
        var rt = puff.rectTransform;
        Vector3 start = rt.localPosition;
        float t = 0f;
        const float d = 0.55f;
        while (t < d && puff != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            rt.localPosition = start + (Vector3)(velocity * k);
            rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.6f, k);
            var c = puff.color;
            c.a = 0.9f * (1f - k);
            puff.color = c;
            yield return null;
        }
        if (puff != null) Destroy(puff.gameObject);
    }
}
