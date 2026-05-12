using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Sahneler arası geçişte tam ekran yükleme görseli gösterir.
/// DontDestroyOnLoad ile sahneler arası yaşar; yeni sahne yüklenince fade-out yapar.
///
/// Kullanım:
///   LoadingScreenManager.Show(sprite);   // LoadScene() çağrısından ÖNCE
///   SceneManager.LoadScene("SahnAdı");
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    private static LoadingScreenManager _instance;

    private CanvasGroup _canvasGroup;
    private float _fadeOutDuration    = 0.35f;
    private float _fadeOutDelay       = 0.08f;
    private float _minimumDisplayTime = 2.0f;
    private float _showTime;

    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tam ekran yükleme ekranını gösterir.
    /// sprite null ise düz siyah ekran gösterilir.
    /// Yeni sahne yüklendiğinde otomatik olarak fade-out yapılır.
    /// </summary>
    public static void Show(Sprite sprite, float fadeOutDuration = 0.35f, float fadeOutDelay = 0.08f,
                            float minimumDisplayTime = 2.0f)
    {
        if (_instance != null)
            Destroy(_instance.gameObject);

        var root = BuildCanvas();
        BuildImage(root.transform, sprite);

        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = false;

        var mgr = root.AddComponent<LoadingScreenManager>();
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
    // Canvas & Image inşası
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

    private static void BuildImage(Transform parent, Sprite sprite)
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
    }
}
