using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Sahneler arası geçişte tam ekran yükleme görseli gösterir.
/// DontDestroyOnLoad ile sahneler arası yaşar; yeni sahne yüklenince fade-out yapar.
///
/// Kullanım:
///   LoadingScreenManager.Show(sprite);   // eski sistem, sadece tam ekran görsel
///   LoadingScreenManager.Show(hint);     // yeni sistem, görsel + localized TMP text
///   SceneManager.LoadScene("SahneAdı");
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    private const string LoadingHintViewResourcePath = "UI/LoadingHintView";

    private static LoadingScreenManager _instance;

    private CanvasGroup _canvasGroup;
    private float _fadeOutDuration    = 0.35f;
    private float _fadeOutDelay       = 0.08f;
    private float _minimumDisplayTime = 2.0f;
    private float _showTime;

    // Runtime fallback references. Prefab-backed LoadingHintView owns its own styling.
    [Header("Runtime Fallback References")]
    [SerializeField] private Image hintImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text loadingText;

    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Eski sistem: tam ekran yükleme ekranını sadece Sprite ile gösterir.
    /// sprite null ise düz siyah ekran gösterilir.
    /// </summary>
    public static void Show(Sprite sprite, float fadeOutDuration = 0.35f, float fadeOutDelay = 0.08f,
                            float minimumDisplayTime = 2.0f)
    {
        CreateAndShow(sprite, null, fadeOutDuration, fadeOutDelay, minimumDisplayTime);
    }

    /// <summary>
    /// Yeni sistem: image + localized title/description/loading text gösterir.
    /// hint null ise eski fallback davranışıyla siyah ekran gösterilir.
    /// </summary>
    public static void Show(LoadingHintEntry hint, float fadeOutDuration = 0.35f, float fadeOutDelay = 0.08f,
                            float minimumDisplayTime = 2.0f)
    {
        CreateAndShow(hint != null ? hint.image : null, hint, fadeOutDuration, fadeOutDelay, minimumDisplayTime);
    }

    private static void CreateAndShow(Sprite sprite, LoadingHintEntry hint, float fadeOutDuration, float fadeOutDelay,
                                      float minimumDisplayTime)
    {
        if (_instance != null)
            Destroy(_instance.gameObject);

        var root = BuildCanvas();
        var mgr = root.AddComponent<LoadingScreenManager>();
        mgr.BuildVisuals(root.transform, sprite, hint);

        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = false;

        mgr._canvasGroup        = cg;
        mgr._fadeOutDuration    = fadeOutDuration;
        mgr._fadeOutDelay       = fadeOutDelay;
        mgr._minimumDisplayTime = minimumDisplayTime;
        mgr._showTime           = Time.realtimeSinceStartup;

        _instance = mgr;
        DontDestroyOnLoad(root);

        SceneManager.sceneLoaded += mgr.HandleSceneLoaded;
    }

    // ─────────────────────────────────────────────────────────────────

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float elapsed = Time.realtimeSinceStartup - _showTime;
        float remaining = _minimumDisplayTime - elapsed;
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        if (_fadeOutDelay > 0f)
            yield return new WaitForSecondsRealtime(_fadeOutDelay);

        float fadeElapsed = 0f;
        while (fadeElapsed < _fadeOutDuration)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / _fadeOutDuration);
            yield return null;
        }

        _instance = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    // ─────────────────────────────────────────────────────────────────
    // Canvas & UI inşası
    // ─────────────────────────────────────────────────────────────────

    private static GameObject BuildCanvas()
    {
        var go = new GameObject("LoadingScreen");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    private void BuildVisuals(Transform parent, Sprite sprite, LoadingHintEntry hint)
    {
        if (TryBuildPrefabView(parent, sprite, hint))
            return;

        BuildRuntimeFallback(parent, sprite, hint);
    }

    private bool TryBuildPrefabView(Transform parent, Sprite sprite, LoadingHintEntry hint)
    {
        LoadingHintView prefab = LoadingScreenPrefabProvider.LoadingHintViewPrefab;
        if (prefab == null)
            prefab = Resources.Load<LoadingHintView>(LoadingHintViewResourcePath);

        if (prefab == null)
            return false;

        LoadingHintView view = Instantiate(prefab, parent, false);
        view.name = "LoadingHintView";
        view.gameObject.SetActive(true);

        if (view.transform is RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        if (hint != null)
            view.Apply(hint);
        else
            view.Apply(sprite);

        return true;
    }

    private void BuildRuntimeFallback(Transform parent, Sprite sprite, LoadingHintEntry hint)
    {
        hintImage = BuildImage(parent, sprite);

        if (hint == null)
            return;

        titleText = BuildText(parent, "HintTitle", GameLocalization.Get(hint.titleLocalizationKey),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f),
            new Vector2(900f, 120f), 56, TextAlignmentOptions.Center);

        descriptionText = BuildText(parent, "HintDescription", GameLocalization.Get(hint.descriptionLocalizationKey),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -265f),
            new Vector2(880f, 92f), 34, TextAlignmentOptions.Center);

        string loadingKey = string.IsNullOrWhiteSpace(hint.loadingLocalizationKey)
            ? "loading_hint_loading"
            : hint.loadingLocalizationKey;

        loadingText = BuildText(parent, "LoadingText", GameLocalization.Get(loadingKey),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f),
            new Vector2(720f, 80f), 34, TextAlignmentOptions.Center);
    }

    private static Image BuildImage(Transform parent, Sprite sprite)
    {
        var go = new GameObject("BG");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.raycastTarget = true;

        if (sprite != null)
        {
            img.sprite          = sprite;
            img.color           = Color.white;
            img.type            = Image.Type.Simple;
            img.preserveAspect  = false;
        }
        else
        {
            img.sprite = null;
            img.color  = Color.black;
        }

        return img;
    }

    private static TMP_Text BuildText(Transform parent, string name, string value, Vector2 anchorMin, Vector2 anchorMax,
                                      Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize,
                                      TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = value ?? string.Empty;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(18f, fontSize * 0.55f);
        text.fontSizeMax = fontSize;
        text.alignment = alignment;
        text.color = new Color(1f, 0.96f, 0.78f, 1f);
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;

        text.outlineColor = new Color32(8, 79, 123, 255);
        text.outlineWidth = 0.22f;

        return text;
    }
}
