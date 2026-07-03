using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Alt-menü ekranlarının (Journey / Rank / Team / Marketplace) ortak görsel dili.
/// Tek kaynak: palet renkleri, spacing, paylaşılan sprite/font referansları.
/// Her ekran controller'ı bir UITheme referansı tutar; renkleri buradan okur.
///
/// Renkler kod default'u olarak gelir (referans görsellerden uyarlandı, bizim oyunun
/// amber-trim'li sıcak mor brand'ine göre). Sprite/font alanları Inspector'da bağlanır;
/// boş bırakılırsa controller'lar düz renk + UI default font'a düşer.
///
/// Oluştur: Assets > Create > TinyFixers > UI Theme
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/UI Theme", fileName = "UITheme")]
public sealed class UITheme : ScriptableObject
{
    [Header("Zemin & Yüzeyler")]
    public Color screenBackground = new Color32(43, 35, 80, 255);   // #2B2350
    public Color panelSurface     = new Color32(81, 69, 140, 255);  // #51458C
    public Color creamSurface     = new Color32(243, 233, 216, 255); // #F3E9D8
    public Color headerBand       = new Color32(110, 84, 181, 255); // #6E54B5
    public Color specialBand      = new Color32(142, 33, 80, 255);  // #8E2150 (Özel Teklifler)

    [Header("Vurgu & Aksiyon")]
    public Color accentAmber = new Color32(255, 178, 62, 255);  // #FFB23E (coin)
    public Color goldTrim    = new Color32(245, 166, 35, 255);  // #F5A623
    public Color ctaGreen    = new Color32(79, 201, 91, 255);   // #4FC95B
    public Color priceGreen  = new Color32(91, 209, 91, 255);   // #5BD15B
    public Color lifeRed     = new Color32(255, 77, 94, 255);   // #FF4D5E
    public Color infoBlue    = new Color32(54, 166, 224, 255);  // #36A6E0

    [Header("Metin")]
    public Color textLight    = Color.white;
    public Color textSub       = new Color32(201, 190, 234, 255); // #C9BEEA
    public Color textOnCream   = new Color32(90, 62, 43, 255);    // #5A3E2B
    public Color textHighlight = new Color32(79, 201, 91, 255);   // kendi satırın / başarı

    [Header("Spacing (px)")]
    public float screenPadding   = 24f;
    public float sectionSpacing  = 20f;
    public float itemSpacing     = 12f;
    public float cardCornerRadius = 24f; // sprite 9-slice kullanılıyorsa bilgi amaçlı

    [Header("Paylaşılan Sprite'lar (opsiyonel — boşsa düz renk)")]
    [Tooltip("Genel panel/kart arka planı (9-slice önerilir).")]
    public Sprite panelBackground;
    [Tooltip("Kart yüzeyi (9-slice).")]
    public Sprite cardBackground;
    [Tooltip("Buton arka planı (9-slice). Renk tint ile değiştirilir.")]
    public Sprite buttonBackground;
    [Tooltip("Bölüm başlığı bandı (9-slice).")]
    public Sprite sectionHeaderBackground;
    [Tooltip("İlerleme çubuğu dolgusu.")]
    public Sprite progressFill;
    [Tooltip("Coin ikonu (HUD ile aynı).")]
    public Sprite coinIcon;
    [Tooltip("Yıldız ikonu.")]
    public Sprite starIcon;
    [Tooltip("Can/kalp ikonu.")]
    public Sprite heartIcon;

    [Header("Font")]
    [Tooltip("Başlık/kalın metin font'u. Boşsa TMP default.")]
    public TMP_FontAsset headingFont;
    [Tooltip("Gövde metni font'u. Boşsa TMP default.")]
    public TMP_FontAsset bodyFont;

    // ----- Yardımcılar ---------------------------------------------------

    /// <summary>Image'a tema sprite + renk uygular. Sprite null ise düz renk (Image.sprite=null).</summary>
    public static void ApplySurface(Image target, Sprite sprite, Color color)
    {
        if (target == null) return;
        target.sprite = sprite;
        target.type   = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        target.color  = color;
    }

    /// <summary>TMP metnine tema font + renk uygular (font null ise dokunmaz).</summary>
    public void ApplyText(TMP_Text text, Color color, bool heading = false)
    {
        if (text == null) return;
        var font = heading ? headingFont : bodyFont;
        if (font != null) text.font = font;
        text.color = color;
    }
}
