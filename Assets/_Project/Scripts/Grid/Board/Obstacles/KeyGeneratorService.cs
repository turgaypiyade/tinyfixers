using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime companion for ObstacleId.KeyGenerator.
/// Hits produce matchable Key tiles on the board. The HUD goal advances later,
/// when those Key tiles are actually cleared by normal match or board effects.
/// </summary>
public sealed class KeyGeneratorService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private RectTransform overlayRoot;

    [Header("Flight")]
    [SerializeField] private Vector2 keyFlySize = new Vector2(72f, 72f);
    [SerializeField, Min(0.05f)] private float flyDuration = 0.42f;
    [SerializeField, Min(0f)] private float riseDuration = 0.12f;
    [SerializeField, Min(0f)] private float hoverHold = 0.04f;
    [SerializeField] private float riseHeight = 70f;
    [SerializeField] private float arcHeight = 95f;
    [SerializeField, Min(0f)] private float hitStagger = 0.035f;

    [Header("Feedback")]
    [SerializeField] private float pulseScale = 1.08f;
    [SerializeField] private float pulseDuration = 0.12f;
    [SerializeField, Min(0.05f)] private float closeDoorDuration = 0.34f;
    [SerializeField] private Vector2 doorSizeScale = new Vector2(0.64f, 0.70f);
    [SerializeField] private Vector2 doorClosedOffset = new Vector2(0f, -0.03f);
    [SerializeField] private float doorStartOffsetY = 0.55f;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    // Katmanlı makine görseli ayarları. Servis runtime'da GridSpawner GO'suna eklendiği için
    // editörde inspector'ı yok → config GridSpawner'dan ApplyMachineConfig ile gelir.
    private KeyGeneratorMachineConfig machineConfig = new KeyGeneratorMachineConfig();
    private readonly Dictionary<int, KeyGeneratorMachineView> machineViewsByOrigin = new();

    /// GridSpawner, kendi serialize'lı config'ini (crank/draft sprite + tunable) buraya pushlar.
    public void ApplyMachineConfig(KeyGeneratorMachineConfig config)
    {
        if (config != null)
            machineConfig = config;
    }

    // committedKeys = keys already placed on the board + keys currently in flight. This is
    // the production cap counter: the generator commits exactly `capacity` keys total.
    private int committedKeys;
    // landedKeys = keys that actually reached the board. The goal advances on landing, and
    // the generator closes its lid once landedKeys reaches capacity.
    private int landedKeys;
    private int inFlightKeys;
    private bool completed;
    private bool goalRecomputeQueued;
    private readonly Dictionary<int, int> hitVisualCounters = new();
    private readonly Dictionary<int, Image> obstacleImageCache = new();
    private readonly Stack<GameObject> ghostPool = new();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    private void OnDisable()
    {
        if (board != null)
        {
            board.OnObstacleViewRestored -= HandleObstacleRevealed;
            board.OnTilesCleared -= HandleTilesClearedForGoal;
        }

        if (board?.ObstacleStateService != null)
            board.ObstacleStateService.KeyGeneratorHitInterceptor = null;

        machineViewsByOrigin.Clear();
    }

    private void ResolveReferences()
    {
        if (board == null)
            board = GetComponent<BoardController>()
                    ?? GetComponentInParent<BoardController>(true)
                    ?? FindFirstObjectByType<BoardController>();

        if (overlayRoot == null && board != null)
            overlayRoot = board.BreakFxParent != null ? board.BreakFxParent : board.ContentRoot;
    }

    private IEnumerator BindWhenReady()
    {
        while (board == null || board.ObstacleStateService == null)
        {
            ResolveReferences();
            yield return null;
        }

        committedKeys = 0;
        landedKeys = 0;
        inFlightKeys = 0;
        completed = ResolveKeyGoalAmount() <= 0;
        board.KeyGeneratorProductionComplete = completed;

        board.ObstacleStateService.KeyGeneratorHitInterceptor = completed ? null : HandleKeyGeneratorHit;

        // A KeyGenerator hidden under a cover (Grass, Safe...) is not in level.obstacles[]
        // while covered, so CloseAllGenerators misses it. When it is later revealed we must
        // sync it to the group state — a generator revealed after production finished must
        // appear CLOSED like its peers, not in the open state.
        board.OnObstacleViewRestored -= HandleObstacleRevealed;
        board.OnObstacleViewRestored += HandleObstacleRevealed;

        // Goal, ground-truth durumdan türetilir (bkz. RecomputeGoalRemaining). Key temizlenince
        // yeniden hesapla — board'da key varken hedef asla tamamlanamaz.
        board.OnTilesCleared -= HandleTilesClearedForGoal;
        board.OnTilesCleared += HandleTilesClearedForGoal;
        RecomputeGoalRemaining();

        if (completed)
            CloseAllGenerators();
        else
            StartCoroutine(CoBuildIdleMachines());

        if (logDebug)
            Debug.Log($"[KeyGenerator] Bound. capacity={ResolveKeyGoalAmount()} completed={completed}");
    }

    private bool HandleKeyGeneratorHit(int originIndex)
    {
        if (completed || board == null)
            return false;

        int capacity = ResolveKeyGoalAmount();
        if (capacity <= 0)
            return false;

        // Produce exactly `capacity` keys over the level (placed + in-flight). Once committed,
        // no further hits produce keys — the generator has emitted its whole quota.
        if (committedKeys >= capacity)
            return false;

        if (!board.TryFindKeyLandingCell(out var targetCell))
            return false;

        committedKeys++;
        inFlightKeys++;

        hitVisualCounters.TryGetValue(originIndex, out int hitIndex);
        hitVisualCounters[originIndex] = hitIndex + 1;

        float delay = hitIndex * Mathf.Max(0f, hitStagger);

        // Key uçuşu async (KeyFlight): board akmaya devam eder. Landing cell reservedKeyLandingCells
        // ile rezerve + uçuş öncesi re-validate edildiği için hareketli board'da güvenli.
        var keyJob = board.BeginJob(BoardController.BoardJobKind.KeyFlight);
        StartCoroutine(CoProduceKey(originIndex, targetCell, delay, () =>
        {
            keyJob.Dispose();
            board.RequestResolveAfterActionSequence();
        }));

        if (logDebug)
        {
            Debug.Log(
                $"[KeyGenerator] hit origin={originIndex} committed={committedKeys}/{capacity} " +
                $"landed={landedKeys} inFlight={inFlightKeys} " +
                $"target=({targetCell.x},{targetCell.y})");
        }

        return true;
    }

    private IEnumerator CoProduceKey(int originIndex, Vector2Int targetCell, float delay, System.Action onDone)
    {
        bool placed = false;

        try
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            ResolveReferences();

            // Re-validate the landing cell right before the flight. During the stagger
            // delay or board settling (gravity/cascade) the originally-picked cell may
            // have been filled by a Key or special. Re-pick a fresh normal cell so the
            // key never flies onto a Key while empty normal cells still exist.
            if (board != null && !board.CanReplaceGeneratedKeyCell(targetCell))
            {
                board.ReleaseKeyLandingReservation(targetCell);
                if (!board.TryFindKeyLandingCell(out targetCell))
                    yield break; // no valid normal cell right now → skip; finally cleans up
            }

            // Katmanlı makine varsa: kol çekilir + yeşil tarama cam alanda yukarı süpürür + draft
            // söner (materialize). Yoksa eski basit pulse feedback'i.
            var machine = EnsureMachineView(originIndex);
            if (machine != null)
            {
                yield return machine.CoPreLaunch();
            }
            else
            {
                Image obstacleImage = FindObstacleImage(originIndex);
                if (obstacleImage != null)
                    StartCoroutine(CoPulseObstacle(obstacleImage.rectTransform));
            }

            RectTransform root = overlayRoot != null ? overlayRoot : board?.ContentRoot;
            Sprite keySprite = board != null ? board.GetIcon(TileType.Key) : null;

            // Gerçek elmas podyumda "oluşur" ve fırlar: CoFlyKey'in rise fazı = materialize.
            if (root != null && keySprite != null && board != null)
                yield return CoFlyKey(root, originIndex, targetCell, keySprite);

            // Kol geri döner + draft geri gelir + idle.
            if (machine != null)
                machine.PlayPostLaunch();

            if (board != null && board.TryPlaceGeneratedKeyAt(targetCell, out var placedCell))
            {
                placed = true;
                var tile = board.GetTileViewAt(placedCell.x, placedCell.y);
                if (tile != null)
                    StartCoroutine(CoPopLandingTile(tile.RectTransform));
            }
        }
        finally
        {
            inFlightKeys = Mathf.Max(0, inFlightKeys - 1);
            if (!placed)
            {
                // Production failed (no landing cell) → free the commitment so another
                // hit can retry. TryPlaceGeneratedKeyAt releases the reservation itself;
                // only release here for paths that bailed before ever calling it.
                committedKeys = Mathf.Max(0, committedKeys - 1);
                board?.ReleaseKeyLandingReservation(targetCell);
            }
            else
            {
                // A key reached the board. Goal is derived from ground-truth state, so refresh
                // it. Production tracking: once the whole quota has landed, close the lids and
                // stop accepting hits — but the goal completes only when all keys are CLEARED.
                landedKeys++;
                RecomputeGoalRemaining();
                if (landedKeys >= ResolveKeyGoalAmount())
                    CompleteGenerators();
            }

            onDone?.Invoke();
        }
    }

    private IEnumerator CoFlyKey(RectTransform root, int originIndex, Vector2Int targetCell, Sprite keySprite)
    {
        Vector2 start = GetOriginCenterIn(root, originIndex);
        Vector2 end = board.WorldToAnchoredIn(root, board.GetCellWorldCenterPosition(targetCell.x, targetCell.y));

        GameObject go = RentGhost(root);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = keyFlySize;
        rt.localScale = Vector3.one * 0.35f;
        rt.localRotation = Quaternion.Euler(0f, 0f, -12f);
        rt.anchoredPosition = start;
        rt.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = keySprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = Color.white;

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 0f;

        Vector2 flightStart = start + Vector2.up * riseHeight;
        float riseTime = 0f;
        float rise = Mathf.Max(0.0001f, riseDuration);
        while (riseTime < rise)
        {
            riseTime += Time.deltaTime;
            float k = Mathf.Clamp01(riseTime / rise);
            float e = 1f - (1f - k) * (1f - k);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, flightStart, e);
            rt.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.08f, e);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-18f, 8f, e));
            cg.alpha = e;
            yield return null;
        }

        if (hoverHold > 0f)
            yield return new WaitForSeconds(hoverHold);

        Vector2 mid = (flightStart + end) * 0.5f;
        float dir = end.x >= flightStart.x ? 1f : -1f;
        Vector2 control = mid + new Vector2(70f * dir, arcHeight);

        float duration = Mathf.Max(0.05f, flyDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = EaseInOut(k);
            rt.anchoredPosition = Bezier2(flightStart, control, end, e);
            rt.localScale = Vector3.one * Mathf.Lerp(1.08f, 0.82f, k);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(8f, 0f, e));
            cg.alpha = k < 0.9f ? 1f : 1f - Mathf.InverseLerp(0.9f, 1f, k);
            yield return null;
        }

        ReturnGhost(go);
    }

    private void CompleteGenerators()
    {
        if (completed)
            return;

        completed = true;

        if (board != null)
            board.KeyGeneratorProductionComplete = true;

        if (board?.ObstacleStateService != null)
            board.ObstacleStateService.KeyGeneratorHitInterceptor = null;

        CloseAllGenerators();
    }

    // Key temizlenince hedefi yeniden hesapla (board key sayısı değişti).
    private void HandleTilesClearedForGoal(TileType tileType, int amount)
    {
        if (tileType != TileType.Key)
            return;

        // Bazı clear yollarında OnTilesCleared, görsel sayım doğruyken grid/tile array'i aynı frame
        // içinde hâlâ son haline oturmadan gelebiliyor. Goal ground-truth'u boarddaki key sayısından
        // türediği için aynı-frame recompute 1 fazla okuyabilir. Önce hemen dene, sonra frame sonu ve
        // bir sonraki frame tekrar oku; son key temizlenince hedef 1'de asılı kalmasın.
        RecomputeGoalRemaining();
        QueueDeferredGoalRecompute();
    }

    private void QueueDeferredGoalRecompute()
    {
        if (goalRecomputeQueued || !isActiveAndEnabled)
            return;

        goalRecomputeQueued = true;
        StartCoroutine(CoDeferredGoalRecompute());
    }

    private IEnumerator CoDeferredGoalRecompute()
    {
        yield return null;
        RecomputeGoalRemaining();
        yield return null;
        RecomputeGoalRemaining();
        goalRecomputeQueued = false;
    }

    // Hedefi GROUND-TRUTH durumdan türetir — event sayımına güvenmez, çift/eksik sayım imkânsız:
    //   kalan = üretilecek toplam(capacity) − landedKeys + boarddaki gerçek key sayısı
    //         = capacity − (üretilen − boarddaki) = capacity − toplanmış.
    // Board'da key varken kalan asla 0 olamaz; tüm keyler üretilip temizlenince tam 0 olur.
    private void RecomputeGoalRemaining()
    {
        if (board == null || board.TopHud == null)
            return;

        int capacity = ResolveKeyGoalAmount();
        if (capacity <= 0)
            return;

        int onBoard = board.CountTilesOfType(TileType.Key);
        int remaining = Mathf.Clamp(capacity - landedKeys + onBoard, 0, capacity);
        board.TopHud.SetKeyGeneratorGoalRemaining(remaining);
    }

    // A beneath KeyGenerator (e.g. under Grass/Safe) gets its view created when the cover
    // is cleared. If production already finished, it must join the group's CLOSED state
    // instead of appearing open. If not finished yet, it simply keeps producing via the
    // shared interceptor — no action needed here.
    private void HandleObstacleRevealed(int x, int y)
    {
        if (!completed)
            return;

        var svc = board != null ? board.ObstacleStateService : null;
        if (svc == null || svc.GetObstacleIdAt(x, y) != ObstacleId.KeyGenerator)
            return;

        int origin = svc.GetObstacleOriginAt(x, y);
        if (origin < 0)
            return;

        // Drop any stale cached Image; the revealed generator has a brand-new view.
        obstacleImageCache.Remove(origin);
        StartCoroutine(CoCloseRevealedGenerator(origin));
    }

    private IEnumerator CoCloseRevealedGenerator(int origin)
    {
        // The restored view may be created a frame after the reveal event; wait for it.
        for (int i = 0; i < 5 && FindObstacleImage(origin) == null; i++)
            yield return null;

        SetClosedFrame(origin);
    }

    private void CloseAllGenerators()
    {
        LevelData level = board != null ? board.ActiveLevelData : null;
        if (level?.obstacles == null || level.obstacleOrigins == null)
            return;

        int size = Mathf.Min(level.obstacles.Length, level.obstacleOrigins.Length);
        for (int i = 0; i < size; i++)
        {
            if ((ObstacleId)level.obstacles[i] != ObstacleId.KeyGenerator)
                continue;
            if (level.obstacleOrigins[i] != i)
                continue;

            StartCoroutine(CoCloseGeneratorDoor(i));
        }
    }

    private IEnumerator CoCloseGeneratorDoor(int originIndex)
    {
        Image image = FindObstacleImage(originIndex);
        if (image == null)
            yield break;

        // Katmanlı makine: kapanış = gövdeyi lights-off sprite'ına (stages[1]) çevir + hologram/kol
        // gizle. Eski kapı-kaydırma overlay'i yeni makinede uygun değil, atla.
        if (machineViewsByOrigin.TryGetValue(originIndex, out var machineView) && machineView != null)
        {
            SetClosedFrame(originIndex);
            machineView.SetClosed();
            yield break;
        }

        Sprite closed = ResolveClosedSprite();
        if (closed == null)
        {
            SetClosedFrame(originIndex);
            yield break;
        }

        Sprite door = ResolveDoorSprite();

        RectTransform imageRt = image.rectTransform;
        Vector3 baseScale = imageRt.localScale;
        Transform parent = imageRt.parent;
        if (parent == null)
        {
            SetClosedFrame(originIndex);
            yield break;
        }

        var maskGo = new GameObject("KeyGeneratorDoorCloseMask", typeof(RectTransform), typeof(RectMask2D));
        var maskRt = (RectTransform)maskGo.transform;
        maskRt.SetParent(parent, false);
        maskRt.SetSiblingIndex(imageRt.GetSiblingIndex() + 1);
        CopyRect(maskRt, imageRt);
        if (door == null)
            SetPivotKeepingRect(maskRt, new Vector2(0.5f, 1f));

        var overlayGo = new GameObject("KeyGeneratorClosedOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        var overlayRt = (RectTransform)overlayGo.transform;
        overlayRt.SetParent(maskRt, false);
        overlayRt.anchorMin = overlayRt.anchorMax = door != null ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
        overlayRt.pivot = door != null ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
        overlayRt.sizeDelta = door != null ? GetDoorSize(imageRt.rect.size) : imageRt.rect.size;
        Vector2 closedPos = door != null ? GetDoorClosedPosition(imageRt.rect.size) : Vector2.zero;
        Vector2 startPos = closedPos + Vector2.up * imageRt.rect.height * Mathf.Max(0f, doorStartOffsetY);
        overlayRt.anchoredPosition = door != null ? startPos : Vector2.zero;
        overlayRt.localScale = Vector3.one;

        var overlay = overlayGo.GetComponent<Image>();
        overlay.sprite = door != null ? door : closed;
        overlay.color = Color.white;
        overlay.preserveAspect = image.preserveAspect;
        overlay.raycastTarget = false;

        var cg = overlayGo.GetComponent<CanvasGroup>();
        cg.alpha = 1f;

        float fullHeight = Mathf.Max(1f, imageRt.rect.height);
        float duration = Mathf.Max(0.05f, closeDoorDuration);
        float t = 0f;
        while (t < duration)
        {
            if (image == null || imageRt == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            if (door != null)
            {
                overlayRt.anchoredPosition = Vector2.LerpUnclamped(startPos, closedPos, e);
            }
            else
            {
                maskRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Lerp(1f, fullHeight, e));
                overlayRt.anchoredPosition = Vector2.zero;
            }

            float bump = Mathf.Sin(k * Mathf.PI) * 0.025f;
            imageRt.localScale = baseScale * (1f + bump);
            yield return null;
        }

        if (imageRt != null)
            imageRt.localScale = baseScale;

        if (maskGo != null)
            Destroy(maskGo);

        if (image != null)
            SetClosedFrame(originIndex);
    }

    private void SetClosedFrame(int originIndex)
    {
        Image image = FindObstacleImage(originIndex);
        if (image == null)
            return;

        Sprite closed = ResolveClosedSprite();
        if (closed != null)
            image.sprite = closed;

        // No alpha dimming: the door/lid overlay already conveys the closed state,
        // so the base image stays fully opaque.
        image.raycastTarget = false;
    }

    private static void CopyRect(RectTransform target, RectTransform source)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }

    private static void SetPivotKeepingRect(RectTransform rect, Vector2 pivot)
    {
        Vector2 delta = pivot - rect.pivot;
        Vector2 size = rect.rect.size;
        rect.pivot = pivot;
        rect.anchoredPosition += new Vector2(delta.x * size.x, delta.y * size.y);
    }

    private Sprite ResolveClosedSprite()
    {
        var lib = board?.ActiveLevelData != null ? board.ActiveLevelData.obstacleLibrary : null;
        var def = lib != null ? lib.Get(ObstacleId.KeyGenerator) : null;
        if (def?.stages != null && def.stages.Count > 1 && def.stages[1] != null && def.stages[1].sprite != null)
            return def.stages[1].sprite;

        return def?.GetPreviewSprite();
    }

    private Sprite ResolveDoorSprite()
    {
        var lib = board?.ActiveLevelData != null ? board.ActiveLevelData.obstacleLibrary : null;
        var def = lib != null ? lib.Get(ObstacleId.KeyGenerator) : null;
        if (def?.auxiliarySprites != null && def.auxiliarySprites.Count > 0)
            return def.auxiliarySprites[0];

        return null;
    }

    private Vector2 GetDoorSize(Vector2 obstacleSize)
    {
        return new Vector2(
            obstacleSize.x * Mathf.Max(0.01f, doorSizeScale.x),
            obstacleSize.y * Mathf.Max(0.01f, doorSizeScale.y));
    }

    private Vector2 GetDoorClosedPosition(Vector2 obstacleSize)
    {
        return new Vector2(
            obstacleSize.x * doorClosedOffset.x,
            obstacleSize.y * doorClosedOffset.y);
    }

    private int ResolveKeyGoalAmount()
    {
        LevelData level = board != null ? board.ActiveLevelData : null;
        if (level?.goals == null)
            return 0;

        for (int i = 0; i < level.goals.Length; i++)
        {
            var goal = level.goals[i];
            if (goal == null)
                continue;

            if (goal.targetType == LevelGoalTargetType.Tile && goal.tileType == TileType.Key)
                return Mathf.Max(1, goal.amount);

            if (goal.targetType == LevelGoalTargetType.Obstacle && goal.obstacleId == ObstacleId.KeyGenerator)
                return Mathf.Max(1, goal.amount);
        }

        return 0;
    }

    private IEnumerator CoPulseObstacle(RectTransform target)
    {
        if (target == null)
            yield break;

        Vector3 baseScale = target.localScale;
        Vector3 peak = baseScale * Mathf.Max(1f, pulseScale);
        float half = Mathf.Max(0.01f, pulseDuration * 0.5f);

        float t = 0f;
        while (t < half)
        {
            if (target == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            target.localScale = Vector3.LerpUnclamped(baseScale, peak, 1f - (1f - k) * (1f - k));
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            if (target == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            target.localScale = Vector3.LerpUnclamped(peak, baseScale, k * k);
            yield return null;
        }

        if (target != null)
            target.localScale = baseScale;
    }

    private static IEnumerator CoPopLandingTile(RectTransform rt)
    {
        if (rt == null)
            yield break;

        Vector3 baseScale = rt.localScale;
        Vector3 peak = baseScale * 1.14f;
        float duration = 0.14f;
        float t = 0f;

        while (t < duration)
        {
            if (rt == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float pulse = Mathf.Sin(k * Mathf.PI);
            rt.localScale = Vector3.LerpUnclamped(baseScale, peak, pulse);
            yield return null;
        }

        if (rt != null)
            rt.localScale = baseScale;
    }

    // ── Layered machine view (per-origin) ────────────────────────────────────

    // Builds (or reuses) the layered machine visual as a child of the generator's body image.
    // Returns null when sprites aren't wired yet (crank+draft both empty) → caller falls back
    // to the legacy pulse feedback, so behavior is unchanged until the art is assigned.
    private KeyGeneratorMachineView EnsureMachineView(int originIndex)
    {
        if (machineConfig == null
            || (machineConfig.crankSprite == null && machineConfig.draftSprite == null))
            return null;

        Image body = FindObstacleImage(originIndex);
        if (body == null)
            return null;

        if (machineViewsByOrigin.TryGetValue(originIndex, out var existing)
            && existing != null && existing.transform.parent == body.transform)
            return existing;

        // Stale (body rebuilt on reveal) or missing → (re)build.
        if (existing != null)
            Destroy(existing.gameObject);

        var go = new GameObject("KeyGeneratorMachine", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(body.rectTransform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = body.rectTransform.rect.size;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        go.layer = body.gameObject.layer;

        var view = go.AddComponent<KeyGeneratorMachineView>();
        view.Build(rt, machineConfig);
        machineViewsByOrigin[originIndex] = view;
        return view;
    }

    // Builds idle machines (breathing hologram) once GridSpawner has drawn the obstacle images.
    private IEnumerator CoBuildIdleMachines()
    {
        for (int i = 0; i < 8; i++)
            yield return null;

        LevelData level = board != null ? board.ActiveLevelData : null;
        if (level?.obstacles == null || level.obstacleOrigins == null)
            yield break;

        int size = Mathf.Min(level.obstacles.Length, level.obstacleOrigins.Length);
        for (int i = 0; i < size; i++)
        {
            if ((ObstacleId)level.obstacles[i] != ObstacleId.KeyGenerator) continue;
            if (level.obstacleOrigins[i] != i) continue;
            EnsureMachineView(i);
        }
    }

    private Image FindObstacleImage(int originIndex)
    {
        if (obstacleImageCache.TryGetValue(originIndex, out var cached) && cached != null)
            return cached;

        // Fast path: O(1) registry from GridSpawner. Avoids the full-hierarchy
        // GetComponentsInChildren<Image> scan (heavy on dense boards with many generators).
        Image found = board != null && board.ObstacleViewByOriginLookup != null
            ? board.ObstacleViewByOriginLookup(originIndex)
            : null;

        // Fallback: legacy scan (covers views not yet in the registry, e.g. mid-reveal).
        if (found == null)
            found = FindObstacleImageScan(originIndex);

        if (found != null)
            obstacleImageCache[originIndex] = found;

        return found;
    }

    private Image FindObstacleImageScan(int originIndex)
    {
        RectTransform root = board != null ? board.ContentRoot : null;
        if (root == null || board.Width <= 0)
            return null;

        int x = originIndex % board.Width;
        int y = originIndex / board.Width;
        string expectedName = $"Obs_{ObstacleId.KeyGenerator}_{x}_{y}";

        var images = root.GetComponentsInChildren<Image>(true);
        Image closest = null;
        float closestDistance = float.MaxValue;
        Vector2 expected = board.WorldToAnchoredIn(root, board.GetCellWorldCenterPosition(x, y));

        for (int i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null)
                continue;

            if (image.name == expectedName)
                return image;

            if (!IsLikelyKeyGeneratorImage(image))
                continue;

            Vector2 pos = root.InverseTransformPoint(image.rectTransform.TransformPoint(image.rectTransform.rect.center));
            float distance = Vector2.SqrMagnitude(pos - expected);
            if (distance < closestDistance)
            {
                closest = image;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private static bool IsLikelyKeyGeneratorImage(Image image)
    {
        Transform t = image != null ? image.transform : null;
        while (t != null)
        {
            string n = t.name;
            if (n.Contains("KeyGenerator") || n.Contains("Obstacles") || n.Contains("OverTiles") || n.Contains("UnderTiles"))
                return true;
            t = t.parent;
        }

        return false;
    }

    private Vector2 GetOriginCenterIn(RectTransform root, int originIndex)
    {
        Image obstacle = FindObstacleImage(originIndex);
        if (obstacle != null)
            return root.InverseTransformPoint(obstacle.rectTransform.TransformPoint(obstacle.rectTransform.rect.center));

        int x = originIndex % board.Width;
        int y = originIndex / board.Width;
        return board.WorldToAnchoredIn(root, board.GetCellWorldCenterPosition(x, y));
    }

    private GameObject RentGhost(RectTransform root)
    {
        GameObject go = null;
        while (ghostPool.Count > 0 && go == null)
            go = ghostPool.Pop();

        if (go == null)
            go = new GameObject("KeyFlyGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));

        go.transform.SetParent(root, false);
        go.SetActive(true);
        return go;
    }

    private void ReturnGhost(GameObject go)
    {
        if (go == null)
            return;

        go.SetActive(false);
        ghostPool.Push(go);
    }

    private static Vector2 Bezier2(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
