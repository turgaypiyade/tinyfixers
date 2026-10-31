#if UNITY_EDITOR && UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;

/// <summary>
/// Facebook 19 ships dynamic vendored XCFrameworks even with static CocoaPods linkage.
/// Linkage from UnityFramework is not enough: the app must embed and sign the binaries.
/// Use CocoaPods' selected slice, so device/simulator/architecture selection stays with Pods.
/// </summary>
public static class IosFacebookFrameworkEmbedPostProcess
{
    private static readonly string[] FrameworkNames =
    {
        "FBAEMKit", "FBSDKCoreKit", "FBSDKCoreKit_Basics",
        "FBSDKGamingServicesKit", "FBSDKLoginKit", "FBSDKShareKit"
    };

    // After pod install (50), after our other Xcode settings hooks, before plist-only auth hook.
    [PostProcessBuild(1006)]
    public static void OnPostprocessBuild(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        string path = PBXProject.GetPBXProjectPath(output);
        var project = new PBXProject();
        project.ReadFromFile(path);
        if (Configure(project, output) > 0) File.WriteAllText(path, project.WriteToString());
    }

    public static int Configure(PBXProject project, string output)
    {
        string pods = Path.Combine(output, "Pods");
        // A project without these Facebook Pods needs no extra embedding.
        if (!Directory.Exists(Path.Combine(pods, "FBAEMKit"))) return 0;
        string app = project.GetUnityMainTargetGuid();
        if (string.IsNullOrEmpty(app))
            throw new BuildFailedException("Facebook framework embedding requires the Unity main app target.");

        // Check the full native dependency set before editing any build phase.
        foreach (string name in FrameworkNames)
            if (!Directory.Exists(Path.Combine(pods, name, "XCFrameworks", name + ".xcframework")))
                throw new BuildFailedException("Facebook native dependency is missing: " + name + ". Run iOS dependency resolution before building.");

        // Unity's embed extension replaces LD_RUNPATH_SEARCH_PATHS. Preserve each
        // app configuration's existing inherited/custom paths before using it.
        var runpaths = new Dictionary<string, string>();
        foreach (string name in project.BuildConfigNames())
        {
            string config = project.BuildConfigByName(app, name);
            if (!string.IsNullOrEmpty(config))
                runpaths[config] = project.GetBuildPropertyForConfig(config, "LD_RUNPATH_SEARCH_PATHS");
        }

        foreach (string name in FrameworkNames)
        {
            string projectPath = "Frameworks/TinyFixersFacebook/" + name + ".framework";
            string file = project.FindFileGuidByProjectPath(projectPath);
            if (string.IsNullOrEmpty(file))
            {
                string builtSlice = "$(PODS_XCFRAMEWORKS_BUILD_DIR)/" + name + "/" + name + ".framework";
                file = project.AddFile(builtSlice, projectPath, PBXSourceTree.Source);
            }
            // Keep exactly one app embed entry on repeated exports. Do not link these
            // again into the app: UnityFramework already owns the native SDK linkage.
            project.RemoveFileFromBuild(app, file);
            PBXProjectExtensions.AddFileToEmbedFrameworks(project, app, file);
        }
        foreach (var entry in runpaths)
        {
            // GetBuildPropertyForConfig joins list values into a string; appending
            // through UpdateBuildProperty would duplicate that joined list each export.
            string paths = string.IsNullOrWhiteSpace(entry.Value) ? "$(inherited)" : entry.Value;
            if (paths.IndexOf("@executable_path/Frameworks", StringComparison.Ordinal) < 0)
                paths += " @executable_path/Frameworks";
            project.SetBuildPropertyForConfig(entry.Key, "LD_RUNPATH_SEARCH_PATHS", paths);
        }
        project.SetBuildProperty(app, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        return FrameworkNames.Length;
    }
}
#endif
