using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vazgeçince (give-up) kaybedilecek TEK bir öğe: ikon + isim + miktar + gerçekleşti-mi.
/// Loss paneli bunları çizer.
/// </summary>
public readonly struct LevelLossItem
{
    public readonly Sprite Icon;     // opsiyonel (null → ikon gizlenir)
    public readonly string Label;    // localized isim ("Safari ilerlemesi")
    public readonly int Amount;
    public readonly bool Achieved;   // true → checkmark, false → cancel rozeti

    public LevelLossItem(Sprite icon, string label, int amount, bool achieved)
    {
        Icon = icon;
        Label = label;
        Amount = amount;
        Achieved = achieved;
    }
}

/// <summary>
/// "Bu level'da vazgeçersem ne kaybederim?" bilgisinin MERKEZİ ama VERİSİZ toplayıcısı.
///
/// Amaç: loss paneli (UI) hiçbir event'i tek tek tanımasın. Her event/sistem kendi riskini
/// bir provider ile buraya kaydeder; UI yalnızca <see cref="Collect"/> ile toplayıp çizer.
/// Yeni event = yeni provider + Register → UI'a HİÇ dokunulmaz (open/closed).
///
/// Pull-based: provider O ANKİ canlı durumu okur (staging/commit/discard senkronu YOK).
/// Keyed: aynı anahtar tekrar Register edilince ÜzerINE yazılır → sahne yeniden yüklense de
/// çift kayıt olmaz.
/// </summary>
public static class LevelLossRegistry
{
    private static readonly Dictionary<string, Func<IEnumerable<LevelLossItem>>> providers = new();

    // İkon köprüsü: static event class'ları (Safari/Streak) kendi sprite'ını tutamaz. Controller
    // Inspector'daki ikonları SADECE anahtarla buraya iter; provider aynı anahtarla GetIcon ile okur.
    // Böylece ikon UI'da (atanabilir) kalır, mantık event'te kalır, registry tek hub olur.
    private static readonly Dictionary<string, Sprite> icons = new();

    public static void SetIcon(string key, Sprite icon)
    {
        if (string.IsNullOrEmpty(key)) return;
        icons[key] = icon;
    }

    public static Sprite GetIcon(string key)
        => !string.IsNullOrEmpty(key) && icons.TryGetValue(key, out var s) ? s : null;

    /// <summary>Bir kaynağı (event) kaydeder. <paramref name="key"/> benzersiz olmalı (idempotent).</summary>
    public static void Register(string key, Func<IEnumerable<LevelLossItem>> provider)
    {
        if (string.IsNullOrEmpty(key) || provider == null)
            return;
        providers[key] = provider;
    }

    public static void Unregister(string key)
    {
        if (!string.IsNullOrEmpty(key))
            providers.Remove(key);
    }

    /// <summary>Tüm provider'ların O ANKİ risklerini toplar. Boş/anlamsız (0 miktar + ikonsuz) öğeleri eler.</summary>
    public static IEnumerable<LevelLossItem> Collect()
    {
        foreach (var provider in providers.Values)
        {
            IEnumerable<LevelLossItem> items = null;
            try { items = provider?.Invoke(); }
            catch (Exception e) { Debug.LogWarning($"[LevelLossRegistry] provider hata: {e.Message}"); }

            if (items == null)
                continue;

            foreach (var item in items)
            {
                if (item.Amount == 0 && item.Icon == null && string.IsNullOrEmpty(item.Label))
                    continue;   // gösterilecek bir şey yok (ikon+sayı+isim hepsi boş)
                yield return item;
            }
        }
    }
}
