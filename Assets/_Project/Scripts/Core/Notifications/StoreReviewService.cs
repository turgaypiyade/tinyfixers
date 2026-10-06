using System;
using System.Collections;
using UnityEngine;
#if TF_PLAY_REVIEW && UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.Review;
#endif

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
/// Android: Google Play In-App Review paketi + Scripting Define <c>TF_PLAY_REVIEW</c> gerekir; define yokken
/// Android'de otomatik istek atlanır (Ayarlar butonu mağaza sayfasını yine açar). Pencere yalnız Play
/// Store'dan (iç test dahil) kurulan sürümde görünür; kabloyla kurulan APK'de görünmemesi normaldir.
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
        if (NotificationPermissionPrompt.WillShowThisSession) return;
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
        Debug.Log("[StoreReview] (editör) mağaza sayfası açılacaktı.");
#endif
    }

    // ── Platform ────────────────────────────────────────────────────

    private static bool PlatformCanRequest()
    {
#if UNITY_EDITOR
        Debug.Log("[StoreReview] (editör) değerlendirme penceresi istenecekti.");
        return true;
#elif UNITY_IOS
        return true;
#elif UNITY_ANDROID && TF_PLAY_REVIEW
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
#elif TF_PLAY_REVIEW && UNITY_ANDROID && !UNITY_EDITOR
        var manager = new ReviewManager();
        var request = manager.RequestReviewFlow();
        yield return request;
        if (request.Error != ReviewErrorCode.NoError)
        {
            Debug.LogWarning($"[StoreReview] RequestReviewFlow: {request.Error}");
            yield break;
        }
        var launch = manager.LaunchReviewFlow(request.GetResult());
        yield return launch;
        if (launch.Error != ReviewErrorCode.NoError)
            Debug.LogWarning($"[StoreReview] LaunchReviewFlow: {launch.Error}");
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
}
