using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makaslı asansör (scissor lift) booster görseli. Taban SABİT durur, tabla makasla yukarı uzar.
/// Tek girdi: <see cref="SetExtension01"/> (0 kapalı → 1 tam açık). Kollar/pimler/tabla θ açısına göre
/// yerleşir; θ = asin((yükseklik/kademe)/L). Parçalar Resources/MiniLift/*'tan yüklenir, grid tile
/// boyutuna göre ölçeklenir. Kademe sayısı sütun yüksekliğine göre otomatik (dinamik).
///
/// Referans art (px): base 300×121, platform(Lift_up) 299×114, arm 300×64 (pim→pim 246, uçlar 27px içeride),
/// bolt 72×72. Kol pivotu merkez (150,32) → rotasyon merkez etrafında; uç pimler ±123 = ±L/2.
/// </summary>
public sealed class ScissorLiftView : MonoBehaviour
{
    // Referans px (çizim ölçüsü)
    private const float RefBaseW = 300f, RefBaseH = 121f;
    private const float RefPlatW = 299f, RefPlatH = 114f;
    private const float RefArmW  = 300f, RefArmH  = 64f;
    private const float RefBolt  = 72f;
    private const float ArmPinToPin = 246f;        // uç pim → uç pim
    private const float BaseMountFraction = 0.62f; // kolların tabandan çıkış yüksekliği (baseH oranı)

    private const float MaxAngleDeg = 80f;   // tam açık kademe açısı (kapalı alt sınır artık 0 = yatık)

    private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

    private RectTransform _root;
    private CanvasGroup _canvasGroup;
    private Image _base, _platform;
    private Image[] _frontArms, _backArms;
    private Image[] _centerBolts;
    private Image[] _boundBoltsL, _boundBoltsR;   // sınır i = 0..stages

    private int _stages;
    private float _u;            // UI birim / ref px
    private float _L;            // kol pim→pim (UI)
    private float _mountY0;      // kademe0 tabanı (root-local UI)
    private float _baseH, _platH, _armW, _armH, _bolt;
    private float _maxHeightUI;

    public int Stages => _stages;

    public float Alpha
    {
        set
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = value;
        }
    }

    private static Sprite Load(string fileName)
    {
        if (_spriteCache.TryGetValue(fileName, out var s) && s != null)
            return s;
        s = Resources.Load<Sprite>("MiniLift/" + fileName);
        _spriteCache[fileName] = s;
        return s;
    }

    /// <summary>Görseli inşa eder. maxHeightUI: tablanın kat edeceği toplam yükseklik (UI birimi).</summary>
    public void Build(float maxHeightUI, float tileSize)
    {
        _maxHeightUI = Mathf.Max(1f, maxHeightUI);
        _u    = (1.15f * tileSize) / RefBaseW;
        _L    = ArmPinToPin * _u;
        _baseH = RefBaseH * _u;
        _platH = RefPlatH * _u;
        _armW  = RefArmW  * _u;
        _armH  = RefArmH  * _u;
        _bolt  = RefBolt  * _u;
        _mountY0 = _baseH * BaseMountFraction;

        // Kademe sayısı: tam açıkken θ ≤ MaxAngle olacak şekilde.
        float maxStageH = _L * Mathf.Sin(MaxAngleDeg * Mathf.Deg2Rad);
        _stages = Mathf.Max(2, Mathf.CeilToInt(_maxHeightUI / Mathf.Max(0.0001f, maxStageH)));

        _root = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0.5f);
        _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;

        // Arkalar önce (geride), sonra önler → doğru çaprazlaşma.
        _backArms  = new Image[_stages];
        _frontArms = new Image[_stages];
        for (int i = 0; i < _stages; i++)
            _backArms[i]  = MakeImage("armBack", Load("arm_back"), _armW, _armH);
        for (int i = 0; i < _stages; i++)
            _frontArms[i] = MakeImage("armFront", Load("arm_front"), _armW, _armH);

        _centerBolts = new Image[_stages];
        for (int i = 0; i < _stages; i++)
            _centerBolts[i] = MakeImage("boltC", Load("Lift_bolt"), _bolt, _bolt);

        int boundaries = _stages + 1;
        _boundBoltsL = new Image[boundaries];
        _boundBoltsR = new Image[boundaries];
        for (int i = 0; i < boundaries; i++)
        {
            _boundBoltsL[i] = MakeImage("boltL", Load("Lift_bolt"), _bolt, _bolt);
            _boundBoltsR[i] = MakeImage("boltR", Load("Lift_bolt"), _bolt, _bolt);
        }

        // Taban + tabla EN ÖNDE (son oluşturulur) → kol uçlarını örter.
        _base     = MakeImage("base", Load("Lift_base"), RefBaseW * _u, _baseH);
        _platform = MakeImage("platform", Load("Lift_up"), RefPlatW * _u, _platH);

        _base.rectTransform.anchoredPosition = new Vector2(0f, _baseH * 0.5f);

        SetExtension01(0f);
    }

    private Image MakeImage(string name, Sprite sprite, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;   // Screen Space Camera culling'e karşı parent layer'ı miras al

        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(_root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = false;   // sizeDelta zaten sprite oranında
        return img;
    }

    /// <summary>0 (kapalı) → 1 (tam açık). Taban sabit; tabla + makas yükselir.</summary>
    public void SetExtension01(float t)
    {
        SetPlatformHeight(Mathf.Clamp01(t) * _maxHeightUI);
    }

    /// <summary>heightUI: mount çizgisinden itibaren tablanın yüksekliği (UI birimi).</summary>
    public void SetPlatformHeight(float heightUI)
    {
        if (_stages <= 0) return;
        heightUI = Mathf.Max(0f, heightUI);

        float perStage = heightUI / _stages;
        // Alt sınır 0: kapalı hâlde (heightUI=0) makas TAM YATIK → collapsed yükseklik = 0,
        // kademe sayısından (grid height) BAĞIMSIZ. Böylece tabla her grid'de aynı yerde başlar.
        float sinMax = Mathf.Sin(MaxAngleDeg * Mathf.Deg2Rad);
        float sinT = Mathf.Clamp(perStage / Mathf.Max(0.0001f, _L), 0f, sinMax);
        float cosT = Mathf.Sqrt(Mathf.Max(0f, 1f - sinT * sinT));
        float thetaDeg = Mathf.Asin(sinT) * Mathf.Rad2Deg;

        float h = _L * sinT;      // clamp sonrası gerçek per-kademe yükseklik
        float span = _L * cosT;

        for (int i = 0; i < _stages; i++)
        {
            float stageCenter = _mountY0 + i * h + h * 0.5f;
            var pos = new Vector2(0f, stageCenter);

            if (_frontArms[i] != null)
            {
                _frontArms[i].rectTransform.anchoredPosition = pos;
                _frontArms[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, thetaDeg);
            }
            if (_backArms[i] != null)
            {
                _backArms[i].rectTransform.anchoredPosition = pos;
                _backArms[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -thetaDeg);
            }
            if (_centerBolts[i] != null)
                _centerBolts[i].rectTransform.anchoredPosition = pos;
        }

        int boundaries = _stages + 1;
        for (int i = 0; i < boundaries; i++)
        {
            float by = _mountY0 + i * h;
            if (_boundBoltsL[i] != null) _boundBoltsL[i].rectTransform.anchoredPosition = new Vector2(-span * 0.5f, by);
            if (_boundBoltsR[i] != null) _boundBoltsR[i].rectTransform.anchoredPosition = new Vector2(span * 0.5f, by);
        }

        float totalH = _stages * h;
        if (_platform != null)
            _platform.rectTransform.anchoredPosition = new Vector2(0f, _mountY0 + totalH + _platH * 0.30f);
    }
}
