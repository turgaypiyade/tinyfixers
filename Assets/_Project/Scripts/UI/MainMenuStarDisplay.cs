using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-20)]
public sealed class MainMenuStarDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text starCountText;
    [SerializeField] private bool formatStarCountN0;

    private bool hasWarnedMissingText;

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

    private void OnDisable()
    {
        PlayerWallet.OnTotalStarsChanged -= RefreshStars;
    }

    public TMP_Text StarCountText
    {
        get
        {
            ResolveReferences();
            return starCountText;
        }
    }

    public void SetStarsInstant(int amount)
    {
        ResolveReferences();
        UiNumberTween.SetValue(starCountText, amount, formatStarCountN0);
    }

    private void RefreshStars(int amount)
    {
        if (StarFlyToWalletAnimator.TryGetPendingReward(out _, out int pendingBefore, out _))
        {
            SetStarsInstant(pendingBefore);
            return;
        }

        SetStarsInstant(amount);
    }

    private void RefreshStarsForCurrentState()
    {
        RefreshStars(PlayerWallet.TotalStars);
    }

    private void ResolveReferences()
    {
        if (starCountText != null)
            return;

        starCountText = ResolveStarCountText(transform);

        if (starCountText == null && !hasWarnedMissingText)
        {
            hasWarnedMissingText = true;
            Debug.LogWarning("[MainMenuStarDisplay] StarCount text not found. Star display will be skipped.");
        }
    }

    public static TMP_Text ResolveStarCountText(Transform searchRoot)
    {
        TMP_Text text = null;

        if (searchRoot != null)
        {
            text = FindTextAtPath(searchRoot, "MainMenuRoot/TopBar/StarCount")
                   ?? FindTextAtPath(searchRoot, "TopBar/StarCount")
                   ?? FindTextAtPath(searchRoot.root, "MainMenuRoot/TopBar/StarCount")
                   ?? FindTextAtPath(searchRoot.root, "TopBar/StarCount");
        }

        if (text != null)
            return text;

        var topBar = GameObject.Find("TopBar");
        if (topBar != null)
        {
            text = FindTextAtPath(topBar.transform, "StarCount");
            if (text != null)
                return text;
        }

        var starCount = GameObject.Find("StarCount");
        return starCount != null ? starCount.GetComponent<TMP_Text>() : null;
    }

    private static TMP_Text FindTextAtPath(Transform root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path))
            return null;

        var child = root.Find(path);
        return child != null ? child.GetComponent<TMP_Text>() : null;
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

        StarFlyToWalletAnimator.EnsureInstalledOnMainMenu(scene);

        if (FindFirstObjectByType<MainMenuStarDisplay>(FindObjectsInactive.Include) != null)
            return;

        var topBar = GameObject.Find("TopBar");
        var host = topBar != null ? topBar : GameObject.Find("MainRoot");
        if (host == null)
            host = new GameObject("MainMenuStarDisplay");

        host.AddComponent<MainMenuStarDisplay>();
    }
}
