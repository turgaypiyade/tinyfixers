using UnityEditor;
using UnityEngine;

// Pençe Makinesi editör testleri (yalnız Play modunda). Oyuncu build'ini etkilemez.
public static class ClawMachineDebugMenu
{
    private const string Root = "TinyFixers/Debug/Pençe Makinesi/";

    [MenuItem(Root + "Etkinliği Zorla Aç (debugForceAvailable)")]
    private static void ForceOpen()
    {
        var cfg = ClawMachineConfig.Shared;
        if (cfg == null) { Debug.LogWarning("[Claw] ClawMachineConfig bulunamadı."); return; }
        cfg.debugForceAvailable = !cfg.debugForceAvailable;
        Debug.Log("[Claw] debugForceAvailable = " + cfg.debugForceAvailable + " (asset'e kaydedilmez; Play bitince Inspector'dan kapat).");
        ClawMachineState.Sync(cfg, System.DateTime.UtcNow);
    }

    [MenuItem(Root + "+5 Jeton")]
    private static void AddTokens() => ClawMachineState.DebugAddTokens(5);

    [MenuItem(Root + "Durumu Sıfırla")]
    private static void ResetState() => ClawMachineState.DebugReset();

    [MenuItem(Root + "Etkinliği Zorla Aç (debugForceAvailable)", true)]
    [MenuItem(Root + "+5 Jeton", true)]
    [MenuItem(Root + "Durumu Sıfırla", true)]
    private static bool Validate() => Application.isPlaying;
}
