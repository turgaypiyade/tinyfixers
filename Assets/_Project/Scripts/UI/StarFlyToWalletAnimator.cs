using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10)]
public sealed class StarFlyToWalletAnimator : MonoBehaviour
{
    public const string PendingRewardKey = "pending_star_reward";
    public const string PendingBeforeKey = "pending_star_before";
    public const string PendingAfterKey = "pending_star_after";

    [Header("References")]
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private RectTransform target;
    [SerializeField] private Image starPrefab;
    [SerializeField] private Sprite starSprite;
    [SerializeField] private TMP_Text starCountText;

    [Header("Motion")]
    [SerializeField, Min(1)] private int maxFlyingStars = 6;
    [SerializeField, Min(0.05f)] private float duration = 0.65f;
    [SerializeField, Min(0f)] private float stagger = 0.08f;
    [SerializeField] private Vector2 startViewportPosition = new Vector2(0.78f, 0.54f);
    [SerializeField] private Vector2 randomStartOffset = new Vector2(36f, 52f);
    [SerializeField] private Vector2 randomControlOffset = new Vector2(34f, 24f);
    [SerializeField] private Vector2 cometControlOffset = new Vector2(45f, 30f);
    [SerializeField] private AnimationCurve moveCurve;
    [SerializeField] private AnimationCurve scaleCurve = new AnimationCurve(
        new Keyframe(0f, 1.8f),
        new Keyframe(1f, 0.65f));

    [Header("Target Punch")]
    [SerializeField, Min(1f)] private float targetPunchScale = 1.12f;
    [SerializeField, Min(0.01f)] private float targetPunchDuration = 0.16f;

    [Header("Text Tween")]
    [SerializeField] private bool formatStarCountN0;

    [Header("Audio Hooks")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip startSfx;
    [SerializeField] private AudioClip hitSfx;
    [SerializeField] private AudioClip finishedSfx;

    public event Action OnStarFlyStarted;
    public event Action OnStarFlyFinished;
    public event Action OnStarHitTarget;

    private bool hasAttemptedPendingReward;
    private bool hasWarnedMissingText;
    private Coroutine targetPunchRoutine;
    private Vector3 targetBaseScale = Vector3.one;
    private bool hasTargetBaseScale;
    private static Texture2D generatedStarTexture;
    private static Sprite generatedStarSprite;

    private void Awake()
    {
        ResolveReferences();
        RefreshStarsForCurrentState();
    }

    private void OnEnable()
    {
        PlayerWallet.OnTotalStarsChanged += RefreshStars;
        ResolveReferences();
        RefreshStarsForCurrentState();
    }

    private void Start()
    {
        if (!hasAttemptedPendingReward)
            StartCoroutine(CoPlayPendingRewardAfterLayout());
    }

    private void OnDisable()
    {
        PlayerWallet.OnTotalStarsChanged -= RefreshStars;

        if (targetPunchRoutine != null)
        {
            StopCoroutine(targetPunchRoutine);
            targetPunchRoutine = null;
        }

        if (target != null && hasTargetBaseScale)
            target.localScale = targetBaseScale;
    }

    public static void ClearPendingReward()
    {
        PlayerPrefs.DeleteKey(PendingRewardKey);
        PlayerPrefs.DeleteKey(PendingBeforeKey);
        PlayerPrefs.DeleteKey(PendingAfterKey);
        PlayerPrefs.Save();
    }

    public static bool TryGetPendingReward(out int pendingReward, out int pendingBefore, out int pendingAfter)
    {
        pendingReward = PlayerPrefs.GetInt(PendingRewardKey, 0);
        pendingAfter = PlayerPrefs.GetInt(PendingAfterKey, PlayerWallet.TotalStars);
        pendingBefore = PlayerPrefs.GetInt(PendingBeforeKey, Mathf.Max(0, pendingAfter - Mathf.Max(0, pendingReward)));

        if (pendingReward > 0)
            return true;

        pendingBefore = PlayerWallet.TotalStars;
        pendingAfter = PlayerWallet.TotalStars;
        return false;
    }

    private IEnumerator CoPlayPendingRewardAfterLayout()
    {
        hasAttemptedPendingReward = true;

        yield return null;
        Canvas.ForceUpdateCanvases();

        if (!TryGetPendingReward(out int pendingReward, out int pendingBefore, out int pendingAfter))
        {
            SetStarsInstant(PlayerWallet.TotalStars);
            ClearPendingReward();
            yield break;
        }

        ResolveReferences();
        SetStarsInstant(pendingBefore);

        if (starCountText == null)
        {
            Debug.LogWarning("[StarFlyToWalletAnimator] StarCount text not found. Pending star reward will be skipped.");
            ClearPendingReward();
            yield break;
        }

        Debug.Log($"[StarFlyToWalletAnimator] Pending star reward found. reward={pendingReward}, before={pendingBefore}, after={pendingAfter}");

        if (!CanPlayVisualStars())
        {
            yield return StartCoroutine(CoPlayTextOnlyReward(pendingBefore, pendingAfter));
            ClearPendingReward();
            yield break;
        }

        yield return StartCoroutine(CoPlayStarFly(pendingReward, pendingBefore, pendingAfter));
        ClearPendingReward();
    }

    private IEnumerator CoPlayStarFly(int pendingReward, int pendingBefore, int pendingAfter)
    {
        int starCount = Mathf.Clamp(pendingReward, 1, Mathf.Max(1, maxFlyingStars));
        float totalDuration = duration + stagger * Mathf.Max(0, starCount - 1);

        OnStarFlyStarted?.Invoke();
        PlayOneShot(startSfx);

        Coroutine countRoutine = StartCoroutine(UiNumberTween.Tween(starCountText, pendingBefore, pendingAfter, totalDuration, formatStarCountN0));

        int completedStars = 0;
        for (int i = 0; i < starCount; i++)
        {
            int starIndex = i;
            StartCoroutine(CoFlyOneStar(starIndex, () => completedStars++));
        }

        while (completedStars < starCount)
            yield return null;

        yield return countRoutine;

        SetStarsInstant(pendingAfter);
        OnStarFlyFinished?.Invoke();
        PlayOneShot(finishedSfx);
    }

    private IEnumerator CoPlayTextOnlyReward(int pendingBefore, int pendingAfter)
    {
        OnStarFlyStarted?.Invoke();
        PlayOneShot(startSfx);

        yield return StartCoroutine(UiNumberTween.Tween(starCountText, pendingBefore, pendingAfter, duration, formatStarCountN0));

        SetStarsInstant(pendingAfter);
        OnStarFlyFinished?.Invoke();
        PlayOneShot(finishedSfx);
    }

    private IEnumerator CoFlyOneStar(int index, Action onComplete)
    {
        float delay = stagger * index;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Image star = CreateStarImage();
        if (star == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        RectTransform starRect = star.rectTransform;
        Vector2 start = GetStartLocalPosition();
        Vector2 end = GetTargetLocalPosition();
        start += new Vector2(
            UnityEngine.Random.Range(-randomStartOffset.x, randomStartOffset.x),
            UnityEngine.Random.Range(-randomStartOffset.y, randomStartOffset.y));

        Vector2 control = GetCometControlPoint(start, end);

        starRect.anchoredPosition = start;
        starRect.localScale = Vector3.one * EvaluateScale(0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EvaluateTravel(t);

            starRect.anchoredPosition = Quadratic(start, control, end, eased);
            starRect.localScale = Vector3.one * EvaluateScale(t);
            yield return null;
        }

        starRect.anchoredPosition = end;
        starRect.localScale = Vector3.one * EvaluateScale(1f);

        OnStarHitTarget?.Invoke();
        PlayOneShot(hitSfx);
        PlayTargetPunch();

        Destroy(star.gameObject);
        onComplete?.Invoke();
    }

    private bool CanPlayVisualStars()
    {
        ResolveReferences();
        return canvasRoot != null
               && target != null
               && (starPrefab != null || ResolveStarSprite() != null);
    }

    private Image CreateStarImage()
    {
        ResolveReferences();

        if (canvasRoot == null)
            return null;

        Image image;
        if (starPrefab != null)
        {
            image = Instantiate(starPrefab, canvasRoot);
        }
        else
        {
            Sprite sprite = ResolveStarSprite();
            if (sprite == null)
                return null;

            var go = new GameObject("StarFlyGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(canvasRoot, false);
            image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        RectTransform rect = image.rectTransform;
        rect.SetParent(canvasRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        if (rect.sizeDelta.x <= 0f || rect.sizeDelta.y <= 0f)
            rect.sizeDelta = new Vector2(64f, 64f);

        image.raycastTarget = false;
        image.transform.SetAsLastSibling();
        return image;
    }

    private Sprite ResolveStarSprite()
    {
        if (starSprite != null)
            return starSprite;

        Transform searchRoot = target != null && target.parent != null ? target.parent : transform.root;
        if (searchRoot == null)
            return null;

        Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.sprite == null)
                continue;

            if (image.name.ToLowerInvariant().Contains("star"))
            {
                starSprite = image.sprite;
                return starSprite;
            }
        }

        starSprite = GetGeneratedStarSprite();
        return starSprite;
    }

    private static Sprite GetGeneratedStarSprite()
    {
        if (generatedStarSprite != null)
            return generatedStarSprite;

        const int size = 64;
        var vertices = new Vector2[10];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        const float outerRadius = 28f;
        const float innerRadius = 12f;

        for (int i = 0; i < vertices.Length; i++)
        {
            float angle = (-90f + i * 36f) * Mathf.Deg2Rad;
            float radius = i % 2 == 0 ? outerRadius : innerRadius;
            vertices[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        generatedStarTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                if (IsPointInPolygon(point, vertices))
                {
                    float distance = Vector2.Distance(point, center) / outerRadius;
                    float shade = Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(distance));
                    generatedStarTexture.SetPixel(x, y, new Color(1f, 0.82f * shade, 0.18f * shade, 1f));
                }
                else
                {
                    generatedStarTexture.SetPixel(x, y, Color.clear);
                }
            }
        }

        generatedStarTexture.Apply();
        generatedStarSprite = Sprite.Create(
            generatedStarTexture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: size);

        return generatedStarSprite;
    }

    private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            bool yIntersects = polygon[i].y > point.y != polygon[j].y > point.y;
            if (!yIntersects)
                continue;

            float xIntersect = (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
                (polygon[j].y - polygon[i].y) + polygon[i].x;
            if (point.x < xIntersect)
                inside = !inside;
        }

        return inside;
    }

    private Vector2 GetStartLocalPosition()
    {
        if (canvasRoot == null)
            return Vector2.zero;

        Rect rect = canvasRoot.rect;
        return new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, Mathf.Clamp01(startViewportPosition.x)),
            Mathf.Lerp(rect.yMin, rect.yMax, Mathf.Clamp01(startViewportPosition.y)));
    }

    private Vector2 GetCometControlPoint(Vector2 start, Vector2 end)
    {
        float horizontalDirection = end.x >= start.x ? 1f : -1f;
        Vector2 baseOffset = new Vector2(cometControlOffset.x * horizontalDirection, cometControlOffset.y);
        Vector2 randomOffset = new Vector2(
            UnityEngine.Random.Range(-randomControlOffset.x, randomControlOffset.x),
            UnityEngine.Random.Range(-randomControlOffset.y, randomControlOffset.y));

        return (start + end) * 0.5f + baseOffset + randomOffset;
    }

    private Vector2 GetTargetLocalPosition()
    {
        if (canvasRoot == null || target == null)
            return Vector2.zero;

        Canvas canvas = canvasRoot.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector3 worldCenter = target.TransformPoint(target.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screenPoint, camera, out Vector2 localPoint);
        return localPoint;
    }

    private void ResolveReferences()
    {
        if (starCountText == null)
            starCountText = MainMenuStarDisplay.ResolveStarCountText(transform);

        if (target == null && starCountText != null)
            target = starCountText.rectTransform;

        if (target != null && !hasTargetBaseScale)
        {
            targetBaseScale = target.localScale;
            hasTargetBaseScale = true;
        }

        if (canvasRoot == null)
        {
            Canvas canvas = target != null
                ? target.GetComponentInParent<Canvas>()
                : FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

            if (canvas != null)
                canvasRoot = canvas.transform as RectTransform;
        }

        if (starCountText == null && !hasWarnedMissingText)
        {
            hasWarnedMissingText = true;
            Debug.LogWarning("[StarFlyToWalletAnimator] StarCount text not found. Star display will be skipped.");
        }
    }

    private void RefreshStars(int amount)
    {
        if (TryGetPendingReward(out _, out int pendingBefore, out _))
            amount = pendingBefore;

        SetStarsInstant(amount);
    }

    private void SetStarsInstant(int amount)
    {
        ResolveReferences();
        UiNumberTween.SetValue(starCountText, amount, formatStarCountN0);
    }

    private void RefreshStarsForCurrentState()
    {
        RefreshStars(PlayerWallet.TotalStars);
    }

    private void PlayTargetPunch()
    {
        if (target == null)
            return;

        if (targetPunchRoutine != null)
            StopCoroutine(targetPunchRoutine);

        targetPunchRoutine = StartCoroutine(CoPunchTarget());
    }

    private IEnumerator CoPunchTarget()
    {
        RectTransform punchTarget = target;
        if (punchTarget == null)
            yield break;

        Vector3 baseScale = hasTargetBaseScale ? targetBaseScale : punchTarget.localScale;
        float halfDuration = Mathf.Max(0.01f, targetPunchDuration * 0.5f);

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            punchTarget.localScale = Vector3.LerpUnclamped(baseScale, baseScale * targetPunchScale, UiNumberTween.EaseOut(t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            punchTarget.localScale = Vector3.LerpUnclamped(baseScale * targetPunchScale, baseScale, UiNumberTween.EaseOut(t));
            yield return null;
        }

        punchTarget.localScale = baseScale;
        targetPunchRoutine = null;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (!GameSettings.SoundEnabled) return;
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private float EvaluateTravel(float t)
    {
        t = Mathf.Clamp01(t);
        return moveCurve != null && moveCurve.length > 0 ? Mathf.Clamp01(moveCurve.Evaluate(t)) : UiNumberTween.EaseOut(t);
    }

    private float EvaluateScale(float t)
    {
        t = Mathf.Clamp01(t);
        return scaleCurve != null && scaleCurve.length > 0
            ? Mathf.Max(0f, scaleCurve.Evaluate(t))
            : Mathf.Lerp(1.8f, 0.65f, t);
    }

    private static Vector2 Quadratic(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float inv = 1f - t;
        return inv * inv * a + 2f * inv * t * b + t * t * c;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstallOnActiveScene()
    {
        TryInstallOnScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstallOnScene(scene);
    }

    public static void EnsureInstalledOnMainMenu(Scene scene)
    {
        TryInstallOnScene(scene);
    }

    private static void TryInstallOnScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "MainMenu")
            return;

        var existing = FindFirstObjectByType<StarFlyToWalletAnimator>(FindObjectsInactive.Include);
        if (existing != null)
        {
            if (!existing.enabled)
                existing.enabled = true;
            return;
        }

        var topBar = GameObject.Find("TopBar");
        var host = topBar != null ? topBar : GameObject.Find("MainRoot");
        if (host == null)
            host = new GameObject("StarFlyToWalletAnimator");

        host.AddComponent<StarFlyToWalletAnimator>();
    }
}
