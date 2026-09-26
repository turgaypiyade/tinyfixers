using UnityEditor;
using UnityEngine;

// Safari/Yükseliş kazanma ekranını Play modunda ödül vermeden açar (tasarım önizlemesi).
public static class SafariDebugMenu
{
    [MenuItem("TinyFixers/Debug/Safari Kazanma Ekranını Göster (Play)")]
    private static void PreviewWinScreen()
    {
        var screen = Object.FindFirstObjectByType<RisingMapScreen>(FindObjectsInactive.Include);
        if (screen == null)
        {
            Debug.LogWarning("[SafariDebug] Sahnede RisingMapScreen yok — MainMenu sahnesinde Play modunda çalıştır.");
            return;
        }
        screen.PreviewFinalReward();
    }

    [MenuItem("TinyFixers/Debug/Safari Kazanma Ekranını Göster (Play)", true)]
    private static bool PreviewWinScreenValidate() => Application.isPlaying;
}
