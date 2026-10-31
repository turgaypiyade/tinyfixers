using System;
using Facebook.Unity;
using Firebase.Auth;
using UnityEngine;

/// <summary>Facebook is initialized only after the player requests sign-in; no game update loop.</summary>
public static class FacebookSignInBridge
{
    private static Action<Credential, string> pending;
    private static readonly string[] Permissions = { "public_profile" };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive) return;
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        var config = SocialSignInConfig.Current;
        if (string.IsNullOrWhiteSpace(config.facebookAppId) || string.IsNullOrWhiteSpace(config.facebookClientToken)) return;
        AuthLinkService.FacebookTokenProvider = Fetch;
        AuthLinkService.NotifyAvailabilityChanged();
#endif
    }
    private static void Fetch(Action<Credential, string> callback)
    {
        if (pending != null) { callback(null, GameLocalization.Get("auth_link_error")); return; }
        pending = callback;
        try
        {
            if (FB.IsInitialized) Login();
            else FB.Init(SocialSignInConfig.Current.facebookAppId,
                clientToken: SocialSignInConfig.Current.facebookClientToken,
                cookie: false, logging: false, status: false, frictionlessRequests: false,
                onInitComplete: () =>
                {
                    if (!FB.IsInitialized) { Finish(null, GameLocalization.Get("auth_link_error")); return; }
                    FB.Mobile.SetAutoLogAppEventsEnabled(false);
                    FB.Mobile.SetAutoLogProductImprovementEnabled(false);
                    FB.Mobile.SetAutoLogMetaDataEnabled(false);
                    FB.Mobile.SetAdvertiserIDCollectionEnabled(false);
                    FB.Mobile.SetAdvertiserTrackingEnabled(false);
                    Login();
                });
        }
        catch { Finish(null, GameLocalization.Get("auth_link_error")); }
    }
    private static void Login()
    {
        if (pending == null) return;
#if UNITY_IOS && !UNITY_EDITOR
        string nonce = AuthNonce.Create();
        // Firebase compares the hash claim in the Limited Login token to this raw nonce.
        FB.Mobile.LoginWithTrackingPreference(LoginTracking.LIMITED, Permissions, AuthNonce.Sha256(nonce), result =>
        {
            if (result.Cancelled) { Finish(null, null); return; }
            if (!string.IsNullOrEmpty(result.Error) || result.AuthenticationToken == null)
            { Finish(null, GameLocalization.Get("auth_link_error")); return; }
            Finish(OAuthProvider.GetCredential("facebook.com", result.AuthenticationToken.TokenString, nonce, null), null);
        });
#else
        FB.LogInWithReadPermissions(Permissions, result =>
        {
            if (result.Cancelled) { Finish(null, null); return; }
            if (!string.IsNullOrEmpty(result.Error) || result.AccessToken == null)
            { Finish(null, GameLocalization.Get("auth_link_error")); return; }
            Finish(FacebookAuthProvider.GetCredential(result.AccessToken.TokenString), null);
        });
#endif
    }
    private static void Finish(Credential credential, string error)
    {
        var callback = pending;
        pending = null;
        callback?.Invoke(credential, error);
    }
}
