using System;
using System.Collections;
using UnityEngine;
#if TF_ADMOB
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

/// <summary>
/// Ödüllü reklam (rewarded) — tek giriş noktası: fail'de bedava devam, can teklifi, ana menü can reklamı.
///
/// Etkinleştirme: Google Mobile Ads Unity eklentisi (.unitypackage — External Dependency Manager'ı Firebase'in
/// mevcut kopyasının ÜSTÜNE YAZMADAN içe aktar) + Player Settings ▸ Scripting Define Symbols: TF_ADMOB.
/// Assets ▸ Google Mobile Ads ▸ Settings: App ID (iOS/Android) + "User Tracking Usage Description" (ATT metni).
///
/// Sıra (gizlilik): UMP onay bilgisi güncellenir → gerekirse onay formu (AB/GDPR; AdMob ▸ Privacy & messaging'de
/// tanımlı IDFA açıklaması iOS'ta ATT izin penceresini de tetikler) → izin varsa SDK başlar → reklam önceden yüklenir.
///
/// TF_ADMOB yokken: editör / development build'de kısa simülasyon (ödül verilir), yayın build'inde reklam yok.
/// </summary>
public static class AdService
{
    // Google'ın resmi TEST birimleri — gerçek kimlikler gelene dek bunlar kullanılır (kendi reklamına tıklamak
    // hesabı kapatabilir; testte hep test birimi).
    private const string TestRewardedAndroid = "ca-app-pub-3940256099942544/5224354917";
    private const string TestRewardedIos     = "ca-app-pub-3940256099942544/1712485313";
    // AdMob ▸ Uygulama ▸ Reklam birimleri ▸ Ödüllü: gerçek kimlikler buraya (boşsa test birimi kullanılır).
    private const string RewardedAndroid = "";
    private const string RewardedIos     = "";

    private const float SimulatedSeconds = 1f;

    private static Runner runner;

    private sealed class Runner : MonoBehaviour { }

    private static Runner Host
    {
        get
        {
            if (runner != null) return runner;
            var go = new GameObject("AdService");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            runner = go.AddComponent<Runner>();
            return runner;
        }
    }

    public static bool IsShowing { get; private set; }

#if TF_ADMOB
    private static RewardedAd rewarded;
    private static bool sdkReady;
    private static bool loading;

    private static string RewardedUnitId
    {
        get
        {
#if UNITY_ANDROID
            return string.IsNullOrEmpty(RewardedAndroid) ? TestRewardedAndroid : RewardedAndroid;
#else
            return string.IsNullOrEmpty(RewardedIos) ? TestRewardedIos : RewardedIos;
#endif
        }
    }

    /// Reklam şu an gösterilebilir mi (yüklü).
    public static bool IsReady => rewarded != null && rewarded.CanShowAd();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive) return;
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
        {
            if (updateError != null) {
                if (GameDiagnostics.Backend)
                    Debug.LogWarning($"[Ads] Onay bilgisi alınamadı: {updateError.Message}");
            }
            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null) {
                    if (GameDiagnostics.Backend)
                        Debug.LogWarning($"[Ads] Onay formu: {formError.Message}");
                }
                StartSdkIfAllowed();
            });
        });
    }

    private static void StartSdkIfAllowed()
    {
        if (sdkReady || !ConsentInformation.CanRequestAds()) return;
        MobileAds.Initialize(_ =>
        {
            sdkReady = true;
            LoadRewarded();
        });
    }

    private static void LoadRewarded()
    {
        if (!sdkReady || loading || IsReady) return;
        loading = true;
        RewardedAd.Load(RewardedUnitId, new AdRequest(), (ad, error) =>
        {
            loading = false;
            if (error != null || ad == null)
            {
                if (GameDiagnostics.Backend)
                    Debug.LogWarning($"[Ads] Reklam yüklenemedi: {error?.GetMessage()}");
                Host.StartCoroutine(RetryLoad());
                return;
            }
            rewarded = ad;
        });
    }

    private static IEnumerator RetryLoad()
    {
        yield return new WaitForSecondsRealtime(30f);
        LoadRewarded();
    }

    /// Ödüllü reklamı gösterir. onDone(true) = oyuncu ödülü hak etti; false = reklam yok / yarıda kapandı.
    public static void ShowRewarded(Action<bool> onDone)
    {
        if (IsShowing) { onDone?.Invoke(false); return; }
        if (!IsReady)
        {
            LoadRewarded();
            onDone?.Invoke(false);
            return;
        }

        IsShowing = true;
        bool earned = false, finished = false;
        var ad = rewarded;
        rewarded = null;
        void Finish()
        {
            if (finished) return;
            finished = true;
            IsShowing = false;
            ad.Destroy();
            LoadRewarded();   // bir sonraki için hazırla
            onDone?.Invoke(earned);
        }
        ad.OnAdFullScreenContentClosed += Finish;
        ad.OnAdFullScreenContentFailed += err =>
        {
            if (GameDiagnostics.Backend)
                Debug.LogWarning($"[Ads] Reklam gösterilemedi: {err?.GetMessage()}");
            Finish();
        };
        ad.Show(_ =>
        {
            earned = true;
            LevelAssist.RecordRewardedAdCompleted();
        });
    }
#else
    public static bool IsReady =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        true;
#else
        false;
#endif

    public static void ShowRewarded(Action<bool> onDone)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsShowing) { onDone?.Invoke(false); return; }
        Host.StartCoroutine(Simulate(onDone));
#else
        if (GameDiagnostics.Backend)
            Debug.LogWarning("[Ads] Reklam SDK'sı bağlı değil (TF_ADMOB yok).");
        onDone?.Invoke(false);
#endif
    }

    private static IEnumerator Simulate(Action<bool> onDone)
    {
        IsShowing = true;
        yield return new WaitForSecondsRealtime(SimulatedSeconds);
        IsShowing = false;
        onDone?.Invoke(true);
    }
#endif
}
