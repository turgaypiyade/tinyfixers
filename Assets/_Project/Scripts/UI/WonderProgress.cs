using System;
using UnityEngine;

/// <summary>
/// Harika ilerleme durumu (kalıcı, PlayerPrefs). Saf mantık — UI/tören içermez.
/// Model: ana menü HER ZAMAN tamamlanmış harikayı gösterir; reveal (kaynak) yalnız
/// mission tarafında olur. 3 sayı: CompletedCount / CurrentStage (+ türetilen indeksler).
/// Görev = kaynak kademesi; her görev WonderDefinition.GetStarCost kadar yıldız harcar.
/// [[project_wonder_reveal_background]] [[project_worldmap_region_unlock]]
/// </summary>
public static class WonderProgress
{
    const string KeyCompleted = "wonder_completed_count";
    const string KeyStage = "wonder_current_stage";

    /// <summary>Bir görev tamamlandı (parametre = yeni CurrentStage).</summary>
    public static event Action<int> OnTaskCompleted;
    /// <summary>Bir harika tamamlandı (parametre = tamamlanan harika indeksi).</summary>
    public static event Action<int> OnWonderCompleted;

    public static int CompletedCount
    {
        get => PlayerPrefs.GetInt(KeyCompleted, 0);
        private set { PlayerPrefs.SetInt(KeyCompleted, value); PlayerPrefs.Save(); }
    }

    public static int CurrentStage
    {
        get => PlayerPrefs.GetInt(KeyStage, 0);
        private set { PlayerPrefs.SetInt(KeyStage, value); PlayerPrefs.Save(); }
    }

    /// <summary>Şu an kaynaklanan (in-progress) harikanın kataloğ indeksi.</summary>
    public static int CurrentWonderIndex => CompletedCount;

    /// <summary>Ana menüde görünen harika indeksi. -1 = default arka plan.</summary>
    public static int BackgroundWonderIndex => CompletedCount - 1;

    public static WonderDefinition CurrentWonder(WonderCatalog cat)
        => cat != null ? cat.Get(CurrentWonderIndex) : null;

    public static WonderDefinition BackgroundWonder(WonderCatalog cat)
        => cat != null ? cat.Get(BackgroundWonderIndex) : null;

    /// <summary>Sıradaki görevin yıldız maliyeti (0 = görev kalmadı/harika yok).</summary>
    public static int NextTaskCost(WonderCatalog cat)
    {
        var w = CurrentWonder(cat);
        if (w == null || CurrentStage >= w.TaskCount) return 0;
        return w.GetStarCost(CurrentStage);
    }

    /// <summary>Aktif harikanın açılma oranı (0..1) = yapılan görev / toplam.</summary>
    public static float CurrentRevealNormalized(WonderCatalog cat)
    {
        var w = CurrentWonder(cat);
        if (w == null || w.TaskCount <= 0) return 0f;
        return Mathf.Clamp01((float)CurrentStage / w.TaskCount);
    }

    public static bool IsCurrentWonderComplete(WonderCatalog cat)
    {
        var w = CurrentWonder(cat);
        return w != null && CurrentStage >= w.TaskCount;
    }

    public static bool CanAffordNextTask(WonderCatalog cat)
    {
        int cost = NextTaskCost(cat);
        return cost > 0 && PlayerWallet.HasEnoughStars(cost);
    }

    /// <summary>
    /// Sıradaki görev için yıldız harcar; başarılıysa CurrentStage'i artırır.
    /// Harika tamamlanmışsa (son görev) burada wonder DEĞİŞMEZ — çağıran tören
    /// (reveal %100 + sandık) sonrası AdvanceToNextWonder çağırır.
    /// </summary>
    public static bool TrySpendForNextTask(WonderCatalog cat)
    {
        var w = CurrentWonder(cat);
        if (w == null || CurrentStage >= w.TaskCount) return false;

        int cost = w.GetStarCost(CurrentStage);
        if (!PlayerWallet.SpendStars(cost)) return false;

        CurrentStage += 1;
        OnTaskCompleted?.Invoke(CurrentStage);
        return true;
    }

    /// <summary>Sandık verildikten sonra çağrılır: sıradaki harikaya geçer.</summary>
    public static void AdvanceToNextWonder()
    {
        int done = CurrentWonderIndex;
        CompletedCount += 1;
        CurrentStage = 0;
        OnWonderCompleted?.Invoke(done);
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KeyCompleted);
        PlayerPrefs.DeleteKey(KeyStage);
        PlayerPrefs.Save();
    }
}
