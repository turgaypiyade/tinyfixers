using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// BottomContent altindaki JokerGrid booster ikonlarini sabit kare boyutta tutar.
/// Slot/hit-area boyutuna dokunmadan, ikon RectTransform'unu merkezde 120x120 yapar.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class JokerIconSquareSizeEnforcer : MonoBehaviour
{
    private const string DefaultBottomContentName = "BottomContent";
    private const string DefaultJokerGridName = "JokerGrid";

    [Header("Target")]
    [SerializeField] private string jokerGridName = DefaultJokerGridName;

    [Header("Icon Size")]
    [SerializeField] private Vector2 iconSize = new Vector2(120f, 120f);
    [SerializeField] private bool centerIcons = true;
    [SerializeField] private bool preserveAspect = true;

    private static bool sceneHookRegistered;
    private bool applyScheduled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterInstaller()
    {
        if (sceneHookRegistered) return;
        sceneHookRegistered = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallInLoadedScene() => TryInstallInLoadedScene();

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstallInLoadedScene();

    private static void TryInstallInLoadedScene()
    {
        bool installedOnBottomContent = false;

        foreach (var tr in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!IsLiveSceneTransform(tr)) continue;
            if (tr.name != DefaultBottomContentName) continue;

            EnsureComponent(tr.gameObject);
            installedOnBottomContent = true;
        }

        // BottomContent yoksa JokerGrid'e direkt takil; prefab/scene varyantlarinda fallback olsun.
        if (installedOnBottomContent) return;

        foreach (var tr in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!IsLiveSceneTransform(tr)) continue;
            if (tr.name != DefaultJokerGridName) continue;

            EnsureComponent(tr.gameObject);
        }
    }

    private static bool IsLiveSceneTransform(Transform tr)
    {
        if (tr == null) return false;
        var go = tr.gameObject;
        if (go == null || !go.scene.IsValid() || !go.scene.isLoaded) return false;
        if ((go.hideFlags & HideFlags.HideInHierarchy) != 0) return false;
        return true;
    }

    private static void EnsureComponent(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<JokerIconSquareSizeEnforcer>() != null) return;
        go.AddComponent<JokerIconSquareSizeEnforcer>();
    }

    private void Awake() => ScheduleApply();
    private void OnEnable() => ScheduleApply();
    private void Start() => ScheduleApply();
    private void OnTransformChildrenChanged() => ScheduleApply();
    private void OnRectTransformDimensionsChange() => ScheduleApply();

    private void ScheduleApply()
    {
        if (!isActiveAndEnabled || applyScheduled) return;
        StartCoroutine(ApplyAfterLayout());
    }

    private IEnumerator ApplyAfterLayout()
    {
        applyScheduled = true;

        // Ilk frame: diger runtime installer'lar ve layout pass'leri once yerlessin.
        yield return null;
        ApplyNow();

        // EndOfFrame: Horizontal/Vertical/Grid Layout Group sonradan ezdiyse tekrar kareye cek.
        yield return new WaitForEndOfFrame();
        ApplyNow();

        applyScheduled = false;
    }

    public void ApplyNow()
    {
        var targetSize = SanitizedIconSize();
        foreach (var grid in ResolveJokerGrids())
            ApplyGrid(grid, targetSize);
    }

    private Vector2 SanitizedIconSize()
    {
        float width = Mathf.Max(1f, iconSize.x);
        float height = Mathf.Max(1f, iconSize.y);

        // Istek kare ikon oldugu icin, inspector'da farkli girilirse buyuk olani baz al.
        float side = Mathf.Max(width, height);
        return new Vector2(side, side);
    }

    private IEnumerable<Transform> ResolveJokerGrids()
    {
        if (transform.name == jokerGridName)
        {
            yield return transform;
            yield break;
        }

        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            if (tr == null || tr == transform) continue;
            if (tr.name == jokerGridName)
                yield return tr;
        }
    }

    private void ApplyGrid(Transform grid, Vector2 targetSize)
    {
        if (grid == null) return;

        for (int i = 0; i < grid.childCount; i++)
        {
            var slot = grid.GetChild(i);
            if (slot == null || IsIgnoredVisual(slot.name)) continue;

            var icon = FindIconImage(slot);
            if (icon == null) continue;

            ApplyIconSize(slot, icon, targetSize);
        }
    }

    private Image FindIconImage(Transform slot)
    {
        Image fallback = null;
        var images = slot.GetComponentsInChildren<Image>(true);

        foreach (var img in images)
        {
            if (img == null) continue;
            if (IsIgnoredVisual(img.gameObject.name)) continue;

            if (img.transform == slot)
            {
                fallback ??= img;
                continue;
            }

            return img;
        }

        return fallback;
    }

    private void ApplyIconSize(Transform slot, Image icon, Vector2 targetSize)
    {
        if (icon == null) return;

        var rt = icon.rectTransform;
        if (rt == null) return;

        if (centerIcons)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetSize.x);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSize.y);
        rt.sizeDelta = targetSize;

        icon.preserveAspect = preserveAspect;

        // Ikon slot'un kendi Image'i degilse, layout gruplari da ayni kare olcuyu tercih etsin.
        if (icon.transform != slot)
        {
            var layout = icon.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = targetSize.x;
                layout.preferredHeight = targetSize.y;
                layout.minWidth = targetSize.x;
                layout.minHeight = targetSize.y;
            }
        }
    }

    private static bool IsIgnoredVisual(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return false;

        string n = objectName.ToLowerInvariant();
        return n.Contains("selectionframe")
            || n.Contains("selectionglow")
            || n.Contains("jokerhitarea")
            || n.Contains("hitarea")
            || n.Contains("frame")
            || n.Contains("border")
            || n == "bg"
            || n.EndsWith("bg")
            || n.Contains("background");
    }
}
