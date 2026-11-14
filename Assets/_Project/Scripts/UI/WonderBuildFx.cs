using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// İnşaat modu görev efektleri (iki resimli harika): parçanın üstünde toz bulutu, çekiçle vuran karakter
/// ve bitişte uçuşan yıldızlar. Yüksek parçalarda karakterler tepedeki vinç kirişinden iplerle inen gondolda
/// çalışır (oyuncu yorumu: havada inşaat garip). Zaman çizelgesini WonderRevealView sürer; burada Update yok.
/// Image'lar harika başına bir kez kurulur ve her görevde yeniden kullanılır. [[project_wonder_reveal_background]]
/// </summary>
public sealed class WonderBuildFx : MonoBehaviour
{
    const int DustCount = 16, StarCount = 10;
    const float SwingPeriod = 0.62f, PopTime = 0.2f, StarLife = 0.9f;
    // 4 kare (kaldır, savur, vur, sek): kaldırışta bekler, vuruş hızlı. Vuruş anı = 3. karenin başı.
    static readonly float[] FourFrameEnds = { 0.42f, 0.54f, 0.8f, 1f };
    const float FourFrameHit = 0.54f, EvenHit = 0.75f;
    static readonly Color DustColor = new Color(0.93f, 0.86f, 0.74f, 1f);
    static AudioClip s_hitClip;
    static bool s_hitLoaded;

    RectTransform _view;
    Image _hammerer, _tool;
    Sprite[] _hammerFrames;
    readonly Image[] _dust = new Image[DustCount];
    readonly Vector2[] _dustHome = new Vector2[DustCount];
    readonly float[] _dustSize = new float[DustCount];
    readonly float[] _dustSeed = new float[DustCount];
    readonly Image[] _stars = new Image[StarCount];
    readonly Vector2[] _starVel = new Vector2[StarCount];
    readonly float[] _starSize = new float[StarCount];
    Vector2 _starOrigin, _hammererPos;
    float _hammererH;
    int _lastHit = -1;
    float _workEndedAt = -1f;

    // ── Vinç: kiriş (sabit) + araba (kayar) + 2 ip + gondol ──
    const float GondolaMinCenter = 0.45f;   // parça merkezi resmin bu oranından yüksekse gondol gelir
    const float RopeThickness = 10f;
    const float CraneTopMargin = 0.06f;     // kirişin ekran üstünden payı (görünüm yüksekliğine oran)
    // Görsel ölçüleri (Resources/WonderBuild): ray kirişin altından %32'de; arabanın üst tekerlek merkezi altından %88'de,
    // alt makaralar %17'de, x %18 / %83. Gondol kancaları üst köşelerde.
    // Ölçülen noktalar (2026-11-14): makara merkezleri x %16.9 / %83.0, alttan %16.9, yarıçap = genişliğin %11.4'ü;
    // gondol kancaları x %5.4 / %90.3 (sağda kova var → asimetrik), alttan %94.
    const float BeamRailFromBottom = 0.32f, TrolleyWheelFromBottom = 0.88f, TrolleyPulleyFromBottom = 0.169f;
    const float TrolleyPulleyLeft = 0.169f, TrolleyPulleyRight = 0.830f, TrolleyPulleyRadius = 0.114f;
    const float GondolaHookLeft = 0.054f, GondolaHookRight = 0.903f, GondolaHookFromBottom = 0.94f;
    const int RopeArcSegments = 4;            // ip makaranın tepesinden dış yanına dolanır
    Image _beam, _trolley, _gondola;
    readonly Image[] _ropes = new Image[2 * (RopeArcSegments + 1)];
    bool _crane;
    float _gx, _gw, _gh, _tw, _th, _railY, _finalBottom, _tuckedBottom, _startX, _endX;
    public bool UsesCrane => _crane;
    /// Gondolun son konumuna göre anlık kayması (karakterler bununla taşınır).
    public Vector2 CraneOffset { get; private set; }
    /// Gondolda kaynakçının ayak noktası (gondol son konumunda).
    public Vector2 WelderFeet { get; private set; }

    public static WonderBuildFx Create(RectTransform view, WonderDefinition def)
    {
        var back = NewLayer("BuildDust", view);
        back.SetAsFirstSibling();   // toz resmin üstünde, kaynakçının arkasında
        var front = NewLayer("BuildFront", view);
        front.SetAsLastSibling();   // vuran karakter + yıldızlar önde
        var crane = NewLayer("BuildCrane", view);
        crane.SetAsLastSibling();   // gondolun ön paneli karakterlerin bacaklarını örter
        var fx = front.gameObject.AddComponent<WonderBuildFx>();
        fx.Init(view, back, front, def);
        fx.InitCrane(crane);
        return fx;
    }

    void InitCrane(RectTransform layer)
    {
        var beam = Resources.Load<Sprite>("WonderBuild/Crane_Beam");
        var trolley = Resources.Load<Sprite>("WonderBuild/Crane_Trolley");
        var gondola = Resources.Load<Sprite>("PaintCleanIntro/Gondola/Gondola_Front");
        var rope = Resources.Load<Sprite>("BoardFixIntro/Rope_Tile");
        if (beam == null || trolley == null || gondola == null || rope == null) return;   // görsel yoksa eski yer düzeni
        _beam = NewImage("CraneBeam", layer, beam);
        _beam.preserveAspect = false;
        _trolley = NewImage("CraneTrolley", layer, trolley);
        _trolley.preserveAspect = false;
        _trolley.rectTransform.pivot = new Vector2(0.5f, 0f);
        // İpler arabanın ÖNÜNDE (makaranın üstünden dolanırken görünsün), gondolun arkasında.
        for (int i = 0; i < _ropes.Length; i++)
        {
            var r = NewImage("CraneRope", layer, rope);
            r.preserveAspect = false;
            r.type = Image.Type.Tiled;
            r.pixelsPerUnitMultiplier = rope.rect.height / RopeThickness;
            r.rectTransform.pivot = new Vector2(0f, 0.5f);
            _ropes[i] = r;
        }
        _gondola = NewImage("CraneGondola", layer, gondola);
        _gondola.preserveAspect = false;
        _gondola.rectTransform.pivot = new Vector2(0.5f, 0f);
    }

    static RectTransform NewLayer(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        // Sıfır boyutlu katman görünümün pivotunda → çocuk konumları görünümün rect koordinatlarıyla aynı.
        rt.anchorMin = rt.anchorMax = parent.pivot;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    static Image NewImage(string name, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
        { layer = parent.gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        go.SetActive(false);
        return img;
    }

    void Init(RectTransform view, RectTransform back, RectTransform front, WonderDefinition def)
    {
        _view = view;
        var dust = def.buildDustSprite != null ? def.buildDustSprite : Resources.Load<Sprite>("Hamster/Hamster_Dust");
        for (int i = 0; i < DustCount; i++)
        {
            _dust[i] = NewImage("Dust", back, dust);
            _dust[i].color = DustColor;
        }
        _hammerFrames = def.buildHammerFrames;
        if (def.buildShowHammerer && _hammerFrames != null && _hammerFrames.Length > 0)
        {
            _hammerer = NewImage("Hammerer", front, _hammerFrames[0]);
            var first = _hammerFrames[0];
            // Pivot = sprite'ın kendi pivotu (BearHammer: botların ortası) → kareler değişirken ayaklar sabit.
            _hammerer.rectTransform.pivot = first != null && first.rect.size.x > 0f
                ? first.pivot / first.rect.size : new Vector2(0.5f, 0f);
            // Çekiç karelerin içinde çiziliyse buildHammerTool boş bırakılır; ayrı döndürülen alet yalnız atanınca.
            if (def.buildHammerTool != null)
            {
                _tool = NewImage("HammerTool", front, def.buildHammerTool);
                _tool.rectTransform.pivot = new Vector2(0.5f, 0.12f);   // sap ucundan döner
            }
        }
        if (def.buildStarSprite != null)
            for (int i = 0; i < StarCount; i++)
                _stars[i] = NewImage("Star", front, def.buildStarSprite);
    }

    /// Görev parçası (görünüm yerel koordinatı) için tozu ve vuran karakteri yerleştirir.
    public void Begin(Rect piece)
    {
        Clear();
        float cw = piece.width / 4f, ch = piece.height / 4f;
        float size = Mathf.Clamp(Mathf.Max(cw, ch) * 3f, 220f, 640f);   // büyük, yoğun bulut
        for (int i = 0; i < DustCount; i++)
        {
            int gx = i % 4, gy = i / 4;
            _dustHome[i] = new Vector2(piece.xMin + (gx + 0.5f + Random.Range(-0.3f, 0.3f)) * cw,
                piece.yMin + (gy + 0.5f + Random.Range(-0.3f, 0.3f)) * ch);
            _dustSize[i] = size * Random.Range(0.85f, 1.15f);
            _dustSeed[i] = Random.Range(0f, 100f);
        }
        // Resim ekranı doldurmak için yanlardan kırpılır: yerleşim GÖRÜNEN alana göre (Kanyon'da ayı ekran dışındaydı).
        Rect bounds = VisibleRect();
        Rect image = _view.rect;
        bool craneWanted = _gondola != null && image.height > 0f
            && (piece.center.y - image.yMin) / image.height >= GondolaMinCenter;
        // Perspektif: boy, ayakların ekrandaki yüksekliğinden — aşağıda (yakın) büyük, yukarıda (uzak) küçük.
        _hammererH = PerspectiveHeight(craneWanted ? piece.yMin + piece.height * 0.3f : piece.yMin, bounds);
        if (craneWanted) PlanCrane(piece, bounds);
        // Yerde: iki karakter parçanın ortasında yan yana (kaynakçı solda, ayı sağda); ikisinin toplam genişliği
        // görünen alana sığacak kadar kenardan içeride (Pisa yan binalarda maymun soldan dışarıda kalıyordu).
        float span = _hammererH * 1.35f;
        WorkX = bounds.width > span * 2f ? Mathf.Clamp(piece.center.x, bounds.xMin + span, bounds.xMax - span) : bounds.center.x;
        if (_hammerer == null) return;
        var sprite = _hammerFrames != null && _hammerFrames.Length > 0 ? _hammerFrames[0] : null;
        float frameH = _hammererH / HammererBodyFraction;
        float w = sprite != null && sprite.rect.height > 0f ? frameH * sprite.rect.width / sprite.rect.height : frameH * 0.9f;
        float x = WorkX + _hammererH * 0.6f;
        _hammererPos = _crane ? new Vector2(_gx + _gw * 0.2f, FloorY)
            : new Vector2(x, piece.yMin + _hammererH * 0.05f);
        var rt = _hammerer.rectTransform;
        rt.sizeDelta = new Vector2(w, frameH);
        rt.anchoredPosition = _hammererPos + CraneOffset;
        // Önceki görevin sönme durumu kalmasın: gondolla gelirken de görünür (Eyfel'de Brumo görünmüyordu).
        _hammerer.sprite = _hammerFrames[0];
        rt.localScale = new Vector3(-1f, 1f, 1f);
        var hc = _hammerer.color; hc.a = 1f; _hammerer.color = hc;
        _hammerer.gameObject.SetActive(true);
        if (_tool != null)
        {
            _tool.rectTransform.sizeDelta = new Vector2(_hammererH * 0.5f, _hammererH * 0.5f);
            _tool.gameObject.SetActive(true);
        }
    }

    float FloorY => _finalBottom + _gh * 0.42f;   // ayaklar ön panelin arkasında kalır

    /// Yerdeki görevde iki karakterin ortası (kaynakçı solunda, ayı sağında çalışır).
    public float WorkX { get; private set; }

    /// Bu görevde karakterlerin boyu (kaynakçı da buna göre ölçeklenir).
    public float CharacterHeight => _hammererH;

    static readonly Vector3[] s_corners = new Vector3[4];
    const float NearHeight = 0.15f, FarHeight = 0.06f;
    // Ölçüldü (2026-11-14): BearHammer çerçevesi kalkık çekici de içerir, gövde (baş→ayak) çerçevenin %68'i.
    // CharacterHeight = görünen gövde boyu; çerçeve buna göre büyütülür → ayı ile maymun aynı boyda görünür.
    const float HammererBodyFraction = 0.68f;   // görünür yüksekliğe oran: ekran altı / ekran üstü

    float PerspectiveHeight(float feetY, Rect visible)
    {
        float t = visible.height > 0f ? Mathf.Clamp01((feetY - visible.yMin) / visible.height) : 0.5f;
        return Mathf.Max(60f, visible.height * Mathf.Lerp(NearHeight, FarHeight, t));
    }

    /// Görünümün ekranda gerçekten görünen kısmı (ebeveyn tam ekran kap), görünüm yerel koordinatında.
    public Rect VisibleRect()
    {
        Rect r = _view.rect;
        if (!(_view.parent is RectTransform parent)) return r;
        var corners = s_corners;
        parent.GetWorldCorners(corners);
        Vector2 a = _view.InverseTransformPoint(corners[0]), b = _view.InverseTransformPoint(corners[2]);
        Rect p = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        Rect v = Rect.MinMaxRect(Mathf.Max(r.xMin, p.xMin), Mathf.Max(r.yMin, p.yMin), Mathf.Min(r.xMax, p.xMax), Mathf.Min(r.yMax, p.yMax));
        return v.width > 1f && v.height > 1f ? v : r;
    }

    void PlanCrane(Rect piece, Rect bounds)
    {
        _crane = true;
        var gs = _gondola.sprite.rect.size;
        var ts = _trolley.sprite.rect.size;
        _gw = Mathf.Clamp(_hammererH * 3f, 240f, 680f);   // iki karakter yan yana sığsın
        _gh = _gw * gs.y / gs.x;
        _gx = Mathf.Clamp(piece.center.x, bounds.xMin + _gw * 0.5f + 8f, bounds.xMax - _gw * 0.5f - 8f);
        float floor = piece.yMin + piece.height * 0.3f;
        _finalBottom = floor - _gh * 0.42f;
        WelderFeet = new Vector2(_gx - _gw * 0.2f, floor);
        _tw = _gw * 0.62f;
        _th = _tw * ts.y / ts.x;
        float beamH = bounds.width * _beam.sprite.rect.height / _beam.sprite.rect.width;
        // Kiriş her zaman ekranın en üstünde (kullanıcı 2026-11-14: ortada durunca dağınık görünüyordu); gondol
        // oradan uzun iplerle iner. Üst pay: çentik/durum çubuğu altında kalsın.
        _railY = bounds.yMax - bounds.height * CraneTopMargin - beamH * (1f - BeamRailFromBottom);
        var brt = _beam.rectTransform;
        brt.sizeDelta = new Vector2(bounds.width, beamH);
        brt.anchoredPosition = new Vector2(bounds.center.x, _railY - beamH * (BeamRailFromBottom - 0.5f));
        _trolley.rectTransform.sizeDelta = new Vector2(_tw, _th);
        _gondola.rectTransform.sizeDelta = new Vector2(_gw, _gh);
        float trolleyBottom = _railY - _th * TrolleyWheelFromBottom;
        _tuckedBottom = Mathf.Max(_finalBottom, trolleyBottom - 24f - _gh);
        float edge = Mathf.Max(_tw, _gw);
        bool fromRight = _gx < bounds.center.x;
        _startX = fromRight ? bounds.xMax + edge : bounds.xMin - edge;
        _endX = fromRight ? bounds.xMin - edge : bounds.xMax + edge;
        _beam.gameObject.SetActive(true);
        _trolley.gameObject.SetActive(true);
        _gondola.gameObject.SetActive(true);
        foreach (var r in _ropes) r.gameObject.SetActive(true);
        ApplyCrane(_startX, 0f, 0f);
    }

    /// Geliş: araba kenardan parçanın üstüne kayar (kiriş belirir), sonra gondol iner. p 0..1.
    public void TickCraneIn(float p)
    {
        if (!_crane) return;
        float x = p < 0.45f ? Mathf.Lerp(_startX, _gx, Smooth01(p / 0.45f)) : _gx;
        float drop = p < 0.45f ? 0f : Smooth01((p - 0.45f) / 0.55f);
        ApplyCrane(x, drop, Mathf.Clamp01(p / 0.25f));
    }

    /// Dönüş: gondol yukarı çekilir, araba karşı kenara kayıp çıkar, kiriş söner. p 0..1.
    public void TickCraneOut(float p)
    {
        if (!_crane) return;
        float drop = p < 0.55f ? 1f - Smooth01(p / 0.55f) : 0f;
        float x = p < 0.55f ? _gx : Mathf.Lerp(_gx, _endX, Smooth01((p - 0.55f) / 0.45f));
        ApplyCrane(x, drop, 1f - Mathf.Clamp01((p - 0.75f) / 0.25f));
    }

    void ApplyCrane(float trolleyX, float drop, float beamAlpha)
    {
        float bottom = Mathf.Lerp(_tuckedBottom, _finalBottom, drop);
        CraneOffset = new Vector2(trolleyX - _gx, bottom - _finalBottom);
        var bc = _beam.color; bc.a = beamAlpha; _beam.color = bc;
        float trolleyBottom = _railY - _th * TrolleyWheelFromBottom;
        _trolley.rectTransform.anchoredPosition = new Vector2(trolleyX, trolleyBottom);
        _gondola.rectTransform.anchoredPosition = new Vector2(trolleyX, bottom);
        float pulleyY = trolleyBottom + _th * TrolleyPulleyFromBottom;
        float hookY = bottom + _gh * GondolaHookFromBottom;
        float radius = _tw * TrolleyPulleyRadius * 0.92f;   // ipin oturduğu oluk, dış kenarın biraz içinde
        WrapRope(0, new Vector2(trolleyX + _tw * (TrolleyPulleyLeft - 0.5f), pulleyY), radius,
            new Vector2(trolleyX + _gw * (GondolaHookLeft - 0.5f), hookY), -1f);
        WrapRope(RopeArcSegments + 1, new Vector2(trolleyX + _tw * (TrolleyPulleyRight - 0.5f), pulleyY), radius,
            new Vector2(trolleyX + _gw * (GondolaHookRight - 0.5f), hookY), 1f);
        if (_hammerer != null && _hammerer.gameObject.activeSelf)
        {
            var rt = _hammerer.rectTransform;
            rt.anchoredPosition = _hammererPos + CraneOffset;
            if (drop < 0.999f || trolleyX != _gx)
            {
                _hammerer.sprite = _hammerFrames[0];
                rt.localScale = new Vector3(-1f, 1f, 1f);
            }
        }
    }

    /// İp makaranın tepesinden (90°) dış yanı boyunca dolanır, teğet noktasından kancaya iner.
    /// side: -1 sol makara (dışı solda), +1 sağ makara.
    void WrapRope(int first, Vector2 center, float r, Vector2 hook, float side)
    {
        Vector2 toHook = hook - center;
        float d = Mathf.Max(r + 0.01f, toHook.magnitude);
        float baseAngle = Mathf.Atan2(toHook.y, toHook.x);
        float off = Mathf.Acos(Mathf.Clamp(r / d, -1f, 1f));
        // Dış teğet: sol makarada açı büyüyerek (saat yönü tersi), sağda küçülerek dolanır.
        float tangent = side < 0f ? baseAngle - off : baseAngle + off;
        float start = Mathf.PI * 0.5f;
        if (side < 0f) { while (tangent < start) tangent += Mathf.PI * 2f; }
        else { while (tangent > start) tangent -= Mathf.PI * 2f; }
        Vector2 prev = center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * r;
        for (int k = 1; k <= RopeArcSegments; k++)
        {
            float a = Mathf.Lerp(start, tangent, k / (float)RopeArcSegments);
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            SetRope(_ropes[first + k - 1], prev, p);
            prev = p;
        }
        SetRope(_ropes[first + RopeArcSegments], prev, hook);
    }

    static void SetRope(Image rope, Vector2 from, Vector2 to)
    {
        var d = to - from;
        var rt = rope.rectTransform;
        rt.anchoredPosition = from;
        rt.sizeDelta = new Vector2(Mathf.Max(1f, d.magnitude), RopeThickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    /// dust 0..1 = örtme miktarı; working kapanınca karakter sönerek çıkar (gondolda kalır, onunla döner).
    public void TickWork(float t, float dust, bool working)
    {
        for (int i = 0; i < DustCount; i++)
        {
            var img = _dust[i];
            if (img == null) continue;
            bool on = dust > 0.001f;
            if (img.gameObject.activeSelf != on) img.gameObject.SetActive(on);
            if (!on) continue;
            float seed = _dustSeed[i];
            // İncelirken söndüğü gibi hafifçe büyür ve yükselir → dağılan perde hissi.
            float s = _dustSize[i] * (0.75f + 0.25f * Mathf.Clamp01(t * 2f) + 0.15f * (1f - dust) + 0.06f * Mathf.Sin(t * 2.1f + seed));
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(s, s);
            rt.anchoredPosition = _dustHome[i] + new Vector2(Mathf.Sin(t * 1.3f + seed) * 14f, t * 16f);
            rt.localRotation = Quaternion.Euler(0f, 0f, seed * 3.6f + t * 25f);
            var c = DustColor;
            c.a = 0.97f * dust;
            img.color = c;
        }
        if (_hammerer == null || !_hammerer.gameObject.activeSelf) return;

        if (!working && _workEndedAt < 0f) _workEndedAt = t;
        if (_crane && !working)
        {
            // Gondolda: vuruş biter, ayı ayakta bekler; gondolla birlikte yukarı çıkar.
            _hammerer.sprite = _hammerFrames[0];
            _hammerer.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            return;
        }
        float pop = _crane ? 1f : Mathf.Clamp01(t / PopTime);   // gondolla gelen karakter zaten görünür
        float fade = _workEndedAt < 0f ? 1f : 1f - Mathf.Clamp01((t - _workEndedAt) / 0.25f);
        if (fade <= 0f)
        {
            _hammerer.gameObject.SetActive(false);
            if (_tool != null) _tool.gameObject.SetActive(false);
            return;
        }
        float scale = Smooth01(pop) * Mathf.Lerp(0.85f, 1f, fade);

        int swing = Mathf.FloorToInt(t / SwingPeriod);
        float phase = t / SwingPeriod - swing;
        int n = _hammerFrames.Length;
        int frame = Mathf.Min(n - 1, Mathf.FloorToInt(phase * n));
        float hitPhase = EvenHit;
        if (n == FourFrameEnds.Length)
        {
            frame = 0;
            while (frame < n - 1 && phase >= FourFrameEnds[frame]) frame++;
            hitPhase = FourFrameHit;
        }
        _hammerer.sprite = _hammerFrames[frame];
        // Sola bakar (kare sağa bakıyor); vuruşta hafif basılma.
        float squash = phase > hitPhase && phase < hitPhase + 0.1f ? 0.95f : 1f;
        var hrt = _hammerer.rectTransform;
        hrt.localScale = new Vector3(-scale, scale * squash, 1f);
        var hc = _hammerer.color; hc.a = fade; _hammerer.color = hc;

        if (_tool != null)
        {
            float angle = phase < 0.6f ? Mathf.Lerp(10f, -35f, phase / 0.6f)
                : phase < hitPhase ? Mathf.Lerp(-35f, 80f, (phase - 0.6f) / Mathf.Max(0.01f, hitPhase - 0.6f))
                : Mathf.Lerp(80f, 10f, (phase - hitPhase) / (1f - hitPhase));
            var trt = _tool.rectTransform;
            trt.anchoredPosition = _hammererPos + new Vector2(-0.33f * _hammererH, 0.28f * _hammererH) * scale;
            trt.localRotation = Quaternion.Euler(0f, 0f, angle);
            trt.localScale = new Vector3(scale, scale, 1f);
            var tc = _tool.color; tc.a = fade; _tool.color = tc;
        }

        if (working && phase >= hitPhase && swing != _lastHit)
        {
            _lastHit = swing;
            PlayHit();
        }
    }

    public void BeginStars(Vector2 origin)
    {
        _starOrigin = origin;
        for (int i = 0; i < StarCount; i++)
        {
            if (_stars[i] == null) continue;
            float a = (i * 36f + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
            _starVel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(380f, 620f);
            _starSize[i] = Random.Range(46f, 74f);
            _stars[i].gameObject.SetActive(true);
        }
        TickStars(0f);
    }

    public void TickStars(float t)
    {
        bool alive = t < StarLife;
        float p = Mathf.Clamp01(t / StarLife);
        float scale = t < 0.1f ? t / 0.1f : Mathf.Lerp(1f, 0.35f, p);
        float alpha = p < 0.6f ? 1f : 1f - (p - 0.6f) / 0.4f;
        for (int i = 0; i < StarCount; i++)
        {
            var img = _stars[i];
            if (img == null) continue;
            if (!alive) { if (img.gameObject.activeSelf) img.gameObject.SetActive(false); continue; }
            var rt = img.rectTransform;
            float s = _starSize[i] * scale;
            rt.sizeDelta = new Vector2(s, s);
            rt.anchoredPosition = _starOrigin + _starVel[i] * t + new Vector2(0f, -300f * t * t);
            rt.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * 300f * t);
            var c = img.color; c.a = alpha; img.color = c;
        }
    }

    public void Clear()
    {
        foreach (var d in _dust) if (d != null) d.gameObject.SetActive(false);
        foreach (var s in _stars) if (s != null) s.gameObject.SetActive(false);
        if (_hammerer != null) _hammerer.gameObject.SetActive(false);
        if (_tool != null) _tool.gameObject.SetActive(false);
        if (_beam != null) _beam.gameObject.SetActive(false);
        if (_trolley != null) _trolley.gameObject.SetActive(false);
        if (_gondola != null) _gondola.gameObject.SetActive(false);
        foreach (var r in _ropes) if (r != null) r.gameObject.SetActive(false);
        _crane = false;
        CraneOffset = Vector2.zero;
        _lastHit = -1;
        _workEndedAt = -1f;
    }

    static void PlayHit()
    {
        if (!s_hitLoaded) { s_hitClip = Resources.Load<AudioClip>("Audio/Jokers/HammerHit"); s_hitLoaded = true; }
        GameEventSfx.PlayUiClip(s_hitClip);   // merkezi kaynak; ses kapalıysa susar
    }

    static float Smooth01(float x) => x * x * (3f - 2f * x);
}
