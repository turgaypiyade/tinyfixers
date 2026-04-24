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

    // ============================================================
    // ROYAL MATCH TARZI CLEAR BURST
    // Taş küçülür + halka (glow ring) açılır + yıldızlar dans eder + altın shardlar saçılır.
    // Burst VFX TileClearBurstVfx sınıfı tarafından üretilir (runtime UI, prefab gerekmez).
    //
    // Hissiyat ayarları (hızlandırıldı):
    //   BURST_DURATION = 0.08s (PlayPop coroutine bekleme, eski 0.12)
    //   Burst kendisi arka planda 0.30s yaşamaya devam eder (fire-and-forget),
    //   sadece PlayPop'un coroutine beklemesi 0.08s → clear anim blokaj azaldı.
    // ============================================================
    private const float BURST_DURATION = 0.08f;   // Taş küçülme süresi (PlayPop coroutine bekleme)
    private const float TILE_SHRINK_END = 0.00f;  // Taş scale sonu
    private const float TILE_SHRINK_MID = 0.55f;  // Taş scale orta (shrink hissini verir)
    private const float BURST_VFX_DURATION = 0.30f; // Halka/yıldız/shard yaşam süresi (paralel)

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

        CanvasGroup canvasGroup = null;
        Vector2 originalPivot = CenterPivot;

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

        // Burst VFX'i paralel tetikle (kendi life'ını yaşar, PlayPop bekleme zorunda değil)
        // Fire-and-forget: burst 300ms yaşar ama PlayPop sadece 120ms blokluyor
        if (board != null)
        {
            board.StartCoroutine(TileClearBurstVfx.CoPlayBurst(tile, board, BURST_VFX_DURATION));
        }

        // Taş animasyonu — çağıran taraftaki duration parametresini dikkate al
        // (cascade sırasında farklı sürede oynatmak isteyebilir)
        float shrinkDuration = Mathf.Max(0.10f, Mathf.Min(duration, BURST_DURATION));
        float t = 0f;

        while (t < shrinkDuration)
        {
            if (tile == null || !tile || root == null) yield break;

            try
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, shrinkDuration));

                // 2 fazlı scale: 1.0 → 0.55 → 0.0
                // Faz 1 (0-0.4): 1.0 → 0.55 (hızlı küçülme, burst ile senkronize)
                // Faz 2 (0.4-1.0): 0.55 → 0.0 (yavaş kayboluş)
                float scale;
                if (k < 0.4f)
                {
                    float kk = k / 0.4f;
                    float eased = 1f - (1f - kk) * (1f - kk); // easeOutQuad
                    scale = Mathf.Lerp(1f, TILE_SHRINK_MID, eased);
                }
                else
                {
                    float kk = (k - 0.4f) / 0.6f;
                    scale = Mathf.Lerp(TILE_SHRINK_MID, TILE_SHRINK_END, kk);
                }

                root.localScale = new Vector3(scale, scale, 1f);

                // Alpha: ilk %60 tam opak, sonra hızlı solma
                if (canvasGroup != null)
                {
                    float alpha = (k < 0.6f) ? 1f : 1f - (k - 0.6f) / 0.4f;
                    canvasGroup.alpha = Mathf.Clamp01(alpha);
                }
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
                root.localScale = Vector3.zero;
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


    public IEnumerator PlaySpecialCreationMerge(TileView createdTile, IEnumerable<TileView> sourceTiles, float duration)
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

        // Special creation hızlandırıldı: maksimum 80ms ile cap'lendi
        // Eski: duration 145-170ms arasında geliyordu (log'dan)
        // Yeni: Mathf.Clamp ile 60-80ms arasında tutuluyor
        // Merge animasyonu (ghost'ların special'a akması) daha hızlı hissettiriyor
        float animDuration = Mathf.Clamp(duration, 0.06f, 0.08f);

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

        // Created tile'ın üstünde burst (merkezi, special'ın doğduğu nokta)
        // Icon rt sprite'ın görünen merkezini verir
        if (board != null && createdIconRt != null)
        {
            Vector3[] _cornersCreated = new Vector3[4];
            createdIconRt.GetWorldCorners(_cornersCreated);
            Vector3 _createdWorldCenter = (_cornersCreated[0] + _cornersCreated[2]) * 0.5f;
            board.StartCoroutine(TileClearBurstVfx.CoPlayBurstAtWorldPosition(
                _createdWorldCenter, ghostParent, board, BURST_VFX_DURATION));
        }

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

                // Merge olan her source tile için de burst (halka + yıldız + shard)
                // iconRt yerine tile.RectTransform — ikon tile içinde offset'li olabilir
                if (board != null)
                {
                    RectTransform tileRootRt = tile.RectTransform;
                    if (tileRootRt != null)
                    {
                        Vector3[] _cornersSource = new Vector3[4];
                        tileRootRt.GetWorldCorners(_cornersSource);
                        Vector3 _srcWorldCenter = (_cornersSource[0] + _cornersSource[2]) * 0.5f;
                        board.StartCoroutine(TileClearBurstVfx.CoPlayBurstAtWorldPosition(
                            _srcWorldCenter, ghostParent, board, BURST_VFX_DURATION));
                    }
                }
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
            float ghostFadeEase = Mathf.Clamp01(k * 0.95f);

            float createdFadeEase;
            if (k < 0.52f)
                createdFadeEase = 0f;
            else
                createdFadeEase = Mathf.Clamp01((k - 0.52f) / 0.48f);

            float createdScaleFactor = EvaluateCreatedSpecialScale(k);

            for (int i = 0; i < ghosts.Count; i++)
            {
                SpecialCreationGhostState ghost = ghosts[i];
                if (ghost.ghostRect == null)
                    continue;

                ghost.ghostRect.anchoredPosition =
                    Vector2.LerpUnclamped(ghost.startPos, targetPos, travelEase);
                ghost.ghostRect.localScale =
                    Vector3.LerpUnclamped(Vector3.one, Vector3.one * 0.28f, travelEase);
                ghost.ghostRect.localRotation =
                    Quaternion.SlerpUnclamped(
                        Quaternion.identity,
                        Quaternion.Euler(0f, 0f, 42f),
                        travelEase);

                if (ghost.ghostGroup != null)
                    ghost.ghostGroup.alpha = Mathf.Lerp(ghost.sourceColor.a, 0f, ghostFadeEase);
            }

            createdIconRt.localScale = createdBaseScale * createdScaleFactor;
            createdIconRt.localRotation = Quaternion.identity;
            createdGroup.alpha = createdFadeEase;
            createdIcon.color = new Color(
                createdBaseColor.r,
                createdBaseColor.g,
                createdBaseColor.b,
                createdFadeEase);

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

        // Special appear fallback — burst'ü de tetikle
        // Icon rt sprite'ın gerçek konumunu verir (tile rootu offsetli olabilir)
        if (board != null && board.Parent != null && createdIconRt != null)
        {
            Vector3[] _cornersAppear = new Vector3[4];
            createdIconRt.GetWorldCorners(_cornersAppear);
            Vector3 _appearCenter = (_cornersAppear[0] + _cornersAppear[2]) * 0.5f;
            board.StartCoroutine(TileClearBurstVfx.CoPlayBurstAtWorldPosition(
                _appearCenter, board.Parent, board, BURST_VFX_DURATION));
        }

        Vector3 baseScale = createdIconRt.localScale;
        Quaternion baseRotation = createdIconRt.localRotation;
        Color baseColor = createdIcon.color;

        createdTile.transform.localScale = Vector3.one;
        createdTile.transform.localRotation = Quaternion.identity;
        createdGroup.alpha = 0f;
        createdIconRt.localScale = baseScale * 0.18f;
        createdIconRt.localRotation = Quaternion.identity;
        createdIcon.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        // Special appear fallback hızlandırıldı: 60-80ms cap
        float animDuration = Mathf.Clamp(duration, 0.06f, 0.08f);
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

        if (t < 0.60f)
        {
            float phase = t / 0.60f;
            return Mathf.LerpUnclamped(0.10f, 1.18f, EaseOutBack(phase));
        }

        float settle = (t - 0.60f) / 0.40f;
        return Mathf.LerpUnclamped(1.18f, 1f, EaseOutCubic(settle));
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

        // ESKİSİ TERS YÖNDEYDİ
        Vector2 pivotDelta = newPivot - rt.pivot;
        Vector2 anchoredOffset = new Vector2(
            pivotDelta.x * size.x,
            pivotDelta.y * size.y);

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
                    Vector3.LerpUnclamped(Vector3.one, Vector3.one * 0.28f, travelEase);
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