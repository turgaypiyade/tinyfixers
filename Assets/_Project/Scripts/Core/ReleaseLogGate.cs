using UnityEngine;

/// <summary>
/// Release filter for Unity/SDK informational output. Project diagnostic calls are separately
/// controlled by Resources/GameDiagnostics.asset, including their warnings and errors.
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
