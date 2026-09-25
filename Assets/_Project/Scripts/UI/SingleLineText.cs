using TMPro;

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
}
