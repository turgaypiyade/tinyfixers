using UnityEditor;
using UnityEngine;

/// Usta Ligi test menüsü (yalnız editör). Ana yolu bitmiş saymak oyuncu ilerlemesini (current_level) değiştirir:
/// geri almak için "Ana Yola Dön" son seviyeye geri koyar.
public static class MasterLeagueDebugMenu
{
    private const string Root = "TinyFixers/Debug/Usta Ligi/";
    private const string CatalogPath = "Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset";

    private static LevelCatalog Catalog => AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

    // Editörde her Play'de TestLevelProgressionBootstrap current_level'ı EditorTestSettings.testLevel'a çeker → ikisini
    // birlikte yaz (yalnız PlayerPrefs yazılırsa Play'de geri ezilir).
    private static void SetLevel(int level)
    {
        PlayerPrefs.SetInt(CurrentLevel.PrefsKey, level);
        var settings = Resources.Load<EditorTestSettings>("EditorTestSettings");
        if (settings == null) return;
        settings.testLevel = level;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
        Debug.Log($"[UstaLigi] EditorTestSettings.testLevel = {level}");
    }

    [MenuItem(Root + "Ligi Aç (ana yolu bitmiş say)")]
    private static void Unlock()
    {
        var catalog = Catalog;
        if (catalog == null) { Debug.LogError("[UstaLigi] katalog bulunamadı: " + CatalogPath); return; }
        SetLevel(catalog.MaximumGlobalLevel + 1);
        PlayerPrefs.Save();
        Debug.Log($"[UstaLigi] current_level = {catalog.MaximumGlobalLevel + 1} (ana yol bitti). Ana menüyü yeniden aç.");
    }

    [MenuItem(Root + "Ana Yola Dön (son seviye)")]
    private static void Relock()
    {
        var catalog = Catalog;
        if (catalog == null) return;
        SetLevel(catalog.MaximumGlobalLevel);
        PlayerPrefs.Save();
        MasterLeague.End();
        Debug.Log($"[UstaLigi] current_level = {catalog.MaximumGlobalLevel}.");
    }

    [MenuItem(Root + "Lig İlerlemesini Sıfırla")]
    private static void ResetLeague()
    {
        PlayerPrefs.DeleteKey(MasterLeague.KeyLap);
        PlayerPrefs.DeleteKey(MasterLeague.KeyLapSignature);
        PlayerPrefs.DeleteKey(MasterLeague.KeyBest);
        PlayerPrefs.DeleteKey(MasterLeague.KeyWeekly);
        PlayerPrefs.DeleteKey(MasterLeague.KeyWeeklyWeek);
        PlayerPrefs.DeleteKey(MasterLeague.KeyCleared);
        PlayerPrefs.DeleteKey(MasterLeague.KeySeason);
        PlayerPrefs.DeleteKey(MasterLeague.KeyReached);
        foreach (var key in MasterLeague.StintKeys) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.DeleteKey(MasterLeagueIntro.KeySeen);
        PlayerPrefs.Save();
        Debug.Log("[UstaLigi] tur/puan + intro sıfırlandı.");
    }

    [MenuItem(Root + "Özeti Göster (Play mode, örnek veri)")]
    private static void PreviewSummary()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[UstaLigi] önizleme Play mode'da çalışır."); return; }
        Canvas target = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas && c.isActiveAndEnabled && (target == null || c.sortingOrder > target.sortingOrder)) target = c;
        MasterLeagueSummary.ShowPreview(target);
    }

    [MenuItem(Root + "Özeti Dene (yeni paket geldi say)")]
    private static void TrySummary()
    {
        var catalog = Catalog;
        if (catalog == null) return;
        PlayerPrefs.SetInt(MasterLeague.KeyReached, 1);
        if (PlayerPrefs.GetInt(MasterLeague.KeyStintWins, 0) <= 0)
        {
            // Örnek dönem verisi (yalnız hiç lig oynanmadıysa).
            PlayerPrefs.SetInt(MasterLeague.KeyStintWins, 23);
            PlayerPrefs.SetInt(MasterLeague.KeyStintPoints, 5640);
            PlayerPrefs.SetInt(MasterLeague.KeyStintBestWeekly, 3200);
            PlayerPrefs.SetInt(MasterLeague.KeyStintBestRank, 12);
            PlayerPrefs.SetInt(MasterLeague.KeyStintCoins, 1150);
        }
        SetLevel(catalog.MaximumGlobalLevel);   // ana yol yeniden açık
        PlayerPrefs.Save();
        MasterLeague.End();
        Debug.Log("[UstaLigi] yeni paket geldi sayıldı; ana menüyü aç → özet.");
    }

    [MenuItem(Root + "Haftalık Ödülü Dene (geçen hafta 3.400 puan)")]
    private static void TryWeeklyReward()
    {
        PlayerPrefs.SetString(MasterLeagueRewards.KeyPendingWeek, LastWeekId());
        PlayerPrefs.SetInt(MasterLeagueRewards.KeyPendingPoints, 3400);
        PlayerPrefs.DeleteKey(MasterLeagueRewards.KeyClaimedWeek);
        PlayerPrefs.Save();
        Debug.Log($"[UstaLigi] bekleyen ödül: {LastWeekId()} / 3400 puan. Ana menüyü aç (giriş yapılmış olmalı).");
    }

    [MenuItem(Root + "Rozetleri Sıfırla")]
    private static void ResetBadges()
    {
        PlayerPrefs.DeleteKey(MasterLeagueRewards.KeyBadgeGold);
        PlayerPrefs.DeleteKey(MasterLeagueRewards.KeyBadgeSilver);
        PlayerPrefs.DeleteKey(MasterLeagueRewards.KeyBadgeBronze);
        PlayerPrefs.Save();
        Debug.Log("[UstaLigi] rozetler sıfırlandı.");
    }

    private static string LastWeekId()
    {
        var then = System.DateTime.UtcNow.AddDays(-7);
        int week = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(then,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);
        return $"{then.Year}-W{week:00}";
    }

    [MenuItem(Root + "Introyu Tekrar Göster")]
    private static void ResetIntro()
    {
        PlayerPrefs.DeleteKey(MasterLeagueIntro.KeySeen);
        PlayerPrefs.Save();
        Debug.Log("[UstaLigi] intro bir sonraki ana menü açılışında oynar.");
    }

    [MenuItem(Root + "Durumu Yazdır")]
    private static void LogState()
    {
        var catalog = Catalog;
        int next = MasterLeague.NextLevel(catalog, out int index, out int size);
        Debug.Log($"[UstaLigi] açık={MasterLeague.IsUnlocked(catalog)} lig no={MasterLeague.LevelNumber} tur={MasterLeague.Lap} sıradaki={next} ({index + 1}/{size}) " +
                  $"kesinti=%{Mathf.RoundToInt(MasterLeague.CutForLap(MasterLeague.Lap) * 100)} haftalık puan={MasterLeague.WeeklyPoints} " +
                  $"bu tur geçilen={MasterLeague.LapBest().Count}");
        // Açılış pencereleri: intro (lig açık + izlenmedi) → haftalık ödül (bekleyen) → özet (ulaştı + ana yol açık + dönem kazancı).
        Debug.Log($"[UstaLigi] açılış: level={CurrentLevel.Global} katalogSon={catalog.MaximumGlobalLevel} sezon={PlayerPrefs.GetInt(MasterLeague.KeySeason, 0)} " +
                  $"introİzlendi={PlayerPrefs.GetInt(MasterLeagueIntro.KeySeen, 0)} ulaştı={MasterLeague.Reached} " +
                  $"dönemKazanç={PlayerPrefs.GetInt(MasterLeague.KeyStintWins, 0)} bekleyenÖdül={MasterLeagueRewards.PendingWeek}/{MasterLeagueRewards.PendingPoints} " +
                  $"rozet A/G/B={MasterLeagueRewards.Count(MasterBadge.Gold)}/{MasterLeagueRewards.Count(MasterBadge.Silver)}/{MasterLeagueRewards.Count(MasterBadge.Bronze)}");
    }
}
