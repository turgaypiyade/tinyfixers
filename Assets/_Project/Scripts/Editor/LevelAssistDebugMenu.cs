using UnityEditor;
using UnityEngine;

// Ad/purchase profiles: 9/12/15 spent lives unlock tiers. Non-monetized profiles still need a 72h return.
public static class LevelAssistDebugMenu
{
    [MenuItem("TinyFixers/Debug/Level Yardımı/Kademe 0 (sıfırla)")]
    private static void Tier0() => SetSpentLives(0);

    [MenuItem("TinyFixers/Debug/Level Yardımı/Kademe 1 (9 can - reklam veya alım şart)")]
    private static void Tier1() => SetSpentLives(9);

    [MenuItem("TinyFixers/Debug/Level Yardımı/Kademe 2 (12 can - reklam veya alım şart)")]
    private static void Tier2() => SetSpentLives(12);

    [MenuItem("TinyFixers/Debug/Level Yardımı/Kademe 3 (15 can - reklam veya alım şart)")]
    private static void Tier3() => SetSpentLives(15);

    private static void SetSpentLives(int spent)
    {
        int level = CurrentLevel.Global;
        PlayerPrefs.SetInt("level_attempt_level", level);
        PlayerPrefs.SetInt("level_attempt_lives_spent", spent);
        PlayerPrefs.Save();
        if (GameDiagnostics.EditorTools)
            Debug.Log($"[LevelAssist] Level {level}: harcanan can={spent}, reklam/alım={LevelAssist.HasAdOrPurchase} → kademe {LevelAssist.TierFor(level)} (level'ı yeniden başlat).");
    }
}
