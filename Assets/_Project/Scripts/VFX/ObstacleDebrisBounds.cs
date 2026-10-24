using UnityEngine;

/// <summary>Horizontal board envelope for obstacle debris, in the chosen FX parent's local units.</summary>
internal struct ObstacleDebrisBounds
{
    internal bool valid;
    internal float left, right;

    internal static ObstacleDebrisBounds For(BoardController board, RectTransform parent)
    {
        if (board == null || parent == null || board.Width < 1 || board.TileSize < 1) return default;
        float a = parent.InverseTransformPoint(board.GetCellWorldPosition(0, 0)).x;
        float b = parent.InverseTransformPoint(board.GetCellWorldPosition(board.Width, 0)).x;
        return new ObstacleDebrisBounds { valid = true, left = Mathf.Min(a, b), right = Mathf.Max(a, b) };
    }

    /// <summary>Fit the complete rotating piece, then turn an outward trajectory inward.</summary>
    internal void Fit(ref Vector2 center, ref Vector2 travel, ref Vector2 size, float life, bool eased = false)
    {
        if (!valid) return;
        // A bounding circle includes corners at every spin angle, not just the unrotated width.
        float radius = size.magnitude * 0.5f;
        float maxRadius = (right - left) * 0.49f;
        if (radius > maxRadius && radius > 0f)
        {
            size *= maxRadius / radius;
            radius = maxRadius;
        }
        float lo = left + radius, hi = right - radius;
        center.x = Mathf.Clamp(center.x, lo, hi);
        float seconds = eased ? 1f : Mathf.Max(0.05f, life);
        float dx = travel.x * seconds;
        if ((dx > 0f && center.x + dx > hi) || (dx < 0f && center.x + dx < lo)) dx = -dx;
        dx = Mathf.Clamp(dx, lo - center.x, hi - center.x);
        travel.x = dx / seconds;
    }

    internal Vector2 ClampCenter(Vector2 center, Vector2 size)
    {
        if (!valid) return center;
        float radius = Mathf.Min(size.magnitude * 0.5f, (right - left) * 0.49f);
        center.x = Mathf.Clamp(center.x, left + radius, right - radius);
        return center;
    }

    internal ObstacleDebrisBounds WithAnchorOffset(float offset)
        => new ObstacleDebrisBounds { valid = valid, left = left - offset, right = right - offset };
}
