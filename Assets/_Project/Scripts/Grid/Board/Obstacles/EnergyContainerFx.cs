using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight VFX helper for EnergyContainerService.
/// Uses three container frame sprites: closed -> half open -> full open.
/// The energy orb starts flying only after the full-open frame is shown.
/// </summary>
public sealed class EnergyContainerFx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private RectTransform overlayRoot;

    [Header("Container Frames")]
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite halfOpenSprite;
    [SerializeField] private Sprite fullOpenSprite;
    [SerializeField] private Sprite exhaustedSprite;
    [SerializeField, Min(0f)] private float halfOpenDelay = 0.07f;
    [SerializeField, Min(0f)] private float fullOpenDelay = 0.08f;
    [SerializeField, Min(0f)] private float fullOpenHold = 0.04f;
    [SerializeField, Min(0f)] private float closeDelay = 0.12f;

    [Header("Container Cell Fit")]
    [SerializeField] private bool fitContainerToCell = true;
    [SerializeField, Range(0.5f, 1.25f)] private float containerCellScale = 1f;
    [SerializeField] private Vector2 containerCellOffset = Vector2.zero;
    [SerializeField] private bool preserveContainerAspect = true;

    [Header("Orb Visual")]
    [SerializeField] private Sprite energyOrbSprite;
    [SerializeField] private Vector2 orbSize = new Vector2(42f, 42f);
    [SerializeField] private float flyDuration = 0.42f;
    [SerializeField] private float orbStagger = 0.035f;
    [SerializeField] private float arcHeight = 130f;
    [SerializeField, Min(0f)] private float orbRiseDuration = 0.16f;
    [SerializeField, Min(0f)] private float orbHoverHold = 0.06f;
    [SerializeField] private float orbRiseHeight = 82f;
    [Tooltip("When enabled, the flying orb is parented to the top canvas instead of the board VFX root, so it can travel outside BoardMask toward TopHUD without being clipped.")]
    [SerializeField] private bool flyOrbOnTopCanvas = true;

    [Header("Container Feedback")]
    [SerializeField] private float pulseScale = 1.08f;
    [SerializeField] private float pulseDuration = 0.10f;
    [Tooltip("Squash efekti: X ekseninde büyütme çarpanı (>1 = yanlara şişer). 0 = uniform pulse kullan.")]
    [SerializeField] private float squashX = 0f;
    [Tooltip("Squash efekti: Y ekseninde küçültme çarpanı (<1 = yukarı kısalır). 0 = uniform pulse kullan.")]
    [SerializeField] private float squashY = 0f;
    [SerializeField, Range(0.1f, 1f)] private float exhaustedAlpha = 0.42f;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly Dictionary<int, int> hitVisualCounters = new();
    private readonly Dictionary<int, int> activeReleaseSequences = new();
    private readonly HashSet<int> exhaustedOrigins = new();

    // Per-origin obstacle Image cache. The full hierarchy scan in FindObstacleImageScan
    // is expensive and used to run several times per hit; launcher/container images persist
    // for the whole level (only faded on exhaust), so the lookup result is stable per origin.
    private readonly Dictionary<int, Image> obstacleImageCache = new();
    private TopHudController cachedHud;

    // Flying orb ghosts are pooled instead of created/destroyed per hit. With a 750 goal and
    // many launchers firing in the same frame this avoids a GameObject alloc + GC churn spike.
    private readonly Stack<GameObject> orbPool = new();

    private void Awake()
    {
        if (board == null)
            board = GetComponent<BoardController>()
                    ?? GetComponentInParent<BoardController>(true)
                    ?? FindFirstObjectByType<BoardController>();

        if (overlayRoot == null && board != null)
            overlayRoot = board.ContentRoot != null ? board.ContentRoot : board.Parent;
    }

    public void PlayHit(int originIndex, CollectibleId collectibleId, int remainingEnergy, bool goalAccepted, System.Action onOrbArrived = null)
    {
        if (originIndex < 0 || board == null)
            return;

        hitVisualCounters.TryGetValue(originIndex, out int hitIndex);
        hitVisualCounters[originIndex] = hitIndex + 1;

        float delay = hitIndex * Mathf.Max(0f, orbStagger);
        StartCoroutine(CoPlayReleaseSequence(originIndex, collectibleId, remainingEnergy, goalAccepted, delay, onOrbArrived));
    }

    public void SetExhausted(int originIndex, Sprite overrideExhaustedSprite)
    {
        if (originIndex < 0 || !exhaustedOrigins.Add(originIndex))
            return;

        StartCoroutine(CoSetExhausted(originIndex, overrideExhaustedSprite));
    }

    private IEnumerator CoPlayReleaseSequence(
        int originIndex,
        CollectibleId collectibleId,
        int remainingEnergy,
        bool goalAccepted,
        float delay,
        System.Action onOrbArrived = null)
    {
        System.IDisposable presentationActivity = BeginPresentationActivity();
        try
        {
            IncrementActiveSequence(originIndex);

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            yield return null;

            Image image = FindObstacleImage(originIndex);
            RectTransform target = image != null ? image.rectTransform : null;

            if (image != null)
                FitContainerImageToCell(image, originIndex);

            if (logDebug)
            {
                Debug.Log(
                    $"[EnergyContainerFx] release origin={originIndex} " +
                    $"target={(target != null ? target.name : "null")} " +
                    $"image={(image != null ? image.name : "null")} " +
                    $"goalAccepted={goalAccepted} remainingEnergy={remainingEnergy}");
            }

            if (target != null)
                StartCoroutine(CoPulseObstacle(target));

            if (image != null)
            {
                ApplyFrame(image, halfOpenSprite, originIndex);
                if (halfOpenDelay > 0f)
                    yield return new WaitForSeconds(halfOpenDelay);

                ApplyFrame(image, fullOpenSprite, originIndex);
                if (fullOpenDelay > 0f)
                    yield return new WaitForSeconds(fullOpenDelay);
            }
            else if (halfOpenDelay + fullOpenDelay > 0f)
            {
                yield return new WaitForSeconds(halfOpenDelay + fullOpenDelay);
            }

            if (fullOpenHold > 0f)
                yield return new WaitForSeconds(fullOpenHold);

            // Orb her zaman görsel olarak uçar; onOrbArrived sadece goal kabul edilmişse çağrılır.
            StartCoroutine(CoFlyOrb(originIndex, collectibleId, 0f, goalAccepted ? onOrbArrived : null));

            if (closeDelay > 0f)
                yield return new WaitForSeconds(closeDelay);

            int activeAfterThis = DecrementActiveSequence(originIndex);
            if (remainingEnergy <= 0)
            {
                SetExhausted(originIndex, null);
                yield break;
            }

            if (activeAfterThis <= 0 && image != null && !exhaustedOrigins.Contains(originIndex))
                ApplyFrame(image, closedSprite, originIndex);
        }
        finally
        {
            presentationActivity?.Dispose();
        }
    }

    private IEnumerator CoPulseObstacle(RectTransform target)
    {
        if (target == null)
            yield break;

        Vector3 baseScale = target.localScale;
        Vector3 peak;

        bool useSquash = squashX > 0f && squashY > 0f;
        if (useSquash)
            peak = new Vector3(baseScale.x * squashX, baseScale.y * squashY, baseScale.z);
        else
            peak = baseScale * Mathf.Max(1f, pulseScale);

        float half = Mathf.Max(0.01f, pulseDuration * 0.5f);

        float t = 0f;
        while (t < half)
        {
            if (target == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            target.localScale = Vector3.LerpUnclamped(baseScale, peak, 1f - (1f - k) * (1f - k));
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            if (target == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            target.localScale = Vector3.LerpUnclamped(peak, baseScale, k * k);
            yield return null;
        }

        if (target != null)
            target.localScale = baseScale;
    }

    private IEnumerator CoSetExhausted(int originIndex, Sprite stageSpriteFallback)
    {
        System.IDisposable presentationActivity = BeginPresentationActivity();
        try
        {
            yield return null;

            Image image = GetObstacleImage(originIndex);
            if (image == null)
            {
                if (logDebug)
                    Debug.LogWarning($"[EnergyContainerFx] Exhausted image not found. origin={originIndex}");
                yield break;
            }

            FitContainerImageToCell(image, originIndex);

            // Explicit exhausted frame must win over the ObstacleLibrary final-stage sprite.
            // The stage sprite is only a fallback for older setups with no exhausted sprite assigned.
            Sprite finalSprite = exhaustedSprite != null
                ? exhaustedSprite
                : (stageSpriteFallback != null ? stageSpriteFallback : fullOpenSprite);

            ApplyFrame(image, finalSprite, originIndex, allowExhaustedOverride: true);

            Color c = image.color;
            c.a = exhaustedAlpha;
            image.color = c;
        }
        finally
        {
            presentationActivity?.Dispose();
        }
    }

    private System.IDisposable BeginPresentationActivity()
    {
        return board != null && board.UseFlowActivities
            ? board.Flow.BeginNonBlocking(BoardFlowScheduler.ActivityKind.Presentation)
            : null;
    }

    private IEnumerator CoFlyOrb(int originIndex, CollectibleId collectibleId, float delay, System.Action onArrived = null)
    {
        // onArrived, orb görsel olarak hedefe varınca çağrılır.
        // Erken çıkış (board null, hud null vb.) durumunda da mutlaka çağrılmalı
        // ki hedef sayacı her koşulda güncellenmis olsun.
        try
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (board == null)
                yield break;

            if (cachedHud == null)
                cachedHud = FindFirstObjectByType<TopHudController>();
            var hud = cachedHud;
            RectTransform targetSlot = null;
            hud?.TryGetGoalTargetRectForCollectible(collectibleId, out targetSlot);

            RectTransform root = targetSlot != null
                ? ResolveOrbFlyRoot(targetSlot)
                : (overlayRoot != null ? overlayRoot : board.ContentRoot);

            if (root == null)
                yield break;

            Vector2 start = GetOriginCenterIn(root, originIndex);
            Vector2 end = targetSlot != null
                ? WorldToLocalIn(root, targetSlot)
                : start + new Vector2(0f, root.rect.height * 0.45f); // hedef yok → yukarı uç

            GameObject go = RentOrb(root);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = orbSize;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = start;
            rt.SetAsLastSibling();

            var image = go.GetComponent<Image>();
            image.sprite = energyOrbSprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = energyOrbSprite == null ? new Color(0.35f, 0.9f, 1f, 1f) : Color.white;

            var cg = go.GetComponent<CanvasGroup>();
            cg.alpha = 1f;

            if (logDebug)
            {
                Debug.Log(
                    $"[EnergyContainerFx] FlyOrb root={root.name} start={start} end={end} " +
                    $"sprite={(energyOrbSprite != null ? energyOrbSprite.name : "null")}");
            }

            Vector2 flightStart = start + Vector2.up * orbRiseHeight;
            float riseDuration = Mathf.Max(0f, orbRiseDuration);
            float riseTime = 0f;
            while (riseTime < riseDuration)
            {
                if (rt == null)
                    break;

                riseTime += Time.deltaTime;
                float k = Mathf.Clamp01(riseTime / Mathf.Max(0.0001f, riseDuration));
                float e = 1f - (1f - k) * (1f - k);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, flightStart, e);
                float s = Mathf.Lerp(0.45f, 1.15f, e);
                rt.localScale = new Vector3(s, s, 1f);
                cg.alpha = e;
                yield return null;
            }

            if (rt != null)
            {
                rt.anchoredPosition = flightStart;
                rt.localScale = new Vector3(1.15f, 1.15f, 1f);
                cg.alpha = 1f;
            }

            if (orbHoverHold > 0f)
                yield return new WaitForSeconds(orbHoverHold);

            Vector2 mid = (flightStart + end) * 0.5f;
            float dir = end.x >= flightStart.x ? 1f : -1f;
            Vector2 control = mid + new Vector2(80f * dir, arcHeight);

            float duration = Mathf.Max(0.08f, flyDuration);
            float t = 0f;
            while (t < duration)
            {
                if (rt == null)
                    break;

                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = EaseInOut(k);
                rt.anchoredPosition = Bezier2(flightStart, control, end, e);
                float s = Mathf.Lerp(1.15f, 0.25f, k * k);
                rt.localScale = new Vector3(s, s, 1f);
                cg.alpha = k < 0.82f ? 1f : 1f - Mathf.InverseLerp(0.82f, 1f, k);
                yield return null;
            }

            if (go != null)
                ReturnOrb(go);
        }
        finally
        {
            onArrived?.Invoke();
        }
    }

    private GameObject RentOrb(RectTransform root)
    {
        GameObject go = null;
        while (orbPool.Count > 0 && go == null)
            go = orbPool.Pop(); // skip any entries destroyed by scene teardown

        if (go == null)
            go = new GameObject("EnergyOrbFlyGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));

        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        go.SetActive(true);
        return go;
    }

    private void ReturnOrb(GameObject go)
    {
        if (go == null)
            return;

        go.SetActive(false);
        orbPool.Push(go);
    }

    private RectTransform ResolveOrbFlyRoot(RectTransform targetSlot)
    {
        if (!flyOrbOnTopCanvas && overlayRoot != null)
            return overlayRoot;

        Canvas targetCanvas = targetSlot != null ? targetSlot.GetComponentInParent<Canvas>() : null;
        Canvas boardCanvas = board != null && board.ContentRoot != null ? board.ContentRoot.GetComponentInParent<Canvas>() : null;
        Canvas selectedCanvas = targetCanvas != null ? targetCanvas.rootCanvas : (boardCanvas != null ? boardCanvas.rootCanvas : null);

        RectTransform canvasRoot = selectedCanvas != null ? selectedCanvas.transform as RectTransform : null;
        if (canvasRoot != null)
            return canvasRoot;

        return overlayRoot != null ? overlayRoot : (board != null && board.ContentRoot != null ? board.ContentRoot : board?.Parent);
    }

    private RectTransform FindObstacleRect(int originIndex)
    {
        Image image = FindObstacleImage(originIndex);
        return image != null ? image.rectTransform : null;
    }

    private Image GetObstacleImage(int originIndex)
    {
        return FindObstacleImage(originIndex);
    }

    private Image FindObstacleImage(int originIndex)
    {
        // Cache hit: launcher/container images are stable for the level, so we avoid the
        // full GetComponentsInChildren<Image> scan on every hit (the main editor stall cause).
        if (obstacleImageCache.TryGetValue(originIndex, out Image cached) && cached != null)
            return cached;

        Image found = FindObstacleImageScan(originIndex);
        if (found != null)
            obstacleImageCache[originIndex] = found;

        return found;
    }

    private Image FindObstacleImageScan(int originIndex)
    {
        if (board == null || board.ContentRoot == null || board.Width <= 0 || board.TileSize <= 0)
            return null;

        RectTransform root = board.ContentRoot;
        Vector2 expected = GetOriginCenterIn(root, originIndex, allowObstacleLookup: false);
        Image[] images = root.GetComponentsInChildren<Image>(true);

        Image best = null;
        float bestDistance = float.MaxValue;
        Image bestLoose = null;
        float bestLooseDistance = float.MaxValue;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            RectTransform rt = image != null ? image.rectTransform : null;
            if (rt == null)
                continue;

            if (ShouldIgnoreImage(image))
                continue;

            Vector2 center = WorldToLocalIn(root, rt);
            float distance = Vector2.SqrMagnitude(center - expected);

            bool strongCandidate = IsContainerFrameSprite(image.sprite) || IsLikelyObstacleHierarchy(rt);
            if (strongCandidate && distance < bestDistance)
            {
                best = image;
                bestDistance = distance;
            }

            if (distance < bestLooseDistance)
            {
                bestLoose = image;
                bestLooseDistance = distance;
            }
        }

        float maxDistance = Mathf.Max(8f, board.TileSize * 0.75f);
        float maxDistanceSq = maxDistance * maxDistance;

        if (best != null && bestDistance <= maxDistanceSq)
            return best;

        if (bestLoose != null && bestLooseDistance <= maxDistanceSq)
            return bestLoose;

        if (logDebug)
        {
            Debug.LogWarning(
                $"[EnergyContainerFx] No obstacle image found. origin={originIndex} " +
                $"expected={expected} best={(best != null ? best.name : "null")} bestDist={Mathf.Sqrt(bestDistance):0.0} " +
                $"loose={(bestLoose != null ? bestLoose.name : "null")} looseDist={Mathf.Sqrt(bestLooseDistance):0.0}");
        }

        return null;
    }

    private bool ShouldIgnoreImage(Image image)
    {
        if (image == null)
            return true;

        string n = image.name;
        if (n.StartsWith("GridLine") || n.Contains("CellBG") || n.Contains("EnergyOrbFlyGhost"))
            return true;

        Transform t = image.transform;
        while (t != null)
        {
            string tn = t.name;
            if (tn.Contains("TopHUD") || tn.Contains("Goal") || tn.Contains("VFXRoot"))
                return true;
            t = t.parent;
        }

        return false;
    }

    private bool IsContainerFrameSprite(Sprite sprite)
    {
        return sprite != null &&
               (sprite == closedSprite || sprite == halfOpenSprite || sprite == fullOpenSprite || sprite == exhaustedSprite);
    }

    private static bool IsLikelyObstacleHierarchy(RectTransform rt)
    {
        Transform t = rt;
        while (t != null)
        {
            string n = t.name;
            if (n.Contains("Obstacle") || n.Contains("Obstacles") || n.Contains("OverTiles") || n.Contains("UnderTiles"))
                return true;
            t = t.parent;
        }

        return false;
    }

    private void FitContainerImageToCell(Image image, int originIndex)
    {
        if (!fitContainerToCell || image == null || board == null || board.Width <= 0 || board.TileSize <= 0)
            return;

        RectTransform rt = image.rectTransform;
        if (rt == null)
            return;

        RectTransform parent = rt.parent as RectTransform;
        if (parent == null)
            return;

        int x = originIndex % board.Width;
        int y = originIndex / board.Width;
        float size = board.TileSize * Mathf.Max(0.01f, containerCellScale);
        Vector3 worldCenter;

        RectTransform tilesRoot = board.Parent;
        if (tilesRoot != null)
        {
            Vector3 localInTilesRoot = new Vector3(
                x * board.TileSize + board.TileSize * 0.5f + containerCellOffset.x,
                -y * board.TileSize - board.TileSize * 0.5f + containerCellOffset.y,
                0f);
            worldCenter = tilesRoot.TransformPoint(localInTilesRoot);
        }
        else
        {
            worldCenter = parent.TransformPoint(new Vector3(
                x * board.TileSize + board.TileSize * 0.5f + containerCellOffset.x,
                -y * board.TileSize - board.TileSize * 0.5f + containerCellOffset.y,
                0f));
        }

        Vector2 parentLocalCenter = parent.InverseTransformPoint(worldCenter);

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = parentLocalCenter;
        rt.sizeDelta = new Vector2(size, size);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        image.preserveAspect = preserveContainerAspect;
        image.raycastTarget = true;

        if (logDebug)
        {
            Debug.Log(
                $"[EnergyContainerFx] FitToCell origin={originIndex} image={image.name} " +
                $"pos={rt.anchoredPosition} size={rt.sizeDelta} preserveAspect={image.preserveAspect}");
        }
    }

    private void ApplyFrame(Image image, Sprite sprite, int originIndex, bool allowExhaustedOverride = false)
    {
        if (image == null)
            return;

        if (!allowExhaustedOverride && exhaustedOrigins.Contains(originIndex))
            return;

        FitContainerImageToCell(image, originIndex);

        if (sprite != null)
            image.sprite = sprite;

        Color c = image.color;
        c.a = 1f;
        image.color = c;

        if (logDebug)
            Debug.Log($"[EnergyContainerFx] ApplyFrame image={image.name} sprite={(image.sprite != null ? image.sprite.name : "null")}");
    }

    private int IncrementActiveSequence(int originIndex)
    {
        activeReleaseSequences.TryGetValue(originIndex, out int count);
        count++;
        activeReleaseSequences[originIndex] = count;
        return count;
    }

    private int DecrementActiveSequence(int originIndex)
    {
        if (!activeReleaseSequences.TryGetValue(originIndex, out int count))
            return 0;

        count = Mathf.Max(0, count - 1);
        if (count <= 0)
            activeReleaseSequences.Remove(originIndex);
        else
            activeReleaseSequences[originIndex] = count;

        return count;
    }

    private Vector2 GetOriginCenterIn(RectTransform root, int originIndex)
    {
        return GetOriginCenterIn(root, originIndex, allowObstacleLookup: true);
    }

    private Vector2 GetOriginCenterIn(RectTransform root, int originIndex, bool allowObstacleLookup)
    {
        if (allowObstacleLookup)
        {
            RectTransform obstacle = FindObstacleRect(originIndex);
            if (obstacle != null)
                return WorldToLocalIn(root, obstacle);
        }

        if (root == null || board == null || board.Width <= 0)
            return Vector2.zero;

        int x = originIndex % board.Width;
        int y = originIndex / board.Width;
        Vector3 localInTilesRoot = new Vector3(
            x * board.TileSize + board.TileSize * 0.5f,
            -y * board.TileSize - board.TileSize * 0.5f,
            0f);

        RectTransform tilesRoot = board.Parent;
        if (tilesRoot != null)
        {
            Vector3 world = tilesRoot.TransformPoint(localInTilesRoot);
            return root.InverseTransformPoint(world);
        }

        return new Vector2(localInTilesRoot.x, localInTilesRoot.y);
    }

    private static Vector2 WorldToLocalIn(RectTransform root, RectTransform source)
    {
        if (root == null || source == null)
            return Vector2.zero;

        Vector3 world = source.TransformPoint(source.rect.center);
        return root.InverseTransformPoint(world);
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
