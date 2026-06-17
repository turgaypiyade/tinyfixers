using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PatchbotDashUI : MonoBehaviour
{
    private sealed class DashMotionState
    {
        public float elapsed;
        public float afterTimer;
    }

    // Dalış boyunca canlı hedef takibi. resolve her çağrıda hedefi mantıksal board
    // durumundan yeniden doğrular (ölmüşse koordinatör yeni hücre seçer); goal son
    // çözülen hedef, target ise drone'un o anda yöneldiği (yumuşatılmış) nokta.
    private sealed class LiveTargetState
    {
        public Vector2 target;
        public Vector2? goal;
        public System.Func<Vector2?> resolve;
        public float resolveTimer;
    }

    [Header("Refs")]
    [SerializeField] private RectTransform boardContent;   // Tile'ların bulunduğu root (sadece test/path bulma için)
    [SerializeField] private RectTransform vfxRoot;        // VFXRoot (runner + afterimage burada)
    [SerializeField] private Image runnerImage;            // PatchbotRunner Image (template only)
    [SerializeField] private TileIconLibrary tileIcons;    // TileIconLibrary asset

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 100f;       // UI units per second (anchored space)
    [SerializeField] private float arriveEps = 2f;
    [SerializeField, Range(0.5f, 1.5f)] private float syncedDurationMultiplier = 1f;

    [Header("Drone Flight Feel")]
    [SerializeField, Min(0f)] private float takeoffBurstDuration = 0.12f;
    [SerializeField, Range(0f, 1.5f)] private float takeoffLateralFactor = 0.46f;
    [SerializeField, Range(0f, 2f)] private float takeoffLiftFactor = 0.78f;
    [SerializeField, Min(0f)] private float hoverHoldDuration = 0.035f;
    [SerializeField, Range(0f, 1f)] private float diveArcFactor = 0.11f;
    [Tooltip("Pike (dalış) hızı = dashSpeed * bu çarpan. Büyüdükçe hedefe daha hızlı/keskin pike. " +
             "Takeoff (2.5x büyüme) ve hover sabit kalır; yalnızca dalış hızlanır.")]
    [SerializeField, Range(0.5f, 4f)] private float pikeSpeedMultiplier = 2.4f;
    [Tooltip("Dalışın ivmelenme keskinliği. 1 = lineer, 2-2.5 = hedefe doğru hızlanan pike hissi.")]
    [SerializeField, Range(1f, 3f)] private float diveEaseInPower = 2.2f;

    [Header("Live Retargeting")]
    [SerializeField, Min(0.01f)] private float liveRetargetInterval = 0.05f;   // hedef doğrulama sıklığı (sn)
    [SerializeField, Min(0.5f)] private float retargetSteerSpeed = 9f;         // yeni hedefe bank/kavis hızı
    [SerializeField, Min(0f)] private float maxRetargetHomingDuration = 0.6f;  // dalış bitince hedefe kilitli ek uçuş sınırı

    [Header("Flight Audio")]
    [SerializeField] private AudioClip flightLoopClip;
    [SerializeField, Min(0f)] private float flightLoopVolume = 0.18f;
    [SerializeField, Min(0f)] private float flightFadeIn = 0.015f;
    [SerializeField, Min(0f)] private float flightFadeOut = 0.04f;
    [SerializeField, Range(0.5f, 2f)] private float flightPitch = 1.12f;
    [SerializeField, Range(0f, 0.25f)] private float flightPitchJitter = 0.05f;
    [SerializeField, Range(0f, 0.15f)] private float flightVolumeJitter = 0.03f;

    [Header("AfterImage")]
    [SerializeField] private float spawnEvery = 0.02f;
    [SerializeField] private float afterLife = 0.28f;
    [SerializeField] private Color afterColor = new Color(0.55f, 0.85f, 1f, 0.85f);
    [Header("Carry Orbit")]
    [SerializeField] private float carrySizeFactor = 0.72f;
    [SerializeField, Range(0.05f, 0.6f)] private float carryOrbitRadiusFactor = 0.32f;
    private Coroutine co;

    void Reset()
    {
        runnerImage = GetComponent<Image>();
    }

    public void PlayDash(List<RectTransform> pathTiles)
    {
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(DashRoutine(pathTiles));
    }

    public Coroutine PlayDashParallel(List<BoardController.PatchbotDashRequest> requests, BoardController board, float syncDuration = -1f)
    {
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        var requestCopy = requests != null
            ? new List<BoardController.PatchbotDashRequest>(requests)
            : null;

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(DashParallelRoutine(requestCopy, board, syncDuration));
        return co;
    }

    private IEnumerator DashParallelRoutine(List<BoardController.PatchbotDashRequest> requests, BoardController board, float syncDuration)
    {
        if (vfxRoot == null || board == null) yield break;
        if (requests == null || requests.Count == 0) yield break;

        if (runnerImage != null) runnerImage.enabled = false;

        Sprite patchbotSprite = null;
        if (runnerImage != null && runnerImage.sprite != null)
            patchbotSprite = runnerImage.sprite;
        else if (tileIcons != null)
            patchbotSprite = tileIcons.GetPatchBotFlightIcon();

        const float stagger = 0.02f;
        int remaining = 0;

        for (int i = 0; i < requests.Count; i++)
        {
            var req = requests[i];
            remaining++;

            StartCoroutine(SingleDashRoutine(req, board, patchbotSprite, syncDuration, () => remaining--));

            if (stagger > 0f)
                yield return new WaitForSeconds(stagger);
        }

        while (remaining > 0)
            yield return null;

        co = null;
    }

    private IEnumerator SingleDashRoutine(
       BoardController.PatchbotDashRequest req,
       BoardController board,
       Sprite sprite,
       float syncDuration,
       System.Action onComplete)
    {
        req.onStart?.Invoke();

        var go = new GameObject("PatchbotRunnerInstance", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(vfxRoot, false);

        var img = go.GetComponent<Image>();
        var rt = (RectTransform)go.transform;

        img.sprite = sprite;
        img.raycastTarget = false;
        img.enabled = true;
        img.color = Color.white;

        rt.SetAsLastSibling();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 size = new Vector2(90f, 90f);
        if (runnerImage != null &&
            runnerImage.rectTransform != null &&
            runnerImage.rectTransform.sizeDelta.sqrMagnitude > 1f)
        {
            size = runnerImage.rectTransform.sizeDelta;
        }
        rt.sizeDelta = size;

        var propellerSprite = board.PatchBotPropellerSprite;
        if (propellerSprite != null)
        {
            var propGo = new GameObject("PatchBotPropeller", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PatchBotPropellerView));
            propGo.transform.SetParent(rt, false);

            var propRt = propGo.GetComponent<RectTransform>();
            propRt.anchorMin = new Vector2(0.5f, 0.5f);
            propRt.anchorMax = new Vector2(0.5f, 0.5f);
            propRt.pivot = new Vector2(0.5f, 0.5f);
            propRt.sizeDelta = size;
            propRt.anchoredPosition = Vector2.zero;

            var propImg = propGo.GetComponent<Image>();
            propImg.sprite = propellerSprite;
            propImg.preserveAspect = true;
            propImg.raycastTarget = false;
            propImg.color = Color.white;

            propGo.GetComponent<PatchBotPropellerView>().StartActivationSpin(5400f);
        }

        RectTransform carryRt = null;
        Image carryImg = null;

        if (req.orbitCarry && req.carriedSprite != null)
        {
            var carryGo = new GameObject("PatchbotCarrySpecial", typeof(RectTransform), typeof(Image));
            carryGo.transform.SetParent(rt, false);

            carryRt = carryGo.GetComponent<RectTransform>();
            carryImg = carryGo.GetComponent<Image>();

            carryRt.anchorMin = new Vector2(0.5f, 0.5f);
            carryRt.anchorMax = new Vector2(0.5f, 0.5f);
            carryRt.pivot = new Vector2(0.5f, 0.5f);
            carryRt.sizeDelta = size * carrySizeFactor;
            carryRt.anchoredPosition = Vector2.down * (Mathf.Min(size.x, size.y) * carryOrbitRadiusFactor);

            carryImg.sprite = req.carriedSprite;
            carryImg.preserveAspect = true;
            carryImg.raycastTarget = false;
            carryImg.color = Color.white;

            carryRt.SetAsLastSibling();
        }

        AudioSource flightSource = CreateFlightAudioSource(rt);
        StartFlightAudio(flightSource);

        Vector3 fromWorld = board.GetCellWorldPosition(req.from.x, req.from.y);
        Vector3 toWorld = board.GetCellWorldPosition(req.to.x, req.to.y);

        Vector2 start = WorldToAnchoredIn(vfxRoot, fromWorld);
        Vector2 target = WorldToAnchoredIn(vfxRoot, toWorld);
        rt.anchoredPosition = start;

        float totalDistance = Vector2.Distance(start, target);
        float effectiveSpeed = Mathf.Max(1f, dashSpeed * Mathf.Max(0.01f, pikeSpeedMultiplier));

        float takeoffDuration;
        float hoverDuration;
        float diveDuration;

        if (syncDuration > 0f)
        {
            // Zincir/sync modunda toplam süre dışarıdan veriliyor; fazlara böl.
            float travelDuration = Mathf.Max(0f, syncDuration * Mathf.Max(0.01f, syncedDurationMultiplier));
            takeoffDuration = Mathf.Min(Mathf.Max(0f, takeoffBurstDuration), Mathf.Max(0f, travelDuration * 0.35f));
            hoverDuration = Mathf.Min(Mathf.Max(0f, hoverHoldDuration), Mathf.Max(0f, travelDuration * 0.18f));
            diveDuration = Mathf.Max(0.01f, travelDuration - takeoffDuration - hoverDuration);
        }
        else
        {
            // Solo dash: takeoff (2.5x şarj) ve hover SABİT; dalış pike hızıyla kısa ve keskin.
            takeoffDuration = Mathf.Max(0f, takeoffBurstDuration);
            hoverDuration = Mathf.Max(0f, hoverHoldDuration);
            diveDuration = totalDistance > arriveEps ? Mathf.Max(0.05f, totalDistance / effectiveSpeed) : 0.01f;
        }

        int side = ((req.from.x + req.from.y + req.to.x + req.to.y) & 1) == 0 ? -1 : 1;
        Vector2 takeoff = start + new Vector2(
            side * Mathf.Min(size.x, size.y) * takeoffLateralFactor,
            Mathf.Min(size.x, size.y) * takeoffLiftFactor);

        Debug.Log($"[PatchbotDashUI] DRONE_DASH from={req.from} to={req.to} takeoff={takeoffDuration:0.000} hover={hoverDuration:0.000} dive={diveDuration:0.000}");

        var motion = new DashMotionState();
        yield return RunTakeoffBurst(rt, carryRt, size, sprite, start, takeoff, takeoffDuration, motion);
        yield return RunHoverHold(rt, carryRt, size, sprite, takeoff, hoverDuration, motion);

        var live = AcquireLiveTarget(req, board, target);

        yield return RunDive(rt, carryRt, size, sprite, takeoff, live, diveDuration, effectiveSpeed, motion);

        rt.anchoredPosition = live.target;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        if (carryRt != null)
            carryRt.localRotation = Quaternion.identity;

        req.onArrived?.Invoke();
        yield return StopFlightAudioRoutine(flightSource);

        Destroy(go);
        onComplete?.Invoke();
    }

    // Dive başında bu dash'ın canlı çözücüsünü teslim alır ve ilk hedefi çözer.
    // Çözücü yoksa (sabit hedefli eski dash'lar) davranış birebir eski hâli: statik hedef.
    private LiveTargetState AcquireLiveTarget(BoardController.PatchbotDashRequest req, BoardController board, Vector2 fallbackTarget)
    {
        var live = new LiveTargetState { target = fallbackTarget };

        if (board == null || vfxRoot == null)
            return live;

        if (!PatchbotLiveDashTargetRegistry.TryAcquireLiveResolver(req.from, req.to, out var cellResolver))
            return live;

        live.resolve = () =>
        {
            var maybeCell = cellResolver();
            if (!maybeCell.HasValue)
                return null;

            var cell = maybeCell.Value;
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                return null;

            return WorldToAnchoredIn(vfxRoot, board.GetCellWorldPosition(cell.x, cell.y));
        };

        var initial = live.resolve();
        if (initial.HasValue)
        {
            live.target = initial.Value;
            live.goal = initial.Value;
        }

        return live;
    }

    // Hedefi periyodik yeniden doğrular; hedef değiştiyse drone'un yöneldiği noktayı
    // exponential smoothing ile yeni hedefe kaydırır (havada dönüş/bank hissi).
    private void TickLiveRetarget(LiveTargetState live, float dt)
    {
        if (live.resolve == null)
            return;

        live.resolveTimer += dt;
        if (live.resolveTimer >= liveRetargetInterval)
        {
            live.resolveTimer = 0f;
            var desired = live.resolve();
            if (desired.HasValue)
                live.goal = desired.Value;
        }

        if (!live.goal.HasValue || live.goal.Value == live.target)
            return;

        float blend = 1f - Mathf.Exp(-retargetSteerSpeed * dt);
        live.target = Vector2.Lerp(live.target, live.goal.Value, blend);

        if ((live.target - live.goal.Value).sqrMagnitude <= 1f)
            live.target = live.goal.Value;
    }

    private IEnumerator RunTakeoffBurst(
        RectTransform rt,
        RectTransform carryRt,
        Vector2 size,
        Sprite sprite,
        Vector2 start,
        Vector2 takeoff,
        float duration,
        DashMotionState motion)
    {
        if (duration <= 0f)
        {
            rt.anchoredPosition = takeoff;
            yield break;
        }

        float local = 0f;
        while (local < duration)
        {
            float dt = Time.deltaTime;
            local += dt;
            motion.elapsed += dt;

            float t = Mathf.Clamp01(local / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float punch = Mathf.Sin(t * Mathf.PI) * Mathf.Min(size.x, size.y) * 0.08f;

            rt.anchoredPosition = Vector2.LerpUnclamped(start, takeoff, eased) + Vector2.up * punch;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 2.5f, eased);

            UpdateCarryOrbit(carryRt, size, motion.elapsed);
            TickAfterImage(rt, sprite, motion);
            yield return null;
        }
    }

    private IEnumerator RunHoverHold(
        RectTransform rt,
        RectTransform carryRt,
        Vector2 size,
        Sprite sprite,
        Vector2 hover,
        float duration,
        DashMotionState motion)
    {
        if (duration <= 0f)
            yield break;

        float local = 0f;
        while (local < duration)
        {
            float dt = Time.deltaTime;
            local += dt;
            motion.elapsed += dt;

            float wobbleX = Mathf.Sin(motion.elapsed * 8.5f) * Mathf.Min(size.x, size.y) * 0.035f;
            float wobbleY = Mathf.Sin(motion.elapsed * 10.0f) * Mathf.Min(size.x, size.y) * 0.025f;

            rt.anchoredPosition = hover + new Vector2(wobbleX, wobbleY);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * 2.5f;

            UpdateCarryOrbit(carryRt, size, motion.elapsed);
            TickAfterImage(rt, sprite, motion);
            yield return null;
        }
    }

    private IEnumerator RunDive(
        RectTransform rt,
        RectTransform carryRt,
        Vector2 size,
        Sprite sprite,
        Vector2 start,
        LiveTargetState live,
        float duration,
        float homingSpeed,
        DashMotionState motion)
    {
        Vector2 initialDelta = live.target - start;
        float arc = Mathf.Clamp(initialDelta.magnitude * Mathf.Max(0f, diveArcFactor), Mathf.Min(size.x, size.y) * 0.10f, Mathf.Min(size.x, size.y) * 0.45f);

        float local = 0f;
        while (local < duration)
        {
            float dt = Time.deltaTime;
            local += dt;
            motion.elapsed += dt;

            // Hedef uçuş sırasında ölmüş olabilir — her tick mantıksal board'dan
            // doğrula; değiştiyse target yumuşakça yeni hücreye kayar (kavisli dönüş).
            TickLiveRetarget(live, dt);

            float t = Mathf.Clamp01(local / duration);
            // Ease-IN: hedefe doğru ivmelenen pike (smoothstep yerine).
            float eased = Mathf.Pow(t, Mathf.Max(1f, diveEaseInPower));
            float curve = Mathf.Sin(t * Mathf.PI) * arc;
            float snap = Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Min(size.x, size.y) * 0.025f;

            Vector2 delta = live.target - start;
            Vector2 normal = delta.sqrMagnitude > 0.001f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;

            rt.anchoredPosition = Vector2.LerpUnclamped(start, live.target, eased) + normal * (curve + snap);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * Mathf.Lerp(2.5f, 1.0f, t);

            UpdateCarryOrbit(carryRt, size, motion.elapsed);
            TickAfterImage(rt, sprite, motion);
            yield return null;
        }

        // Hedef dalış sırasında değiştiyse süre dolduğunda hâlâ uzakta olabiliriz —
        // canlı hedefe kilitli düz uçuşla tamamla (süre sınırlı; hedef yine ölürse
        // takip devam eder, son bilinen hücreye konar).
        float homingElapsed = 0f;
        while ((rt.anchoredPosition - live.target).magnitude > arriveEps && homingElapsed < maxRetargetHomingDuration)
        {
            float dt = Time.deltaTime;
            homingElapsed += dt;
            motion.elapsed += dt;

            TickLiveRetarget(live, dt);

            rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, live.target, Mathf.Max(1f, homingSpeed) * dt);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            UpdateCarryOrbit(carryRt, size, motion.elapsed);
            TickAfterImage(rt, sprite, motion);
            yield return null;
        }
    }

    private void UpdateCarryOrbit(RectTransform carryRt, Vector2 size, float elapsed)
    {
        if (carryRt == null)
            return;

        float footOffset = Mathf.Min(size.x, size.y) * carryOrbitRadiusFactor;
        carryRt.anchoredPosition = Vector2.down * footOffset;
        carryRt.localRotation = Quaternion.identity;
    }

    private void TickAfterImage(RectTransform rt, Sprite sprite, DashMotionState motion)
    {
        motion.afterTimer += Time.deltaTime;
        if (motion.afterTimer < spawnEvery)
            return;

        motion.afterTimer = 0f;
        SpawnAfterImageAt(rt, sprite);
    }

    private IEnumerator DashRoutine(List<RectTransform> path)
    {
        if (runnerImage == null || tileIcons == null || boardContent == null || vfxRoot == null) yield break;
        if (path == null || path.Count == 0) yield break;

        if (transform.parent != vfxRoot)
            transform.SetParent(vfxRoot, false);

        if (runnerImage.sprite == null && tileIcons != null)
            runnerImage.sprite = tileIcons.GetPatchBotFlightIcon();
        runnerImage.raycastTarget = false;
        runnerImage.enabled = true;
        runnerImage.color = Color.white;

        Vector2 tileSize = new Vector2(90f, 90f);
        var tileImage = path[0].GetComponent<Image>();
        if (tileImage != null)
        {
            var tileRT = tileImage.rectTransform;
            if (tileRT.rect.width > 1f && tileRT.rect.height > 1f)
                tileSize = tileRT.rect.size;
        }

        runnerImage.rectTransform.sizeDelta = tileSize;
        runnerImage.rectTransform.SetAsLastSibling();
        runnerImage.rectTransform.anchoredPosition = WorldToAnchoredIn(vfxRoot, path[0].position);

        AudioSource flightSource = CreateFlightAudioSource(runnerImage.rectTransform);
        StartFlightAudio(flightSource);

        float tAfter = 0f;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2 target = WorldToAnchoredIn(vfxRoot, path[i].position);

            while (Vector2.Distance(runnerImage.rectTransform.anchoredPosition, target) > arriveEps)
            {
                runnerImage.rectTransform.anchoredPosition =
                    Vector2.MoveTowards(runnerImage.rectTransform.anchoredPosition, target, dashSpeed * Time.deltaTime);

                tAfter += Time.deltaTime;
                if (tAfter >= spawnEvery)
                {
                    tAfter = 0f;
                    SpawnAfterImageAt(runnerImage.rectTransform, runnerImage.sprite);
                }

                yield return null;
            }
        }

        var rt = runnerImage.rectTransform;
        Vector3 baseScale = rt.localScale;
        rt.localScale = baseScale * 1.15f;
        yield return new WaitForSeconds(0.06f);
        rt.localScale = baseScale;

        yield return StopFlightAudioRoutine(flightSource);

        runnerImage.enabled = false;
        co = null;
    }

    private void SpawnAfterImageAt(RectTransform source, Sprite sprite)
    {
        if (vfxRoot == null || source == null) return;

        var go = new GameObject("PatchbotAfterImage", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(vfxRoot, false);

        var img = go.GetComponent<Image>();
        var rt = (RectTransform)go.transform;

        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = afterColor;

        rt.anchorMin = source.anchorMin;
        rt.anchorMax = source.anchorMax;
        rt.pivot = source.pivot;

        rt.sizeDelta = source.sizeDelta;
        rt.anchoredPosition = source.anchoredPosition;
        rt.localScale = source.localScale;

        rt.SetSiblingIndex(Mathf.Max(0, source.GetSiblingIndex() - 1));

        StartCoroutine(FadeAndDestroy(go, img, afterLife));
    }

    private IEnumerator FadeAndDestroy(GameObject go, Image img, float life)
    {
        float t = 0f;
        Color start = img.color;

        while (t < life)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(start.a, 0f, t / life);
            img.color = new Color(start.r, start.g, start.b, a);
            yield return null;
        }

        Destroy(go);
    }

    public float EstimateDashDuration(BoardController board, Vector2Int fromCell, Vector2Int toCell, float syncDuration = -1f)
    {
        if (board == null || vfxRoot == null) return 0f;

        Vector3 fromWorld = board.GetCellWorldPosition(fromCell.x, fromCell.y);
        Vector3 toWorld = board.GetCellWorldPosition(toCell.x, toCell.y);
        Vector2 from = WorldToAnchoredIn(vfxRoot, fromWorld);
        Vector2 to = WorldToAnchoredIn(vfxRoot, toWorld);

        float distance = Vector2.Distance(from, to);
        if (distance <= arriveEps) return 0f;

        if (syncDuration > 0f)
            return syncDuration * Mathf.Max(0.01f, syncedDurationMultiplier);

        // Solo dash timing ile aynı: takeoff + hover sabit, dive pike hızıyla.
        float speed = Mathf.Max(1f, dashSpeed * Mathf.Max(0.01f, pikeSpeedMultiplier));
        float dive = Mathf.Max(0.05f, distance / speed);
        float takeoff = Mathf.Max(0f, takeoffBurstDuration);
        float hover = Mathf.Max(0f, hoverHoldDuration);
        return takeoff + hover + dive;
    }

    private AudioSource CreateFlightAudioSource(RectTransform attachTo)
    {
        if (attachTo == null || flightLoopClip == null)
            return null;

        var go = new GameObject("PatchbotFlightAudio", typeof(AudioSource));
        go.transform.SetParent(attachTo, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var source = go.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.clip = flightLoopClip;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = Mathf.Max(0f, flightLoopVolume + Random.Range(-flightVolumeJitter, flightVolumeJitter));
        source.pitch = Mathf.Max(0.01f, flightPitch + Random.Range(-flightPitchJitter, flightPitchJitter));
        source.priority = 160;
        return source;
    }

    private void StartFlightAudio(AudioSource source)
    {
        if (source == null || source.clip == null)
            return;

        if (!GameSettings.SoundEnabled)
            return;

        float targetVolume = source.volume;
        if (flightFadeIn > 0f)
            source.volume = 0f;

        source.Play();

        if (flightFadeIn > 0f && targetVolume > 0f)
            StartCoroutine(FadeAudioVolumeRoutine(source, targetVolume, flightFadeIn));
    }

    private IEnumerator StopFlightAudioRoutine(AudioSource source)
    {
        if (source == null)
            yield break;

        if (flightFadeOut <= 0f || !source.isPlaying)
        {
            source.Stop();
            if (source.gameObject != null)
                Destroy(source.gameObject);
            yield break;
        }

        float startVolume = source.volume;
        float elapsed = 0f;
        while (elapsed < flightFadeOut && source != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, flightFadeOut));
            source.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (source != null)
        {
            source.Stop();
            if (source.gameObject != null)
                Destroy(source.gameObject);
        }
    }

    private IEnumerator FadeAudioVolumeRoutine(AudioSource source, float targetVolume, float duration)
    {
        if (source == null)
            yield break;

        if (duration <= 0f)
        {
            source.volume = targetVolume;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && source != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        if (source != null)
            source.volume = targetVolume;
    }

static Vector2 WorldToAnchoredIn(RectTransform targetSpace, Vector3 worldPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetSpace,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out var localPoint
        );
        return localPoint;
    }
}
