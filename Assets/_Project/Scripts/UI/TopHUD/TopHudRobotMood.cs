using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TopHudRobotMood : MonoBehaviour
{
    public enum Mood
    {
        Idle,
        Happy,
        Sad,
        Excited
    }

    [Header("References")]
    [SerializeField] private Image robotImage;

    [Header("Character (optional; legacy sprites remain the fallback)")]
    [SerializeField] private TopHudPortraitProfile portraitProfile;

    [Header("Sprites")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite happySprite;
    [SerializeField] private Sprite sadSprite;
    [SerializeField] private Sprite excitedSprite;

    [Header("Animation")]
    [SerializeField] private bool animateMoodChange = true;
    [SerializeField] private float punchScale = 1.08f;
    [SerializeField] private float punchUpTime = 0.08f;
    [SerializeField] private float punchDownTime = 0.12f;

    private Coroutine temporaryMoodRoutine;
    private Coroutine punchRoutine;
    private Vector3 originalScale = Vector3.one;
    private readonly int[] variationIndices = new int[4];
    private Mood? currentMood;

    private void Awake()
    {
        if (robotImage == null)
            robotImage = GetComponent<Image>();

        if (robotImage == null)
            robotImage = GetComponentInChildren<Image>(true);

        if (robotImage != null)
        {
            robotImage.preserveAspect = true;
            robotImage.raycastTarget = false;
            originalScale = robotImage.rectTransform.localScale;
        }
    }

    private void Start()
    {
        SetMood(Mood.Idle, animate: false);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        temporaryMoodRoutine = null;
        punchRoutine = null;
        currentMood = null;
        if (robotImage != null)
            robotImage.rectTransform.localScale = originalScale;
    }

    public void SetIdle()
    {
        SetMood(Mood.Idle);
    }

    public void SetHappy()
    {
        SetMood(Mood.Happy);
    }

    public void SetSad()
    {
        SetMood(Mood.Sad);
    }

    public void SetExcited()
    {
        SetMood(Mood.Excited);
    }

    public void SetMood(Mood mood, bool animate = true)
    {
        if (temporaryMoodRoutine != null)
        {
            StopCoroutine(temporaryMoodRoutine);
            temporaryMoodRoutine = null;
        }

        ApplyMood(mood, animate);
    }

    public void PlayTemporaryMood(Mood mood, float duration)
    {
        if (temporaryMoodRoutine != null)
            StopCoroutine(temporaryMoodRoutine);

        temporaryMoodRoutine = StartCoroutine(CoTemporaryMood(mood, duration));
    }

    private IEnumerator CoTemporaryMood(Mood mood, float duration)
    {
        ApplyMood(mood, animate: true);

        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        ApplyMood(Mood.Idle, animate: true);
        temporaryMoodRoutine = null;
    }

    private void ApplyMood(Mood mood, bool animate)
    {
        if (robotImage == null)
            return;

        // A cascade can send the same mood many times. Keep its expression stable
        // until the mood changes, while allowing the temporary duration to restart.
        bool changed = currentMood != mood;
        if (changed)
        {
            int moodIndex = (int)mood;
            TopHudPortraitProfile.Portrait portrait = portraitProfile != null
                ? portraitProfile.GetPortrait(mood, variationIndices[moodIndex])
                : null;

            if (portrait != null)
            {
                robotImage.sprite = portrait.sprite;
                robotImage.maskable = !portraitProfile.allowOverflow;
                RectTransform rect = robotImage.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = portrait.sprite.rect.size
                    * (portraitProfile.pixelScale * portrait.scale);
                rect.anchoredPosition = portraitProfile.offset + portrait.offset;
                variationIndices[moodIndex] = (variationIndices[moodIndex] + 1) % int.MaxValue;
            }
            else
            {
                Sprite sprite = GetSprite(mood);
                if (sprite != null)
                    robotImage.sprite = sprite;
            }

            currentMood = mood;
        }

        robotImage.enabled = robotImage.sprite != null;

        if (changed && animate && animateMoodChange)
            PlayPunch();
    }

    private Sprite GetSprite(Mood mood)
    {
        switch (mood)
        {
            case Mood.Happy:
                return happySprite != null ? happySprite : idleSprite;

            case Mood.Sad:
                return sadSprite != null ? sadSprite : idleSprite;

            case Mood.Excited:
                return excitedSprite != null
                    ? excitedSprite
                    : happySprite != null
                        ? happySprite
                        : idleSprite;

            case Mood.Idle:
            default:
                return idleSprite;
        }
    }

    private void PlayPunch()
    {
        if (robotImage == null)
            return;

        if (punchRoutine != null)
            StopCoroutine(punchRoutine);

        punchRoutine = StartCoroutine(CoPunch());
    }

    private IEnumerator CoPunch()
    {
        RectTransform rt = robotImage.rectTransform;
        if (rt == null)
            yield break;

        Vector3 startScale = originalScale;
        Vector3 peakScale = originalScale * punchScale;

        float t = 0f;
        while (t < punchUpTime)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / punchUpTime);
            rt.localScale = Vector3.Lerp(startScale, peakScale, u);
            yield return null;
        }

        t = 0f;
        while (t < punchDownTime)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / punchDownTime);
            rt.localScale = Vector3.Lerp(peakScale, originalScale, u);
            yield return null;
        }

        rt.localScale = originalScale;
        punchRoutine = null;
    }
}
