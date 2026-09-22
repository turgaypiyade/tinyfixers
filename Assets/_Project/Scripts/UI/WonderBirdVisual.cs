using UnityEngine;
using UnityEngine.UI;

/// <summary>Layered UI bird with slope-aware flap bursts and gliding. The agent owns the route.</summary>
public sealed class WonderBirdVisual : MonoBehaviour
{
    WonderBirdProfile _profile;
    Image _body, _leftWing, _rightWing;
    RectTransform _root;
    int _poseIndex = -1;
    enum FlightMode { Level, Climbing, Descending, Hovering }
    FlightMode _flightMode;
    float _levelTime, _wingFrame;
    bool _wasGliding;

    public float GlideAmount { get; private set; }

    public static WonderBirdVisual Create(RectTransform root, Image placeholder, WonderBirdProfile profile)
    {
        var bird = root.GetComponent<WonderBirdVisual>();
        if (bird == null) bird = root.gameObject.AddComponent<WonderBirdVisual>();
        bird._root = root;
        bird._profile = profile;
        if (placeholder != null) placeholder.enabled = false;
        // Wings behind the body hide the attachment seams.
        bird._leftWing = bird.GetOrCreateImage("BirdLeftWing");
        bird._rightWing = bird.GetOrCreateImage("BirdRightWing");
        bird._body = bird.GetOrCreateImage("BirdBody");
        bird._body.transform.SetAsLastSibling();
        bird._body.sprite = profile.body;
        bird._body.rectTransform.sizeDelta = root.rect.size;
        bird._body.preserveAspect = true;
        bird._poseIndex = -1;
        bird._flightMode = FlightMode.Level;
        bird._levelTime = 0f;
        bird._wingFrame = 0f;
        bird._wasGliding = true;
        bird.GlideAmount = 0f;
        bird.ApplyPose(profile.glidePoseIndex);
        return bird;
    }

    public void Animate(Vector2 movement, float deltaTime)
    {
        if (_profile == null || _profile.body == null) return;
        var poses = _profile.wingPoses;
        if (poses == null || poses.Length == 0) return;
        if (deltaTime <= 0f) return;

        // Use route movement, not the decorative bob, so the wings react to actual climbs/descents.
        float slope = movement.normalized.y;
        float threshold = Mathf.Sin(Mathf.Clamp(_profile.slopeThresholdDegrees, 1f, 45f) * Mathf.Deg2Rad);
        var mode = movement.sqrMagnitude < 0.00000001f ? FlightMode.Hovering
            : slope > threshold ? FlightMode.Climbing
            : slope < -threshold ? FlightMode.Descending
            : FlightMode.Level;
        if (mode != _flightMode)
        {
            _flightMode = mode;
            _levelTime = 0f;
        }

        float burst = Mathf.Max(0.05f, _profile.flapBurstDuration);
        float cycle = burst + Mathf.Max(0.05f, _profile.glideDuration);
        bool gliding = mode == FlightMode.Descending || (mode == FlightMode.Level && _levelTime >= burst);
        if (mode == FlightMode.Level) _levelTime = Mathf.Repeat(_levelTime + deltaTime, cycle);
        GlideAmount = Mathf.MoveTowards(GlideAmount, gliding ? 1f : 0f, deltaTime * 5f);

        int glidePose = Mathf.Clamp(_profile.glidePoseIndex, 0, poses.Length - 1);
        if (gliding)
        {
            ApplyPose(glidePose);
        }
        else
        {
            // Restart at the open-wing pose when leaving a glide, instead of an arbitrary frame.
            if (_wasGliding) _wingFrame = glidePose;
            float fps = Mathf.Max(0.1f, _profile.flapFps);
            if (mode == FlightMode.Climbing) fps *= Mathf.Max(1f, _profile.climbFlapMultiplier);
            _wingFrame = Mathf.Repeat(_wingFrame + deltaTime * fps, poses.Length);
            ApplyPose(Mathf.FloorToInt(_wingFrame));
        }
        _wasGliding = gliding;
    }

    void ApplyPose(int index)
    {
        if (_profile == null || _profile.body == null) return;
        var poses = _profile.wingPoses;
        if (poses == null || poses.Length == 0) return;
        index = Mathf.Clamp(index, 0, poses.Length - 1);
        if (_poseIndex == index) return;
        _poseIndex = index;
        var pose = poses[index];
        var size = _root.rect.size;
        var bodySize = _profile.body.rect.size;
        float scale = Mathf.Min(size.x / bodySize.x, size.y / bodySize.y) * _profile.wingScale;
        SetWing(_leftWing, pose.left, pose.leftPivot, _profile.leftShoulder, size, scale);
        SetWing(_rightWing, pose.right, pose.rightPivot, _profile.rightShoulder, size, scale);
    }

    static void SetWing(Image image, Sprite sprite, Vector2 pivot, Vector2 shoulder, Vector2 size, float scale)
    {
        image.sprite = sprite;
        image.enabled = sprite != null;
        if (sprite == null) return;
        var rt = image.rectTransform;
        rt.pivot = pivot;
        rt.anchoredPosition = Vector2.Scale(shoulder, size);
        rt.sizeDelta = sprite.rect.size * scale;
    }

    Image GetOrCreateImage(string childName)
    {
        var child = transform.Find(childName);
        if (child != null) return child.GetComponent<Image>();
        var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
}
