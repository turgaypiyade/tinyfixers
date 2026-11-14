using System.IO;
using UnityEngine;

/// <summary>
/// TEST: "Kayıtları Sil" — cihazı oyunu ilk kez açan oyuncu gibi başlatır (seviye 1, varsayılan altın/yıldız).
/// Yalnız debug seviye seçicisinden istenir (editör + Development Build); mağaza sürümünde istenemez.
///
/// Silme bir SONRAKİ açılışın en başında yapılır: uygulama kapanırken servisler kayıt yazsa da
/// açılışta henüz hiçbir şey yüklenmeden hepsi silinir. Firebase o açılışta eski anonim oturumu kapatıp
/// YENİ bir UID açar → buluttan hiçbir şey çekilmez; eski hesabın bulut verisine dokunulmaz.
/// </summary>
public static class DevFreshStart
{
    private const string MarkerFile = "dev_fresh_start.marker";
    private const string SignOutPendingKey = "dev_fresh_start_signout";

    private static string MarkerPath => Path.Combine(Application.persistentDataPath, MarkerFile);

    /// Debug panelinden: sonraki açılışta silmeyi planla. Bu oturumda bulut itmesini durdurmak için
    /// Firebase oturumu hemen kapatılır (IsReady=false → eski UID'ye hiçbir şey yazılmaz).
    public static void Schedule()
    {
        File.WriteAllText(MarkerPath, System.DateTime.UtcNow.ToString("O"));
        FirebaseAuthService.SignOutForFreshStart();
    }

    /// Firebase dahil her şeyden önce: işaret varsa bütün yerel kaydı sil.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void ApplyPendingWipe()
    {
        if (!File.Exists(MarkerPath)) return;
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt(SignOutPendingKey, 1);   // Firebase başlarken eski oturumu kapatsın
        PlayerPrefs.Save();
        TryDeleteDirectory(Path.Combine(Application.persistentDataPath, "account-recovery"));
        File.Delete(MarkerPath);
        Debug.LogWarning("[DevFreshStart] Tüm yerel kayıtlar silindi; oyun yeni oyuncu olarak başlıyor.");
    }

    /// FirebaseAuthService girişten önce sorar: silmeden sonraki ilk açılışta eski oturum kapatılır.
    internal static bool ConsumeSignOutPending()
    {
        if (PlayerPrefs.GetInt(SignOutPendingKey, 0) != 1) return false;
        PlayerPrefs.DeleteKey(SignOutPendingKey);
        PlayerPrefs.Save();
        return true;
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (System.Exception e) { Debug.LogWarning($"[DevFreshStart] {path} silinemedi: {e.Message}"); }
    }
}
