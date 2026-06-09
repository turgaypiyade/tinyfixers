using System;
using System.Collections.Generic;
using UnityEngine;

/// Hangi progress event'in ne zaman aktif olacağını tanımlar.
/// Resources/Events/ProgressEventScheduler.asset yoluna kaydet.
/// Yoksa ProgressEventConfig direkt yüklenir (geriye dönük uyumluluk).
[CreateAssetMenu(fileName = "ProgressEventScheduler",
                 menuName = "TinyFixers/Events/Progress Event Scheduler")]
public class ProgressEventScheduler : ScriptableObject
{
    [Tooltip("Tanımlı event'ler. Aynı anda birden fazlası aktifse priority'ye göre seçilir.")]
    public List<ScheduledProgressEvent> events = new();

    // ── Public API ────────────────────────────────────────────────

    /// Şu an aktif olan en yüksek öncelikli event, yoksa null.
    public ScheduledProgressEvent GetActiveEvent()
    {
        int currentLevel = PlayerPrefs.GetInt("current_level", 1);
        ScheduledProgressEvent best = null;

        foreach (var e in events)
        {
            if (e.config == null)          continue;
            if (!e.schedule.enabled)       continue;
            if (currentLevel < e.schedule.minLevel) continue;
            if (!e.schedule.IsActiveNow()) continue;
            if (best == null || e.priority > best.priority)
                best = e;
        }

        return best;
    }

    public ProgressEventConfig GetActiveConfig() => GetActiveEvent()?.config;

    /// Mevcut aktif pencerenin bitiş zamanı. Always → DateTime.MaxValue.
    public DateTime GetWindowEnd(ScheduledProgressEvent e) =>
        e?.schedule.GetWindowEnd() ?? DateTime.MaxValue;

    /// Döngü anahtarı — değişince goal'ler otomatik sıfırlanır.
    public string GetCycleKey(ScheduledProgressEvent e) =>
        e?.schedule.GetCycleKey() ?? "always";
}
