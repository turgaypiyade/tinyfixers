using UnityEngine;

/// <summary>
/// Görev → resim bölgesi eşlemesi (WonderTask.regions). Görev metni neyi söylüyorsa (kubbe, minare, meydan) restorasyon
/// tam orayı açar. Aynı sahiplik kuralı UI_WonderReveal.shader'da (taskOwner): noktayı içeren EN KÜÇÜK dikdörtgen kazanır,
/// hiçbirine girmeyen çevre pikselleri bütün adımlar boyunca yumuşakça renk kazanır.
/// Hiçbir görev tek başına bütün arka planı açmaz. Harika başına bir kez kurulur; animasyonda tahsis yok.
/// [[project_wonder_reveal_background]]
/// </summary>
public sealed class WonderTaskRegions
{
    public const int MaxRects = 24;   // shader dizi boyutu ile aynı

    static readonly int TaskRectsId = Shader.PropertyToID("_TaskRects");
    static readonly int TaskRectOwnersId = Shader.PropertyToID("_TaskRectOwners");
    static readonly int TaskRectCountId = Shader.PropertyToID("_TaskRectCount");

    readonly Vector4[] rects = new Vector4[MaxRects];   // xMin, yMin, xMax, yMax
    readonly float[] owners = new float[MaxRects];
    readonly Rect[] bounds;
    readonly WonderTaskAction[] actions;
    readonly float[] traceLengths;
    int rectCount;

    WonderTaskRegions(int taskCount)
    {
        bounds = new Rect[taskCount];
        actions = new WonderTaskAction[taskCount];
        traceLengths = new float[taskCount];
    }

    /// Bölge tanımlı değilse null → görünüm eski eğri parça düzenini kullanır.
    public static WonderTaskRegions From(WonderDefinition def)
    {
        if (def == null || !def.HasTaskRegions) return null;
        var tasks = def.tasks;
        var r = new WonderTaskRegions(tasks.Length);
        for (int t = 0; t < tasks.Length; t++)
        {
            var task = tasks[t];
            r.actions[t] = task != null ? task.action : WonderTaskAction.Repair;
            bool any = false;
            Rect b = new Rect(0f, 0f, 1f, 1f);
            bool owns = r.actions[t] != WonderTaskAction.Shine;   // parlama bölge sahiplenmez, yalnız üstünden geçer
            if (task != null && task.regions != null)
                foreach (var region in task.regions)
                {
                    Rect c = Clamp01(region);
                    if (c.width <= 0f || c.height <= 0f) continue;
                    if (!owns)
                    {
                        b = any ? Rect.MinMaxRect(Mathf.Min(b.xMin, c.xMin), Mathf.Min(b.yMin, c.yMin),
                            Mathf.Max(b.xMax, c.xMax), Mathf.Max(b.yMax, c.yMax)) : c;
                        any = true;
                        continue;
                    }
                    if (r.rectCount >= MaxRects)
                    {
                        Debug.LogWarning($"[Wonder] {def.wonderId}: en fazla {MaxRects} görev bölgesi; fazlası yok sayıldı.");
                        break;
                    }
                    r.rects[r.rectCount] = new Vector4(c.xMin, c.yMin, c.xMax, c.yMax);
                    r.owners[r.rectCount] = t;
                    r.rectCount++;
                    r.traceLengths[t] += 2f * (c.width + c.height);
                    b = any ? Rect.MinMaxRect(Mathf.Min(b.xMin, c.xMin), Mathf.Min(b.yMin, c.yMin),
                        Mathf.Max(b.xMax, c.xMax), Mathf.Max(b.yMax, c.yMax)) : c;
                    any = true;
                }
            r.bounds[t] = b;
        }
        return r;
    }

    static Rect Clamp01(Rect c) => Rect.MinMaxRect(Mathf.Clamp01(c.xMin), Mathf.Clamp01(c.yMin),
        Mathf.Clamp01(c.xMax), Mathf.Clamp01(c.yMax));

    public int TaskCount => bounds.Length;

    public bool HasOwnedSurface(int task) => task >= 0 && task < traceLengths.Length && traceLengths[task] > 0f;


    public WonderTaskAction Action(int task) =>
        task >= 0 && task < actions.Length ? actions[task] : WonderTaskAction.Repair;

    /// Görevin bölgelerini saran dikdörtgen (normalize).
    public Rect Bounds(int task) => task >= 0 && task < bounds.Length ? bounds[task] : new Rect(0f, 0f, 1f, 1f);

    public Vector4 BoundsVector(int task)
    {
        Rect b = Bounds(task);
        return new Vector4(b.xMin, b.yMin, b.xMax, b.yMax);
    }

    public Vector2 SurfacePoint(int task, float u, float v)
    {
        Rect b = Bounds(task);
        return new Vector2(Mathf.Lerp(b.xMin, b.xMax, u), Mathf.Lerp(b.yMin, b.yMax, v));
    }

    /// Kaynak torcunun bölge çevresindeki konumu (WonderRestorationLayout.Point ile aynı tur sırası).
    public Vector2 Point(int task, float progress)
        => TracePoint(task, progress, out _, out _);

    /// Trace each authored region separately. Distant piers/minarets must not turn
    /// into one large rectangle across the sky. The shader receives this same index.
    public Vector2 TracePoint(int task, float progress, out int rectangle, out float localProgress)
    {
        rectangle = -1;
        localProgress = Mathf.Clamp01(progress);
        Rect b = Bounds(task);
        if (task >= 0 && task < traceLengths.Length && traceLengths[task] > 0f)
        {
            float distance = localProgress * traceLengths[task];
            for (int i = 0; i < rectCount; i++)
            {
                if ((int)owners[i] != task) continue;
                Vector4 r = rects[i];
                float length = 2f * (r.z - r.x + r.w - r.y);
                // Retain the last region at p=1 despite floating point rounding.
                rectangle = i;
                b = Rect.MinMaxRect(r.x, r.y, r.z, r.w);
                localProgress = Mathf.Clamp01(distance / length);
                if (distance <= length) break;
                distance -= length;
            }
        }
        float phase = localProgress * 4f;
        float u, v;
        if (phase < 1f) { u = phase; v = 0f; }
        else if (phase < 2f) { u = 1f; v = phase - 1f; }
        else if (phase < 3f) { u = 3f - phase; v = 1f; }
        else { u = 0f; v = 4f - phase; }
        return new Vector2(Mathf.Lerp(b.xMin, b.xMax, u), Mathf.Lerp(b.yMin, b.yMax, v));
    }

    public void ApplyTo(Material m)
    {
        if (m == null) return;
        m.SetVectorArray(TaskRectsId, rects);
        m.SetFloatArray(TaskRectOwnersId, owners);
        m.SetFloat(TaskRectCountId, rectCount);
    }
}
