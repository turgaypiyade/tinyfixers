using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play modunda sahnelerde silinmiş bir assete (Missing) bağlı kalan alanları bulur. Config çalışma anında alanı
/// ezdiyse raporlanmaz: bir alan ancak art arda iki taramada hâlâ eksikse yazılır. Yalnız aktif objeler taranır,
/// ekranları gezdikçe liste dolar. Çıkışta yüklenmiş _Project config'lerindeki (ScriptableObject) eksik alanlar da
/// eklenir. Rapor: Logs/MissingRefsReport.txt. Menüden açılıp kapanır; kapalıyken hiçbir şey yapmaz.
/// </summary>
internal static class MissingRefProbe
{
    private const string MenuPath = "TinyFixers/Debug/Kayıp Referans Taraması (Play)";
    private const string PrefKey = "TinyFixers.MissingRefProbe";
    private const double ScanInterval = 3.0;
    private static readonly string ReportPath = Path.Combine("Logs", "MissingRefsReport.txt");

    private static readonly HashSet<string> PrimitiveArrayTypes = new()
    {
        "int", "unsigned int", "float", "double", "bool", "char", "string", "UInt8", "SInt8", "UInt16", "SInt16",
        "SInt64", "UInt64", "Vector2", "Vector3", "Vector4", "Vector2Int", "Vector3Int", "Quaternion", "Color",
    };

    private static HashSet<string> previousScan = new();
    private static HashSet<string> currentScan = new();
    private static readonly SortedSet<string> confirmed = new();
    private static double nextScan;

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, false);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [InitializeOnLoadMethod]
    private static void Init()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.delayCall += () => Menu.SetChecked(MenuPath, Enabled);
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Menu.SetChecked(MenuPath, Enabled);
        if (!EditorApplication.isPlaying) return;
        if (Enabled) Begin();
        else End();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!Enabled) return;
        if (state == PlayModeStateChange.EnteredPlayMode) Begin();
        else if (state == PlayModeStateChange.ExitingPlayMode) End();
    }

    private static void Begin()
    {
        previousScan.Clear();
        confirmed.Clear();
        nextScan = EditorApplication.timeSinceStartup + ScanInterval;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log($"[MissingRef] Tarama açık. Ekranları gez; Play'den çıkınca rapor: {ReportPath}");
    }

    private static void End()
    {
        EditorApplication.update -= Tick;
        WriteReport(ScanLoadedConfigs());
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextScan) return;
        nextScan = EditorApplication.timeSinceStartup + ScanInterval;
        ScanScenes();
    }

    private static void ScanScenes()
    {
        currentScan.Clear();
        foreach (var c in Object.FindObjectsByType<Component>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c == null) continue;
            string where = null;
            foreach (string prop in MissingProps(c))
            {
                where ??= Describe(c);
                string key = $"{where}.{prop}";
                currentScan.Add(key);
                // İlk görüldüğü taramada config henüz ezmemiş olabilir; ikinci taramada hâlâ eksikse gerçek.
                if (previousScan.Contains(key) && confirmed.Add(key))
                    Debug.LogWarning($"[MissingRef] {key}", c);
            }
        }
        (previousScan, currentScan) = (currentScan, previousScan);
    }

    private static List<string> ScanLoadedConfigs()
    {
        var found = new List<string>();
        foreach (var so in Resources.FindObjectsOfTypeAll<ScriptableObject>())
        {
            string path = AssetDatabase.GetAssetPath(so);
            if (!path.StartsWith("Assets/_Project/")) continue;
            foreach (string prop in MissingProps(so)) found.Add($"{path} :: {prop}");
        }
        found.Sort();
        return found;
    }

    private static IEnumerable<string> MissingProps(Object target)
    {
        using var so = new SerializedObject(target);
        var p = so.GetIterator();
        bool enter = true;
        while (p.Next(enter))
        {
#pragma warning disable CS0618
            if (p.propertyType == SerializedPropertyType.ObjectReference &&
                p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0)
                yield return p.propertyPath;
#pragma warning restore CS0618
            // Büyük sayı/vektör dizilerinin (level verisi vb.) içine girme: referans içermezler.
            enter = p.propertyType == SerializedPropertyType.Generic &&
                    !(p.isArray && PrimitiveArrayTypes.Contains(p.arrayElementType));
        }
    }

    private static string Describe(Component c)
    {
        var sb = new StringBuilder();
        for (var t = c.transform; t != null; t = t.parent) sb.Insert(0, "/" + t.name);
        return $"{c.gameObject.scene.name}{sb} [{c.GetType().Name}]";
    }

    private static void WriteReport(List<string> configs)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Kayıp referans raporu — {System.DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"\n=== Sahnede gerçekten boş kalan alanlar ({confirmed.Count}) ===");
        foreach (string k in confirmed) sb.AppendLine(k);
        sb.AppendLine($"\n=== Yüklenen config'lerde kayıp alan ({configs.Count}) — kod bu alanı kullanıyorsa boş görünür ===");
        foreach (string k in configs) sb.AppendLine(k);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
        Debug.Log($"[MissingRef] Rapor yazıldı: {ReportPath} (sahne {confirmed.Count}, config {configs.Count})");
    }
}
