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
    public Sprite bannerArt;    // top-3 büyük kart görseli (opsiyonel)
    public bool isSelf;
}

/// <summary>
/// Liderlik verisi kaynağı. v1'de MockLeaderboardService; backend gelince (UGS/Firebase)
/// aynı arayüze gerçek implementasyon takılır — controller değişmez.
/// </summary>
public interface ILeaderboardService
{
    /// <summary>Şu an eldeki (cache'lenmiş) sıralı liste. Mock anında dolu; Firebase Fetch sonrası dolar.</summary>
    List<LeaderboardEntry> GetEntries(LeaderboardTab tab);

    /// <summary>Yarışmanın kalan süre etiketi (örn "2g 20s", "Bitti").</summary>
    string GetTimeLabel(LeaderboardTab tab);

    /// <summary>Sekmenin verisini (yeniden) yükle. Async olabilir; bitince OnChanged tetiklenir.</summary>
    void Fetch(LeaderboardTab tab);

    /// <summary>Veri güncellendiğinde tetiklenir → controller listeyi yeniden basar.</summary>
    event Action OnChanged;
}
