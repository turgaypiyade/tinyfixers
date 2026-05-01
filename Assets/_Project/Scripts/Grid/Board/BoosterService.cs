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

        // BoardRoot coordinate sistemiyle aynı çalışsın.
        cannon.anchorMin = new Vector2(0f, 1f);
        cannon.anchorMax = new Vector2(0f, 1f);
        cannon.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup canvasGroup = cannon.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = cannon.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;

        Vector2 targetPos = GetColumnBottomAnchoredCenter(columnX);

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

    private Vector2 GetColumnBottomAnchoredCenter(int columnX)
    {
        float size = board.TileSize;

        // Hücrenin X merkezi.
        float x = columnX * size + size * 0.5f + size * 0.15f;

        // Grid'in altı: son row'un altından biraz aşağı.
        // Board top-left anchored olduğu için aşağı yön negatif Y.
        float y = -board.Height * size - size * 0.88f;

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

        Vector2 targetPos = GetCellAnchoredCenter(cell);

        // Başlangıçta çekiç hedefin biraz üst-solunda dursun.
        // UI coordinate sisteminde yukarı = pozitif Y.
        Vector2 startPos = targetPos + new Vector2(-board.TileSize * 0.10f, board.TileSize * 0.14f);

        // Vuruş anında merkeze biraz daha yaklaşsın.
        Vector2 hitPos = targetPos + new Vector2(board.TileSize * 0.015f, -board.TileSize * 0.03f);

        hammer.anchoredPosition = startPos;
        hammer.localScale = Vector3.one * 1.15f;

        // Senin istediğin hareket:
        // 45 derece diyagonal başla, 0 dereceye vur.
        const float startAngle = 45f;
        const float hitAngle = -20f;

        hammer.localRotation = Quaternion.Euler(0f, 0f, startAngle);

        // Çok kısa ama görülebilir swing.
        // 1 frame çok hızlı kaçabiliyor; bu yüzden 0.07 sn daha iyi vuruş hissi verir.
        const float swingDuration = 0.085f;

        float t = 0f;

        while (t < swingDuration)
        {
            if (hammer == null || !hammer)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / swingDuration);

            // Hızlanarak vurur.
            float eased = k * k;

            hammer.anchoredPosition = Vector2.LerpUnclamped(startPos, hitPos, eased);
            hammer.localRotation = Quaternion.LerpUnclamped(
                Quaternion.Euler(0f, 0f, startAngle),
                Quaternion.Euler(0f, 0f, hitAngle),
                eased);

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

    private Vector2 GetCellAnchoredCenter(Vector2Int cell)
    {
        float size = board.TileSize;

        return new Vector2(
            cell.x * size + size * 0.5f,
            -cell.y * size - size * 0.5f);
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

        // BoardRoot coordinate sistemiyle ayni calissin.
        booster.anchorMin = new Vector2(0f, 1f);
        booster.anchorMax = new Vector2(0f, 1f);
        booster.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup canvasGroup = booster.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = booster.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;

        Vector2 targetPos = GetRowStartAnchoredCenter(rowY);

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

    private Vector2 GetRowStartAnchoredCenter(int rowY)
    {
        float size = board.TileSize;

        // Satirin Y merkezi.
        float y = -rowY * size - size * 1.5f;

        // Board'un sol disina koyuyoruz.
        // Bu degeri arttirip azaltarak govdenin ne kadar disarida kalacagini ayarlarsin.
        float x = -size * 0.10f;

        return new Vector2(x, y);
    }

    public IEnumerator ShuffleBoardRoutine(ActionSequencer actionSequencer)
    {
        yield return SafeShuffleBoardRoutine(board.BoardInitService);
    }

    // BoosterService.cs

    public IEnumerator SafeShuffleBoardRoutine(BoardInitService boardInitService)
    {
        board.BeginBusy();

        var currentTypes = new TileType[board.Width, board.Height];
        var lockedMask = new bool[board.Width, board.Height];

        BuildSafeShuffleState(currentTypes, lockedMask);

        if (boardInitService != null &&
            boardInitService.TryBuildSafeShuffleTypes(currentTypes, lockedMask, board.RandomPool, out var finalTypes))
        {
            var sourceForDest = new Vector2Int[board.Width, board.Height];

            bool hasMapping = BuildShuffleSourceMap(currentTypes, finalTypes, lockedMask, sourceForDest);

            if (hasMapping)
            {
                yield return AnimateShufflePreview(sourceForDest, lockedMask);
                CommitShuffleFromSourceMap(sourceForDest, lockedMask);
            }
            else
            {
                // çok nadir fallback
                ApplyShuffledTypes(finalTypes, lockedMask);
            }

            board.SyncAllTilesToGridData();
            board.RefreshAllTileObstacleVisuals();
            board.RefreshAllSortingOrders();
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