using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tiny Safari yarış eventinin tüm parametreleri. Inspector'dan ayarlanır, kod değişmez.
/// Asset: Resources/Events/SafariConfig.asset (SafariRuntime otomatik yükler).
///
/// Kurallar (spec):
///  - Level <see cref="minLevelGate"/> ve sonrasında açılır.
///  - Haftada <see cref="activeDays"/> günleri sahneye çıkar; bir kez çıkınca <see cref="windowHours"/> saat sürer.
///  - Hiç katılmadıysa <see cref="reAskIntervalHours"/> saatte bir "katıl" popup'ı çıkar.
///  - Düşünce (ilk-hakta geçemeyince) <see cref="fallCooldownMinutes"/> dk beklenir.
///  - <see cref="pitstopCount"/> pitstop; 7 strike tamamlayan kazananlar <see cref="prizePoolGold"/> altını paylaşır (booster YOK).
/// </summary>
[CreateAssetMenu(fileName = "SafariConfig", menuName = "TinyFixers/Events/Safari Config")]
public sealed class SafariConfig : ScriptableObject
{
    [Header("Kimlik")]
    public string eventId   = "tiny_safari";
    public string eventName = "Tiny Safari";
    [Tooltip("Fail popup 'kaybedeceklerin' listesinde bu event için gösterilecek ikon.")]
    public Sprite lossIcon;

    [Header("Yarış")]
    [Tooltip("Toplam pitstop (arka planda 7 var). 7. pitstopa ulaşan eventi kazanır.")]
    [Min(1)] public int pitstopCount = 7;

    [Header("Kapı & Zamanlama")]
    [Tooltip("Bu level ve sonrasında event açılır.")]
    [Min(1)] public int minLevelGate = 50;

    [Tooltip("Eventin sahneye çıkacağı günler (UTC). Boşsa her gün açık kabul edilir.")]
    public List<DayOfWeek> activeDays = new()
    {
        DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    [Tooltip("Event bir kez çıkınca kaç saat açık kalır.")]
    [Min(1)] public int windowHours = 24;

    [Tooltip("Açıksa event ilk aktifte / saatte bir otomatik 'katıl' popup'ı çıkar. " +
             "Kapalıysa popup YALNIZ ikona tıklayınca açılır.")]
    public bool autoShowJoinPopup = false;

    [Tooltip("Hiç katılmayan oyuncuya kaç saatte bir 'katıl' popup'ı gösterilsin (autoShowJoinPopup açıksa).")]
    [Min(1)] public int reAskIntervalHours = 1;

    [Tooltip("İlk-hakta geçemeyip (düşünce) tekrar devam için beklenecek süre (dk).")]
    [Min(0)] public int fallCooldownMinutes = 30;

    [Header("Katılımcı Gösterimi")]
    [Tooltip("Yarışa katılmış gibi gösterilecek toplam kişi (sayaç 1→bu).")]
    [Min(1)] public int participantVisualCount = 100;

    [Tooltip("Sol-üst dairede aynı anda ekranda tutulacak avatar sayısı (arkalı-önlü yığın).")]
    [Min(1)] public int avatarsOnScreen = 24;

    [Header("Ödül (kazananlar arasında paylaşılır)")]
    [Tooltip("Safari'yi tamamlayan (7 strike) TÜM kazananlar arasında paylaşılacak toplam altın. " +
             "Booster verilmez.")]
    [Min(0)] public int prizePoolGold = 2000;

    [Tooltip("Ödülü paylaşan tahmini kazanan sayısı (oyuncu dahil). Canlıda bu sayı backend'den gelir; " +
             "oyuncunun payı = prizePoolGold / kazanan sayısı.")]
    [Min(1)] public int simulatedWinnerCount = 20;

    [Header("Debug (yalnız Editor — PRODUCTION'DA KAPAT)")]
    [Tooltip("Açıkken schedule (aktif gün) ve level kapısı yok sayılır; event her zaman erişilebilir. " +
             "Yalnız UNITY_EDITOR'da etkilidir; canlıya çıkmadan kapatılmalı. Bkz. editor_test_overrides_revert.")]
    public bool debugForceAvailable = false;
}
