using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Perf Probe menüsü: kayıt aç/kapa + UI denetim anlık görüntüsü (Play modunda). Çıktılar proje kökünde PerfLogs/.
/// UI denetimi: Canvas başına görsel sayısı, farklı doku/malzeme (batch bölen), tıklanabilir görsel, Pixel Perfect,
/// iç içe Canvas'lar, en çok kullanılan ve en büyük dokular. Salt okuma; sahneyi değiştirmez.
/// </summary>
public static class PerfProbeMenu
{
    private const string Root = "TinyFixers/Debug/Perf Probe/";

    [MenuItem(Root + "Kaydı Başlat (Play)")]
    private static void StartRec() => PerfProbe.Start();
    [MenuItem(Root + "Kaydı Başlat (Play)", true)]
    private static bool CanStart() => Application.isPlaying && !PerfProbe.IsRecording;

    [MenuItem(Root + "Kaydı Durdur (Play)")]
    private static void StopRec() => PerfProbe.Stop();
    [MenuItem(Root + "Kaydı Durdur (Play)", true)]
    private static bool CanStop() => Application.isPlaying && PerfProbe.IsRecording;

    [MenuItem(Root + "UI Denetim Anlık Görüntüsü (Play)")]
    private static void Audit()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# UI denetimi — {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} · seviye {CurrentLevel.Global}");
        sb.AppendLine();
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        sb.AppendLine("| Canvas | kök mü | render | pixelPerfect | override sıralama | aktif görsel | raycastTarget | farklı doku | farklı malzeme | iç Canvas |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        var textureUse = new Dictionary<Texture, int>();
        foreach (var c in canvases.OrderByDescending(c => c.GetComponentsInChildren<Graphic>(false).Length))
        {
            // Bu Canvas'ın kendi batch'ine giren görseller: alt Canvas'larınkiler hariç.
            var own = c.GetComponentsInChildren<Graphic>(false).Where(g => g.canvas == c && g.enabled).ToList();
            int ray = own.Count(g => g.raycastTarget);
            var tex = new HashSet<Texture>(); var mats = new HashSet<Material>();
            foreach (var g in own)
            {
                var t = g.mainTexture; if (t != null) { tex.Add(t); textureUse[t] = textureUse.TryGetValue(t, out int n) ? n + 1 : 1; }
                if (g.materialForRendering != null) mats.Add(g.materialForRendering);
            }
            int nested = c.GetComponentsInChildren<Canvas>(false).Count(x => x != c);
            sb.AppendLine($"| {Path(c.transform)} | {(c.isRootCanvas ? "evet" : "hayır")} | {c.renderMode} | {c.pixelPerfect} | {c.overrideSorting} | {own.Count} | {ray} | {tex.Count} | {mats.Count} | {nested} |");
        }
        sb.AppendLine();
        sb.AppendLine("## En çok kullanılan dokular (batch'i bölen her farklı doku ayrı çizim demek)");
        sb.AppendLine("| doku | boyut | kullanım |");
        sb.AppendLine("|---|---|---|");
        foreach (var kv in textureUse.OrderByDescending(k => k.Value).Take(40))
            sb.AppendLine($"| {kv.Key.name} | {kv.Key.width}×{kv.Key.height} | {kv.Value} |");
        sb.AppendLine();
        sb.AppendLine("## Büyük dokular (≥1024 px, ekranda aktif)");
        foreach (var t in textureUse.Keys.Where(t => t.width >= 1024 || t.height >= 1024).OrderByDescending(t => t.width * t.height))
            sb.AppendLine($"- {t.name} {t.width}×{t.height} ×{textureUse[t]}");

        // Görünmez ama etkin görseller: alpha≈0 iken Canvas yine de her yeniden kurulumda işler (gizlemek için
        // enabled=false / SetActive(false) gerekir). Sprite'sız (UnityWhite) görseller: düz renk zemin/çizgi/vuruş alanı.
        var all = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(g => g.enabled).ToList();
        void Group(string title, IEnumerable<Graphic> list)
        {
            var groups = list.GroupBy(g => $"{(g.transform.parent != null ? g.transform.parent.name + "/" : "")}{g.name} [{g.GetType().Name}]")
                             .OrderByDescending(x => x.Count()).Take(30).ToList();
            sb.AppendLine();
            sb.AppendLine($"## {title} — toplam {list.Count()}");
            sb.AppendLine("| ebeveyn/nesne [tür] | adet |");
            sb.AppendLine("|---|---|");
            foreach (var x in groups) sb.AppendLine($"| {x.Key} | {x.Count()} |");
        }
        Group("Görünmez ama etkin görseller (alpha ≤ 0.01)", all.Where(g => g.color.a <= 0.01f || g.canvasRenderer.GetAlpha() <= 0.01f));
        Group("Sprite'sız (UnityWhite) görseller", all.Where(g => g.mainTexture != null && g.mainTexture.name == "UnityWhite"));
        Group("Tıklanabilir (raycastTarget) görseller", all.Where(g => g.raycastTarget));

        string dir = System.IO.Path.Combine(Directory.GetParent(Application.dataPath).FullName, "PerfLogs");
        Directory.CreateDirectory(dir);
        string file = System.IO.Path.Combine(dir, $"ui_audit_{System.DateTime.Now:yyyyMMdd_HHmmss}.md");
        File.WriteAllText(file, sb.ToString());
        Debug.Log($"[PerfProbe] UI denetimi yazıldı → {file}");
    }
    [MenuItem(Root + "UI Denetim Anlık Görüntüsü (Play)", true)]
    private static bool CanAudit() => Application.isPlaying;

    private static string Path(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null && parts.Count < 4; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
