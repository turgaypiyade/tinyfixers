using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Link in place; switching accounts requires an acknowledged source save and an exact target restore.</summary>
public static class AuthLinkService
{
    public enum Provider { Google, Apple, Facebook }
    public static event Action<Provider> OnLinked;
    public static event Action<Provider, Credential> OnLinkConflict;
    public static event Action<Provider, string> OnLinkFailed;
    public static event Action OnStateChanged;
    public static event Action OnCanceled;
    public static Action<Action<Credential, string>> GoogleTokenProvider;
    public static Action<Action<Credential, string>> AppleTokenProvider;
    public static Action<Action<Credential, string>> FacebookTokenProvider;
    public static bool IsBusy { get; private set; }
    private static int operation;
    private static Credential pendingCredential;
    private static Provider pendingProvider;
    private static string pendingOwner;

    public static bool IsAvailable(Provider p) => Fetcher(p) != null;
    public static void NotifyAvailabilityChanged() => OnStateChanged?.Invoke();
    public static bool IsLinked => FirebaseAuthService.IsReady && FirebaseAuthService.CurrentUser != null
        && !FirebaseAuthService.CurrentUser.IsAnonymous;
    public static bool CanStart => !RuntimeSimulationSession.IsActive && !IsBusy
        && FirebaseAuthService.IsReady && FirebaseCloudSaveService.RestoreResolved
        && !FirebaseCloudSaveService.SuspendForDeletion;

    public static bool HasProvider(Provider p)
    {
        if (!FirebaseAuthService.IsReady) return false;
        var user = FirebaseAuthService.CurrentUser;
        if (user == null) return false;
        string id = p == Provider.Apple ? "apple.com" : p == Provider.Google ? "google.com" : "facebook.com";
        foreach (var data in user.ProviderData) if (data.ProviderId == id) return true;
        return false;
    }

    private static Action<Action<Credential, string>> Fetcher(Provider p) => p switch
    {
        Provider.Google => GoogleTokenProvider,
        Provider.Apple => AppleTokenProvider,
        Provider.Facebook => FacebookTokenProvider,
        _ => null,
    };

    public static void Link(Provider provider)
    {
        if (IsBusy) return;
        if (!CanStart) { Fail(provider, "auth_session_not_ready"); return; }
        if (HasProvider(provider)) { OnLinked?.Invoke(provider); return; }
        var fetch = Fetcher(provider);
        if (fetch == null) { Fail(provider, "auth_provider_inactive"); return; }
        CancelConflict();
        string uid = FirebaseAuthService.UserId;
        int token = ++operation;
        bool received = false;
        SetBusy(true);
        try
        {
            fetch((credential, error) =>
            {
                if (received || token != operation) return;
                received = true;
                if (!FirebaseAuthService.IsReady || FirebaseAuthService.UserId != uid)
                { Fail(provider, "auth_session_not_ready"); return; }
                if (credential == null)
                {
                    SetBusy(false);
                    if (!string.IsNullOrEmpty(error)) OnLinkFailed?.Invoke(provider, error);
                    else OnCanceled?.Invoke();
                    return;
                }
                LinkCredential(provider, credential, uid, token);
            });
        }
        catch { Fail(provider, "auth_link_error"); }
    }

    private static void LinkCredential(Provider provider, Credential credential, string uid, int token)
    {
        var user = FirebaseAuthService.CurrentUser;
        if (user == null || user.UserId != uid) { Fail(provider, "auth_session_not_ready"); return; }
        user.LinkWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
        {
            if (token != operation) return;
            if (FirebaseAuthService.UserId != uid)
            { Fail(provider, "auth_session_not_ready"); return; }
            if (!task.IsFaulted && !task.IsCanceled)
            {
                SetBusy(false);
                FirebaseCloudSaveService.MarkDirty();
                FirebaseCloudSaveService.Push();
                OnLinked?.Invoke(provider);
                return;
            }
            var error = task.Exception?.GetBaseException();
            int code = error is FirebaseException firebase ? firebase.ErrorCode : -1;
            if (code == (int)AuthError.CredentialAlreadyInUse || code == (int)AuthError.EmailAlreadyInUse)
            {
                // Apple credentials can be single-use: Firebase supplies the replacement on collision.
                if (error is FirebaseAccountLinkException link && link.UserInfo?.UpdatedCredential != null
                    && link.UserInfo.UpdatedCredential.IsValid()) credential = link.UserInfo.UpdatedCredential;
                pendingCredential = credential;
                pendingProvider = provider;
                pendingOwner = uid;
                SetBusy(false);
                OnLinkConflict?.Invoke(provider, credential);
                return;
            }
            Fail(provider, code == (int)AuthError.NetworkRequestFailed ? "auth_network_error" : "auth_link_error");
        });
    }

    public static void CancelConflict()
    {
        pendingCredential = null;
        pendingOwner = null;
    }

    public static void SwitchToExisting(Provider provider, Credential credential)
    {
        if (IsBusy) return;
        if (!CanStart || credential == null || credential != pendingCredential || provider != pendingProvider
            || pendingOwner != FirebaseAuthService.UserId)
        { Fail(provider, "auth_session_not_ready"); return; }
        if (LevelAttemptStats.HasActiveAttempt) { Fail(provider, "auth_switch_from_menu"); return; }
        string uid = FirebaseAuthService.UserId;
        int token = ++operation;
        SetBusy(true);
        FirebaseCloudSaveService.FlushBeforeAccountSwitch().ContinueWithOnMainThread(saved =>
        {
            if (token != operation) return;
            if (saved.IsCanceled || saved.IsFaulted || FirebaseAuthService.UserId != uid)
            { Fail(provider, "auth_backup_required"); return; }
            try { LocalAccountData.PrepareSwitch(uid); }
            catch { Fail(provider, "auth_backup_required"); return; }
            FirebaseCloudSaveService.BeginAccountSwitch();
            FirebaseAuthService.SuspendForAccountSwitch();
            FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(signedIn =>
            {
                if (token != operation) return;
                if (signedIn.IsCanceled || signedIn.IsFaulted)
                {
                    FirebaseAuthService.ResumeCurrentSession();
                    Fail(provider, "auth_link_error");
                    return;
                }
                string destination = signedIn.Result.UserId;
                Action restored = null;
                restored = () =>
                {
                    if (FirebaseAuthService.UserId != destination || !FirebaseCloudSaveService.RestoreResolved) return;
                    FirebaseCloudSaveService.OnRestored -= restored;
                    FirebaseCloudSaveService.OnRestoreFailed -= RestoreFailed;
                    CancelConflict();
                    SetBusy(false);
                    OnLinked?.Invoke(provider);
                    SceneManager.LoadScene("MainMenu");
                };
                FirebaseCloudSaveService.OnRestored += restored;
                FirebaseCloudSaveService.OnRestoreFailed += RestoreFailed;
                // OnReady runs the cloud restore with the NEW UID; uploads stay gated until it finishes.
                FirebaseAuthService.ResumeCurrentSession();
            });
        });
    }

    private static void RestoreFailed()
        => OnLinkFailed?.Invoke(pendingProvider, GameLocalization.Get("auth_restore_retry"));
    private static void SetBusy(bool value) { IsBusy = value; OnStateChanged?.Invoke(); }
    private static void Fail(Provider p, string key) { SetBusy(false); OnLinkFailed?.Invoke(p, GameLocalization.Get(key)); }
}
