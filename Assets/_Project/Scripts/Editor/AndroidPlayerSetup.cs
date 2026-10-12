using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Android oyuncu ayarları: paket adı (Firebase google-services.json ile aynı) + uygulama ikonu (iOS ile aynı logo).
/// Android modülü kurulu editörde (Windows) açılışta bir kez kendiliğinden uygular; menüden de çalıştırılabilir:
/// TinyFixers ▸ Android ▸ Oyuncu Ayarlarını Uygula. Android modülü yoksa (Mac) ikon türleri boş gelir → atlanır.
/// Adaptive (Android 8+) ikon katmanlı tasarım ister; logo tek parça olduğu için şimdilik Legacy + Round
/// kullanılır (Android adaptive yoksa bunlara düşer).
/// </summary>
[InitializeOnLoad]
public static class AndroidPlayerSetup
{
    private const string PackageName = "com.yigefep.tinyfixers";
    private const string IconPath = "Assets/_Project/Art/UI/Logos/RamLogo.png";

    static AndroidPlayerSetup()
    {
        EditorApplication.delayCall += () => Apply(force: false);
    }

    [MenuItem("TinyFixers/Android/Oyuncu Ayarlarını Uygula")]
    private static void ApplyFromMenu() => Apply(force: true);

    private static void Apply(bool force)
    {
        var android = NamedBuildTarget.Android;
        bool changed = false;

        if (PlayerSettings.GetApplicationIdentifier(android) != PackageName)
        {
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            changed = true;
        }

        var kinds = PlayerSettings.GetSupportedIconKinds(android);
        if (kinds != null && kinds.Length > 0)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                foreach (var kind in kinds)
                {
                    string name = kind.ToString();
                    if (!name.Contains("Legacy") && !name.Contains("Round")) continue;
                    var icons = PlayerSettings.GetPlatformIcons(android, kind);
                    if (icons == null || icons.Length == 0) continue;
                    bool missing = false;
                    foreach (var pi in icons)
                        if (pi.GetTexture(0) == null) { missing = true; break; }
                    if (!missing && !force) continue;
                    foreach (var pi in icons) pi.SetTexture(icon, 0);
                    PlayerSettings.SetPlatformIcons(android, kind, icons);
                    changed = true;
                }
            }
            else
            {
                Debug.LogWarning($"[AndroidPlayerSetup] İkon bulunamadı: {IconPath}");
            }
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[AndroidPlayerSetup] Android paket adı / ikonları ayarlandı.");
        }
    }
}
