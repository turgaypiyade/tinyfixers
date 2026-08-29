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
                // Cargo (exitAtBottom) KIRILMAZ — hammer/joker ile vurulsa da etkilenmez.
                if (target != null && !IsUnbreakableCargo(target.X, target.Y))
                    matches.Add(target);

                if (hasValidTargetCell && !IsUnbreakableCargo(targetCell.Value.x, targetCell.Value.y)
                    && IsCellBoosterAffectable(targetCell.Value.x, targetCell.Value.y))
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

            // Mini Elevator booster: sadece DÜZ sütun temizliğinde (özel/zincir yokken) devreye girer.
            // Sütunda special varsa (chain) mevcut lightning davranışı korunur → özel aktivasyonu bozulmaz.
            bool useElevatorClear = mode == BoardController.BoosterMode.Column
                && !hasLineActivation
                && chainLineStrikes.Count == 0
                && matches.Count > 0;

            ClearAnimationMode animationMode;
            if (useElevatorClear)
            {
                animationMode = ClearAnimationMode.ElevatorLift;
            }
            else
            {
                animationMode = (mode == BoardController.BoosterMode.Row || mode == BoardController.BoosterMode.Column)
                    ? ClearAnimationMode.LightningStrike
                    : ClearAnimationMode.Default;

                if (hasLineActivation)
                    animationMode = ClearAnimationMode.LightningStrike;
            }

            // Asansör alttan yukarı tararken taşları sıra ile temizler: her taşın gecikmesi
            // ((Height-1) - y) * step. Aynı step, asansör görselinin yükseliş hızını da sürer (senkron).
            Dictionary<TileView, float> elevatorClearDelays = null;
            if (useElevatorClear)
            {
                elevatorClearDelays = new Dictionary<TileView, float>(matches.Count);
                int gridHeight = board.Height;
                foreach (var tv in matches)
                {
                    if (tv == null) continue;
                    int order = gridHeight > 0 ? (gridHeight - 1 - tv.Y) : tv.Y;
                    elevatorClearDelays[tv] = Mathf.Max(0, order) * ElevatorStepDelay;
                }
            }

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
                        mode == BoardController.BoosterMode.Row,
                        startDelaySeconds: 0f,
                        // Row booster (joker) drill VFX kullanır; LineH special değil.
                        useDrillSweep: mode == BoardController.BoosterMode.Row));
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
                if (useElevatorClear)
                {
                    IEnumerator elevatorLift = null;
                    yield return PlayElevatorBoosterEnterFx(targetCell.Value.x, liftRoutine: r => elevatorLift = r);

                    if (elevatorLift != null)
                        cannonExitRoutine = board.StartCoroutine(elevatorLift);
                }
                else
                {
                    IEnumerator cannonExit = null;
                    yield return PlayCannonBoosterEnterAndFireFx(targetCell.Value.x, exitRoutine: r => cannonExit = r);

                    if (cannonExit != null)
                        cannonExitRoutine = board.StartCoroutine(cannonExit);
                }
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
                perTileClearDelays: elevatorClearDelays,
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

        RectTransform parent = GetBoosterFxParent();
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

    // ============================================================
    // MINI ELEVATOR / SERVİS ASANSÖRÜ BOOSTER FX
    //
    // Column booster'ın yeni görseli: platform sütunun altına kayar, sonra yukarı
    // "araba kaldıracı" gibi yükselir. Taşları bu görsel silmez — asıl temizlik
    // MatchClearAction (ElevatorLift) tarafında, aynı step ile senkron savurma ile olur.
    // Görsel şimdilik cannon prefabını (placeholder) kullanır; asıl asansör sprite'ı
    // board.CannonBoosterFxPrefab değiştirilerek takılır.
    // ============================================================

    private const float ElevatorStepDelay = 0.05f;

    // ── Joker/booster tek-atış SFX (Resources/Audio/Jokers/*) ─────────────────
    private static readonly Dictionary<string, AudioClip> _jokerSfxCache = new Dictionary<string, AudioClip>();

    private void PlayJokerSfx(string fileName, float volume = 1f)
    {
        if (board == null || board.Audio == null || string.IsNullOrEmpty(fileName))
            return;

        if (!_jokerSfxCache.TryGetValue(fileName, out AudioClip clip) || clip == null)
        {
            clip = Resources.Load<AudioClip>("Audio/Jokers/" + fileName);
            _jokerSfxCache[fileName] = clip;
        }

        if (clip != null)
            board.Audio.PlayOneShotClip(clip, volume);
    }

    private IEnumerator PlayElevatorBoosterEnterFx(int columnX, Action<IEnumerator> liftRoutine)
    {
        // BoosterFxParent = BoardMask ve grid'e MASKELİYOR (çizginin ~12px altı kırpılır) → makasın
        // tabanı/altı kesiliyordu. Makası maskenin DIŞINA (BoardMask.parent) koyuyoruz → kırpılmaz,
        // çizginin altına serbestçe iner. Mask'in hemen ÖNÜNE koyup board üstünde görünür yaparız.
        RectTransform maskParent = GetBoosterFxParent();
        RectTransform parent = maskParent != null ? maskParent.parent as RectTransform : null;
        int inFrontIndex = -1;
        if (parent != null && maskParent != null)
            inFrontIndex = maskParent.GetSiblingIndex() + 1;
        else
            parent = maskParent;   // fallback
        if (parent == null)
            yield break;

        // Makaslı asansör (scissor lift) — Resources/MiniLift parçalarından prosedürel inşa.
        var go = new GameObject("__ScissorLiftFx", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = parent.gameObject.layer;   // Screen Space Camera culling'e karşı

        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        if (inFrontIndex >= 0)
            rt.SetSiblingIndex(Mathf.Min(inFrontIndex, parent.childCount - 1));   // mask'in hemen önü
        else
            rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        Canvas.ForceUpdateCanvases();

        Vector2 bottomRowCenter;
        if (!TryGetColumnAnchoredCenter(columnX, parent, out bottomRowCenter))
            bottomRowCenter = GetColumnBottomAnchoredCenter(columnX, parent);

        // Taban ~cannon referansına (alt satır merkezinin ~1 tile altı) otursun. Ön katman KIRPMADIĞI
        // için serbestçe çizginin altına inebilir; collapsed kademe-bağımsız olduğu için tüm grid
        // yüksekliklerinde tutarlı. (restPos = bottomRowCenter - 0.5 - lowerOffset ≈ cannon noktası.)
        float lowerOffset = board.TileSize * 0.7f;
        Vector2 restPos  = new Vector2(bottomRowCenter.x, bottomRowCenter.y - board.TileSize * 0.5f - lowerOffset);
        Vector2 startPos = restPos + new Vector2(0f, -board.TileSize * 2.2f);   // ekranın altından gelir

        rt.anchoredPosition = startPos;

        var view = go.AddComponent<ScissorLiftView>();
        // Tabla en tepede ~üst satıra ulaşsın diye lowerOffset yükseklik bütçesine geri eklenir.
        float maxHeightUI = Mathf.Max(1f, (board.Height - 1) * board.TileSize + lowerOffset);
        view.Build(maxHeightUI, board.TileSize);

        PlayJokerSfx("Mini1");   // slide-in

        // Kapalı makas ekranın altından dinlenme konumuna KAYARAK gelir (slide-in).
        const float enterDuration = 0.30f;
        float t = 0f;
        while (t < enterDuration)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / enterDuration);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, restPos, EaseOutBackLight(k));
            yield return null;
        }
        if (go == null) yield break;
        rt.anchoredPosition = restPos;

        // Kaldırma + çıkış, clear ile paralel çalışsın diye lift routine olarak devredilir.
        liftRoutine?.Invoke(PlayScissorLiftAndExitFx(go, view));
    }

    private IEnumerator PlayScissorLiftAndExitFx(GameObject go, ScissorLiftView view)
    {
        if (go == null || view == null)
            yield break;

        PlayJokerSfx("Mini2");   // yükseliş

        // Lineer açılım = taş gecikmeleriyle (ElevatorStepDelay) senkron.
        float riseDuration = Mathf.Max(0.12f, (board.Height - 1) * ElevatorStepDelay);
        float t = 0f;

        while (t < riseDuration)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            view.SetExtension01(Mathf.Clamp01(t / riseDuration));
            yield return null;
        }

        if (go == null) yield break;
        view.SetExtension01(1f);

        PlayJokerSfx("mini3");   // tepe / bırakma

        // Tepede kısa bekleme.
        yield return new WaitForSeconds(0.06f);

        // Çıkış: hafif geri toplanarak sönme.
        const float exitDuration = 0.18f;
        t = 0f;
        while (t < exitDuration)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / exitDuration);
            view.SetExtension01(Mathf.Lerp(1f, 0.82f, k));
            view.Alpha = 1f - k;
            yield return null;
        }

        if (go != null)
            UnityEngine.Object.Destroy(go);
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
        RectTransform parent = GetBoosterFxParent();
        if (parent == null)
            yield break;

        RectTransform hammer = CreateHm2HammerFx(parent);
        if (hammer == null)
            yield break;

        CanvasGroup canvasGroup = hammer.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = hammer.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        Canvas.ForceUpdateCanvases();

        // Başlangıç: Hammer joker slotunun ÜZERİNDEN, joker boyutunda.
        Vector2 startPos;
        Vector2 jokerSize;
        if (!TryGetHammerSlotStart(parent, out startPos, out jokerSize))
        {
            // Fallback: sol-alt köşe, ~tile boyutu.
            jokerSize = new Vector2(board.TileSize, board.TileSize);
            startPos = GetCellAnchoredCenter(new Vector2Int(0, board.Height - 1), parent)
                       + new Vector2(-board.TileSize, -board.TileSize);
        }

        // Boyutu makul aralığa sıkıştır (cross-canvas ölçek hatası dev/minik yapmasın).
        float baseSize = board.TileSize;
        jokerSize = new Vector2(
            Mathf.Clamp(jokerSize.x, baseSize * 0.6f, baseSize * 1.6f),
            Mathf.Clamp(jokerSize.y, baseSize * 0.6f, baseSize * 1.6f));

        hammer.sizeDelta = jokerSize;
        Vector2 targetPos = GetCellAnchoredCenter(cell, parent);

        hammer.anchoredPosition = startPos;
        hammer.localScale = Vector3.one;                        // joker boyutu (1x)
        hammer.localRotation = Quaternion.Euler(0f, 0f, 18f);   // hafif ön açı (saat yönü tersi)

        PlayJokerSfx("Hammerswing");

        // Açı ayarları (2D, Z ekseni): cock = geri çekilmeden ÖNCE saat YÖNÜ TERSİNE (sola/dikeye) dönüş.
        const float cockAngle = 25f;    // saat yönü tersi, dikeye yakın.
        const float hitAngle  = 6f;     // vuruş anı açısı.

        // FAZ A: slottan taşın biraz ÜST-GERİSİNE yaklaş + büyü (1 → 2.1). Düz yaklaşır.
        const float travelDuration = 0.30f;
        Vector2 readyPos = targetPos + new Vector2(-board.TileSize * 0.12f, board.TileSize * 0.45f);
        Vector2 arcPeak = (startPos + readyPos) * 0.5f + new Vector2(0f, board.TileSize * 0.8f);
        float t = 0f;
        while (t < travelDuration)
        {
            if (hammer == null || !hammer) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / travelDuration);
            float e = EaseOut(k);

            Vector2 a = Vector2.LerpUnclamped(startPos, arcPeak, e);
            Vector2 b = Vector2.LerpUnclamped(arcPeak, readyPos, e);
            hammer.anchoredPosition = Vector2.LerpUnclamped(a, b, e);
            hammer.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 2.1f, e);
            hammer.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(18f, 0f, e));
            yield return null;
        }
        if (hammer == null || !hammer) yield break;

        // FAZ COCK: GERİ ÇEKİLMEDEN ÖNCE sola/dikeye döndür (kaldır). Konum readyPos'ta sabit.
        const float cockDuration = 0.13f;
        t = 0f;
        while (t < cockDuration)
        {
            if (hammer == null || !hammer) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / cockDuration);
            float e = EaseOut(k);
            hammer.anchoredPosition = readyPos;
            hammer.localScale = Vector3.one * Mathf.LerpUnclamped(2.1f, 2.3f, e);
            hammer.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(0f, cockAngle, e));
            yield return null;
        }
        if (hammer == null || !hammer) yield break;

        // FAZ WIND-UP: SONRA geri çekil (readyPos → pullbackPos) + BÜYÜ (2.3 → 2.8), cock açısını KORU.
        Vector2 pullbackPos = targetPos + new Vector2(-board.TileSize * 0.30f, board.TileSize * 1.05f);
        const float windupDuration = 0.16f;
        t = 0f;
        while (t < windupDuration)
        {
            if (hammer == null || !hammer) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / windupDuration);
            float e = EaseOut(k);   // yavaşlayarak geri çekilir (yaylanma öncesi)
            hammer.anchoredPosition = Vector2.LerpUnclamped(readyPos, pullbackPos, e);
            hammer.localScale = Vector3.one * Mathf.LerpUnclamped(2.3f, 2.8f, e);
            hammer.localRotation = Quaternion.Euler(0f, 0f, cockAngle);   // cocked açı sabit
            yield return null;
        }
        if (hammer == null || !hammer) yield break;

        // FAZ SLAM: cocked açıdan aşağı SERT SAVUR (cockAngle → hitAngle); hızlanarak; 2.8 → 2.1.
        const float slamDuration = 0.09f;
        t = 0f;
        while (t < slamDuration)
        {
            if (hammer == null || !hammer) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / slamDuration);
            float e = k * k;   // easeIn → hızlanan sert vuruş
            hammer.anchoredPosition = Vector2.LerpUnclamped(pullbackPos, targetPos, e);
            hammer.localScale = Vector3.one * Mathf.LerpUnclamped(2.8f, 2.1f, e);
            hammer.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(cockAngle, hitAngle, e));
            yield return null;
        }
        if (hammer == null || !hammer) yield break;

        // VURUŞ ANI: alev + ses + squash.
        hammer.anchoredPosition = targetPos;
        hammer.localScale = Vector3.one * 2.1f;
        hammer.localRotation = Quaternion.Euler(0f, 0f, hitAngle);
        board.PatchbotDashUI?.PlayImpactBurstAtCell(board, cell, 1.35f);
        PlayJokerSfx("HammerHit");

        yield return HammerImpactPulse(cell);

        // Impact anından sonra taş kırma başlasın; hammer paralel olarak düşerek kaybolur.
        exitRoutine?.Invoke(PlayHammerFallFx(hammer, targetPos));
    }

    // Vuruştan sonra: orijinal (joker) boyuta dön, sonra yerçekimiyle serbest düşüş → ekran altında yok ol.
    private IEnumerator PlayHammerFallFx(RectTransform hammer, Vector2 hitPos)
    {
        if (hammer == null || !hammer)
            yield break;

        PlayJokerSfx("HammerFalling");

        // Orijinal boyuta (1x) dön, hafif düşmeye başla.
        const float settleDuration = 0.10f;
        Vector3 fromScale = hammer.localScale;   // ~2.0x
        Vector2 p = hitPos;
        float t = 0f;
        while (t < settleDuration)
        {
            if (hammer == null || !hammer) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / settleDuration);
            hammer.localScale = Vector3.LerpUnclamped(fromScale, Vector3.one, EaseOut(k));
            p.y -= board.TileSize * 0.15f * (Time.deltaTime / settleDuration);
            hammer.anchoredPosition = p;
            yield return null;
        }
        if (hammer == null || !hammer) yield break;
        hammer.localScale = Vector3.one;

        // Serbest düşüş: yerçekimi ivmesi + hafif tumble. Ekran altına inince yok ol.
        float vy = board.TileSize * 1.5f;
        float gravity = board.TileSize * 45f;
        float rot = hammer.localRotation.eulerAngles.z;
        float rotSpeed = UnityEngine.Random.Range(-160f, 160f);
        float limitY = hitPos.y - board.TileSize * (board.Height + 4);

        while (hammer != null && hammer)
        {
            float dt = Time.deltaTime;
            vy += gravity * dt;
            p.y -= vy * dt;
            hammer.anchoredPosition = p;
            rot += rotSpeed * dt;
            hammer.localRotation = Quaternion.Euler(0f, 0f, rot);
            if (p.y <= limitY) break;
            yield return null;
        }

        if (hammer != null && hammer)
            UnityEngine.Object.Destroy(hammer.gameObject);
    }

    // HM2 sprite'lı hammer görseli üretir (Resources/Boosters/HM2).
    private RectTransform CreateHm2HammerFx(RectTransform parent)
    {
        var go = new GameObject("__HammerBoosterFx", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.layer = parent.gameObject.layer;   // Screen Space Camera culling guard

        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.SetAsLastSibling();
        // KRİTİK: GetCellAnchoredCenter / ScreenToParentAnchored (0,1) top-left uzayında pozisyon
        // veriyor → hammer da (0,1) anchor olmalı, yoksa yarım-parent kadar kayıp ekran dışına gider.
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.sprite = Resources.Load<Sprite>("Boosters/HM2");
        img.color = Color.white;
        img.raycastTarget = false;
        img.preserveAspect = true;
        return rt;
    }

    // Hammer joker slotunun (index 0) ikon konumu + boyutunu FX parent uzayına çevirir.
    // Slot farklı bir canvas'ta olabileceği için dönüşüm SCREEN SPACE üzerinden yapılır (cross-canvas güvenli).
    private bool TryGetHammerSlotStart(RectTransform parent, out Vector2 startPos, out Vector2 sizeLocal)
    {
        startPos = default;
        sizeLocal = default;

        var slots = UnityEngine.Object.FindObjectsByType<BoosterSlotView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        BoosterSlotView hammerSlot = null;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].ResolvedIndex == 0) { hammerSlot = slots[i]; break; }

        RectTransform iconRt = hammerSlot != null ? hammerSlot.IconRect : null;
        if (iconRt == null || !iconRt)
            return false;

        var corners = new Vector3[4];
        iconRt.GetWorldCorners(corners);           // 0=BL, 2=TR (world)
        Camera iconCam = CanvasCameraFor(iconRt);
        Vector2 sBL = RectTransformUtility.WorldToScreenPoint(iconCam, corners[0]);
        Vector2 sTR = RectTransformUtility.WorldToScreenPoint(iconCam, corners[2]);

        Vector2 aBL = ScreenToParentAnchored(parent, sBL);
        Vector2 aTR = ScreenToParentAnchored(parent, sTR);

        startPos = (aBL + aTR) * 0.5f;
        sizeLocal = new Vector2(Mathf.Abs(aTR.x - aBL.x), Mathf.Abs(aTR.y - aBL.y));

        if (sizeLocal.x < 1f || sizeLocal.y < 1f)
            sizeLocal = new Vector2(board.TileSize, board.TileSize);

        return true;
    }

    // Ekran noktasını, FX parent'ın (0.5,0.5) anchor'lı çocuk uzayına (anchoredPosition) çevirir.
    private static Vector2 ScreenToParentAnchored(RectTransform parent, Vector2 screenPoint)
    {
        Camera cam = CanvasCameraFor(parent);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, cam, out Vector2 local);
        Rect rect = parent.rect;
        // (0,1) top-left anchor referansı — GetCellAnchoredCenter ile aynı uzay.
        Vector2 anchorRef = new Vector2(rect.xMin, rect.yMax);
        return local - anchorRef;
    }

    private static Camera CanvasCameraFor(RectTransform rt)
    {
        var canvas = rt != null ? rt.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
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

            if (cell.x >= 0 && cell.x < board.Width && cell.y >= 0 && cell.y < board.Height)
            {
                Vector3 worldCenter = board.GetCellWorldCenterPosition(cell.x, cell.y);
                return WorldToAnchoredInParent(parent, worldCenter, new Vector2(0f, 1f));
            }
        }

        // Fallback: eski manuel hesap.
        float size = board.TileSize;
        return new Vector2(
            cell.x * size + size * 0.5f,
            -cell.y * size - size * 0.5f);
    }

    private RectTransform GetBoosterFxParent()
    {
        RectTransform parent = board.BoosterFxParent;
        RectTransform contentRoot = board.ContentRoot;

        if (parent != null && parent != board.TilesRoot && contentRoot != null && parent.parent == contentRoot)
            parent.SetAsLastSibling();

        return parent;
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

        PlayJokerSfx("DrillSound");

        Image boosterImg = booster.GetComponent<Image>();
        if (boosterImg == null)
            boosterImg = booster.GetComponentInChildren<Image>();

        if (boosterImg != null && board.RowBoosterWithDrillSprite != null)
        {
            boosterImg.sprite = board.RowBoosterWithDrillSprite;
        }

        RectTransform parent = GetBoosterFxParent();
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

        // Drill yola çıkınca drill'siz sprite'a geç
        if (boosterImg != null && board.RowBoosterWithoutDrillSprite != null)
        {
            boosterImg.sprite = board.RowBoosterWithoutDrillSprite;
        }

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
        PlayJokerSfx("shuffle1");
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

        // ÖNCE mevcut taşların PERMÜTASYONUNU dene (aynı taşlar → yeni yerler). Böylece
        // BuildShuffleSourceMap eşleşir → taşlar GÖRÜNÜR şekilde hareket eder (kullanıcı değişimi
        // izleyebilir). SimulateInitialTypes yeni tipler ürettiği için mapping tutmaz → anında/
        // animasyonsuz else dalına düşülüyordu (auto-shuffle'da görülmezdi).
        TileType[,] finalTypes;
        bool ok;

        if (TryBuildPermutationShuffleTypes(currentTypes, lockedMask, out var permuted))
        {
            finalTypes = permuted;
            ok = true;
            Debug.Log("[Shuffle] Permutation shuffle (animasyonlu, görünür).");
        }
        else
        {
            // Yedek: no-match + oynanabilir permütasyon bulunamadı → eski yol (yeni tipler, anında).
            var simResult = boardInitService.SimulateInitialTypes(
                board.Width, board.Height, lockedMask, board.RandomPool);

            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                    if (lockedMask[x, y])
                        simResult[x, y] = currentTypes[x, y];

            finalTypes = simResult;
            ok = finalTypes != null;
            Debug.Log($"[Shuffle] Permutation başarısız → SimulateInitialTypes fallback ok={ok}");
        }

        if (ok)
        {
            var sourceForDest = new Vector2Int[board.Width, board.Height];

            bool hasMapping = BuildShuffleSourceMap(currentTypes, finalTypes, lockedMask, sourceForDest);
            Debug.Log($"[Shuffle] BuildShuffleSourceMap hasMapping={hasMapping}");

            if (hasMapping)
            {
                // Shuffle'dan ÖNCE ekran biraz kalsın — kullanıcı "hamle yok, board değişecek"i
                // fark etsin (yoksa ani değişimi anlamıyor).
                yield return new WaitForSeconds(0.6f);

                yield return AnimateShufflePreview(sourceForDest, lockedMask);
                CommitShuffleFromSourceMap(sourceForDest, lockedMask);

                // Yeni board'a da kısa bir hold — yerleşimi görsün.
                yield return new WaitForSeconds(0.25f);
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

    // Mevcut taşların (unlocked) tiplerini karıştırıp yeni yerlere koyar — PERMÜTASYON.
    // no-immediate-match + en az bir oynanabilir swap garantisi (deadlock çözülsün) arar.
    // Bulamazsa false → çağıran SimulateInitialTypes'a düşer.
    private bool TryBuildPermutationShuffleTypes(
        TileType[,] currentTypes, bool[,] lockedMask, out TileType[,] result)
    {
        int w = board.Width, h = board.Height;
        result = null;

        var cells = new List<Vector2Int>();
        var types = new List<TileType>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (!lockedMask[x, y])
                {
                    cells.Add(new Vector2Int(x, y));
                    types.Add(currentTypes[x, y]);
                }

        if (cells.Count < 2)
            return false;

        var candidate = (TileType[,])currentTypes.Clone();   // locked'lar korunur

        const int MaxAttempts = 40;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            for (int i = types.Count - 1; i > 0; i--)   // Fisher-Yates
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (types[i], types[j]) = (types[j], types[i]);
            }

            for (int i = 0; i < cells.Count; i++)
                candidate[cells[i].x, cells[i].y] = types[i];

            if (!HasImmediateMatchInTypes(candidate, lockedMask)
                && HasPlayableSwapInTypes(candidate, lockedMask))
            {
                result = candidate;
                return true;
            }
        }

        return false;
    }

    // (x,y) kendi tipiyle yatay/dikey 3+ dizi tamamlıyor mu? locked hücre diziyi kırar.
    private bool CompletesRunAt(TileType[,] t, bool[,] locked, int x, int y)
    {
        int w = board.Width, h = board.Height;
        TileType c = t[x, y];

        int run = 1;
        for (int i = x - 1; i >= 0 && !locked[i, y] && t[i, y] == c; i--) run++;
        for (int i = x + 1; i < w && !locked[i, y] && t[i, y] == c; i++) run++;
        if (run >= 3) return true;

        run = 1;
        for (int j = y - 1; j >= 0 && !locked[x, j] && t[x, j] == c; j--) run++;
        for (int j = y + 1; j < h && !locked[x, j] && t[x, j] == c; j++) run++;
        return run >= 3;
    }

    private bool HasImmediateMatchInTypes(TileType[,] t, bool[,] locked)
    {
        int w = board.Width, h = board.Height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (!locked[x, y] && CompletesRunAt(t, locked, x, y))
                    return true;
        return false;
    }

    private bool HasPlayableSwapInTypes(TileType[,] t, bool[,] locked)
    {
        int w = board.Width, h = board.Height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (locked[x, y]) continue;
                if (x + 1 < w && !locked[x + 1, y] && SwapCreatesMatch(t, locked, x, y, x + 1, y)) return true;
                if (y + 1 < h && !locked[x, y + 1] && SwapCreatesMatch(t, locked, x, y, x, y + 1)) return true;
            }
        return false;
    }

    private bool SwapCreatesMatch(TileType[,] t, bool[,] locked, int ax, int ay, int bx, int by)
    {
        (t[ax, ay], t[bx, by]) = (t[bx, by], t[ax, ay]);
        bool match = CompletesRunAt(t, locked, ax, ay) || CompletesRunAt(t, locked, bx, by);
        (t[ax, ay], t[bx, by]) = (t[bx, by], t[ax, ay]);
        return match;
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

        // Shuffle taşları yavaş taşınsın ki kullanıcı neyin nereye gittiğini görebilsin
        // (eski: SwapDuration*0.85 ≈ 0.17s, çok hızlıydı).
        float duration = Mathf.Max(0.35f, board.SwapDurationWithMultiplier * 2.6f);
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
            if (!board.Holes[x, y] && board.Tiles[x, y] != null && !IsUnbreakableCargo(x, y))
                matches.Add(board.Tiles[x, y]);
    }

    public void AddColumn(HashSet<TileView> matches, int x)
    {
        if (x < 0 || x >= board.Width)
            return;

        for (int y = 0; y < board.Height; y++)
            if (!board.Holes[x, y] && board.Tiles[x, y] != null && !IsUnbreakableCargo(x, y))
                matches.Add(board.Tiles[x, y]);
    }

    // Cargo (exitAtBottom) KIRILMAZ — booster da onu temizleyemez; etrafından geçer, cargo düşer.
    private bool IsUnbreakableCargo(int x, int y)
        => board.ObstacleStateService != null && board.ObstacleStateService.IsExitAtBottomAt(x, y);

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
