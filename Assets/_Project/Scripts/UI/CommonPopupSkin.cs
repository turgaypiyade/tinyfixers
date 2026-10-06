using TMPro;
using UnityEngine;

/// <summary>Shared artwork and safe content regions for utility popups.</summary>
[CreateAssetMenu(menuName = "TinyFixers/UI/Common Popup Skin")]
public sealed class CommonPopupSkin : ScriptableObject
{
    public Sprite background;
    public Sprite continueButton;
    public Sprite accountButton;
    [Tooltip("Yeşil butonla aynı kalıpta mavi buton — iki seçenekli popup'larda ikincil seçenek (Hayır/İptal).")]
    public Sprite secondaryButton;
    public Material saveProgressBackgroundMaterial;
    public Sprite saveProgressContinueButton;
    public Sprite closeButton;
    public TMP_FontAsset font;
    [Tooltip("Başlık fontu (gösterim: Baloo 2 ExtraBold). Boşsa 'font'.")]
    public TMP_FontAsset titleFont;
    [Tooltip("Buton fontu (Nunito Black). Boşsa 'font'.")]
    public TMP_FontAsset buttonFont;
    [Header("Zemine göre yazı stili (kontur/gölge = zeminin koyu tonu; font materyalinden türetilir)")]
    [Tooltip("Başlık (bordo zemin). Boşsa font'un varsayılan materyali.")]
    public Material titleMaterial;
    [Tooltip("Kutlama anı başlığı (satın alma \"HARİKA!\") — DynaPuff: yalnız oyun anı yazıları.")]
    public TMP_FontAsset celebrationFont;
    public Material celebrationMaterial;
    [Tooltip("Başlık yüzü üst → alt geçiş (joker başlığı gibi krem → açık altın). titleMaterial atanmışsa kullanılır.")]
    public Color titleGradientTop = Color.white;
    public Color titleGradientBottom = new Color(1f, 0.97f, 0.78f, 1f);
    [Tooltip("Yeşil buton yazısı (continueButton / saveProgressContinueButton).")]
    public Material greenButtonMaterial;
    [Tooltip("Mavi buton yazısı (accountButton).")]
    public Material blueButtonMaterial;
    [Tooltip("Buton yazısı yüzü üst → alt geçiş (beyaz → krem-sarı). Buton materyali atanmışsa kullanılır.")]
    public Color buttonGradientTop = Color.white;
    public Color buttonGradientBottom = new Color(1f, 0.99f, 0.9f, 1f);
    [Tooltip("Alttaki ana buton yazısı punto (buttonFont cinsinden; tüm popup'larda aynı). 0 = actionFontSize'tan türet.")]
    [Min(0)] public float buttonFontSize = 74f;
    // Fiyat yazılarında <sprite name="goldmoney"> için altın ikonu.
    public TMP_SpriteAsset coinSpriteAsset;
    [Min(1)] public float titleFontSize = 80f;
    [Range(-90f, 90f)] public float titleArcDegrees = 15f;
    [Min(1)] public float actionFontSize = 72f;
    public Color bodyTextColor = new Color(0.24f, 0.12f, 0.10f);
    public Vector2 panelSize = new Vector2(900, 1245);
    // Normalized regions let replacement artwork be aligned in the Inspector.
    public Rect titleRegion = new Rect(0.20f, 0.85f, 0.60f, 0.11f);
    public Rect bodyRegion = new Rect(0.18f, 0.27f, 0.64f, 0.46f);
    public Rect actionsRegion = new Rect(0.12f, 0.035f, 0.76f, 0.21f);
    public Rect closeRegion = new Rect(0.85f, 0.87f, 0.10f, 0.072f);

    private void OnEnable() => ValidateRegions();
    private void OnValidate() => ValidateRegions();

    private void ValidateRegions()
    {
        // Rect YAML without serializedVersion: 2 imports as an empty rectangle.
        // Field initializers do not protect against those deserialized zero sizes.
        if (!IsUsableRegion(titleRegion)) titleRegion = new Rect(0.20f, 0.85f, 0.60f, 0.11f);
        if (!IsUsableRegion(bodyRegion)) bodyRegion = new Rect(0.18f, 0.27f, 0.64f, 0.46f);
        if (!IsUsableRegion(actionsRegion)) actionsRegion = new Rect(0.12f, 0.035f, 0.76f, 0.21f);
        if (!IsUsableRegion(closeRegion)) closeRegion = new Rect(0.85f, 0.87f, 0.10f, 0.072f);
    }

    private static bool IsUsableRegion(Rect region)
        => region.width > 0 && region.height > 0 && region.xMin >= 0 && region.yMin >= 0
            && region.xMax <= 1 && region.yMax <= 1;

    private static CommonPopupSkin cached;
    public static CommonPopupSkin Shared
    {
        get
        {
            if (cached == null) cached = Resources.Load<CommonPopupSkin>("CommonPopupSkin");
            if (cached == null)
            {
                Debug.LogError("[CommonPopup] Resources/CommonPopupSkin is missing.");
                cached = CreateInstance<CommonPopupSkin>();
            }
            return cached;
        }
    }
}
