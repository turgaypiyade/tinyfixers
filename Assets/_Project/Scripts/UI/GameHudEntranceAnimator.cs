using UnityEngine;

/// <summary>Slides HUD visuals with the board while keeping its layout anchors stationary.</summary>
public sealed class GameHudEntranceAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform topVisuals;
    [SerializeField] private RectTransform bottomVisuals;
    [SerializeField, Min(0f)] private float offscreenMargin = 32f;

    private sealed class Panel
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 Home;
        public Vector2 Start;
        public float Alpha;
        public bool Interactable;
        public bool BlocksRaycasts;
    }

    private Panel top;
    private Panel bottom;
    private bool sliding;
    private bool completed;

    private void Awake()
    {
        // Hide before the first rendered frame, but leave layout in its final position
        // until GridSpawner and the boss layout have measured the board's available space.
        top = Hide(topVisuals);
        bottom = Hide(bottomVisuals);
    }

    private static Panel Hide(RectTransform rect)
    {
        if (rect == null) return null;
        var group = rect.GetComponent<CanvasGroup>();
        if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
        var panel = new Panel
        {
            Rect = rect, Group = group, Home = rect.anchoredPosition,
            Alpha = group.alpha, Interactable = group.interactable,
            BlocksRaycasts = group.blocksRaycasts
        };
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return panel;
    }

    public void BeginSlide()
    {
        if (!isActiveAndEnabled || completed) return;
        Canvas.ForceUpdateCanvases();
        Prepare(top, true);
        Prepare(bottom, false);
        sliding = true;
    }

    private void Prepare(Panel panel, bool fromTop)
    {
        if (panel == null || panel.Rect == null) return;
        var rect = panel.Rect;
        panel.Home = rect.anchoredPosition;
        var canvas = rect.GetComponentInParent<Canvas>();
        var viewport = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        Vector3 offset;
        if (viewport != null)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, rect);
            float distance = fromTop
                ? viewport.rect.yMax - bounds.min.y + offscreenMargin
                : viewport.rect.yMin - bounds.max.y - offscreenMargin;
            offset = rect.parent.InverseTransformVector(viewport.TransformVector(Vector3.up * distance));
        }
        else
        {
            offset = Vector3.up * (rect.rect.height + offscreenMargin) * (fromTop ? 1f : -1f);
        }
        panel.Start = panel.Home + new Vector2(offset.x, offset.y);
        rect.anchoredPosition = panel.Start;
        panel.Group.alpha = panel.Alpha;
    }

    // Receives the board's eased progress so all three entrances finish together.
    public void SetProgress(float easedProgress)
    {
        if (!sliding) return;
        Move(top, easedProgress);
        Move(bottom, easedProgress);
    }

    private static void Move(Panel panel, float progress)
    {
        if (panel != null && panel.Rect != null)
            panel.Rect.anchoredPosition = Vector2.LerpUnclamped(panel.Start, panel.Home, progress);
    }

    public void Complete()
    {
        Restore(top);
        Restore(bottom);
        sliding = false;
        completed = true;
    }

    private static void Restore(Panel panel)
    {
        if (panel == null || panel.Rect == null) return;
        panel.Rect.anchoredPosition = panel.Home;
        panel.Group.alpha = panel.Alpha;
        panel.Group.interactable = panel.Interactable;
        panel.Group.blocksRaycasts = panel.BlocksRaycasts;
    }

    private void OnDisable() => Complete();
}
