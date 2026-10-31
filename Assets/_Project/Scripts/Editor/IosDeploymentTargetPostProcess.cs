#if UNITY_EDITOR && UNITY_IOS
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;

/// <summary>Keep Pods, including privacy bundles, at the app's supported iOS floor.</summary>
public static class IosDeploymentTargetPostProcess
{
    private const string Marker = "# TinyFixers iOS deployment floor";

    // EDM4U generates Podfile at 40 and installs Pods at 50 (its documented extension point).
    [PostProcessBuild(45)]
    public static void ConfigurePodfile(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        string path = Path.Combine(output, "Podfile");
        if (!File.Exists(path)) return;
        string before = File.ReadAllText(path);
        string after = UpdatePodfile(before, MinimumVersion());
        if (after != before) File.WriteAllText(path, after);
    }

    // Also covers exports where Pods already exist and automatic pod install is disabled.
    [PostProcessBuild(1005)]
    public static void NormalizeExistingPods(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        string path = Path.Combine(output, "Pods", "Pods.xcodeproj", "project.pbxproj");
        if (!File.Exists(path)) return;
        string before = File.ReadAllText(path);
        string after = NormalizeProjectTargets(before, MinimumVersion());
        if (after != before) File.WriteAllText(path, after);
    }

    public static string MinimumVersion()
    {
        var floor = new Version(15, 0);
        if (Version.TryParse(PlayerSettings.iOS.targetOSVersionString, out var app) && app > floor)
            return app.ToString();
        return floor.ToString();
    }

    public static string NormalizeProjectTargets(string text, string minimum)
    {
        var floor = Version.Parse(minimum);
        return Regex.Replace(text,
            @"(?m)(\bIPHONEOS_DEPLOYMENT_TARGET\s*=\s*)(""?)(\d+(?:\.\d+)+)(\2\s*;)",
            match => Version.TryParse(match.Groups[3].Value, out var version) && version < floor
                ? match.Groups[1].Value + match.Groups[2].Value + minimum + match.Groups[4].Value
                : match.Value);
    }

    public static string UpdatePodfile(string text, string minimum)
    {
        // Regenerate only our marked helper; preserve all existing plugin hooks and dependencies.
        text = Regex.Replace(text, @"(?ms)^# TinyFixers iOS deployment floor\r?\n.*?^# End TinyFixers iOS deployment floor\r?\n?", "");
        string helper = Marker + "\n" +
            "def tinyfixers_apply_ios_deployment_floor(installer)\n" +
            "  minimum = Gem::Version.new('" + Version.Parse(minimum) + "')\n" +
            "  projects = [installer.pods_project]\n" +
            "  projects.concat(Array(installer.generated_projects)) if installer.respond_to?(:generated_projects)\n" +
            "  projects.compact.uniq.each do |project|\n" +
            "    configurations = project.build_configurations + project.targets.flat_map(&:build_configurations)\n" +
            "    configurations.each do |config|\n" +
            "      current = config.build_settings['IPHONEOS_DEPLOYMENT_TARGET']\n" +
            "      if current.nil? || current.to_s == '$(inherited)' ||\n" +
            "          (current.to_s.match?(/\\A\\d+(?:\\.\\d+)*\\z/) && Gem::Version.new(current.to_s) < minimum)\n" +
            "        config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = minimum.to_s\n" +
            "      end\n" +
            "    end\n" +
            "  end\n" +
            "end\n" +
            "# End TinyFixers iOS deployment floor\n";
        var hook = Regex.Match(text, @"(?m)^[ \t]*post_install[ \t]+do[ \t]*\|(?<arg>[A-Za-z_]\w*)\|[ \t]*(?:#[^\r\n]*)?(?=\r?$)");
        if (text.Contains("tinyfixers_apply_ios_deployment_floor("))
            return text.TrimEnd() + "\n\n" + helper;
        if (hook.Success)
        {
            string call = "\n  tinyfixers_apply_ios_deployment_floor(" + hook.Groups["arg"].Value + ")";
            text = text.Insert(hook.Index + hook.Length, call);
        }
        else
        {
            // Avoid silently registering a second hook if a custom plugin uses different Ruby syntax.
            if (Regex.IsMatch(text, @"(?m)^\s*post_install\b"))
                throw new InvalidOperationException("TinyFixers: unsupported post_install syntax in Podfile; existing hook was preserved.");
            text = text.TrimEnd() + "\n\npost_install do |installer|\n  tinyfixers_apply_ios_deployment_floor(installer)\nend\n";
        }
        return text.TrimEnd() + "\n\n" + helper;
    }
}
#endif
