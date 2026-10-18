using UnityEngine;

[CreateAssetMenu(fileName = "GameDiagnostics", menuName = "TinyFixers/Diagnostics Settings")]
public sealed class GameDiagnosticsSettings : ScriptableObject
{
    [Tooltip("Runtime warnings, missing references and caught exceptions.")]
    public bool criticalErrors;
    [Tooltip("Board timing, resolve and fall traces (Editor / Development build).")]
    public bool boardFlow;
    [Tooltip("Expensive GridData / TileView snapshots and development integrity validation.")]
    public bool boardIntegrity;
    [Tooltip("Special chain scheduling and dispatch diagnostics.")]
    public bool specialChains;
    [Tooltip("Frame / managed-heap sampling and spike reports.")]
    public bool performance;
    [Tooltip("Cloud save, account, purchase and network failure diagnostics.")]
    public bool backend;
    [Tooltip("Level start/end payload and SDK dispatch logs, only in Editor / Development builds.")]
    public bool analyticsEvents;
    [Tooltip("Editor command, import and simulation reports.")]
    public bool editorTools;
}

/// <summary>Call-site switches: disabled logs do not format strings or evaluate diagnostic arguments.</summary>
public static class GameDiagnostics
{
    private static GameDiagnosticsSettings current;
    private static bool loaded;

    private static GameDiagnosticsSettings Settings
    {
        get
        {
            if (!loaded)
            {
                current = Resources.Load<GameDiagnosticsSettings>("GameDiagnostics");
                loaded = true;
            }
            return current;
        }
    }

    public static bool CriticalErrors => Settings != null && Settings.criticalErrors;
    public static bool BoardFlow => Settings != null && Settings.boardFlow;
    public static bool BoardIntegrity => Settings != null && Settings.boardIntegrity;
    public static bool SpecialChains => Settings != null && Settings.specialChains;
    public static bool Performance => Settings != null && Settings.performance;
    public static bool Backend => Settings != null && Settings.backend;
    public static bool AnalyticsEvents => Settings != null && Settings.analyticsEvents;
    public static bool EditorTools => Settings != null && Settings.editorTools;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        current = null;
        loaded = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Warmup() { _ = Settings; }
}
