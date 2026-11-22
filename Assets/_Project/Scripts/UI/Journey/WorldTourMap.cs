using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yolculuk sekmesinin dünya haritası (kullanıcı 2026-11-18: "tamamladığımız bölgeleri gezdirelim").
/// Yatay kaydırılan tek harita; her harika kendi ülkesinde rozet: tamamlanan = renkli resim + altın çerçeve,
/// şu anki = adı + "Şimdi burada", açılmamış = bulut/kilit. Harikalar oyuncu sırasıyla kesikli rotayla bağlanır;
/// yeni harikaya geçilmişse ekibin uçağı rotada bir kez uçar. Rozete dokununca harika büyük açılır (bitmişse
/// "Arka plan yap"). Görseller Resources/WorldTour/ altından okunur; WorldMap yoksa Yolculuk eski listeyi gösterir.
/// Sürekli animasyon yok; kurulum sekme açılışında bir kez.
/// </summary>
public sealed class WorldTourMap : MonoBehaviour
{
    private const string ArtPath = "WorldTour/";
    private const string SeenKey = "worldtour_seen_active";
    private const float PinWidth = 120f;
    private const float PinAspect = 906f / 716f;   // Pin_Locked/Pin_Open görsel oranı
    private const float DotSpacing = 34f;

    // Normalize harita konumları (sol-alt 0,0) — Resources/WorldTour/WorldMap.png'ye göre (2026-11-22 yeni stilize
    // harita: Avrupa büyük çizilmiş; konumlar görselden okundu, eskisi Docs/Degisenler/2026-11-22/WorldTour/).
    private static readonly Dictionary<string, Vector2> Spots = new Dictionary<string, Vector2>
    {
        { "mis9",  new Vector2(0.392f, 0.808f) },   // Big Ben — Londra
        { "mis3",  new Vector2(0.312f, 0.745f) },   // Eyfel — Paris
        { "mis1",  new Vector2(0.428f, 0.628f) },   // Pisa
        { "mis8",  new Vector2(0.585f, 0.478f) },   // Rodos
        { "mis7",  new Vector2(0.592f, 0.655f) },   // Ayasofya — İstanbul
        { "mis4",  new Vector2(0.553f, 0.405f) },   // Piramit — Nil deltası
        { "mis5",  new Vector2(0.845f, 0.715f) },   // Çin Seddi
        { "mis2",  new Vector2(0.035f, 0.680f) },   // Golden Gate
        { "mis6",  new Vector2(0.078f, 0.655f) },   // Büyük Kanyon
        { "mis10", new Vector2(0.905f, 0.190f) },   // Sidney
    };

    public static bool HasArt => Resources.Load<Sprite>(ArtPath + "WorldMap") != null;

    private WonderCatalog _catalog;
    private RectTransform _content;
    private ScrollRect _scroll;
    private readonly List<RectTransform> _badges = new List<RectTransform>();
    private Sprite _pinLocked, _pinOpen, _plane, _circle;

    public static WorldTourMap Build(RectTransform panel, WonderCatalog catalog, float topInset, float bottomInset)
    {
        var go = new GameObject("WorldTourMap", typeof(RectTransform));
        go.layer = panel.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(panel, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0f, bottomInset);
        rt.offsetMax = new Vector2(0f, -topInset);
        var map = go.AddComponent<WorldTourMap>();
        map._catalog = catalog;
        map.Construct();
        return map;
    }

    private static GameObject s_fullScreen;
    private static RectTransform s_activeBadge;

    public static bool IsFullScreenOpen => s_fullScreen != null;
    /// Sıradaki (tamir edilecek) harikanın pini — dünya öğreticisi bunu gösterir.
    public static RectTransform ActiveBadge => s_activeBadge;

    public static void CloseFullScreen()
    {
        if (s_fullScreen != null) Destroy(s_fullScreen);
        s_fullScreen = null;
    }

    /// Ana menü dünya butonundan: harita kendi tam ekran canvas'ında açılır; sol üst ✕ kapatır.
    /// Şehir gezisi (860) bu ekranın (850) üstünde açılır.
    public static void ShowFullScreen(WonderCatalog catalog)
    {
        if (s_fullScreen != null || catalog == null || catalog.Count == 0 || !HasArt) return;
        var go = new GameObject("WorldTourScreen", typeof(RectTransform));
        go.layer = 5;
        s_fullScreen = go;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        var root = (RectTransform)go.transform;

        var bg = NewRectStatic("Backdrop", root);
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        bg.gameObject.AddComponent<Image>().color = new Color(0.36f, 0.66f, 0.86f);   // harita denizi

        Canvas.ForceUpdateCanvases();
        Build(root, catalog, 0f, 0f);

        var close = NewRectStatic("Close", root);
        close.anchorMin = close.anchorMax = new Vector2(0f, 1f);
        close.anchoredPosition = new Vector2(90f, -150f);
        close.sizeDelta = new Vector2(110f, 110f);
        var closeImg = close.gameObject.AddComponent<Image>();
        closeImg.sprite = UiIcons.Close;
        closeImg.preserveAspect = true;
        close.gameObject.AddComponent<Button>().onClick.AddListener(() => Destroy(go));
    }

    private static RectTransform NewRectStatic(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private void Construct()
    {
        var mapSprite = Resources.Load<Sprite>(ArtPath + "WorldMap");
        _pinLocked = Resources.Load<Sprite>(ArtPath + "Pin_Locked");
        _pinOpen = Resources.Load<Sprite>(ArtPath + "Pin_Open");
        _plane = Resources.Load<Sprite>(ArtPath + "Plane");
        _circle = CircleSprite();

        var self = (RectTransform)transform;
        _scroll = gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = true; _scroll.vertical = false;
        _scroll.movementType = ScrollRect.MovementType.Elastic;
        _scroll.inertia = true;

        var viewport = NewRect("Viewport", self);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.001f);
        _scroll.viewport = viewport;

        // Harita yüksekliği görünen alanı doldurur; genişlik oranla (yatay kaydırılır).
        float h = Mathf.Max(400f, self.rect.height > 10f ? self.rect.height : 1500f);
        float aspect = mapSprite.rect.width / Mathf.Max(1f, mapSprite.rect.height);
        _content = NewRect("Map", viewport);
        _content.anchorMin = _content.anchorMax = new Vector2(0f, 0.5f);
        _content.pivot = new Vector2(0f, 0.5f);
        _content.sizeDelta = new Vector2(h * aspect, h);
        var mapImg = _content.gameObject.AddComponent<Image>();
        mapImg.sprite = mapSprite;
        mapImg.raycastTarget = true;   // sürükleme
        _scroll.content = _content;

        int count = _catalog.Count;
        int active = WonderProgress.ActiveEventIndex(_catalog);
        var positions = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            var w = _catalog.Get(i);
            positions[i] = SpotOf(w, i, count);
        }

        // Rota: sıradaki harikalar arası kesikli noktalar (açılmamışa giden kısım soluk).
        var routeLayer = NewRect("Route", _content);
        Stretch(routeLayer);
        for (int i = 0; i + 1 < count; i++)
            DrawDots(routeLayer, positions[i], positions[i + 1], i + 1 <= active ? 1f : 0.35f);

        for (int i = 0; i < count; i++)
        {
            var w = _catalog.Get(i);
            if (w == null) { _badges.Add(null); continue; }
            bool done = WonderProgress.IsEventComplete(_catalog, i);
            bool isActive = i == active && !done;
            bool open = WonderProgress.IsEventUnlocked(_catalog, i);
            _badges.Add(BuildBadge(w, i, positions[i], done, isActive, open || done));
        }

        s_activeBadge = active >= 0 && active < _badges.Count ? _badges[active] : null;
        StartCoroutine(Intro(active, positions));
    }

    private Vector2 SpotOf(WonderDefinition w, int i, int count)
    {
        Vector2 n = w != null && Spots.TryGetValue(w.wonderId, out var p) ? p
            : new Vector2((i + 1f) / (count + 1f), 0.5f);   // tanımsız harika: yatay dizilir
        return new Vector2(n.x * _content.sizeDelta.x, (n.y - 0.5f) * _content.sizeDelta.y);
    }

    private RectTransform BuildBadge(WonderDefinition w, int index, Vector2 pos, bool done, bool isActive, bool open)
    {
        var badge = NewRect($"Badge_{w.wonderId}", _content);
        badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
        // Pin: ucu harikanın konumuna oturur (pivot alt-orta). Kilitli = kapalı kilit, açılmış = açık kilit.
        badge.pivot = new Vector2(0.5f, 0f);
        badge.sizeDelta = new Vector2(PinWidth, PinWidth * PinAspect);
        badge.anchoredPosition = pos;
        var pin = NewRect("Pin", badge);
        Stretch(pin);
        var pinImg = pin.gameObject.AddComponent<Image>();
        pinImg.sprite = open ? _pinOpen : _pinLocked;
        pinImg.preserveAspect = true;
        pinImg.raycastTarget = false;

        if (done || isActive)
        {
            float half = badge.sizeDelta.y * 0.5f;
            var label = NewText(badge, w.LocalizedName, new Vector2(0f, -half - 34f), new Vector2(PinWidth * 2.4f, 56f), 40f);
            TextSurfaceStyle.Apply(label, new Color(0.30f, 0.14f, 0.05f));
            if (isActive)
            {
                // Warm gold marks the current destination against the blue/green map.
                // Vertex tint keeps the shared text material and other destinations unchanged.
                label.color = new Color(1f, 0.78f, 0.20f);
                var here = NewText(badge, GameLocalization.GetOr("worldtour_here", "Şimdi burada"),
                    new Vector2(0f, half + 34f), new Vector2(PinWidth * 2.2f, 50f), 36f);
                TextSurfaceStyle.Apply(here, new Color(0.62f, 0.08f, 0.10f));
                here.color = new Color(1f, 0.90f, 0.52f);
            }
            var button = badge.gameObject.AddComponent<Button>();
            var hit = badge.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            int captured = index;
            var badgeRt = badge;
            button.onClick.AddListener(() =>
            {
                // Şehir görseli varsa şehir gezisi (rozetten yakınlaşır), yoksa harikanın büyük görünümü.
                if (!CityTourView.Show(_catalog, captured, badgeRt)) WorldTourWonderView.Show(_catalog, captured);
            });
        }
        return badge;
    }

    private void DrawDots(RectTransform layer, Vector2 a, Vector2 b, float alpha)
    {
        // Hafif kavisli rota: orta nokta yukarı itilir.
        Vector2 ctrl = (a + b) * 0.5f + new Vector2(0f, Vector2.Distance(a, b) * 0.18f);
        float len = Vector2.Distance(a, b) * 1.1f;
        int n = Mathf.Max(2, Mathf.FloorToInt(len / DotSpacing));
        float edge = 24f / Mathf.Max(1f, len);   // pin uçlarının hemen dibi boş kalır
        for (int k = 1; k < n; k++)
        {
            float t = k / (float)n;
            if (t < edge || t > 1f - edge) continue;
            var dot = NewRect("Dot", layer);
            dot.anchorMin = dot.anchorMax = new Vector2(0f, 0.5f);
            dot.sizeDelta = new Vector2(16f, 16f);
            dot.anchoredPosition = Bezier(a, ctrl, b, t);
            var img = dot.gameObject.AddComponent<Image>();
            img.sprite = _circle;
            img.color = new Color(1f, 1f, 1f, 0.9f * alpha);
            img.raycastTarget = false;
        }
    }

    // Açılış: şu anki harikaya kaydır; yeni harikaya geçilmişse uçak rotada bir kez uçar.
    private IEnumerator Intro(int active, Vector2[] positions)
    {
        yield return null;
        int seen = PlayerPrefs.GetInt(SeenKey, -1);
        int target = Mathf.Clamp(active, 0, positions.Length - 1);
        bool fly = _plane != null && seen >= 0 && seen < target && target < positions.Length;
        CenterOn(positions[fly ? seen : target]);
        PlayerPrefs.SetInt(SeenKey, target);
        PlayerPrefs.Save();
        if (!fly) yield break;

        yield return new WaitForSecondsRealtime(0.4f);
        var plane = NewRect("Plane", _content);
        plane.anchorMin = plane.anchorMax = new Vector2(0f, 0.5f);
        plane.sizeDelta = new Vector2(190f, 190f);
        var img = plane.gameObject.AddComponent<Image>();
        img.sprite = _plane;
        img.preserveAspect = true;
        img.raycastTarget = false;
        for (int leg = seen; leg < target; leg++)
        {
            Vector2 a = positions[leg], b = positions[leg + 1];
            Vector2 ctrl = (a + b) * 0.5f + new Vector2(0f, Vector2.Distance(a, b) * 0.18f);
            img.rectTransform.localScale = new Vector3(b.x >= a.x ? 1f : -1f, 1f, 1f);   // uçağın yönü
            const float dur = 1.6f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                Vector2 p = Bezier(a, ctrl, b, k) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * 30f);
                plane.anchoredPosition = p;
                CenterOn(p);
                yield return null;
            }
        }
        var badge = _badges.Count > target ? _badges[target] : null;
        Destroy(plane.gameObject);
        if (badge != null) yield return Punch(badge);
    }

    private void CenterOn(Vector2 contentPos)
    {
        float viewW = ((RectTransform)_scroll.viewport).rect.width;
        float maxX = Mathf.Max(0f, _content.sizeDelta.x - viewW);
        float x = Mathf.Clamp(contentPos.x - viewW * 0.5f, 0f, maxX);
        _content.anchoredPosition = new Vector2(-x, 0f);
        _scroll.velocity = Vector2.zero;
    }

    private static IEnumerator Punch(RectTransform rt)
    {
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            rt.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(t / 0.35f * Mathf.PI));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        => Vector2.Lerp(Vector2.Lerp(a, c, t), Vector2.Lerp(c, b, t), t);

    // ── Yardımcılar ──────────────────────────────────────────────────

    private static Sprite s_circle;

    // Yumuşak kenarlı daire (rozet maskesi + rota noktası). Bir kez üretilir, paylaşılır.
    private static Sprite CircleSprite()
    {
        if (s_circle != null) return s_circle;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        float r = n * 0.5f - 1f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
            float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s_circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return s_circle;
    }

    private RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
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
