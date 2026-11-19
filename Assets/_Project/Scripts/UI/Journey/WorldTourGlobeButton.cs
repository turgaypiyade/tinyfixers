using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menü sağ alt köşedeki dünya butonu (kullanıcı 2026-11-18): dokununca dünya haritası tam ekran açılır.
/// BottomTabController kurar ve yalnız HOME sekmesinde gösterir. Görsel: Resources/WorldTour/GlobeIcon.
/// Dikkat çekme: birkaç saniyede bir kare kare (Globe_0..3) hızlıca döner ve yavaşlayıp durur (arada iş yapılmaz); dokunuşta kısa zıplama.
/// </summary>
public sealed class WorldTourGlobeButton : MonoBehaviour
{
    private const float Size = 150f;   // 200→150 (kullanıcı: %25 küçük, daha sevimli)
    private const float Margin = 24f;
    private const float SpinEvery = 5f;       // iki dönüş arası bekleme (sn)

    /// Dünya butonu açılır: ilk görev adımı yapılınca ya da bu seviyeye gelince (hangisi önce; kullanıcı 2026-11-19).
    public const int UnlockLevel = 8;

    public static bool IsUnlocked(WonderCatalog catalog)
        => CurrentLevel.Global >= UnlockLevel || (catalog != null && catalog.Count > 0 && WonderProgress.StageOf(0) > 0);

    private WonderCatalog _catalog;
    private RectTransform _canvasRoot, _bottomBar, _sitAbove, _icon;
    private Image _iconImage;
    private Sprite[] _frames;   // Globe_0..3 (dönen küre); yoksa tek GlobeIcon, dönüş yok
    private RectTransform _plane;  // WorldTour/Plane: kürenin etrafındaki eğik yörüngede tur atar

    // Uçak yörüngesi (kürenin merkezine göre, Size oranı): eğik elips; üst yarıda kürenin arkasından geçer.
    private const float OrbitRx = 0.52f, OrbitRy = 0.20f, OrbitTiltDeg = 18f, PlaneParkDeg = -25f;   // park: sağda, kürenin önünde, burun sağ-yukarı
    private const float PlaneSize = 0.40f;
    private const float PlaneNoseDeg = 14f;   // görselde gövde ekseni (kuyruk→burun), sağ-hafif yukarı
    private bool _punching;

    /// <param name="placeAfter">Butonun sahnede hemen arkasına konacağı kardeş (popup'ların altında kalsın diye).</param>
    /// <param name="sitAbove">Dünya bu butonun (GÖREV) üstüne, sağ kenarına hizalı oturur; yoksa alt menünün üstüne.</param>
    public static GameObject Create(RectTransform canvasRoot, RectTransform bottomBar, RectTransform sitAbove,
        Transform placeAfter, WonderCatalog catalog)
    {
        if (canvasRoot == null || catalog == null || catalog.Count == 0 || !WorldTourMap.HasArt) return null;
        var frames = LoadFrames();
        var icon = frames != null ? frames[0] : Resources.Load<Sprite>("WorldTour/GlobeIcon");
        if (icon == null) return null;

        var go = new GameObject("WorldTourGlobeButton", typeof(RectTransform));
        go.layer = canvasRoot.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvasRoot, false);
        if (placeAfter != null && placeAfter.parent == canvasRoot) rt.SetSiblingIndex(placeAfter.GetSiblingIndex() + 1);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(Size, Size);

        // Görsel ayrı çocukta: dönüş/zıplama merkezden olur (kök pivotu sağ-alt köşe).
        var iconRt = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
        iconRt.gameObject.layer = go.layer;
        iconRt.SetParent(rt, false);
        iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
        var img = iconRt.gameObject.AddComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        var button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = img;
        var self = go.AddComponent<WorldTourGlobeButton>();
        self._icon = iconRt;
        self._iconImage = img;
        self._frames = frames;
        var planeSprite = Resources.Load<Sprite>("WorldTour/Plane");
        if (planeSprite != null && frames != null)
        {
            var planeRt = new GameObject("Plane", typeof(RectTransform)).GetComponent<RectTransform>();
            planeRt.gameObject.layer = go.layer;
            planeRt.SetParent(rt, false);
            planeRt.anchorMin = planeRt.anchorMax = planeRt.pivot = new Vector2(0.5f, 0.5f);
            planeRt.sizeDelta = Vector2.one * Size * PlaneSize;
            var pImg = planeRt.gameObject.AddComponent<Image>();
            pImg.sprite = planeSprite;
            pImg.preserveAspect = true;
            pImg.raycastTarget = false;
            self._plane = planeRt;
            self.PosePlane(PlaneParkDeg);
        }
        self._catalog = catalog;
        self._canvasRoot = canvasRoot;
        self._bottomBar = bottomBar;
        self._sitAbove = sitAbove;
        self.Place();
        if (frames != null) self.StartCoroutine(self.SpinLoop());   // ilk OnEnable alanlar atanmadan çalıştı
        button.onClick.AddListener(self.Open);
        return go;
    }

    // SafeArea/yerleşim ilk karede oturur → görünür olduğunda bir kare sonra yeniden hizala.
    private void OnEnable()
    {
        if (_canvasRoot != null) StartCoroutine(PlaceNextFrame());
        if (_frames != null) StartCoroutine(SpinLoop());
    }

    // Kare kare dönüş: hızlı başlar, yavaşlayarak ilk karede (Amerika) durur; bekleme süresince Update yok.
    private static readonly float[] SpinFrameTimes = { 0.05f, 0.05f, 0.05f, 0.05f, 0.06f, 0.07f, 0.08f, 0.10f, 0.13f, 0.17f, 0.22f, 0.28f };

    private IEnumerator SpinLoop()
    {
        var wait = new WaitForSecondsRealtime(SpinEvery);
        while (true)
        {
            yield return wait;
            if (_plane != null) StartCoroutine(PlaneLap());
            for (int n = 0; n < SpinFrameTimes.Length; n++)
            {
                _iconImage.sprite = _frames[(n + 1) % _frames.Length];
                yield return new WaitForSecondsRealtime(SpinFrameTimes[n]);
            }
            _iconImage.sprite = _frames[0];
        }
    }

    private IEnumerator PlaceNextFrame()
    {
        yield return null;
        Place();
        if (_plane != null) PosePlane(PlaneParkDeg);   // kapanırken yarım kalan turun katman sırası burada düzelir
    }

    // Konum gerçek buton köşelerinden hesaplanır (canvas birimi, sağ-alt çapaya göre).
    private void Place()
    {
        var rt = (RectTransform)transform;
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        Rect canvasRect = _canvasRoot.rect;
        if (_sitAbove != null && _sitAbove.gameObject.activeInHierarchy)
        {
            _sitAbove.GetWorldCorners(corners);   // 1 = sol-üst, 2 = sağ-üst
            Vector3 topRight = _canvasRoot.InverseTransformPoint(corners[2]);
            rt.anchoredPosition = new Vector2(topRight.x - canvasRect.xMax, topRight.y - canvasRect.yMin + Margin);
            return;
        }
        float barTop = 260f;
        if (_bottomBar != null)
        {
            _bottomBar.GetWorldCorners(corners);
            barTop = _canvasRoot.InverseTransformPoint(corners[1]).y - canvasRect.yMin;
        }
        rt.anchoredPosition = new Vector2(-Margin, Mathf.Max(0f, barTop) + Margin);
    }

    // Bir tur: park noktasından başlar, hızlı çıkar, yavaşlayarak park noktasına döner (küreyle aynı süre).
    private IEnumerator PlaneLap()
    {
        float total = 0f;
        foreach (float t in SpinFrameTimes) total += t;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / total, 3f);
            PosePlane(PlaneParkDeg + 360f * k);
            yield return null;
        }
        PosePlane(PlaneParkDeg);
    }

    // Elips üzerindeki açıya göre konum, yön (teğet), derinlik (arkada küçük + kürenin arkasında).
    // reorder=false: obje açılıp kapanırken çağrılır — Unity o anda kardeş sırası değişimine izin vermez.
    private void PosePlane(float deg, bool reorder = true)
    {
        float a = deg * Mathf.Deg2Rad, tilt = OrbitTiltDeg * Mathf.Deg2Rad;
        var local = new Vector2(Mathf.Cos(a) * OrbitRx, Mathf.Sin(a) * OrbitRy) * Size;
        var tangent = new Vector2(-Mathf.Sin(a) * OrbitRx, Mathf.Cos(a) * OrbitRy);
        local = Rotate(local, tilt);
        tangent = Rotate(tangent, tilt);
        _plane.anchoredPosition = local;

        bool left = tangent.x < 0f;
        float heading = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
        // Sola giderken ayna: burun (180 - PlaneNoseDeg) yönüne bakar.
        float rot = left ? heading - (180f - PlaneNoseDeg) : heading - PlaneNoseDeg;
        _plane.localRotation = Quaternion.Euler(0f, 0f, rot);
        bool behind = Mathf.Sin(a) > 0f;   // üst yarı = kürenin arkası
        float depth = behind ? 0.8f : 1f;
        _plane.localScale = new Vector3(left ? -depth : depth, depth, 1f);
        if (!reorder) return;
        int iconIndex = _icon.GetSiblingIndex();
        if (behind && _plane.GetSiblingIndex() > iconIndex) _plane.SetSiblingIndex(iconIndex);
        else if (!behind && _plane.GetSiblingIndex() < iconIndex) _plane.SetAsLastSibling();
    }

    private static Vector2 Rotate(Vector2 v, float rad)
        => new Vector2(v.x * Mathf.Cos(rad) - v.y * Mathf.Sin(rad), v.x * Mathf.Sin(rad) + v.y * Mathf.Cos(rad));

    private static Sprite[] LoadFrames()
    {
        var frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = Resources.Load<Sprite>("WorldTour/Globe_" + i);
            if (frames[i] == null) return null;
        }
        return frames;
    }

    private void Open()
    {
        if (!_punching) StartCoroutine(Punch());
        WorldTourMap.ShowFullScreen(_catalog);
    }

    private IEnumerator Punch()
    {
        _punching = true;
        const float d = 0.22f;
        for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
        {
            float k = t / d;
            _icon.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        _icon.localScale = Vector3.one;
        _punching = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (_icon == null) return;
        if (_frames != null) _iconImage.sprite = _frames[0];
        if (_plane != null) PosePlane(PlaneParkDeg, reorder: false);
        _icon.localScale = Vector3.one;
        _punching = false;
    }
}
