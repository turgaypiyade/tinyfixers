using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Booster (0=Hammer/Single, 1=Row, 2=Column, 3=Shuffle) kilit + free-oyun + hak kuralları —
/// TEK merkez. PreLevelSpecialPopup ile aynı desen:
///   - Booster kendi unlock level'ına kadar kilitli (8 / 13 / 18 / 24).
///   - Unlock SONRASI girilen İLK oyun o booster için FREE: kullanım hak düşürmez.
///     Free, oyuna girmekle tüketilir (kullanılmasa bile o oyuna özeldir).
///   - Unlock ile birlikte tek seferlik 3 hak hediye edilir (free oyun bunları harcamaz).
/// Oturum kavramı: her sahne yüklemesi yeni oyun oturumu sayılır; free bayrakları o oyun
/// boyunca RAM'de yaşar (ilk erişimde kurulur), prefs'e "kullanıldı" hemen yazılır.
/// </summary>
public static class BoosterAccessService
{
    private const int BoosterCount = 4;
    private static readonly int[] UnlockLevels = { 8, 13, 18, 24 };
    private const string LevelPrefsKey = "current_level";

    private static readonly bool[] sessionFree = new bool[BoosterCount];
    private static bool sessionInitialized;
    private static int sessionInitFrame = -1;
    private static bool sceneHookRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        if (sceneHookRegistered) return;
        sceneHookRegistered = true;
        SceneManager.sceneLoaded += (_, _) =>
        {
            // Sahne objelerinin OnEnable'ı sceneLoaded'dan ÖNCE (aynı karede) koşar.
            // O karede kurulmuş oturumu SIFIRLAMA — yoksa free bayrağı ilk karede
            // prefs'e "kullanıldı" yazılıp RAM'den siliniyordu (rozet var, hak yok).
            if (Time.frameCount != sessionInitFrame)
                sessionInitialized = false;
        };
    }

    public static int GetUnlockLevel(int boosterIndex)
        => boosterIndex >= 0 && boosterIndex < UnlockLevels.Length ? UnlockLevels[boosterIndex] : int.MaxValue;

    public static bool IsUnlocked(int boosterIndex)
        => PlayerPrefs.GetInt(LevelPrefsKey, 1) >= GetUnlockLevel(boosterIndex);

    /// <summary>
    /// Oyun oturumunu kurar (ilk çağrıda): unlock hediyesi (3 hak) + free-oyun bayrağı.
    /// Oyun sahnesindeki ilk erişimde (slot view / seçim / kullanım) otomatik çağrılır.
    /// </summary>
    public static void EnsureGameSession()
    {
        if (sessionInitialized) return;
        sessionInitialized = true;
        sessionInitFrame = Time.frameCount;

        bool dirty = false;
        for (int i = 0; i < BoosterCount; i++)
        {
            sessionFree[i] = false;
            if (!IsUnlocked(i)) continue;

            if (PlayerPrefs.GetInt(RewardKey(i), 0) == 0)
            {
                PlayerPrefs.SetInt(RewardKey(i), 1);
                BoosterInventory.Add(ToMode(i), 3);
                dirty = true;
            }

            if (PlayerPrefs.GetInt(FreeKey(i), 0) == 0)
            {
                sessionFree[i] = true;                 // bu oyun boyunca free
                PlayerPrefs.SetInt(FreeKey(i), 1);     // oyuna girmek free'yi tüketir (prelevel kuralı)
                dirty = true;
            }
        }

        if (dirty)
            PlayerPrefs.Save();
    }

    /// <summary>Bu oyunda booster free mi? (unlock sonrası ilk oyun)</summary>
    public static bool IsFreeThisGame(int boosterIndex)
    {
        EnsureGameSession();
        return boosterIndex >= 0 && boosterIndex < BoosterCount && sessionFree[boosterIndex];
    }

    /// <summary>Seçilebilir/kullanılabilir mi: kilitsiz VE (free oyun VEYA hak > 0).</summary>
    public static bool CanUse(int boosterIndex)
    {
        EnsureGameSession();
        if (!IsUnlocked(boosterIndex)) return false;
        return sessionFree[boosterIndex] || BoosterInventory.GetCount(ToMode(boosterIndex)) > 0;
    }

    /// <summary>Free kullanım tüketildiğinde (booster index'i ile) yayınlanır — UI rozeti kapatıp sayacı açar.</summary>
    public static event System.Action<int> OnFreeConsumed;

    /// <summary>
    /// Booster gerçekten ateşlendiğinde çağrılır. Free hakkı varsa ÖNCE o tüketilir
    /// (tek kullanımlık — rozet kalkar, sayaç görünür); yoksa 1 hak harcanır.
    /// </summary>
    public static void OnBoosterUsed(BoardController.BoosterMode mode)
    {
        EnsureGameSession();
        int index = ToIndex(mode);
        if (index < 0) return;

        if (sessionFree[index])
        {
            sessionFree[index] = false;
            OnFreeConsumed?.Invoke(index);
            return;
        }

        BoosterInventory.Spend(mode, 1);
    }

    public static BoardController.BoosterMode ToMode(int boosterIndex) => boosterIndex switch
    {
        0 => BoardController.BoosterMode.Single,
        1 => BoardController.BoosterMode.Row,
        2 => BoardController.BoosterMode.Column,
        3 => BoardController.BoosterMode.Shuffle,
        _ => BoardController.BoosterMode.None
    };

    public static int ToIndex(BoardController.BoosterMode mode) => mode switch
    {
        BoardController.BoosterMode.Single  => 0,
        BoardController.BoosterMode.Row     => 1,
        BoardController.BoosterMode.Column  => 2,
        BoardController.BoosterMode.Shuffle => 3,
        _ => -1
    };

    private static string RewardKey(int i) => $"booster_rewarded_{i}";
    private static string FreeKey(int i) => $"booster_free_used_{i}";
}
