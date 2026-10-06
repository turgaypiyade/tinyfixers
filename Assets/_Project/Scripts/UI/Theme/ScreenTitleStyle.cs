using TMPro;
using UnityEngine;

/// <summary>
/// Alt-menü ekran başlıkları (Liderlik / Takım …) için ortak görünüm: büyük, krem-beyaz,
/// kalın koyu kontur + aşağı düşen gölge (Royal Match başlık dili). Font değişmez
/// (UITheme.headingFont); yalnız boyut + materyal (outline/underlay) ayarlanır.
/// </summary>
public static class ScreenTitleStyle
{
    public const float FontSize = 76f;
    private static readonly Color Face = new Color(1f, 0.97f, 0.88f, 1f);
    private static readonly Color Outline = new Color(0.30f, 0.06f, 0.10f, 1f);
    private static readonly Color Shadow = new Color(0.18f, 0.03f, 0.06f, 0.85f);

    /// <summary>Başlık merkezinin ekran kökünün ÜST kenarına uzaklığı (1080x1920 referans birimi).
    /// Journey / Ranks / Team başlıkları, TopBar'ları farklı kurulmuş olsa da hep aynı yerde durur.</summary>
    public const float CenterFromTop = 140f;
    private const float TitleHeight = 120f;
    private const float SidePadding = 40f;

    /// <summary>Ekran kökünün altındaki "TopBar/Title" yazısını biçimlendirir ve ortak konuma koyar.
    /// font = ortak başlık fontu (UITheme.headingFont) — sahnede farklı fontla kurulmuş başlıklar da eşitlenir.</summary>
    public static void ApplyToScreen(Transform screenRoot, TMP_FontAsset font = null)
    {
        if (screenRoot == null) return;
        var title = screenRoot.Find("TopBar/Title");
        if (title == null || !title.TryGetComponent(out TMP_Text text)) return;

        if (font != null && text.font != font) text.font = font;
        Apply(text);

        // Konum bileşeni: panel ilk açılışta henüz boyutlanmamış olabilir / ekran boyu değişebilir →
        // başlık her etkinleşmede ve boyut değişiminde ortak konuma yeniden oturur.
        if (!title.TryGetComponent(out ScreenTitlePlacer placer)) placer = title.gameObject.AddComponent<ScreenTitlePlacer>();
        placer.Init(screenRoot as RectTransform);
    }

    // TopBar'ın kendi yerleşiminden bağımsız: ekran kökünün üstünden CenterFromTop aşağıda, tam genişlik.
    internal static void PlaceAtCommonPosition(RectTransform title, RectTransform root)
    {
        if (title == null || root == null || title.parent is not RectTransform parent) return;

        Rect r = root.rect;
        if (r.height <= 1f) return;
        Vector3 worldCenter = root.TransformPoint(new Vector3(r.center.x, r.yMax - CenterFromTop, 0f));
        Vector3 local = parent.InverseTransformPoint(worldCenter);

        title.anchorMin = title.anchorMax = title.pivot = new Vector2(0.5f, 0.5f);
        // Ebeveyn merkezine göre konum (anchor = ebeveyn merkezi).
        Vector2 parentCenter = parent.rect.center;
        title.anchoredPosition = new Vector2(0f, local.y - parentCenter.y);
        title.sizeDelta = new Vector2(r.width - SidePadding * 2f, TitleHeight);
    }

    public static void Apply(TMP_Text title)
    {
        if (title == null) return;

        title.fontSize = FontSize;
        title.enableAutoSizing = true;   // uzun yerelleştirmeler ("Liderlik Panosu") sığsın
        title.fontSizeMax = FontSize;
        title.fontSizeMin = FontSize * 0.6f;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.color = Face;
        title.alignment = TextAlignmentOptions.Center;

        // fontMaterial = bu yazıya özel kopya (paylaşılan font materyali değişmez).
        var mat = title.fontMaterial;
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.24f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Outline);
        mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, Shadow);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.9f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.24f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        title.UpdateMeshPadding();
    }
}
