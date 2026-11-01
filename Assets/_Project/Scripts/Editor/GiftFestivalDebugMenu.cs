using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GiftFestivalDebugMenu
{
    private const string Root = "TinyFixers/Debug/Gift Festival/";

    [MenuItem(Root + "Force Available (Editor Only)")]
    private static void ToggleForce()
    {
        var cfg = GiftFestivalConfig.Shared;
        if (cfg == null) return;
        Undo.RecordObject(cfg, "Toggle Gift Festival preview");
        cfg.debugForceAvailable = !cfg.debugForceAvailable;
        EditorUtility.SetDirty(cfg); AssetDatabase.SaveAssets();
    }
    [MenuItem(Root + "Force Available (Editor Only)", true)]
    private static bool ForceValidate()
    {
        Menu.SetChecked(Root + "Force Available (Editor Only)", GiftFestivalConfig.Shared != null && GiftFestivalConfig.Shared.debugForceAvailable);
        return GiftFestivalConfig.Shared != null;
    }

    [MenuItem(Root + "Open (Play)")]
    private static void Open()
    {
        var controller = GiftFestivalController.Install(SceneManager.GetActiveScene());
        if (controller != null) controller.Open();
    }
    [MenuItem(Root + "Open (Play)", true)]
    private static bool OpenValidate() => Application.isPlaying;

    [MenuItem(Root + "Preview (Play, Isolated)")]
    private static void Preview()
    {
        GiftFestivalState.DebugPreview = true;
        var controller = GiftFestivalController.Install(SceneManager.GetActiveScene());
        if (controller != null) controller.Open();
    }
    [MenuItem(Root + "Preview (Play, Isolated)", true)]
    private static bool PreviewValidate() => Application.isPlaying;

    [MenuItem(Root + "Add 10 Balls (Play)")]
    private static void AddBalls() => GiftFestivalState.DebugAddBalls(10);
    [MenuItem(Root + "Add 10 Balls (Play)", true)]
    private static bool BallsValidate() => Application.isPlaying;

    [MenuItem(Root + "Skip Lap Rest (Play)")]
    private static void SkipRest() => GiftFestivalState.DebugSkipLapRest();
    [MenuItem(Root + "Skip Lap Rest (Play)", true)]
    private static bool CanSkipRest() => Application.isPlaying;

    [MenuItem(Root + "Reset Event")]
    private static void Reset() => GiftFestivalState.DebugReset();

    [MenuItem("TinyFixers/Events/Gift Festival Settings")]
    private static void SelectConfig() => Selection.activeObject = GiftFestivalConfig.Shared;
}
