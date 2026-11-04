using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// Google Play makbuz doğrulama anahtarı (GooglePlayTangle) üretimi — IAP Project Settings sayfası servis anahtarı kapalıyken
/// Google Play bölümünü göstermediği için (servisi açmak projeyi Unity bulutuna bağlar) paketin kendi üreticisini doğrudan çağırır.
/// Kullanım: Play Console ▸ Monetize with Play ▸ Monetization setup ▸ Licensing anahtarını KOPYALA → bu menü (panodan okur).
public static class GooglePlayTangleMenu
{
    [MenuItem("TinyFixers/IAP/Google Play Tangle Üret (panodaki lisans anahtarından)")]
    private static void Generate()
    {
        string key = (EditorGUIUtility.systemCopyBuffer ?? "").Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
        if (!LooksLikeLicenseKey(key, out string reason))
        {
            EditorUtility.DisplayDialog("Google Play Tangle",
                "Panodaki metin Play lisans anahtarına benzemiyor (" + reason + ").\n\nPlay Console ▸ Monetization setup ▸ Licensing " +
                "anahtarını kopyala butonuyla kopyalayıp tekrar dene. Hiçbir dosya yazılmadı.", "Tamam");
            return;
        }

        var generator = Type.GetType("UnityEditor.Purchasing.ObfuscationGenerator, UnityEditor.Purchasing");
        var method = generator?.GetMethod("ObfuscateGoogleSecrets", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (method == null)
        {
            EditorUtility.DisplayDialog("Google Play Tangle", "IAP paketinde üretici bulunamadı (paket sürümü değişmiş olabilir).", "Tamam");
            return;
        }

        string error = method.Invoke(null, new object[] { key }) as string;
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog("Google Play Tangle", "Üretim hatası:" + error, "Tamam");
            return;
        }
        Debug.Log("[IAP] GooglePlayTangle üretildi: Assets/Scripts/UnityPurchasing/generated/GooglePlayTangle.cs");
        EditorUtility.DisplayDialog("Google Play Tangle",
            "GooglePlayTangle.cs üretildi (Assets/Scripts/UnityPurchasing/generated).\nAndroid satın almalarında Google imza kontrolü artık açık.", "Tamam");
    }

    // Play lisans anahtarı: Base64 X.509 SubjectPublicKeyInfo (RSA 2048) → "MIIB…", çözülünce ~294 bayt, DER SEQUENCE (0x30).
    private static bool LooksLikeLicenseKey(string key, out string reason)
    {
        reason = null;
        if (string.IsNullOrEmpty(key)) { reason = "pano boş"; return false; }
        if (!key.StartsWith("MIIB", StringComparison.Ordinal)) { reason = "'MIIB' ile başlamıyor"; return false; }
        byte[] bytes;
        try { bytes = Convert.FromBase64String(key); }
        catch { reason = "Base64 değil"; return false; }
        if (bytes.Length < 200 || bytes[0] != 0x30) { reason = "anahtar biçimi geçersiz"; return false; }
        return true;
    }
}
