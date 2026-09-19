using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Animates full-body sprites on the body child only. The controller owns root hit/win motion.
public sealed class BossDuelCharacterView : MonoBehaviour
{
    private Image body;
    private BossDuelCharacterProfile profile;
    private Vector2 ground;
    private float standingHeight;
    private Vector3 baseScale;
    private bool focused, attacking, finished;
    private float idleTime;

    public void Initialize(Image image, BossDuelCharacterProfile character)
    {
        body = image;
        profile = character;
        var rt = body.rectTransform;
        standingHeight = rt.rect.height;
        ground = rt.anchoredPosition - new Vector2(0f, rt.rect.height * rt.pivot.y);
        baseScale = rt.localScale;
        if (profile.mirrorHorizontally) baseScale.x *= -1f;
        body.type = Image.Type.Simple;
        body.preserveAspect = true;
        body.raycastTarget = false;
        Show(profile.idle);
    }

    public void SetFocused(bool value) => focused = value;

    private void Update()
    {
        if (profile == null || attacking || finished) return;
        idleTime += Time.deltaTime;
        float interval = Mathf.Max(0.1f, profile.blinkInterval);
        bool blink = idleTime % (interval + profile.blinkDuration) >= interval;
        Show(focused ? profile.focused : blink ? profile.blink : profile.idle);
        float breath = 1f + Mathf.Sin(idleTime * 2.2f) * profile.breathingAmount;
        body.rectTransform.localScale = Vector3.Scale(baseScale, new Vector3(1f, breath, 1f));
    }

    private void Show(BossDuelCharacterProfile.Pose pose, Vector2 motion = default)
    {
        if (body == null || profile == null) return;
        if (pose == null || pose.sprite == null) pose = profile.idle;
        var rt = body.rectTransform;
        body.sprite = pose.sprite;
        rt.pivot = pose.groundPivot;
        float height = standingHeight * Mathf.Max(0.01f, pose.height);
        rt.sizeDelta = new Vector2(height * pose.sprite.rect.width / pose.sprite.rect.height, height);
        rt.anchoredPosition = ground + pose.offset + motion;
        rt.localScale = baseScale;
    }

    public IEnumerator Attack(RectTransform target, Action impact, Func<bool> cancelled)
    {
        attacking = true;
        try
        {
            Show(profile.windup);
            yield return new WaitForSeconds(Mathf.Max(0.02f, profile.windupDuration));
            if (finished || cancelled()) yield break;

            // Bring the hammer/fists close to the opponent without moving HP bars or hit roots.
            float dx = 0f;
            if (target != null && body.rectTransform.parent != null)
            {
                float targetX = body.rectTransform.parent.InverseTransformPoint(target.position).x;
                float distance = targetX - body.rectTransform.localPosition.x;
                dx = Mathf.Sign(distance) * Mathf.Max(0f, Mathf.Abs(distance) - standingHeight * 0.7f);
            }
            Vector2 reach = new Vector2(dx, 0f);
            float duration = Mathf.Max(0.02f, profile.swingDuration);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (finished || cancelled()) yield break;
                Show(profile.windup, Vector2.Lerp(Vector2.zero, reach, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            if (finished || cancelled()) yield break;
            Show(profile.strike, reach);
            impact?.Invoke();
            if (finished) yield break;
            yield return new WaitForSeconds(Mathf.Max(0.02f, profile.impactDuration));

            duration = Mathf.Max(0.02f, profile.recoveryDuration);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (finished || cancelled()) yield break;
                Show(profile.focused, Vector2.Lerp(reach, Vector2.zero, Mathf.Clamp01(t / duration)));
                yield return null;
            }
        }
        finally
        {
            attacking = false;
            if (!finished) Show(focused ? profile.focused : profile.idle);
        }
    }

    public void Finish(bool won)
    {
        finished = true;
        Show(won ? profile.victory : profile.defeated);
    }

    public void ResetForWave()
    {
        finished = false;
        focused = false;
        idleTime = 0f;
        Show(profile.idle);
    }
}
