using System;
using UnityEngine;

/// <summary>
/// Harika (event) ilerleme durumu — kalıcı, PlayerPrefs. Saf mantık; UI/tören içermez.
///
/// MODEL:
/// • WonderCatalog'daki her kayıt bir EVENT'tir; her event'in kendi görev listesi vardır
///   (WonderDefinition.tasks). Her görev WonderDefinition.GetStarCost kadar yıldız harcar.
/// • Her event'in ilerlemesi BAĞIMSIZ tutulur (wonder_stage_{index}) → bir event bitmeden
///   diğerinde de görev yapılabilir.
/// • EVENT KİLİDİ = BÖLÜM (chapter): kaç event açık olduğu oyuncunun bulunduğu bölüme eşittir
///   (bölüm 1 → 1 event, bölüm 2 → 2 event ...). Yani ilk event bitmemiş olsa bile bölüm
///   bitince sıradaki event açılır; bölüm bitmeden yeni event AÇILMAZ.
/// • Ana menü arka planı = en son TAMAMLANAN event (LastCompletedIndex).
///
/// [[project_wonder_reveal_background]] [[project_worldmap_region_unlock]]
/// </summary>
public static class WonderProgress
{
    // Event başına görev sırası. CloudSaveManifest'te "wonder_stage_" aile öneki olarak taranır.
    const string KeyStagePrefix = "wonder_stage_";
    const string KeyLastCompleted = "wonder_last_completed";
    const string KeyModelVersion = "wonder_model_v2";

    // v1 (tek aktif harika) anahtarları — yalnız migration kaynağı olarak okunur.
    const string KeyLegacyCompleted = "wonder_completed_count";
    const string KeyLegacyStage = "wonder_current_stage";

    // Migration'da "bu event bitmişti" demek için kullanılan sınırsız sayaç.
    const int CompletedSentinel = 9999;
    const int MaxEventScan = 64;

    /// <summary>Bir görev tamamlandı (parametre = event indeksi).</summary>
    public static event Action<int> OnTaskCompleted;
    /// <summary>Bir event (harika) tamamlandı (parametre = tamamlanan event indeksi).</summary>
    public static event Action<int> OnWonderCompleted;

    // ─── Migration (v1 tek-harika modeli → v2 event başına ilerleme) ──────────

    private static bool _migrationChecked;

    private static void EnsureMigrated()
    {
        if (_migrationChecked) return;
        _migrationChecked = true;

        if (PlayerPrefs.GetInt(KeyModelVersion, 0) >= 2) return;

        int legacyCompleted = PlayerPrefs.GetInt(KeyLegacyCompleted, 0);
        int legacyStage = PlayerPrefs.GetInt(KeyLegacyStage, 0);

        for (int i = 0; i < legacyCompleted && i < MaxEventScan; i++)
            PlayerPrefs.SetInt(KeyStagePrefix + i, CompletedSentinel);

        if (legacyCompleted < MaxEventScan && legacyStage > 0)
            PlayerPrefs.SetInt(KeyStagePrefix + legacyCompleted, legacyStage);

        PlayerPrefs.SetInt(KeyLastCompleted, legacyCompleted - 1);
        PlayerPrefs.SetInt(KeyModelVersion, 2);
        PlayerPrefs.Save();
    }

    // ─── Event kilidi (bölüm kapısı) ─────────────────────────────────────────

    /// <summary>
    /// Kaç event açık? = oyuncunun bulunduğu bölüm (katalog sayısıyla sınırlı).
    /// Bölüm bilgisi yoksa (kütüphane bulunamadı) hepsi açık sayılır — konfigürasyon
    /// hatası oyuncuyu ilerlemeden kilitlemez.
    /// </summary>
    public static int UnlockedEventCount(WonderCatalog cat)
    {
        if (cat == null || cat.Count <= 0) return 0;
        if (!ChapterProgress.HasLibrary) return cat.Count;
        return Mathf.Clamp(ChapterProgress.Current, 1, cat.Count);
    }

    public static bool IsEventUnlocked(WonderCatalog cat, int index)
        => index >= 0 && index < UnlockedEventCount(cat);

    /// <summary>Kilitli bir event daha var mı (katalogda kaldı ve bölüm yetmiyor)?</summary>
    public static bool HasLockedEvent(WonderCatalog cat)
        => cat != null && UnlockedEventCount(cat) < cat.Count;

    /// <summary>Sıradaki event'in açılması için BİTİRİLMESİ gereken bölüm numarası.</summary>
    public static int ChapterToFinishForNextEvent => ChapterProgress.Current;

    // ─── Event başına görev ilerlemesi ───────────────────────────────────────

    /// <summary>Bu event'te kaç görev yapıldı (0..TaskCount).</summary>
    public static int StageOf(int index)
    {
        if (index < 0) return 0;
        EnsureMigrated();
        return Mathf.Max(0, PlayerPrefs.GetInt(KeyStagePrefix + index, 0));
    }

    private static void SetStage(int index, int value)
    {
        if (index < 0) return;
        PlayerPrefs.SetInt(KeyStagePrefix + index, Mathf.Max(0, value));
        PlayerPrefs.Save();
    }

    public static bool IsEventComplete(WonderCatalog cat, int index)
    {
        var w = cat != null ? cat.Get(index) : null;
        return w != null && StageOf(index) >= w.TaskCount;
    }

    /// <summary>Bu event'te kalan görev sayısı.</summary>
    public static int RemainingTasks(WonderCatalog cat, int index)
    {
        var w = cat != null ? cat.Get(index) : null;
        if (w == null) return 0;
        return Mathf.Max(0, w.TaskCount - StageOf(index));
    }

    /// <summary>Bu event'in sıradaki görevinin yıldız maliyeti (0 = görev kalmadı).</summary>
    public static int NextTaskCost(WonderCatalog cat, int index)
    {
        var w = cat != null ? cat.Get(index) : null;
        if (w == null) return 0;
        int stage = StageOf(index);
        return stage >= w.TaskCount ? 0 : w.GetStarCost(stage);
    }

    public static bool CanAffordNextTask(WonderCatalog cat, int index)
    {
        int cost = NextTaskCost(cat, index);
        return cost > 0 && PlayerWallet.HasEnoughStars(cost);
    }

    /// <summary>Görev gerçekten başlatılabilir mi: event açık + görev kaldı + yıldız yeterli.</summary>
    public static bool CanStartNextTask(WonderCatalog cat, int index)
        => IsEventUnlocked(cat, index) && CanAffordNextTask(cat, index);

    /// <summary>Açık event'lerden herhangi birinde yapılabilecek görev var mı (bildirim noktası).</summary>
    public static bool HasStartableTask(WonderCatalog cat)
    {
        int unlocked = UnlockedEventCount(cat);
        for (int i = 0; i < unlocked; i++)
            if (CanStartNextTask(cat, i)) return true;
        return false;
    }

    /// <summary>
    /// Belirtilen event'in sıradaki görevi için yıldız harcar; başarılıysa o event'in
    /// stage'ini artırır. Event tamamlanmışsa (son görev) burada TAMAMLANDI İŞARETLENMEZ —
    /// çağıran tören (reveal %100 + sandık) sonrası MarkEventCompleted çağırır.
    /// </summary>
    public static bool TrySpendForNextTask(WonderCatalog cat, int index)
    {
        var w = cat != null ? cat.Get(index) : null;
        if (w == null) return false;
        if (!IsEventUnlocked(cat, index)) return false;

        int stage = StageOf(index);
        if (stage >= w.TaskCount) return false;

        int cost = w.GetStarCost(stage);
        if (!PlayerWallet.SpendStars(cost)) return false;

        SetStage(index, stage + 1);
        OnTaskCompleted?.Invoke(index);
        return true;
    }

    /// <summary>Sandık verildikten sonra çağrılır: event tamamlandı olarak işaretlenir.</summary>
    public static void MarkEventCompleted(WonderCatalog cat, int index)
    {
        if (!IsEventComplete(cat, index)) return;
        if (index > LastCompletedIndex)
        {
            PlayerPrefs.SetInt(KeyLastCompleted, index);
            PlayerPrefs.Save();
        }
        OnWonderCompleted?.Invoke(index);
    }

    // ─── Türetilen sorgular ──────────────────────────────────────────────────

    /// <summary>En son tamamlanan event indeksi. -1 = hiçbiri.</summary>
    public static int LastCompletedIndex
    {
        get { EnsureMigrated(); return PlayerPrefs.GetInt(KeyLastCompleted, -1); }
    }

    /// <summary>Ana menüde görünen harika (en son tamamlanan). null = default arka plan.</summary>
    public static WonderDefinition BackgroundWonder(WonderCatalog cat)
        => cat != null ? cat.Get(LastCompletedIndex) : null;

    /// <summary>Tamamlanmış event sayısı (katalog üzerinden sayılır).</summary>
    public static int CompletedCount(WonderCatalog cat)
    {
        if (cat == null) return 0;
        int n = 0;
        for (int i = 0; i < cat.Count; i++)
            if (IsEventComplete(cat, i)) n++;
        return n;
    }

    /// <summary>Açık ve henüz bitmemiş İLK event indeksi. -1 = yapılacak görev yok.</summary>
    public static int ActiveEventIndex(WonderCatalog cat)
    {
        int unlocked = UnlockedEventCount(cat);
        for (int i = 0; i < unlocked; i++)
            if (!IsEventComplete(cat, i)) return i;
        return -1;
    }

    public static WonderDefinition ActiveWonder(WonderCatalog cat)
    {
        int i = ActiveEventIndex(cat);
        return i >= 0 && cat != null ? cat.Get(i) : null;
    }

    /// <summary>Bir event'in açılma oranı (0..1) = yapılan görev / toplam.</summary>
    public static float RevealNormalized(WonderCatalog cat, int index)
    {
        var w = cat != null ? cat.Get(index) : null;
        if (w == null || w.TaskCount <= 0) return 0f;
        return Mathf.Clamp01((float)StageOf(index) / w.TaskCount);
    }

    public static void ResetAll()
    {
        for (int i = 0; i < MaxEventScan; i++)
            PlayerPrefs.DeleteKey(KeyStagePrefix + i);

        PlayerPrefs.DeleteKey(KeyLastCompleted);
        PlayerPrefs.DeleteKey(KeyModelVersion);
        PlayerPrefs.DeleteKey(KeyLegacyCompleted);
        PlayerPrefs.DeleteKey(KeyLegacyStage);
        PlayerPrefs.Save();
        _migrationChecked = false;
    }
}
