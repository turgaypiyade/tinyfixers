using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bir "dünya harikası" arka planını alttan yukarı kaynak/inşa efektiyle açar.
/// Tek imaj + UI/WonderReveal shader; yıldız harcadıkça kademe artar, _Reveal animasyonla dolar.
/// Açılmamış kısım hologram olarak durduğu için ekran hiçbir zaman boş/çirkin görünmez.
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
    bool _welding;

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
        // Her view kendi materyal örneğini kullanır (paylaşımı kirletmesin)
        if (_mat == null || _image.material == null || _image.material.shader == null ||
            _image.material.shader.name != "UI/WonderReveal")
        {
            var shader = Shader.Find("UI/WonderReveal");
            if (shader == null) return;
            _mat = new Material(shader) { name = $"WonderReveal_{wonderId}" };
            _image.material = _mat;
        }
        else
        {
            _mat = _image.material;
        }
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
        ApplyReveal(StageToReveal(_stage));
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
        if (!animated || !Application.isPlaying)
        {
            ApplyReveal(target);
            return;
        }
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateTo(target));
    }

    /// <summary>Ham _Reveal önizleme (animasyonsuz, test slider'ı için).</summary>
    public void PreviewRevealValue(float r) => ApplyReveal(r);

    /// <summary>_Reveal'i anında ayarlar (kaynak animasyonu başlamadan başlangıç noktası).</summary>
    public void SetRevealImmediate(float normalized) => ApplyReveal(Mathf.Clamp01(normalized));

    /// <summary>Mevcut _Reveal'den hedefe kaynaklayarak açar (yield edilebilir). Overlay kullanır.</summary>
    public IEnumerator PlayRevealRoutine(float targetNormalized)
    {
        yield return AnimateTo(Mathf.Clamp01(targetNormalized));
    }

    float StageToReveal(int stage) => totalStages <= 0 ? 1f : (float)stage / totalStages;

    // ---- İç işleyiş ----------------------------------------------------

    IEnumerator AnimateTo(float target)
    {
        EnsureMaterial();
        float start = _mat != null ? _mat.GetFloat("_Reveal") : 0f;
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
                UpdateWelderFrame(t);
                yield return null;
            }
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
                t += Time.deltaTime;
                float k = ease.Evaluate(Mathf.Clamp01(t / duration));
                float r = Mathf.Lerp(start, target, k);
                ApplyReveal(r);
                UpdateWelder(r);
                UpdateWeldEmitter();
                UpdateWeldLight(t);
                yield return null;
            }
            ApplyReveal(target);
            UpdateWelder(target);
        }
        finally
        {
            StopWeldingEffects(false);
            _anim = null;
        }
        // Leave the final pose in place; reset only when the next operation starts.
        // Tam açıldıysa robotu gizle + ambient robotları başlat
        if (target >= 0.999f)
        {
            if (welderRobot != null) welderRobot.gameObject.SetActive(false);
            StartAmbient();
        }
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

    void OnDisable() => StopWeldingEffects(true);

    void StartAmbient()
    {
        if (ambientAgents == null) return;
        foreach (var a in ambientAgents)
            if (a != null) a.BeginWalking();
    }

    void ApplyReveal(float r)
    {
        EnsureMaterial();
        if (_mat != null) _mat.SetFloat("_Reveal", r);
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
        c.a = intensity;
        weldLight.color = c;

        float s = weldLightScale * Mathf.Lerp(0.82f, 1.18f, n);
        weldLight.rectTransform.localScale = new Vector3(s, s, 1f);
    }
}
