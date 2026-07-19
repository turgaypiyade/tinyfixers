#if UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS
#define UNITY_XCODE_EXTENSIONS_AVAILABLE
#endif

using System;
using AppleAuth.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_XCODE_EXTENSIONS_AVAILABLE
using UnityEditor.iOS.Xcode;
#endif

namespace AppleAuthSample.Editor
{
    public class SignInWithApplePostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1;
        public void OnPostprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            var path = report.summary.outputPath;
                
            // Sign in with Apple capability yalnız TF_APPLE_SIGNIN_CAPABILITY define'ı
            // AÇIKKEN eklenir. Ücretsiz/kişisel Apple hesabı bu capability'yi PROVISION
            // EDEMEZ → kabloyla local test bile bozulur. PARALI Developer Program alınca
            // Player Settings > Scripting Define Symbols'a TF_APPLE_SIGNIN_CAPABILITY ekle.
            if (target == BuildTarget.iOS || target == BuildTarget.tvOS)
            {
                #if UNITY_XCODE_EXTENSIONS_AVAILABLE && TF_APPLE_SIGNIN_CAPABILITY
                    var projectPath = PBXProject.GetPBXProjectPath(path);
                    var project = new PBXProject();
                    project.ReadFromString(System.IO.File.ReadAllText(projectPath));
                    var manager = new ProjectCapabilityManager(projectPath, "Entitlements.entitlements", null, project.GetUnityMainTargetGuid());
                    manager.AddSignInWithAppleWithCompatibility();
                    manager.WriteToFile();
                #endif
            }
            else if (target == BuildTarget.StandaloneOSX)
            {
                try
                {
                    AppleAuthMacosPostprocessorHelper.FixManagerBundleIdentifier(target, path);
                }
                catch (Exception exception)
                {
                    throw new BuildFailedException(exception);
                }
            }

           #if UNITY_2022_3_OR_NEWER
                if (target == BuildTarget.VisionOS)
                {
                    #if UNITY_XCODE_EXTENSIONS_AVAILABLE && TF_APPLE_SIGNIN_CAPABILITY
                    var projectPath = PBXProject.GetPBXProjectPath(path);

                    // This is a temporary fix for the Unity Editor's bug:
                    // After switch to VisionOS platform the projectPath is still "xx/Unity-iPhone.xcodeproj/project.pbxproj",
                    // while the expected path is "xx/Unity-VisionOS.xcodeproj/project.pbxproj",
                    projectPath = projectPath.Replace("Unity-iPhone.xcodeproj", "Unity-VisionOS.xcodeproj");

                    var project = new PBXProject();
                    project.ReadFromString(System.IO.File.ReadAllText(projectPath));
                    var manager = new ProjectCapabilityManager(projectPath, "Entitlements.entitlements", null, project.GetUnityMainTargetGuid());
                    manager.AddSignInWithAppleWithCompatibility();
                    manager.WriteToFile();
                    #endif
                }
            #endif
        }
    }
}
