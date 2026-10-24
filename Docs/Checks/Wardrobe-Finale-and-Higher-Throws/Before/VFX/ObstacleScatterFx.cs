using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Short obstacle throws: pooled images, one update per board, no per-piece coroutines.</summary>
[DisallowMultipleComponent]
public sealed class ObstacleScatterFx : MonoBehaviour
{
    private const string PoolKey = "ObstacleScatter.Image";

    public struct Motion
    {
        // Ballistic: pixels/second and pixels/second². Eased: total displacement and fall distance.
        public Vector2 travel;
        public float gravity, spin, life, delay, rotation;
        public bool eased, burst;
    }

    private struct Flight
    {
        public Image image;
        public RectTransform rect;
        public Vector2 start;
        public Color tint;
        public Motion motion;
        public float age;
    }

    private BoardController owner;
    private LevelData level;
    private readonly List<Flight> flights = new(64);
    // PlaySplashFx consumes the sprite list synchronously; never retain a per-throw array.
    private readonly Sprite[] burstSprite = new Sprite[1];

    public static bool PlayCopy(BoardController board, RectTransform parent, Image source,
        string objectName, Motion motion)
    {
        if (source == null || parent == null) return false;
        var rect = source.rectTransform;
        Vector2 size = SizeIn(parent, rect);
        return Play(board, parent, source.sprite, rect.TransformPoint(rect.rect.center),
            size, source.color, objectName, motion);
    }

    public static Vector2 SizeIn(RectTransform parent, RectTransform source)
    {
        return new Vector2(
            parent.InverseTransformVector(source.TransformVector(new Vector3(source.rect.width, 0f, 0f))).magnitude,
            parent.InverseTransformVector(source.TransformVector(new Vector3(0f, source.rect.height, 0f))).magnitude);
    }

    public static bool Play(BoardController board, RectTransform parent, Sprite sprite,
        Vector3 worldCenter, Vector2 size, Color tint, string objectName, Motion motion)
    {
        if (board == null || !board.isActiveAndEnabled || parent == null ||
            !parent.gameObject.activeInHierarchy || sprite == null || !Application.isPlaying)
            return false;
        if (!board.TryGetComponent<ObstacleScatterFx>(out var driver))
            driver = board.gameObject.AddComponent<ObstacleScatterFx>();
        if (!driver.enabled) driver.enabled = true;
        if (driver.owner != board || driver.level != board.ActiveLevelData)
            driver.CancelAll();
        driver.owner = board;
        driver.level = board.ActiveLevelData;

        var image = UiVfxPool.RentImage(PoolKey, parent, objectName);
        if (image == null) return false;
        image.gameObject.layer = parent.gameObject.layer;
        image.sprite = sprite;
        image.color = tint;
        image.preserveAspect = true;
        var rect = image.rectTransform;
        rect.SetAsLastSibling();
        rect.position = worldCenter;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, motion.rotation);
        motion.life = Mathf.Max(0.05f, motion.life);
        motion.delay = Mathf.Max(0f, motion.delay);
        driver.flights.Add(new Flight
        {
            image = image, rect = rect, start = rect.anchoredPosition, tint = tint, motion = motion
        });
        return true;
    }

    public static void Cancel(BoardController board)
    {
        if (board != null && board.TryGetComponent<ObstacleScatterFx>(out var driver))
            driver.CancelAll();
    }

    private void LateUpdate()
    {
        if (flights.Count == 0) return;
        if (owner == null || !owner.isActiveAndEnabled || owner.ActiveLevelData != level)
        {
            CancelAll();
            return;
        }
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            var f = flights[i];
            if (f.image == null || !f.image.gameObject.activeInHierarchy)
            {
                Release(i, false);
                continue;
            }
            f.age += Time.deltaTime;
            float t = Mathf.Clamp(f.age - f.motion.delay, 0f, f.motion.life);
            float k = t / f.motion.life;
            f.rect.anchoredPosition = f.start + Offset(f.motion, t);
            float angle = f.age < f.motion.delay
                ? 10f * Mathf.Sin(f.age / f.motion.delay * Mathf.PI * 2f)
                : f.motion.spin * t;
            f.rect.localRotation = Quaternion.Euler(0f, 0f, f.motion.rotation + angle);
            float scale = f.motion.eased
                ? (k < 0.14f ? Mathf.Lerp(0.7f, 1f, k / 0.14f) : Mathf.Lerp(1f, 0.82f, (k - 0.14f) / 0.86f))
                : 1f;
            f.rect.localScale = Vector3.one * scale;
            Color c = f.tint;
            // Whole bulbs stay visible up to the burst; loose pieces fade over their final 40%.
            c.a *= f.motion.burst ? 1f : 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
            f.image.color = c;
            flights[i] = f;
            if (f.age >= f.motion.delay + f.motion.life) Release(i, f.motion.burst);
        }
    }

    internal static Vector2 Offset(Motion motion, float time)
    {
        float k = Mathf.Clamp01(time / Mathf.Max(0.05f, motion.life));
        float progress = motion.eased ? 1f - (1f - k) * (1f - k) : time;
        float fallTime = motion.eased ? k : time;
        return motion.travel * progress + Vector2.down * (0.5f * motion.gravity * fallTime * fallTime);
    }

    private void Release(int index, bool burst)
    {
        var f = flights[index];
        int last = flights.Count - 1;
        flights[index] = flights[last];
        flights.RemoveAt(last);
        if (f.image == null) return;
        Vector3 position = f.rect.TransformPoint(f.rect.rect.center);
        Sprite sprite = f.image.sprite;
        f.image.sprite = null;
        UiVfxPool.Return(PoolKey, f.image.gameObject);
        if (burst && owner != null && owner.isActiveAndEnabled && owner.ActiveLevelData == level &&
            owner.BreakFx != null && owner.ObstacleBreakFxPrefab != null)
        {
            burstSprite[0] = sprite;
            owner.BreakFx.PlaySplashFx(owner.ObstacleBreakFxPrefab, owner.ObstacleBreakFxLifetime,
                position, burstSprite);
            burstSprite[0] = null;
        }
    }

    private void CancelAll()
    {
        while (flights.Count > 0) Release(flights.Count - 1, false);
        owner = null;
        level = null;
    }

    private void OnDisable() => CancelAll();
    private void OnDestroy() => CancelAll();
}
