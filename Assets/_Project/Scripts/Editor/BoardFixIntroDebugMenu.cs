using UnityEditor;
using UnityEngine;

// Level giriş eğlencelerini (LevelIntroFun: Brumo / Pemi) bir sonraki level girişinde zorla gösterir.
public static class BoardFixIntroDebugMenu
{
    [MenuItem("TinyFixers/Debug/Brumo Tahta Düzeltme - Sonraki Girişte Göster")]
    private static void ForceBrumo() => Force(LevelIntroFun.DebugForceBrumoKey, "Brumo");

    [MenuItem("TinyFixers/Debug/Pemi Boya Temizliği - Sonraki Girişte Göster")]
    private static void ForcePemi() => Force(LevelIntroFun.DebugForcePemiKey, "Pemi");

    private const string PauseMenu = "TinyFixers/Debug/Brumo - Makara İnince Duraklat (ölçüm)";

    [MenuItem(PauseMenu)]
    private static void TogglePause()
    {
        bool on = PlayerPrefs.GetInt(BoardFixIntro.DebugPauseKey, 0) == 0;
        PlayerPrefs.SetInt(BoardFixIntro.DebugPauseKey, on ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[BoardFixIntro] Makara inince duraklatma: {(on ? "AÇIK" : "KAPALI")}");
    }

    [MenuItem(PauseMenu, true)]
    private static bool TogglePauseValidate()
    {
        Menu.SetChecked(PauseMenu, PlayerPrefs.GetInt(BoardFixIntro.DebugPauseKey, 0) == 1);
        return true;
    }

    private static void Force(string key, string who)
    {
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        Debug.Log($"[LevelIntroFun] {who} bir sonraki level girişinde gösterilecek.");
    }
}
