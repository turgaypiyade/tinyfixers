using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-20)]
public sealed class MainMenuWalletDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text chipMoneyText;

    private bool hasWarnedMissingText;

    private void Awake()
    {
        ResolveReferences();
        RefreshCoinsForCurrentState();
    }

    private void OnEnable()
    {
        PlayerWallet.OnCoinsChanged += RefreshCoins;
        ResolveReferences();
        RefreshCoinsForCurrentState();
    }

    private void OnDisable()
    {
        PlayerWallet.OnCoinsChanged -= RefreshCoins;
    }

    public TMP_Text ChipMoneyText
    {
        get
        {
            ResolveReferences();
            return chipMoneyText;
        }
    }

    public void SetCoinsInstant(int amount)
    {
        ResolveReferences();
        if (chipMoneyText != null)
            chipMoneyText.text = amount.ToString();
    }

    private void RefreshCoins(int amount)
    {
        if (CoinFlyToWalletAnimator.TryGetPendingReward(out _, out int pendingBefore, out _))
        {
            SetCoinsInstant(pendingBefore);
            return;
        }

        SetCoinsInstant(amount);
    }

    private void RefreshCoinsForCurrentState()
    {
        RefreshCoins(PlayerWallet.Coins);
    }

    private void ResolveReferences()
    {
        if (chipMoneyText != null)
            return;

        chipMoneyText = ResolveChipMoneyText(transform);

        if (chipMoneyText == null && !hasWarnedMissingText)
        {
            hasWarnedMissingText = true;
            Debug.LogWarning("[MainMenuWalletDisplay] ChipMoney text not found. Wallet display will be skipped.");
        }
    }

    public static TMP_Text ResolveChipMoneyText(Transform searchRoot)
    {
        TMP_Text text = null;

        if (searchRoot != null)
        {
            text = FindTextAtPath(searchRoot, "MainRoot/TopBar/ChipMoney")
                   ?? FindTextAtPath(searchRoot, "TopBar/ChipMoney")
                   ?? FindTextAtPath(searchRoot.root, "MainRoot/TopBar/ChipMoney")
                   ?? FindTextAtPath(searchRoot.root, "TopBar/ChipMoney");
        }

        if (text != null)
            return text;

        var topBar = GameObject.Find("TopBar");
        if (topBar != null)
        {
            text = FindTextAtPath(topBar.transform, "ChipMoney");
            if (text != null)
                return text;
        }

        var chipMoney = GameObject.Find("ChipMoney");
        return chipMoney != null ? chipMoney.GetComponent<TMP_Text>() : null;
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

        if (FindFirstObjectByType<MainMenuWalletDisplay>(FindObjectsInactive.Include) != null)
            return;

        var topBar = GameObject.Find("TopBar");
        var host = topBar != null ? topBar : GameObject.Find("MainRoot");
        if (host == null)
            host = new GameObject("MainMenuWalletDisplay");

        host.AddComponent<MainMenuWalletDisplay>();
    }
}
