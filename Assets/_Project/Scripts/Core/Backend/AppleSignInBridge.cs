using System;
using System.Security.Cryptography;
using System.Text;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
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
    private static string rawNonce;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Sign in with Apple capability yalnız TF_APPLE_SIGNIN_CAPABILITY define'ı açıkken
        // Xcode'a eklenir (paralı hesap şart). Define kapalıyken capability yok → giriş
        // çalışmaz; buton "Yakında" kalsın diye köprüyü hiç kurma (aktif ama bozuk buton olmasın).
#if !TF_APPLE_SIGNIN_CAPABILITY
        return;
#else
        if (!AppleAuthManager.IsCurrentPlatformSupported)
            return;   // editor/Android: Apple butonu "Yakında" kalır

        var go = new GameObject("AppleSignInBridge");
        DontDestroyOnLoad(go);
        go.AddComponent<AppleSignInBridge>();

        manager = new AppleAuthManager(new PayloadDeserializer());
        AuthLinkService.AppleTokenProvider = Fetch;
        Debug.Log("[AppleSignIn] köprü hazır — Apple butonu aktif.");
#endif
    }

    // Eklenti callback'leri ana thread'e bu Update pompasıyla düşer.
    private void Update() => manager?.Update();

    private static void Fetch(Action<Credential, string> callback)
    {
        if (manager == null)
        {
            callback(null, "Apple girişi bu cihazda desteklenmiyor.");
            return;
        }

        // Firebase, replay koruması için SHA256(nonce) Apple'a / raw nonce kendisine ister.
        rawNonce = GenerateNonce(32);
        var loginArgs = new AppleAuthLoginArgs(
            LoginOptions.IncludeEmail | LoginOptions.IncludeFullName,
            Sha256(rawNonce));

        manager.LoginWithAppleId(
            loginArgs,
            credential =>
            {
                var apple = credential as IAppleIDCredential;
                if (apple?.IdentityToken == null)
                {
                    callback(null, "Apple kimliği alınamadı, tekrar dene.");
                    return;
                }

                string idToken = Encoding.UTF8.GetString(apple.IdentityToken);
                string authCode = apple.AuthorizationCode != null
                    ? Encoding.UTF8.GetString(apple.AuthorizationCode)
                    : null;

                callback(OAuthProvider.GetCredential("apple.com", idToken, rawNonce, authCode), null);
            },
            error => callback(null, null));   // kullanıcı iptali → sessiz kapan
    }

    private static string GenerateNonce(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";
        var bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(bytes);

        var sb = new StringBuilder(length);
        foreach (var b in bytes)
            sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }

    private static string Sha256(string input)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
