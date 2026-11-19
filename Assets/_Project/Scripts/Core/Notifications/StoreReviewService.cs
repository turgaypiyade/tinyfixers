using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Mağaza değerlendirmesi (rate us) — işletim sisteminin KENDİ penceresi, araya soru koymadan.
/// "Beğendin mi? Evet → mağaza" ön-sorusu (review gating) Google Play politikasına aykırı → YOK.
///
/// Akış: mutlu an olur (ilk denemede kazanma, Harika tamamlama, event birinciliği) → bayrak düşer →
/// ana menü açılınca kısa gecikmeyle sistem penceresi istenir. Gerçekten gösterilip gösterilmeyeceğine
/// OS karar verir (iOS yılda en fazla 3; Google kendi kotası) — bu yüzden seyrek ve doğru anda çağrılır:
///  - level ≥ <see cref="MinLevel"/>, son istekten ≥ <see cref="MinDaysBetween"/> gün, uygulama sürümü başına 1.
///  - Bildirim izni ön-popup'ı bekliyorsa o oturumda istenmez (iki pencere üst üste binmesin).
///
/// Android: Play In-App Review kütüphanesi (com.google.android.play:review, Editor/Review/PlayReviewDependencies.xml)
/// doğrudan JNI ile çağrılır; Unity eklenti paketi/define gerekmez. Pencere yalnız Play Store'dan (iç test dahil)
/// kurulan sürümde görünür; kabloyla kurulan APK'de görünmemesi normaldir.
/// </summary>
public static class StoreReviewService
{
    private const int MinLevel = 15;
    private const int MinDaysBetween = 30;
    private const float MainMenuDelaySeconds = 2.5f;

    /// App Store Connect > Uygulama Bilgileri > Apple ID (sayısal). Boşken Ayarlar butonu iOS'ta sistem penceresini ister.
    private const string AppStoreId = "";

    private const string KeyPending = "review_happy_pending";
    private const string KeyLastRequestTicks = "review_last_request_ticks";
    private const string KeyLastRequestVersion = "review_last_request_version";

    private static Runner s_runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        PlayerStats.OnLevelCleared += HandleLevelCleared;
        WonderProgress.OnWonderCompleted += _ => MarkHappyMoment();
    }

    /// İlk denemede kazanınca seri artar; fail sonrası kazanmada seri 0 kalır.
    private static void HandleLevelCleared()
    {
        if (PlayerStats.CurrentStreak > 0)
            MarkHappyMoment();
    }

    /// Oyuncunun sevindiği bir an (event birinciliği vb.); istek bir sonraki ana menü açılışında yapılır.
    public static void MarkHappyMoment()
    {
        PlayerPrefs.SetInt(KeyPending, 1);
        PlayerPrefs.Save();
    }

    /// Ana menü açılışında çağrılır.
    public static void TryRequestOnMainMenu()
    {
        if (PlayerPrefs.GetInt(KeyPending, 0) == 0) return;
        if (!IsEligible()) return;
        if (NotificationPermissionPrompt.WillShowThisSession || DailySlotEventButton.WheelHasPriority) return;
        EnsureRunner().StartCoroutine(CoRequestDelayed());
    }

    private static bool IsEligible()
    {
        if (CurrentLevel.Global < MinLevel) return false;
        if (PlayerPrefs.GetString(KeyLastRequestVersion, "") == Application.version) return false;
        long last = long.TryParse(PlayerPrefs.GetString(KeyLastRequestTicks, "0"), out long t) ? t : 0;
        return last <= 0 || DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc) >= TimeSpan.FromDays(MinDaysBetween);
    }

    private static IEnumerator CoRequestDelayed()
    {
        yield return new WaitForSecondsRealtime(MainMenuDelaySeconds);
        if (PlayerPrefs.GetInt(KeyPending, 0) == 0 || !IsEligible()) yield break;
        if (DailySlotEventButton.WheelHasPriority) yield break;   // çark açıksa bu ziyaret atlanır; bayrak sonraki menüye kalır
        if (!PlatformCanRequest()) yield break;

        PlayerPrefs.SetInt(KeyPending, 0);
        PlayerPrefs.SetString(KeyLastRequestTicks, DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.SetString(KeyLastRequestVersion, Application.version);
        PlayerPrefs.Save();
        yield return PlatformRequestReview();
    }

    /// Ayarlar > "Bizi Değerlendir": oyuncu istedi → doğrudan mağaza yorum sayfası.
    public static void OpenStorePage()
    {
#if UNITY_IOS
        if (string.IsNullOrEmpty(AppStoreId))
        {
            UnityEngine.iOS.Device.RequestStoreReview();
            return;
        }
        Application.OpenURL($"itms-apps://itunes.apple.com/app/id{AppStoreId}?action=write-review");
#elif UNITY_ANDROID && !UNITY_EDITOR
        Application.OpenURL($"market://details?id={Application.identifier}");
#else
        ;
#endif
    }

    // ── Platform ────────────────────────────────────────────────────

    private static bool PlatformCanRequest()
    {
#if UNITY_EDITOR
        ;
        return true;
#elif UNITY_IOS
        return true;
#elif UNITY_ANDROID
        return true;
#else
        return false;
#endif
    }

    private static IEnumerator PlatformRequestReview()
    {
#if UNITY_IOS && !UNITY_EDITOR
        UnityEngine.iOS.Device.RequestStoreReview();
        yield break;
#elif UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject activity, manager;
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using (var factory = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory"))
                manager = factory.CallStatic<AndroidJavaObject>("create", activity);
        }
        catch (Exception e)
        {
            if (GameDiagnostics.CriticalErrors) Debug.LogWarning($"[StoreReview] create: {e.Message}");
            yield break;
        }

        var request = new TaskListener(wantResult: true);
        using (var task = manager.Call<AndroidJavaObject>("requestReviewFlow"))
            task.Call<AndroidJavaObject>("addOnCompleteListener", request).Dispose();
        yield return request.Wait();
        if (!request.Success || request.Result == null)
        {
            if (GameDiagnostics.CriticalErrors) Debug.LogWarning("[StoreReview] requestReviewFlow başarısız");
            manager.Dispose(); activity.Dispose();
            yield break;
        }

        var launch = new TaskListener(wantResult: false);
        using (var task = manager.Call<AndroidJavaObject>("launchReviewFlow", activity, request.Result))
            task.Call<AndroidJavaObject>("addOnCompleteListener", launch).Dispose();
        yield return launch.Wait();
        request.Result.Dispose(); manager.Dispose(); activity.Dispose();
#else
        yield break;
#endif
    }

    private static Runner EnsureRunner()
    {
        if (s_runner != null) return s_runner;
        var go = new GameObject("[StoreReviewService]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        s_runner = go.AddComponent<Runner>();
        return s_runner;
    }

    private sealed class Runner : MonoBehaviour { }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// gms Task tamamlanma dinleyicisi. Java tarafında (Unity dışı iş parçacığı) çağrılır: yalnız bayrak/sonuç yazar,
    /// coroutine bunu zaman aşımıyla bekler (Play cevap vermezse menü takılmaz).
    private sealed class TaskListener : AndroidJavaProxy
    {
        private const float TimeoutSeconds = 10f;
        private readonly bool _wantResult;
        private volatile bool _done;
        public bool Success { get; private set; }
        public AndroidJavaObject Result { get; private set; }

        public TaskListener(bool wantResult) : base("com.google.android.gms.tasks.OnCompleteListener")
            => _wantResult = wantResult;

        // Java imzası: void onComplete(Task task)
        private void onComplete(AndroidJavaObject task)
        {
            try
            {
                Success = task.Call<bool>("isSuccessful");
                if (Success && _wantResult) Result = task.Call<AndroidJavaObject>("getResult");
            }
            catch (Exception) { Success = false; }
            _done = true;
        }

        public IEnumerator Wait()
        {
            float end = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!_done && Time.realtimeSinceStartup < end) yield return null;
        }
    }
#endif
}
