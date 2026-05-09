using System.Collections;
using TMPro;
using UnityEngine;

public static class UiNumberTween
{
    public static IEnumerator Tween(TMP_Text text, int from, int to, float duration, bool formatN0 = false)
    {
        if (text == null)
            yield break;

        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        SetValue(text, from, formatN0);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int value = Mathf.RoundToInt(Mathf.Lerp(from, to, EaseOut(t)));
            SetValue(text, value, formatN0);
            yield return null;
        }

        SetValue(text, to, formatN0);
    }

    public static void SetValue(TMP_Text text, int value, bool formatN0 = false)
    {
        if (text == null)
            return;

        text.text = FormatValue(value, formatN0);
    }

    public static string FormatValue(int value, bool formatN0 = false)
    {
        return Mathf.Max(0, value).ToString(formatN0 ? "N0" : "0");
    }

    public static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }
}
