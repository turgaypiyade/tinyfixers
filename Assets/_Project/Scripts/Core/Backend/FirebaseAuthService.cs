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
    // Hold the default app for the process lifetime, independently of sign-in/network readiness.
    private static FirebaseApp app;
    private static FirebaseAuth auth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (RuntimeSimulationSession.IsActive) return;
        if (initStarted) return;
        initStarted = true;

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                if (GameDiagnostics.Backend)
                    Debug.LogError($"[Firebase] Bağımlılık kontrolü tamamlanamadı: canceled={task.IsCanceled} {task.Exception}");
                initStarted = false;
                return;
            }
            if (task.Result != DependencyStatus.Available)
            {
                if (GameDiagnostics.Backend)
                    Debug.LogError($"[Firebase] Bağımlılıklar hazır değil: {task.Result}");
                initStarted = false;
                return;
            }
            try
            {
                app = FirebaseApp.DefaultInstance;
                auth = FirebaseAuth.GetAuth(app);
                if (GameDiagnostics.Backend)
                    Debug.Log("[Firebase] Default app hazır; kimlik doğrulama başlıyor.");
                SignInAnonymously();
            }
            catch (Exception e)
            {
                initStarted = false;
                if (GameDiagnostics.Backend)
                    Debug.LogError($"[Firebase] Default app başlatılamadı: {e}");
            }
        });
    }

    /// Hesap silme (AccountDeletionService) için oturumdaki Firebase kullanıcısı.
    internal static FirebaseUser CurrentUser => (auth ?? FirebaseAuth.DefaultInstance)?.CurrentUser;

    /// Hesap silindikten sonra: oturumu kapat; IsReady kapanır → bulut kayıt eski UID'ye yazmaz. Sahne yeniden
    /// yüklenince Initialize yeni bir anonim kullanıcı açar.
    internal static void ResetAfterAccountDeletion()
    {
        try { (auth ?? FirebaseAuth.DefaultInstance)?.SignOut(); } catch (Exception) { }
        IsReady = false;
        UserId = null;
        initStarted = false;
    }

#if UNITY_EDITOR
    /// EDİTÖR TESTİ: anonim kullanıcıdan çıkış → bir sonraki Play'de Firebase YENİ bir anonim UID üretir.
    /// IsReady kapatılır: bulut kayıt itmesi (Push, IsReady şartlı) silinmiş yerel veriyi eski UID'ye yazmasın.
    public static string DebugSignOutForFreshUser()
    {
        string oldUid = UserId;
        (auth ?? FirebaseAuth.DefaultInstance)?.SignOut();
        IsReady = false;
        UserId = null;
        initStarted = false;   // domain reload kapalı olsa bile sonraki Play'de yeniden başlasın
        return oldUid;
    }
#endif

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
                if (GameDiagnostics.Backend)
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
        onReady?.Invoke();
    }
}
