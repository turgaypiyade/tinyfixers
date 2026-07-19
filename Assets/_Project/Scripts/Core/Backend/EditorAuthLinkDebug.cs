#if UNITY_EDITOR
using Firebase.Auth;
using UnityEngine;

/// <summary>
/// YALNIZ EDITOR: sosyal login akışını uçtan uca test etmek için Google butonunun
/// slotunu GERÇEK bir Firebase Email/Password credential'ıyla doldurur. Link, çakışma
/// ("o hesaba geç"), restore — hepsi gerçek Firebase'le denenir; cihaz build'inde bu
/// dosya derlenmez, buton gerçek Google köprüsünü bekler.
///
/// KULLANICI ADIMI (bir kez): Firebase Console → Authentication → Sign-in method →
/// Email/Password → Enable.
///
/// Test e-postası cihaz kimliğinden türer (her editor kurulumunda sabit) — ikinci bir
/// "hesap" denemek için maili elle değiştir.
/// </summary>
public static class EditorAuthLinkDebug
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (AuthLinkService.GoogleTokenProvider != null) return;   // gerçek köprü varsa karışma

        AuthLinkService.GoogleTokenProvider = callback =>
        {
            string email = $"editor-{Mathf.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode())}@tinyfixers.test";
            const string password = "TinyFixers-Editor-1!";

            Debug.Log($"[EditorAuthDebug] test credential: {email}");
            callback(EmailAuthProvider.GetCredential(email, password), null);
        };

        Debug.Log("[EditorAuthDebug] Google butonu EDITOR'de e-posta test credential'ına bağlandı.");
    }
}
#endif
