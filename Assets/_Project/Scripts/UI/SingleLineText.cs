using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;

/// <summary>
/// Oyuncu/takım isimleri gibi uzunluğu bilinmeyen metinler: HER ZAMAN tek satır.
/// Sığmazsa font küçülür (tasarım boyutunun minRatio'suna kadar), yine sığmazsa "…" ile kesilir.
/// Prefab'taki font boyutu üst sınırdır; tekrar çağrılması güvenlidir.
/// </summary>
public static class SingleLineText
{
    public static void Fit(TMP_Text text, float minRatio = 0.55f)
    {
        if (text == null) return;

        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;

        // Autosize açıkken fontSize hesaplanan değerdir; tasarım boyutu yalnız ilk seferde okunur.
        if (!text.enableAutoSizing)
        {
            float designSize = text.fontSize;
            text.fontSizeMax = designSize;
            text.fontSizeMin = designSize * minRatio;
            text.enableAutoSizing = true;
        }
    }

    // Tasarım (prefab) font boyutu, metin başına ilk çağrıda kaydedilir.
    private static readonly ConditionalWeakTable<TMP_Text, object> designSizes = new();

    /// <summary>
    /// Başlıklar ("Seviye 101" gibi uzunluğu değişen): tek satır; sığmazsa YALNIZ GENİŞLİĞE göre küçülür.
    /// <see cref="Fit"/>'ten farkı: TMP autosize yüksekliği de sığdırır — kutusu yazıdan kısa çizilmiş
    /// başlıkları gereksiz küçültürdü. Metin atandıktan SONRA çağrılır; tekrar çağrılması güvenlidir.
    /// </summary>
    public static void FitWidth(TMP_Text text, float minRatio = 0.6f)
    {
        if (text == null) return;

        if (!designSizes.TryGetValue(text, out var boxed))
        {
            boxed = text.fontSize;
            designSizes.Add(text, boxed);
        }
        float designSize = (float)boxed;

        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontSize = designSize;

        float width = text.rectTransform.rect.width;
        if (width <= 0f) return;

        float preferred = text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x;
        if (preferred > width)
            text.fontSize = Mathf.Max(designSize * minRatio, designSize * width / preferred);
    }
}
