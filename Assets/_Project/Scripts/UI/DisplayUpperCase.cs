using System.Globalization;
using System.Text;
using TMPro;

/// <summary>
/// Başlıklar için dile duyarlı BÜYÜK HARF: TMP'nin kendi UpperCase stili Türkçede i → I yapar ("SEVIYE");
/// bu ön-işlemci oyunun diline göre büyütür (tr: i → İ, ı → I). Rich-text etiketlerine (&lt;sprite …&gt;) dokunmaz.
/// Metin sonradan değişse de her yeniden çizimde uygulanır. Tek paylaşılan örnek; ayırma yok (önbellekli StringBuilder).
/// </summary>
public sealed class DisplayUpperCase : ITextPreprocessor
{
    public static readonly DisplayUpperCase Instance = new DisplayUpperCase();
    private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");
    private readonly StringBuilder buffer = new StringBuilder(64);

    public static void Apply(TMP_Text text)
    {
        if (text == null) return;
        text.textPreprocessor = Instance;
        text.fontStyle &= ~FontStyles.UpperCase;   // TMP'nin kültürsüz büyütmesi devreye girmesin
        text.havePropertiesChanged = true;
    }

    public string PreprocessText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var culture = GameLocalization.CurrentLanguage == "tr" ? Turkish : CultureInfo.InvariantCulture;
        buffer.Clear();
        bool inTag = false;
        foreach (char c in text)
        {
            if (c == '<') inTag = true;
            buffer.Append(inTag ? c : char.ToUpper(c, culture));
            if (c == '>') inTag = false;
        }
        return buffer.ToString();
    }
}
