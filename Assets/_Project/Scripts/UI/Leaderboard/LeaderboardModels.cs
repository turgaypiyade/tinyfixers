using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Liderlik panosu sekmeleri.</summary>
public enum LeaderboardTab { Weekly, Friends, Players, Team }

/// <summary>Tek bir liderlik satırı verisi. Avatar/banner görselleri opsiyonel.</summary>
public sealed class LeaderboardEntry
{
    public int rank;
    public string playerName;
    public string subtitle;     // takım adı / ülke vb.
    public int score;
    public Sprite avatar;
    public Sprite bannerArt;    // satırın sağındaki dekoratif art (opsiyonel)
    public bool isSelf;

    // Oyuncu satırları: bulunduğu bölüm ("Bölüm 4401"). 0 = gösterme.
    public int chapter;

    // Takım satırları: üye doluluğu ("49/50"). max 0 = takım değil, çip gizlenir.
    public int capacityCurrent;
    public int capacityMax;
}

/// <summary>
/// Liderlik verisi kaynağı. v1'de MockLeaderboardService; backend gelince (UGS/Firebase)
/// aynı arayüze gerçek implementasyon takılır — controller değişmez.
/// </summary>
public interface ILeaderboardService
{
    /// <summary>
    /// Şu an eldeki (cache'lenmiş) sıralı liste. subFilter = sekmenin alt-toggle indexi
    /// (örn Oyuncular: 0=Dünya 1=Türkiye; Arkadaşlar: 0=Liste 1=Ekle). Toggle'ı olmayan
    /// sekmede 0 geçilir.
    /// </summary>
    List<LeaderboardEntry> GetEntries(LeaderboardTab tab, int subFilter);

    /// <summary>
    /// Sekmenin alt-toggle etiketleri (örn {"Dünya","Türkiye"}).
    /// Boş/null dizi = bu sekmede toggle çubuğu gizlenir (örn Haftalık).
    /// </summary>
    string[] GetSubFilters(LeaderboardTab tab);

    /// <summary>Yarışmanın kalan süre etiketi (örn "2g 20s", "Bitti").</summary>
    string GetTimeLabel(LeaderboardTab tab);

    /// <summary>Sekmenin verisini (yeniden) yükle. Async olabilir; bitince OnChanged tetiklenir.</summary>
    void Fetch(LeaderboardTab tab);

    /// <summary>Veri güncellendiğinde tetiklenir → controller listeyi yeniden basar.</summary>
    event Action OnChanged;
}
