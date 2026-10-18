using UnityEngine;
using UnityEngine.UI;

/// <summary>One visible-screen update loop, cached sprites and two reusable orbit layers.</summary>
public sealed class GearCupHeroAnimator : MonoBehaviour
{
    [SerializeField] private GearCupPreviewConfig config;
    [SerializeField] private Image rabbit;
    [SerializeField] private RectTransform breathingRoot;
    [SerializeField] private Image pig;
    [SerializeField] private RectTransform pigBreathingRoot;
    [SerializeField] private Image orbitBack;
    [SerializeField] private Image orbitFront;
    [SerializeField] private Image lightRing;
    [SerializeField] private Vector2 orbitCenter = new Vector2(-20f, 50f);
    [SerializeField] private Vector2 orbitRadius = new Vector2(245f, 80f);

    private Vector2 rabbitHome;
    private Vector3 breathingScale;
    private Vector2 pigHome;
    private Vector3 pigBreathingScale;
    private float elapsed;
    private int shownFrame = -1;
    private int shownPigFrame = -1;
    private bool captured;
    private bool pigCaptured;
    private bool suspended;

    public void Configure(GearCupPreviewConfig value, Image character, RectTransform breathing,
        Image back, Image front, Image ring, Image pigCharacter, RectTransform pigBreathing)
    {
        config = value;
        rabbit = character;
        breathingRoot = breathing;
        pig = pigCharacter;
        pigBreathingRoot = pigBreathing;
        orbitBack = back;
        orbitFront = front;
        lightRing = ring;
        captured = false;
        pigCaptured = false;
    }

    public void ConfigureOrbit(Vector2 center, Vector2 radius)
    {
        orbitCenter = center;
        orbitRadius = radius;
    }

    private void OnEnable()
    {
        if (!captured && rabbit != null && breathingRoot != null)
        {
            rabbitHome = rabbit.rectTransform.anchoredPosition;
            breathingScale = breathingRoot.localScale;
            captured = true;
        }
        if (!pigCaptured && pig != null && pigBreathingRoot != null)
        {
            pigHome = pig.rectTransform.anchoredPosition;
            pigBreathingScale = pigBreathingRoot.localScale;
            pigCaptured = true;
        }
        elapsed = 0f;
        shownFrame = -1;
        shownPigFrame = -1;
        ApplyFrame(0f);
    }

    private void OnDisable()
    {
        if (captured && breathingRoot != null) breathingRoot.localScale = breathingScale;
        if (captured && rabbit != null) rabbit.rectTransform.anchoredPosition = rabbitHome;
        if (pigCaptured && pigBreathingRoot != null) pigBreathingRoot.localScale = pigBreathingScale;
        if (pigCaptured && pig != null) pig.rectTransform.anchoredPosition = pigHome;
        shownFrame = -1;
        shownPigFrame = -1;
    }

    private void OnApplicationPause(bool paused) => suspended = paused;

    private void Update()
    {
        if (suspended || config == null || !captured) return;
        float cycle = Mathf.Max(3f, config.animationCycleSeconds);
        elapsed = Mathf.Repeat(elapsed + Mathf.Min(Time.unscaledDeltaTime, 0.05f), cycle);
        ApplyFrame(elapsed / cycle);
    }

    private void ApplyFrame(float phase)
    {
        if (config == null || !captured) return;
        int frame = FrameAt(phase);
        if (frame != shownFrame && rabbit != null && config.rabbitFrames != null)
        {
            var sprite = frame < config.rabbitFrames.Length ? config.rabbitFrames[frame] : null;
            if (sprite == null && config.rabbitFrames.Length > 0) sprite = config.rabbitFrames[0];
            rabbit.sprite = sprite;
            rabbit.enabled = sprite != null;
            float correction = config.rabbitFootY != null && frame < config.rabbitFootY.Length
                ? (config.rabbitFootY[frame] - config.rabbitFootY[0]) / 1536f * rabbit.rectTransform.rect.height : 0f;
            // Unity Y points up; the source PNG's Y points down. Lower the lifted-foot frame.
            rabbit.rectTransform.anchoredPosition = rabbitHome + Vector2.up * correction;
            shownFrame = frame;
        }

        float breath = Mathf.Sin(phase * Mathf.PI * 4f);
        breathingRoot.localScale = new Vector3(breathingScale.x * (1f - breath * 0.0015f),
            breathingScale.y * (1f + breath * 0.0035f), breathingScale.z);
        ApplyPigFrame(phase);

        float angle = phase * Mathf.PI * 2f - 1.35f;
        float depth = Mathf.Sin(angle);
        bool front = depth < 0f;
        if (orbitFront != null && orbitFront.enabled != front) orbitFront.enabled = front;
        if (orbitBack != null && orbitBack.enabled == front) orbitBack.enabled = !front;
        var gear = front ? orbitFront : orbitBack;
        if (gear != null)
        {
            var rt = gear.rectTransform;
            rt.anchoredPosition = orbitCenter + new Vector2(Mathf.Cos(angle) * orbitRadius.x, depth * orbitRadius.y);
            rt.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.06f, (1f - depth) * 0.5f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -phase * 720f);
        }
        if (lightRing != null)
        {
            var color = lightRing.color;
            color.a = 0.78f + 0.14f * Mathf.Sin(phase * Mathf.PI * 4f);
            lightRing.color = color;
        }
    }

    private void ApplyPigFrame(float phase)
    {
        if (!pigCaptured || pig == null || pigBreathingRoot == null) return;
        int frame = PigFrameAt(phase);
        if (frame != shownPigFrame && config.pigFrames != null)
        {
            var sprite = frame < config.pigFrames.Length ? config.pigFrames[frame] : null;
            if (sprite == null && config.pigFrames.Length > 0) sprite = config.pigFrames[0];
            pig.sprite = sprite;
            pig.enabled = sprite != null;
            float correction = config.pigFootY != null && frame < config.pigFootY.Length
                ? (config.pigFootY[frame] - config.pigFootY[0]) / 1536f * pig.rectTransform.rect.height : 0f;
            pig.rectTransform.anchoredPosition = pigHome + Vector2.up * correction;
            shownPigFrame = frame;
        }
        // One shared loop; a small continuous breath offsets the held poses without crossfade ghosts.
        // The foot pivot remains fixed and both axes scale together to preserve proportions.
        float breath = 1f + Mathf.Sin((phase * 2f + 0.35f) * Mathf.PI * 2f) * 0.003f;
        pigBreathingRoot.localScale = new Vector3(pigBreathingScale.x * breath,
            pigBreathingScale.y * breath, pigBreathingScale.z);
    }

    // Cheer after the rabbit reaches for the gear, then reuse the transition poses on the way down.
    public static int PigFrameAt(float phase)
    {
        if (phase >= 0.580f && phase < 0.620f) return 1;
        if (phase >= 0.620f && phase < 0.660f) return 2;
        if (phase >= 0.660f && phase < 0.740f) return 3;
        if (phase >= 0.740f && phase < 0.780f) return 2;
        if (phase >= 0.780f && phase < 0.820f) return 1;
        return 0;
    }

    // Key poses are held as in the approved source preview; no whole-character crossfade/ghosting.
    public static int FrameAt(float phase)
    {
        if (phase >= 0.225f && phase < 0.235f) return 1;
        if (phase >= 0.235f && phase < 0.250f) return 2;
        if (phase >= 0.250f && phase < 0.260f) return 1;
        if (phase >= 0.350f && phase < 0.385f) return 3;
        if (phase >= 0.385f && phase < 0.420f) return 4;
        if (phase >= 0.420f && phase < 0.450f) return 5;
        if (phase >= 0.450f && phase < 0.490f) return 6;
        if (phase >= 0.490f && phase < 0.515f) return 5;
        if (phase >= 0.515f && phase < 0.545f) return 4;
        if (phase >= 0.545f && phase < 0.580f) return 3;
        return 0;
    }
}
