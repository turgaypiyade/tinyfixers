using UnityEditor;
using UnityEngine;

// Profil açılış öğreticisini yeniden test etmek için (level ≥ PlayerProfile.UnlockLevel iken ana menüye dönünce açılır).
public static class ProfileTutorialDebugMenu
{
    [MenuItem("TinyFixers/Debug/Profil Öğreticisini Sıfırla")]
    private static void ResetProfileTutorial()
    {
        ProfileUnlockTutorial.ResetSeen();
        if (GameDiagnostics.EditorTools)
            Debug.Log($"[ProfileTutorial] Sıfırlandı; isim varsayılana döndü. Level ≥ {PlayerProfile.UnlockLevel} iken ana menüye dönünce açılır.");
    }
}
