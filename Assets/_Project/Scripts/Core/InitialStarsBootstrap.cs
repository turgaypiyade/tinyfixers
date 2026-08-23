using UnityEngine;

/// <summary>
/// Oyunu ilk defa açan kullanıcıya başlangıç yıldızı verir.
/// Oyuncu bu yıldızlarla ilk harikanın (mission) görevlerini açmaya başlar.
///
/// PlayerPrefs flag: "initial_stars_granted"
/// - 0 (veya yok) → henüz verilmemiş, ilk açılış, başlangıç yıldızı ver
/// - 1           → zaten verilmiş, atla
///
/// TestLevelProgressionBootstrap bu flag'i her launch'ta siler → her test'te tekrar verilir.
/// Production'da TestLevelProgressionBootstrap kaldırıldığında flag tek sefer çalışır.
/// </summary>
public static class InitialStarsBootstrap
{
    private const string KeyInitialStarsGranted = "initial_stars_granted";
    private const int InitialStarAmount         = 100;

    // AfterSceneLoad → TestLevelProgressionBootstrap'in (BeforeSceneLoad) flag silmesinden SONRA çalışır.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GrantInitialStarsIfFirstLaunch()
    {
        if (PlayerPrefs.GetInt(KeyInitialStarsGranted, 0) == 1)
            return;

        // PlayerWallet.AddStars → PlayerPrefs günceller + OnTotalStarsChanged event fire eder (UI'lar güncellenir).
        PlayerWallet.AddStars(InitialStarAmount);
        PlayerPrefs.SetInt(KeyInitialStarsGranted, 1);
        PlayerPrefs.Save();

        Debug.Log($"[InitialStarsBootstrap] First launch — granted {InitialStarAmount} stars. Total: {PlayerWallet.TotalStars}");
    }
}
