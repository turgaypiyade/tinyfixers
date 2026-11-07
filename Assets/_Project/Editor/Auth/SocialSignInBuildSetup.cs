#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using Facebook.Unity.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using UnityEditor.Callbacks;
#endif

/// <summary>Uses local public client IDs; never reads the Meta App Secret or developer credentials.</summary>
public sealed class SocialSignInBuildSetup : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        string path = "Assets/_Project/Resources/AuthProviderConfig.local.json";
        if (!File.Exists(path)) return;
        var config = ReadConfig(path);
        FacebookSettings.AutoLogAppEventsEnabled = false;
        FacebookSettings.AutoLogProductImprovementEnabled = false;
        FacebookSettings.AutoLogMetaDataEnabled = false;
        FacebookSettings.AdvertiserIDCollectionEnabled = false;
        FacebookSettings.Logging = false;
        if (!string.IsNullOrWhiteSpace(config.facebookAppId) && !string.IsNullOrWhiteSpace(config.facebookClientToken))
        {
            FacebookSettings.AppIds = new List<string> { config.facebookAppId };
            FacebookSettings.AppLabels = new List<string> { "Wonder Fixers" };
            FacebookSettings.ClientTokens = new List<string> { config.facebookClientToken };
            FacebookSettings.SelectedAppIndex = 0;
        }
        EditorUtility.SetDirty(FacebookSettings.Instance);
        AssetDatabase.SaveAssets();
        if (report.summary.platform == BuildTarget.Android
            && !string.IsNullOrWhiteSpace(config.facebookAppId)
            && !string.IsNullOrWhiteSpace(config.facebookClientToken))
        {
            Facebook.Unity.Editor.ManifestMod.GenerateManifest();
            // The Facebook template hardcodes debuggable=true; let Unity's build type own it.
            const string manifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
            var manifest = new XmlDocument();
            manifest.Load(manifestPath);
            var application = manifest.SelectSingleNode("/manifest/application") as XmlElement;
            if (application != null)
            {
                application.RemoveAttribute("debuggable", "http://schemas.android.com/apk/res/android");
                manifest.Save(manifestPath);
            }
            if (!Facebook.Unity.Editor.ManifestMod.CheckManifest())
                throw new BuildFailedException("Facebook Android manifest configuration is incomplete.");
        }
    }
    private static SocialSignInConfig ReadConfig(string path)
    {
        string json = File.ReadAllText(path);
        var allowed = new HashSet<string> { "googleIosClientId", "googleWebClientId", "facebookAppId", "facebookClientToken" };
        foreach (Match field in Regex.Matches(json, "\"([^\"]+)\"\\s*:"))
            if (!allowed.Contains(field.Groups[1].Value))
                throw new BuildFailedException("AuthProviderConfig.local.json may contain only public client configuration fields.");
        return JsonUtility.FromJson<SocialSignInConfig>(json)
            ?? throw new BuildFailedException("AuthProviderConfig.local.json is invalid.");
    }
#if UNITY_IOS
    [PostProcessBuild(2000)]
    private static void ConfigureIos(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        var plistPath = Path.Combine(output, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;
        string configPath = "Assets/_Project/Resources/AuthProviderConfig.local.json";
        if (!File.Exists(configPath)) return;
        var config = ReadConfig(configPath);
        root.SetBoolean("FacebookAutoLogAppEventsEnabled", false);
        root.SetBoolean("FacebookAutoLogProductImprovementEnabled", false);
        root.SetBoolean("FacebookAutoLogMetaDataEnabled", false);
        root.SetBoolean("FacebookAdvertiserIDCollectionEnabled", false);
        if (SocialSignInConfig.ValidGoogleClient(config.googleIosClientId))
        {
            root.SetString("GIDClientID", config.googleIosClientId);
            if (SocialSignInConfig.ValidGoogleClient(config.googleWebClientId))
                root.SetString("GIDServerClientID", config.googleWebClientId);
            var types = root.values.ContainsKey("CFBundleURLTypes") ? root["CFBundleURLTypes"].AsArray() : root.CreateArray("CFBundleURLTypes");
            string scheme = Reverse(config.googleIosClientId);
            bool exists = false;
            foreach (var value in types.values)
            {
                var entry = value.AsDict();
                if (!entry.values.ContainsKey("CFBundleURLSchemes")) continue;
                foreach (var existing in entry["CFBundleURLSchemes"].AsArray().values)
                    if (existing.AsString() == scheme) exists = true;
            }
            if (!exists) types.AddDict().CreateArray("CFBundleURLSchemes").AddString(scheme);
        }
        plist.WriteToFile(plistPath);
    }
    private static string Reverse(string value)
    {
        var parts = value.Split('.');
        System.Array.Reverse(parts);
        return string.Join(".", parts);
    }
#endif
}
#endif
