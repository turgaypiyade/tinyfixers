#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// iOS export'unda Info.plist mağaza ayarları (her export'ta yeniden yazılır, elle düzenleme gerekmez):
///   • ITSAppUsesNonExemptEncryption = false — oyun yalnız standart HTTPS/TLS kullanır (Firebase, IAP, web);
///     bu muaf şifrelemedir. Anahtar yoksa App Store Connect her yüklemede "Missing Compliance" sorar ve
///     TestFlight dağıtımı beklemede kalır.
///   • NSUserTrackingUsageDescription — yalnız reklam (TF_ADMOB) açıkken; AdMob ATT penceresi bu metni gösterir.
/// </summary>
public static class IosInfoPlistPostProcess
{
    [PostProcessBuild(1010)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        if (!File.Exists(plistPath)) return;

        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;

        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
#if TF_ADMOB
        root.SetString("NSUserTrackingUsageDescription",
            "Your data will be used to show you more relevant ads. / Verilerin sana daha ilgili reklamlar göstermek için kullanılır.");
#endif
        plist.WriteToFile(plistPath);
    }
}
#endif
