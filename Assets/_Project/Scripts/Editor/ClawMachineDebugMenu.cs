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

    [MenuItem(Root + "Koleksiyonu Son Peluşa Kadar Doldur")]
    private static void FillToLast() => ClawMachineState.DebugFillToLastPlush();

    [MenuItem(Root + "Makineyi Şimdi Aç (kapalı gün + mola atla)")]
    private static void OpenNow() => ClawMachineState.DebugOpenNow();

    [MenuItem(Root + "Etkinliği Zorla Aç (debugForceAvailable)", true)]
    [MenuItem(Root + "+5 Jeton", true)]
    [MenuItem(Root + "Durumu Sıfırla", true)]
    [MenuItem(Root + "Koleksiyonu Son Peluşa Kadar Doldur", true)]
    [MenuItem(Root + "Makineyi Şimdi Aç (kapalı gün + mola atla)", true)]
    private static bool Validate() => Application.isPlaying;
}
