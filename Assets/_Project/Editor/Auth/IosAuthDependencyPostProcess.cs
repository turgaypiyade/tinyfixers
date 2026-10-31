#if UNITY_EDITOR && UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Firebase and Google Sign-In share one SPM dependency graph. Facebook's independent
/// Pods remain supported. Native provider code, client IDs and Apple entitlements are untouched.
/// </summary>
public sealed class IosAuthDependencyPostProcess : IPreprocessBuildWithReport
{
    public int callbackOrder => -900;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;
        Google.IOSResolver.UseProjectSettings = true;
        Google.IOSResolver.SwiftPackageManagerEnabled = true;
        // The app target needs CocoaPods' configuration variables for selected Facebook slices.
        Google.IOSResolver.PodfileAlwaysAddMainTarget = true;
    }

    // EDM4U adds SPM references at 35, then generates Podfile at 40.
    [PostProcessBuild(39)]
    public static void NormalizeSwiftPackages(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        string path = PBXProject.GetPBXProjectPath(output);
        string before = File.ReadAllText(path);
        string after = DeduplicateSwiftPackages(before);
        if (!Regex.IsMatch(after, @"\bproductName\s*=\s*""?GoogleSignIn""?\s*;"))
            throw new BuildFailedException("GoogleSignIn SPM product is missing. Check GoogleSignInDependencies.xml and iOS Resolver SPM integration.");
        if (after != before) File.WriteAllText(path, after);
    }

    // Do not let a resolver/settings regression silently reintroduce the conflicting Google Pod.
    [PostProcessBuild(46)]
    public static void ValidatePodfile(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        string path = Path.Combine(output, "Podfile");
        if (!File.Exists(path)) return;
        if (Regex.IsMatch(File.ReadAllText(path), @"(?m)^[ \t]*pod[ \t]+['""]GoogleSignIn(?:/[^'""]*)?['""]"))
            throw new BuildFailedException("GoogleSignIn must use SPM alongside Firebase, not CocoaPods. Check its replacesPod mapping and iOS Resolver settings.");
    }

    /// <summary>
    /// Unity serializes these objects in canonical OpenStep layout. Merge only identical
    /// package requirements/products/build settings; preserve different versions and targets.
    /// Prevents references accumulating when exporting repeatedly into the same folder.
    /// </summary>
    public static string DeduplicateSwiftPackages(string text)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        const string multiline = @"(?m)^\t\t(?<id>[A-F0-9]{24})(?: /\*[^\r\n]*\*/)? = \{\r?\n(?<body>[\s\S]*?)^\t\t\};\r?\n?";
        text = MergeEntries(text, multiline, "XCRemoteSwiftPackageReference", aliases);
        text = MergeEntries(text, multiline, "XCSwiftPackageProductDependency", aliases);
        const string buildFile = @"(?m)^\t\t(?<id>[A-F0-9]{24})(?: /\*[^\r\n]*\*/)? = \{(?<body>isa = PBXBuildFile;[^\r\n]*)\};\r?\n?";
        text = MergeEntries(text, buildFile, "PBXBuildFile", aliases);
        if (aliases.Count == 0) return text;

        var mergedIds = new HashSet<string>(aliases.Values, StringComparer.Ordinal);
        return Regex.Replace(text,
            @"(?m)(?<prefix>^[ \t]*(?:files|packageReferences|packageProductDependencies) = \(\r?\n)(?<items>[\s\S]*?)(?<suffix>^[ \t]*\);)",
            list =>
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                string items = Regex.Replace(list.Groups["items"].Value,
                    @"(?m)^[ \t]*(?<id>[A-F0-9]{24})(?: /\*[^\r\n]*\*/)?,[ \t]*\r?\n?",
                    item => mergedIds.Contains(item.Groups["id"].Value) && !seen.Add(item.Groups["id"].Value)
                        ? "" : item.Value);
                return list.Groups["prefix"].Value + items + list.Groups["suffix"].Value;
            });
    }

    private static string MergeEntries(string text, string pattern, string isa, Dictionary<string, string> aliases)
    {
        var canonical = new Dictionary<string, string>(StringComparer.Ordinal);
        var removed = new HashSet<string>(StringComparer.Ordinal);
        string merged = Regex.Replace(text, pattern, entry =>
        {
            string body = entry.Groups["body"].Value;
            if (!body.Contains("isa = " + isa + ";") || (isa == "PBXBuildFile" && !body.Contains("productRef = ")))
                return entry.Value;
            // Ignore formatting/comments while retaining quoted strings exactly (URLs, requirements).
            string key = Regex.Replace(body, @"""(?:\\.|[^""\\])*""|/\*[\s\S]*?\*/|\s+",
                token => token.Value.StartsWith("\"", StringComparison.Ordinal) ? token.Value : "");
            string id = entry.Groups["id"].Value;
            if (!canonical.TryGetValue(key, out string existing)) { canonical[key] = id; return entry.Value; }
            aliases[id] = existing;
            removed.Add(id);
            return "";
        });
        if (removed.Count == 0) return text;
        return Regex.Replace(merged, @"\b[A-F0-9]{24}\b",
            reference => aliases.TryGetValue(reference.Value, out string canonicalId) ? canonicalId : reference.Value);
    }
}
#endif
