using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Web sitesi (Website/) için oyun içi kare yakalama. Play modunda Game görünümünü kendi çözünürlüğünde
/// Website/raw/ klasörüne PNG olarak yazar (UI dahil, ekranda ne varsa). superSize KULLANILMAZ: oyun sahnesinin
/// Screen Space Camera tuvalleriyle büyütülmüş çekim yalnız bir köşeyi devasa yakalıyordu (menü doğruydu).
///   • Cmd/Ctrl+Shift+K → kare yakala
///   • Cmd/Ctrl+Shift+J → ağır çekim aç/kapat (patlama/kombo anını yakalamak için; Time.timeScale 0.2)
/// İpucu: Game görünümünü telefon dikey çözünürlüğüne al (ör. 1179x2556). Duraklatılmışken (Pause) yakalarsan
/// dosya bir sonraki karede yazılır → Step (Cmd+Alt+P) ile bir kare ilerlet.
/// </summary>
public static class WebsiteScreenshotCapture
{
    private const float SlowMotionScale = 0.2f;
    private static float s_savedTimeScale = 1f;
    private static bool s_slowMotion;

    [MenuItem("TinyFixers/Website/Capture Screenshot %#k")]
    private static void Capture()
    {
        string dir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Website", "raw");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"shot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[WebsiteShot] Kare yakalandı → {path}");
    }

    [MenuItem("TinyFixers/Website/Capture Screenshot %#k", true)]
    private static bool CanCapture() => EditorApplication.isPlaying;

    [MenuItem("TinyFixers/Website/Toggle Slow Motion %#j")]
    private static void ToggleSlowMotion()
    {
        s_slowMotion = !s_slowMotion;
        if (s_slowMotion)
        {
            s_savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = SlowMotionScale;
        }
        else
        {
            Time.timeScale = s_savedTimeScale;
        }
        Debug.Log($"[WebsiteShot] Ağır çekim {(s_slowMotion ? "AÇIK" : "kapalı")} (timeScale {Time.timeScale:0.##})");
    }

    [MenuItem("TinyFixers/Website/Toggle Slow Motion %#j", true)]
    private static bool CanToggleSlowMotion() => EditorApplication.isPlaying;

    // Play modundan çıkınca ağır çekim bayrağı sıfırlansın (timeScale Unity'de zaten sıfırlanır).
    [InitializeOnLoadMethod]
    private static void ResetOnPlayModeExit()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode) s_slowMotion = false;
        };
    }
}
