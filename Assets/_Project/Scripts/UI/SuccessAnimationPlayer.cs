using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a frame-by-frame sprite animation on the success screen.
/// Call Play() as a coroutine; it fades the board, shows all frames, then restores the board.
/// </summary>
public class SuccessAnimationPlayer : MonoBehaviour
{
    [Header("Frames")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] [Min(1)] private float fps = 12f;
    [SerializeField] [Min(1)] private int loopCount = 1;

    [Header("UI")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Image backgroundImage;

    [Header("Board Fade")]
    [SerializeField] private CanvasGroup boardCanvasGroup;
    [Range(0f, 1f)]
    [SerializeField] private float boardFadeAlpha = 0.35f;

    [Header("Fade Transition")]
    [SerializeField] private float fadeInDuration  = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.2f;

    // ─────────────────────────────────────────────────────────────────

    public void SetBackground(Sprite sprite)
    {
        if (backgroundImage == null || sprite == null) return;
        backgroundImage.sprite  = sprite;
        backgroundImage.enabled = true;
    }

    // ─────────────────────────────────────────────────────────────────

    public IEnumerator Play()
    {
        if (frames == null || frames.Length == 0 || frameImage == null)
            yield break;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // Fade board down
        if (boardCanvasGroup != null)
            yield return FadeCanvasGroup(boardCanvasGroup, boardCanvasGroup.alpha, boardFadeAlpha, fadeInDuration);

        float delay = 1f / Mathf.Max(1f, fps);

        for (int loop = 0; loop < loopCount; loop++)
        {
            foreach (var frame in frames)
            {
                if (frame != null)
                    frameImage.sprite = frame;
                yield return new WaitForSeconds(delay);
            }
        }

        // Fade board back up
        if (boardCanvasGroup != null)
            yield return FadeCanvasGroup(boardCanvasGroup, boardCanvasGroup.alpha, 1f, fadeOutDuration);

        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────

    private static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }
}
