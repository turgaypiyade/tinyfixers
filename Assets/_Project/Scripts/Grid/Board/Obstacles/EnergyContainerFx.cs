using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight VFX helper for EnergyContainerService.
/// It intentionally uses runtime UI ghosts and existing obstacle visuals so no prefab
/// is required for the first integration pass. You can later replace this with a
/// dedicated prefab under Assets/_Project/Prefabs/FX/EnergyContainer.
/// </summary>
public sealed class EnergyContainerFx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private RectTransform overlayRoot;

    [Header("Orb Visual")]
    [SerializeField] private Sprite energyOrbSprite;
    [SerializeField] private Vector2 orbSize = new Vector2(42f, 42f);
    [SerializeField] private float flyDuration = 0.42f;
    [SerializeField] private float orbStagger = 0.035f;
    [SerializeField] private float arcHeight = 130f;

    [Header("Container Feedback")]
    [SerializeField] private float pulseScale = 1.08f;
    [SerializeField] private float pulseDuration = 0.10f;
    [SerializeField, Range(0.1f, 1f)] private float exhaustedAlpha = 0.42f;

    private readonly Dictionary<int, int> hitVisualCounters = new();
    private readonly HashSet<int> exhaustedOrigins = new();

    private void Awake()
    {
        if (board == null)
            board = GetComponent<BoardController>()
                    ?? GetComponentInParent<BoardController>(true)
                    ?? FindFirstObjectByType<BoardController>();

        if (overlayRoot == null && board != null)
            overlayRoot = board.ContentRoot != null ? board.ContentRoot : board.Parent;
    }

    public void PlayHit(int originIndex, CollectibleId collectibleId, int remainingEnergy, bool goalAccepted)
    {
        if (originIndex < 0 || board == null)
            return;

        hitVisualCounters.TryGetValue(originIndex, out int hitIndex);
        hitVisualCounters[originIndex] = hitIndex + 1;

        StartCoroutine(CoPulseObstacle(originIndex));

        if (goalAccepted)
            StartCoroutine(CoFlyOrb(originIndex, collectibleId, hitIndex * Mathf.Max(0f, orbStagger)));

        if (remainingEnergy <= 0)
            SetExhausted(originIndex, null);
    }

    public void SetExhausted(int originIndex, Sprite exhaustedSprite)
    {
        if (originIndex < 0 || !exhaustedOrigins.Add(originIndex))
            return;

        StartCoroutine(CoSetExhausted(originIndex, exhaustedSprite));
    }

    private IEnumerator CoPulseObstacle(int originIndex)
    {
        RectTransform target = FindObstacleRect(originIndex);
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

    private IEnumerator CoSetExhausted(int originIndex, Sprite exhaustedSprite)
    {
        // Wait one frame so GridSpawner has applied the stage visual change first.
        yield return null;

        RectTransform target = FindObstacleRect(originIndex);
        if (target == null)
            yield break;

        if (target.TryGetComponent<Image>(out var image))
        {
            if (exhaustedSprite != null)
                image.sprite = exhaustedSprite;

            Color c = image.color;
            c.a = exhaustedAlpha;
            image.color = c;
        }
    }

    private IEnumerator CoFlyOrb(int originIndex, CollectibleId collectibleId, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (board == null)
            yield break;

        var hud = board.TopHud;
        if (hud == null || !hud.TryGetGoalTargetRectForCollectible(collectibleId, out var targetSlot) || targetSlot == null)
            yield break;

        RectTransform source = FindObstacleRect(originIndex);
        if (source == null)
            yield break;

        RectTransform root = overlayRoot != null ? overlayRoot : (board.ContentRoot != null ? board.ContentRoot : board.Parent);
        if (root == null)
            yield break;

        var go = new GameObject("EnergyOrbFlyGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = orbSize;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var image = go.GetComponent<Image>();
        image.sprite = energyOrbSprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        if (energyOrbSprite == null)
            image.color = new Color(0.35f, 0.9f, 1f, 1f);

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 1f;

        Vector2 start = WorldToLocalIn(root, source);
        Vector2 end = WorldToLocalIn(root, targetSlot);
        Vector2 mid = (start + end) * 0.5f;
        float dir = end.x >= start.x ? 1f : -1f;
        Vector2 control = mid + new Vector2(80f * dir, arcHeight);

        float duration = Mathf.Max(0.08f, flyDuration);
        float t = 0f;
        while (t < duration)
        {
            if (rt == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = EaseInOut(k);
            rt.anchoredPosition = Bezier2(start, control, end, e);
            float s = Mathf.Lerp(1.15f, 0.25f, k * k);
            rt.localScale = new Vector3(s, s, 1f);
            cg.alpha = k < 0.82f ? 1f : 1f - Mathf.InverseLerp(0.82f, 1f, k);
            yield return null;
        }

        if (go != null)
            Destroy(go);

        yield return PunchTarget(targetSlot);
    }

    private RectTransform FindObstacleRect(int originIndex)
    {
        if (board == null || board.ContentRoot == null)
            return null;

        string expectedName = $"Obstacle_{originIndex}";
        var rects = board.ContentRoot.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            var rt = rects[i];
            if (rt != null && rt.name == expectedName)
                return rt;
        }

        return null;
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

    private IEnumerator PunchTarget(RectTransform target)
    {
        if (target == null)
            yield break;

        RectTransform punchTarget = (target.parent as RectTransform) ?? target;
        Vector3 baseScale = punchTarget.localScale;
        Vector3 peak = baseScale * 1.10f;
        float dur = 0.08f;

        float t = 0f;
        while (t < dur)
        {
            if (punchTarget == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            punchTarget.localScale = Vector3.LerpUnclamped(baseScale, peak, 1f - (1f - k) * (1f - k));
            yield return null;
        }

        t = 0f;
        while (t < dur)
        {
            if (punchTarget == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            punchTarget.localScale = Vector3.LerpUnclamped(peak, baseScale, k * k);
            yield return null;
        }

        if (punchTarget != null)
            punchTarget.localScale = baseScale;
    }
}
