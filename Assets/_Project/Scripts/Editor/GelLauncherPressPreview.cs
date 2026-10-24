using UnityEditor;
using UnityEngine;

/// <summary>Static IMGUI assembly of the runtime press art; creates no scene objects or textures.</summary>
internal static class GelLauncherPressPreview
{
    private static Texture2D frame, gel, head;
    private static readonly Rect GelUv = new(82f / 887f, 99f / 1774f, 730f / 887f, 995f / 1774f);
    private static readonly Rect HeadUv = new(65f / 887f, 339f / 1774f, 760f / 887f, 385f / 1774f);
    // Static shaft from the supplied PNG; the runtime version animates its stripes in a shader.
    private static readonly Rect RodUv = new(305f / 887f, 734f / 1774f, 275f / 887f, 990f / 1774f);

    internal static bool TryDraw(ObstacleId id, Rect bounds, float padding, int remainingHits)
    {
        if (id != ObstacleId.GelLauncherUp && id != ObstacleId.GelLauncherDown &&
            id != ObstacleId.GelLauncherLeft && id != ObstacleId.GelLauncherRight) return false;
        if (frame == null) frame = Resources.Load<Texture2D>("GelLauncher/Press/PressFrame");
        if (gel == null) gel = Resources.Load<Texture2D>("GelLauncher/Press/PressGel");
        if (head == null) head = Resources.Load<Texture2D>("GelLauncher/Press/PressHead");
        if (frame == null || gel == null || head == null) return false;
        if (Event.current.type != EventType.Repaint) return true;

        bool vertical = id == ObstacleId.GelLauncherUp || id == ObstacleId.GelLauncherDown;
        float availableWidth = Mathf.Max(0f, bounds.width - padding * 2f);
        float availableHeight = Mathf.Max(0f, bounds.height - padding * 2f);
        float width = vertical ? Mathf.Min(availableWidth, availableHeight * 0.5f)
            : Mathf.Min(availableHeight, availableWidth * 0.5f);
        if (width <= 0f) return true;
        var rig = new Rect(bounds.center.x - width * 0.5f, bounds.center.y - width, width, width * 2f);
        // IMGUI y points down; runtime RectTransform rotations use the opposite sign.
        float angle = id == ObstacleId.GelLauncherDown ? 180f : id == ObstacleId.GelLauncherRight ? 90f
            : id == ObstacleId.GelLauncherLeft ? -90f : 0f;
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        try
        {
            GUIUtility.RotateAroundPivot(angle, bounds.center);
            GUI.color = Color.white;
            // Match GelLauncherPressView's chamber anchors and initial / half-pressed pose.
            var chamber = new Rect(rig.x + rig.width * 0.205f, rig.y + rig.height * 0.12f,
                rig.width * 0.60f, rig.height * 0.745f);
            EditorGUI.DrawRect(chamber, new Color(0.10f, 0.025f, 0.21f, 0.94f));
            float progress = remainingHits == 1 ? 0.5f : 0f;
            float liquidHeight = chamber.height * Mathf.Lerp(0.63f, 0.14f, progress);
            var liquid = new Rect(chamber.x, chamber.yMax - liquidHeight, chamber.width, liquidHeight);
            GUI.DrawTextureWithTexCoords(liquid, gel, GelUv, true);

            float headWidth = chamber.width * 0.96f, headHeight = headWidth * 385f / 760f;
            float headBottom = liquidHeight - headHeight * 0.025f;
            var plate = new Rect(chamber.center.x - headWidth * 0.5f, chamber.yMax - headBottom - headHeight,
                headWidth, headHeight);
            float rodBottom = liquidHeight + headHeight * 0.92f;
            float rodHeight = Mathf.Max(1f, chamber.height - rodBottom + headHeight * 0.1f);
            var shaft = new Rect(chamber.center.x - chamber.width * 0.13f,
                chamber.yMax - rodBottom - rodHeight, chamber.width * 0.26f, rodHeight);
            DrawClipped(shaft, head, RodUv, chamber);
            DrawClipped(plate, head, HeadUv, chamber);
            EditorGUI.DrawRect(new Rect(chamber.x + chamber.width * 0.03f, chamber.y + chamber.height * 0.02f,
                chamber.width * 0.035f, chamber.height * 0.96f), new Color(1f, 0.91f, 1f, 0.12f));
            EditorGUI.DrawRect(new Rect(chamber.x + chamber.width * 0.96f, chamber.y + chamber.height * 0.02f,
                chamber.width * 0.018f, chamber.height * 0.96f), new Color(1f, 0.91f, 1f, 0.07f));
            GUI.DrawTextureWithTexCoords(rig, frame, new Rect(0f, 0f, 1f, 1f), true);
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }
        return true;
    }

    private static void DrawClipped(Rect rect, Texture2D texture, Rect uv, Rect clip)
    {
        float xMin = Mathf.Max(rect.xMin, clip.xMin), yMin = Mathf.Max(rect.yMin, clip.yMin);
        float xMax = Mathf.Min(rect.xMax, clip.xMax), yMax = Mathf.Min(rect.yMax, clip.yMax);
        if (xMin >= xMax || yMin >= yMax) return;
        var croppedUv = new Rect(uv.x + (xMin - rect.x) / rect.width * uv.width,
            uv.y + (rect.yMax - yMax) / rect.height * uv.height,
            (xMax - xMin) / rect.width * uv.width, (yMax - yMin) / rect.height * uv.height);
        GUI.DrawTextureWithTexCoords(new Rect(xMin, yMin, xMax - xMin, yMax - yMin), texture, croppedUv, true);
    }
}
