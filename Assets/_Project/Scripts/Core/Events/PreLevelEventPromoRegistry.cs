using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level öncesi popup'ın altındaki event şeridinde gösterilecek TEK kart: "etkin ama katılmadın, katıl!".
/// </summary>
public readonly struct PreLevelEventPromo
{
    public readonly string Title;           // kulpçuktaki event adı ("Köprü Tamiri")
    public readonly string Tagline;         // kısa çağrı ("5 kişilik köprü yarışı!")
    public readonly Sprite Icon;            // event ikonu (null → ikon gizli)
    public readonly DateTime WindowEndUtc;  // geri sayım (MinValue → süre gösterilmez)
    public readonly Action Join;            // "Katıl": doğrudan katılım + event ekranı

    public PreLevelEventPromo(string title, string tagline, Sprite icon, DateTime windowEndUtc, Action join)
    {
        Title = title;
        Tagline = tagline;
        Icon = icon;
        WindowEndUtc = windowEndUtc;
        Join = join;
    }
}

/// <summary>
/// Level öncesi event şeridinin VERİSİZ toplayıcısı (<see cref="LevelLossRegistry"/> deseni): şerit hiçbir
/// event'i tanımaz; her event controller'ı kendi sağlayıcısını kaydeder. Sağlayıcı O ANKİ durumu okur ve
/// yalnız "etkin + katılmamış" iken kart döner (yoksa null). Yeni event = yeni Register, şerit koduna dokunulmaz.
/// Keyed: aynı anahtarla tekrar kayıt üzerine yazar (sahne yeniden yüklense de çift kart olmaz).
/// </summary>
public static class PreLevelEventPromoRegistry
{
    private static readonly Dictionary<string, Func<PreLevelEventPromo?>> providers = new();

    public static void Register(string key, Func<PreLevelEventPromo?> provider)
    {
        if (string.IsNullOrEmpty(key) || provider == null) return;
        providers[key] = provider;
    }

    public static void Unregister(string key)
    {
        if (!string.IsNullOrEmpty(key)) providers.Remove(key);
    }

    /// <summary>Şu an gösterilecek kartlar (kayıt sırasıyla).</summary>
    public static List<PreLevelEventPromo> Collect()
    {
        var result = new List<PreLevelEventPromo>();
        foreach (var provider in providers.Values)
        {
            PreLevelEventPromo? promo = null;
            try { promo = provider?.Invoke(); }
            catch (Exception e) { Debug.LogWarning($"[PreLevelEventPromoRegistry] provider hata: {e.Message}"); }
            if (promo.HasValue && promo.Value.Join != null)
                result.Add(promo.Value);
        }
        return result;
    }
}
