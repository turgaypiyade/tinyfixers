using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Şehir gezisi (kullanıcı 2026-11-18): dünya haritasında rozete dokununca rozetten büyüyerek şehir panoraması açılır;
/// bizim harika ortada; bisikletli ayı meydanda sağa-sola gider, kamera onu izler (oyuncu kaydırmayı öğrenmeden şehri gezer).
/// Panorama yatay kaydırılır. Görseller: Resources/WorldTour/City_{wonderId} (onarılmış) ve
/// City_{wonderId}_Site (iskeleli, tamamlanmamış harika). Görsel yoksa harita eski büyük görünümü açar.
/// Bisikletli: Resources/WorldTour/Rider/Ride_0..2 (sağa pedal) + Turn_0..2 (arkadan dönüş). Yalnız ekran açıkken tek Update.
/// </summary>
public sealed class CityTourView : MonoBehaviour
{
    private const string ArtPath = "WorldTour/City_";
    // Bisikletli ayı (panorama yüksekliğine oranlar; hız ekran genişliği/sn).
    private const float RiderGroundY = 0.28f, RideSpeed = 0.26f;   // meydanın ortası

    // Perspektif: ayak hizası ufka (HorizonY, alttan oran) yaklaştıkça karakter küçülür.
    // Ayakta bir karakterin boyu = DepthK × (HorizonY − y) (panorama yüksekliğine oran). Meydan arkası (y≈0.36) ≈ 0.06.
    private const float HorizonY = 0.45f, DepthK = 0.667f;
    // Şehre özel perspektif (x = HorizonY, y = DepthK); resimde meydanın derinliği farklıysa.
    private static readonly Dictionary<string, Vector2> CityDepth = new Dictionary<string, Vector2>
    {
        { "mis9", new Vector2(0.48f, 0.56f) },   // Big Ben: meydan arkası alttan ~%40
        { "mis2", new Vector2(0.455f, 0.780f) },  // Golden Gate: meydan arkası alttan ~%36
        { "mis7", new Vector2(0.506f, 0.500f) },  // Ayasofya: meydan arkası alttan ~%42
        { "mis10", new Vector2(0.437f, 0.778f) },
        { "mis5", new Vector2(0.484f, 0.500f) },
        { "mis3", new Vector2(0.407f, 0.611f) },
        { "mis4", new Vector2(0.447f, 0.611f) },
        { "mis6", new Vector2(0.490f, 0.556f) },
        { "mis8", new Vector2(0.477f, 0.500f) },
    };
    private float _horizonY = HorizonY, _depthK = DepthK;
    private float StandHeight(float groundY) => _depthK * Mathf.Max(0.02f, _horizonY - groundY);
    private const float PedalFrameSec = 0.11f, TurnFrameSec = 0.16f, StartWait = 1.2f;
    private const float EdgeMargin = 0.07f;      // panorama kenarına bu kadar kala döner
    private const float UserHoldSec = 2.5f;      // oyuncu kaydırınca kamera bu kadar bekler, sonra ayıya döner
    private const float RideMin = 2.5f, RideMax = 5f, IdleMin = 1.5f, IdleMax = 2.8f;   // arada durup bekler

    // Bilgi balonu (GameSettings.CityFactsEnabled): ayı mola verince başının üstünde sıradaki bilgi; okunsun diye mola uzar.
    private const float FactIdle = 4.8f, BubbleWidth = 660f, BubbleHeight = 210f, BubblePop = 0.18f;
    private const float BubbleBorderScale = 1.6f;   // 9-slice çerçeve inceltme (büyüdükçe ince)

    // Ortam uğultusu (hayvan sohbeti + kafe): merkezi EventSfx loop kaynağı, ses ayarına uyar.
    private const string AmbiencePath = "WorldTour/CityAmbience";
    private const float AmbienceVolume = 0.55f;
    private AudioClip _ambience;
    private const float TallArtAspect = 1.8f;  // bundan dar resim = dikey ekran için çizilmiş (ör. İstanbul 2580x1910)
    private const float ViewAspect = 0.97f;   // ekranda görünen pencere: genişlik/yükseklik (resmin tam yüksekliği; harika + hafif kenar)

    // Açılışta ortalanacak yatay nokta (resim genişliğine oran) — harika tam ortada değilse.
    private static readonly Dictionary<string, float> FocusX = new Dictionary<string, float>
    {
    };

    private static CityTourView _instance;
    public static bool IsOpen => _instance != null;

    public static bool HasArt(WonderDefinition w)
        => w != null && (Resources.Load<Sprite>(ArtPath + w.wonderId) != null
                         || Resources.Load<Sprite>(ArtPath + w.wonderId + "_Site") != null);

    // Yerinde duran karakter (masa/tezgâh görselin içinde): 2 kare, arada bir hareket karesine geçer.
    // Konum panoramaya oranla (x soldan, y alttan = ayak hizası), yükseklik panorama yüksekliğine oran.
    private readonly struct NpcSpec
    {
        public readonly string Prefix; public readonly float X, Y, Size;   // Size: görselin, ayaktaki karaktere göre boy çarpanı (tezgâh/şemsiye dahil)
        public NpcSpec(string prefix, float x, float y, float size) { Prefix = prefix; X = x; Y = y; Size = size; }
    }

    // Bisiklet şeridinin (alttan %22-34) ARKASINDA durur. Şehre özel liste yoksa varsayılan.
    private static readonly NpcSpec[] DefaultNpcs =
    {
        new NpcSpec("Bunny_", 0.31f, 0.345f, 1.05f),  // Pemi: kafe masalarının önünde çay
        new NpcSpec("Ram_", 0.465f, 0.35f, 1.0f),     // Bram: haritaya bakar, harikayı (sağı) gösterir
        new NpcSpec("Monkey_", 0.60f, 0.35f, 1.3f),   // Fiko: tezgâhında el sallar (gölden uzak)
    };
    private static readonly Dictionary<string, NpcSpec[]> CityNpcs = new Dictionary<string, NpcSpec[]>
    {
        // Golden Gate: food truck + piknik masaları x .02-.33, dondurma .72-.81, teleferik .82-.97
        { "mis2", new[]
            {
                new NpcSpec("Bunny_", 0.36f, 0.36f, 1.05f),
                new NpcSpec("Ram_", 0.44f, 0.36f, 1.0f),
                new NpcSpec("Monkey_", 0.65f, 0.36f, 1.3f),
            }
        },
        // Ayasofya: simit arabası + masalar x .13-.26, sağ köşk .86; bina .33-.67
        { "mis7", new[]
            {
                new NpcSpec("Bunny_", 0.28f, 0.40f, 1.05f),
                new NpcSpec("Ram_", 0.36f, 0.41f, 1.0f),
                new NpcSpec("Monkey_", 0.70f, 0.41f, 1.3f),
            }
        },
        // Big Ben: kafe masaları x .32-.41, telefon kulübesi .23-.26, çiçek tezgâhı .61-.67; meydan arkası alttan ~.395
        { "mis9", new[]
            {
                new NpcSpec("Bunny_", 0.295f, 0.39f, 1.05f),   // telefon kulübesiyle kafe masaları arasında
                new NpcSpec("Ram_", 0.445f, 0.395f, 1.0f),     // kulenin solunda, kuleyi gösterir
                new NpcSpec("Monkey_", 0.575f, 0.39f, 1.3f),   // çiçek tezgâhının solunda
            }
        },
        { "mis10", new[]
            {
                new NpcSpec("Bunny_", 0.34f, 0.335f, 1.05f),
                new NpcSpec("Ram_", 0.44f, 0.34f, 1.0f),
                new NpcSpec("Monkey_", 0.72f, 0.335f, 1.3f),
            }
        },
        { "mis5", new[]
            {
                new NpcSpec("Bunny_", 0.3f, 0.39f, 1.05f),
                new NpcSpec("Ram_", 0.41f, 0.39f, 1.0f),
                new NpcSpec("Monkey_", 0.62f, 0.39f, 1.3f),
            }
        },
        { "mis3", new[]
            {
                new NpcSpec("Bunny_", 0.31f, 0.31f, 1.05f),
                new NpcSpec("Ram_", 0.42f, 0.315f, 1.0f),
                new NpcSpec("Monkey_", 0.59f, 0.315f, 1.3f),
            }
        },
        { "mis4", new[]
            {
                new NpcSpec("Bunny_", 0.36f, 0.35f, 1.05f),
                new NpcSpec("Ram_", 0.41f, 0.355f, 1.0f),
                new NpcSpec("Monkey_", 0.575f, 0.35f, 1.3f),
            }
        },
        { "mis6", new[]
            {
                new NpcSpec("Bunny_", 0.36f, 0.4f, 1.05f),
                new NpcSpec("Ram_", 0.46f, 0.4f, 1.0f),
                new NpcSpec("Monkey_", 0.6f, 0.4f, 1.3f),
            }
        },
        { "mis8", new[]
            {
                new NpcSpec("Bunny_", 0.37f, 0.385f, 1.05f),
                new NpcSpec("Ram_", 0.47f, 0.385f, 1.0f),
                new NpcSpec("Monkey_", 0.65f, 0.385f, 1.3f),
            }
        },
    };

    private sealed class Npc
    {
        public Image Img;
        public Sprite Idle, Act;
        public float Timer;
        public bool Acting;
    }

    private readonly List<Npc> _npcs = new List<Npc>();
    private const float NpcIdleMin = 2.5f, NpcIdleMax = 5.5f, NpcActMin = 1.1f, NpcActMax = 1.6f;

    private enum RiderState { Wait, Ride, Turn }

    // Oyuncunun sürüklemesini yakalar (ScrollRect ile aynı objede; olay ikisine de gider).
    private sealed class DragProbe : MonoBehaviour, UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IEndDragHandler
    {
        public bool Dragging;
        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData e) => Dragging = true;
        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData e) => Dragging = false;
    }

    private ScrollRect _scroll;
    private DragProbe _drag;
    private RectTransform _rider;
    private Image _riderImg;
    private Sprite[] _rideFrames, _turnFrames;
    private RiderState _state;
    private float _riderX, _stateT, _frameT, _holdT, _screenW, _bobT, _rideLeft;
    private int _dir = 1, _frame;
    private RectTransform _bubble;
    private TMP_Text _bubbleText;
    private readonly List<string> _facts = new List<string>();
    private int _factIndex;
    private float _bubbleT;          // >0: açılış animasyonu sürüyor
    private bool _bubbleShown;
    private RectTransform _pano;
    private RectTransform _stage;
    private CanvasGroup _group;
    private bool _closing;
    private float _panoW, _panoH;

    public static void Show(WonderCatalog catalog, int index, RectTransform fromBadge)
    {
        var w = catalog != null ? catalog.Get(index) : null;
        if (w == null || _instance != null || !HasArt(w)) return;
        bool done = WonderProgress.IsEventComplete(catalog, index);
        // Tamamlanmış → onarılmış hâl; değilse iskeleli. Biri eksikse diğeri kullanılır.
        Sprite repaired = Resources.Load<Sprite>(ArtPath + w.wonderId);
        Sprite site = Resources.Load<Sprite>(ArtPath + w.wonderId + "_Site");
        Sprite art = done ? (repaired != null ? repaired : site) : (site != null ? site : repaired);
        var go = new GameObject("CityTourView", typeof(RectTransform));
        _instance = go.AddComponent<CityTourView>();
        _instance.Build(catalog, index, w, art, fromBadge);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        EventSfx.FadeOutAmbientClip(_ambience, 0.15f);   // sahne kapanışı vb.: döngü askıda kalmasın
    }

    private void Build(WonderCatalog catalog, int index, WonderDefinition w, Sprite art, RectTransform fromBadge)
    {
        gameObject.layer = 5;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 860;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        _group = gameObject.AddComponent<CanvasGroup>();
        // Kök Canvas'ın RectTransform'unu Canvas yönetir → yakınlaşma iç sahnede (_stage) oynar.
        var canvasRt = (RectTransform)transform;
        _stage = NewRect("Stage", canvasRt);
        Stretch(_stage);
        var root = _stage;

        var bg = NewRect("Backdrop", root);
        Stretch(bg);
        bg.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);   // panorama dışı: harita koyulaşır

        // Kaydırılan panorama: ekran yüksekliğini doldurur.
        var scrollRt = NewRect("Scroll", root);
        Stretch(scrollRt);
        var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        _scroll = scroll;
        _drag = scrollRt.gameObject.AddComponent<DragProbe>();
        scroll.horizontal = true; scroll.vertical = false;
        var viewport = NewRect("Viewport", scrollRt);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        scroll.viewport = viewport;

        Canvas.ForceUpdateCanvases();
        float screenW = canvasRt.rect.width > 10f ? canvasRt.rect.width : 1080f;
        float screenH = canvasRt.rect.height > 10f ? canvasRt.rect.height : 1920f;
        // Resmin TAM yüksekliği görünür, ekran genişliği resmin ~yükseklik kadarlık dilimini gösterir (harika + hafif kenar);
        // pencere dikeyde ortalanır, kalan yanlar yatay kaydırmayla gezilir. Gökyüzü uzatılmaz.
        // Dikey için çizilmiş resim (en/boy < TallArtAspect): ekran boyunu doldurur. Eski geniş panoramalar: pencere modu.
        float artAspect = art.rect.width / Mathf.Max(1f, art.rect.height);
        _panoH = artAspect < TallArtAspect ? screenH : screenW / ViewAspect;
        _panoW = Mathf.Max(screenW, _panoH * art.rect.width / Mathf.Max(1f, art.rect.height));
        _pano = NewRect("Panorama", viewport);
        _pano.anchorMin = _pano.anchorMax = new Vector2(0f, 0.5f);
        _pano.pivot = new Vector2(0f, 0.5f);
        _pano.sizeDelta = new Vector2(_panoW, _panoH);
        float focus = FocusX.TryGetValue(w.wonderId, out var f) ? f : 0.5f;
        _pano.anchoredPosition = new Vector2(-Mathf.Clamp(_panoW * focus - screenW * 0.5f, 0f, _panoW - screenW), 0f);
        var panoImg = _pano.gameObject.AddComponent<Image>();
        panoImg.sprite = art;
        scroll.content = _pano;
        _screenW = screenW;
        if (CityDepth.TryGetValue(w.wonderId, out var depth)) { _horizonY = depth.x; _depthK = depth.y; }
        SpawnNpcs(w.wonderId);   // ayıdan önce → ayının arkasında çizilir
        SpawnRider(focus);
        BuildBubble(w.wonderId);
        _ambience = Resources.Load<AudioClip>(AmbiencePath);
        EventSfx.StartAmbientClip(_ambience, AmbienceVolume);

        // Üst: geri (haritaya döner) + şehir/harika adı.
        var back = NewRect("Back", root);
        back.anchorMin = back.anchorMax = new Vector2(0f, 1f);
        back.anchoredPosition = new Vector2(90f, -150f);
        back.sizeDelta = new Vector2(110f, 110f);
        var backImg = back.gameObject.AddComponent<Image>();
        backImg.sprite = UiIcons.Close;
        backImg.preserveAspect = true;
        back.gameObject.AddComponent<Button>().onClick.AddListener(Close);

        var title = NewText(root, w.LocalizedName, new Vector2(0f, -150f), new Vector2(760f, 130f), 80f);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        TextSurfaceStyle.Apply(title, new Color(0.72f, 0.30f, 0.05f));
        StartCoroutine(TitleLetterPop.Play(title));

        if (!WonderProgress.IsEventComplete(catalog, index)) BuildRepairButton(root);
        StartCoroutine(ZoomIn(fromBadge));
    }

    // Tamir edilmemiş şehirde alttaki "Tamir Et!": şehir + harita kapanır, Görevler paneli açılır
    // (görev yapmayan oyuncuyu yıkık şehirden göreve taşır; kullanıcı 2026-11-19).
    private void BuildRepairButton(RectTransform root)
    {
        var rt = NewRect("RepairCta", root);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 230f);
        rt.sizeDelta = new Vector2(520f, 150f);
        var img = rt.gameObject.AddComponent<Image>();
        img.type = Image.Type.Sliced;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        var label = NewText(rt, GameLocalization.GetOr("worldtour_repair_cta", "Tamir Et!"), Vector2.zero, new Vector2(440f, 110f), 60f);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        UiButtons.Apply(button, UiButtons.Kind.Green);
        button.onClick.AddListener(OpenMissions);
    }

    private void OpenMissions()
    {
        if (_closing) return;
        _closing = true;
        EventSfx.FadeOutAmbientClip(_ambience, 0.15f);
        var tutorial = FindAnyObjectByType<WorkshopRepairButtonTutorial>(FindObjectsInactive.Include);
        var repair = tutorial != null ? tutorial.RepairButton : null;
        WorldTourMap.CloseFullScreen();
        Destroy(gameObject);
        if (repair != null && repair.isActiveAndEnabled) repair.onClick.Invoke();
    }

    // Rozetten büyüyerek açılır (harita → şehir "dalış").
    private IEnumerator ZoomIn(RectTransform fromBadge)
    {
        var root = _stage;
        Vector2 from = Vector2.zero;
        if (fromBadge != null)
        {
            Vector3 world = fromBadge.TransformPoint(fromBadge.rect.center);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                RectTransformUtility.WorldToScreenPoint(null, world), null, out from);
        }
        const float dur = 0.5f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / dur, 3f);
            root.localScale = Vector3.one * Mathf.Lerp(0.15f, 1f, k);
            root.anchoredPosition = Vector2.Lerp(from, Vector2.zero, k);
            _group.alpha = Mathf.Clamp01(k * 2f);
            yield return null;
        }
        root.localScale = Vector3.one;
        root.anchoredPosition = Vector2.zero;
        _group.alpha = 1f;
    }

    private void Close()
    {
        if (_closing) return;
        _closing = true;
        EventSfx.FadeOutAmbientClip(_ambience, 0.35f);
        StartCoroutine(ZoomOut());
    }

    private IEnumerator ZoomOut()
    {
        var root = _stage;
        const float dur = 0.3f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur;
            root.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
            _group.alpha = 1f - k;
            yield return null;
        }
        Destroy(gameObject);
    }

    // ── Yerinde duran karakterler ─────────────────────────────────────

    private void SpawnNpcs(string wonderId)
    {
        var specs = CityNpcs.TryGetValue(wonderId, out var list) ? list : DefaultNpcs;
        foreach (var spec in specs)
        {
            var idle = Resources.Load<Sprite>("WorldTour/Npc/" + spec.Prefix + "0");
            var act = Resources.Load<Sprite>("WorldTour/Npc/" + spec.Prefix + "1");
            if (idle == null || act == null) continue;
            var rt = NewRect("Npc_" + spec.Prefix, _pano);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            float h = _panoH * StandHeight(spec.Y) * spec.Size;
            rt.sizeDelta = new Vector2(h * idle.rect.width / Mathf.Max(1f, idle.rect.height), h);
            rt.anchoredPosition = new Vector2(_panoW * spec.X, _panoH * spec.Y);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = idle;
            img.preserveAspect = true;
            img.raycastTarget = false;
            _npcs.Add(new Npc { Img = img, Idle = idle, Act = act, Timer = Random.Range(0.8f, NpcIdleMax) });
        }
    }

    private void TickNpcs(float dt)
    {
        for (int i = 0; i < _npcs.Count; i++)
        {
            var n = _npcs[i];
            n.Timer -= dt;
            if (n.Timer > 0f) continue;
            n.Acting = !n.Acting;
            n.Img.sprite = n.Acting ? n.Act : n.Idle;
            n.Timer = n.Acting ? Random.Range(NpcActMin, NpcActMax) : Random.Range(NpcIdleMin, NpcIdleMax);
        }
    }

    // ── Bilgi balonu ─────────────────────────────────────────────────

    private void BuildBubble(string wonderId)
    {
        if (_rider == null) return;
        for (int i = 0; i < 20; i++)
        {
            string fact = GameLocalization.GetOr("worldtour_fact_" + wonderId + "_" + i, null);
            if (string.IsNullOrEmpty(fact)) break;
            _facts.Add(fact);
        }
        var body = Resources.Load<Sprite>("WorldTour/BubbleBody");
        var tail = Resources.Load<Sprite>("WorldTour/BubbleTail");
        if (_facts.Count == 0 || body == null || tail == null) return;
        _factIndex = Random.Range(0, _facts.Count);

        _bubble = NewRect("FactBubble", _pano);
        _bubble.anchorMin = _bubble.anchorMax = new Vector2(0f, 0f);
        _bubble.pivot = new Vector2(0.08f, 0f);   // kuyruk ucu tarafı
        _bubble.sizeDelta = new Vector2(BubbleWidth, BubbleHeight);
        var bodyImg = _bubble.gameObject.AddComponent<Image>();
        bodyImg.sprite = body;
        bodyImg.type = Image.Type.Sliced;
        bodyImg.pixelsPerUnitMultiplier = BubbleBorderScale;
        bodyImg.raycastTarget = false;

        // Kuyruk: gövdenin sol-altına, üst kısmı gövdenin alt çizgisini örter (görselden ölçülen oranlar).
        float k = 1f / BubbleBorderScale;
        var tailRt = NewRect("Tail", _bubble);
        tailRt.anchorMin = tailRt.anchorMax = new Vector2(0f, 0f);
        tailRt.pivot = new Vector2(0f, 1f);
        tailRt.sizeDelta = new Vector2(tail.rect.width, tail.rect.height) * k;
        tailRt.anchoredPosition = new Vector2((16f + 43.5f) * k, (24f + 40.5f) * k);   // görselde gölge payı: sol 16, alt 24 px
        var tailImg = tailRt.gameObject.AddComponent<Image>();
        tailImg.sprite = tail;
        tailImg.raycastTarget = false;

        _bubbleText = NewText(_bubble, "", new Vector2(0f, 7f), new Vector2(BubbleWidth - 90f, BubbleHeight - 74f), 46f);
        _bubbleText.rectTransform.anchorMin = _bubbleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _bubbleText.textWrappingMode = TextWrappingModes.Normal;
        // Açık (krem) balonda koyu yazı okunur; beyaz yüz + kontur burada kontrastsız kaldı (kullanıcı 2026-11-19).
        _bubbleText.color = new Color(0.55f, 0.27f, 0.15f);   // kiremit-kahve (kullanıcı referansı)
        var font = ResolveBubbleFont();
        if (font != null) _bubbleText.font = font;
        _bubble.gameObject.SetActive(false);
        BuildFactsToggle();
    }

    // Sağ üst "i": bilgileri açar/kapatır (GameSettings.CityFactsEnabled, kalıcı). Kapalıyken soluk.
    private Image _factsIcon;

    private void BuildFactsToggle()
    {
        var rt = NewRect("FactsToggle", _stage);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-90f, -150f);
        rt.sizeDelta = new Vector2(110f, 110f);
        _factsIcon = rt.gameObject.AddComponent<Image>();
        _factsIcon.sprite = UiIcons.Info;
        _factsIcon.preserveAspect = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            GameSettings.CityFactsEnabled = !GameSettings.CityFactsEnabled;
            if (!GameSettings.CityFactsEnabled) HideFact();
            RefreshFactsIcon();
        });
        RefreshFactsIcon();
    }

    private void RefreshFactsIcon()
    {
        if (_factsIcon != null) _factsIcon.color = GameSettings.CityFactsEnabled ? Color.white : new Color(1f, 1f, 1f, 0.4f);
    }

    // Yuvarlak kalın okuma fontu: Nunito Black, yoksa Inter ExtraBold/Bold (yüklü olanlar arasından).
    private static TMP_FontAsset ResolveBubbleFont()
    {
        TMP_FontAsset nunito = null, interXB = null, interB = null;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f == null) continue;
            if (f.name == "Nunito-Black SDF") nunito = f;
            else if (f.name == "Inter_28pt-ExtraBold SDF") interXB = f;
            else if (f.name == "Inter_28pt-Bold SDF") interB = f;
        }
        return nunito != null ? nunito : (interXB != null ? interXB : interB);
    }

    private bool FactsOn => _bubble != null && GameSettings.CityFactsEnabled;

    private void ShowFact()
    {
        _bubbleText.text = _facts[_factIndex];
        _factIndex = (_factIndex + 1) % _facts.Count;
        _bubble.gameObject.SetActive(true);
        _bubble.localScale = Vector3.one * 0.6f;
        _bubbleT = BubblePop;
        _bubbleShown = true;
        PlaceBubble();
    }

    private void HideFact()
    {
        if (!_bubbleShown) return;
        _bubbleShown = false;
        _bubble.gameObject.SetActive(false);
    }

    // Kuyruk ayının başını gösterir; balon ekrandan taşmasın diye görünen alana sıkıştırılır.
    private void PlaceBubble()
    {
        float headY = _rider.anchoredPosition.y + _rider.sizeDelta.y * 0.95f;
        float viewLeft = -_pano.anchoredPosition.x;
        float leftEdge = Mathf.Clamp(_riderX - BubbleWidth * 0.08f, viewLeft + 24f, viewLeft + _screenW - BubbleWidth - 24f);
        _bubble.anchoredPosition = new Vector2(leftEdge + BubbleWidth * 0.08f, headY + 30f);
    }

    private void TickBubble(float dt)
    {
        if (!_bubbleShown) return;
        if (_bubbleT > 0f)
        {
            _bubbleT = Mathf.Max(0f, _bubbleT - dt);
            float k = 1f - _bubbleT / BubblePop;
            _bubble.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, 1f + 0.12f * Mathf.Sin(k * Mathf.PI) - (1f - k));
        }
        PlaceBubble();
    }

    // ── Bisikletli ayı ───────────────────────────────────────────────

    private static Sprite[] LoadFrames(string prefix, int count)
    {
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            frames[i] = Resources.Load<Sprite>("WorldTour/Rider/" + prefix + i);
            if (frames[i] == null) return null;
        }
        return frames;
    }

    private void SpawnRider(float startXNorm)
    {
        _rideFrames = LoadFrames("Ride_", 3);
        _turnFrames = LoadFrames("Turn_", 3);
        if (_rideFrames == null || _turnFrames == null) return;
        _rider = NewRect("Rider", _pano);
        _rider.anchorMin = _rider.anchorMax = new Vector2(0f, 0f);
        _rider.pivot = new Vector2(0.5f, 0f);   // tekerlek altı
        float h = _panoH * StandHeight(RiderGroundY);
        var s0 = _rideFrames[0];
        _rider.sizeDelta = new Vector2(h * s0.rect.width / Mathf.Max(1f, s0.rect.height), h);
        _riderImg = _rider.gameObject.AddComponent<Image>();
        _riderImg.sprite = s0;
        _riderImg.preserveAspect = true;
        _riderImg.raycastTarget = false;
        _riderX = Mathf.Clamp(_panoW * startXNorm, _panoW * EdgeMargin, _panoW * (1f - EdgeMargin));
        _state = RiderState.Wait;
        _stateT = StartWait;
        PlaceRider();
    }

    private void PlaceRider()
    {
        float bob = _state == RiderState.Ride ? Mathf.Abs(Mathf.Sin(_bobT * 9f)) * _panoH * 0.003f : 0f;
        _rider.anchoredPosition = new Vector2(_riderX, _panoH * RiderGroundY + bob);
        // Sürüş kareleri sağa bakar → sola giderken ayna; dönüş kareleri kendi yönünde çizili.
        _rider.localScale = new Vector3(_state == RiderState.Turn || _dir > 0 ? 1f : -1f, 1f, 1f);
    }

    private void Update()
    {
        if (_closing) return;
        float dt = Time.unscaledDeltaTime;
        TickNpcs(dt);
        if (_rider == null) return;
        switch (_state)
        {
            case RiderState.Wait:
                _stateT -= dt;
                if (_stateT <= 0f) { HideFact(); _state = RiderState.Ride; _rideLeft = Random.Range(RideMin, RideMax); }
                break;
            case RiderState.Ride:
                _bobT += dt;
                _rideLeft -= dt;
                if (_rideLeft <= 0f)
                {
                    // Kısa mola: pedal durur, ayı olduğu yerde bekler.
                    _state = RiderState.Wait;
                    _stateT = FactsOn ? FactIdle : Random.Range(IdleMin, IdleMax);
                    if (FactsOn) ShowFact();
                    _frame = 0;
                    _riderImg.sprite = _rideFrames[0];
                    break;
                }
                _riderX += _dir * RideSpeed * _screenW * dt;
                _frameT += dt;
                if (_frameT >= PedalFrameSec) { _frameT = 0f; _frame = (_frame + 1) % _rideFrames.Length; _riderImg.sprite = _rideFrames[_frame]; }
                float lo = _panoW * EdgeMargin, hi = _panoW * (1f - EdgeMargin);
                if ((_dir > 0 && _riderX >= hi) || (_dir < 0 && _riderX <= lo))
                {
                    _riderX = Mathf.Clamp(_riderX, lo, hi);
                    _state = RiderState.Turn;
                    _frame = 0;
                    _frameT = 0f;
                    _riderImg.sprite = _turnFrames[_dir > 0 ? 0 : _turnFrames.Length - 1];
                }
                break;
            case RiderState.Turn:
                // Sağa giderken: sağ-arka → arka → sol-arka; sola giderken tersi.
                _frameT += dt;
                if (_frameT >= TurnFrameSec)
                {
                    _frameT = 0f;
                    _frame++;
                    if (_frame >= _turnFrames.Length)
                    {
                        _dir = -_dir;
                        _frame = 0;
                        _riderImg.sprite = _rideFrames[0];
                        _state = RiderState.Ride;
                        _rideLeft = Random.Range(RideMin, RideMax);
                    }
                    else _riderImg.sprite = _turnFrames[_dir > 0 ? _frame : _turnFrames.Length - 1 - _frame];
                }
                break;
        }
        PlaceRider();
        FollowRider(dt);
        TickBubble(dt);
    }

    // Kamera ayıyı ortada tutar; oyuncu kaydırıyorsa bırakır, kısa süre sonra yumuşakça geri döner.
    private void FollowRider(float dt)
    {
        if (_drag != null && _drag.Dragging) { _holdT = UserHoldSec; return; }
        if (_holdT > 0f) { _holdT -= dt; if (_holdT <= 0f) _scroll.StopMovement(); return; }
        float target = -Mathf.Clamp(_riderX - _screenW * 0.5f, 0f, Mathf.Max(0f, _panoW - _screenW));
        var p = _pano.anchoredPosition;
        p.x = Mathf.Lerp(p.x, target, 1f - Mathf.Exp(-4f * dt));
        _pano.anchoredPosition = p;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────

    private RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private TMP_Text NewText(Transform parent, string text, Vector2 pos, Vector2 size, float fontSize)
    {
        var rt = NewRect("Text", parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = fontSize;
        tmp.fontSizeMin = fontSize * 0.5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }
}
