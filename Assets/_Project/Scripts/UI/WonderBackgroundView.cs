using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menü arka planı: her zaman TAMAMLANMIŞ harikayı (BackgroundWonder) tam açık +
/// ambient robotlarla gösterir. Hiç tamamlanmamışsa (completedCount=0) catalog.defaultBackground.
/// Bir harika bitince (WonderProgress.OnWonderCompleted) yeni arka plan SAĞDAN slide-in gelir.
/// Reveal (kaynak) burada OLMAZ — o overlay/mission tarafında. [[project_wonder_reveal_background]]
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class WonderBackgroundView : MonoBehaviour
{
    [SerializeField] private WonderCatalog catalog;
    [SerializeField] private Shader revealShader;
    [SerializeField] private Sprite weldLightSprite;
    [Tooltip("İçeriğin kurulduğu tam ekran kap (slide bunun x'ini oynatır)")]
    [SerializeField] private RectTransform content;
    [SerializeField] private float slideDuration = 0.6f;

    WonderScene _scene;
    Image _defaultImg;
    RectTransform _rt;
    Coroutine _slide;

    void Awake() => _rt = (RectTransform)transform;

    void OnEnable()
    {
        WonderProgress.OnWonderCompleted += HandleWonderCompleted;
        RefreshInstant();
    }

    void OnDisable() => WonderProgress.OnWonderCompleted -= HandleWonderCompleted;

    /// <summary>Mevcut duruma göre arka planı anında kurar (slide yok).</summary>
    public void RefreshInstant()
    {
        var bg = WonderProgress.BackgroundWonder(catalog);
        if (bg != null) BuildWonder(bg);
        else ShowDefault();
        Container.anchoredPosition = Vector2.zero;
    }

    void HandleWonderCompleted(int completedIndex)
    {
        // Yeni completed harika = artık BackgroundWonder. Sağdan kaydırarak getir.
        var bg = WonderProgress.BackgroundWonder(catalog);
        if (bg == null) { RefreshInstant(); return; }
        BuildWonder(bg);
        if (_slide != null) StopCoroutine(_slide);
        _slide = StartCoroutine(SlideInFromRight());
    }

    RectTransform Container => content != null ? content : _rt;

    IEnumerator SlideInFromRight()
    {
        float w = _rt.rect.width > 0 ? _rt.rect.width : 1080f;
        var c = Container;
        c.anchoredPosition = new Vector2(w, 0f);
        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideDuration));
            c.anchoredPosition = new Vector2(Mathf.Lerp(w, 0f, k), 0f);
            yield return null;
        }
        c.anchoredPosition = Vector2.zero;
        _slide = null;
    }

    // ---- İçerik kurulumu ----------------------------------------------

    void BuildWonder(WonderDefinition def)
    {
        if (_defaultImg != null) _defaultImg.gameObject.SetActive(false);
        EnsureScene();
        var view = _scene.Build(def);
        view.SetRevealImmediate(1f);                 // tamamlanmış = tam açık
        if (view.welderRobot != null)
            view.welderRobot.gameObject.SetActive(false);  // kaynakçı gizli (iş bitti)
        // Ambient robotlar hemen yürüsün
        if (view.ambientAgents != null)
            foreach (var a in view.ambientAgents)
                if (a != null) { a.startWalking = true; a.BeginWalking(); }
    }

    void ShowDefault()
    {
        if (_scene != null) _scene.gameObject.SetActive(false);
        EnsureDefaultImage();
        _defaultImg.gameObject.SetActive(true);
        _defaultImg.sprite = catalog != null ? catalog.defaultBackground : null;
    }

    void EnsureScene()
    {
        if (_scene != null) { _scene.gameObject.SetActive(true); return; }
        var go = new GameObject("BackgroundWonderScene", typeof(RectTransform), typeof(WonderScene))
        { layer = gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(Container, false);
        Stretch(rt);
        _scene = go.GetComponent<WonderScene>();
        _scene.revealShader = revealShader != null ? revealShader : Shader.Find("UI/WonderReveal");
        _scene.weldLightSprite = weldLightSprite;
        _scene.buildOnStart = false;
        _scene.charactersWalkImmediately = true;
    }

    void EnsureDefaultImage()
    {
        if (_defaultImg != null) return;
        var go = new GameObject("DefaultBackground", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter))
        { layer = gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(Container, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        _defaultImg = go.GetComponent<Image>();
        _defaultImg.preserveAspect = false;
        var fitter = go.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        var s = catalog != null ? catalog.defaultBackground : null;
        if (s != null) fitter.aspectRatio = (float)s.texture.width / s.texture.height;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
