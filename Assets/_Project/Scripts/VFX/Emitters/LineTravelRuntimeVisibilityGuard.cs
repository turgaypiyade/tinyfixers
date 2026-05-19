using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime safety net for LineTravelSplitSwapTestUI instances.
///
/// Device builds can end up with the line-travel head Image GameObjects inactive
/// or alpha-zero while the trail beam still runs from their RectTransforms. When
/// that happens RocketTrailBeam is visible, but the actual LineTravel lightning
/// heads/ghosts are invisible. This guard only touches active line-travel players
/// whose Image components are currently enabled by Play().
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class LineTravelRuntimeVisibilityGuard : MonoBehaviour
{
    private static LineTravelRuntimeVisibilityGuard instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;

        var go = new GameObject("LineTravelRuntimeVisibilityGuard");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LineTravelRuntimeVisibilityGuard>();
    }

    private void LateUpdate()
    {
        var players = FindObjectsOfType<LineTravelSplitSwapTestUI>();
        for (int i = 0; i < players.Length; i++)
            ForceVisibleIfPlaying(players[i]);
    }

    private static void ForceVisibleIfPlaying(LineTravelSplitSwapTestUI player)
    {
        if (player == null || !player.gameObject.activeInHierarchy)
            return;

        bool leftPlaying = player.leftImage != null && player.leftImage.enabled;
        bool rightPlaying = player.rightImage != null && player.rightImage.enabled;

        if (!leftPlaying && !rightPlaying)
            return;

        bool changed = false;
        changed |= ForceImageVisible(player.leftImage, player.transform);
        changed |= ForceImageVisible(player.rightImage, player.transform);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (changed)
        {
            Debug.Log(
                $"[LineTravelVisibilityGuard] forcedVisible " +
                $"player={player.name} " +
                $"left={(player.leftImage != null ? player.leftImage.gameObject.activeInHierarchy.ToString() : "null")} " +
                $"right={(player.rightImage != null ? player.rightImage.gameObject.activeInHierarchy.ToString() : "null")}");
        }
#endif
    }

    private static bool ForceImageVisible(Image image, Transform ownerRoot)
    {
        if (image == null || !image.enabled)
            return false;

        bool changed = false;

        if (!image.gameObject.activeSelf)
        {
            image.gameObject.SetActive(true);
            changed = true;
        }

        image.raycastTarget = false;

        var color = image.color;
        if (color.a < 0.95f)
        {
            image.color = new Color(color.r, color.g, color.b, 1f);
            changed = true;
        }

        Transform t = image.transform;
        while (t != null && t != ownerRoot.parent)
        {
            if (t.TryGetComponent<CanvasGroup>(out var cg))
            {
                if (cg.alpha < 0.95f)
                {
                    cg.alpha = 1f;
                    changed = true;
                }

                cg.blocksRaycasts = false;
                cg.interactable = false;
            }

            if (t == ownerRoot)
                break;

            t = t.parent;
        }

        image.transform.SetAsLastSibling();
        return changed;
    }
}
