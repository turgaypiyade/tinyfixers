using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TileAnimator
{
    private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);

    private readonly BoardController board;

    public TileAnimator(BoardController board)
    {
        this.board = board;
    }

    public IEnumerator PlayPop(TileView tile, float duration)
    {
        if (tile == null) yield break;

        Transform root = tile.transform;
        RectTransform rt = tile.RectTransform;

        root.localScale = Vector3.one;

        float popDuration = Mathf.Max(0.0001f, duration);
        float impactDuration = Mathf.Min(0.055f, popDuration * 0.40f);
        float t = 0f;

        Vector2 originalPivot = rt != null ? rt.pivot : CenterPivot;
        CanvasGroup canvasGroup = tile.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = tile.gameObject.AddComponent<CanvasGroup>();

        if (rt != null && rt.pivot != CenterPivot)
            SetPivotWithoutVisualJump(rt, CenterPivot);

        canvasGroup.alpha = 1f;

        // 1) kısa impact punch
        while (t < impactDuration)
        {
            if (tile == null || root == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, impactDuration));
            float squish = 1f + Mathf.Lerp(0f, 0.12f, k);
            float stretch = 1f - Mathf.Lerp(0f, 0.08f, k);
            root.localScale = new Vector3(squish, stretch, 1f);
            yield return null;
        }

        // 2) parçalanarak küçülme
        t = 0f;
        Vector3 start = root.localScale;
        Vector3 end = Vector3.zero;

        float shatterDuration = Mathf.Max(0.0001f, popDuration - impactDuration);
        while (t < shatterDuration)
        {
            if (tile == null || root == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / shatterDuration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);

            root.localScale = Vector3.Lerp(start, end, eased);
            root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 16f, eased));
            canvasGroup.alpha = 1f - eased;
            yield return null;
        }

        if (root != null)
        {
            root.localScale = end;
            root.localRotation = Quaternion.identity;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (rt != null && rt.pivot != originalPivot)
            SetPivotWithoutVisualJump(rt, originalPivot);
    }

    public IEnumerator PlayLightningStrikeAndShrink(TileView tile, float duration, Color lightningColor)
    {
        if (tile == null) yield break;

        Image iconImage = tile.IconImage;
        if (iconImage == null)
        {
            yield return PlayPop(tile, duration);
            yield break;
        }

        Transform root = tile.transform;
        Color baseColor = iconImage.color;

        float flashTime = Mathf.Min(0.05f, duration * 0.30f);
        float impactTime = Mathf.Min(0.04f, duration * 0.25f);
        float t = 0f;

        // 1) flash
        while (t < flashTime)
        {
            if (tile == null || iconImage == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, flashTime));
            iconImage.color = Color.Lerp(baseColor, lightningColor, k);
            yield return null;
        }

        // 2) kısa sert punch
        t = 0f;
        while (t < impactTime)
        {
            if (tile == null || root == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, impactTime));
            float s = Mathf.Lerp(1f, 1.14f, k);
            root.localScale = new Vector3(s, 1f - (s - 1f) * 0.65f, 1f);
            yield return null;
        }

        if (iconImage != null)
            iconImage.color = baseColor;

        // 3) shrink out
        float shrinkDuration = Mathf.Max(0.04f, duration - flashTime - impactTime);
        t = 0f;
        Vector3 start = root != null ? root.localScale : Vector3.one;
        Vector3 end = Vector3.zero;

        while (t < shrinkDuration)
        {
            if (tile == null || root == null || iconImage == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, shrinkDuration));
            float eased = k * k;

            root.localScale = Vector3.Lerp(start, end, eased);
            root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 18f, eased));

            var c = iconImage.color;
            c.a = Mathf.Lerp(baseColor.a, 0f, eased);
            iconImage.color = c;

            yield return null;
        }

        if (root != null)
        {
            root.localScale = end;
            root.localRotation = Quaternion.identity;
        }

        if (iconImage != null)
        {
            var finalColor = iconImage.color;
            finalColor.a = 0f;
            iconImage.color = finalColor;
        }
    }

    public void PlaySelectionPulse(
        TileView tile,
        float delay = 0f,
        float peakScale = 1.12f,
        float upTime = 0.06f,
        float downTime = 0.08f)
    {
        if (tile == null || board == null) return;
        board.StartCoroutine(CoSelectionPulse(tile, delay, peakScale, upTime, downTime));
    }

    private IEnumerator CoSelectionPulse(
        TileView tile,
        float delay,
        float peakScale,
        float upTime,
        float downTime)
    {
        if (tile == null) yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile == null) yield break;

        Transform tr = GetVisualTarget(tile);
        if (tr == null) yield break;

        Vector3 baseScale = tr.localScale;
        float peak = Mathf.Max(1f, peakScale);
        Vector3 targetScale = baseScale * peak;

        float t = 0f;
        float upDur = Mathf.Max(0.0001f, upTime);
        while (t < upDur)
        {
            if (tile == null || tr == null) yield break;

            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / upDur);
            float e = 1f - (1f - a) * (1f - a); // easeOutQuad
            tr.localScale = Vector3.LerpUnclamped(baseScale, targetScale, e);
            yield return null;
        }

        t = 0f;
        float downDur = Mathf.Max(0.0001f, downTime);
        while (t < downDur)
        {
            if (tile == null || tr == null) yield break;

            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / downDur);
            float e = a * a; // easeInQuad
            tr.localScale = Vector3.LerpUnclamped(targetScale, baseScale, e);
            yield return null;
        }

        if (tr != null)
            tr.localScale = baseScale;
    }

    public IEnumerator PlayPulseImpact(TileView tile, float delay, float totalTime)
    {
        if (tile == null) yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile == null) yield break;

        RectTransform rt = tile.RectTransform;
        if (rt == null) yield break;

        CanvasGroup g = tile.GetComponent<CanvasGroup>();
        if (g == null)
            g = tile.gameObject.AddComponent<CanvasGroup>();

        Vector3 start = rt.localScale;
        Vector3 up = start * 1.08f;
        Vector3 down = start * 0.90f;

        float t = 0f;
        float half = totalTime * 0.45f;

        while (t < half)
        {
            if (tile == null || rt == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, half));
            rt.localScale = Vector3.Lerp(start, up, k);
            yield return null;
        }

        t = 0f;
        float backDur = Mathf.Max(0.0001f, totalTime - half);
        while (t < backDur)
        {
            if (tile == null || rt == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / backDur);
            rt.localScale = Vector3.Lerp(up, down, k);
            g.alpha = Mathf.Lerp(1f, 0f, k);
            yield return null;
        }
    }

    public IEnumerator PlaySpecialCreationMerge(
        TileView createdTile,
        IEnumerable<TileView> sourceTiles,
        float duration)
    {
        if (createdTile == null)
            yield break;

        RectTransform targetRt = createdTile.RectTransform;
        if (targetRt == null)
            yield break;

        var contributors = new List<TileView>();
        if (sourceTiles != null)
        {
            foreach (var tile in sourceTiles)
            {
                if (tile == null || tile == createdTile || contributors.Contains(tile))
                    continue;

                contributors.Add(tile);
            }
        }

        Transform createdRoot = createdTile.transform;
        Transform createdVisual = GetVisualTarget(createdTile);

        CanvasGroup createdGroup = createdTile.GetComponent<CanvasGroup>();
        if (createdGroup == null)
            createdGroup = createdTile.gameObject.AddComponent<CanvasGroup>();

        if (createdRoot != null)
            createdRoot.localScale = Vector3.one;
        if (createdVisual != null)
            createdVisual.localScale = Vector3.one;

        Vector3 createdBaseScale = Vector3.one;
        Vector3 createdStartScale = createdBaseScale * 0.12f;
        Vector3 createdOvershoot = createdBaseScale * 1.14f;
        createdGroup.alpha = 1f;

        if (createdVisual != null)
            createdVisual.localScale = createdStartScale;

        var sourceRts = new List<RectTransform>(contributors.Count);
        var sourceStarts = new List<Vector2>(contributors.Count);
        var sourceScales = new List<Vector3>(contributors.Count);
        var sourceVisuals = new List<Transform>(contributors.Count);
        var sourceVisualScales = new List<Vector3>(contributors.Count);
        var sourceGroups = new List<CanvasGroup>(contributors.Count);
        Vector2 targetPos = targetRt.anchoredPosition;

        for (int i = 0; i < contributors.Count; i++)
        {
            var tile = contributors[i];
            RectTransform rt = tile != null ? tile.RectTransform : null;
            if (rt == null)
                continue;

            CanvasGroup group = tile.GetComponent<CanvasGroup>();
            if (group == null)
                group = tile.gameObject.AddComponent<CanvasGroup>();

            sourceRts.Add(rt);
            sourceStarts.Add(rt.anchoredPosition);
            rt.localScale = Vector3.one;
            sourceScales.Add(rt.localScale);

            Transform visual = GetVisualTarget(tile);
            if (visual != null)
                visual.localScale = Vector3.one;

            sourceVisuals.Add(visual);
            sourceVisualScales.Add(visual != null ? visual.localScale : Vector3.one);
            sourceGroups.Add(group);

            group.alpha = 1f;
        }

        float animDuration = Mathf.Max(0.18f, duration);
        float t = 0f;

        while (t < animDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / animDuration);
            float moveEase = 1f - Mathf.Pow(1f - k, 3f);
            float growEase = EaseOutBack(k);
            float shrinkEase = Mathf.SmoothStep(0f, 1f, k);

            for (int i = 0; i < sourceRts.Count; i++)
            {
                RectTransform rt = sourceRts[i];
                if (rt == null)
                    continue;

                rt.anchoredPosition = Vector2.Lerp(sourceStarts[i], targetPos, moveEase);
                rt.localScale = Vector3.Lerp(sourceScales[i], Vector3.one * 0.18f, shrinkEase);

                Transform visual = sourceVisuals[i];
                if (visual != null)
                    visual.localScale = Vector3.Lerp(sourceVisualScales[i], Vector3.zero, shrinkEase);

                CanvasGroup group = sourceGroups[i];
                if (group != null)
                    group.alpha = 1f - shrinkEase;
            }

            if (createdVisual != null)
            {
                createdVisual.localScale = k < 0.78f
                    ? Vector3.LerpUnclamped(createdStartScale, createdOvershoot, growEase)
                    : Vector3.LerpUnclamped(createdOvershoot, createdBaseScale, Mathf.InverseLerp(0.78f, 1f, k));
            }

            yield return null;
        }

        for (int i = 0; i < sourceRts.Count; i++)
        {
            RectTransform rt = sourceRts[i];
            if (rt == null)
                continue;

            rt.anchoredPosition = targetPos;
            rt.localScale = Vector3.zero;

            Transform visual = sourceVisuals[i];
            if (visual != null)
                visual.localScale = Vector3.zero;

            CanvasGroup group = sourceGroups[i];
            if (group != null)
                group.alpha = 0f;
        }

        if (createdRoot != null)
            createdRoot.localScale = Vector3.one;
        if (createdVisual != null)
            createdVisual.localScale = createdBaseScale;

        createdGroup.alpha = 1f;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static Transform GetVisualTarget(TileView tile)
    {
        if (tile == null) return null;

        Image icon = tile.IconImage;
        if (icon != null && icon.transform != null && icon.transform != tile.transform)
            return icon.transform;

        return tile.transform;
    }

    private static void SetPivotWithoutVisualJump(RectTransform rt, Vector2 newPivot)
    {
        if (rt == null)
            return;

        Vector2 size = rt.rect.size;
        Vector2 pivotDelta = rt.pivot - newPivot;
        Vector2 anchoredOffset = new Vector2(pivotDelta.x * size.x, pivotDelta.y * size.y);
        rt.pivot = newPivot;
        rt.anchoredPosition += anchoredOffset;
    }
}
