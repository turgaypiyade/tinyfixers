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
[RequireComponent(typeof(CanvasGroup))]   // Alpha setter/Build CanvasGroup'a erişir; her zaman var olsun
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

    [Header("Tema override (boşsa Resources/MiniLift'ten yüklenir — booster default'u etkilenmez)")]
    [SerializeField] private Sprite baseSpriteOverride;
    [SerializeField] private Sprite platformSpriteOverride;
    [SerializeField] private Sprite armFrontSpriteOverride;
    [SerializeField] private Sprite armBackSpriteOverride;
    [SerializeField] private Sprite boltSpriteOverride;
    [SerializeField] private bool preserveRootTransform;
    [SerializeField, Range(0.5f, 1f)] private float backArmAlpha = 1f;
    [SerializeField, Range(0.5f, 1f)] private float boltScale = 1f;
    [SerializeField, Min(0f)] private float armLayerOffsetY = 0f;
    [SerializeField, Min(0f)] private float baseMountYReferencePx = 0f;
    [SerializeField] private bool armsInFrontOfBase;
    [SerializeField] private bool simpleCrossMode;
    [SerializeField] private bool progressiveStageReveal;
    [SerializeField, Range(0f, 1f)] private float collapsedStageAlpha = 0.28f;
    [SerializeField, Min(0)] private int stageCountOverride = 0;

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

    // Override varsa onu kullan (Rising teması); yoksa Resources/MiniLift default (booster).
    private Sprite Resolve(Sprite over, string file) => over != null ? over : Load(file);

    /// <summary>Görseli inşa eder. maxHeightUI: tablanın kat edeceği toplam yükseklik (UI birimi).</summary>
    public void Build(float maxHeightUI, float tileSize)
    {
        _maxHeightUI = Mathf.Max(1f, maxHeightUI);
        var baseSprite = Resolve(baseSpriteOverride, "Lift_base");

        _u    = (1.15f * tileSize) / RefBaseW;
        _L    = ArmPinToPin * _u;
        _baseH = ReferenceHeightFor(baseSprite, RefBaseW, RefBaseH) * _u;
        _platH = RefPlatH * _u;
        _armW  = RefArmW  * _u;
        _armH  = RefArmH  * _u;
        _bolt  = RefBolt  * _u;
        _mountY0 = baseMountYReferencePx > 0f
            ? baseMountYReferencePx * _u
            : _baseH * BaseMountFraction;

        // Kademe sayısı: tam açıkken θ ≤ MaxAngle olacak şekilde.
        float maxStageH = _L * Mathf.Sin(MaxAngleDeg * Mathf.Deg2Rad);
        _stages = stageCountOverride > 0
            ? stageCountOverride
            : Mathf.Max(2, Mathf.CeilToInt(_maxHeightUI / Mathf.Max(0.0001f, maxStageH)));

        _root = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        if (!preserveRootTransform)
        {
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
        }
        _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;

        if (armsInFrontOfBase)
        {
            _base = MakeImage("base", baseSprite, RefBaseW * _u, _baseH);
            _base.rectTransform.anchoredPosition = new Vector2(0f, _baseH * 0.5f);
        }

        // Arkalar önce (geride), sonra önler → doğru çaprazlaşma.
        _backArms  = new Image[_stages];
        _frontArms = new Image[_stages];
        for (int i = 0; i < _stages; i++)
            _backArms[i]  = MakeImage("armBack", Resolve(armBackSpriteOverride, "arm_back"), _armW, _armH, new Color(0.78f, 0.84f, 0.94f, backArmAlpha));
        for (int i = 0; i < _stages; i++)
            _frontArms[i] = MakeImage("armFront", Resolve(armFrontSpriteOverride, "arm_front"), _armW, _armH);

        if (!simpleCrossMode)
        {
            _centerBolts = new Image[_stages];
            for (int i = 0; i < _stages; i++)
                _centerBolts[i] = MakeImage("boltC", Resolve(boltSpriteOverride, "Lift_bolt"), _bolt * boltScale, _bolt * boltScale);

            int boundaries = _stages + 1;
            _boundBoltsL = new Image[boundaries];
            _boundBoltsR = new Image[boundaries];
            for (int i = 0; i < boundaries; i++)
            {
                _boundBoltsL[i] = MakeImage("boltL", Resolve(boltSpriteOverride, "Lift_bolt"), _bolt * boltScale, _bolt * boltScale);
                _boundBoltsR[i] = MakeImage("boltR", Resolve(boltSpriteOverride, "Lift_bolt"), _bolt * boltScale, _bolt * boltScale);
            }
        }

        // Default booster'da taban önde kalır; Rising'de kollar tabanın içinden öne çıkar.
        if (!armsInFrontOfBase)
        {
            _base = MakeImage("base", baseSprite, RefBaseW * _u, _baseH);
            _base.rectTransform.anchoredPosition = new Vector2(0f, _baseH * 0.5f);
        }
        _platform = MakeImage("platform", Resolve(platformSpriteOverride, "Lift_up"), RefPlatW * _u, _platH);

        SetExtension01(0f);
    }

    private Image MakeImage(string name, Sprite sprite, float w, float h, Color? colorOverride = null)
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
        img.color = colorOverride ?? Color.white;
        img.raycastTarget = false;
        img.preserveAspect = false;   // sizeDelta zaten sprite oranında
        return img;
    }

    private static float ReferenceHeightFor(Sprite sprite, float referenceWidth, float fallbackHeight)
    {
        if (sprite == null || sprite.rect.width <= 0.01f)
            return fallbackHeight;
        return sprite.rect.height / sprite.rect.width * referenceWidth;
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

        if (progressiveStageReveal)
        {
            SetPlatformHeightProgressive(heightUI);
            return;
        }

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
                _frontArms[i].rectTransform.anchoredPosition = pos + new Vector2(0f, armLayerOffsetY * 0.5f);
                _frontArms[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, thetaDeg);
            }
            if (_backArms[i] != null)
            {
                _backArms[i].rectTransform.anchoredPosition = pos - new Vector2(0f, armLayerOffsetY * 0.5f);
                _backArms[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -thetaDeg);
            }
            if (_centerBolts != null && _centerBolts[i] != null)
                _centerBolts[i].rectTransform.anchoredPosition = pos;
        }

        if (_boundBoltsL != null && _boundBoltsR != null)
        {
            int boundaries = _stages + 1;
            for (int i = 0; i < boundaries; i++)
            {
                float by = _mountY0 + i * h;
                if (_boundBoltsL[i] != null) _boundBoltsL[i].rectTransform.anchoredPosition = new Vector2(-span * 0.5f, by);
                if (_boundBoltsR[i] != null) _boundBoltsR[i].rectTransform.anchoredPosition = new Vector2(span * 0.5f, by);
            }
        }

        float totalH = _stages * h;
        if (_platform != null)
            _platform.rectTransform.anchoredPosition = new Vector2(0f, _mountY0 + totalH + _platH * 0.30f);
    }

    private void SetPlatformHeightProgressive(float heightUI)
    {
        heightUI = Mathf.Clamp(heightUI, 0f, _maxHeightUI);

        float normalized = heightUI / Mathf.Max(0.0001f, _maxHeightUI);
        int activeStages = heightUI <= 0.01f
            ? 0
            : Mathf.Clamp(Mathf.CeilToInt(normalized * _stages), 1, _stages);

        float perActiveStage = activeStages > 0 ? heightUI / activeStages : 0f;
        float sinMax = Mathf.Sin(MaxAngleDeg * Mathf.Deg2Rad);
        float sinT = Mathf.Clamp(perActiveStage / Mathf.Max(0.0001f, _L), 0f, sinMax);
        float cosT = Mathf.Sqrt(Mathf.Max(0f, 1f - sinT * sinT));
        float thetaDeg = Mathf.Asin(sinT) * Mathf.Rad2Deg;

        float h = _L * sinT;
        float span = _L * cosT;
        float collapsedStepY = Mathf.Max(1f, _armH * 0.06f);

        for (int i = 0; i < _stages; i++)
        {
            bool active = i < activeStages;
            float stageCenter = active
                ? _mountY0 + i * h + h * 0.5f
                : _mountY0 + Mathf.Min(i, 2) * collapsedStepY;
            float stageTheta = active ? thetaDeg : 0f;

            var pos = new Vector2(0f, stageCenter);

            if (_frontArms[i] != null)
            {
                _frontArms[i].rectTransform.anchoredPosition = pos + new Vector2(0f, armLayerOffsetY * 0.5f);
                _frontArms[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, stageTheta);
                SetAlpha(_frontArms[i], active ? 1f : collapsedStageAlpha);
            }

            if (_backArms[i] != null)
            {
                _backArms[i].rectTransform.anchoredPosition = pos - new Vector2(0f, armLayerOffsetY * 0.5f);
                _backArms[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -stageTheta);
                SetAlpha(_backArms[i], active ? backArmAlpha : collapsedStageAlpha * backArmAlpha);
            }

            if (_centerBolts != null && _centerBolts[i] != null)
            {
                _centerBolts[i].rectTransform.anchoredPosition = pos;
                SetAlpha(_centerBolts[i], active ? 1f : collapsedStageAlpha);
            }
        }

        if (_boundBoltsL != null && _boundBoltsR != null)
        {
            int boundaries = _stages + 1;
            for (int i = 0; i < boundaries; i++)
            {
                bool active = i <= activeStages;
                float by = active
                    ? _mountY0 + Mathf.Min(i, activeStages) * h
                    : _mountY0 + Mathf.Min(i, 2) * collapsedStepY;

                if (_boundBoltsL[i] != null)
                {
                    _boundBoltsL[i].rectTransform.anchoredPosition = new Vector2(-span * 0.5f, by);
                    SetAlpha(_boundBoltsL[i], active ? 1f : collapsedStageAlpha);
                }

                if (_boundBoltsR[i] != null)
                {
                    _boundBoltsR[i].rectTransform.anchoredPosition = new Vector2(span * 0.5f, by);
                    SetAlpha(_boundBoltsR[i], active ? 1f : collapsedStageAlpha);
                }
            }
        }

        if (_platform != null)
            _platform.rectTransform.anchoredPosition = new Vector2(0f, _mountY0 + activeStages * h + _platH * 0.30f);
    }

    private static void SetAlpha(Image image, float alpha)
    {
        var c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
