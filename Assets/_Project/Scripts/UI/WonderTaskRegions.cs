using UnityEngine;

/// <summary>
/// Görev → resim bölgesi eşlemesi (WonderTask.regions). Görev metni neyi söylüyorsa (kubbe, minare, meydan) restorasyon
/// tam orayı açar. Aynı sahiplik kuralı UI_WonderReveal.shader'da (taskOwner): noktayı içeren EN KÜÇÜK dikdörtgen kazanır,
/// hiçbirine girmeyen nokta "kalan alan" görevine düşer. Harika başına bir kez kurulur; animasyonda tahsis yok.
/// [[project_wonder_reveal_background]]
/// </summary>
public sealed class WonderTaskRegions
{
    public const int MaxRects = 24;   // shader dizi boyutu ile aynı

    static readonly int TaskRectsId = Shader.PropertyToID("_TaskRects");
    static readonly int TaskRectOwnersId = Shader.PropertyToID("_TaskRectOwners");
    static readonly int TaskRectCountId = Shader.PropertyToID("_TaskRectCount");
    static readonly int RemainderTaskId = Shader.PropertyToID("_RemainderTask");

    readonly Vector4[] rects = new Vector4[MaxRects];   // xMin, yMin, xMax, yMax
    readonly float[] owners = new float[MaxRects];
    readonly Rect[] bounds;
    readonly WonderTaskAction[] actions;
    int rectCount;
    int remainderTask;

    WonderTaskRegions(int taskCount)
    {
        bounds = new Rect[taskCount];
        actions = new WonderTaskAction[taskCount];
    }

    /// Bölge tanımlı değilse null → görünüm eski eğri parça düzenini kullanır.
    public static WonderTaskRegions From(WonderDefinition def)
    {
        if (def == null || !def.HasTaskRegions) return null;
        var tasks = def.tasks;
        var r = new WonderTaskRegions(tasks.Length);
        r.remainderTask = tasks.Length - 1;
        for (int t = tasks.Length - 1; t >= 0; t--)
            if (tasks[t] == null || tasks[t].regions == null || tasks[t].regions.Length == 0) { r.remainderTask = t; break; }

        for (int t = 0; t < tasks.Length; t++)
        {
            var task = tasks[t];
            r.actions[t] = task != null ? task.action : WonderTaskAction.Repair;
            bool any = false;
            Rect b = new Rect(0f, 0f, 1f, 1f);
            bool owns = r.actions[t] != WonderTaskAction.Shine;   // parlama bölge sahiplenmez, yalnız üstünden geçer
            if (task != null && task.regions != null && t != r.remainderTask)
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
                    b = any ? Rect.MinMaxRect(Mathf.Min(b.xMin, c.xMin), Mathf.Min(b.yMin, c.yMin),
                        Mathf.Max(b.xMax, c.xMax), Mathf.Max(b.yMax, c.yMax)) : c;
                    any = true;
                }
            r.bounds[t] = b;   // kalan alan / bölgesiz görev: tüm resim (sahiplik maskesi kırpar)
        }
        return r;
    }

    static Rect Clamp01(Rect c) => Rect.MinMaxRect(Mathf.Clamp01(c.xMin), Mathf.Clamp01(c.yMin),
        Mathf.Clamp01(c.xMax), Mathf.Clamp01(c.yMax));

    public int TaskCount => bounds.Length;

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
    {
        float phase = Mathf.Clamp01(progress) * 4f;
        float u, v;
        if (phase < 1f) { u = phase; v = 0f; }
        else if (phase < 2f) { u = 1f; v = phase - 1f; }
        else if (phase < 3f) { u = 3f - phase; v = 1f; }
        else { u = 0f; v = 4f - phase; }
        return SurfacePoint(task, u, v);
    }

    public void ApplyTo(Material m)
    {
        if (m == null) return;
        m.SetVectorArray(TaskRectsId, rects);
        m.SetFloatArray(TaskRectOwnersId, owners);
        m.SetFloat(TaskRectCountId, rectCount);
        m.SetFloat(RemainderTaskId, remainderTask);
    }
}
