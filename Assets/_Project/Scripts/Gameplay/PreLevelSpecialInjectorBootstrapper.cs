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
        if (RuntimeSimulationSession.IsActive) return;
        if (!PreLevelSpecialSelectionState.HasSelection)
            return;

        var existing = Object.FindAnyObjectByType<PreLevelSpecialRuntimeInjector>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        var userSelected = PreLevelSpecialSelectionState.GetSelectionSnapshot();
        var timedSpecials = PreLevelAutoSpecials.Collect();
        var combined = new List<TileSpecial>(timedSpecials);
        combined.AddRange(userSelected);

        Scene activeScene = SceneManager.GetActiveScene();
        var go = new GameObject("PreLevelSpecialInjector_Runtime");
        if (activeScene.IsValid())
            SceneManager.MoveGameObjectToScene(go, activeScene);

        go.AddComponent<PreLevelSpecialRuntimeInjector>().Initialize(combined);
    }
}
