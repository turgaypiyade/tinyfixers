using UnityEngine;

/// <summary>Bounded two-hit presentation state. No board mutation or per-step allocation.</summary>
public sealed class GelLauncherPressMotion
{
    public float Progress { get; private set; }
    public float Phase { get; private set; }
    public bool Moving { get; private set; }
    private float from, target, elapsed, duration, turns;

    public void Reset(bool damaged)
    {
        Progress = damaged ? 0.5f : 0f;
        Phase = 0f;
        Moving = false;
        elapsed = 0f;
    }

    public void MoveTo(float value, float seconds, float revolutions)
    {
        from = Progress;
        target = Mathf.Clamp01(value);
        elapsed = 0f;
        duration = Mathf.Max(0.01f, seconds);
        turns = revolutions;
        Moving = !Mathf.Approximately(from, target);
    }

    public void Step(float delta)
    {
        if (!Moving || delta <= 0f) return;
        float previous = Mathf.Clamp01(elapsed / duration);
        elapsed = Mathf.Min(duration, elapsed + delta);
        float k = elapsed / duration;
        float eased = k * k * (3f - 2f * k);
        Progress = Mathf.LerpUnclamped(from, target, eased);
        Phase = Mathf.Repeat(Phase + (k - previous) * turns, 1f);
        if (elapsed >= duration) { Progress = target; Moving = false; }
    }

    public void Cancel() => Moving = false;
}
