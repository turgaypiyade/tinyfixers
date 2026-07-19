using UnityEngine;

/// <summary>
/// Launch hijyeni (Docs/ProductionPlan.md P5): RELEASE build'de Debug.Log seli kapatılır —
/// projede yüzlerce ayrıntılı log var (special zinciri, cloud save, workshop...);
/// cihazda hem maliyet hem gürültü. Warning/Error AÇIK kalır (Crashlytics gelince
/// breadcrumb olarak da onlar değerli). Editor ve Development Build'de her şey açık.
/// </summary>
public static class ReleaseLogGate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Apply()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        Debug.unityLogger.filterLogType = LogType.Warning;   // Log kapalı; Warning+Error açık
#endif
    }
}
