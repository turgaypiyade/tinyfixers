using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime safety net for LineTravelSplitSwapTestUI instances.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class LineTravelRuntimeVisibilityGuard : MonoBehaviour
{
    private static LineTravelRuntimeVisibilityGuard instance;
    private static Sprite fallbackSprite;

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
        changed |= ForceRectOnCanvasPlane(player.transform);
        changed |= ForceImageVisible(player.leftImage, player.transform);
        changed |= ForceImageVisible(player.rightImage, player.transform);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (changed)
        {
            var rt = player.transform as RectTransform;
            Debug.Log(
                $"[LineTravelVisibilityGuard] forcedVisible player={player.name} " +
                $"localZ={(rt != null ? rt.localPosition.z.ToString("0.###") : "n/a")} " +
                $"left={(player.leftImage != null ? player.leftImage.gameObject.activeInHierarchy.ToString() : "null")} " +
                $"right={(player.rightImage != null ? player.rightImage.gameObject.activeInHierarchy.ToString() : "null")}");
        }
#endif
    }

    private static bool ForceRectOnCanvasPlane(Transform target)
    {
        var rt = target as RectTransform;
        if (rt == null)
            return false;

        bool changed = false;
        Vector3 lp = rt.localPosition;
        if (Mathf.Abs(lp.z) > 0.001f)
        {
            rt.localPosition = new Vector3(lp.x, lp.y, 0f);
            changed = true;
        }

        Vector3 ls = rt.localScale;
        if (ls.z != 1f)
        {
            rt.localScale = new Vector3(ls.x, ls.y, 1f);
            changed = true;
        }

        target.SetAsLastSibling();
        return changed;
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
        image.maskable = false;
        image.material = null;
        image.canvasRenderer.cull = false;
        image.canvasRenderer.SetAlpha(1f);

        if (image.sprite == null)
        {
            image.sprite = GetFallbackSprite();
            image.type = Image.Type.Simple;
            changed = true;
        }

        var rt = image.rectTransform;
        Vector3 lp = rt.localPosition;
        if (Mathf.Abs(lp.z) > 0.001f)
        {
            rt.localPosition = new Vector3(lp.x, lp.y, 0f);
            changed = true;
        }

        if (rt.sizeDelta.x < 8f || rt.sizeDelta.y < 8f)
        {
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 48f), Mathf.Max(rt.sizeDelta.y, 48f));
            changed = true;
        }

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

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        fallbackSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        fallbackSprite.name = "LineTravelFallbackWhiteSprite";
        return fallbackSprite;
    }
}
