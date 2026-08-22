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
    [Tooltip("Açılma sınırında gezen robot (RectTransform)")]
    public RectTransform welderRobot;
    [Tooltip("Robotun ucundaki kıvılcım efekti")]
    public ParticleSystem welderSparks;
    [Tooltip("Robotun sınır boyunca rastgele yatay salınım genliği (px)")]
    public float welderXJitter = 120f;

    [Header("Editör Önizleme")]
    [Range(0, 1)] public float previewReveal = 1f;

    Image _image;
    Material _mat;
    RectTransform _rt;
    int _stage;
    Coroutine _anim;

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

    float StageToReveal(int stage) => totalStages <= 0 ? 1f : (float)stage / totalStages;

    // ---- İç işleyiş ----------------------------------------------------

    IEnumerator AnimateTo(float target)
    {
        EnsureMaterial();
        float start = _mat != null ? _mat.GetFloat("_Reveal") : 0f;
        float t = 0f;

        if (welderRobot != null) welderRobot.gameObject.SetActive(true);
        if (welderSparks != null) welderSparks.Play();

        while (t < animateDuration)
        {
            t += Time.deltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / animateDuration));
            float r = Mathf.Lerp(start, target, k);
            ApplyReveal(r);
            UpdateWelder(r);
            yield return null;
        }
        ApplyReveal(target);
        UpdateWelder(target);

        if (welderSparks != null) welderSparks.Stop();
        // Tam açıldıysa robotu gizle
        if (welderRobot != null && target >= 0.999f) welderRobot.gameObject.SetActive(false);
        _anim = null;
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
}
