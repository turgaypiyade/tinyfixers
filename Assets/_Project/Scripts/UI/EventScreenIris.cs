using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Event ekranı geçişi: ekran, tıklanan ikonun noktasından büyüyen bir daire içinde açılır; kapanışta aynı
/// daire ikona geri küçülür. Ortak — Harvest'te kullanılıyor, Bridge/Safari'ye de takılabilir.
///
/// Kurulum ilk çağrıda bir kez yapılır: host'un mevcut çocukları IrisMask/IrisContent altına taşınır
/// (referanslar bozulmaz). Mask yalnız animasyon süresince açık; dinlenmede kapalı ve tam ekran gerilmiş
/// olduğundan normal çizime/dokunuşa maliyeti yoktur. Tüm zamanlar unscaled; kare başı ayırma yok.
/// </summary>
public sealed class EventScreenIris : MonoBehaviour
{
    public const float OpenDuration = 0.38f;
    public const float CloseDuration = 0.28f;
    private const float RingScale = 1.04f;      // halkanın parlak dış kenarı dairenin kenarına otursun
    private const float MaxStep = 1f / 30f;     // ilk karedeki ağır açılış (grid kurulumu) animasyonu atlatmasın

    private static Sprite s_circle;
    private static Sprite s_ring;

    private RectTransform host;
    private RectTransform maskRt;
    private RectTransform contentRt;
    private RectTransform ringRt;
    private Image maskImage;
    private Mask mask;
    private Image ringImage;
    private Image blocker;

    private bool running;
    private bool opening;
    private float elapsed;
    private float duration;
    private float startDiameter;
    private float endDiameter;
    private Vector2 center;
    private Action onDone;

    public bool IsRunning => running;

    /// Iris'in eklediği içerik kabı mı? "Ebeveyn = ekran kökü" varsayan kodlar (ör. başlık kabı arama)
    /// bu kabı ekranın kendisi gibi saymalı.
    public static bool IsContainer(Transform t) =>
        t != null && t.parent != null && t.parent.parent != null &&
        t.parent.parent.TryGetComponent(out EventScreenIris iris) && iris.contentRt == t;

    /// Event ikonunun tıklama/geri dönüş noktası (görünür kök, yoksa kendisi).
    public static RectTransform IconRect(Component button, GameObject visibilityRoot) =>
        (visibilityRoot != null ? visibilityRoot.transform : button != null ? button.transform : null) as RectTransform;

    public static EventScreenIris For(RectTransform host)
    {
        if (!host.TryGetComponent(out EventScreenIris iris)) iris = host.gameObject.AddComponent<EventScreenIris>();
        iris.Build(host);
        return iris;
    }

    /// Ekran açılışı. origin null ise ekran ortasından açılır. Host aktif olmalı.
    public void PlayOpen(RectTransform origin, Action done = null) => Begin(origin, true, OpenDuration, done);

    /// Kapanış: daire origin'e küçülür, sonra done (genelde root.SetActive(false)).
    public void PlayClose(RectTransform origin, Action done = null) => Begin(origin, false, CloseDuration, done);

    private void Begin(RectTransform origin, bool open, float time, Action done)
    {
        Rect r = host.rect;
        center = OriginInHost(origin);
        startDiameter = origin != null ? Mathf.Max(60f, Mathf.Min(origin.rect.width, origin.rect.height)) : 60f;
        // En uzak köşeye kadar → daire bitişte tüm ekranı örter.
        float dx = r.width * 0.5f + Mathf.Abs(center.x), dy = r.height * 0.5f + Mathf.Abs(center.y);
        endDiameter = 2f * Mathf.Sqrt(dx * dx + dy * dy) + 8f;

        opening = open;
        duration = time;
        elapsed = 0f;
        onDone = done;
        running = true;

        maskRt.anchorMin = maskRt.anchorMax = maskRt.pivot = new Vector2(0.5f, 0.5f);
        maskRt.anchoredPosition = center;
        contentRt.anchorMin = contentRt.anchorMax = contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.sizeDelta = r.size;
        contentRt.anchoredPosition = -center;          // içerik ekranda sabit kalır, yalnız pencere büyür
        ringRt.anchoredPosition = center;
        maskImage.enabled = true;
        mask.enabled = true;
        ringImage.gameObject.SetActive(ringImage.sprite != null);
        blocker.gameObject.SetActive(true);            // daire dışındaki ana menüye tık geçmesin
        Apply(0f);
    }

    private void Update()
    {
        if (!running) return;
        elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
        float k = Mathf.Clamp01(elapsed / duration);
        Apply(k);
        if (k < 1f) return;

        running = false;
        Rest();
        var done = onDone;
        onDone = null;
        done?.Invoke();
    }

    private void Apply(float k)
    {
        // Açılış: hızlı başlar, yumuşak oturur. Kapanış: yavaş başlar, ikona hızla girer.
        float e = opening ? 1f - Cube(1f - k) : 1f - Cube(k);
        float d = Mathf.LerpUnclamped(startDiameter, endDiameter, e);
        maskRt.sizeDelta = new Vector2(d, d);
        ringRt.sizeDelta = new Vector2(d * RingScale, d * RingScale);
        // Kenar ışığı: açılırken sonda, kapanırken başta söner (tam ekranda halka görünmesin).
        float a = opening ? 1f - Mathf.Clamp01((k - 0.6f) / 0.4f) : Mathf.Clamp01(k / 0.25f);
        var c = ringImage.color;
        c.a = 0.85f * a;
        ringImage.color = c;
    }

    private static float Cube(float x) => x * x * x;

    private void OnDisable()
    {
        // Host animasyon ortasında kapanırsa (sahne geçişi vb.) dinlenme durumuna dön; geri çağrı düşer.
        if (!running) return;
        running = false;
        onDone = null;
        Rest();
    }

    // Dinlenme: tam ekran, mask kapalı → normal çizim/raycast, stencil maliyeti yok.
    private void Rest()
    {
        if (maskRt == null) return;
        Stretch(maskRt);
        Stretch(contentRt);
        mask.enabled = false;
        maskImage.enabled = false;
        ringImage.gameObject.SetActive(false);
        blocker.gameObject.SetActive(false);
    }

    private Vector2 OriginInHost(RectTransform origin)
    {
        if (origin == null || !origin.gameObject.activeInHierarchy) return Vector2.zero;
        Vector3 world = origin.TransformPoint(origin.rect.center);
        Vector2 local = host.InverseTransformPoint(world);
        return local - host.rect.center;
    }

    // ── Kurulum (bir kez) ──────────────────────────────────────────────────────

    private void Build(RectTransform target)
    {
        if (maskRt != null) return;
        host = target;
        int layer = host.gameObject.layer;

        maskImage = NewImage("IrisMask", host, layer, out maskRt);
        maskImage.sprite = Circle();
        maskImage.raycastTarget = false;
        mask = maskImage.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentGo = new GameObject("IrisContent", typeof(RectTransform)) { layer = layer };
        contentRt = (RectTransform)contentGo.transform;
        contentRt.SetParent(maskRt, false);

        // Mevcut çocuklar içeriğe; çizim sırası korunur.
        for (int i = host.childCount - 1; i >= 0; i--)
        {
            var child = host.GetChild(i);
            if (child == maskRt) continue;
            child.SetParent(contentRt, false);
            child.SetAsFirstSibling();
        }
        maskRt.SetAsFirstSibling();

        ringImage = NewImage("IrisRing", host, layer, out ringRt);
        ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.5f);
        ringImage.sprite = Ring();
        ringImage.color = new Color(1f, 0.97f, 0.86f, 0f);   // sıcak beyaz kenar ışığı
        ringImage.raycastTarget = false;
        ringRt.SetSiblingIndex(maskRt.GetSiblingIndex() + 1);

        blocker = NewImage("IrisBlocker", host, layer, out var blockRt);
        Stretch(blockRt);
        blocker.color = Color.clear;
        blocker.raycastTarget = true;

        Rest();
    }

    private static Image NewImage(string name, Transform parent, int layer, out RectTransform rt)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { layer = layer };
        rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private static Sprite Ring()
    {
        if (s_ring == null) s_ring = Resources.Load<Sprite>("VFX/shockwave_ring");
        return s_ring;
    }

    // Kenarı yumuşatılmış daire (mask şekli). Uygulama ömrü boyunca bir kez üretilir.
    private static Sprite Circle()
    {
        if (s_circle != null) return s_circle;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.Alpha8, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "IrisCircle",
        };
        var px = new Color32[size * size];
        float rad = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - rad, dy = y + 0.5f - rad;
                float a = Mathf.Clamp01(rad - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s_circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_circle;
    }
}
