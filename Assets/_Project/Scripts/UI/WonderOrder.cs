using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyuncuya özel harika sırası: YENİ oyuncunun ilk harikası ülkesinin harikası olur (Türkiye → Ayasofya, ABD → Grand
/// Kanyon…), gerisi katalog sırasıyla. WonderCatalog.Get(slot) bu eşlemeden geçer → ilerleme (wonder_stage_{slot}),
/// ana menü, görevler ve Journey kendiliğinden yeni sıraya uyar. [[project_wonder_reveal_background]]
///   • Sıra wonderId listesi olarak kaydedilir ("wonder_order", bulut kayıtta) → katalog büyüyünce yeni harikalar sona.
///   • İlerlemesi olan (mevcut) oyuncunun sırası ASLA değişmez: kayıt yoksa ve ilerleme varsa katalog sırası yazılır.
///   • Bulut geri yükleme kararı verilmeden karar "geçici"dir: geri yükleme ilerleme getirdiyse (başka cihazdan dönen,
///     güncelleme öncesi oyuncu) katalog sırasına döner; bulut kendi sırasını getirdiyse o geçerli.
/// Get başına bellek ayırmaz (eşleme önbellekli; yalnız geri yüklemede/katalog değişince yeniden kurulur).
/// </summary>
public static class WonderOrder
{
    public const string PrefKey = "wonder_order";
    private const string ProvisionalKey = "wonder_order_provisional";   // yerel; buluta gitmez

    // Ülke → ilk harika (wonderId). Listede olmayan ülke katalog sırasıyla başlar.
    private static readonly Dictionary<string, string> FirstByCountry = new()
    {
        ["TR"] = "mis7",                                                    // Ayasofya
        ["US"] = "mis6", ["CA"] = "mis6",                                   // Grand Kanyon
        ["CN"] = "mis5", ["TW"] = "mis5", ["HK"] = "mis5", ["MO"] = "mis5", ["SG"] = "mis5",   // Çin Seddi
        ["FR"] = "mis3", ["BE"] = "mis3", ["LU"] = "mis3", ["MC"] = "mis3", // Eyfel
        ["IT"] = "mis1", ["SM"] = "mis1", ["VA"] = "mis1",                  // Pisa (zaten ilk)
        ["EG"] = "mis4", ["SA"] = "mis4", ["AE"] = "mis4", ["QA"] = "mis4", ["KW"] = "mis4", ["BH"] = "mis4",
        ["OM"] = "mis4", ["JO"] = "mis4", ["LB"] = "mis4", ["IQ"] = "mis4", ["MA"] = "mis4", ["DZ"] = "mis4",
        ["TN"] = "mis4", ["LY"] = "mis4", ["SD"] = "mis4", ["YE"] = "mis4", ["PS"] = "mis4", ["SY"] = "mis4",  // Piramit
    };

    private static WonderCatalog cachedCatalog;
    private static int[] map;
    private static bool dirty = true;
    private static bool subscribed;

    /// <summary>Oyuncu sırasındaki <paramref name="slot"/> → katalogdaki gerçek indeks.</summary>
    public static int RawIndex(WonderCatalog catalog, int slot)
    {
        var list = catalog != null ? catalog.wonders : null;
        if (list == null || list.Length == 0) return slot;
        if (dirty || cachedCatalog != catalog || map == null || map.Length != list.Length)
            Build(catalog);
        return slot >= 0 && slot < map.Length ? map[slot] : slot;
    }

    private static void Build(WonderCatalog catalog)
    {
        if (!subscribed)
        {
            subscribed = true;
            FirebaseCloudSaveService.OnRestored += HandleRestored;
        }
        dirty = false;
        cachedCatalog = catalog;
        int n = catalog.wonders.Length;

        string saved = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(saved))
            saved = Decide(catalog);

        var order = new List<int>(n);
        foreach (var id in saved.Split(','))
        {
            int idx = IndexOf(catalog, id.Trim());
            if (idx >= 0 && !order.Contains(idx)) order.Add(idx);
        }
        for (int i = 0; i < n; i++)
            if (!order.Contains(i)) order.Add(i);      // sonradan eklenen harikalar sona
        map = order.ToArray();
    }

    private static string Decide(WonderCatalog catalog)
    {
        string first = null;
        bool fresh = !HasProgress(catalog.wonders.Length);
        string country = DeviceCountry.Get();
        if (fresh && FirstByCountry.TryGetValue(country, out var id) && IndexOf(catalog, id) >= 0)
            first = id;

        var ids = new List<string>(catalog.wonders.Length);
        if (first != null) ids.Add(first);
        foreach (var w in catalog.wonders)
            if (w != null && w.wonderId != first) ids.Add(w.wonderId);
        string result = string.Join(",", ids);

        PlayerPrefs.SetString(PrefKey, result);
        if (!FirebaseCloudSaveService.RestoreResolved) PlayerPrefs.SetString(ProvisionalKey, result);
        PlayerPrefs.Save();
        FirebaseCloudSaveService.MarkDirty();
        if (fresh) GameAnalytics.SetStartWonder(ids.Count > 0 ? ids[0] : "", country);
        return result;
    }

    // Geri yükleme yerel kaydı değiştirdi. Sıra geri yüklemeden ÖNCE geçici kararlaştırıldıysa: bulut kendi sırasını
    // getirdiyse (kayıt değişti) o geçerli; getirmediyse ama ilerleme getirdiyse (güncelleme öncesi, başka cihazdan dönen
    // oyuncu) katalog sırasına dön — onun ilerlemesi katalog sırasına göre kaydedilmişti.
    private static void HandleRestored()
    {
        string provisional = PlayerPrefs.GetString(ProvisionalKey, "");
        if (provisional.Length > 0)
        {
            PlayerPrefs.DeleteKey(ProvisionalKey);
            if (cachedCatalog != null && PlayerPrefs.GetString(PrefKey, "") == provisional
                && HasProgress(cachedCatalog.wonders.Length))
            {
                var ids = new List<string>(cachedCatalog.wonders.Length);
                foreach (var w in cachedCatalog.wonders) if (w != null) ids.Add(w.wonderId);
                PlayerPrefs.SetString(PrefKey, string.Join(",", ids));
            }
            PlayerPrefs.Save();
        }
        dirty = true;
    }

    private static bool HasProgress(int wonderCount)
    {
        if (CurrentLevel.Global > 1) return true;
        if (PlayerPrefs.GetInt("wonder_last_completed", -1) >= 0) return true;
        for (int i = 0; i < wonderCount; i++)
            if (PlayerPrefs.GetInt("wonder_stage_" + i, 0) > 0) return true;
        return false;
    }

    private static int IndexOf(WonderCatalog catalog, string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < catalog.wonders.Length; i++)
            if (catalog.wonders[i] != null && catalog.wonders[i].wonderId == id) return i;
        return -1;
    }
}
