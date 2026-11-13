using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ara sıra level girişi şakası (kullanıcı 2026-11-13): tahta sağı aşağıda ~20° yamuk kayarak gelir, top HUD'un
/// altından tahta makara iner, Brumo soldan yürüyüp ipi tutar (ip: patiler → makara → tahtanın sağ üst köşesi),
/// iki-üç asılışta tahtayı yaylanarak düzeltir, başparmak kaldırır; makara yukarı çekilir, oyun başlar.
/// Ne zaman oynanacağı: LevelIntroFun (level 5 garantili, 6+ rastgele, Pemi ile ortak).
/// pencereleri şaka bitince gelir (öğretici board meşgulken beklemez, ipucu IsPlaying'i bekler).
/// İp, tahtanın sağ üstteki GERÇEK hücresine bağlanır (köşe boşluk/hole olabilir — ip havaya sarkmasın).
/// Dokununca anında atlanır. Yalnız giriş sırasında çalışır (board zaten meşgul); sonra kendini yok eder.
/// Görseller: Resources/BoardFixIntro/*. Ses yok (eklenirse merkezi GameEventSfx).
/// </summary>
public sealed class BoardFixIntro : MonoBehaviour, IPointerDownHandler
{
    public const float TiltDegrees = -20f;   // negatif = saat yönü → sağ taraf aşağıda (kullanıcı: 30 çok fazla)

    // Zamanlama (sn)
    private const float WalkIn = 0.6f, PulleyDrop = 0.35f, GrabHold = 0.18f, Pull = 1.0f, Win = 0.55f, Exit = 0.35f;
    private const float FrameStep = 0.14f;

    // Ölçüler (board ebeveyninin UI birimi)
    private const float BearHeight = 340f, WheelSize = 120f, BracketHeight = 190f, RopeThickness = 18f;
    // Halka ucunun TopBG alt kenarına göre ince ayarı (+ yukarı). TopBG görselinin altında saydam pay varsa buradan.
    private const float HudHangOffsetY = 140f;   // TopBG dikdörtgeni görünen kenardan aşağı uzanıyor (kullanıcı ekranı, 2026-11-13)

    // Patilerin ipi tuttuğu nokta, kare başına (x, y-alttan) normalize — Resources görsellerinden ölçüldü.
    private static readonly Vector2 GripPull0 = new Vector2(0.77f, 0.75f);
    private static readonly Vector2 GripPull1 = new Vector2(0.89f, 0.77f);
    private static readonly Vector2 GripGrab = new Vector2(0.92f, 0.90f);
    private static readonly Vector2 BracketAxle = new Vector2(0.501f, 0.20f);   // askının yuvarlak diskinin merkezi (silüetten)

    private static Sprite walk0, walk1, grab, pull0, pull1, win, wheelSprite, bracketSprite, ropeSprite;
    // Sesler (opsiyonel; dosya yoksa sessiz): Resources/BoardFixIntro/Audio/*. Merkezi GameEventSfx.PlayUiClip
    // (Ayarlar'da ses kapalıysa çalmaz).
    private static AudioClip sfxPulley, sfxTug, sfxSettle, sfxGrunt;

    private RectTransform target;
    private BoardController board;
    private Vector2Int anchorCell;
    private RectTransform root, bear, wheel, bracket, ropeA, ropeB;
    private const int ArcSegments = 4;
    private readonly RectTransform[] ropeArc = new RectTransform[ArcSegments];   // ipin tekerin üstünden dönen kısmı
    // Makara + askı HUD'un ARKASINDA çizilir (kullanıcı 2026-11-13: HUD'un üstünden iniyordu, ucu rakamlara değiyordu);
    // ip, Brumo ve tahta root'ta (tahtanın üstünde). Konumlar root uzayında tutulur, dünya üzerinden aktarılır.
    private RectTransform pulleyLayer;
    private Vector2 pulleyCenter;
    private Image bearImage;
    private Vector2 grip;
    private bool skip;

#if UNITY_EDITOR
    public const string DebugPauseKey = "debug_boardfix_pause_at_pulley";
#endif

    /// Görseller yüklenebiliyor mu? (Ne zaman oynanacağına LevelIntroFun karar verir.)
    public static bool CanPlay() => LoadSprites();

    private static bool LoadSprites()
    {
        if (pull0 != null) return true;
        const string P = "BoardFixIntro/";
        walk0 = Resources.Load<Sprite>(P + "Bear_Walk_0");
        walk1 = Resources.Load<Sprite>(P + "Bear_Walk_1");
        grab = Resources.Load<Sprite>(P + "Bear_Pull_Grab");
        pull0 = Resources.Load<Sprite>(P + "Bear_Pull_0");
        pull1 = Resources.Load<Sprite>(P + "Bear_Pull_1");
        win = Resources.Load<Sprite>(P + "Bear_Pull_Win");
        wheelSprite = Resources.Load<Sprite>(P + "Pulley_Wheel");
        bracketSprite = Resources.Load<Sprite>(P + "Pulley_Bracket");
        ropeSprite = Resources.Load<Sprite>(P + "Rope_Tile");
        sfxPulley = Resources.Load<AudioClip>(P + "Audio/BoardFix_PulleyDrop");
        sfxTug = Resources.Load<AudioClip>(P + "Audio/BoardFix_Tug");
        sfxSettle = Resources.Load<AudioClip>(P + "Audio/BoardFix_Settle");
        sfxGrunt = Resources.Load<AudioClip>(P + "Audio/BoardFix_Grunt");
        return walk0 && walk1 && grab && pull0 && pull1 && win && wheelSprite && bracketSprite && ropeSprite;
    }

    /// Tahta (yamuk hâlde, yerine oturmuş) → düzeltme sahnesi. Bitince açı 0, sahne yok edilir.
    public static IEnumerator Play(BoardController board, RectTransform boardRoot)
    {
        if (board == null || boardRoot == null || boardRoot.parent == null || board.TilesRoot == null) yield break;
        var go = new GameObject("BoardFixIntro", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var intro = go.AddComponent<BoardFixIntro>();
        intro.board = board;
        LevelIntroFun.IsPlaying = true;
        try { yield return intro.Run(boardRoot); }
        finally
        {
            LevelIntroFun.IsPlaying = false;
            if (boardRoot != null) boardRoot.localRotation = Quaternion.identity;
            if (intro != null) Destroy(intro.gameObject);
        }
    }

    // Sağ üstteki gerçek hücre (hole değil, ya da üstünde engel var): x - y en büyük, eşitlikte en sağdaki.
    private Vector2Int FindAnchorCell()
    {
        var holes = board.Holes;
        var obstacles = board.ObstacleStateService;
        var best = new Vector2Int(board.Width - 1, 0);
        int bestScore = int.MinValue;
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            bool real = holes == null || !holes[x, y] || (obstacles != null && obstacles.HasObstacleAt(x, y));
            int score = x - y;
            if (!real || score < bestScore || (score == bestScore && x <= best.x)) continue;
            bestScore = score;
            best = new Vector2Int(x, y);
        }
        return best;
    }

    public void OnPointerDown(PointerEventData eventData) => skip = true;

    private IEnumerator Run(RectTransform boardRoot)
    {
        target = boardRoot;
        int layer = target.gameObject.layer;
        root = (RectTransform)transform;
        root.SetParent(target.parent, false);
        root.SetSiblingIndex(target.GetSiblingIndex() + 1);   // tahtanın üstünde, sonraki (HUD) kardeşlerin altında
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        gameObject.layer = layer;
        var catcher = GetComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);   // görünmez; dokunuşu yakalar (atla)

        // Hedef geometri: tahta düz olsaydı köşe nerede olurdu → makara onun üstünde.
        anchorCell = FindAnchorCell();
        Quaternion tilt = target.localRotation;
        target.localRotation = Quaternion.identity;
        var r = target.rect;
        Vector2 straightCorner = CornerPoint();
        Vector2 boardLeftMid = Local(target.TransformPoint(new Vector3(r.xMin, r.center.y)));
        target.localRotation = tilt;

        Vector2 pulleyPos = straightCorner + new Vector2(-30f, 190f);
        Vector2 pulleyStart = pulleyPos + new Vector2(0f, 420f);
        // Makara HUD'dan sarkar (kullanıcı 2026-11-13): yatayda hamle sayacının hizası, ip halkasının UCU top HUD arka
        // plan görselinin (TopBG) bittiği alt kenarda. İniş HUD'un arkasından başlar (makara katmanı HUD'un arkasında).
        var hudBg = board.TopHud != null ? board.TopHud.BackgroundRect : null;
        var badge = board.TopHud != null ? board.TopHud.MovesBadge : null;
        if (hudBg != null)
        {
            var hc = new Vector3[4];
            hudBg.GetWorldCorners(hc);
            float hangY = Local(hc[0]).y + HudHangOffsetY;
            float hangX = pulleyPos.x;
            if (badge != null)
            {
                badge.GetWorldCorners(hc);
                hangX = Local((hc[0] + hc[3]) * 0.5f).x;
            }
            float topAboveAxle = (1f - BracketAxle.y) * BracketHeight;
            pulleyPos = new Vector2(hangX, hangY - topAboveAxle);
            pulleyStart = pulleyPos + new Vector2(0f, topAboveAxle + 40f);
        }
        Vector2 bearFoot = new Vector2(boardLeftMid.x + 150f, boardLeftMid.y - 260f);
        Vector2 bearStart = new Vector2(root.rect.xMin - 260f, bearFoot.y);

        ropeA = NewRope("RopeA", layer);
        ropeB = NewRope("RopeB", layer);
        for (int i = 0; i < ArcSegments; i++) ropeArc[i] = NewRope("RopeArc" + i, layer);
        pulleyLayer = CreatePulleyLayer(layer);
        wheel = NewImage("PulleyWheel", wheelSprite, layer, new Vector2(0.5f, 0.5f), new Vector2(WheelSize, WheelSize), pulleyLayer);
        bracket = NewImage("PulleyBracket", bracketSprite, layer, BracketAxle,
            new Vector2(BracketHeight * bracketSprite.rect.width / bracketSprite.rect.height, BracketHeight), pulleyLayer);
        bear = NewImage("Brumo", walk0, layer, new Vector2(0.5f, 0f), Vector2.one * BearHeight);
        bearImage = bear.GetComponent<Image>();
        SetRopes(false);

        // 1) Makara iner + Brumo yürüyerek gelir.
        GameEventSfx.PlayUiClip(sfxPulley);
        for (float t = 0f; t < WalkIn && !skip; t += Time.deltaTime)
        {
            float k = t / WalkIn;
            SetPulley(Vector2.LerpUnclamped(pulleyStart, pulleyPos, EaseOutBack(Mathf.Clamp01(t / PulleyDrop))));
            SetBear((int)(t / FrameStep) % 2 == 0 ? walk0 : walk1, default);
            bear.anchoredPosition = Vector2.Lerp(bearStart, bearFoot, 1f - (1f - k) * (1f - k))
                                    + new Vector2(0f, Mathf.Abs(Mathf.Sin(t * 14f)) * 8f);
            yield return null;
        }
        SetPulley(pulleyPos);
        bear.anchoredPosition = bearFoot;
#if UNITY_EDITOR
        // Ölçüm: TinyFixers/Debug menüsünden açıldıysa makara yerine inince editör duraklar (konumu incelemek için).
        if (PlayerPrefs.GetInt(DebugPauseKey, 0) == 1) Debug.Break();
#endif

        // 2) İpi yakalar.
        SetBear(grab, GripGrab);
        SetRopes(true);
        for (float t = 0f; t < GrabHold && !skip; t += Time.deltaTime) { UpdateRopes(); yield return null; }

        // 3) Asılır: tahta yaylanarak düzelir, makara döner, Brumo biraz geri gider.
        float startAngle = target.localEulerAngles.z > 180f ? target.localEulerAngles.z - 360f : target.localEulerAngles.z;
        float lastRope = RopeLength();
        GameEventSfx.PlayUiClip(sfxGrunt);
        int lastTugFrame = -1;
        for (float t = 0f; t < Pull && !skip; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / Pull);
            target.localRotation = Quaternion.Euler(0f, 0f, startAngle * (1f - EaseOutBack(k)));
            int tugFrame = (int)(t / FrameStep);
            bool alt = tugFrame % 2 == 1;
            if (!alt && tugFrame != lastTugFrame && tugFrame < 6) GameEventSfx.PlayUiClip(sfxTug);   // her asılışta ip gıcırtısı
            lastTugFrame = tugFrame;
            SetBear(alt ? pull1 : pull0, alt ? GripPull1 : GripPull0);
            bear.anchoredPosition = bearFoot + new Vector2(-40f * k, 0f);
            UpdateRopes();
            float len = RopeLength();
            wheel.localRotation *= Quaternion.Euler(0f, 0f, (lastRope - len) / (WheelSize * 0.5f) * Mathf.Rad2Deg);
            lastRope = len;
            yield return null;
        }
        target.localRotation = Quaternion.identity;
        if (!skip) GameEventSfx.PlayUiClip(sfxSettle);   // tahta "tık" diye yerine oturur

        // 4) Başparmak; ip bırakılır.
        SetRopes(false);
        SetBear(win, default);
        for (float t = 0f; t < Win && !skip; t += Time.deltaTime) yield return null;

        // 5) Brumo sola çıkar, makara yukarı.
        Vector2 bearAt = bear.anchoredPosition;
        bear.localScale = new Vector3(-1f, 1f, 1f);   // sola dönük yürür
        for (float t = 0f; t < Exit && !skip; t += Time.deltaTime)
        {
            float k = t / Exit;
            SetBear((int)(t / FrameStep) % 2 == 0 ? walk0 : walk1, default);
            bear.anchoredPosition = Vector2.Lerp(bearAt, bearStart, k * k);
            SetPulley(Vector2.Lerp(pulleyPos, pulleyStart, k * k));
            yield return null;
        }

        target.localRotation = Quaternion.identity;   // atlansa da tahta düz kalır (yok etme Play'in finally'sinde)
    }

    private Vector2 Local(Vector3 world) => root.InverseTransformPoint(world);

    // HUD'un hemen arkasında (aynı ebeveynde, HUD'dan önceki kardeş) tam ekran katman; HUD yoksa root.
    private RectTransform CreatePulleyLayer(int layer)
    {
        var hud = board.TopHud != null ? board.TopHud.transform : null;
        if (hud == null || hud.parent == null) return root;
        var rt = (RectTransform)new GameObject("BoardFixPulleyLayer", typeof(RectTransform)).transform;
        rt.gameObject.layer = hud.gameObject.layer;
        rt.SetParent(hud.parent, false);
        rt.SetSiblingIndex(hud.GetSiblingIndex());   // HUD bundan sonra çizilir → makara arkada kalır
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private void OnDestroy()
    {
        if (pulleyLayer != null && pulleyLayer != root) Destroy(pulleyLayer.gameObject);
    }

    private RectTransform NewImage(string name, Sprite sprite, int layer, Vector2 pivot, Vector2 size, RectTransform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent != null ? parent : root, false);
        if (parent != null) go.layer = parent.gameObject.layer;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return rt;
    }

    private RectTransform NewRope(string name, int layer)
    {
        var rt = NewImage(name, ropeSprite, layer, new Vector2(0f, 0.5f), new Vector2(1f, RopeThickness));
        var img = rt.GetComponent<Image>();
        img.preserveAspect = false;
        img.type = Image.Type.Tiled;
        img.pixelsPerUnitMultiplier = ropeSprite.rect.height / RopeThickness;   // ip kalınlığı = RopeThickness
        return rt;
    }

    private void SetPulley(Vector2 pos)
    {
        pulleyCenter = pos;
        Vector3 world = root.TransformPoint(pos);
        wheel.position = world;
        bracket.position = world;
        if (ropeA.gameObject.activeSelf) UpdateRopes();
    }

    private void SetBear(Sprite sprite, Vector2 gripNorm)
    {
        bearImage.sprite = sprite;
        bear.sizeDelta = new Vector2(BearHeight * sprite.rect.width / sprite.rect.height, BearHeight);
        grip = gripNorm;
    }

    private Vector2 GripPoint() =>
        bear.anchoredPosition + new Vector2((grip.x - 0.5f) * bear.sizeDelta.x * bear.localScale.x, grip.y * bear.sizeDelta.y);

    // Bağlantı hücresinin sağ üst köşesi (biraz içeride). TilesRoot'ta hücre (x*ts, -y*ts) sol üst köşesidir.
    private Vector2 CornerPoint()
    {
        float ts = board.TileSize, inset = ts * 0.18f;
        return Local(board.TilesRoot.TransformPoint(
            new Vector3((anchorCell.x + 1) * ts - inset, -anchorCell.y * ts - inset, 0f)));
    }

    private void SetRopes(bool on)
    {
        ropeA.gameObject.SetActive(on);
        ropeB.gameObject.SetActive(on);
        for (int i = 0; i < ArcSegments; i++) ropeArc[i].gameObject.SetActive(on);
        if (on) UpdateRopes();
    }

    // İp tekerin oluğuna sarılı (kullanıcı 2026-11-13: "ip makaranın üstünden dönmeli"): patilerden tekere teğet,
    // tekerin üstünden saat yönünde yay, tekerden tahtanın köşesine teğet. Teğetler ipin dönüş yönüne göre seçilir
    // (köşe makaranın sol-altında olsa da ip ters tur atmaz).
    private void UpdateRopes()
    {
        Vector2 p = pulleyCenter, g = GripPoint(), c = CornerPoint();
        float radius = WheelSize * 0.42f;   // oluk yarıçapı
        float aIn = TangentAngle(p, g, radius, arriving: true);
        float aOut = TangentAngle(p, c, radius, arriving: false);
        Vector2 tIn = OnCircle(p, aIn, radius), tOut = OnCircle(p, aOut, radius);
        Stretch(ropeA, g, tIn);
        Stretch(ropeB, tOut, c);
        // Saat yönünde (açı azalarak) aIn → aOut.
        while (aOut > aIn) aOut -= 2f * Mathf.PI;
        while (aIn - aOut > 2f * Mathf.PI) aOut += 2f * Mathf.PI;
        Vector2 prev = tIn;
        for (int i = 1; i <= ArcSegments; i++)
        {
            Vector2 next = OnCircle(p, Mathf.Lerp(aIn, aOut, i / (float)ArcSegments), radius);
            Stretch(ropeArc[i - 1], prev, next);
            prev = next;
        }
    }

    private static Vector2 OnCircle(Vector2 c, float ang, float r) => c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;

    // Dış noktadan çembere teğet (radyan). Saat yönünde sarılan ipte teğet yönü T(a) = (sin a, -cos a):
    // gelen ip (nokta → teğet) bu yönde, giden ip (teğet → nokta) da bu yönde ilerler.
    private static float TangentAngle(Vector2 center, Vector2 point, float radius, bool arriving)
    {
        Vector2 d = point - center;
        float dist = Mathf.Max(d.magnitude, radius + 0.01f);
        float baseAng = Mathf.Atan2(d.y, d.x), off = Mathf.Acos(radius / dist);
        float a1 = baseAng + off, a2 = baseAng - off;
        Vector2 t1 = OnCircle(center, a1, radius);
        Vector2 travel1 = arriving ? t1 - point : point - t1;
        return Vector2.Dot(travel1, new Vector2(Mathf.Sin(a1), -Mathf.Cos(a1))) > 0f ? a1 : a2;
    }

    private float RopeLength()
    {
        Vector2 p = pulleyCenter;
        return Vector2.Distance(GripPoint(), p) + Vector2.Distance(p, CornerPoint());
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
