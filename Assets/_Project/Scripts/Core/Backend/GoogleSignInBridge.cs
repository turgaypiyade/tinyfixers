using System;
using System.Runtime.InteropServices;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Scripting;

/// <summary>Official Google native sign-in. Created on demand, no Update loop or game-scene binding.</summary>
public sealed class GoogleSignInBridge : MonoBehaviour
{
    private static GoogleSignInBridge instance;
    private Action<Credential, string> pending;
    private int requestId;
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void TFGoogleSignIn(string target, int request,
        string clientId, string serverClientId);
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive) return;
#if UNITY_IOS && !UNITY_EDITOR
        if (!SocialSignInConfig.ValidGoogleClient(SocialSignInConfig.Current.googleIosClientId)) return;
#elif UNITY_ANDROID && !UNITY_EDITOR
        if (!SocialSignInConfig.ValidGoogleClient(SocialSignInConfig.Current.googleWebClientId)) return;
#else
        return;
#endif
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        AuthLinkService.GoogleTokenProvider = Fetch;
        AuthLinkService.NotifyAvailabilityChanged();
#endif
    }
    private static void Fetch(Action<Credential, string> callback)
    {
        if (instance == null)
        {
            var go = new GameObject("[GoogleSignIn]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GoogleSignInBridge>();
        }
        if (instance.pending != null) { callback(null, GameLocalization.Get("auth_link_error")); return; }
        instance.pending = callback;
        int id = ++instance.requestId;
        var config = SocialSignInConfig.Current;
        try
        {
#if UNITY_IOS && !UNITY_EDITOR
            TFGoogleSignIn(instance.gameObject.name, id, config.googleIosClientId, config.googleWebClientId ?? "");
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge = new AndroidJavaClass("com.wondigames.tinyfixers.TFGoogleSignIn");
            bridge.CallStatic("signIn", activity, config.googleWebClientId, instance.gameObject.name, id);
#else
            instance.Complete(null, GameLocalization.Get("auth_provider_inactive"));
#endif
        }
        catch { instance.Complete(null, GameLocalization.Get("auth_link_error")); }
    }
    [Serializable] private sealed class Result
    {
        public int requestId;
        public string idToken, error;
        public bool canceled;
    }
    [Preserve] public void OnGoogleSignIn(string json)
    {
        if (pending == null) return;
        Result result;
        try { result = JsonUtility.FromJson<Result>(json); }
        catch { Complete(null, GameLocalization.Get("auth_link_error")); return; }
        if (result == null || result.requestId != requestId) return;
        if (result.canceled) { Complete(null, null); return; }
        if (!string.IsNullOrEmpty(result.error) || string.IsNullOrWhiteSpace(result.idToken))
        { Complete(null, GameLocalization.Get("auth_link_error")); return; }
        Complete(GoogleAuthProvider.GetCredential(result.idToken, null), null);
    }
    private void Complete(Credential credential, string error)
    {
        var callback = pending;
        pending = null;
        callback?.Invoke(credential, error);
    }
    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        ++requestId;
        Complete(null, null);
    }
}
