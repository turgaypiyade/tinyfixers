using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Merkezi dokunsal geri bildirim (titreşim). Tüm titreşim buradan geçer ve GameSettings.VibrationEnabled'a uyar.
/// Line hücre kırılışı ve başarılı engel isabeti hafif; Pulse patlaması ve combo başlangıcı tek vurgu.
/// Eşzamanlı/hızlı istekler merkezi süre kapısıyla birleşir; normal match/düşüş/menü titremez.
/// iOS: UIImpactFeedbackGenerator (Plugins/iOS/TFHaptics.mm); Android: VibrationEffect (API 29+ hazır efekt,
/// 26-28 kısa genlikli titreşim, eski cihaz Handheld.Vibrate). Editörde no-op.
/// </summary>
public static class Haptics
{
    public enum Strength { Light = 0, Medium = 1, Heavy = 2 }

    // Aynı anda gelen birden çok istek tek titreşim olsun (kombo + çekiç aynı karede vb.).
    private const float MinInterval = 0.06f;
    private static float lastTime = -1f;
    private static bool settingsBound, vibrationEnabled;

    private static bool IsVibrationEnabled
    {
        get
        {
            if (!settingsBound) BindSettings();
            return vibrationEnabled;
        }
    }

    private static void BindSettings()
    {
        vibrationEnabled = GameSettings.VibrationEnabled;
        GameSettings.OnVibrationChanged -= HandleSettingChanged;
        GameSettings.OnVibrationChanged += HandleSettingChanged;
        settingsBound = true;
    }

    public static void Play(Strength strength)
    {
        if (!IsVibrationEnabled)
            return;
        float now = Time.unscaledTime;
        if (lastTime >= 0f && now - lastTime < MinInterval) return;
        lastTime = now;
        PlatformPlay(strength);
    }

    public static void OnLineCellBreak() => Play(Strength.Light);
    public static void OnObstacleHit() => Play(Strength.Light);

    /// <summary>One opening accent. Override implants never add their own repeated taps.</summary>
    public static void PlayCombo(TileSpecial a, TileSpecial b)
    {
        bool big = a == TileSpecial.PulseCore || b == TileSpecial.PulseCore
                || a == TileSpecial.SystemOverride || b == TileSpecial.SystemOverride;
        Play(big ? Strength.Heavy : Strength.Medium);

        if (a == TileSpecial.SystemOverride || b == TileSpecial.SystemOverride)
        {
            sessionStart = sessionUntil = -100f;
            return;
        }
        float now = Time.unscaledTime;
        sessionStart = now;
        sessionUntil = now + SessionIdle;
    }

    // Kombo oturumu: son kombo anından SessionIdle sn sonra ya da en fazla SessionMax sn sonra kapanır.
    // Only PatchBot takeoff uses the combo session; real explosions/cell hits are independent.
    private const float SessionIdle = 1.5f, SessionMax = 10f;
    private static float sessionStart = -100f, sessionUntil = -100f;

    /// <summary>Legacy visual hooks: real Pulse explosions and combo PatchBot takeoffs only.
    /// Implant/launch notifications remain compatible, but impacts own their own feedback.</summary>
    public enum ComboMoment { Implant, PulseBlast, RocketLaunch, PatchBotTakeoff }

    public static void OnComboMoment(ComboMoment moment)
    {
        // Pulse is an actual visible explosion, including solo use. Line taps are
        // emitted by cell clears; an Override implant/rocket launch is not an impact.
        if (moment == ComboMoment.PulseBlast) { Play(Strength.Heavy); return; }
        if (moment != ComboMoment.PatchBotTakeoff) return;
        float now = Time.unscaledTime;
        if (now > sessionUntil || now - sessionStart > SessionMax) return;
        sessionUntil = Mathf.Min(now + SessionIdle, sessionStart + SessionMax);
        Play(Strength.Medium);
    }

    // Bomba patlaması görsel olayı birkaç yoldan yayınlanıyor (PulseBehaviorEvents); tek abonelik yeter.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void HookVisualEvents()
    {
        PulseBehaviorEvents.PulseExplosionPlayed -= HandlePulseExplosion;
        PulseBehaviorEvents.PulseExplosionPlayed += HandlePulseExplosion;
        BindSettings();
        EndBoard();
    }

    public static void EndBoard()
    {
        sessionStart = sessionUntil = -100f;
        lastTime = -1f;
        PlatformCancel();
    }

    private static void HandleSettingChanged(bool enabled)
    {
        vibrationEnabled = enabled;
        if (!enabled) EndBoard();
    }

    private static void HandlePulseExplosion(Vector2Int _) => OnComboMoment(ComboMoment.PulseBlast);

    /// Sürekli titreşim (roket kombosu gibi). intensity 0-1, duration sn.
    public static void PlayRumble(float intensity, float duration)
    {
        if (!IsVibrationEnabled || duration <= 0f) return;
        PlatformRumble(Mathf.Clamp01(intensity), duration);
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void TF_HapticImpact(int style);
    [DllImport("__Internal")] private static extern void TF_HapticContinuous(float intensity, float duration);
    [DllImport("__Internal")] private static extern void TF_HapticCancel();
    private static void PlatformCancel() => TF_HapticCancel();

    private static void PlatformPlay(Strength strength) => TF_HapticImpact((int)strength);
    private static void PlatformRumble(float intensity, float duration) => TF_HapticContinuous(intensity, duration);

#elif UNITY_ANDROID && !UNITY_EDITOR
    // Android VibrationEffect sabitleri.
    private const int EffectClick = 0, EffectTick = 2, EffectHeavyClick = 5;
    private static AndroidJavaObject vibrator;
    private static AndroidJavaClass effectClass;
    private static int sdk = -1;
    private static readonly AndroidJavaObject[] impactEffects = new AndroidJavaObject[3];
    // Reuse the JNI argument array too; gameplay calls run synchronously on Unity's main thread.
    private static readonly object[] impactArgs = new object[1];
    private static void PlatformCancel()
    {
        try { vibrator?.Call("cancel"); } catch (System.Exception) { }
    }

    private static void PlatformPlay(Strength strength)
    {
        try
        {
            if (sdk < 0)
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (sdk >= 26) effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
            if (vibrator == null) return;

            if (sdk >= 26)
            {
                int index = (int)strength;
                var effect = impactEffects[index];
                if (effect == null)
                {
                    if (sdk >= 29)
                    {
                        int id = strength == Strength.Heavy ? EffectHeavyClick : strength == Strength.Medium ? EffectClick : EffectTick;
                        effect = effectClass.CallStatic<AndroidJavaObject>("createPredefined", id);
                    }
                    else
                    {
                        long ms = strength == Strength.Heavy ? 35L : strength == Strength.Medium ? 22L : 12L;
                        int amplitude = strength == Strength.Heavy ? 255 : strength == Strength.Medium ? 170 : 90;
                        effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
                    }
                    impactEffects[index] = effect;
                }
                impactArgs[0] = effect;
                vibrator.Call("vibrate", impactArgs);
            }
            else
            {
                Handheld.Vibrate();   // eski cihaz: tek tip titreşim (VIBRATE iznini de Unity buradan ekler)
            }
        }
        catch (System.Exception)
        {
            vibrator = null;   // cihaz desteklemiyor: sessizce kapat
        }
    }

    private static void PlatformRumble(float intensity, float duration)
    {
        try
        {
            if (sdk < 0) PlatformPlay(Strength.Light);   // ilk kurulum (vibrator/effect sınıfı)
            if (vibrator == null || sdk < 26) return;
            long ms = (long)(duration * 1000f);
            int amplitude = Mathf.Clamp(Mathf.RoundToInt(intensity * 255f), 1, 255);
            using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                vibrator.Call("vibrate", effect);
        }
        catch (System.Exception)
        {
            vibrator = null;
        }
    }

#else
    private static void PlatformPlay(Strength strength) { }
    private static void PlatformRumble(float intensity, float duration) { }
    private static void PlatformCancel() { }
#endif
}
