using UnityEngine;

/// <summary>
/// Sahne geçişlerinde yaşayan singleton.
/// LivesManager regen timer'ını tıklar ve ilk açılış ödüllerini verir.
///
/// Kullanım:
///   LivesTimerService.InitialCoins = 500; // opsiyonel, default 500
///   LivesTimerService.EnsureExists();
/// </summary>
public class LivesTimerService : MonoBehaviour
{
    private static LivesTimerService _instance;

    /// <summary>İlk açılışta verilecek altın miktarı. EnsureExists() öncesi set et.</summary>
    public static int InitialCoins { get; set; } = 500;

    private float _elapsed;

    private const string KeyFirstLaunch = "first_launch_done";

    // ── Singleton ─────────────────────────────────────────────────────────────

    public static void EnsureExists()
    {
        if (_instance != null) return;
        var go = new GameObject("[LivesTimerService]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LivesTimerService>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        LivesManager.Initialize();
        GrantFirstLaunchRewards();
    }

    // ── İlk Açılış ───────────────────────────────────────────────────────────

    private static void GrantFirstLaunchRewards()
    {
        if (PlayerPrefs.GetInt(KeyFirstLaunch, 0) != 0) return;

        PlayerPrefs.SetInt(KeyFirstLaunch, 1);
        PlayerPrefs.Save();

        PlayerWallet.AddCoins(InitialCoins);
        Debug.Log($"[LivesTimerService] İlk açılış — {InitialCoins} altın verildi.");
    }

    // ── Timer ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed < 1f) return;
        _elapsed = 0f;
        LivesManager.TickRegen();
    }
}
