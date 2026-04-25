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

    [Header("Sprites")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite happySprite;
    [SerializeField] private Sprite sadSprite;
    [SerializeField] private Sprite excitedSprite;

    [Header("Behaviour")]
    [SerializeField] private bool setIdleOnStart = true;
    [SerializeField] private bool playMoodPunch = true;
    [SerializeField] private float punchScale = 1.08f;
    [SerializeField] private float punchUpTime = 0.08f;
    [SerializeField] private float punchDownTime = 0.12f;

    private Coroutine moodRoutine;
    private Coroutine punchRoutine;
    private Mood currentMood = Mood.Idle;
    private Vector3 originalScale = Vector3.one;

    private void Awake()
    {
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
        if (setIdleOnStart)
            SetMood(Mood.Idle, false);
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

    public void SetMood(Mood mood)
    {
        SetMood(mood, true);
    }

    public void SetMood(Mood mood, bool animate)
    {
        currentMood = mood;

        if (robotImage == null)
            return;

        Sprite sprite = GetSprite(mood);
        if (sprite != null)
            robotImage.sprite = sprite;

        robotImage.enabled = robotImage.sprite != null;

        if (animate && playMoodPunch)
            PlayPunch();
    }

    public void PlayTemporaryMood(Mood mood, float duration = 0.85f)
    {
        if (moodRoutine != null)
            StopCoroutine(moodRoutine);

        moodRoutine = StartCoroutine(CoTemporaryMood(mood, duration));
    }

    private IEnumerator CoTemporaryMood(Mood mood, float duration)
    {
        Mood previousMood = currentMood;

        SetMood(mood, true);
        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        SetMood(previousMood, true);
        moodRoutine = null;
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
                return excitedSprite != null ? excitedSprite : happySprite != null ? happySprite : idleSprite;

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