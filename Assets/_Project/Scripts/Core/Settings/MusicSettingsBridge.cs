using UnityEngine;

/// <summary>
/// GameSettings.MusicEnabled değişimini MusicManager'a aktarır.
/// Off → master volume 0, On → master volume 1 (veya orijinal değer).
/// Sahne başlangıcında da senkron eder.
/// </summary>
[DefaultExecutionOrder(-50)]
public class MusicSettingsBridge : MonoBehaviour
{
    private void OnEnable()
    {
        GameSettings.OnMusicChanged += ApplyMusic;
        // Sahne başlangıcında mevcut ayarı uygula
        ApplyMusic(GameSettings.MusicEnabled);
    }

    private void OnDisable()
    {
        GameSettings.OnMusicChanged -= ApplyMusic;
    }

    private void ApplyMusic(bool enabled)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetMasterVolume(enabled ? 1f : 0f);
    }
}
