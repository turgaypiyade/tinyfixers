using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dünya haritasında açılabilir bir bölge (ada/alan).
/// Yıldız HARCAMA modeli: görev listesinden tıklanınca starCost kadar yıldız düşülür,
/// bölge kalıcı olarak "açıldı" işaretlenir ve sis (fog) animasyonla kalkar.
///
/// Açılma durumu PlayerPrefs'te regionId ile saklanır (toplam yıldıza bağlı DEĞİL).
/// Bina/dekor ayrı; bu component sadece sisi + pin'i + kalıcı durumu yönetir.
/// </summary>
public sealed class WorldMapRegion : MonoBehaviour
{
    [Header("Kimlik & Liste Bilgisi")]
    [Tooltip("Benzersiz kalıcı anahtar. Açılma durumu 'region_unlocked_<id>' olarak saklanır. " +
             "Her bölgede FARKLI olmalı (örn: living_area, garden, harbor).")]
    [SerializeField] private string regionId;
    [Tooltip("Görev listesinde gösterilecek ad için lokalizasyon anahtarı (tinyfixers_localization.json).")]
    [SerializeField] private string nameLocalizationKey;
    [Tooltip("Lokalizasyon bulunamazsa kullanılacak yedek ad.")]
    [SerializeField] private string fallbackName = "Bölge";
    [Tooltip("Bu bölgeyi açmak için harcanacak yıldız.")]
    [SerializeField, Min(0)] private int starCost = 10;
    [Tooltip("Görev listesi satırında gösterilecek ikon (opsiyonel — bölge küçük görseli).")]
    [SerializeField] private Sprite taskIcon;
    [Tooltip("Oyun ilk açıldığında zaten açık başlasın mı? (örn. başlangıç/ev bölgesi).")]
    [SerializeField] private bool startUnlocked;

    [Header("Sis (Fog)")]
    [Tooltip("Bölgeyi örten sis/bulut. CanvasGroup'lu bir obje; açılınca fade-out olur.")]
    [SerializeField] private CanvasGroup fog;
    [Tooltip("Kilitliyken sis opaklığı. 1 = tam kapalı, 0.6-0.8 = altındaki siluet hafif görünür.")]
    [SerializeField, Range(0f, 1f)] private float lockedFogAlpha = 0.75f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.6f;

    [Header("Pin (opsiyonel)")]
    [Tooltip("Bölge pini. Sisin DIŞINDA olmalı (fade olmasın).")]
    [SerializeField] private Image pin;
    [SerializeField] private Sprite lockedPinSprite;
    [SerializeField] private Sprite unlockedPinSprite;

    [Header("Hedef (görev listesi yıldızları buraya uçar)")]
    [Tooltip("Sinematikte yıldızların uçacağı / kutlamanın patlayacağı nokta. " +
             "Boşsa fog'un (yoksa bu objenin) transform'u kullanılır.")]
    [SerializeField] private RectTransform revealFocus;

    // ─── Public ──────────────────────────────────────────────────────────────

    public string RegionId => regionId;
    public string NameLocalizationKey => nameLocalizationKey;
    public string FallbackName => fallbackName;
    public int StarCost => starCost;
    public Sprite TaskIcon => taskIcon;

    public RectTransform RevealFocus =>
        revealFocus != null ? revealFocus :
        (fog != null ? (RectTransform)fog.transform : (RectTransform)transform);

    private string Key => $"region_unlocked_{regionId}";

    public bool IsUnlocked
    {
        get => startUnlocked || PlayerPrefs.GetInt(Key, 0) == 1;
        private set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    private Coroutine fadeCo;

    // ─── State ───────────────────────────────────────────────────────────────

    /// <summary>Kalıcı duruma göre sisi anında uygula (animasyonsuz). Sahne açılışında çağrılır.</summary>
    public void ApplyInstant()
    {
        bool unlocked = IsUnlocked;

        if (pin != null)
        {
            var s = unlocked ? unlockedPinSprite : lockedPinSprite;
            if (s != null) pin.sprite = s;
        }

        if (fog == null) return;
        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }

        if (unlocked)
        {
            fog.alpha = 0f;
            fog.gameObject.SetActive(false);
        }
        else
        {
            fog.gameObject.SetActive(true);
            fog.alpha = lockedFogAlpha;
        }
    }

    /// <summary>Bölgeyi kalıcı aç + sisi animasyonla kaldır. WorldMapController çağırır.</summary>
    public IEnumerator RevealRoutine()
    {
        IsUnlocked = true;   // kesinti olsa bile kaydedildi

        if (pin != null && unlockedPinSprite != null) pin.sprite = unlockedPinSprite;

        if (fog == null) yield break;
        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }

        if (!fog.gameObject.activeSelf) fog.gameObject.SetActive(true);
        float start = fog.alpha <= 0.001f ? lockedFogAlpha : fog.alpha;
        fog.alpha = start;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fog.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
            yield return null;
        }
        fog.alpha = 0f;
        fog.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(regionId))
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (string.IsNullOrEmpty(regionId))
                    Debug.LogWarning($"[WorldMapRegion] '{name}' için regionId boş — açılma durumu kaydedilemez.", this);
            };
    }
#endif
}
