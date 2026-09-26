using UnityEditor;
using UnityEngine;

// Can-0 akışlarını (ana menü "Canın Bitti" teklifi) test etmek için Play modunda canları sıfırlar.
public static class LivesDebugMenu
{
    [MenuItem("TinyFixers/Debug/Canları Sıfırla (Play)")]
    private static void ZeroLives()
    {
        int guard = 100;
        while (LivesManager.Current > 0 && guard-- > 0)
        {
            if (!LivesManager.SpendLife())
                break;
        }

        if (TimedRewardService.IsLivesFree())
            Debug.LogWarning("[LivesDebug] Sonsuz can (timed reward) aktif — teklif popup'ı bu sürede açılmaz.");
        Debug.Log($"[LivesDebug] Can = {LivesManager.Current}");
    }

    [MenuItem("TinyFixers/Debug/Canları Sıfırla (Play)", true)]
    private static bool ZeroLivesValidate() => Application.isPlaying;
}
