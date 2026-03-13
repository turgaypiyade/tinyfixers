using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles all visual effects during special tile resolution:
/// tile hiding, teleport markers, transient ghost sprites, override radial visuals, etc.
///
/// Extracted from SpecialResolver — no gameplay logic, only presentation.
/// </summary>
public class SpecialVisualService
{
    private readonly BoardController board;
    private readonly BoardAnimator boardAnimator;
    private readonly PatchbotComboService patchbotComboService;

    public SpecialVisualService(BoardController board, BoardAnimator boardAnimator, PatchbotComboService patchbotComboService)
    {
        this.board = board;
        this.boardAnimator = boardAnimator;
        this.patchbotComboService = patchbotComboService;
    }

    // ─────────────────────────────────────────────
    //  Tile Hiding
    // ─────────────────────────────────────────────

    public static void HideTileVisualForCombo(TileView t)
    {
        if (t == null) return;

        if (!t.TryGetComponent<CanvasGroup>(out var cg))
            cg = t.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    public void ConsumeSwapSourceVisuals(TileView a, TileView b)
    {
        HideTileVisualForCombo(a);
        HideTileVisualForCombo(b);
    }

    /// <summary>
    /// Determines which tiles should be visually hidden at the start of a special swap.
    /// Handles Override deferral (Override stays visible during fan-out), combo vs solo cases.
    /// </summary>
    public void HideSwapSourceVisuals(TileView a, TileView b, TileSpecial sa, TileSpecial sb,
        bool consumeNormalPartner)
    {
        if (consumeNormalPartner)
        {
            bool aIsOvr = sa == TileSpecial.SystemOverride;
            bool bIsOvr = sb == TileSpecial.SystemOverride;
            bool deferOverrideHide = (aIsOvr || bIsOvr) && !(aIsOvr && bIsOvr);
            if (deferOverrideHide)
            {
                if (aIsOvr) HideTileVisualForCombo(b);
                else        HideTileVisualForCombo(a);
            }
            else
            {
                ConsumeSwapSourceVisuals(a, b);
            }
        }
        else
        {
            var onlySpecial = (sa != TileSpecial.None) ? a : b;
            if (onlySpecial.GetSpecial() != TileSpecial.SystemOverride)
                HideTileVisualForCombo(onlySpecial);
        }
    }

    // ─────────────────────────────────────────────
    //  Teleport Markers
    // ─────────────────────────────────────────────

    public void PlayTeleportMarkers(TileView sourceTile, int targetX, int targetY)
    {
        if (board.BoardVfxPlayer == null || sourceTile == null) return;

        static Vector3 WorldCenter(TileView tv)
        {
            if (tv == null) return Vector3.zero;
            var rt = tv.GetComponent<RectTransform>();
            if (rt != null) return rt.TransformPoint(rt.rect.center);
            return tv.transform.position;
        }

        Vector3 CellWorldCenterVia(Transform reference, int x, int y, float ts)
        {
            var local = new Vector3(x * ts + ts * 0.5f, -y * ts - ts * 0.5f, 0f);
            return reference.TransformPoint(local);
        }

        var fromWorld = WorldCenter(sourceTile);
        var targetTile = board.Tiles[targetX, targetY];
        Vector3 toWorld;
        if (targetTile != null)
            toWorld = WorldCenter(targetTile);
        else
        {
            var reference = sourceTile.transform.parent != null ? sourceTile.transform.parent : sourceTile.transform;
            toWorld = CellWorldCenterVia(reference, targetX, targetY, board.TileSize);
        }

        board.BoardVfxPlayer.PlayTeleportMarkers(toWorld, fromWorld);
    }

    // ─────────────────────────────────────────────
    //  Transient Special Ghost (PatchBot partner visual)
    // ─────────────────────────────────────────────

    public void PlayTransientSpecialVisualAt(TileView sourceTile, int targetX, int targetY)
    {
        if (sourceTile == null) return;

        var sprite = sourceTile.GetIconSprite();
        if (sprite == null) return;

        var parent = board.Parent != null ? board.Parent : sourceTile.transform.parent as RectTransform;
        if (parent == null) return;

        var ghostGo = new GameObject("PatchBotSpecialGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var ghostRt = ghostGo.GetComponent<RectTransform>();
        ghostRt.SetParent(parent, false);
        ghostRt.anchorMin = new Vector2(0.5f, 0.5f);
        ghostRt.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRt.pivot = new Vector2(0.5f, 0.5f);
        ghostRt.sizeDelta = new Vector2(board.TileSize, board.TileSize);

        var image = ghostGo.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.95f);

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(targetX, targetY);
        float yOffset = hasObstacleAtTarget ? board.TileSize * 0.22f : 0f;
        ghostRt.anchoredPosition = new Vector2(targetX * board.TileSize + board.TileSize * 0.5f, -targetY * board.TileSize - board.TileSize * 0.5f + yOffset);
        ghostRt.localScale = hasObstacleAtTarget ? Vector3.one * 1.08f : Vector3.one;

        board.StartCoroutine(FadeAndDestroySpecialGhost(image, ghostRt, 0.24f));
    }

    public void PlayTransientSpecialPairVisualAt(TileView firstTile, TileView secondTile, int targetX, int targetY)
    {
        if (firstTile == null || secondTile == null)
        {
            PlayTransientSpecialVisualAt(firstTile != null ? firstTile : secondTile, targetX, targetY);
            return;
        }

        var firstSprite = firstTile.GetIconSprite();
        var secondSprite = secondTile.GetIconSprite();
        if (firstSprite == null || secondSprite == null)
        {
            if (firstSprite != null) PlayTransientSpecialVisualAt(firstTile, targetX, targetY);
            if (secondSprite != null) PlayTransientSpecialVisualAt(secondTile, targetX, targetY);
            return;
        }

        var parent = board.Parent != null ? board.Parent : firstTile.transform.parent as RectTransform;
        if (parent == null) return;

        var pairGo = new GameObject("PatchBotSpecialPairGhost", typeof(RectTransform));
        var pairRt = pairGo.GetComponent<RectTransform>();
        pairRt.SetParent(parent, false);
        pairRt.anchorMin = new Vector2(0.5f, 0.5f);
        pairRt.anchorMax = new Vector2(0.5f, 0.5f);
        pairRt.pivot = new Vector2(0.5f, 0.5f);
        pairRt.sizeDelta = new Vector2(board.TileSize, board.TileSize);

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(targetX, targetY);
        float yOffset = hasObstacleAtTarget ? board.TileSize * 0.22f : 0f;
        pairRt.anchoredPosition = new Vector2(
            targetX * board.TileSize + board.TileSize * 0.5f,
            -targetY * board.TileSize - board.TileSize * 0.5f + yOffset);
        pairRt.localScale = hasObstacleAtTarget ? Vector3.one * 1.08f : Vector3.one;

        var firstImage = CreatePairGhostImage(pairRt, "GhostA", firstSprite);
        var secondImage = CreatePairGhostImage(pairRt, "GhostB", secondSprite);

        board.StartCoroutine(AnimateAndDestroySpecialPairGhost(firstImage, secondImage, pairRt));
    }

    private UnityEngine.UI.Image CreatePairGhostImage(RectTransform parent, string name, Sprite sprite)
    {
        var ghostGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var ghostRt = ghostGo.GetComponent<RectTransform>();
        ghostRt.SetParent(parent, false);
        ghostRt.anchorMin = new Vector2(0.5f, 0.5f);
        ghostRt.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRt.pivot = new Vector2(0.5f, 0.5f);
        ghostRt.sizeDelta = new Vector2(board.TileSize * 0.92f, board.TileSize * 0.92f);

        var image = ghostGo.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.95f);
        return image;
    }

    private IEnumerator FadeAndDestroySpecialGhost(UnityEngine.UI.Image image, RectTransform ghostRt, float duration)
    {
        float elapsed = 0f;
        Vector2 startPos = ghostRt != null ? ghostRt.anchoredPosition : Vector2.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            if (image != null)
            {
                var c = image.color;
                c.a = Mathf.Lerp(0.95f, 0f, t);
                image.color = c;
            }

            if (ghostRt != null)
            {
                float rise = board.TileSize * 0.08f * t;
                ghostRt.anchoredPosition = new Vector2(startPos.x, startPos.y + rise);
            }

            yield return null;
        }

        if (ghostRt != null)
            Object.Destroy(ghostRt.gameObject);
    }


    public void PlayTransientSpecialPairTravelVisualAt(
        TileView firstTile,
        TileView secondTile,
        int targetX,
        int targetY,
        float travelDuration)
    {
        if (firstTile == null || secondTile == null)
        {
            PlayTransientSpecialVisualAt(firstTile != null ? firstTile : secondTile, targetX, targetY);
            return;
        }

        var firstSprite = firstTile.GetIconSprite();
        var secondSprite = secondTile.GetIconSprite();
        if (firstSprite == null || secondSprite == null)
        {
            PlayTransientSpecialPairVisualAt(firstTile, secondTile, targetX, targetY);
            return;
        }

        var parent = board.Parent != null ? board.Parent : firstTile.transform.parent as RectTransform;
        if (parent == null) return;

        var pairGo = new GameObject("PatchBotSpecialPairTravelGhost", typeof(RectTransform));
        var pairRt = pairGo.GetComponent<RectTransform>();
        pairRt.SetParent(parent, false);
        pairRt.anchorMin = new Vector2(0.5f, 0.5f);
        pairRt.anchorMax = new Vector2(0.5f, 0.5f);
        pairRt.pivot = new Vector2(0.5f, 0.5f);
        pairRt.sizeDelta = new Vector2(board.TileSize, board.TileSize);

        Vector2 fromAnchored = new Vector2(
            firstTile.X * board.TileSize + board.TileSize * 0.5f,
            -firstTile.Y * board.TileSize - board.TileSize * 0.5f);

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(targetX, targetY);
        float yOffset = hasObstacleAtTarget ? board.TileSize * 0.22f : 0f;
        Vector2 toAnchored = new Vector2(
            targetX * board.TileSize + board.TileSize * 0.5f,
            -targetY * board.TileSize - board.TileSize * 0.5f + yOffset);

        pairRt.anchoredPosition = fromAnchored;
        pairRt.localScale = hasObstacleAtTarget ? Vector3.one * 1.08f : Vector3.one;

        var firstImage = CreatePairGhostImage(pairRt, "GhostA", firstSprite);
        var secondImage = CreatePairGhostImage(pairRt, "GhostB", secondSprite);

        board.StartCoroutine(AnimateAndDestroySpecialPairTravelGhost(firstImage, secondImage, pairRt, fromAnchored, toAnchored, travelDuration));
    }

    public void PlayPulseExplosionAtDelayed(int x, int y, float delay)
    {
        board.StartCoroutine(CoPlayPulseExplosionDelayed(x, y, delay));
    }

    private IEnumerator CoPlayPulseExplosionDelayed(int x, int y, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        board.PlayPulsePulseExplosionVfxAtCell(x, y);
    }

    private IEnumerator AnimateAndDestroySpecialPairTravelGhost(
        UnityEngine.UI.Image firstImage,
        UnityEngine.UI.Image secondImage,
        RectTransform pairRt,
        Vector2 fromAnchored,
        Vector2 toAnchored,
        float travelDuration)
    {
        if (firstImage == null || secondImage == null || pairRt == null)
            yield break;

        var tuning = board.PatchBotGhostTuning;
        float duration = Mathf.Max(0.05f, travelDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            pairRt.anchoredPosition = Vector2.Lerp(fromAnchored, toAnchored, t);

            float radius = Mathf.Lerp(
                board.TileSize * tuning.StartRadiusFactor,
                board.TileSize * tuning.EndRadiusFactor,
                t);
            float angle = tuning.SpinDegrees * t;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            firstImage.rectTransform.anchoredPosition = dir * radius;
            secondImage.rectTransform.anchoredPosition = -dir * radius;

            float alpha = t < 0.85f ? 0.95f : Mathf.Lerp(0.95f, 0f, (t - 0.85f) / 0.15f);
            var c1 = firstImage.color; c1.a = alpha; firstImage.color = c1;
            var c2 = secondImage.color; c2.a = alpha; secondImage.color = c2;

            yield return null;
        }

        if (pairRt != null)
            Object.Destroy(pairRt.gameObject);
    }

    private IEnumerator AnimateAndDestroySpecialPairGhost(
        UnityEngine.UI.Image firstImage,
        UnityEngine.UI.Image secondImage,
        RectTransform pairRt)
    {
        if (firstImage == null || secondImage == null || pairRt == null)
            yield break;

        var tuning = board.PatchBotGhostTuning;
        float elapsed = 0f;
        float duration = tuning.Duration;
        float startRadius = board.TileSize * tuning.StartRadiusFactor;
        float endRadius = board.TileSize * tuning.EndRadiusFactor;
        float maxSpin = tuning.SpinDegrees;
        Vector2 pairStart = pairRt.anchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));

            float radius = Mathf.Lerp(startRadius, endRadius, t);
            float angle = maxSpin * t;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            var firstRt = firstImage.rectTransform;
            var secondRt = secondImage.rectTransform;
            firstRt.anchoredPosition = dir * radius;
            secondRt.anchoredPosition = -dir * radius;

            float alpha = Mathf.Lerp(0.95f, 0f, t);
            var c1 = firstImage.color;
            c1.a = alpha;
            firstImage.color = c1;
            var c2 = secondImage.color;
            c2.a = alpha;
            secondImage.color = c2;

            float rise = board.TileSize * tuning.RiseFactor * t;
            pairRt.anchoredPosition = new Vector2(pairStart.x, pairStart.y + rise);

            yield return null;
        }

        if (pairRt != null)
            Object.Destroy(pairRt.gameObject);
    }

    // ─────────────────────────────────────────────
    //  Override+Override Radial Visual Effects
    // ─────────────────────────────────────────────

    /// <summary>
    /// Override+Override radial dalga sırasında yoluna çıkan special taşların
    /// görsel efektini (sadece VFX, mantıksal etki yok) ateşler.
    /// Her special'ın efekti, dalganın o hücreye ulaştığı zamana göre tetiklenir.
    /// </summary>
    public void FireOverrideOverrideSpecialVisuals(HashSet<TileView> affected, Dictionary<TileView, float> radialDelays)
    {
        if (affected == null || radialDelays == null || board == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log("[SVS] OVR-OVR-VISUALS affected=" + affected.Count + " radialDelays=" + radialDelays.Count);
#endif

        foreach (var tile in affected)
        {
            if (tile == null) continue;
            var spec = tile.GetSpecial();
            if (spec == TileSpecial.None) continue;
            if (!radialDelays.TryGetValue(tile, out float delay)) continue;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SVS] OVR-OVR-SCHEDULE cell=(" + tile.X + "," + tile.Y + ") special=" + spec + " delay=" + delay);
#endif

            int x = tile.X;
            int y = tile.Y;
            board.StartCoroutine(DelayedSpecialVisualTrigger(x, y, spec, delay));
        }
    }

    private IEnumerator DelayedSpecialVisualTrigger(int x, int y, TileSpecial special, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log("[SVS] DELAYED-TRIGGER cell=(" + x + "," + y + ") special=" + special);
#endif

        switch (special)
        {
            case TileSpecial.PulseCore:
                board.PlayPulsePulseExplosionVfxAtCell(x, y);
                board.StartCoroutine(boardAnimator.MicroShake(0.08f, board.ShakeStrength * 0.4f));
                break;

            case TileSpecial.LineH:
            case TileSpecial.LineV:
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SVS] DELAYED-LINE-VISUAL cell=(" + x + "," + y + ") special=" + special);
#endif

                    var strikes = new List<LightningLineStrike>(1)
            {
                new LightningLineStrike(new Vector2Int(x, y), special == TileSpecial.LineH)
            };
                    board.PlayLightningLineStrikes(strikes, null);
                    break;
                }
        }
    }

    /// <summary>
    /// Builds center-out radial clear delays for Override+Override combo.
    /// </summary>
    public Dictionary<TileView, float> BuildCenterOutClearDelays(HashSet<TileView> targets, float maxDelay)
    {
        if (targets == null || targets.Count == 0 || maxDelay <= 0f)
            return null;

        float centerX = (board.Width - 1) * 0.5f;
        float centerY = (board.Height - 1) * 0.5f;
        var center = new Vector2(centerX, centerY);
        float maxDistance = 0f;

        foreach (var tile in targets)
        {
            if (tile == null) continue;
            float distance = Vector2.Distance(new Vector2(tile.X, tile.Y), center);
            if (distance > maxDistance)
                maxDistance = distance;
        }

        if (maxDistance <= Mathf.Epsilon)
            return null;

        var delays = new Dictionary<TileView, float>(targets.Count);
        foreach (var tile in targets)
        {
            if (tile == null) continue;
            float distance = Vector2.Distance(new Vector2(tile.X, tile.Y), center);
            float normalized = Mathf.Clamp01(distance / maxDistance);
            float eased = 1f - (1f - normalized) * (1f - normalized);
            delays[tile] = eased * maxDelay;
        }

        return delays;
    }

    // ─────────────────────────────────────────────
    //  PatchBot Immediate Dash VFX
    // ─────────────────────────────────────────────

    public void FireImmediateDash(int fromX, int fromY, int targetX, int targetY, float delay = 0f)
    {
        if (board.PatchbotDashUI == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log("[SVS] DASH-SCHEDULE from=(" + fromX + "," + fromY + ") to=(" + targetX + "," + targetY + ") delay=" + delay);
#endif

        IEnumerator CoPlayDash()
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SVS] DASH-PLAY from=(" + fromX + "," + fromY + ") to=(" + targetX + "," + targetY + ")");
#endif

            var req = new BoardController.PatchbotDashRequest
            {
                from = new Vector2Int(fromX, fromY),
                to = new Vector2Int(targetX, targetY)
            };
            var singleDash = new List<BoardController.PatchbotDashRequest>(1) { req };
            board.PatchbotDashUI.PlayDashParallel(singleDash, board);
        }

        board.StartCoroutine(CoPlayDash());
    }
}
