using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Bir level'e girerken default loading screen yerine ÖZEL intro 'load' gösterir:
/// iki tam-ekran parça soldan/sağdan gelip ortada birleşir. Intro, kalıcı (DontDestroyOnLoad)
/// bir katmanda çalışır ve hedef sahne ASENKRON yüklenir — yani board hiç görünmeden, sahne
/// yüklenirken intro oynar; intro bitip sahne hazır olunca aktive edilip fade-out yapılır.
///
/// Kullanım (sahne yükleme bu manager'a aittir, ayrıca LoadScene ÇAĞIRMA):
///   CustomIntroLoadingManager.Show(leftSprite, rightSprite, "01_Game", slideIn, hold);
/// </summary>
public sealed class CustomIntroLoadingManager : MonoBehaviour
{
    private static CustomIntroLoadingManager _instance;

    public static bool IsVisible => _instance != null;

    private CanvasGroup   _canvasGroup;
    private RectTransform _canvasRect;
    private RectTransform _leftPiece;
    private RectTransform _rightPiece;

    private string _sceneName;
    private float  _slideInDuration;
    private float  _holdDuration;
    private float  _fadeOutDuration;
    private float  _offscreenMargin;

    public static void Show(Sprite left, Sprite right, string sceneName,
                            float slideInDuration = 0.6f, float holdDuration = 0.8f,
                            float fadeOutDuration = 0.35f, float offscreenMargin = 150f,
                            Color? backdrop = null)
    {
        if (left == null || right == null || string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[CustomIntroLoading] Eksik sprite/scene — default akışa düşülmeli.");
            return;
        }

        if (_instance != null)
            Destroy(_instance.gameObject);

        var root = BuildCanvas();
        var mgr  = root.AddComponent<CustomIntroLoadingManager>();

        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha          = 1f;
        cg.blocksRaycasts = true;   // alttaki sahnenin input'unu da bloklar
        cg.interactable   = false;

        mgr._canvasGroup     = cg;
        mgr._canvasRect      = (RectTransform)root.transform;
        mgr._sceneName       = sceneName;
        mgr._slideInDuration = Mathf.Max(0.05f, slideInDuration);
        mgr._holdDuration    = Mathf.Max(0f, holdDuration);
        mgr._fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        mgr._offscreenMargin = Mathf.Max(0f, offscreenMargin);

        // Backdrop (opak) — slide sırasında parçalar arasındaki boşlukta board görünmesin.
        BuildBackdrop(root.transform, backdrop ?? Color.black);
        mgr._leftPiece  = BuildPiece(root.transform, "IntroLeft",  left);
        mgr._rightPiece = BuildPiece(root.transform, "IntroRight", right);

        _instance = mgr;
        DontDestroyOnLoad(root);

        mgr.StartCoroutine(mgr.PlayAndLoad());
    }

    private IEnumerator PlayAndLoad()
    {
        // İlk kare render olmadan parçaları ekran dışına al (layout daha hesaplanmamış olabilir,
        // bu yüzden geçici büyük sabit offset — birleşmiş görüntünün bir kare flaş etmesini önler).
        _leftPiece.anchoredPosition  = new Vector2(-10000f, 0f);
        _rightPiece.anchoredPosition = new Vector2( 10000f, 0f);

        // Sahneyi arkada yüklemeye başla ama biz hazır deyene kadar aktive etme.
        AsyncOperation op = SceneManager.LoadSceneAsync(_sceneName);
        op.allowSceneActivation = false;

        // Bir kare bekle ki canvas/parça boyutları otursun, sonra hassas başlangıcı hesapla.
        yield return null;

        float canvasW = _canvasRect.rect.width;  if (canvasW <= 1f) canvasW = Screen.width;
        float leftW   = _leftPiece.rect.width;    if (leftW   <= 1f) leftW   = canvasW;
        float rightW  = _rightPiece.rect.width;   if (rightW  <= 1f) rightW  = canvasW;

        // Birleşme hedefi = tam ekran, offset 0 → anchoredPos 0.
        Vector2 leftMeet  = Vector2.zero;
        Vector2 rightMeet = Vector2.zero;
        float travel = canvasW * 0.5f + _offscreenMargin;
        Vector2 leftStart  = leftMeet  + new Vector2(-(travel + leftW),  0f);
        Vector2 rightStart = rightMeet + new Vector2( (travel + rightW), 0f);

        _leftPiece.anchoredPosition  = leftStart;
        _rightPiece.anchoredPosition = rightStart;

        // Kayarak ortada birleş (ease-out quad).
        float t = 0f;
        while (t < _slideInDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / _slideInDuration);
            float e = 1f - (1f - k) * (1f - k);
            _leftPiece.anchoredPosition  = Vector2.LerpUnclamped(leftStart,  leftMeet,  e);
            _rightPiece.anchoredPosition = Vector2.LerpUnclamped(rightStart, rightMeet, e);
            yield return null;
        }
        _leftPiece.anchoredPosition  = leftMeet;
        _rightPiece.anchoredPosition = rightMeet;

        // Birleşmiş halde bekle.
        if (_holdDuration > 0f)
            yield return new WaitForSecondsRealtime(_holdDuration);

        // Sahne yüklenene kadar bekle (allowSceneActivation=false iken progress 0.9'da takılır).
        while (op.progress < 0.9f)
            yield return null;

        // Aktive et ve gerçekten geçene kadar bekle.
        op.allowSceneActivation = true;
        while (!op.isDone)
            yield return null;

        // Yeni sahnenin ilk karesi otursun, sonra fade-out → oyun.
        yield return null;

        float f = 0f;
        while (f < _fadeOutDuration)
        {
            f += Time.unscaledDeltaTime;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f - Mathf.Clamp01(f / _fadeOutDuration);
            yield return null;
        }

        _instance = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── UI inşası ────────────────────────────────────────────────────────────

    private static GameObject BuildCanvas()
    {
        var go = new GameObject("CustomIntroLoading");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // normal loading screen'in (999) de üstünde

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    private static void BuildBackdrop(Transform parent, Color color)
    {
        var go = new GameObject("Backdrop");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        Stretch(rt);

        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = true;
    }

    private static RectTransform BuildPiece(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        Stretch(rt);

        var img = go.AddComponent<Image>();
        img.sprite         = sprite;
        img.color          = Color.white;
        img.preserveAspect = false;
        img.raycastTarget  = false;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
