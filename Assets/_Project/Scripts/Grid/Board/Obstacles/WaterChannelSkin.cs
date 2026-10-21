using UnityEngine;

/// <summary>
/// Su kanalı görselleri (Resources/WaterChannelSkin). Sahneye referans bağlamadan WaterChannelService yükler.
/// Şekil anahtarı = parçanın bağladığı iki kenar: H (sol-sağ), V (üst-alt), LB, LT, RB, RT (L sol, R sağ, T üst, B alt).
/// Diziler <see cref="ShapeKeys"/> sırasıyla doldurulur. Geometri: 0-10 taş, 10-90 kanal, 90-100 taş; köşeler kavisli.
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Water Channel Skin")]
public sealed class WaterChannelSkin : ScriptableObject
{
    public static readonly string[] ShapeKeys = { "H", "V", "LB", "LT", "RB", "RT" };

    [Tooltip("Kapalı kanal (pirinç ızgaralı kapak), ShapeKeys sırasıyla.")]
    public Sprite[] closed = new Sprite[6];
    [Tooltip("Açık-kuru kanal, ShapeKeys sırasıyla.")]
    public Sprite[] open = new Sprite[6];
    [Tooltip("Su katmanı (saydam zemin, açık kanalın üstüne biner), ShapeKeys sırasıyla.")]
    public Sprite[] water = new Sprite[6];
    [Tooltip("Depo / çeşme başlığı: depo hücresinde, taşların üstünde durur.")]
    public Sprite source;
    [Tooltip("Akış ucu köpüğü: sağa akan su için çizilmiş dikey şerit (dalgalı ön kenar sağda); akış yönüne döndürülür.")]
    public Sprite foam;
    [Tooltip("Çıkışta fışkıran damla.")]
    public Sprite droplet;
    [Tooltip("Şelale: dikişsiz su dokusu (Repeat) + TinyFixers/UI/WaterfallFlow malzemesi. Çıkış alt kenardaysa kanal " +
             "tamamlanınca tahtanın altından sürekli dökülür (animasyon shader'da, CPU işi yok).")]
    public Texture waterfallTexture;
    public Material waterfallMaterial;

    [Header("Ses")]
    [Tooltip("Kanal hücresi açılınca (üstündeki taş eşleşti / special vurdu).")]
    public AudioClip openSound;
    [Range(0f, 1f)] public float openSoundVolume = 1f;
    [Tooltip("Su ilerlerken aralıklarla çalan kısa şırıltı (döngü değil).")]
    public AudioClip flowSound;
    [Range(0f, 1f)] public float flowSoundVolume = 0.8f;
    [Tooltip("Kanal tamamlanıp su kenardan dışarı fışkırınca.")]
    public AudioClip splashSound;
    [Range(0f, 1f)] public float splashSoundVolume = 1f;

    private static WaterChannelSkin shared;
    private static bool loaded;

    public static WaterChannelSkin Shared
    {
        get
        {
            if (!loaded)
            {
                shared = Resources.Load<WaterChannelSkin>("WaterChannelSkin");
                loaded = true;
            }
            return shared;
        }
    }

    public static int ShapeIndex(string key)
    {
        for (int i = 0; i < ShapeKeys.Length; i++)
            if (ShapeKeys[i] == key) return i;
        return 0;
    }

    public Sprite Closed(string key) => Get(closed, key);
    public Sprite Open(string key) => Get(open, key);
    public Sprite Water(string key) => Get(water, key);

    private static Sprite Get(Sprite[] list, string key)
    {
        int i = ShapeIndex(key);
        return list != null && i < list.Length ? list[i] : null;
    }
}
