#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

/// <summary>
/// Cihazın BÖLGE ayarındaki ülke kodu ("TR", "US"…) — DİL değil: Almanya'da yaşayan Türk oyuncu dili Türkçe olsa da
/// "DE" döner. iOS: NSLocale (Plugins/iOS/TFDeviceCountry.mm), Android: java.util.Locale, diğer: RegionInfo
/// (Unity'nin CultureInfo'su iOS IL2CPP'de cihaz bölgesini güvenilir vermez). Bilinemezse dil fallback'i (Türkçe → TR),
/// o da yoksa "XX". İzin gerektirmez, çevrimdışı çalışır. Editörde test: PlayerPrefs "debug_country" (ör. "TR").
/// </summary>
public static class DeviceCountry
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string TF_GetCountryCode();
#endif

    private static string cached;

    public static string Get()
    {
        if (cached != null) return cached;
        string code = null;
#if UNITY_EDITOR
        code = PlayerPrefs.GetString("debug_country", "");
#endif
        try
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (string.IsNullOrEmpty(code)) code = TF_GetCountryCode();
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrEmpty(code))
            {
                using var locale = new AndroidJavaClass("java.util.Locale");
                using var current = locale.CallStatic<AndroidJavaObject>("getDefault");
                code = current.Call<string>("getCountry");
            }
#endif
            if (string.IsNullOrEmpty(code))
            {
                var region = System.Globalization.RegionInfo.CurrentRegion;
                if (region != null) code = region.TwoLetterISORegionName;
            }
        }
        catch (System.Exception) { code = null; }

        if (string.IsNullOrEmpty(code) || code.Length != 2)
            code = Application.systemLanguage == SystemLanguage.Turkish ? "TR" : "XX";
        cached = code.ToUpperInvariant();
        return cached;
    }
}
