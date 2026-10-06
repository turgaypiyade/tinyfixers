using UnityEditor;
using UnityEngine;

// Gizli yardım kademelerini (LevelAssist) test etmek için mevcut level'ın üst üste fail sayısını ayarlar.
// Kademe: 3+ fail → refill yardımı, 5+ → hazır roket. Sonraki level açılışında geçerli olur.
public static class LevelAssistDebugMenu
{
    [MenuItem("TinyFixers/Debug/Level Yardımı/Kademe 0 (sıfırla)")]
    private static void Tier0() => SetFails(0);

    [MenuItem("TinyFixers/Debug/Level Yardımı/Kademe 1 (3 fail)")]
    private static void Tier1() => SetFails(3);

    [MenuItem("TinyFixers/Debug/Level Yardımı/Kademe 2 (5 fail)")]
    private static void Tier2() => SetFails(5);

    private static void SetFails(int fails)
    {
        int level = CurrentLevel.Global;
        PlayerPrefs.SetInt("level_attempt_level", level);
        PlayerPrefs.SetInt("level_attempt_fails", fails);
        PlayerPrefs.SetInt("level_attempt_count", fails);
        PlayerPrefs.Save();
        Debug.Log($"[LevelAssist] Level {level}: üst üste fail={fails} → kademe {LevelAssist.TierFor(level)} (level'ı yeniden başlat).");
    }
}
