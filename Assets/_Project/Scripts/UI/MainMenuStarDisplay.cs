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
            Debug.LogWarning("[MainMenuStarDisplay] Star count text not found. Checked StarCount, ChipStar, Stars, StarText and TotalStars paths. Star display will be skipped until a TMP text is assigned or renamed.");
        }
    }

    public static TMP_Text ResolveStarCountText(Transform searchRoot)
    {
        TMP_Text text = null;

        if (searchRoot != null)
        {
            text = FindTextAtPath(searchRoot, "MainRoot/TopBar/StarCount")
                   ?? FindTextAtPath(searchRoot, "TopBar/StarCount")
                   ?? FindTextAtPath(searchRoot.root, "MainRoot/TopBar/StarCount")
                   ?? FindTextAtPath(searchRoot.root, "TopBar/StarCount");
        }

        if (text != null)
            return text;

        var topBar = GameObject.Find("TopBar");
        if (topBar != null)
        {
            text = FindStarTextNear(topBar.transform)
                   ?? FindTextAtAnyPath(topBar.transform, TopBarRelativeStarTextPaths);
            if (text != null)
                return text;
        }

        foreach (string objectName in StarObjectNames)
        {
            var go = GameObject.Find(objectName);
            text = GetTextOnOrUnder(go != null ? go.transform : null);
            if (text != null)
                return text;
        }

        return null;
    }

    private static readonly string[] StarTextPaths =
    {
        "MainRoot/TopBar/StarCount",
        "MainRoot/TopBar/ChipStar",
        "MainRoot/TopBar/ChipStar/Text",
        "MainRoot/TopBar/ChipStar/StarText",
        "MainRoot/TopBar/ChipStar/StarCount",
        "MainRoot/TopBar/Stars",
        "MainRoot/TopBar/Stars/Text",
        "MainRoot/TopBar/StarText",
        "MainRoot/TopBar/TotalStars",
        "TopBar/StarCount",
        "TopBar/ChipStar",
        "TopBar/ChipStar/Text",
        "TopBar/ChipStar/StarText",
        "TopBar/ChipStar/StarCount",
        "TopBar/Stars",
        "TopBar/Stars/Text",
        "TopBar/StarText",
        "TopBar/TotalStars"
    };

    private static readonly string[] TopBarRelativeStarTextPaths =
    {
        "StarCount",
        "ChipStar",
        "ChipStar/Text",
        "ChipStar/StarText",
        "ChipStar/StarCount",
        "Stars",
        "Stars/Text",
        "StarText",
        "TotalStars"
    };

    private static readonly string[] StarObjectNames =
    {
        "StarCount",
        "ChipStar",
        "Stars",
        "StarText",
        "TotalStars"
    };

    private static TMP_Text FindTextAtAnyPath(Transform root, string[] paths)
    {
        if (root == null || paths == null)
            return null;

        for (int i = 0; i < paths.Length; i++)
        {
            TMP_Text text = FindTextAtPath(root, paths[i]);
            if (text != null)
                return text;
        }

        return null;
    }

    private static TMP_Text FindTextAtPath(Transform root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path))
            return null;

        var child = root.Find(path);
        return GetTextOnOrUnder(child);
    }

    private static TMP_Text GetTextOnOrUnder(Transform transform)
    {
        if (transform == null)
            return null;

        TMP_Text text = transform.GetComponent<TMP_Text>();
        if (text != null)
            return text;

        return transform.GetComponentInChildren<TMP_Text>(true);
    }

    private static TMP_Text FindStarTextNear(Transform root)
    {
        if (root == null)
            return null;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            string objectName = text.gameObject.name.ToLowerInvariant();
            string parentName = text.transform.parent != null
                ? text.transform.parent.name.ToLowerInvariant()
                : string.Empty;

            if (objectName.Contains("star") || parentName.Contains("star"))
                return text;
        }

        return null;
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
