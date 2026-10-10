using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A scored stone's ghost: lift, curved flight, colored trail, then an arrival flash.</summary>
public sealed class AncientSealCollectFx : IDisposable
{
    private readonly AncientSealView target;
    private readonly int color;
    private readonly float stagger;
    private readonly float cellSize;
    private readonly Color tint;
    private readonly Vector2 start;
    private RectTransform root;
    private RectTransform stone;
    private Image stoneImage;
    private FlightTrailFx trail;

    public AncientSealCollectFx(MonoBehaviour host, RectTransform overlay, BoardController board,
        AncientSealView target, TileType type, int color, Vector3 worldStart, float stagger)
    {
        this.target = target;
        this.color = color;
        this.stagger = stagger;
        tint = color == 0 ? new Color(1f, 0.25f, 0.3f)
            : color == 1 ? new Color(1f, 0.82f, 0.2f) : new Color(0.3f, 1f, 0.45f);

        var go = new GameObject("SealStoneFlight", typeof(RectTransform));
        go.layer = overlay.gameObject.layer;
        root = (RectTransform)go.transform;
        root.SetParent(overlay, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = root.offsetMax = Vector2.zero;
        start = root.InverseTransformPoint(worldStart);
        cellSize = Mathf.Max(1f, root.InverseTransformVector(
            board.TilesRoot.TransformVector(Vector3.right * board.TileSize)).magnitude);
        stone = FlightTrailFx.CreateImage(root, "CollectedStone", board.GetIcon(type), Color.white, cellSize * 0.7f);
        stoneImage = stone.GetComponent<Image>();
        stoneImage.preserveAspect = true;
        stone.anchoredPosition = start;
        trail = FlightTrailFx.Create(host, stone, tint, cellSize * 0.45f);
    }

    private bool HasTarget => root != null && stone != null && target != null && target.isActiveAndEnabled;

    public IEnumerator Play(Action onArrival)
    {
        // A quick lift makes the ghost readable against the original tile's break particles.
        float liftTime = 0.1f + stagger;
        Vector2 launch = start + Vector2.up * (cellSize * 0.25f);
        for (float t = 0f; t < liftTime; t += Time.deltaTime)
        {
            if (!HasTarget) yield break;
            float k = Mathf.Clamp01(t / liftTime);
            stone.anchoredPosition = Vector2.Lerp(start, launch, 1f - (1f - k) * (1f - k));
            stone.localScale = Vector3.one * (0.85f + Mathf.Sin(k * Mathf.PI) * 0.2f);
            yield return null;
        }

        if (!HasTarget) yield break;
        Vector2 initialEnd = root.InverseTransformPoint(target.GetCollectionTarget(color));
        Vector2 delta = initialEnd - launch;
        float distance = delta.magnitude;
        Vector2 normal = distance > 0.01f ? new Vector2(-delta.y, delta.x) / distance : Vector2.right;
        float bend = Mathf.Clamp(distance * 0.22f, cellSize * 0.6f, cellSize * 2f);
        float direction = delta.x >= 0f ? 1f : -1f;
        Vector2 control = (launch + initialEnd) * 0.5f + normal * (bend * direction);
        float duration = Mathf.Clamp(0.46f + distance / cellSize * 0.035f, 0.5f, 0.78f);
        float progress = 0f;
        while (progress < 1f)
        {
            if (!HasTarget) yield break;
            progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
            // The next colored socket may still be rotating into place. Wait just short of landing.
            if (!target.IsCollectionTargetReady(color)) progress = Mathf.Min(progress, 0.9f);
            float k = progress * progress * (3f - 2f * progress);
            Vector2 end = root.InverseTransformPoint(target.GetCollectionTarget(color));
            Vector2 position = (1f - k) * (1f - k) * launch + 2f * (1f - k) * k * control + k * k * end;
            stone.anchoredPosition = position;
            stone.localScale = Vector3.one * Mathf.Lerp(0.95f, 0.22f, k * k);
            stone.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI) * direction * 18f);
            trail?.Step(position, progress);
            if (progress < 1f) yield return null;
        }

        // This is the only point at which gameplay consumes this stone.
        Vector2 impactPosition = stone.anchoredPosition;
        stoneImage.enabled = false;
        trail?.Finish();
        onArrival?.Invoke();

        var flash = FlightTrailFx.CreateImage(root, "SealArrivalGlow", FlightTrailFx.GlowSprite(), tint, cellSize);
        flash.anchoredPosition = impactPosition;
        var flashImage = flash.GetComponent<Image>();
        var sparks = new RectTransform[5];
        var sparkImages = new Image[sparks.Length];
        for (int i = 0; i < sparks.Length; i++)
        {
            sparks[i] = FlightTrailFx.CreateImage(root, "SealArrivalSpark", FlightTrailFx.GlowSprite(), Color.white, cellSize * 0.12f);
            sparkImages[i] = sparks[i].GetComponent<Image>();
        }
        const float impactTime = 0.2f;
        for (float t = 0f; t < impactTime; t += Time.deltaTime)
        {
            if (root == null || flash == null) yield break;
            float k = Mathf.Clamp01(t / impactTime);
            flash.localScale = Vector3.one * Mathf.Lerp(0.3f, 0.95f, k);
            flashImage.color = new Color(tint.r, tint.g, tint.b, (1f - k) * 0.85f);
            for (int i = 0; i < sparks.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / sparks.Length;
                sparks[i].anchoredPosition = impactPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (cellSize * 0.45f * k);
                sparkImages[i].color = new Color(1f, 1f, 0.8f, 1f - k);
            }
            yield return null;
        }
    }

    public void Dispose()
    {
        trail?.Finish();
        trail = null;
        if (root != null) UnityEngine.Object.Destroy(root.gameObject);
        root = null;
    }
}
