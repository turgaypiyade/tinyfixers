using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bir "dünya harikası" arka planını alttan yukarı kaynak/inşa efektiyle açar.
/// Tek imaj + UI/WonderReveal shader; yıldız harcadıkça kademe artar, _Reveal animasyonla dolar.
/// Görev overlay'i paslı resmin eğri kenarlı bölgelerini onarır; diğer görünümler hologramı korur.
/// Ada sistemine dokunmaz — bağımsız bir sunum bileşenidir. [[project_worldmap_region_unlock]]
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class WonderRevealView : MonoBehaviour
{
    [Header("Kimlik / Kalıcılık")]
    [Tooltip("PlayerPrefs anahtarı: wonder_reveal_<id>")]
    public string wonderId = "pisa";
    [Tooltip("Kaç yıldız kademesinde tam açılsın")]
    public int totalStages = 5;

    [Header("Animasyon")]
    public float animateDuration = 1.1f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Paslı Resim Restorasyonu")]
    [Tooltip("Paslı ve solgun resmi sabit sıradaki eğri kenarlı bölgeler halinde onarır.")]
    public bool useRustRestoration;
    [Tooltip("Kesilen paslı parçanın öne kıvrılıp düşme süresi.")]
    [Min(0.1f)] public float restorationDuration = 1.15f;

    [Header("Kaynakçı Robot (opsiyonel)")]
    [Tooltip("Açılma sınırında gezen robot konteyneri (RectTransform)")]
    public RectTransform welderRobot;
    [Tooltip("Frame'lerin yazılacağı robot Image'ı (boşsa welderRobot'un kendi Image'ı)")]
    public Image welderImage;
    [Tooltip("Robotun ucundaki kıvılcım efekti")]
    public ParticleSystem welderSparks;
    [Tooltip("Robotun sınır boyunca rastgele yatay salınım genliği (px)")]
    public float welderXJitter = 120f;

    [Header("Kaynak Arkı Işığı (torç ucu)")]
    [Tooltip("Torç ucunda titreşen radyal ışık (Image, yumuşak daire)")]
    public Image weldLight;
    [Tooltip("Işık taban rengi (kaynak arkı — sıcak sarı-beyaz, maviyle blend)")]
    public Color weldLightColor = new Color(1.5f, 1.2f, 0.55f, 1f);
    [Tooltip("Titreşim hızı")]
    public float weldFlickerSpeed = 28f;
    [Tooltip("Işığın taban ölçeği")]
    public float weldLightScale = 1f;

    [Header("Kaynak Frame Animasyonu")]
    [Tooltip("MW_1..MW_5 bir kez oynar; kaynak boyunca son kare sabit kalır. Her yeni kaynak ilk kareden başlar.")]
    public Sprite[] welderFrames;
    [Tooltip("Saniyedeki kare sayısı")]
    public float welderFps = 10f;
    [Tooltip("Kaynak yapılırken son karede geçirilecek en kısa süre (sn).")]
    [Min(0f)] public float minimumWeldHold = 0.6f;
    [Tooltip("Son karedeki torç ucu; görselin sol altından ölçülen normalize konum.")]
    public Vector2 weldTipNormalized = new Vector2(0.9f, 0.14f);

    [Header("Ambient Robotlar")]
    [Tooltip("Sahne %100 açılınca yürümeye başlayacak robotlar")]
    public WonderAmbientAgent[] ambientAgents;

    [Header("Editör Önizleme")]
    [Range(0, 1)] public float previewReveal = 1f;

    Image _image;
    Material _mat;
    RectTransform _rt;
    Image _welderImg;
    int _stage;
    Coroutine _anim;
    WonderWeldSparkGraphic _uiSparks;
    WonderRestorationPieceGraphic _fallingPiece;
    bool _welding;
    int _animationVersion;
    float _reveal;
    static readonly int RevealId = Shader.PropertyToID("_Reveal");
    static readonly int RestorationId = Shader.PropertyToID("_Restoration");
    static readonly int RestorationRectId = Shader.PropertyToID("_RestorationRect");
    static readonly int RegionCountId = Shader.PropertyToID("_RegionCount");
    static readonly int RegionCutId = Shader.PropertyToID("_RegionCut");
    static readonly int RegionStrideId = Shader.PropertyToID("_RegionStride");
    static readonly int SpriteUVRectId = Shader.PropertyToID("_SpriteUVRect");
    static readonly int TorchId = Shader.PropertyToID("_Torch");
    static readonly int RegionFinishId = Shader.PropertyToID("_RegionFinish");

    Image WelderImage
    {
        get
        {
            if (welderImage != null) return welderImage;
            if (_welderImg == null && welderRobot != null)
                _welderImg = welderRobot.GetComponent<Image>();
            return _welderImg;
        }
    }

    string PrefKey => $"wonder_reveal_{wonderId}";

    void OnEnable()
    {
        _image = GetComponent<Image>();
        _rt = (RectTransform)transform;
        EnsureMaterial();

        if (Application.isPlaying)
        {
            _stage = PlayerPrefs.GetInt(PrefKey, 0);
            ApplyReveal(StageToReveal(_stage));
        }
        else
        {
            ApplyReveal(previewReveal);
        }
    }

    void EnsureMaterial()
    {
        if (_image == null) _image = GetComponent<Image>();
        if (_rt == null) _rt = (RectTransform)transform;
        if (_mat != null) return;
        var assigned = _image.material;
        var shader = assigned != null && assigned.shader != null && assigned.shader.name == "UI/WonderReveal"
            ? assigned.shader : Shader.Find("UI/WonderReveal");
        if (shader == null) return;
        _mat = assigned != null && assigned.shader == shader ? new Material(assigned) : new Material(shader);
        _mat.name = $"WonderReveal_{wonderId}";
        _mat.hideFlags = HideFlags.HideAndDontSave;
        _image.material = _mat;
    }

    public void ConfigureShader(Shader shader)
    {
        EnsureMaterial();
        if (_mat != null && shader != null) _mat.shader = shader;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            _image = GetComponent<Image>();
            EnsureMaterial();
            ApplyReveal(previewReveal);
        }
    }
#endif

    // ---- Genel API -----------------------------------------------------

    /// <summary>Kaydedilmiş kademeyi anında uygular (animasyonsuz).</summary>
    public void ApplySavedImmediate()
    {
        _stage = PlayerPrefs.GetInt(PrefKey, 0);
        SetRevealImmediate(StageToReveal(_stage));
    }

    /// <summary>Bir kademe aç (yıldız harcandığında çağır). Animasyonlu.</summary>
    public void AdvanceOneStage()
    {
        SetStage(Mathf.Min(_stage + 1, totalStages), animated: true);
    }

    /// <summary>Belirli bir kademeye git. animated=false ise anında.</summary>
    public void SetStage(int stage, bool animated)
    {
        stage = Mathf.Clamp(stage, 0, totalStages);
        _stage = stage;
        if (Application.isPlaying)
            PlayerPrefs.SetInt(PrefKey, stage);

        float target = StageToReveal(stage);
        CancelAnimation();
        if (!animated || !Application.isPlaying)
        {
            ApplyReveal(target);
            return;
        }
        _anim = StartCoroutine(AnimateTo(target, _animationVersion));
    }

    /// <summary>Ham _Reveal önizleme (animasyonsuz, test slider'ı için).</summary>
    public void PreviewRevealValue(float r) => SetRevealImmediate(r);

    /// <summary>_Reveal'i anında ayarlar (kaynak animasyonu başlamadan başlangıç noktası).</summary>
    public void SetRevealImmediate(float normalized)
    {
        CancelAnimation();
        ApplyReveal(Mathf.Clamp01(normalized));
    }

    /// <summary>Mevcut _Reveal'den hedefe kaynaklayarak açar (yield edilebilir). Overlay kullanır.</summary>
    public IEnumerator PlayRevealRoutine(float targetNormalized)
    {
        CancelAnimation();
        return AnimateTo(Mathf.Clamp01(targetNormalized), _animationVersion);
    }

    float StageToReveal(int stage) => totalStages <= 0 ? 1f : (float)stage / totalStages;

    // ---- İç işleyiş ----------------------------------------------------

    IEnumerator AnimateTo(float target, int version)
    {
        EnsureMaterial();
        float start = _reveal;
        if (target <= start || _mat == null)
        {
            ApplyReveal(target);
            yield break;
        }
        if (useRustRestoration)
        {
            yield return AnimateRestoration(target, version);
            yield break;
        }
        ApplyReveal(start);
        ResetWelderFrame();
        UpdateWelder(start);
        StopWeldingEffects(true);
        if (welderRobot != null) welderRobot.gameObject.SetActive(true);
        try
        {
            // Preparation plays once. Keep MW_1 visible before advancing any time.
            float fps = Mathf.Max(0.01f, welderFps);
            float preparation = welderFrames != null ? Mathf.Max(0, welderFrames.Length - 1) / fps : 0f;
            for (float t = 0f; t < preparation; t += Time.deltaTime)
            {
                if (!AnimationIsCurrent(version)) yield break;
                UpdateWelderFrame(t);
                yield return null;
            }
            if (!AnimationIsCurrent(version)) yield break;
            // Step past the boundary to avoid float rounding holding the penultimate frame.
            UpdateWelderFrame(preparation + 1f / fps);

            // The closed-mask MW_5 pose is held for the actual welding/reveal.
            EnsureWeldSparks();
            UpdateWeldEmitter();
            if (_uiSparks != null) _uiSparks.Begin(GetWeldTipLocal(), GetWelderSize());
            if (welderSparks != null) welderSparks.Play();
            if (weldLight != null) weldLight.gameObject.SetActive(true);
            _welding = true;
            GameEventSfx.StartWelding();
            float duration = Mathf.Max(0.02f, Mathf.Max(animateDuration, minimumWeldHold));
            for (float t = 0f; t < duration;)
            {
                if (!AnimationIsCurrent(version)) yield break;
                t += Time.deltaTime;
                float k = ease.Evaluate(Mathf.Clamp01(t / duration));
                float r = Mathf.Lerp(start, target, k);
                ApplyReveal(r);
                UpdateWelder(r);
                UpdateWeldEmitter();
                UpdateWeldLight(t);
                yield return null;
            }
            if (!AnimationIsCurrent(version)) yield break;
            ApplyReveal(target);
            UpdateWelder(target);
        }
        finally
        {
            if (version == _animationVersion)
            {
                StopWeldingEffects(false);
                ResetRestorationAnimation();
                _anim = null;
            }
        }
        // Leave the final pose in place; reset only when the next operation starts.
        // Tam açıldıysa robotu gizle + ambient robotları başlat
        if (target >= 0.999f)
        {
            if (welderRobot != null) welderRobot.gameObject.SetActive(false);
            StartAmbient();
        }
    }

    IEnumerator AnimateRestoration(float target, int version)
    {
        int count = Mathf.Max(1, totalStages);
        int first = Mathf.Clamp(Mathf.FloorToInt(_reveal * count + 0.001f), 0, count - 1);
        int end = Mathf.Clamp(Mathf.RoundToInt(target * count), first + 1, count);
        try
        {
            for (int stage = first; stage < end; stage++)
            {
                if (!AnimationIsCurrent(version)) yield break;
                int cell = WonderRestorationLayout.CellForStage(stage, count);
                ApplyReveal((float)stage / count);
                StopWeldingEffects(true);
                ResetWelderFrame();
                if (welderRobot != null) welderRobot.gameObject.SetActive(true);
                float fps = Mathf.Max(0.01f, welderFps);
                float preparation = welderFrames != null ? Mathf.Max(0, welderFrames.Length - 1) / fps : 0f;
                for (float t = 0f; t < preparation; t += Time.deltaTime)
                {
                    if (!AnimationIsCurrent(version)) yield break;
                    UpdateWelderFrame(t);
                    UpdateRestorationWelder(cell, 0f);
                    yield return null;
                }
                if (!AnimationIsCurrent(version)) yield break;
                UpdateWelderFrame(preparation + 1f / fps);
                UpdateRestorationWelder(cell, 0f);
                EnsureWeldSparks();
                UpdateWeldEmitter();
                if (_uiSparks != null) _uiSparks.Begin(GetWeldTipLocal(), GetWelderSize());
                if (welderSparks != null) welderSparks.Play();
                if (weldLight != null) weldLight.gameObject.SetActive(true);
                _welding = true;
                GameEventSfx.StartWelding();
                float duration = Mathf.Max(0.02f, Mathf.Max(animateDuration, minimumWeldHold));
                for (float t = 0f; t < duration;)
                {
                    if (!AnimationIsCurrent(version)) yield break;
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / duration);
                    UpdateRestorationWelder(cell, p);
                    _mat.SetVector(RegionCutId, new Vector4(stage, p, 1f, 0f));
                    UpdateWeldEmitter();
                    UpdateWeldLight(t);
                    yield return null;
                }
                if (!AnimationIsCurrent(version)) yield break;
                StopWeldingEffects(false);
                _mat.SetVector(TorchId, Vector4.zero);
                EnsureFallingPiece();
                _fallingPiece.Begin(_image.sprite, _mat, _rt.rect, cell, count);
                _mat.SetVector(RegionFinishId, new Vector4(stage, 0f, 1f, 0f));
                float settleDuration = Mathf.Max(0.1f, restorationDuration);
                for (float t = 0f; t < settleDuration;)
                {
                    if (!AnimationIsCurrent(version)) yield break;
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / settleDuration);
                    _mat.SetVector(RegionFinishId, new Vector4(stage, p, 1f, 0f));
                    _fallingPiece.SetProgress(p);
                    yield return null;
                }
                if (!AnimationIsCurrent(version)) yield break;
                ResetRestorationAnimation();
                ApplyReveal((float)(stage + 1) / count);
            }
            ApplyReveal(target);
            if (welderRobot != null) welderRobot.gameObject.SetActive(false);
            if (target >= 0.999f) StartAmbient();
        }
        finally
        {
            if (version == _animationVersion)
            {
                StopWeldingEffects(false);
                ResetRestorationAnimation();
                _anim = null;
            }
        }
    }

    void EnsureFallingPiece()
    {
        if (_fallingPiece != null) return;
        var go = new GameObject("RestorationFallingPiece", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(WonderRestorationPieceGraphic));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = _rt.pivot;
        rt.SetAsFirstSibling(); // The welder and pooled sparks stay in front.
        _fallingPiece = go.GetComponent<WonderRestorationPieceGraphic>();
        _fallingPiece.raycastTarget = false;
    }

    void EnsureWeldSparks()
    {
        if (_uiSparks != null || WelderImage == null) return;
        var go = new GameObject("WeldFlyingSparks", typeof(RectTransform), typeof(CanvasRenderer), typeof(WonderWeldSparkGraphic));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = _rt.pivot;
        _uiSparks = go.GetComponent<WonderWeldSparkGraphic>();
        _uiSparks.raycastTarget = false;
        _uiSparks.maskable = false;
    }

    Vector3 GetWeldTipWorld()
    {
        var img = WelderImage;
        if (img == null) return transform.position;
        Rect rect = img.rectTransform.rect;
        if (img.preserveAspect && img.sprite != null)
        {
            Vector2 source = img.sprite.rect.size;
            float scale = Mathf.Min(rect.width / source.x, rect.height / source.y);
            Vector2 size = source * scale;
            rect = new Rect(rect.center - size * 0.5f, size);
        }
        return img.rectTransform.TransformPoint(new Vector3(
            rect.xMin + rect.width * weldTipNormalized.x,
            rect.yMin + rect.height * weldTipNormalized.y, 0f));
    }

    Vector2 GetWeldTipLocal() => _uiSparks.rectTransform.InverseTransformPoint(GetWeldTipWorld());

    float GetWelderSize()
    {
        var img = WelderImage;
        if (img == null) return 240f;
        return _rt.InverseTransformVector(img.rectTransform.TransformVector(Vector3.up * img.rectTransform.rect.height)).magnitude;
    }

    void UpdateWeldEmitter()
    {
        if (_uiSparks != null) _uiSparks.SetEmitter(GetWeldTipLocal(), GetWelderSize());
        if (weldLight != null) weldLight.rectTransform.position = GetWeldTipWorld();
        if (welderSparks != null) welderSparks.transform.position = GetWeldTipWorld();
    }

    void StopWeldingEffects(bool clear)
    {
        if (_uiSparks != null)
        {
            if (clear) _uiSparks.Clear();
            else _uiSparks.StopEmitting();
        }
        if (welderSparks != null) welderSparks.Stop(true,
            clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        if (weldLight != null) weldLight.gameObject.SetActive(false);
        if (_welding) GameEventSfx.StopWelding();
        _welding = false;
    }

    bool AnimationIsCurrent(int version) => version == _animationVersion && isActiveAndEnabled;

    void ResetRestorationAnimation()
    {
        if (_fallingPiece != null) _fallingPiece.Clear();
        if (_mat == null) return;
        _mat.SetVector(RegionCutId, Vector4.zero);
        _mat.SetVector(RegionFinishId, Vector4.zero);
        _mat.SetVector(TorchId, Vector4.zero);
    }

    void CancelAnimation()
    {
        // Also invalidates routines yielded by an external overlay coroutine.
        ++_animationVersion;
        if (_anim != null) StopCoroutine(_anim);
        _anim = null;
        StopWeldingEffects(true);
        if (welderRobot != null) welderRobot.gameObject.SetActive(false);
        ResetRestorationAnimation();
    }

    void OnDisable() => CancelAnimation();

    void OnDestroy()
    {
        if (_mat == null) return;
        if (_image != null && _image.material == _mat) _image.material = null;
        if (Application.isPlaying) Destroy(_mat);
        else DestroyImmediate(_mat);
        _mat = null;
    }

    void StartAmbient()
    {
        if (ambientAgents == null) return;
        foreach (var a in ambientAgents)
            if (a != null) a.BeginWalking();
    }

    void ApplyReveal(float r)
    {
        EnsureMaterial();
        _reveal = Mathf.Clamp01(r);
        if (_mat == null) return;
        _mat.SetFloat(RevealId, _reveal);
        _mat.SetFloat(RestorationId, useRustRestoration ? 1f : 0f);
        _mat.SetFloat(RegionCountId, Mathf.Max(1, totalStages));
        _mat.SetFloat(RegionStrideId, WonderRestorationLayout.Stride(totalStages));
        _mat.SetVector(SpriteUVRectId, _image.sprite != null
            ? UnityEngine.Sprites.DataUtility.GetOuterUV(_image.sprite) : new Vector4(0f, 0f, 1f, 1f));
        Rect rect = _rt.rect;
        _mat.SetVector(RestorationRectId, new Vector4(rect.xMin, rect.yMin, rect.width, rect.height));
    }

    void OnRectTransformDimensionsChange()
    {
        if (_mat == null || _rt == null) return;
        Rect rect = _rt.rect;
        _mat.SetVector(RestorationRectId, new Vector4(rect.xMin, rect.yMin, rect.width, rect.height));
    }

    void UpdateRestorationWelder(int cell, float progress)
    {
        Vector2 uv = WonderRestorationLayout.Point(cell, Mathf.Max(1, totalStages), progress);
        // Map follows the photo's local rect, independent of sprite atlas packing.
        Rect rect = _rt.rect;
        Vector3 local = new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, uv.x),
            Mathf.Lerp(rect.yMin, rect.yMax, uv.y), 0f);
        if (welderRobot != null && WelderImage != null)
        {
            Vector3 origin = _rt.InverseTransformPoint(welderRobot.position);
            Vector3 tip = _rt.InverseTransformPoint(GetWeldTipWorld());
            welderRobot.position = _rt.TransformPoint(local - (tip - origin));
        }
        _mat.SetVector(TorchId, new Vector4(uv.x, uv.y, 1f, 0f));
    }

    /// <summary>Robotu açılma sınırının Y'sine oturt, X'te hafif salla.</summary>
    void UpdateWelder(float reveal)
    {
        if (welderRobot == null || _rt == null) return;
        var rect = _rt.rect;
        // reveal 0..1 -> rect alt kenarından üst kenarına
        float y = Mathf.Lerp(rect.yMin, rect.yMax, reveal);
        float x = Mathf.Sin(Time.time * 6f) * welderXJitter;
        welderRobot.anchoredPosition = new Vector2(x, y);
    }

    /// <summary>İlk kareden son kareye bir kez ilerler; son karede kalır.</summary>
    void UpdateWelderFrame(float elapsed)
    {
        if (welderFrames == null || welderFrames.Length == 0) return;
        var img = WelderImage;
        if (img == null) return;
        int idx = Mathf.Clamp(Mathf.FloorToInt(elapsed * Mathf.Max(0.01f, welderFps)), 0, welderFrames.Length - 1);
        if (welderFrames[idx] != null) img.sprite = welderFrames[idx];
    }

    void ResetWelderFrame()
    {
        if (welderFrames == null || welderFrames.Length == 0) return;
        var img = WelderImage;
        if (img != null && welderFrames[0] != null) img.sprite = welderFrames[0];
    }

    /// <summary>Torç ucu kaynak arkı: hızlı düzensiz parlaklık + ölçek titreşimi.</summary>
    void UpdateWeldLight(float elapsed)
    {
        if (weldLight == null) return;
        // İki farklı frekanslı gürültü → düzensiz "cızırdayan" ark hissi
        float n = Mathf.PerlinNoise(elapsed * weldFlickerSpeed, 0.37f);
        float n2 = Mathf.PerlinNoise(elapsed * weldFlickerSpeed * 2.3f, 5.1f);
        float intensity = Mathf.Lerp(0.45f, 1f, n) * Mathf.Lerp(0.7f, 1f, n2);

        var c = weldLightColor;
        c.a = intensity * (useRustRestoration ? 0.72f : 1f);
        weldLight.color = c;

        float s = weldLightScale * Mathf.Lerp(0.82f, 1.18f, n) * (useRustRestoration ? 0.45f : 1f);
        weldLight.rectTransform.localScale = new Vector3(s, s, 1f);
    }
}
