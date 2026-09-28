using UnityEditor;
using UnityEngine;

// Editörde sıfırdan yeni bir oyuncu olarak başlamak: Firebase anonim çıkış (yeni UID) + yerel veri silme +
// bir sonraki Play'i test değerleri olmadan "ilk açılış" gibi başlatma. Yalnız Play modunda (Firebase hazır).
public static class FreshUserDebugMenu
{
    private const string MenuPath = "TinyFixers/Debug/Yeni Kullanıcı Olarak Başla (Play)";

    [MenuItem(MenuPath)]
    private static void StartAsFreshUser()
    {
        if (!EditorUtility.DisplayDialog("Yeni Kullanıcı",
                "Firebase anonim kullanıcısından çıkılacak, tüm yerel veri (PlayerPrefs) silinecek ve Play durdurulacak.\n\n" +
                "Bir sonraki Play'de yeni bir UID alınır ve oyun ilk kez açılıyormuş gibi başlar " +
                "(test değerleri basılmaz; sonraki Play'ler normal test moduna döner).\n\n" +
                "Eski anonim hesaba geri dönülemez (verisi Firebase'de kalır).",
                "Devam", "Vazgeç"))
            return;

        string oldUid = FirebaseAuthService.DebugSignOutForFreshUser();
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        EditorPrefs.SetBool(TestLevelProgressionBootstrap.FreshUserNextLaunchKey, true);
        Debug.Log($"[FreshUser] Çıkış yapıldı (eski UID: {oldUid ?? "yok"}). Bir sonraki Play yeni kullanıcıyla başlar.");
        EditorApplication.isPlaying = false;
    }

    [MenuItem(MenuPath, true)]
    private static bool StartAsFreshUserValidate() => Application.isPlaying;
}
