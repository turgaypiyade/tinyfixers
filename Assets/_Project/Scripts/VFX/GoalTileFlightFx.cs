using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Normal goal tiles gather briefly in their match cluster, then peel toward the HUD.
/// Only copies move: tile lifetimes/data are never retained or modified by this driver.
/// One update loop, pooled Images, one epoch-safe level-end job per cluster.
/// </summary>
[DisallowMultipleComponent]
public sealed class GoalTileFlightFx : MonoBehaviour
{
    internal const string PoolKey = "GoalFly.Tile";
    internal const float GatherSeconds = .22f;
    internal const float StaggerSeconds = .045f;
    private const float PulseSeconds = .14f;

    private struct Cluster
    {
        public BoardController board;
        public LevelData level;
        public RectTransform target;
        public Sprite sprite;
        public IDisposable job;
        public Vector2 sum;
        public float cell;
        public int frame, count, remaining;
    }

    private struct Flight
    {
        public Image image;
        public RectTransform rect;
        public Vector2 start;
        public float age, delay, duration;
        public int cluster;
    }

    private struct Pulse
    {
        public RectTransform target;
        public Vector3 scale;
        public float age;
    }

    internal struct Pose
    {
        public Vector2 position;
        public Vector3 scale;
        public float alpha;
        public bool arrived;
    }

    private RectTransform root;
    private readonly List<Cluster> clusters = new(16);
    private readonly List<Flight> flights = new(64);
    private readonly List<Pulse> pulses = new(8);

    public bool Launch(BoardController board, RectTransform overlay, TileView tile, RectTransform target)
    {
        if (!isActiveAndEnabled || !Application.isPlaying || board == null || !board.isActiveAndEnabled
            || tile == null || overlay == null || !overlay.gameObject.activeInHierarchy || target == null)
            return false;
        var sprite = tile.GetIconSprite();
        if (sprite == null) return false;
        if (root != overlay) { CancelAll(); root = overlay; }

        var source = tile.IconImage != null ? tile.IconImage.rectTransform : tile.RectTransform;
        Vector2 start = root.InverseTransformPoint(source.TransformPoint(source.rect.center));
        Vector2 size = ObstacleScatterFx.SizeIn(root, source);
        float cell = Mathf.Max(1f, root.InverseTransformVector(tile.RectTransform.TransformVector(new Vector3(board.TileSize, 0, 0))).magnitude);
        var image = UiVfxPool.RentImage(PoolKey, root, "GoalTileGhost");
        if (image == null) return false;
        image.gameObject.layer = root.gameObject.layer;
        image.sprite = sprite;
        image.preserveAspect = true;
        var rect = image.rectTransform;
        // Anchoring at the parent's pivot makes anchoredPosition equal its local coordinate.
        rect.anchorMin = rect.anchorMax = root.pivot;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = start;
        rect.SetAsLastSibling();

        int index = FindCluster(board, target, sprite, start, cell);
        Cluster cluster;
        if (index < 0)
        {
            cluster = new Cluster
            {
                board = board, level = board.ActiveLevelData, target = target, sprite = sprite,
                cell = cell, frame = Time.frameCount,
                job = board.BeginJob(BoardController.BoardJobKind.GoalOrbFlight)
            };
            index = FreeCluster();
        }
        else cluster = clusters[index];
        int rank = cluster.count;
        cluster.sum += start;
        cluster.count++;
        cluster.remaining++;
        clusters[index] = cluster;
        Vector2 end = root.InverseTransformPoint(target.TransformPoint(target.rect.center));
        flights.Add(new Flight
        {
            image = image, rect = rect, start = start, cluster = index,
            delay = Mathf.Min(rank * StaggerSeconds, .18f),
            duration = Mathf.Clamp(.50f + Vector2.Distance(start, end) / cell * .025f, .56f, .78f)
        });
        return true;
    }

    private int FindCluster(BoardController board, RectTransform target, Sprite sprite, Vector2 start, float cell)
    {
        for (int i = 0; i < clusters.Count; i++)
        {
            var c = clusters[i];
            if (c.remaining > 0 && c.board == board && c.target == target && c.sprite == sprite
                && c.level == board.ActiveLevelData && c.frame == Time.frameCount
                && (start - c.sum / c.count).sqrMagnitude <= cell * cell * 6.25f)
                return i;
        }
        return -1;
    }

    private int FreeCluster()
    {
        for (int i = 0; i < clusters.Count; i++) if (clusters[i].remaining == 0) return i;
        clusters.Add(default);
        return clusters.Count - 1;
    }

    private void Update() => Tick(Time.deltaTime);

    private void Tick(float delta)
    {
        TickPulses(delta);
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            Cluster c = clusters[f.cluster];
            if (root == null || c.board == null || !c.board.isActiveAndEnabled || c.board.ActiveLevelData != c.level
                || c.target == null || !c.target.gameObject.activeInHierarchy || f.rect == null || f.image == null)
            { Release(i, false); continue; }
            // All clears submitted this frame join before the centroid starts moving.
            if (c.frame == Time.frameCount) continue;
            f.age += delta;
            Vector2 center = c.sum / c.count;
            Vector2 gather = center + (f.start - center) * .32f + Vector2.down * (c.cell * .10f);
            Vector2 end = root.InverseTransformPoint(c.target.TransformPoint(c.target.rect.center));
            Vector2 control = (gather + end) * .5f + new Vector2(end.x >= gather.x ? c.cell * .45f : -c.cell * .45f, c.cell * 1.2f);
            Pose pose = Sample(f.start, gather, control, end, f.age, f.delay, f.duration);
            f.rect.anchoredPosition = pose.position;
            f.rect.localScale = pose.scale;
            f.image.color = new Color(1, 1, 1, pose.alpha);
            flights[i] = f;
            if (pose.arrived) Release(i, true);
        }
    }

    internal static Pose Sample(Vector2 start, Vector2 gather, Vector2 control, Vector2 end, float age, float delay, float duration)
    {
        float pack = Mathf.Clamp01(age / GatherSeconds);
        float k = Mathf.Clamp01((age - GatherSeconds - delay) / Mathf.Max(.01f, duration));
        float smooth = pack * pack * (3f - 2f * pack);
        Vector2 position = Vector2.LerpUnclamped(start, gather, smooth);
        float scale = Mathf.Lerp(1f, .68f, smooth) + Mathf.Sin(pack * Mathf.PI) * .055f;
        if (k > 0)
        {
            float u = k * k * (3f - 2f * k), inv = 1f - u;
            position = inv * inv * gather + 2f * inv * u * control + u * u * end;
            scale = k < .75f ? Mathf.Lerp(.68f, .52f, k / .75f)
                : Mathf.Lerp(.52f, .24f, Mathf.SmoothStep(0, 1, (k - .75f) / .25f));
        }
        float stretch = Mathf.Sin(k * Mathf.PI) * .06f;
        // Fade only over the last 10% of the flight (Mathf.SmoothStep(a,b,t) is a lerp, not a threshold).
        float fade = Mathf.Clamp01((k - .90f) / .10f);
        return new Pose
        {
            position = position,
            scale = new Vector3(scale * (1f - stretch * .25f), scale * (1f + stretch), 1),
            alpha = 1f - fade * fade * (3f - 2f * fade),
            arrived = age >= GatherSeconds + delay + duration
        };
    }

    private void Release(int index, bool arrived)
    {
        Flight f = flights[index];
        Cluster c = clusters[f.cluster];
        if (arrived && c.target != null) StartPulse(c.target.parent as RectTransform ?? c.target);
        if (f.image != null) UiVfxPool.Return(PoolKey, f.image.gameObject);
        c.remaining--;
        if (c.remaining == 0) { c.job?.Dispose(); c = default; }
        clusters[f.cluster] = c;
        int last = flights.Count - 1;
        flights[index] = flights[last];
        flights.RemoveAt(last);
    }

    private void StartPulse(RectTransform target)
    {
        for (int i = 0; i < pulses.Count; i++)
        {
            var p = pulses[i];
            if (p.target != target) continue;
            p.age = 0; pulses[i] = p; return;
        }
        var scale = TryGetComponent(out GoalFlyFx owner) ? owner.BaseScaleOf(target) : target.localScale;
        pulses.Add(new Pulse { target = target, scale = scale });
    }

    private void TickPulses(float delta)
    {
        for (int i = pulses.Count - 1; i >= 0; i--)
        {
            var p = pulses[i]; p.age += delta;
            float k = Mathf.Clamp01(p.age / PulseSeconds);
            if (p.target != null) p.target.localScale = p.scale * (1f + .08f * Mathf.Sin(k * Mathf.PI));
            if (p.target != null && k < 1) { pulses[i] = p; continue; }
            if (p.target != null) p.target.localScale = p.scale;
            int last = pulses.Count - 1; pulses[i] = pulses[last]; pulses.RemoveAt(last);
        }
    }

    private void OnDisable() => CancelAll();
    private void OnDestroy() => CancelAll();
    private void CancelAll()
    {
        for (int i = flights.Count - 1; i >= 0; i--) Release(i, false);
        for (int i = 0; i < pulses.Count; i++)
            if (pulses[i].target != null) pulses[i].target.localScale = pulses[i].scale;
        pulses.Clear();
        clusters.Clear();
    }
}
