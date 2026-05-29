using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles booster activation, application, and shuffle.
/// Coroutines use board.StartCoroutine.
/// </summary>
public class BoosterService
{
    private readonly BoardController board;

    public BoosterService(BoardController board)
    {
        this.board = board;
    }

    public IEnumerator ApplyBoosterRoutine(
        BoardController.BoosterMode mode,
        TileView target,
        Vector2Int? targetCell,
        SpecialResolver specialResolver,
        ActionSequencer actionSequencer,
        CascadeLogic cascadeLogic,
        LineSweepService lineSweepService,
        LightningSpawner lightningSpawner,
        LineTravelSplitSwapTestUI lineTravelPlayer)
    {
        board.BeginBusy();
        board.IsSpecialActivationPhase = true;

        bool hasValidTargetCell = targetCell.HasValue
                                  && targetCell.Value.x >= 0 && targetCell.Value.x < board.Width
                                  && targetCell.Value.y >= 0 && targetCell.Value.y < board.Height;

        if (target == null && !hasValidTargetCell)
        {
            board.IsSpecialActivationPhase = false;
            board.EndBusy();
            yield break;
        }

        var matches = new HashSet<TileView>();
        HashSet<TileView> initialLightningTargets = null;
        var affectedCells = new HashSet<Vector2Int>();

        switch (mode)
        {
            case BoardController.BoosterMode.Single:
                if (target != null)
                    matches.Add(target);

                if (hasValidTargetCell && IsCellBoosterAffectable(targetCell.Value.x, targetCell.Value.y))
                    affectedCells.Add(targetCell.Value);
                break;

            case BoardController.BoosterMode.Row:
                int rowY = target != null ? target.Y : targetCell.GetValueOrDefault().y;
                AddRow(matches, rowY);
                AddRowCells(affectedCells, rowY);
                break;

            case BoardController.BoosterMode.Column:
                int columnX = target != null ? target.X : targetCell.GetValueOrDefault().x;
                AddColumn(matches, columnX);
                AddColumnCells(affectedCells, columnX);
                break;
        }

        if ((mode == BoardController.BoosterMode.Row || mode == BoardController.BoosterMode.Column) && matches.Count > 0)
            initialLightningTargets = new HashSet<TileView>(matches);

        if (matches.Count > 0 || affectedCells.Count > 0)
        {
            bool hasLineActivation = false;

            var chainLineStrikes = new List<LightningLineStrike>();
            specialResolver.ExpandSpecialChain(
                matches,
                affectedCells,
                out hasLineActivation,
                out _,
                lightningVisualTargets: initialLightningTargets,
                lightningLineStrikes: chainLineStrikes);

            var animationMode = (mode == BoardController.BoosterMode.Row || mode == BoardController.BoosterMode.Column)
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default;

            if (hasLineActivation)
                animationMode = ClearAnimationMode.LightningStrike;

            ObstacleHitContext obstacleHitContext = ObstacleHitContext.Booster;

            List<LightningLineStrike> lightningLineStrikes = null;
            if (animationMode == ClearAnimationMode.LightningStrike)
            {
                lightningLineStrikes = chainLineStrikes.Count > 0
                    ? chainLineStrikes
                    : new List<LightningLineStrike>();

                if (targetCell.HasValue &&
                    (mode == BoardController.BoosterMode.Row || mode == BoardController.BoosterMode.Column))
                {
                    lightningLineStrikes.Add(new LightningLineStrike(
                        targetCell.Value,
                        mode == BoardController.BoosterMode.Row));
                }

                if (lightningLineStrikes.Count == 0)
                    lightningLineStrikes = null;
            }

            Coroutine hammerExitRoutine = null;
            Coroutine cannonExitRoutine = null;
            Coroutine verticalExitRoutine = null;

            if (mode == BoardController.BoosterMode.Single && targetCell.HasValue)
            {
                IEnumerator hammerExit = null;
                yield return PlayHammerBoosterImpactFx(targetCell.Value, exitRoutine: r => hammerExit = r);

                if (hammerExit != null)
                    hammerExitRoutine = board.StartCoroutine(hammerExit);
            }

            if (mode == BoardController.BoosterMode.Column && targetCell.HasValue)
            {
                IEnumerator cannonExit = null;
                yield return PlayCannonBoosterEnterAndFireFx(targetCell.Value.x, exitRoutine: r => cannonExit = r);

                if (cannonExit != null)
                    cannonExitRoutine = board.StartCoroutine(cannonExit);
            }

            if (mode == BoardController.BoosterMode.Row && targetCell.HasValue)
            {
                IEnumerator verticalExit = null;
                yield return PlayVerticalBoosterEnterAndFireFx(targetCell.Value.y, exitRoutine: r => verticalExit = r);

                if (verticalExit != null)
                    verticalExitRoutine = board.StartCoroutine(verticalExit);
            }

            actionSequencer.Enqueue(new MatchClearAction(
                matches,
                doShake: true,
                animationMode: animationMode,
                affectedCells: affectedCells,
                obstacleHitContext: obstacleHitContext,
                includeAdjacentOverTileBlockerDamage: false,
                lightningOriginTile: target,
                lightningOriginCell: targetCell,
                lightningVisualTargets: initialLightningTargets,
                lightningLineStrikes: lightningLineStrikes,
                enqueueCascadeOnComplete: true));

            while (actionSequencer.IsPlaying)
                yield return null;

            if (hammerExitRoutine != null)
                yield return hammerExitRoutine;

            if (cannonExitRoutine != null)
                yield return cannonExitRoutine;

            if (verticalExitRoutine != null)
                yield return verticalExitRoutine;

            yield return board.ResolveBoardPublic();
        }

        board.IsSpecialActivationPhase = false;
        board.EndBusy();
    }
    // ============================================================
    // CANNON BOOSTER FX
    //
    // Column booster icin gorsel top/cannon animasyonu.
    // Sütunu bu animasyon silmez; asil kirma MatchClearAction tarafinda kalir.
    // ============================================================

    private IEnumerator PlayCannonBoosterEnterAndFireFx(int columnX, Action<IEnumerator> exitRoutine)
    {
        RectTransform cannon = CreateCannonFxInstance();

        if (cannon == null)
            yield break;

        RectTransform parent = board.BoosterFxParent;
        if (parent == null)
        {
            UnityEngine.Object.Destroy(cannon.gameObject);
            yield break;
        }

        cannon.SetParent(parent, false);
        cannon.SetAsLastSibling();

        cannon.anchorMin = new Vector2(0.5f, 0.5f);
        cannon.anchorMax = new Vector2(0.5f, 0.5f);
        cannon.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup canvasGroup = cannon.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = cannon.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;

        Canvas.ForceUpdateCanvases();

        Vector2 targetPos = GetColumnBottomAnchoredCenter(columnX, parent);

        // BottomArea'nın altından geliyormuş gibi board'un altından başlatıyoruz.
        // Daha aşağıdan gelsin istersen 2.4f değerini büyüt.
        Vector2 startPos = targetPos + new Vector2(0f, -board.TileSize * 2.4f);

        cannon.anchoredPosition = startPos;
        cannon.localScale = Vector3.one;
        cannon.localRotation = Quaternion.identity;

        const float enterDuration = 0.32f;

        float t = 0f;

        while (t < enterDuration)
        {
            if (cannon == null || !cannon)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / enterDuration);
            float eased = EaseOutBackLight(k);

            cannon.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, eased);

            yield return null;
        }

        if (cannon == null || !cannon)
            yield break;

        cannon.anchoredPosition = targetPos;

        // Fire/recoil: cannon tetikleniyor hissi.
        yield return PlayCannonFirePulse(cannon, targetPos);

        // Impact/fire anından sonra mevcut Column clear başlasın.
        exitRoutine?.Invoke(PlayCannonBoosterExitFx(cannon, canvasGroup, targetPos));
    }

    private IEnumerator PlayCannonFirePulse(RectTransform cannon, Vector2 basePos)
    {
        if (cannon == null || !cannon)
            yield break;

        const float recoilDuration = 0.075f;
        const float recoverDuration = 0.10f;

        Vector2 recoilPos = basePos + new Vector2(0f, -board.TileSize * 0.16f);

        Vector3 baseScale = cannon.localScale;
        Vector3 fireScale = new Vector3(
            baseScale.x * 1.08f,
            baseScale.y * 0.92f,
            baseScale.z);

        float t = 0f;

        while (t < recoilDuration)
        {
            if (cannon == null || !cannon)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / recoilDuration);
            float eased = k * k;

            cannon.anchoredPosition = Vector2.LerpUnclamped(basePos, recoilPos, eased);
            cannon.localScale = Vector3.LerpUnclamped(baseScale, fireScale, eased);

            yield return null;
        }

        t = 0f;

        while (t < recoverDuration)
        {
            if (cannon == null || !cannon)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / recoverDuration);
            float eased = EaseOut(k);

            cannon.anchoredPosition = Vector2.LerpUnclamped(recoilPos, basePos, eased);
            cannon.localScale = Vector3.LerpUnclamped(fireScale, baseScale, eased);

            yield return null;
        }

        if (cannon != null && cannon)
        {
            cannon.anchoredPosition = basePos;
            cannon.localScale = baseScale;
        }
    }

    private IEnumerator PlayCannonBoosterExitFx(RectTransform cannon, CanvasGroup canvasGroup, Vector2 targetPos)
    {
        if (cannon == null || !cannon)
            yield break;

        const float holdDuration = 0.05f;
        const float exitDuration = 0.18f;

        yield return new WaitForSeconds(holdDuration);

        Vector2 startPos = cannon.anchoredPosition;
        Vector2 exitPos = targetPos + new Vector2(0f, -board.TileSize * 1.7f);

        Vector3 startScale = cannon.localScale;
        Vector3 endScale = startScale * 0.92f;

        float t = 0f;

        while (t < exitDuration)
        {
            if (cannon == null || !cannon)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / exitDuration);
            float eased = k * k;

            cannon.anchoredPosition = Vector2.LerpUnclamped(startPos, exitPos, eased);
            cannon.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

            if (canvasGroup != null)
                canvasGroup.alpha = 1f - k;

            yield return null;
        }

        if (cannon != null && cannon)
            UnityEngine.Object.Destroy(cannon.gameObject);
    }

    private RectTransform CreateCannonFxInstance()
    {
        RectTransform prefab = board.CannonBoosterFxPrefab;

        if (prefab != null)
            return UnityEngine.Object.Instantiate(prefab);

        return CreateFallbackCannonFx();
    }

    private RectTransform CreateFallbackCannonFx()
    {
        GameObject root = new GameObject(
            "__CannonBoosterFx",
            typeof(RectTransform),
            typeof(CanvasGroup));

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 1f);
        rootRt.anchorMax = new Vector2(0f, 1f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(board.TileSize * 1.15f, board.TileSize * 1.15f);

        GameObject body = new GameObject(
            "Body",
            typeof(RectTransform),
            typeof(Image));

        body.transform.SetParent(rootRt, false);

        RectTransform bodyRt = body.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.anchoredPosition = Vector2.zero;
        bodyRt.sizeDelta = new Vector2(board.TileSize * 0.85f, board.TileSize * 0.85f);

        Image img = body.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(0.18f, 0.22f, 0.28f, 1f);

        return rootRt;
    }

    private Vector2 GetColumnBottomAnchoredCenter(int columnX, RectTransform parent)
    {
        float size = board.TileSize;

        if (TryGetColumnAnchoredCenter(columnX, parent, out var columnCenter))
        {
            // columnCenter.y = visual bottom row (Height-1) center.
            // Grid bottom line = columnCenter.y - size * 0.5f.
            // Cannon fires from the bottom grid line: center 1 tile below row center
            // = top of cannon at the grid bottom line.
            return new Vector2(
                columnCenter.x,
                columnCenter.y - size * 1.0f);
        }

        // Fallback: eski manuel hesap.
        float x = columnX * size + size * 0.5f;
        float y = -board.Height * size - size * 0.5f;
        return new Vector2(x, y);
    }

    private static float EaseOutBackLight(float t)
    {
        t = Mathf.Clamp01(t);

        const float c1 = 1.15f;
        const float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
    // ============================================================
    // HAMMER BOOSTER FX
    //
    // Single booster icin gorsel vurma animasyonu.
    // Taşi bu animasyon silmez; asil kirma MatchClearAction tarafinda kalir.
    // ============================================================

    private IEnumerator PlayHammerBoosterImpactFx(Vector2Int cell, Action<IEnumerator> exitRoutine)
    {
        RectTransform hammer = CreateHammerFxInstance();

        if (hammer == null)
            yield break;

        RectTransform parent = board.BoosterFxParent;
        if (parent == null)
        {
            UnityEngine.Object.Destroy(hammer.gameObject);
            yield break;
        }

        hammer.SetParent(parent, false);
        hammer.SetAsLastSibling();

        hammer.anchorMin = new Vector2(0f, 1f);
        hammer.anchorMax = new Vector2(0f, 1f);
        hammer.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup canvasGroup = hammer.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = hammer.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;

        Vector2 cellCenter = GetCellAnchoredCenter(cell, parent);

        // Hammer prefab/sprite görsel ağırlığı pivot'a göre yukarıda kaldığı için
        // root hedefini tile merkezinden biraz aşağı alıyoruz.
        // Eğer hâlâ yukarıda kalırsa 0.30f değerini 0.40f / 0.50f yapabilirsin.
        Vector2 targetPos = cellCenter + new Vector2(-board.TileSize * 0.60f, -board.TileSize * 0.01f);

        // Hareket iki fazlı:
        // 1) Çekiç önce yukarı kalkar ve Y eksenine/vertical poza yaklaşır.
        // 2) Sonra X eksenine/horizontal poza doğru hedefe vurur.
        Vector2 readyPos = targetPos + new Vector2(-board.TileSize * 0.08f, board.TileSize * 0.22f);
        Vector2 liftPos = targetPos + new Vector2(-board.TileSize * 0.18f, board.TileSize * 0.45f);
        Vector2 hitPos = targetPos + new Vector2(board.TileSize * 0.02f, -board.TileSize * 0.05f);

        hammer.anchoredPosition = readyPos;
        hammer.localScale = Vector3.one * 1.12f;

        // Sprite'ın 0 derecesini X ekseni/horizontal kabul ediyoruz.
        // Önce Y eksenine yaklaşır, sonra tekrar X eksenine vurur.
        const float readyAngle = 12f;
        const float liftAngle = 78f;
        const float hitAngle = 0f;

        hammer.localRotation = Quaternion.Euler(0f, 0f, readyAngle);

        const float liftDuration = 0.070f;
        const float strikeDuration = 0.085f;

        float t = 0f;

        // Lift: x ekseninden y eksenine doğru kalkış.
        while (t < liftDuration)
        {
            if (hammer == null || !hammer)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / liftDuration);
            float eased = EaseOut(k);

            hammer.anchoredPosition = Vector2.LerpUnclamped(readyPos, liftPos, eased);
            hammer.localRotation = Quaternion.LerpUnclamped(
                Quaternion.Euler(0f, 0f, readyAngle),
                Quaternion.Euler(0f, 0f, liftAngle),
                eased);
            hammer.localScale = Vector3.LerpUnclamped(Vector3.one * 1.12f, Vector3.one * 1.18f, eased);

            yield return null;
        }

        if (hammer == null || !hammer)
            yield break;

        t = 0f;

        // Strike: y ekseninden x eksenine doğru vuruş.
        while (t < strikeDuration)
        {
            if (hammer == null || !hammer)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / strikeDuration);
            float eased = k * k;

            hammer.anchoredPosition = Vector2.LerpUnclamped(liftPos, hitPos, eased);
            hammer.localRotation = Quaternion.LerpUnclamped(
                Quaternion.Euler(0f, 0f, liftAngle),
                Quaternion.Euler(0f, 0f, hitAngle),
                eased);
            hammer.localScale = Vector3.LerpUnclamped(Vector3.one * 1.18f, Vector3.one * 1.20f, eased);

            yield return null;
        }

        if (hammer == null || !hammer)
            yield break;

        // Impact frame.
        hammer.anchoredPosition = hitPos;
        hammer.localRotation = Quaternion.Euler(0f, 0f, hitAngle);
        hammer.localScale = Vector3.one * 1.20f;

        // Taşa küçük squash/pulse ver.
        yield return HammerImpactPulse(cell);

        // Impact anından sonra taş kırma başlasın.
        exitRoutine?.Invoke(PlayHammerBoosterExitFx(hammer, canvasGroup, targetPos));
    }
    private IEnumerator PlayHammerBoosterExitFx(RectTransform hammer, CanvasGroup canvasGroup, Vector2 targetPos)
    {
        if (hammer == null || !hammer)
            yield break;

        const float holdDuration = 0.035f;
        const float fadeDuration = 0.12f;

        yield return new WaitForSeconds(holdDuration);

        float t = 0f;

        Vector2 startPos = hammer.anchoredPosition;
        Vector2 exitPos = startPos + new Vector2(0f, board.TileSize * 0.20f);

        Vector3 startScale = hammer.localScale;
        Vector3 endScale = startScale * 0.92f;

        while (t < fadeDuration)
        {
            if (hammer == null || !hammer)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / fadeDuration);
            float eased = EaseOut(k);

            hammer.anchoredPosition = Vector2.LerpUnclamped(startPos, exitPos, eased);
            hammer.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

            // Artık çıkarken rotate etmiyoruz.
            hammer.localRotation = Quaternion.Euler(0f, 0f, 0f);

            if (canvasGroup != null)
                canvasGroup.alpha = 1f - k;

            yield return null;
        }

        if (hammer != null && hammer)
            UnityEngine.Object.Destroy(hammer.gameObject);
    }
    private IEnumerator HammerImpactPulse(Vector2Int cell)
    {
        TileView targetTile = board.GetTileViewAt(cell.x, cell.y);
        if (targetTile == null || !targetTile)
            yield break;

        RectTransform rt = targetTile.RectTransform;
        if (rt == null || !rt)
            yield break;

        Vector3 baseScale = rt.localScale;
        Vector2 basePos = rt.anchoredPosition;

        const float duration = 0.075f;
        float t = 0f;

        while (t < duration)
        {
            if (rt == null || !rt)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / duration);
            float punch = Mathf.Sin(k * Mathf.PI);

            rt.localScale = new Vector3(
                baseScale.x * (1f + 0.045f * punch),
                baseScale.y * (1f - 0.055f * punch),
                baseScale.z);

            rt.anchoredPosition = basePos + new Vector2(
                0f,
                -board.TileSize * 0.035f * punch);

            yield return null;
        }

        if (rt != null && rt)
        {
            rt.localScale = baseScale;
            rt.anchoredPosition = basePos;
        }
    }

    private RectTransform CreateHammerFxInstance()
    {
        RectTransform prefab = board.HammerBoosterFxPrefab;

        if (prefab != null)
            return UnityEngine.Object.Instantiate(prefab);

        return CreateFallbackHammerFx();
    }

    private RectTransform CreateFallbackHammerFx()
    {
        GameObject root = new GameObject(
            "__HammerBoosterFx",
            typeof(RectTransform),
            typeof(CanvasGroup));

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 1f);
        rootRt.anchorMax = new Vector2(0f, 1f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(board.TileSize * 1.25f, board.TileSize * 1.25f);

        Sprite fallbackSprite = board.HammerBoosterFallbackSprite;

        if (fallbackSprite != null)
        {
            Image img = root.AddComponent<Image>();
            img.sprite = fallbackSprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = Color.white;
            return rootRt;
        }

        CreateFallbackHammerHead(rootRt);
        CreateFallbackHammerHandle(rootRt);

        return rootRt;
    }

    private void CreateFallbackHammerHead(RectTransform root)
    {
        GameObject head = new GameObject(
            "Head",
            typeof(RectTransform),
            typeof(Image));

        head.transform.SetParent(root, false);

        RectTransform rt = head.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, board.TileSize * 0.20f);
        rt.sizeDelta = new Vector2(board.TileSize * 0.88f, board.TileSize * 0.32f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 0f);

        Image img = head.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(0.82f, 0.84f, 0.90f, 1f);
    }

    private void CreateFallbackHammerHandle(RectTransform root)
    {
        GameObject handle = new GameObject(
            "Handle",
            typeof(RectTransform),
            typeof(Image));

        handle.transform.SetParent(root, false);

        RectTransform rt = handle.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, board.TileSize * 0.08f);
        rt.sizeDelta = new Vector2(board.TileSize * 0.18f, board.TileSize * 0.78f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 0f);

        Image img = handle.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(0.60f, 0.36f, 0.18f, 1f);
    }

    private bool TryGetColumnAnchoredCenter(int columnX, RectTransform parent, out Vector2 center)
    {
        center = default;

        if (parent == null || columnX < 0 || columnX >= board.Width || board.Height <= 0)
            return false;

        // GetCellWorldCenterPosition works for any cell type (tile, obstacle, or hole).
        // Use the grid bottom row of this column as the reference point.
        Vector3 worldCenter = board.GetCellWorldCenterPosition(columnX, board.Height - 1);
        center = WorldToAnchoredInParent(parent, worldCenter, new Vector2(0.5f, 0.5f));
        return true;
    }

    private bool TryGetRowAnchoredCenter(int rowY, RectTransform parent, out Vector2 center)
    {
        center = default;

        if (parent == null || rowY < 0 || rowY >= board.Height || board.Width <= 0)
            return false;

        // GetCellWorldCenterPosition works for any cell type (tile, obstacle, or hole).
        // Use the grid left column of this row as the reference point.
        Vector3 worldCenter = board.GetCellWorldCenterPosition(0, rowY);
        center = WorldToAnchoredInParent(parent, worldCenter, new Vector2(0.5f, 0.5f));
        return true;
    }

    private Vector2 GetCellAnchoredCenter(Vector2Int cell, RectTransform parent)
    {
        if (parent != null)
        {
            TileView tile = board.GetTileViewAt(cell.x, cell.y);
            RectTransform tileRt = tile != null ? tile.RectTransform : null;

            if (tileRt != null)
            {
                Vector3 worldCenter = tileRt.TransformPoint(tileRt.rect.center);
                return WorldToAnchoredInParent(parent, worldCenter, new Vector2(0f, 1f));
            }
        }

        // Fallback: eski manuel hesap.
        float size = board.TileSize;
        return new Vector2(
            cell.x * size + size * 0.5f,
            -cell.y * size - size * 0.5f);
    }

    private static Vector2 WorldToAnchoredInParent(RectTransform parent, Vector3 worldPos, Vector2 childAnchor)
    {
        Vector2 localPos = parent.InverseTransformPoint(worldPos);
        Rect rect = parent.rect;
        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, childAnchor.x),
            Mathf.Lerp(rect.yMin, rect.yMax, childAnchor.y));
        return localPos - anchorReference;
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    private static float EaseInBackLight(float t)
    {
        t = Mathf.Clamp01(t);

        const float c1 = 1.25f;
        const float c3 = c1 + 1f;

        return c3 * t * t * t - c1 * t * t;
    }


    // ============================================================
    // VERTICAL / ROW BOOSTER FX
    //
    // Row booster icin gorsel animasyon.
    // Booster, secilen satirin basina yerlestirilir.
    // Govdenin buyuk kismi board disinda kalabilir.
    // Satiri bu animasyon silmez; asil temizleme MatchClearAction tarafinda kalir.
    // ============================================================

    private IEnumerator PlayVerticalBoosterEnterAndFireFx(int rowY, Action<IEnumerator> exitRoutine)
    {
        RectTransform booster = CreateVerticalBoosterFxInstance();

        if (booster == null)
            yield break;

        RectTransform parent = board.BoosterFxParent;
        if (parent == null)
        {
            UnityEngine.Object.Destroy(booster.gameObject);
            yield break;
        }

        booster.SetParent(parent, false);
        booster.SetAsLastSibling();

        booster.anchorMin = new Vector2(0.5f, 0.5f);
        booster.anchorMax = new Vector2(0.5f, 0.5f);
        booster.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup canvasGroup = booster.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = booster.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;

        // Ensure canvas layout is fully calculated before reading parent.rect or InverseTransformPoint.
        // On the very first activation parent.rect can be default-sized (zero), causing wrong positions.
        Canvas.ForceUpdateCanvases();

        Vector2 targetPos = GetRowStartAnchoredCenter(rowY, parent);

        // Soldan / ekran disindan biraz daha iceri kayarak gelsin.
        // Govde zaten disarida kalacagi icin start da target'a yakin.
        Vector2 startPos = targetPos + new Vector2(-board.TileSize * 0.55f, 0f);

        booster.anchoredPosition = startPos;
        booster.localScale = Vector3.one;
        booster.localRotation = Quaternion.identity;

        const float enterDuration = 0.16f;

        float t = 0f;

        while (t < enterDuration)
        {
            if (booster == null || !booster)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / enterDuration);
            float eased = EaseOut(k);

            booster.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, eased);

            yield return null;
        }

        if (booster == null || !booster)
            yield break;

        booster.anchoredPosition = targetPos;

        yield return PlayVerticalBoosterFirePulse(booster, targetPos);

        // Fire anindan sonra mevcut Row clear baslasin.
        exitRoutine?.Invoke(PlayVerticalBoosterExitFx(booster, canvasGroup, targetPos));
    }

    private IEnumerator PlayVerticalBoosterFirePulse(RectTransform booster, Vector2 basePos)
    {
        if (booster == null || !booster)
            yield break;

        const float recoilDuration = 0.07f;
        const float recoverDuration = 0.10f;

        // Row temizleme soldan saga oldugu icin recoil biraz sola.
        Vector2 recoilPos = basePos + new Vector2(-board.TileSize * 0.16f, 0f);

        Vector3 baseScale = booster.localScale;
        Vector3 fireScale = new Vector3(
            baseScale.x * 0.94f,
            baseScale.y * 1.06f,
            baseScale.z);

        float t = 0f;

        while (t < recoilDuration)
        {
            if (booster == null || !booster)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / recoilDuration);
            float eased = k * k;

            booster.anchoredPosition = Vector2.LerpUnclamped(basePos, recoilPos, eased);
            booster.localScale = Vector3.LerpUnclamped(baseScale, fireScale, eased);

            yield return null;
        }

        t = 0f;

        while (t < recoverDuration)
        {
            if (booster == null || !booster)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / recoverDuration);
            float eased = EaseOut(k);

            booster.anchoredPosition = Vector2.LerpUnclamped(recoilPos, basePos, eased);
            booster.localScale = Vector3.LerpUnclamped(fireScale, baseScale, eased);

            yield return null;
        }

        if (booster != null && booster)
        {
            booster.anchoredPosition = basePos;
            booster.localScale = baseScale;
        }
    }

    private IEnumerator PlayVerticalBoosterExitFx(RectTransform booster, CanvasGroup canvasGroup, Vector2 targetPos)
    {
        if (booster == null || !booster)
            yield break;

        const float holdDuration = 0.04f;
        const float exitDuration = 0.14f;

        yield return new WaitForSeconds(holdDuration);

        Vector2 startPos = booster.anchoredPosition;
        Vector2 exitPos = targetPos + new Vector2(-board.TileSize * 0.85f, 0f);

        Vector3 startScale = booster.localScale;
        Vector3 endScale = startScale * 0.94f;

        float t = 0f;

        while (t < exitDuration)
        {
            if (booster == null || !booster)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / exitDuration);
            float eased = k * k;

            booster.anchoredPosition = Vector2.LerpUnclamped(startPos, exitPos, eased);
            booster.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

            if (canvasGroup != null)
                canvasGroup.alpha = 1f - k;

            yield return null;
        }

        if (booster != null && booster)
            UnityEngine.Object.Destroy(booster.gameObject);
    }

    private RectTransform CreateVerticalBoosterFxInstance()
    {
        RectTransform prefab = board.VerticalBoosterFxPrefab;

        if (prefab != null)
            return UnityEngine.Object.Instantiate(prefab);

        return null;
    }

    private Vector2 GetRowStartAnchoredCenter(int rowY, RectTransform parent)
    {
        float size = board.TileSize;

        if (TryGetRowAnchoredCenter(rowY, parent, out var rowCenter))
        {
            // rowCenter.x = visual left column (0) center X.
            // Grid left line = rowCenter.x - size * 0.5f.
            // Ideal: booster right edge at grid left line = center at gridLeft - boosterHalfW.
            // Clamp: keep booster inside screen. Screen left in (0.5,0.5) anchor space = -width/2
            // regardless of parent pivot — rect.width is pivot-independent.
            float gridLeftX   = rowCenter.x - size * 0.5f;
            float halfW       = size * 0.5f;
            float screenLeftX = -parent.rect.width * 0.5f;
            float targetX     = Mathf.Max(gridLeftX - halfW, screenLeftX + halfW);
            return new Vector2(targetX, rowCenter.y);
        }

        // Fallback: eski manuel hesap.
        float y = -rowY * size - size * 0.5f;
        float x = -size * 0.5f;
        return new Vector2(x, y);
    }

    public IEnumerator ShuffleBoardRoutine(ActionSequencer actionSequencer)
    {
        yield return SafeShuffleBoardRoutine(board.BoardInitService);
    }

    // BoosterService.cs

    public IEnumerator SafeShuffleBoardRoutine(BoardInitService boardInitService)
    {
        Debug.Log("[Shuffle] SafeShuffleBoardRoutine START");
        board.BeginBusy();

        var currentTypes = new TileType[board.Width, board.Height];
        var lockedMask = new bool[board.Width, board.Height];

        BuildSafeShuffleState(currentTypes, lockedMask);

        int unlockedCount = 0;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (!lockedMask[x, y]) unlockedCount++;
        Debug.Log($"[Shuffle] Unlocked cells: {unlockedCount} / {board.Width * board.Height}");

        if (boardInitService == null)
        {
            Debug.LogError("[Shuffle] boardInitService NULL! Shuffle çalışmaz.");
            board.EndBusy();
            yield break;
        }

        // Initial simulation gibi: randomPool'dan tek tek yerleştir, match oluşmadan.
        var simResult = boardInitService.SimulateInitialTypes(
            board.Width, board.Height, lockedMask, board.RandomPool);

        // Locked hücrelerin tiplerini koru (special, obstacle vb.)
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (lockedMask[x, y])
                    simResult[x, y] = currentTypes[x, y];

        TileType[,] finalTypes = simResult;
        bool ok = finalTypes != null;
        Debug.Log($"[Shuffle] SimulateInitialTypes returned ok={ok}");

        if (ok)
        {
            var sourceForDest = new Vector2Int[board.Width, board.Height];

            bool hasMapping = BuildShuffleSourceMap(currentTypes, finalTypes, lockedMask, sourceForDest);
            Debug.Log($"[Shuffle] BuildShuffleSourceMap hasMapping={hasMapping}");

            if (hasMapping)
            {
                yield return AnimateShufflePreview(sourceForDest, lockedMask);
                CommitShuffleFromSourceMap(sourceForDest, lockedMask);
            }
            else
            {
                Debug.LogWarning("[Shuffle] Mapping failed, applying types directly (no animation)");
                ApplyShuffledTypes(finalTypes, lockedMask);
            }

            board.SyncAllTilesToGridData();
            board.RefreshAllTileObstacleVisuals();
            board.RefreshAllSortingOrders();
            Debug.Log("[Shuffle] COMPLETE");
        }
        else
        {
            Debug.LogWarning("[Shuffle] TryBuildSafeShuffleTypes FAILED — board değişmedi.");
        }

        board.EndBusy();
    }

    private bool BuildShuffleSourceMap(
        TileType[,] currentTypes,
        TileType[,] finalTypes,
        bool[,] lockedMask,
        Vector2Int[,] sourceForDest)
    {
        var buckets = new Dictionary<TileType, Queue<Vector2Int>>();

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                sourceForDest[x, y] = new Vector2Int(x, y);

                if (lockedMask[x, y])
                    continue;

                var type = currentTypes[x, y];
                if (!buckets.TryGetValue(type, out var q))
                {
                    q = new Queue<Vector2Int>();
                    buckets[type] = q;
                }

                q.Enqueue(new Vector2Int(x, y));
            }
        }

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (lockedMask[x, y])
                    continue;

                var finalType = finalTypes[x, y];

                if (!buckets.TryGetValue(finalType, out var q) || q.Count == 0)
                    return false;

                sourceForDest[x, y] = q.Dequeue();
            }
        }

        return true;
    }

    private IEnumerator AnimateShufflePreview(Vector2Int[,] sourceForDest, bool[,] lockedMask)
    {
        var movingTiles = new List<TileView>();
        var starts = new List<Vector2>();
        var ends = new List<Vector2>();

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (lockedMask[x, y])
                    continue;

                var src = sourceForDest[x, y];
                if (src.x == x && src.y == y)
                    continue;

                var tile = board.Tiles[src.x, src.y];
                if (tile == null)
                    continue;

                movingTiles.Add(tile);
                starts.Add(tile.RectTransform.anchoredPosition);
                ends.Add(new Vector2(x * board.TileSize, -y * board.TileSize));

                // üstte çizilsin
                tile.transform.SetAsLastSibling();
            }
        }

        if (movingTiles.Count == 0)
            yield break;

        float duration = Mathf.Max(0.08f, board.SwapDurationWithMultiplier * 0.85f);
        var curve = board.SwapMoveCurve;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            float s = (curve != null && curve.length > 0)
                ? Mathf.Clamp01(curve.Evaluate(k))
                : k;

            for (int i = 0; i < movingTiles.Count; i++)
            {
                var tile = movingTiles[i];
                if (tile == null)
                    continue;

                tile.RectTransform.anchoredPosition = Vector2.LerpUnclamped(starts[i], ends[i], s);
            }

            yield return null;
        }

        for (int i = 0; i < movingTiles.Count; i++)
        {
            var tile = movingTiles[i];
            if (tile == null)
                continue;

            tile.RectTransform.anchoredPosition = ends[i];
        }
    }

    private void CommitShuffleFromSourceMap(Vector2Int[,] sourceForDest, bool[,] lockedMask)
    {
        var snapshot = new TileView[board.Width, board.Height];

        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                snapshot[x, y] = board.Tiles[x, y];

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (lockedMask[x, y])
                    continue;

                var src = sourceForDest[x, y];
                var tile = snapshot[src.x, src.y];

                board.Tiles[x, y] = tile;

                if (tile != null)
                {
                    tile.SetCoords(x, y);
                    tile.SnapToGrid(board.TileSize);
                    board.RefreshTileObstacleVisual(tile);
                }
            }
        }
    }

    public void AddRow(HashSet<TileView> matches, int y)
    {
        if (y < 0 || y >= board.Height)
            return;

        for (int x = 0; x < board.Width; x++)
            if (!board.Holes[x, y] && board.Tiles[x, y] != null)
                matches.Add(board.Tiles[x, y]);
    }

    public void AddColumn(HashSet<TileView> matches, int x)
    {
        if (x < 0 || x >= board.Width)
            return;

        for (int y = 0; y < board.Height; y++)
            if (!board.Holes[x, y] && board.Tiles[x, y] != null)
                matches.Add(board.Tiles[x, y]);
    }

    public void AddRowCells(HashSet<Vector2Int> affectedCells, int y)
    {
        if (affectedCells == null || y < 0 || y >= board.Height)
            return;

        for (int x = 0; x < board.Width; x++)
            if (IsCellBoosterAffectable(x, y))
                affectedCells.Add(new Vector2Int(x, y));
    }

    public void AddColumnCells(HashSet<Vector2Int> affectedCells, int x)
    {
        if (affectedCells == null || x < 0 || x >= board.Width)
            return;

        for (int y = 0; y < board.Height; y++)
            if (IsCellBoosterAffectable(x, y))
                affectedCells.Add(new Vector2Int(x, y));
    }

    public bool IsCellBoosterAffectable(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (!board.Holes[x, y])
            return true;

        return board.ObstacleStateService != null && board.ObstacleStateService.HasObstacleAt(x, y);
    }

    private void BuildSafeShuffleState(TileType[,] currentTypes, bool[,] lockedMask)
    {
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                bool locked = false;

                if (board.Holes[x, y])
                {
                    locked = true;
                }
                else
                {
                    var tile = board.Tiles[x, y];
                    if (tile == null)
                    {
                        locked = true;
                    }
                    else if (tile.GetSpecial() != TileSpecial.None)
                    {
                        locked = true;
                    }
                    else if (board.ObstacleStateService != null &&
                             board.ObstacleStateService.IsMovableObstacleAt(x, y))
                    {
                        locked = true;
                    }
                }

                lockedMask[x, y] = locked;

                var tv = board.Tiles[x, y];
                currentTypes[x, y] = tv != null ? tv.GetTileType() : default;
            }
        }
    }

    private void ApplyShuffledTypes(TileType[,] finalTypes, bool[,] lockedMask)
    {
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (lockedMask[x, y])
                    continue;

                var tile = board.Tiles[x, y];
                if (tile == null)
                    continue;

                tile.SetType(finalTypes[x, y]);
                board.SyncTileData(x, y);
                board.RefreshTileObstacleVisual(tile);
            }
        }
    }
}