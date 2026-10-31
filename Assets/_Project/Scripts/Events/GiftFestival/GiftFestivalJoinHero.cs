using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusable, silent throw/swing demonstration for the shared Gear Cup invitation shell.</summary>
public sealed class GiftFestivalJoinHero : MonoBehaviour
{
    private GiftFestivalArt art;
    private Image bear, rabbit, ball;
    private float elapsed;
    private int bearPose = -1, rabbitPose = -1, ballFrame = -1;
    private bool focused = true;
    // Arena-local coordinates, measured from its bottom-left. The batter is in the
    // foreground; the smaller pitcher stands on the field a little farther back.
    private static readonly Vector2 BearFeet = new(175, 15), RabbitFeet = new(425, 125);
    private static readonly Vector2 Release = new(420, 330), Contact = new(210, 210), Destination = new(470, 485);

    public void Configure(GiftFestivalArt artwork)
    {
        art = artwork;
        rabbit = GiftFestivalUI.Picture("Rabbit", transform, null, new Vector2(230, 284), RabbitFeet);
        PlaceOnField(rabbit, RabbitFeet);
        bear = GiftFestivalUI.Picture("Bear", transform, null, new Vector2(350, 343), BearFeet);
        PlaceOnField(bear, BearFeet);
        ball = GiftFestivalUI.Picture("FlyingBall", transform, art.ball, Vector2.one * 38, Vector2.zero);
        ball.rectTransform.anchorMin = ball.rectTransform.anchorMax = Vector2.zero;
        ball.gameObject.SetActive(false);
        Apply(0);
    }

    private static void PlaceOnField(Image image, Vector2 feet)
    {
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = feet;
    }

    private void OnEnable()
    {
        elapsed = 0; focused = true;
        bearPose = rabbitPose = ballFrame = -1;
        if (art != null) Apply(0);
    }

    private void OnDisable()
    {
        elapsed = 0;
        if (ball != null) ball.gameObject.SetActive(false);
    }

    private void OnApplicationFocus(bool value) => focused = value;
    private void OnApplicationPause(bool paused) => focused = !paused;

    private void Update()
    {
        if (art == null || !focused) return;
        elapsed = (elapsed + Mathf.Min(Time.unscaledDeltaTime, 0.05f)) % 3.8f;
        Apply(elapsed - 0.5f);
    }

    private void Apply(float t)
    {
        bool idle = t < 0 || t >= 2.2f;
        SetRabbit(idle ? 0 : t < 0.12f ? 0 : t < 0.26f ? 1 : t < 0.38f ? 2 : t < 0.50f ? 3 : 4);
        SetBear(idle || t < 0.64f ? 0 : t < 0.74f ? 1 : t < 0.84f ? 2 : t < 0.96f ? 3 : t < 1.09f ? 4 : 5);
        bool flying = t >= 0.5f && t < 1.45f;
        if (ball.gameObject.activeSelf != flying) ball.gameObject.SetActive(flying);
        if (!flying) return;
        bool outbound = t >= 0.90f;
        float progress = outbound ? (t - 0.90f) / 0.55f : (t - 0.50f) / 0.40f;
        ball.rectTransform.anchoredPosition = Vector2.Lerp(outbound ? Contact : Release, outbound ? Destination : Contact, progress)
            + Vector2.up * (Mathf.Sin(progress * Mathf.PI) * 22);
        ball.rectTransform.localScale = Vector3.one * (outbound ? Mathf.Lerp(1, 0.6f, progress) : 1);
        int frame = art.ballSpin != null && art.ballSpin.Length > 0 ? (int)(t * 20) % art.ballSpin.Length : -1;
        if (frame == ballFrame) return;
        ballFrame = frame;
        ball.sprite = frame >= 0 ? art.ballSpin[frame] : art.ball;
    }

    private void SetBear(int pose)
    {
        if (pose == bearPose || art.bearFrames == null || art.bearFrames.Length == 0) return;
        bearPose = pose;
        bear.sprite = art.bearFrames[Mathf.Min(pose, art.bearFrames.Length - 1)];
        bear.enabled = bear.sprite != null;
    }

    private void SetRabbit(int pose)
    {
        if (pose == rabbitPose || art.rabbitFrames == null || art.rabbitFrames.Length == 0) return;
        rabbitPose = pose;
        rabbit.sprite = art.rabbitFrames[Mathf.Min(pose, art.rabbitFrames.Length - 1)];
        rabbit.enabled = rabbit.sprite != null;
        bool followThrough = pose >= 4;
        rabbit.rectTransform.sizeDelta = followThrough ? new Vector2(310, 284) : new Vector2(230, 284);
        rabbit.rectTransform.anchoredPosition = RabbitFeet;
    }
}
