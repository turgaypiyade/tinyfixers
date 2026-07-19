using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyuncunun müzik parçası sahipliği + seçimi — kalıcı (PlayerPrefs), tek kaynak.
/// Parça 0 her zaman ÜCRETSİZ/sahipli (varsayılan menü müziği). Diğerleri 100 altınla
/// bir kez açılır (TryUnlock → PlayerWallet.SpendCoins), sonra bedava seçilir.
/// CloudSaveManifest'e dahildir → cihaz değişiminde açılan parçalar korunur.
/// </summary>
public static class MusicState
{
    public const int TrackCostCoins = 100;

    private const string KeyOwned    = "music_owned";      // CSV of ids ("0\n2")
    private const string KeySelected = "music_selected";   // int
    private const char Sep = '\n';

    /// <summary>Sahiplik veya seçim değişince tetiklenir → müzik/UI tazelenir.</summary>
    public static event Action OnChanged;

    /// <summary>
    /// Aktif müzik kataloğu — MainMenuMusicStarter (veya kütüphaneyi atayan her kim ise)
    /// burayı doldurur. Profil ekranı/popup kendi ref'i yoksa buradan okur → kütüphaneyi
    /// TEK yere atamak yeter. Seçili parçanın adı da buradan gelir.
    /// </summary>
    public static MusicLibrary Library { get; set; }

    /// <summary>Seçili parçanın görünen adı (kütüphane yoksa boş).</summary>
    public static string SelectedTrackName
    {
        get
        {
            if (Library == null || Library.Count == 0) return "";
            var t = Library.Get(SelectedTrack);
            return t != null ? t.displayName : "";
        }
    }

    private static HashSet<int> owned;

    /// <summary>Şu an çalan/seçili parçanın id'si (varsayılan 0).</summary>
    public static int SelectedTrack
    {
        get => PlayerPrefs.GetInt(KeySelected, 0);
        private set { PlayerPrefs.SetInt(KeySelected, value); PlayerPrefs.Save(); }
    }

    /// <summary>Parça sahipli mi? (id 0 her zaman sahipli.)</summary>
    public static bool IsOwned(int id)
    {
        if (id <= 0) return true;
        EnsureLoaded();
        return owned.Contains(id);
    }

    /// <summary>
    /// 100 altın harcayıp parçayı açar. Zaten sahipse true (harcamaz).
    /// Yetersiz altın → false. Başarıda parça ayrıca SEÇİLİR (satın al = kullan).
    /// </summary>
    public static bool TryUnlock(int id)
    {
        if (IsOwned(id)) { Select(id); return true; }

        if (!PlayerWallet.SpendCoins(TrackCostCoins))
            return false;

        EnsureLoaded();
        owned.Add(id);
        SaveOwned();
        Select(id);   // açar açmaz çalsın
        return true;
    }

    /// <summary>Sahip olunan bir parçayı seç (çalmaya başlasın). Sahip değilse yok sayar.</summary>
    public static void Select(int id)
    {
        if (!IsOwned(id)) return;
        SelectedTrack = id;
        OnChanged?.Invoke();
    }

    private static void EnsureLoaded()
    {
        if (owned != null) return;
        owned = new HashSet<int> { 0 };
        foreach (var part in PlayerPrefs.GetString(KeyOwned, "").Split(Sep))
            if (int.TryParse(part.Trim(), out int id)) owned.Add(id);
    }

    private static void SaveOwned()
    {
        PlayerPrefs.SetString(KeyOwned, string.Join(Sep.ToString(), owned));
        PlayerPrefs.Save();
    }
}
