using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10)]
public sealed class CoinFlyToWalletAnimator : MonoBehaviour
{
    public const string PendingRewardKey = "pending_coin_reward";
    public const string PendingBeforeKey = "pending_coin_before";
    public const string PendingAfterKey = "pending_coin_after";

    [Header("References")]
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private RectTransform target;
    [SerializeField] private Image coinPrefab;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private TMP_Text chipMoneyText;

    [Header("Motion")]
    [SerializeField, Min(1)] private int maxFlyingCoins = 12;
    [SerializeField, Min(0.05f)] private float duration = 0.65f;
    [SerializeField, Min(0f)] private float stagger = 0.05f;
    [SerializeField] private Vector2 startViewportPosition = new Vector2(0.78f, 0.5f);
    [SerializeField] private Vector2 randomStartOffset = new Vector2(44f, 80f);
    [SerializeField] private Vector2 randomControlOffset = new Vector2(34f, 24f);
    [SerializeField] private Vector2 cometControlOffset = new Vector2(45f, 30f);
    [SerializeField] private AnimationCurve moveCurve;
    [SerializeField] private AnimationCurve scaleCurve = new AnimationCurve(
        new Keyframe(0f, 2f),
        new Keyframe(1f, 0.6f));

    [Header("Target Punch")]
    [SerializeField, Min(1f)] private float targetPunchScale = 1.12f;
    [SerializeField, Min(0.01f)] private float targetPunchDuration = 0.16f;

    [Header("Text Tween")]
    [SerializeField] private bool formatChipMoneyN0;

    [Header("Audio Hooks")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip startSfx;
    [SerializeField] private AudioClip hitSfx;
    [SerializeField] private AudioClip finishedSfx;

    public event Action OnCoinFlyStarted;
    public event Action OnCoinFlyFinished;
    public event Action OnCoinHitTarget;

    private bool hasAttemptedPendingReward;
    private bool hasWarnedMissingText;
    private Coroutine targetPunchRoutine;
    private Vector3 targetBaseScale = Vector3.one;
    private bool hasTargetBaseScale;

    private void Awake()
    {
        ResolveReferences();
        RefreshWalletTextBeforePendingReward();
    }

    private void Start()
    {
        if (!hasAttemptedPendingReward)
            StartCoroutine(CoPlayPendingRewardAfterLayout());
    }

    private void OnDisable()
    {
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
        int walletCoins = PlayerWallet.Coins;
        pendingAfter = PlayerPrefs.GetInt(PendingAfterKey, walletCoins);
        pendingBefore = PlayerPrefs.GetInt(PendingBeforeKey, Mathf.Max(0, pendingAfter - Mathf.Max(0, pendingReward)));

        if (pendingReward > 0)
        {
            if (pendingAfter != walletCoins)
            {
                Debug.LogWarning(
                    $"[CoinFlyToWalletAnimator] Clearing stale pending coin reward. " +
                    $"pendingReward={pendingReward} pendingBefore={pendingBefore} pendingAfter={pendingAfter} wallet={walletCoins}");
                ClearPendingReward();
                pendingReward = 0;
                pendingBefore = walletCoins;
                pendingAfter = walletCoins;
                return false;
            }

            return true;
        }

        pendingBefore = walletCoins;
        pendingAfter = walletCoins;
        return false;
    }

    private IEnumerator CoPlayPendingRewardAfterLayout()
    {
        hasAttemptedPendingReward = true;

        yield return null;
        Canvas.ForceUpdateCanvases();

        if (!TryGetPendingReward(out int pendingReward, out int pendingBefore, out int pendingAfter))
        {
            RefreshWalletText(PlayerWallet.Coins);
            ClearPendingReward();
            yield break;
        }

        ResolveReferences();
        RefreshWalletText(pendingBefore);

        if (chipMoneyText == null)
        {
            Debug.LogWarning("[CoinFlyToWalletAnimator] ChipMoney text not found. Pending coin reward will be applied without text animation.");
            ClearPendingReward();
            yield break;
        }

        if (!CanPlayVisualCoins())
        {
            yield return StartCoroutine(CoPlayTextOnlyReward(pendingBefore, pendingAfter));
            ClearPendingReward();
            yield break;
        }

        yield return StartCoroutine(CoPlayCoinFly(pendingReward, pendingBefore, pendingAfter));
        ClearPendingReward();
    }

    private IEnumerator CoPlayCoinFly(int pendingReward, int pendingBefore, int pendingAfter)
    {
        int coinCount = Mathf.Clamp(Mathf.CeilToInt(pendingReward / 25f), 3, Mathf.Max(1, maxFlyingCoins));
        float totalDuration = duration + stagger * Mathf.Max(0, coinCount - 1);

        OnCoinFlyStarted?.Invoke();
        PlayOneShot(startSfx);

        Coroutine countRoutine = StartCoroutine(UiNumberTween.Tween(chipMoneyText, pendingBefore, pendingAfter, totalDuration, formatChipMoneyN0));

        int completedCoins = 0;
        for (int i = 0; i < coinCount; i++)
        {
            int coinIndex = i;
            StartCoroutine(CoFlyOneCoin(coinIndex, () => completedCoins++));
        }

        while (completedCoins < coinCount)
            yield return null;

        yield return countRoutine;

        RefreshWalletText(pendingAfter);
        OnCoinFlyFinished?.Invoke();
        PlayOneShot(finishedSfx);
    }

    private IEnumerator CoPlayTextOnlyReward(int pendingBefore, int pendingAfter)
    {
        OnCoinFlyStarted?.Invoke();
        PlayOneShot(startSfx);

        yield return StartCoroutine(UiNumberTween.Tween(chipMoneyText, pendingBefore, pendingAfter, duration, formatChipMoneyN0));

        RefreshWalletText(pendingAfter);
        OnCoinFlyFinished?.Invoke();
        PlayOneShot(finishedSfx);
    }

    private IEnumerator CoFlyOneCoin(int index, Action onComplete)
    {
        float delay = stagger * index;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Image coin = CreateCoinImage();
        if (coin == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        RectTransform coinRect = coin.rectTransform;
        Vector2 start = GetStartLocalPosition();
        Vector2 end = GetTargetLocalPosition();
        start += new Vector2(
            UnityEngine.Random.Range(-randomStartOffset.x, randomStartOffset.x),
            UnityEngine.Random.Range(-randomStartOffset.y, randomStartOffset.y));

        Vector2 control = GetCometControlPoint(start, end);

        coinRect.anchoredPosition = start;
        coinRect.localScale = Vector3.one * EvaluateScale(0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EvaluateTravel(t);

            coinRect.anchoredPosition = Quadratic(start, control, end, eased);
            coinRect.localScale = Vector3.one * EvaluateScale(t);

            yield return null;
        }

        coinRect.anchoredPosition = end;
        coinRect.localScale = Vector3.one * EvaluateScale(1f);

        OnCoinHitTarget?.Invoke();
        PlayOneShot(hitSfx);
        PlayTargetPunch();

        Destroy(coin.gameObject);
        onComplete?.Invoke();
    }

    private bool CanPlayVisualCoins()
    {
        ResolveReferences();
        return canvasRoot != null
               && target != null
               && (coinPrefab != null || ResolveCoinSprite() != null);
    }

    private Image CreateCoinImage()
    {
        ResolveReferences();

        if (canvasRoot == null)
            return null;

        Image image;
        if (coinPrefab != null)
        {
            image = Instantiate(coinPrefab, canvasRoot);
        }
        else
        {
            Sprite sprite = ResolveCoinSprite();
            if (sprite == null)
                return null;

            var go = new GameObject("CoinFlyGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup), typeof(Image));
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
            rect.sizeDelta = new Vector2(72f, 72f);

        image.raycastTarget = false;
        image.transform.SetAsLastSibling();
        return image;
    }

    private Sprite ResolveCoinSprite()
    {
        if (coinSprite != null)
            return coinSprite;

        if (target == null || target.parent == null)
            return null;

        Image[] images = target.parent.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.sprite == null)
                continue;

            string lowerName = image.name.ToLowerInvariant();
            if (lowerName.Contains("coin") || lowerName.Contains("chip") || lowerName.Contains("money") || lowerName.Contains("gold"))
            {
                coinSprite = image.sprite;
                return coinSprite;
            }
        }

        return null;
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
        if (chipMoneyText == null)
            chipMoneyText = MainMenuWalletDisplay.ResolveChipMoneyText(transform);

        if (target == null && chipMoneyText != null)
            target = chipMoneyText.rectTransform;

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

        if (chipMoneyText == null && !hasWarnedMissingText)
        {
            hasWarnedMissingText = true;
            Debug.LogWarning("[CoinFlyToWalletAnimator] ChipMoney text not found. Coin reward animation will be skipped.");
        }
    }

    private void RefreshWalletText(int amount)
    {
        ResolveReferences();
        UiNumberTween.SetValue(chipMoneyText, amount, formatChipMoneyN0);
    }

    private void RefreshWalletTextBeforePendingReward()
    {
        if (TryGetPendingReward(out _, out int pendingBefore, out _))
            RefreshWalletText(pendingBefore);
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
            : Mathf.Lerp(2f, 0.6f, t);
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

    private static void TryInstallOnScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "MainMenu")
            return;

        if (FindFirstObjectByType<CoinFlyToWalletAnimator>(FindObjectsInactive.Include) != null)
            return;

        var topBar = GameObject.Find("TopBar");
        var host = topBar != null ? topBar : GameObject.Find("MainRoot");
        if (host == null)
            host = new GameObject("CoinFlyToWalletAnimator");

        host.AddComponent<CoinFlyToWalletAnimator>();
    }
}
