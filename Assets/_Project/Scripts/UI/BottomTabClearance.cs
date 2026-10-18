using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main-menu layout spacer for the bottom tab bar. The tab bar is anchored to the full canvas bottom while the
/// menu layout lives in the SafeArea; on iPhones the home-indicator inset hides the difference, but on phones
/// without a bottom inset (most Android, 16:9) the authored 190 spacer left the Level/Missions buttons behind
/// the 250-tall bar. The spacer becomes max(authored, bar top above the safe-area bottom + gap) — iPhone keeps
/// its authored look. Recomputed only when the screen or safe area changes.
/// </summary>
[RequireComponent(typeof(LayoutElement))]
public sealed class BottomTabClearance : MonoBehaviour
{
    [SerializeField] private RectTransform tabBar;
    [Tooltip("Space kept between the content above and the top edge of the tab bar (layout units).")]
    [SerializeField, Min(0f)] private float gap = 15f;

    private LayoutElement element;
    private float authoredHeight = -1f;
    private Rect lastSafeArea;
    private Vector2Int lastScreen;
    private readonly Vector3[] corners = new Vector3[4];

    private void Awake()
    {
        element = GetComponent<LayoutElement>();
        authoredHeight = element.preferredHeight;
    }

    private void OnEnable() => lastScreen = Vector2Int.zero;   // force a refresh

    private void LateUpdate()
    {
        // SafeAreaFitter applies at end of frame; checking here (cheap struct compares) follows it.
        var screen = new Vector2Int(Screen.width, Screen.height);
        if (screen == lastScreen && Screen.safeArea == lastSafeArea) return;
        lastScreen = screen;
        lastSafeArea = Screen.safeArea;
        Refresh();
    }

    private void Refresh()
    {
        var parent = transform.parent as RectTransform;
        if (tabBar == null || parent == null || element == null) return;
        parent.GetWorldCorners(corners);
        float safeBottom = corners[0].y;
        tabBar.GetWorldCorners(corners);
        float barTop = corners[1].y;
        float scale = Mathf.Abs(parent.lossyScale.y);
        if (scale <= 0f) return;
        float needed = (barTop - safeBottom) / scale + gap;
        float height = Mathf.Max(authoredHeight, needed);
        if (!Mathf.Approximately(element.preferredHeight, height))
        {
            element.preferredHeight = height;
            LayoutRebuilder.MarkLayoutForRebuild(parent);
        }
    }
}
