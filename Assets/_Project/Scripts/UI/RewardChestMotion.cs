using System.Collections;
using UnityEngine;

/// <summary>Shared anticipation for reward chests. One coroutine, no frame allocations or extra objects.</summary>
public static class RewardChestMotion
{
    public readonly struct Home
    {
        private readonly RectTransform target;
        private readonly Vector2 position;
        private readonly Vector3 scale;
        private readonly Quaternion rotation;

        public Home(RectTransform target)
        {
            this.target = target;
            position = target.anchoredPosition;
            scale = target.localScale;
            rotation = target.localRotation;
        }

        public void Apply(float progress)
        {
            if (target == null) return;
            Sample(progress, out Vector2 offset, out Vector2 stretch, out float angle);
            // Relative travel also works for small progress-bar chests and scaled canvases.
            float width = target.rect.width * Mathf.Abs(scale.x);
            target.anchoredPosition = position + offset * width;
            target.localScale = new Vector3(scale.x * stretch.x, scale.y * stretch.y, scale.z);
            target.localRotation = rotation * Quaternion.Euler(0f, 0f, angle);
        }

        public void Restore()
        {
            if (target == null) return;
            target.anchoredPosition = position;
            target.localScale = scale;
            target.localRotation = rotation;
        }
    }

    public static void Sample(float progress, out Vector2 offset, out Vector2 stretch, out float angle)
    {
        float t = Mathf.Clamp01(progress);
        float envelope = Mathf.Sin(t * Mathf.PI);
        // Two breaths with distinct width/height changes, rather than uniform scaling.
        float breath = Mathf.Sin(t * Mathf.PI * 4f);
        float pressure = t * t * 0.12f;
        float squash = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.83f, 1f, t));
        stretch = new Vector2(1f + pressure + breath * envelope * 0.12f + squash * 0.08f,
                              1f + pressure - breath * envelope * 0.08f - squash * 0.16f);
        // Alternating rocking accelerates toward the release; no random jitter spikes.
        float rock = Mathf.Sin((t * 4f + t * t * 4f) * Mathf.PI * 2f);
        float strength = envelope * Mathf.Lerp(0.35f, 1f, t);
        offset = new Vector2(rock * strength * 0.025f, Mathf.Abs(rock) * strength * 0.007f);
        angle = rock * strength * 9f;
    }

    public static IEnumerator Anticipate(RectTransform target, float duration = 1.05f)
    {
        if (target == null) yield break;
        var home = new Home(target);
        float elapsed = 0f;
        float seconds = Mathf.Max(0.1f, duration);
        try
        {
            while (elapsed < seconds && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                home.Apply(elapsed / seconds);
                yield return null;
            }
        }
        finally { home.Restore(); }
    }
}
