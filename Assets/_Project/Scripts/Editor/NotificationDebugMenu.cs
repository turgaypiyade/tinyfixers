using UnityEditor;
using UnityEngine;

// Yerel bildirim planını editörde görmek / izin ön-popup'ını yeniden test etmek için.
public static class NotificationDebugMenu
{
    [MenuItem("TinyFixers/Debug/Bildirim Planını Logla (Play)")]
    private static void LogPlan() => Debug.Log(LocalNotificationService.DescribePlan());

    [MenuItem("TinyFixers/Debug/Bildirim Planını Logla (Play)", true)]
    private static bool LogPlanValidate() => Application.isPlaying;

    [MenuItem("TinyFixers/Debug/Bildirim İzin Popup'ını Sıfırla")]
    private static void ResetConsent()
    {
        PlayerPrefs.DeleteKey("notif_consent");
        PlayerPrefs.Save();
        Debug.Log("[Notifications] İzin cevabı silindi; ana menüye dönünce (level ≥ 4) popup tekrar açılır.");
    }

    [MenuItem("TinyFixers/Debug/Değerlendirme İsteğini Sıfırla")]
    private static void ResetReview()
    {
        PlayerPrefs.DeleteKey("review_last_request_ticks");
        PlayerPrefs.DeleteKey("review_last_request_version");
        PlayerPrefs.SetInt("review_happy_pending", 1);
        PlayerPrefs.Save();
        Debug.Log("[StoreReview] Sıfırlandı + mutlu an işaretlendi; ana menüye dönünce (level ≥ 15) istek denenir.");
    }
}
