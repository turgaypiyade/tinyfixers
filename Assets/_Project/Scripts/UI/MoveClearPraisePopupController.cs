using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hamle övgüsü (GOOD! / WOW! / GREAT!) — kullanıcı tasarımı (2026-10-27): arka plan/rozet YOK; son hamlenin
/// yerinden sıcak altın ışık hüzmeleri yükselir; yazı hüzmelerin içinde hologram gibi oluşur (düşük alfa + küçükten
/// büyüğe + sönümlenen titreşim, 2026-10-27 geri bildirimi: "bir anda çıkmasın"), biraz yükselip yukarı uzayarak söner. Yazı TinyFixers Pop + yazı kuralı (beyaz yüz, kontur/gölge = altın ışığın koyusu).
/// Tüm parçalar bir kez kurulur ve her övgüde yeniden kullanılır (anlık nesne/materyal üretimi yok).
/// </summary>
public sealed class MoveClearPraisePopupController : MonoBehaviour
{
    [SerializeField] private int minimumClearedTiles = 30;
    [SerializeField, Range(48f, 140f)] private float praiseFontSize = 96f;
    [Tooltip("Yazının hamle noktasından ne kadar yükseleceği (hücre cinsinden).")]
    [SerializeField, Range(0f, 3f)] private float riseCells = 1.3f;
    [SerializeField, Range(0.1f, 1.5f)] private float holdDuration = 0.45f;
    [Tooltip("Yazının tam oluştuğundaki opaklığı. Hologram hissi + beyaz yüz göze batmasın diye yarı saydam (kullanıcı, 2026-10-27).")]
    [SerializeField, Range(0.3f, 1f)] private float textOpacity = 0.72f;

    // Altın ışık = yazının "zemini": kontur/gölge bunun koyusu (TextSurfaceStyle).
    private static readonly Color LightColor = new Color(1f, 0.70f, 0.18f, 1f);
    private static readonly Color BeamColor = new Color(1f, 0.88f, 0.50f, 0.62f);   // geniş hüzme → alfa biraz düşük
    private static readonly Color GlowColor = new Color(1f, 0.82f, 0.40f, 0.60f);
    private static readonly Color SparkColor = new Color(1f, 0.97f, 0.72f, 1f);

    private const int MaxBeams = 5, MaxSparks = 6;
    private const float InDuration = 0.45f, OutDuration = 0.35f;
    // Hologram oluşumu: hüzmeler önce çıkar, yazı az sonra içlerinde belirir.
    private const float MaterializeDelay = 0.06f, MaterializeDuration = 0.42f;
    private const float MaterializeStartScale = 0.3f;

    private BoardController board;
    private Coroutine activeRoutine;

    // Bir kez kurulan, yeniden kullanılan parçalar.
    private RectTransform root;
    private CanvasGroup group;
    private TextMeshProUGUI text;
    private RectTransform textRt;
    private CanvasGroup textGroup;   // yazı alfası: mesh yeniden kurulmadan (alt-mesh'ler dahil)
    private PraiseLightGraphic glow;
    private readonly PraiseLightGraphic[] beams = new PraiseLightGraphic[MaxBeams];
    private readonly JaggedBadgeGraphic[] sparks = new JaggedBadgeGraphic[MaxSparks];
    private readonly Vector2[] beamSize = new Vector2[MaxBeams];
    private readonly float[] beamDelay = new float[MaxBeams];
    private readonly Vector2[] sparkFrom = new Vector2[MaxSparks];
    private readonly Vector2[] sparkVelocity = new Vector2[MaxSparks];
    private readonly float[] sparkSpin = new float[MaxSparks];

    public void Bind(BoardController target)
    {
        // The controller is a persistent host; authored hosts may be saved inactive in the scene.
        if (target != null && !gameObject.activeSelf)
            gameObject.SetActive(true);

        if (board == target)
            return;

        if (board != null)
            board.OnMoveClearPraise -= HandleMoveClearPraise;

        board = target;

        if (board != null)
            board.OnMoveClearPraise += HandleMoveClearPraise;
    }

    private void OnDestroy()
    {
        if (board != null)
            board.OnMoveClearPraise -= HandleMoveClearPraise;
        StopActivePopup();
    }

    // Unity stops host coroutines on deactivation; hide the half-played effect so it cannot reappear frozen.
    private void OnDisable() => StopActivePopup();

    private void HandleMoveClearPraise(int clearedTiles, Vector3 worldOrigin)
    {
        // C# events can still reach us while a parent Canvas or this component is disabled.
        if (!isActiveAndEnabled || clearedTiles < minimumClearedTiles)
            return;

        StopActivePopup();
        EnsureBuilt();
        activeRoutine = StartCoroutine(Play(clearedTiles, worldOrigin));
    }

    private void StopActivePopup()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
        if (root != null) root.gameObject.SetActive(false);
    }

    // ── Kurulum (bir kez) ──────────────────────────────────────────────────────────────────

    private void EnsureBuilt()
    {
        if (root != null) return;

        var go = new GameObject("MoveClearPraise", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = gameObject.layer;
        root = (RectTransform)go.transform;
        root.SetParent(transform, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        glow = NewPart<PraiseLightGraphic>("Glow");
        glow.Kind = PraiseLightGraphic.Shape.Glow;
        glow.color = GlowColor;
        for (int i = 0; i < MaxBeams; i++)
        {
            beams[i] = NewPart<PraiseLightGraphic>("Beam" + i);
            beams[i].Kind = PraiseLightGraphic.Shape.Beam;
            beams[i].color = BeamColor;
            beams[i].rectTransform.pivot = new Vector2(0.5f, 0f);   // hüzme hamle noktasından yukarı büyür
        }
        for (int i = 0; i < MaxSparks; i++)
        {
            sparks[i] = NewPart<JaggedBadgeGraphic>("Spark" + i);
            sparks[i].Spikes = 4;
            sparks[i].InnerRadius = 0.38f;
            sparks[i].color = SparkColor;
        }

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(CanvasGroup));
        textGo.layer = go.layer;
        textRt = (RectTransform)textGo.transform;
        textRt.SetParent(root, false);
        textRt.anchorMin = textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.pivot = new Vector2(0.5f, 0f);
        textRt.sizeDelta = new Vector2(praiseFontSize * 7f, praiseFontSize * 1.4f);
        text = textGo.GetComponent<TextMeshProUGUI>();
        textGroup = textGo.GetComponent<CanvasGroup>();
        textGroup.blocksRaycasts = false;
        textGroup.interactable = false;
        text.fontSize = praiseFontSize;
        text.alignment = TextAlignmentOptions.Bottom;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        TextSurfaceStyle.Apply(text, LightColor);   // Pop + beyaz yüz + altının koyusu kontur/gölge (paylaşılan materyal)
        text.fontSize = praiseFontSize;              // Apply font değişiminde puntoyu oranlar; övgüde sabit kalsın

        root.gameObject.SetActive(false);
    }

    private T NewPart<T>(string partName) where T : MaskableGraphic
    {
        var go = new GameObject(partName, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var graphic = go.GetComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    // ── Oynatma ────────────────────────────────────────────────────────────────────────────

    private IEnumerator Play(int clearedTiles, Vector3 worldOrigin)
    {
        int tier = PickTier(clearedTiles, out string label);
        GameEventSfx.PlayPraise(tier);   // 0 GOOD, 1 WOW, 2 GREAT
        float cell = CellSize();
        root.gameObject.SetActive(true);
        root.localScale = Vector3.one;
        group.alpha = 1f;

        text.text = label;
        textGroup.alpha = 0f;   // ilk karede yazı gizli başlasın (tam görünür bir kare yanıp sönmesin)
        root.anchoredPosition = ClampToBoard(ToAnchored(worldOrigin), cell);   // genişlik metin atandıktan sonra belli
        textRt.anchoredPosition = Vector2.zero;
        textRt.localScale = Vector3.one * MaterializeStartScale;
        float textHalf = Mathf.Max(text.preferredWidth * 0.5f, cell);

        int beamCount = tier == 0 ? 3 : tier == 1 ? 4 : 5;
        int sparkCount = tier == 0 ? 0 : tier == 1 ? 3 : MaxSparks;
        SetupBeams(beamCount, textHalf, cell);
        SetupSparks(sparkCount, textHalf, cell);
        var glowRt = glow.rectTransform;
        glowRt.sizeDelta = new Vector2(textHalf * 2.2f, cell * 1.1f);
        glowRt.anchoredPosition = new Vector2(0f, cell * 0.15f);

        // Durağan bekleme yok (kullanıcı, 2026-10-27): yazı oluşur oluşmaz sönmeye başlar ve tüm süre boyunca
        // yükselmeye devam eder; ekranda kaldığı her an hareketlidir. Toplam süre aynı kalır.
        float total = InDuration + holdDuration + OutDuration;
        float fadeStart = MaterializeDelay + MaterializeDuration;
        float riseTotal = (riseCells + 0.6f) * cell;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float u = t / total;
            float rise = (u * (2f - u) * 0.7f + u * 0.3f) * riseTotal;   // yavaşlar ama hiç durmaz
            float outK = Mathf.Clamp01((t - fadeStart) / (total - fadeStart));   // 0 → 1 sönüş
            textRt.anchoredPosition = new Vector2(0f, rise);
            float form = AnimateMaterialize(t, out float formScale);
            textRt.localScale = new Vector3(formScale * (1f - outK * 0.08f), formScale * (1f + outK * 0.18f), 1f);   // yukarı doğru uzayarak söner
            textGroup.alpha = form * (1f - outK * outK) * textOpacity;   // önce yavaş, sonda hızlı söner
            AnimateBeams(t, outK);
            AnimateSparks(t, sparkCount, total);
            float glowIn = Mathf.Clamp01(t / 0.12f);
            glow.canvasRenderer.SetAlpha(glowIn * (1f - outK));
            yield return null;
        }

        root.gameObject.SetActive(false);
        activeRoutine = null;
    }

    // Hologram gibi oluşum: alfa düşükten yükselir, yazı küçükten büyür (hafif aşma), ışık kararsızmış gibi
    // titreşir ve titreşim yazı netleştikçe söner. Dönüş: alfa çarpanı; formScale: yazı ölçeği.
    private static float AnimateMaterialize(float t, out float formScale)
    {
        float k = Mathf.Clamp01((t - MaterializeDelay) / MaterializeDuration);
        formScale = Mathf.LerpUnclamped(MaterializeStartScale, 1f, EaseOutBack(k, 0.9f));
        float alpha = k * k * (3f - 2f * k);                                   // yumuşak artış (düşük alfayla başlar)
        float flicker = Mathf.Repeat(t * 28f, 1f) < 0.5f ? 1f : 0.55f;        // ~14 Hz hologram titreşimi
        float settle = 1f - k;                                                 // oluştukça titreşim söner
        return alpha * Mathf.Lerp(1f, flicker, settle * 0.8f);
    }

    private void SetupBeams(int count, float textHalf, float cell)
    {
        for (int i = 0; i < MaxBeams; i++)
        {
            bool on = i < count;
            beams[i].gameObject.SetActive(on);
            if (!on) continue;
            // Hüzmeler yazının genişliğine yayılır; ortadakiler daha uzun.
            float lane = count == 1 ? 0f : Mathf.Lerp(-0.85f, 0.85f, i / (float)(count - 1));
            float centerBias = 1f - Mathf.Abs(lane) * 0.35f;
            beamSize[i] = new Vector2(cell * Random.Range(0.38f, 0.62f), cell * Random.Range(2.4f, 3.2f) * centerBias);
            beamDelay[i] = Random.Range(0f, 0.08f);
            var rt = beams[i].rectTransform;
            rt.anchoredPosition = new Vector2(lane * textHalf + Random.Range(-0.1f, 0.1f) * cell, -cell * 0.35f);
            rt.sizeDelta = new Vector2(beamSize[i].x, 0f);
            beams[i].canvasRenderer.SetAlpha(0f);
        }
    }

    private void AnimateBeams(float t, float outK)
    {
        for (int i = 0; i < MaxBeams; i++)
        {
            if (!beams[i].gameObject.activeSelf) continue;
            float grow = EaseOut(Mathf.Clamp01((t - beamDelay[i]) / 0.22f));
            var rt = beams[i].rectTransform;
            // Sönerken incelir ve yukarı doğru uzamaya devam eder.
            rt.sizeDelta = new Vector2(beamSize[i].x * (1f - outK * 0.7f), beamSize[i].y * (grow + outK * 0.25f));
            beams[i].canvasRenderer.SetAlpha(grow * (1f - outK));
        }
    }

    private void SetupSparks(int count, float textHalf, float cell)
    {
        for (int i = 0; i < MaxSparks; i++)
        {
            bool on = i < count;
            sparks[i].gameObject.SetActive(on);
            if (!on) continue;
            float size = cell * Random.Range(0.22f, 0.38f);
            sparks[i].rectTransform.sizeDelta = new Vector2(size, size);
            sparkFrom[i] = new Vector2(Random.Range(-1f, 1f) * textHalf, Random.Range(-0.2f, 0.4f) * cell);
            sparkVelocity[i] = new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1.6f, 2.6f)) * cell;
            sparkSpin[i] = Random.Range(-220f, 220f);
            sparks[i].canvasRenderer.SetAlpha(0f);
        }
    }

    private void AnimateSparks(float t, int count, float total)
    {
        for (int i = 0; i < count; i++)
        {
            float k = Mathf.Clamp01((t - 0.08f - i * 0.05f) / (total * 0.8f));
            var rt = sparks[i].rectTransform;
            rt.anchoredPosition = sparkFrom[i] + sparkVelocity[i] * EaseOut(k);
            rt.localRotation = Quaternion.Euler(0f, 0f, sparkSpin[i] * k);
            float alpha = k <= 0f ? 0f : Mathf.Sin(k * Mathf.PI);   // doğar → parlar → söner
            sparks[i].canvasRenderer.SetAlpha(alpha);
            rt.localScale = Vector3.one * (0.6f + 0.4f * alpha);
        }
    }

    // ── Konum ──────────────────────────────────────────────────────────────────────────────

    private float CellSize()
    {
        var host = transform as RectTransform;
        if (host == null || board == null || board.Width < 2) return praiseFontSize;
        Vector2 a = ToAnchored(board.GetCellWorldCenterPosition(0, 0));
        Vector2 b = ToAnchored(board.GetCellWorldCenterPosition(1, 0));
        return Mathf.Max(1f, Mathf.Abs(b.x - a.x));
    }

    // Yazı + yükselme tahtanın dışına taşmasın (üst sıra / kenar hamleleri).
    private Vector2 ClampToBoard(Vector2 p, float cell)
    {
        if (board == null || board.Width <= 0 || board.Height <= 0) return p;
        Vector2 a = ToAnchored(board.GetCellWorldCenterPosition(0, 0));
        Vector2 b = ToAnchored(board.GetCellWorldCenterPosition(board.Width - 1, board.Height - 1));
        float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
        float minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);
        float halfText = Mathf.Min(text.preferredWidth * 0.5f, (maxX - minX) * 0.5f);
        p.x = Mathf.Clamp(p.x, minX + halfText - cell * 0.5f, maxX - halfText + cell * 0.5f);
        float topRoom = (riseCells + 0.6f) * cell + text.fontSize;
        p.y = Mathf.Clamp(p.y, minY, Mathf.Max(minY, maxY + cell * 0.5f - topRoom));
        return p;
    }

    private Vector2 ToAnchored(Vector3 world)
    {
        // Parçaların anchor'ı host'un ortası → anchoredPosition = local - rect.center.
        var host = (RectTransform)transform;
        Vector2 local = host.InverseTransformPoint(world);
        return local - host.rect.center;
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    private static float EaseOutBack(float t, float c1)
    {
        float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    [ContextMenu("Preview WOW (Play Mode)")]
    private void PreviewWow()
    {
        if (!Application.isPlaying || board == null) return;
        HandleMoveClearPraise(45, board.GetCellWorldCenterPosition(board.Width / 2, board.Height - 2));
    }

    // 0 GOOD, 1 WOW, 2 GREAT — efekt büyüklüğü kademeyle artar (hüzme/parıltı sayısı).
    private static int PickTier(int clearedTiles, out string label)
    {
        if (clearedTiles >= 50) { label = GameLocalization.Get("praise_great"); return 2; }
        if (clearedTiles >= 40) { label = GameLocalization.Get("praise_wow"); return 1; }
        label = GameLocalization.Get("praise_good");
        return 0;
    }
}

public sealed class JaggedBadgeGraphic : MaskableGraphic
{
    [SerializeField] private int spikes = 18;
    [SerializeField, Range(0.35f, 0.95f)] private float innerRadius = 0.74f;

    public int Spikes
    {
        get => spikes;
        set
        {
            spikes = Mathf.Max(4, value);
            SetVerticesDirty();
        }
    }

    public float InnerRadius
    {
        get => innerRadius;
        set
        {
            innerRadius = Mathf.Clamp(value, 0.35f, 0.95f);
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        var rt = transform as RectTransform;
        if (rt == null)
            return;

        Rect rect = rt.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        Vector2 center = rect.center;
        float rx = rect.width * 0.5f;
        float ry = rect.height * 0.5f;
        int pointCount = Mathf.Max(8, spikes * 2);

        vh.AddVert(center, color, new Vector2(0.5f, 0.5f));

        for (int i = 0; i < pointCount; i++)
        {
            float angle = -Mathf.PI * 0.5f + (Mathf.PI * 2f * i / pointCount);
            float radius = (i & 1) == 0 ? 1f : innerRadius;
            Vector2 point = center + new Vector2(Mathf.Cos(angle) * rx * radius, Mathf.Sin(angle) * ry * radius);
            vh.AddVert(point, color, new Vector2((point.x - rect.xMin) / rect.width, (point.y - rect.yMin) / rect.height));
        }

        for (int i = 1; i <= pointCount; i++)
        {
            int next = i == pointCount ? 1 : i + 1;
            vh.AddTriangle(0, i, next);
        }
    }
}
