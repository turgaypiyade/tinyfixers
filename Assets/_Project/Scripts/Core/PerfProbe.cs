using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
using UnityEngine.UI;
#endif

/// <summary>
/// Performans ölçüm kaydedici (yalnız Editör ve Development Build; sürüm build'inde boş gövde, maliyet sıfır).
/// Her kare Unity'nin kendi sayaçlarını okur (kare/ana iş parçacığı süresi, Canvas kurulumu, UGUI batch, script
/// güncellemeleri, coroutine, GC ayırma, çizim çağrısı/batch/SetPass/vertex) + tahtanın o anki durumunu (boşta / akış /
/// special / arka plan işi). Saniyelik özet + takılma kareleri CSV'ye; 5 sn'de bir kısa "[PerfProbe]" özeti konsola.
/// Açma: GameDiagnostics.Performance ya da editör menüsü TinyFixers ▸ Debug ▸ Perf Probe.
/// Dosya: Editörde proje kökünde PerfLogs/, cihazda Application.persistentDataPath/PerfLogs/.
/// Yeniden kurulum sayacı: her kare UGUI'nin yeniden kurulum kuyruğunu (CanvasUpdateRegistry) okur; hangi görselin kaç
/// karede yeniden kurulduğunu sayar (renk/sprite/boyut değişimi = yeniden kurulum; taşıma/ölçek/döndürme değil).
/// Kayıt durunca rebuild_*.md'ye en çok yeniden kurulan nesne grupları yazılır.
/// </summary>
[DefaultExecutionOrder(32000)]   // kuyruğu karenin son LateUpdate'inde, Canvas kurulumundan hemen önce oku
public sealed class PerfProbe : MonoBehaviour
{
    public static bool IsRecording => instance != null;
    public static string CurrentPath => instance != null ? instance.path : null;

    private static PerfProbe instance;
    private string path;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const float SpikeFrameMs = 33.4f;   // 30 fps altı kare
    private const long SpikeGcBytes = 64 * 1024;
    private const float SummaryLogSeconds = 5f;

    private struct Rec
    {
        public string column;
        public ProfilerRecorder recorder;
        public bool isTime;          // ns → ms
        public double sum, max;
    }

    private Rec[] recs;
    private StreamWriter writer;
    private readonly StringBuilder line = new StringBuilder(512);
    private float windowStart, lastSummaryLog;
    private int frames, busyFrames, specialFrames, seqFrames;
    private double frameSum, frameMax;
    private int jobsMax;
    private int spikes;
    private BoardController board;
    private float nextBoardLookup;

    // UGUI yeniden kurulum kuyrukları (iç alan; yansıma ile bir kez alınır, okuma ayırmasız indeksle).
    private IList<ICanvasElement> graphicQueue, layoutQueue;
    private readonly Dictionary<UnityEngine.Object, int> graphicRebuilds = new Dictionary<UnityEngine.Object, int>(1024);
    private readonly Dictionary<UnityEngine.Object, int> layoutRebuilds = new Dictionary<UnityEngine.Object, int>(256);
    private int graphicQueueMax, layoutQueueMax, graphicQueueFrameMax;
    private long graphicQueueSum;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoStart()
    {
        if (GameDiagnostics.Performance) Start();
    }

    public static void Start()
    {
        if (instance != null) return;
        var go = new GameObject("PerfProbe");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PerfProbe>();
    }

    public static void Stop()
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
        instance = null;
    }

    private void OnEnable()
    {
        recs = new[]
        {
            Time("mainMs",        ProfilerCategory.Internal, "Main Thread"),
            Time("canvasBuildMs", ProfilerCategory.Gui,      "Canvas.BuildBatch"),
            Time("willRenderMs",  ProfilerCategory.Gui,      "Canvas.SendWillRenderCanvases"),
            Time("uguiBatchMs",   ProfilerCategory.Gui,      "UGUI.Rendering.UpdateBatches"),
            Time("canvasUpdMs",   ProfilerCategory.Gui,      "PostLateUpdate.PlayerUpdateCanvases"),
            Time("scriptUpdMs",   ProfilerCategory.Scripts,  "Update.ScriptRunBehaviourUpdate"),
            Time("scriptLateMs",  ProfilerCategory.Scripts,  "PreLateUpdate.ScriptRunBehaviourLateUpdate"),
            Time("coroutineMs",   ProfilerCategory.Scripts,  "Update.ScriptRunDelayedDynamicFrameRate"),
            Time("gcCollectMs",   ProfilerCategory.Memory,   "GC.Collect"),
            Count("gcAllocBytes", ProfilerCategory.Memory,   "GC Allocated In Frame"),
            Count("gcAllocCount", ProfilerCategory.Memory,   "GC Allocation In Frame Count"),
            Count("drawCalls",    ProfilerCategory.Render,   "Draw Calls Count"),
            Count("batches",      ProfilerCategory.Render,   "Batches Count"),
            Count("setPass",      ProfilerCategory.Render,   "SetPass Calls Count"),
            Count("vertices",     ProfilerCategory.Render,   "Vertices Count"),
        };

        try
        {
            var reg = CanvasUpdateRegistry.instance;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            graphicQueue = typeof(CanvasUpdateRegistry).GetField("m_GraphicRebuildQueue", flags)?.GetValue(reg) as IList<ICanvasElement>;
            layoutQueue = typeof(CanvasUpdateRegistry).GetField("m_LayoutRebuildQueue", flags)?.GetValue(reg) as IList<ICanvasElement>;
        }
        catch (Exception e) { Debug.LogWarning($"[PerfProbe] yeniden kurulum kuyruğu okunamadı: {e.Message}"); }
        graphicRebuilds.Clear(); layoutRebuilds.Clear();
        graphicQueueFrameMax = 0; graphicQueueSum = 0;

        string dir;
#if UNITY_EDITOR
        dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "PerfLogs");
#else
        dir = Path.Combine(Application.persistentDataPath, "PerfLogs");
#endif
        Directory.CreateDirectory(dir);
        path = Path.Combine(dir, $"perf_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        writer = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = false };
        line.Clear().Append("type,t,level,frames,fps,frameAvgMs,frameMaxMs");
        foreach (var r in recs) line.Append(',').Append(r.column).Append("Avg,").Append(r.column).Append("Max");
        line.Append(",busyPct,specialPct,sequencePct,jobsMax,state,valid");
        writer.WriteLine(line.ToString());
        var valid = new StringBuilder();
        foreach (var r in recs) if (!r.recorder.Valid) valid.Append(r.column).Append(' ');
        Debug.Log($"[PerfProbe] kayıt başladı → {path}" + (valid.Length > 0 ? $" (bu platformda okunamayan sayaçlar: {valid})" : ""));
        windowStart = lastSummaryLog = UnityEngine.Time.realtimeSinceStartup;
        ResetWindow();
    }

    private static Rec Time(string col, ProfilerCategory cat, string name) =>
        new Rec { column = col, recorder = ProfilerRecorder.StartNew(cat, name, 1), isTime = true };
    private static Rec Count(string col, ProfilerCategory cat, string name) =>
        new Rec { column = col, recorder = ProfilerRecorder.StartNew(cat, name, 1), isTime = false };

    private void OnDisable()
    {
        if (frames > 0) WriteRow("window");
        WriteRebuildReport();
        writer?.Flush();
        writer?.Dispose();
        writer = null;
        if (recs != null) for (int i = 0; i < recs.Length; i++) recs[i].recorder.Dispose();
        recs = null;
        Debug.Log($"[PerfProbe] kayıt bitti → {path}");
        if (instance == this) instance = null;
    }

    private void ResetWindow()
    {
        frames = busyFrames = specialFrames = seqFrames = jobsMax = 0;
        graphicQueueMax = layoutQueueMax = 0;
        frameSum = frameMax = 0;
        for (int i = 0; i < recs.Length; i++) { recs[i].sum = 0; recs[i].max = 0; }
    }

    private void LateUpdate()
    {
        if (writer == null) return;
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (board == null && now >= nextBoardLookup)
        {
            nextBoardLookup = now + 2f;
            board = FindFirstObjectByType<BoardController>();
        }

        SampleRebuildQueues();
        double frameMs = UnityEngine.Time.unscaledDeltaTime * 1000.0;
        frames++;
        frameSum += frameMs;
        if (frameMs > frameMax) frameMax = frameMs;
        for (int i = 0; i < recs.Length; i++)
        {
            if (!recs[i].recorder.Valid) continue;
            double v = recs[i].recorder.LastValue;
            if (recs[i].isTime) v *= 1e-6;
            recs[i].sum += v;
            if (v > recs[i].max) recs[i].max = v;
        }
        if (board != null)
        {
            if (board.IsBusy) busyFrames++;
            if (board.IsSpecialActivationPhase) specialFrames++;
            if (board.IsActionSequencePlaying) seqFrames++;
            if (board.ActiveBackgroundJobs > jobsMax) jobsMax = board.ActiveBackgroundJobs;
        }

        // Takılma karesi: o karenin değerleriyle ayrı satır (hangi alt sistemin şiştiği görünür).
        long gc = recs[9].recorder.Valid ? recs[9].recorder.LastValue : 0;
        if (frameMs >= SpikeFrameMs || gc >= SpikeGcBytes)
        {
            spikes++;
            WriteSpike(now, frameMs);
        }

        if (now - windowStart >= 1f)
        {
            WriteRow("window");
            windowStart = now;
            if (now - lastSummaryLog >= SummaryLogSeconds)
            {
                lastSummaryLog = now;
                writer.Flush();
            }
            ResetWindow();
        }
    }

    private string BoardStateText() =>
        board == null ? "noboard" : board.IsSpecialActivationPhase ? "special" : board.IsBusy ? "resolving"
        : board.ActiveBackgroundJobs > 0 ? "jobs" : "idle";

    private void WriteRow(string type)
    {
        float t = UnityEngine.Time.realtimeSinceStartup;
        double avg = frames > 0 ? frameSum / frames : 0;
        line.Clear().Append(type).Append(',').Append(t.ToString("0.00")).Append(',').Append(CurrentLevel.Global)
            .Append(',').Append(frames).Append(',').Append((avg > 0 ? 1000.0 / avg : 0).ToString("0.0"))
            .Append(',').Append(avg.ToString("0.00")).Append(',').Append(frameMax.ToString("0.00"));
        for (int i = 0; i < recs.Length; i++)
        {
            double a = frames > 0 ? recs[i].sum / frames : 0;
            line.Append(',').Append(recs[i].recorder.Valid ? a.ToString(recs[i].isTime ? "0.000" : "0") : "-1")
                .Append(',').Append(recs[i].recorder.Valid ? recs[i].max.ToString(recs[i].isTime ? "0.000" : "0") : "-1");
        }
        float f = Mathf.Max(1, frames);
        line.Append(',').Append((busyFrames * 100f / f).ToString("0")).Append(',').Append((specialFrames * 100f / f).ToString("0"))
            .Append(',').Append((seqFrames * 100f / f).ToString("0")).Append(',').Append(jobsMax).Append(',').Append(BoardStateText()).Append(",1");
        writer.WriteLine(line.ToString());

        if (t - lastSummaryLog >= SummaryLogSeconds - 0.01f)
            Debug.Log($"[PerfProbe] L{CurrentLevel.Global} fps={(avg > 0 ? 1000.0 / avg : 0):0} kare ort/maks={avg:0.0}/{frameMax:0.0}ms " +
                      $"canvasBuild={Avg(1):0.00}ms uguiBatch={Avg(3):0.00}ms script={Avg(5):0.00}ms gcKB/kare={Avg(9) / 1024:0.0} " +
                      $"draw={Avg(11):0} batch={Avg(12):0} yenidenKurulan(maks/kare) görsel={graphicQueueMax} layout={layoutQueueMax} " +
                      $"takılma(son pencere)={spikes} durum={BoardStateText()}");
        spikes = 0;
    }

    private void SampleRebuildQueues()
    {
        if (graphicQueue != null)
        {
            int n = graphicQueue.Count;
            if (n > graphicQueueMax) graphicQueueMax = n;
            if (n > graphicQueueFrameMax) graphicQueueFrameMax = n;
            graphicQueueSum += n;
            for (int i = 0; i < n; i++) Bump(graphicRebuilds, graphicQueue[i]);
        }
        if (layoutQueue != null)
        {
            int n = layoutQueue.Count;
            if (n > layoutQueueMax) layoutQueueMax = n;
            for (int i = 0; i < n; i++) Bump(layoutRebuilds, layoutQueue[i]);
        }
    }

    private static void Bump(Dictionary<UnityEngine.Object, int> map, ICanvasElement e)
    {
        if (e is not UnityEngine.Object o || o == null) return;
        map.TryGetValue(o, out int c);
        map[o] = c + 1;
    }

    // Grup = ebeveyn adı / nesne adı [tür]; havuzdan dönen aynı adlı nesneler tek satırda toplanır.
    private void WriteRebuildReport()
    {
        if (path == null || (graphicRebuilds.Count == 0 && layoutRebuilds.Count == 0)) return;
        var sb = new StringBuilder();
        sb.AppendLine($"# UGUI yeniden kurulum raporu — {DateTime.Now:yyyy-MM-dd HH:mm:ss} · seviye {CurrentLevel.Global}");
        sb.AppendLine();
        sb.AppendLine($"Kayıt: {frames} kare (son pencere) · tüm kayıtta tek karede en çok yeniden kurulan görsel: {graphicQueueFrameMax} · toplam görsel yeniden kurulumu: {graphicQueueSum}");
        sb.AppendLine("Adet = o nesnenin yeniden kurulduğu kare sayısı (renk/alpha-color/sprite/boyut/fillAmount değişimi). Taşıma/ölçek/döndürme sayılmaz.");
        AppendGroups(sb, "Görsel (Graphic) yeniden kurulumları", graphicRebuilds);
        AppendGroups(sb, "Layout yeniden kurulumları", layoutRebuilds);
        string file = Path.Combine(Path.GetDirectoryName(path), "rebuild_" + Path.GetFileNameWithoutExtension(path).Replace("perf_", "") + ".md");
        File.WriteAllText(file, sb.ToString());
        Debug.Log($"[PerfProbe] yeniden kurulum raporu → {file}");
    }

    private static void AppendGroups(StringBuilder sb, string title, Dictionary<UnityEngine.Object, int> map)
    {
        var groups = new Dictionary<string, (int total, int objects)>();
        long all = 0;
        foreach (var kv in map)
        {
            all += kv.Value;
            string key;
            if (kv.Key is Component c && c != null)
                key = $"{(c.transform.parent != null ? c.transform.parent.name + "/" : "")}{c.name} [{c.GetType().Name}]";
            else key = "(yok edilmiş nesne)";
            groups.TryGetValue(key, out var g);
            groups[key] = (g.total + kv.Value, g.objects + 1);
        }
        var list = new List<KeyValuePair<string, (int total, int objects)>>(groups);
        list.Sort((a, b) => b.Value.total.CompareTo(a.Value.total));
        sb.AppendLine();
        sb.AppendLine($"## {title} — toplam {all}");
        sb.AppendLine("| ebeveyn/nesne [tür] | yeniden kurulum | farklı nesne | pay |");
        sb.AppendLine("|---|---|---|---|");
        for (int i = 0; i < list.Count && i < 40; i++)
            sb.AppendLine($"| {list[i].Key} | {list[i].Value.total} | {list[i].Value.objects} | {(all > 0 ? list[i].Value.total * 100.0 / all : 0):0.0}% |");
    }

    private double Avg(int i) => frames > 0 && recs[i].recorder.Valid ? recs[i].sum / frames * 1.0 : -1;

    private void WriteSpike(float t, double frameMs)
    {
        line.Clear().Append("spike,").Append(t.ToString("0.000")).Append(',').Append(CurrentLevel.Global).Append(",1,")
            .Append((1000.0 / Math.Max(0.001, frameMs)).ToString("0.0")).Append(',').Append(frameMs.ToString("0.00")).Append(',').Append(frameMs.ToString("0.00"));
        for (int i = 0; i < recs.Length; i++)
        {
            string v = "-1";
            if (recs[i].recorder.Valid)
            {
                double x = recs[i].recorder.LastValue;
                v = recs[i].isTime ? (x * 1e-6).ToString("0.000") : x.ToString("0");
            }
            line.Append(',').Append(v).Append(',').Append(v);
        }
        line.Append(",,,,").Append(board != null ? board.ActiveBackgroundJobs : 0).Append(',').Append(BoardStateText()).Append(",1");
        writer.WriteLine(line.ToString());
    }
#else
    public static void Start() { }
    public static void Stop() { }
#endif
}
