using System;
using System.Collections;
using UnityEngine;
#if TF_ADMOB
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
#endif

/// <summary>Shared rewarded ad lifecycle, consent, test inventory and exactly-once completion.</summary>
public static class AdService
{
    private static Runner runner;
    private static bool stopping;
    private static int generation;
    public static bool IsShowing { get; private set; }

    private sealed class Runner : MonoBehaviour
    {
        private void OnDestroy() { if (runner == this) Shutdown(); }
        private void OnApplicationQuit() => Shutdown();
#if TF_ADMOB
        private void OnApplicationPause(bool paused)
        {
            if (paused || stopping || IsShowing) return;
            if (!ConsentInformation.CanRequestAds()) RequestConsent();
            else StartSdkIfAllowed();
        }
#endif
    }

    private static Runner Host
    {
        get
        {
            if (runner != null) return runner;
            var go = new GameObject("AdService");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
            return runner;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Shutdown();
        stopping = false;
    }

    private static void Shutdown()
    {
        stopping = true;
        ++generation; // Late native callbacks cannot touch a new play session.
        if (runner != null) runner.StopAllCoroutines();
#if TF_ADMOB
        GameSettings.OnSoundChanged -= ApplySoundSetting;
        rewarded?.Destroy();
        rewarded = null;
        showingAd?.Destroy();
        showingAd = null;
        sdkReady = initializing = loading = privacyShowing = consentUpdating = false;
        retry = consentRetry = null;
        completion = null;
#endif
        runner = null;
        IsShowing = false;
    }

    private static void ShowUnavailable() => RuntimeChoicePopup.Show(
        GameLocalization.GetOr("ads_unavailable_title", "Reklam hazır değil"),
        GameLocalization.GetOr("ads_unavailable_body", "Şu anda reklam yüklenemedi. Biraz sonra tekrar deneyebilirsin."));

#if TF_ADMOB
    private static RewardedAd rewarded, showingAd;
    private static bool sdkReady, initializing, loading, privacyShowing, consentUpdating;
    private static int loadVersion;
    private static float loadedAt;
    private static Coroutine retry, consentRetry;
    private static Action<bool> completion;
    private const float MaxAdAgeSeconds = 3500f;

    private static string RewardedUnitId => MonetizationConfig.Current.RewardedId(
#if UNITY_ANDROID
        true,
#else
        false,
#endif
        Debug.isDebugBuild);

    public static bool IsReady => !stopping && !privacyShowing && ConsentInformation.CanRequestAds()
        && rewarded != null && Time.realtimeSinceStartup - loadedAt < MaxAdAgeSeconds && rewarded.CanShowAd();

    public static bool IsPrivacyOptionsRequired => !stopping
        && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive || stopping || runner != null) return;
        if (string.IsNullOrEmpty(RewardedUnitId))
        {
            Debug.LogError("[Ads] Missing production rewarded ad unit ID in MonetizationConfig.json.");
            return;
        }
        // Reklam olayları ana iş parçacığına MobileAdsEventExecutor.ExecuteInUpdate ile aktarılır (eski
        // MobileAds.RaiseAdEventsOnUnityMainThread obsolete). Her SDK geri çağrısı aşağıda tek tek sarılı.
        GameSettings.OnSoundChanged += ApplySoundSetting;
        Host.StartCoroutine(InitializeConsent());
    }

    private static IEnumerator InitializeConsent()
    {
        // Avoid overlapping UMP/ATT with our boot restore and terms dialog.
        while (!CurrencyLedger.SyncGateOpen || LegalConsent.IsShowing) yield return null;
        RequestConsent();
    }

    private static void RequestConsent()
    {
        if (stopping || consentUpdating || privacyShowing || !CurrencyLedger.SyncGateOpen) return;
        consentUpdating = true;
        int session = generation;
        ConsentInformation.Update(new ConsentRequestParameters(), error =>
        {
            if (stopping || session != generation) return;
            if (error != null)
            {
                consentUpdating = false;
                Debug.LogWarning($"[Ads] Consent update failed: {error.Message}");
                StartSdkIfAllowed(); // UMP may still allow requests using stored consent.
                if (!ConsentInformation.CanRequestAds()) ScheduleConsentRetry();
                return;
            }
            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (stopping || session != generation) return;
                consentUpdating = false;
                if (formError != null) Debug.LogWarning($"[Ads] Consent form failed: {formError.Message}");
                StartSdkIfAllowed();
                if (!ConsentInformation.CanRequestAds()) ScheduleConsentRetry();
            });
        });
    }

    private static void ScheduleConsentRetry()
    {
        if (consentRetry == null && !stopping) consentRetry = Host.StartCoroutine(RetryConsent());
    }

    private static IEnumerator RetryConsent()
    {
        yield return new WaitForSecondsRealtime(30f);
        consentRetry = null;
        RequestConsent();
    }

    private static void ApplySoundSetting(bool enabled) => MobileAds.SetApplicationMuted(!enabled);

    private static void StartSdkIfAllowed()
    {
        if (stopping || initializing || !ConsentInformation.CanRequestAds()) return;
        if (sdkReady) { LoadRewarded(); return; }
        initializing = true;
        int session = generation;
        MobileAds.Initialize(_ => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (stopping || session != generation) return;
            initializing = false;
            sdkReady = true;
            ApplySoundSetting(GameSettings.SoundEnabled);
            LoadRewarded();
        }));
    }

    private static void LoadRewarded()
    {
        if (stopping || !sdkReady || loading || IsShowing || privacyShowing
            || !ConsentInformation.CanRequestAds() || IsReady || string.IsNullOrEmpty(RewardedUnitId)) return;
        rewarded?.Destroy();
        rewarded = null;
        loading = true;
        int session = generation;
        int request = ++loadVersion;
        RewardedAd.Load(RewardedUnitId, new AdRequest(), (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (stopping || session != generation || request != loadVersion) { ad?.Destroy(); return; }
            loading = false;
            if (!ConsentInformation.CanRequestAds() || privacyShowing) { ad?.Destroy(); return; }
            if (error != null || ad == null)
            {
                ad?.Destroy();
                if (GameDiagnostics.Backend) Debug.LogWarning($"[Ads] Load failed: {error?.GetMessage()}");
                if (retry == null) retry = Host.StartCoroutine(RetryLoad());
                return;
            }
            rewarded = ad;
            loadedAt = Time.realtimeSinceStartup;
        }));
    }

    private static IEnumerator RetryLoad()
    {
        yield return new WaitForSecondsRealtime(30f);
        retry = null;
        LoadRewarded();
    }

    public static void ShowPrivacyOptions(Action onClosed = null)
    {
        if (!IsPrivacyOptionsRequired || IsShowing || privacyShowing) return;
        privacyShowing = true;
        ++loadVersion;
        loading = false;
        rewarded?.Destroy();
        rewarded = null;
        int session = generation;
        ConsentForm.ShowPrivacyOptionsForm(error =>
        {
            if (stopping || session != generation) return;
            privacyShowing = false;
            if (error != null) Debug.LogWarning($"[Ads] Privacy options failed: {error.Message}");
            StartSdkIfAllowed();
            onClosed?.Invoke();
        });
    }

    /// <summary>true only after the SDK reward event; every request completes at most once.</summary>
    public static void ShowRewarded(Action<bool> onDone)
    {
        if (IsShowing || privacyShowing || stopping) { onDone?.Invoke(false); return; }
        if (!IsReady)
        {
            if (!ConsentInformation.CanRequestAds()) RequestConsent();
            StartSdkIfAllowed();
            ShowUnavailable();
            onDone?.Invoke(false);
            return;
        }
        IsShowing = true;
        bool earned = false, finished = false;
        int session = generation;
        var ad = rewarded;
        showingAd = ad;
        rewarded = null;
        completion = onDone;
        void Finish(bool failed)
        {
            if (finished || stopping || session != generation) return;
            finished = true;
            IsShowing = false;
            showingAd = null;
            ad.Destroy();
            var callback = completion;
            completion = null;
            LoadRewarded();
            if (earned) LevelAssist.RecordRewardedAdCompleted();
            else if (failed) ShowUnavailable();
            callback?.Invoke(earned);
        }
        // SDK olayları arka iş parçacığından gelebilir: sırayı koruyarak ana iş parçacığına aktar (ödül → kapanış).
        ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() => Finish(false));
        ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            Debug.LogWarning($"[Ads] Show failed: {error?.GetMessage()}");
            Finish(true);
        });
        try
        {
            ad.Show(_ => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (finished || stopping || session != generation || earned) return;
                earned = true;
            }));
        }
        catch (Exception error)
        {
            Debug.LogWarning($"[Ads] Show failed: {error.Message}");
            Finish(true);
        }
    }
#else
    public static bool IsPrivacyOptionsRequired => false;
    public static void ShowPrivacyOptions(Action onClosed = null) => onClosed?.Invoke();
    public static bool IsReady =>
#if UNITY_EDITOR
        !stopping;
#else
        false;
#endif
    public static void ShowRewarded(Action<bool> onDone)
    {
#if UNITY_EDITOR
        if (IsShowing || stopping) { onDone?.Invoke(false); return; }
        Host.StartCoroutine(Simulate(onDone));
#else
        ShowUnavailable();
        onDone?.Invoke(false);
#endif
    }

#if UNITY_EDITOR
    private static IEnumerator Simulate(Action<bool> onDone)
    {
        IsShowing = true;
        yield return new WaitForSecondsRealtime(1f);
        IsShowing = false;
        onDone?.Invoke(true);
    }
#endif
#endif
}
