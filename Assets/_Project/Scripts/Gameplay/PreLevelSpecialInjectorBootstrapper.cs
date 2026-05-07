using UnityEngine;
using UnityEngine.SceneManagement;

public static class PreLevelSpecialInjectorBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        if (!PreLevelSpecialSelectionState.HasSelection)
            return;

        var existing = Object.FindObjectOfType<PreLevelSpecialRuntimeInjector>(true);
        if (existing != null)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        var go = new GameObject("PreLevelSpecialInjector_Runtime");
        if (activeScene.IsValid())
            SceneManager.MoveGameObjectToScene(go, activeScene);

        go.AddComponent<PreLevelSpecialRuntimeInjector>();
    }
}
