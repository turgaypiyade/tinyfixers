using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Fallback injector for direct scene loads that bypass the pre-level popup.
/// Normal path: popup's HandleContinueClicked calls EnsureForSelection with combined list.
/// This bootstrapper only runs when HasSelection is true (meaning popup set a selection)
/// and no injector was created yet — which shouldn't happen in the normal flow but
/// guards against edge cases (e.g. app killed and relaunched mid-level).
public static class PreLevelSpecialInjectorBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        if (!PreLevelSpecialSelectionState.HasSelection)
            return;

        var existing = Object.FindAnyObjectByType<PreLevelSpecialRuntimeInjector>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        var userSelected = PreLevelSpecialSelectionState.GetSelectionSnapshot();
        var timedSpecials = GetTimedSpecials();
        var combined = new List<TileSpecial>(timedSpecials);
        combined.AddRange(userSelected);

        Scene activeScene = SceneManager.GetActiveScene();
        var go = new GameObject("PreLevelSpecialInjector_Runtime");
        if (activeScene.IsValid())
            SceneManager.MoveGameObjectToScene(go, activeScene);

        go.AddComponent<PreLevelSpecialRuntimeInjector>().Initialize(combined);
    }

    private static List<TileSpecial> GetTimedSpecials()
    {
        var list = new List<TileSpecial>();
        if (TimedRewardService.IsActive(DailySlotRewardType.Joker_Line) ||
            TimedRewardService.IsActive(DailySlotRewardType.Joker_LineH))
            list.Add(TileSpecial.LineH);
        if (TimedRewardService.IsActive(DailySlotRewardType.Joker_PulseCore))
            list.Add(TileSpecial.PulseCore);
        if (TimedRewardService.IsActive(DailySlotRewardType.Joker_SystemOverride))
            list.Add(TileSpecial.SystemOverride);
        return list;
    }
}
