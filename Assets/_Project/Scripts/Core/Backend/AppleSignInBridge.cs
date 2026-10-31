using System;
using System.Text;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Extensions;
using AppleAuth.Native;
using Firebase.Auth;
using UnityEngine;

/// <summary>
/// Sign in with Apple köprüsü (Docs/ProductionPlan.md P4): lupidan/apple-signin-unity
/// eklentisinden Apple kimliği alır, Firebase Credential'a çevirip
/// AuthLinkService.AppleTokenProvider slotunu doldurur — popup'taki Apple butonu
/// bu sayede aktifleşir. Yalnız desteklenen platformda (iOS cihaz) devreye girer;
/// editor/Android'de slot boş kalır → buton "(Yakında)" görünür.
/// </summary>
public sealed class AppleSignInBridge : MonoBehaviour
{
    private static AppleAuthManager manager;
    private static AppleSignInBridge instance;
    private static bool pending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Sign in with Apple capability yalnız TF_APPLE_SIGNIN_CAPABILITY define'ı açıkken
        // Xcode'a eklenir (paralı hesap şart). Define kapalıyken capability yok → giriş
        // çalışmaz; buton "Yakında" kalsın diye köprüyü hiç kurma (aktif ama bozuk buton olmasın).
#if !TF_APPLE_SIGNIN_CAPABILITY
        return;
#else
        if (RuntimeSimulationSession.IsActive || instance != null || !AppleAuthManager.IsCurrentPlatformSupported)
            return;   // editor/Android: Apple butonu "Yakında" kalır

        var go = new GameObject("AppleSignInBridge");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AppleSignInBridge>();
        instance.enabled = false;

        manager = new AppleAuthManager(new PayloadDeserializer());
        AuthLinkService.AppleTokenProvider = Fetch;
        AuthLinkService.NotifyAvailabilityChanged();
#endif
    }

    // Eklenti callback'leri ana thread'e bu Update pompasıyla düşer.
    private void Update() => manager?.Update();

    private static void Fetch(Action<Credential, string> callback)
    {
        if (manager == null || instance == null || pending)
        {
            callback(null, GameLocalization.Get("auth_apple_unsupported"));
            return;
        }

        // Firebase, replay koruması için SHA256(nonce) Apple'a / raw nonce kendisine ister.
        string rawNonce = AuthNonce.Create();
        pending = true;
        instance.enabled = true;
        var loginArgs = new AppleAuthLoginArgs(
            LoginOptions.IncludeEmail | LoginOptions.IncludeFullName,
            AuthNonce.Sha256(rawNonce));

        try
        {
        manager.LoginWithAppleId(
            loginArgs,
            credential =>
            {
                pending = false;
                if (instance != null) instance.enabled = false;
                var apple = credential as IAppleIDCredential;
                if (apple?.IdentityToken == null)
                {
                    callback(null, GameLocalization.Get("auth_apple_failed"));
                    return;
                }

                string idToken = Encoding.UTF8.GetString(apple.IdentityToken);
                string authCode = apple.AuthorizationCode != null
                    ? Encoding.UTF8.GetString(apple.AuthorizationCode)
                    : null;

                callback(OAuthProvider.GetCredential("apple.com", idToken, rawNonce, authCode), null);
            },
            error =>
            {
                pending = false;
                if (instance != null) instance.enabled = false;
                callback(null, error.GetAuthorizationErrorCode() == AuthorizationErrorCode.Canceled
                    ? null : GameLocalization.Get("auth_apple_failed"));
            });
        }
        catch
        {
            pending = false;
            if (instance != null) instance.enabled = false;
            callback(null, GameLocalization.Get("auth_apple_failed"));
        }
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        manager = null;
        pending = false;
        AuthLinkService.AppleTokenProvider = null;
        AuthLinkService.NotifyAvailabilityChanged();
    }
}
