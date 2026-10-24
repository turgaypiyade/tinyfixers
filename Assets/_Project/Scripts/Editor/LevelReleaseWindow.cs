#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Operator UI for level_release.py; never invokes a Unity build.</summary>
public sealed class LevelReleaseWindow : EditorWindow
{
    private string releaseId;
    private string rollbackRevision = "";
    private string resultPath;
    private string status = "";
    private Process running;
    private Vector2 scroll;
    private double nextPoll;

    private static string Root => Directory.GetParent(Application.dataPath).FullName;

    [MenuItem("TinyFixers/Yayın/Level Yayın Aracı")]
    private static void Open() => GetWindow<LevelReleaseWindow>("Level Yayını");

    private void OnEnable()
    {
        releaseId ??= Application.version + "-b" + PlayerSettings.Android.bundleVersionCode;
        Run("status");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Uygulama sürümü → level güncellemeleri", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("İlk mağaza sürümünden önce oyunun uzaktan level okuma desteği de tamamlanmalıdır. " +
            "Bu pencere sürüm kaydı, değişiklik kontrolü ve Cloudflare yayınını yönetir; uygulama derlemez.", MessageType.Info);

        using (new EditorGUI.DisabledScope(running != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            releaseId = EditorGUILayout.TextField("Uygulama release kimliği", releaseId);
            EditorGUILayout.HelpBox("“Bu ilk release” kaydı, şu an diskteki kodu, gömülü asset'leri ve katalogdaki levelleri " +
                "referans alır. Yalnız gerçek mağaza sürümünü temsil eden projede kullan.", MessageType.None);
            if (GUILayout.Button("Bu uygulama release'ini kaydet"))
                Run("mark-release", "--release", releaseId);
            if (GUILayout.Button("Yeni / değişen levelleri hazırla")) Run("prepare");
            if (GUILayout.Button("Hazırlanan levelleri Cloudflare'a gönder")) Run("publish", "--apply");
            if (GUILayout.Button("Yayın sonucunu doğrula")) Run("verify-publication");
            if (GUILayout.Button("Durumu yenile")) Run("status");
            EditorGUILayout.Space();
            rollbackRevision = EditorGUILayout.TextField("Geri dönülecek revizyon", rollbackRevision);
            if (GUILayout.Button("Bu revizyona geri dönüşü incele")) Run("rollback", "--revision", rollbackRevision);
            if (GUILayout.Button("Bu revizyona Cloudflare'da geri dön")) Run("rollback", "--revision", rollbackRevision, "--apply");
        }
        if (running != null) EditorGUILayout.LabelField("İşlem sürüyor…");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(status, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Run(params string[] command)
    {
        if (running != null) return;
        if (command[0] != "status" && command[0] != "verify-publication")
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    status = "Önce açık sahnelerdeki değişiklikleri kaydet; sürüm kaydına yalnız diskteki dosyalar girer.";
                    return;
                }
            AssetDatabase.SaveAssets();
        }
        var python = new[] { "/opt/homebrew/bin/python3", "/usr/local/bin/python3", "/usr/bin/python3" }
            .FirstOrDefault(File.Exists) ?? "python3";
        resultPath = Path.Combine(Root, "Exports/LevelReleases/editor-jobs", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
        var arguments = new[] { Path.Combine(Root, "Tools/level_release.py"), "--result-file", resultPath }.Concat(command);
        var start = new ProcessStartInfo
        {
            FileName = python, Arguments = string.Join(" ", arguments.Select(Quote)), WorkingDirectory = Root,
            UseShellExecute = false, CreateNoWindow = true
        };
        try
        {
            running = Process.Start(start);
            status = "İşlem: " + string.Join(" ", command);
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }
        catch (Exception error)
        {
            status = "Araç başlatılamadı: " + error.Message;
            running?.Dispose();
            running = null;
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.15;
        if (running == null || !running.HasExited) return;
        status = File.Exists(resultPath) ? File.ReadAllText(resultPath) : "Araç tamamlandı; sonuç dosyası yok. Konsolu kontrol et.";
        running.Dispose();
        running = null;
        EditorApplication.update -= Poll;
        Repaint();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Poll;
        // A window closing must not kill a deployment midway. The independent CLI records/verifies its own result.
        running?.Dispose();
        running = null;
    }
}
#endif
