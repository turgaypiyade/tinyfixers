#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Xcode 15+ "User Script Sandboxing" (ENABLE_USER_SCRIPT_SANDBOXING=YES) Firebase Crashlytics'in
/// run script'ini (process_symbols.sh) engelliyor: "Sandbox: bash deny file-read-data".
/// Aynı şekilde Module Verifier (ENABLE_MODULE_VERIFIER) Unity header'larında hata verir.
/// Unity Xcode projesini her export'ta yeniden ürettiği için ayarlar burada kalıcı olarak kapatılır.
/// </summary>
public static class IosScriptSandboxingPostProcess
{
    [PostProcessBuild(1000)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);

        foreach (string guid in new[]
                 {
                     project.ProjectGuid(),
                     project.GetUnityMainTargetGuid(),
                     project.GetUnityFrameworkTargetGuid(),
                 })
        {
            if (string.IsNullOrEmpty(guid)) continue;
            project.SetBuildProperty(guid, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
            // Yeni Xcode, framework header'larını "VerifyModule" adımında ayrıca derler; Unity'nin
            // UnityFramework header'ları buna uygun değil ("double-quoted include in framework
            // header", "umbrella header does not include ..."). Asıl derlemeyi etkilemez, kapatılır.
            project.SetBuildProperty(guid, "ENABLE_MODULE_VERIFIER", "NO");
        }

        File.WriteAllText(projectPath, project.WriteToString());
    }
}
#endif
