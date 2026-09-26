using TMPro;
using UnityEngine;

/// <summary>
/// Ödül yazılarının ortak görünümü (günlük bonus çarkı, sandık açılışı…): Safari kazanma ekranındaki
/// "SAFARİ TAMAMLANDI" stili — Inter ExtraBold + kalın altın outline + yumuşak koyu gölge
/// (Fonts/Materials/Inter_ExtraBold_GoldOutlineGlow). Koddan kurulan overlay'ler de erişebilsin diye
/// Resources/RewardTextStyle.asset üzerinden yüklenir. Materyal font atlasına bağlı olduğundan font
/// ile birlikte atanmalı.
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Reward Text Style", fileName = "RewardTextStyle")]
public sealed class RewardTextStyle : ScriptableObject
{
    public TMP_FontAsset font;
    public Material material;
    public Color faceColor = new Color(1f, 0.98f, 0.9f, 1f);

    private static RewardTextStyle shared;
    private static bool loaded;

    public static RewardTextStyle Shared
    {
        get
        {
            if (!loaded)
            {
                shared = Resources.Load<RewardTextStyle>("RewardTextStyle");
                loaded = true;
            }
            return shared;
        }
    }

    /// Stil yoksa yazıya dokunmaz (eski görünüm kalır).
    public static void Apply(TMP_Text text, float fontSize)
    {
        var style = Shared;
        if (text == null || style == null || style.font == null) return;

        text.font = style.font;
        if (style.material != null)
            text.fontSharedMaterial = style.material;
        // Font zaten ExtraBold: faux-bold harf aralığını açar.
        text.fontStyle = FontStyles.Normal;
        text.color = style.faceColor;
        text.enableVertexGradient = false;
        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSize = fontSize;
        text.fontSizeMin = fontSize * 0.6f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.Center;
    }
}
