using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;

/// <summary>
/// Firebase anonim kimlik doğrulama. Uygulama açılışında otomatik başlar (sahne wiring gerekmez):
/// bağımlılıkları kontrol eder → FirebaseApp'i hazırlar → anonim giriş yapar → cihaza-kalıcı bir
/// UserId elde eder. Sosyal login (Google/Apple) sonra bunun üzerine bağlanır.
///
/// Leaderboard/Team servisleri IsReady/UserId'yi kullanır; hazır olunca OnReady tetiklenir.
/// </summary>
public static class FirebaseAuthService
{
    public static bool   IsReady { get; private set; }
    public static string UserId  { get; private set; }

    /// <summary>Anonim giriş tamamlanınca (UserId hazır) bir kez tetiklenir; sonradan abone olan hemen çağrılır.</summary>
    public static event Action OnReady
    {
        add    { onReady += value; if (IsReady) value?.Invoke(); }
        remove { onReady -= value; }
    }
    private static Action onReady;

    private static bool initStarted;
    private static FirebaseAuth auth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (initStarted) return;
        initStarted = true;

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.Result != DependencyStatus.Available)
            {
                Debug.LogError($"[Firebase] Bağımlılıklar hazır değil: {task.Result} {task.Exception}");
                return;
            }
            auth = FirebaseAuth.DefaultInstance;
            SignInAnonymously();
        });
    }

    private static void SignInAnonymously()
    {
        if (auth.CurrentUser != null)      // zaten girişli (önceki oturumdan) → tekrar giriş yapma
        {
            Complete(auth.CurrentUser);
            return;
        }

        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError($"[Firebase] Anonim giriş başarısız: {task.Exception}");
                return;
            }
            Complete(task.Result.User);
        });
    }

    private static void Complete(FirebaseUser user)
    {
        UserId  = user.UserId;
        IsReady = true;
        Debug.Log($"[Firebase] Hazır ✅  UID: {UserId}");
        onReady?.Invoke();
    }
}
