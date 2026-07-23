using UnityEngine;

/// <summary>
/// Cihazda yüksek yenileme hızını garanti eder. Ayarlanmazsa Unity iOS varsayılanı
/// genelde 30fps'e düşer → hızlı taş düşüşü kare başına büyük zıplayarak "kesik/sahte"
/// akar (Editor 60fps'te iyi görünür, cihazda bozulur). ProMotion (120Hz) açıksa 120,
/// değilse cihaz zaten 60'a sınırlar.
///
/// NOT: targetFrameRate'in dikkate alınması için QualitySettings.vSyncCount = 0 olmalı;
/// aksi halde framerate vSync'e bağlanır ve targetFrameRate yok sayılır. iOS'ta ekran
/// zaten çift tamponlu olduğundan vSync=0 tearing yaratmaz.
/// </summary>
public static class FrameRateBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyHighRefreshRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
    }
}
