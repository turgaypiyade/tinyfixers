using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
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
        if (tile == null || !tile)
            yield break;

        Transform root;
        RectTransform rt;

        try
        {
            root = tile.transform;
            rt = tile.RectTransform;
        }
        catch (MissingReferenceException)
        {
            yield break;
        }

        if (root == null || rt == null)
            yield break;

        root.localScale = Vector3.one;

        float popDuration = Mathf.Max(0.0001f, duration);
        float impactDuration = Mathf.Min(0.055f, popDuration * 0.40f);
        float t = 0f;

        Vector2 originalPivot = CenterPivot;
        CanvasGroup canvasGroup = null;

        try
        {
            canvasGroup = tile.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = tile.gameObject.AddComponent<CanvasGroup>();

            originalPivot = rt.pivot;

            if (rt.pivot != CenterPivot)
                SetPivotWithoutVisualJump(rt, CenterPivot);

            canvasGroup.alpha = 1f;
        }
        catch (MissingReferenceException)
        {
            yield break;
        }

        // 1) Kısa impact punch
        while (t < impactDuration)
        {
            if (tile == null || !tile || root == null)
                yield break;

            try
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, impactDuration));
                float squish = 1f + Mathf.Lerp(0f, 0.12f, k);
                float stretch = 1f - Mathf.Lerp(0f, 0.08f, k);

                root.localScale = new Vector3(squish, stretch, 1f);
            }
            catch (MissingReferenceException)
            {
                yield break;
            }

            yield return null;
        }

        // 2) Parçalanarak küçülme
        t = 0f;
        Vector3 start;

        try
        {
            start = root.localScale;
        }
        catch (MissingReferenceException)
        {
            yield break;
        }

        Vector3 end = Vector3.zero;
        float shatterDuration = Mathf.Max(0.0001f, popDuration - impactDuration);

        while (t < shatterDuration)
        {
            if (tile == null || !tile || root == null)
                yield break;

            try
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / shatterDuration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);

                root.localScale = Vector3.Lerp(start, end, eased);
                root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 16f, eased));

                if (canvasGroup != null)
                    canvasGroup.alpha = 1f - eased;
            }
            catch (MissingReferenceException)
            {
                yield break;
            }

            yield return null;
        }

        try
        {
            if (root != null)
            {
                root.localScale = end;
                root.localRotation = Quaternion.identity;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (rt != null && rt)
            {
                if (rt.pivot != originalPivot)
                    SetPivotWithoutVisualJump(rt, originalPivot);
            }
        }
        catch (MissingReferenceException)
        {
            yield break;
        }
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

        RectTransform ghostParent = board != null ? board.Parent : null;
        Image createdIcon = createdTile.IconImage;
        RectTransform createdIconRt = createdIcon != null ? createdIcon.rectTransform : null;

        if (ghostParent == null || createdIcon == null || createdIconRt == null)
        {
            yield return PlayCreatedSpecialAppearOnly(createdTile, duration);
            yield break;
        }

        float animDuration = Mathf.Max(0.08f, duration);

        Transform createdRoot = createdTile.transform;
        CanvasGroup createdGroup = createdTile.GetComponent<CanvasGroup>();
        if (createdGroup == null)
            createdGroup = createdTile.gameObject.AddComponent<CanvasGroup>();

        Vector3 createdBaseScale = createdIconRt.localScale;
        Quaternion createdBaseRotation = createdIconRt.localRotation;
        Color createdBaseColor = createdIcon.color;

        createdRoot.localScale = Vector3.one;
        createdRoot.localRotation = Quaternion.identity;
        createdGroup.alpha = 0f;
        createdIconRt.localScale = createdBaseScale * 0.18f;
        createdIconRt.localRotation = Quaternion.identity;
        createdIcon.color = new Color(
            createdBaseColor.r,
            createdBaseColor.g,
            createdBaseColor.b,
            0f);

        Vector2 targetPos = GetRectCenterInParentSpace(ghostParent, createdIconRt);

        var ghosts = new List<SpecialCreationGhostState>();
        var seenTiles = new HashSet<TileView>();

        if (sourceTiles != null)
        {
            foreach (TileView tile in sourceTiles)
            {
                if (tile == null || tile == createdTile || !seenTiles.Add(tile))
                    continue;

                Image sourceIcon = tile.IconImage;
                RectTransform sourceIconRt = sourceIcon != null ? sourceIcon.rectTransform : null;
                if (sourceIcon == null || sourceIconRt == null || sourceIcon.sprite == null)
                    continue;

                GameObject ghostGo = new GameObject(
                    "SpecialCreationGhost",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Image));

                RectTransform ghostRt = ghostGo.GetComponent<RectTransform>();
                ghostRt.SetParent(ghostParent, false);
                ghostRt.anchorMin = CenterPivot;
                ghostRt.anchorMax = CenterPivot;
                ghostRt.pivot = CenterPivot;
                ghostRt.SetAsLastSibling();
                ghostRt.sizeDelta = GetRectSizeInParentSpace(sourceIconRt, ghostParent);
                ghostRt.anchoredPosition = GetRectCenterInParentSpace(ghostParent, sourceIconRt);
                ghostRt.localScale = Vector3.one;
                ghostRt.localRotation = Quaternion.identity;

                Image ghostImage = ghostGo.GetComponent<Image>();
                ghostImage.sprite = sourceIcon.sprite;
                ghostImage.type = sourceIcon.type;
                ghostImage.preserveAspect = sourceIcon.preserveAspect;
                ghostImage.material = sourceIcon.material;
                ghostImage.raycastTarget = false;
                ghostImage.color = sourceIcon.color;

                CanvasGroup ghostGroup = ghostGo.GetComponent<CanvasGroup>();
                ghostGroup.alpha = sourceIcon.color.a;

                ghosts.Add(new SpecialCreationGhostState
                {
                    tile = tile,
                    sourceImage = sourceIcon,
                    sourceColor = sourceIcon.color,
                    ghostRect = ghostRt,
                    ghostGroup = ghostGroup,
                    startPos = ghostRt.anchoredPosition
                });

                sourceIcon.color = new Color(
                    sourceIcon.color.r,
                    sourceIcon.color.g,
                    sourceIcon.color.b,
                    0f);
            }
        }

        float t = 0f;
        while (t < animDuration)
        {
            if (createdTile == null || createdIconRt == null)
            {
                RestoreTileVisualState(createdTile);
                yield break;
            }

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / animDuration);
            float travelEase = EaseOutCubic(k);
            float fadeEase = Mathf.Clamp01(k * 1.15f);
            float createdScaleFactor = EvaluateCreatedSpecialScale(k);

            for (int i = 0; i < ghosts.Count; i++)
            {
                SpecialCreationGhostState ghost = ghosts[i];
                if (ghost.ghostRect == null)
                    continue;

                ghost.ghostRect.anchoredPosition =
                    Vector2.LerpUnclamped(ghost.startPos, targetPos, travelEase);
                ghost.ghostRect.localScale =
                    Vector3.LerpUnclamped(Vector3.one, Vector3.one * 0.08f, travelEase);
                ghost.ghostRect.localRotation =
                    Quaternion.SlerpUnclamped(
                        Quaternion.identity,
                        Quaternion.Euler(0f, 0f, 24f),
                        travelEase);

                if (ghost.ghostGroup != null)
                    ghost.ghostGroup.alpha = Mathf.Lerp(ghost.sourceColor.a, 0f, fadeEase);
            }

            createdIconRt.localScale = createdBaseScale * createdScaleFactor;
            createdIconRt.localRotation = Quaternion.identity;
            createdGroup.alpha = fadeEase;
            createdIcon.color = new Color(
                createdBaseColor.r,
                createdBaseColor.g,
                createdBaseColor.b,
                fadeEase);

            yield return null;
        }

        for (int i = 0; i < ghosts.Count; i++)
        {
            SpecialCreationGhostState ghost = ghosts[i];
            if (ghost.ghostRect != null)
                Object.Destroy(ghost.ghostRect.gameObject);
        }

        createdRoot.localScale = Vector3.one;
        createdRoot.localRotation = Quaternion.identity;
        createdIconRt.localScale = createdBaseScale;
        createdIconRt.localRotation = createdBaseRotation;
        createdGroup.alpha = 1f;
        createdIcon.color = createdBaseColor;

        RestoreTileVisualState(createdTile);
    }

    private static float EaseOutCubic(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - (inv * inv * inv);
    }

    private IEnumerator PlayCreatedSpecialAppearOnly(TileView createdTile, float duration)
    {
        if (createdTile == null)
            yield break;

        Image createdIcon = createdTile.IconImage;
        RectTransform createdIconRt = createdIcon != null ? createdIcon.rectTransform : null;
        if (createdIcon == null || createdIconRt == null)
        {
            RestoreTileVisualState(createdTile);
            yield break;
        }

        CanvasGroup createdGroup = createdTile.GetComponent<CanvasGroup>();
        if (createdGroup == null)
            createdGroup = createdTile.gameObject.AddComponent<CanvasGroup>();

        Vector3 baseScale = createdIconRt.localScale;
        Quaternion baseRotation = createdIconRt.localRotation;
        Color baseColor = createdIcon.color;

        createdTile.transform.localScale = Vector3.one;
        createdTile.transform.localRotation = Quaternion.identity;
        createdGroup.alpha = 0f;
        createdIconRt.localScale = baseScale * 0.18f;
        createdIconRt.localRotation = Quaternion.identity;
        createdIcon.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        float animDuration = Mathf.Max(0.08f, duration);
        float t = 0f;
        while (t < animDuration)
        {
            if (createdTile == null || createdIconRt == null)
            {
                RestoreTileVisualState(createdTile);
                yield break;
            }

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / animDuration);
            float fadeEase = Mathf.Clamp01(k * 1.15f);
            float createdScaleFactor = EvaluateCreatedSpecialScale(k);

            createdIconRt.localScale = baseScale * createdScaleFactor;
            createdIconRt.localRotation = Quaternion.identity;
            createdGroup.alpha = fadeEase;
            createdIcon.color = new Color(baseColor.r, baseColor.g, baseColor.b, fadeEase);
            yield return null;
        }

        createdTile.transform.localScale = Vector3.one;
        createdTile.transform.localRotation = Quaternion.identity;
        createdGroup.alpha = 1f;
        createdIconRt.localScale = baseScale;
        createdIconRt.localRotation = baseRotation;
        createdIcon.color = baseColor;

        RestoreTileVisualState(createdTile);
    }

    private Vector2 GetRectCenterInParentSpace(RectTransform parent, RectTransform rect)
    {
        if (parent == null || rect == null)
            return Vector2.zero;

        return board != null
            ? board.WorldToAnchoredIn(parent, rect.TransformPoint(rect.rect.center))
            : Vector2.zero;
    }

    private static Vector2 GetRectSizeInParentSpace(RectTransform rect, RectTransform parent)
    {
        if (rect == null || parent == null)
            return Vector2.zero;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = parent.InverseTransformPoint(corners[i]);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return max - min;
    }
    private static float EvaluateCreatedSpecialScale(float t)
    {
        t = Mathf.Clamp01(t);
        if (t < 0.72f)
        {
            float phase = t / 0.72f;
            return Mathf.LerpUnclamped(0.18f, 1.12f, EaseOutBack(phase));
        }

        float settle = (t - 0.72f) / 0.28f;
        return Mathf.LerpUnclamped(1.12f, 1f, EaseOutCubic(settle));
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private struct SpecialCreationGhostState
    {
        public TileView tile;
        public Image sourceImage;
        public Color sourceColor;
        public RectTransform ghostRect;
        public CanvasGroup ghostGroup;
        public Vector2 startPos;
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
    private static void RestoreTileVisualState(TileView tile)
    {
        if (tile == null)
            return;

        RectTransform tileRt = tile.RectTransform;
        if (tileRt != null)
        {
            tileRt.localScale = Vector3.one;
            tileRt.localRotation = Quaternion.identity;
        }

        if (tile.TryGetComponent<CanvasGroup>(out var cg))
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }

        tile.SetIconAlpha(1f);

        Image icon = tile.IconImage;
        if (icon != null)
        {
            Color c = icon.color;
            c.a = 1f;
            icon.color = c;

            RectTransform iconRt = icon.rectTransform;
            if (iconRt != null)
            {
                iconRt.localScale = Vector3.one;
                iconRt.localRotation = Quaternion.identity;
            }
        }
    }


    public IEnumerator PlayTilesImplodeToCell(
    Vector2Int targetCell,
    IReadOnlyList<TileView> sourceTiles,
    float duration,
    float clearAtNormalizedTime,
    Action<TileView> onTileClear)
    {
        if (board == null || sourceTiles == null || sourceTiles.Count == 0)
            yield break;

        RectTransform ghostParent = board.Parent;
        if (ghostParent == null)
            yield break;

        float animDuration = Mathf.Max(0.10f, duration);
        float clearT = Mathf.Clamp01(clearAtNormalizedTime);

        Vector2 targetPos = GetCellCenterInParentSpace(ghostParent, targetCell);

        var ghosts = new List<SpecialCreationGhostState>();
        var cleared = new HashSet<TileView>();
        var seen = new HashSet<TileView>();

        foreach (var tile in sourceTiles)
        {
            if (tile == null || !seen.Add(tile))
                continue;

            Image sourceIcon = tile.IconImage;
            RectTransform sourceIconRt = sourceIcon != null ? sourceIcon.rectTransform : null;
            if (sourceIcon == null || sourceIconRt == null || sourceIcon.sprite == null)
                continue;

            GameObject ghostGo = new GameObject(
                "PulseImplodeGhost",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image));

            RectTransform ghostRt = ghostGo.GetComponent<RectTransform>();
            ghostRt.SetParent(ghostParent, false);
            ghostRt.anchorMin = CenterPivot;
            ghostRt.anchorMax = CenterPivot;
            ghostRt.pivot = CenterPivot;
            ghostRt.SetAsLastSibling();
            ghostRt.sizeDelta = GetRectSizeInParentSpace(sourceIconRt, ghostParent);
            ghostRt.anchoredPosition = GetRectCenterInParentSpace(ghostParent, sourceIconRt);
            ghostRt.localScale = Vector3.one;
            ghostRt.localRotation = Quaternion.identity;

            Image ghostImage = ghostGo.GetComponent<Image>();
            ghostImage.sprite = sourceIcon.sprite;
            ghostImage.type = sourceIcon.type;
            ghostImage.preserveAspect = sourceIcon.preserveAspect;
            ghostImage.material = sourceIcon.material;
            ghostImage.raycastTarget = false;
            ghostImage.color = sourceIcon.color;

            CanvasGroup ghostGroup = ghostGo.GetComponent<CanvasGroup>();
            ghostGroup.alpha = sourceIcon.color.a;

            ghosts.Add(new SpecialCreationGhostState
            {
                tile = tile,
                sourceImage = sourceIcon,
                sourceColor = sourceIcon.color,
                ghostRect = ghostRt,
                ghostGroup = ghostGroup,
                startPos = ghostRt.anchoredPosition
            });

            sourceIcon.color = new Color(
                sourceIcon.color.r,
                sourceIcon.color.g,
                sourceIcon.color.b,
                0f);
        }

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / animDuration);
            float travelEase = 1f - Mathf.Pow(1f - k, 3f);

            for (int i = 0; i < ghosts.Count; i++)
            {
                var ghost = ghosts[i];
                if (ghost.ghostRect == null)
                    continue;

                ghost.ghostRect.anchoredPosition =
                    Vector2.LerpUnclamped(ghost.startPos, targetPos, travelEase);
                ghost.ghostRect.localScale =
                    Vector3.LerpUnclamped(Vector3.one, Vector3.one * 0.08f, travelEase);
                ghost.ghostRect.localRotation =
                    Quaternion.SlerpUnclamped(
                        Quaternion.identity,
                        Quaternion.Euler(0f, 0f, 20f),
                        travelEase);

                if (ghost.ghostGroup != null)
                    ghost.ghostGroup.alpha = Mathf.Lerp(ghost.sourceColor.a, 0f, k);
            }

            if (k >= clearT)
            {
                for (int i = 0; i < ghosts.Count; i++)
                {
                    var tile = ghosts[i].tile;
                    if (tile == null || cleared.Contains(tile))
                        continue;

                    cleared.Add(tile);
                    onTileClear?.Invoke(tile);
                }
            }

            yield return null;
        }

        for (int i = 0; i < ghosts.Count; i++)
        {
            var ghost = ghosts[i];
            if (ghost.ghostRect != null)
                UnityEngine.Object.Destroy(ghost.ghostRect.gameObject);
        }
    }

    private Vector2 GetCellCenterInParentSpace(RectTransform parent, Vector2Int cell)
    {
        if (board == null || parent == null)
            return Vector2.zero;

        TileView tile = board.Tiles[cell.x, cell.y];
        if (tile != null && tile.IconImage != null && tile.IconImage.rectTransform != null)
            return GetRectCenterInParentSpace(parent, tile.IconImage.rectTransform);

        Vector2 basePos = new Vector2(cell.x * board.TileSize, -cell.y * board.TileSize);
        return basePos + new Vector2(board.TileSize * 0.5f, -board.TileSize * 0.5f);
    }
}
