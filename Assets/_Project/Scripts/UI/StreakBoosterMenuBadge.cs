using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main-menu'de streak booster event badge'i: badge arka planı (AllianceHelp) + iki gold ring
/// arasındaki kanala oturan YEŞİL radial progress (0-1-2-3) + canlı streak.
///
/// Kurulum: LeftEventPanel içine bir badge GameObject koy. Badge sprite'ını `badgeBackground`'a,
/// yeşil halka Image'ini `progressFill`'e ata (progressFill konsantrik, kanala oturacak boyutta).
/// Bu component progressFill'i Filled/Radial360'a ayarlar; fillAmount = CurrentStage/3.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class StreakBoosterMenuBadge : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Opsiyonel — badge arka plan görseli (AllianceHelp). Sadece görsel; boş olabilir.")]
    [SerializeField] private Image badgeBackground;
    [Tooltip("İki gold ring arasına oturan YEŞİL halka. Radial360 fill'e bu component ayarlar.")]
    [SerializeField] private Image progressFill;
    [Tooltip("Opsiyonel — '2/3' gibi stage yazısı.")]
    [SerializeField] private TMP_Text stageLabel;
    [Tooltip("Event level eşiğinden önce kilitli gösterilecek root. Boşsa bu nesnenin kendisi (paneli gizlemez).")]
    [SerializeField] private GameObject visibilityRoot;

    [Header("Fill")]
    [Tooltip("Dolumun tepe'den saat yönünde ilerlemesi (kapatırsan ters yön).")]
    [SerializeField] private bool clockwise = true;
    [Tooltip("Dolum değişiminin yumuşama süresi (sn). 0 = anında.")]
    [SerializeField, Min(0f)] private float fillLerpDuration = 0.35f;

    [Header("Halka (prosedürel — sprite ATAMA gerekmez)")]
    [Tooltip("Yeşil halkanın rengi. progressFill sprite'ını kod üretir; sen sadece rengi seç.")]
    [SerializeField] private Color ringColor = new Color(0.25f, 0.95f, 0.35f, 1f);
    [Tooltip("Halka DELİĞİ yarıçapı (0..1). Büyük = ince + DIŞA yakın halka. Kanal kenara yakınsa yükselt.")]
    [SerializeField, Range(0f, 0.98f)] private float innerRadius01 = 0.90f;
    [Tooltip("Halka DIŞ yarıçapı (0..1). Kenara kadar için ~0.99.")]
    [SerializeField, Range(0.1f, 1f)] private float outerRadius01 = 0.99f;
    [Tooltip("Kenar yumuşaklığı (0..0.1 texture oranı).")]
    [SerializeField, Range(0f, 0.1f)] private float edgeSoftness01 = 0.02f;

    private static readonly Color PlateColor = new Color(0.92f, 0.42f, 0.08f);   // alev turuncusu
    private static readonly Color TrackColor = new Color(0.18f, 0.08f, 0.02f, 0.55f);

    private int targetStage;
    private float displayedFill;
    private float velFill;
    private Sprite ownedRingSprite;
    private Texture2D ownedRingTexture;
    private Image progressTrack;

    private void Awake()
    {
        // Yalnız kendini yönetir: eskiden boşsa EBEVEYN paneli (LeftEventPanel) gizleniyordu → panel artık başka
        // etkinlikleri de (kilitli/açık) taşıdığı için level 30'a kadar hepsi kayboluyordu (2026-11-10).
        if (visibilityRoot == null)
            visibilityRoot = gameObject;

        if (progressFill != null)
        {
            // Halka sprite'ını KOD üretir (atama gerekmez); renk Image.color'dan gelir.
            progressFill.sprite = BuildRingSprite();
            progressFill.color = ringColor;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Radial360;
            progressFill.fillOrigin = (int)Image.Origin360.Bottom;
            progressFill.fillClockwise = clockwise;
            BuildTrack();
        }

        // Seri sayısı (1/3) diğer etkinlik ikonları gibi alttaki plakada; kilitliyken "SEVİYE N" de buraya yazılır.
        if (stageLabel == null) stageLabel = CreateStageLabel();
        EventIconPlate.Attach(stageLabel, PlateColor);
    }

    // Seri 0'dayken de halkanın "dolacak çubuk" olduğu okunsun: yeşil dolumun altında koyu, yarı saydam iz.
    private void BuildTrack()
    {
        var src = progressFill.rectTransform;
        var go = new GameObject("ProgressTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(src.parent, false);
        rt.SetSiblingIndex(src.GetSiblingIndex());   // dolumun hemen altında
        rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax; rt.pivot = src.pivot;
        rt.anchoredPosition = src.anchoredPosition; rt.sizeDelta = src.sizeDelta;
        var img = go.GetComponent<Image>();
        progressTrack = img;
        img.sprite = progressFill.sprite;   // aynı halka
        img.color = TrackColor;
        img.raycastTarget = false;
    }

    private TMP_Text CreateStageLabel()
    {
        var go = new GameObject("StageLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.fontSize = label.fontSizeMax = 22f;
        label.fontSizeMin = 14f;
        label.enableAutoSizing = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    // Prosedürel yeşil HALKA (annulus): merkezden uzaklığa göre iç/dış yarıçap arası opak, kenarlar yumuşak.
    // Beyaz üretilir; renk Image.color ile verilir. RectTransform boyutu kanala göre sahnede ayarlanır.
    private Sprite BuildRingSprite()
    {
        const int size = 256;
        float half = size * 0.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        ownedRingTexture = tex;
        // Write into the texture's native buffer; no temporary 1 MB managed Color array.
        var px = tex.GetPixelData<Color32>(0);
        float inner = Mathf.Clamp01(innerRadius01);
        float outer = Mathf.Clamp(outerRadius01, inner + 0.01f, 1f);
        float soft = Mathf.Max(0.0001f, edgeSoftness01);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - half) / half;   // -1..1
            float dy = (y + 0.5f - half) / half;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            // İç kenar: inner'da 0→1, dış kenar: outer'da 1→0 (yumuşak)
            float a = Mathf.Clamp01((r - inner) / soft) * Mathf.Clamp01((outer - r) / soft);
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.Apply(false, true); // Upload once and discard the CPU copy.
        ownedRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect);
        return ownedRingSprite;
    }

    private void OnDestroy()
    {
        if (progressFill != null && progressFill.sprite == ownedRingSprite) progressFill.sprite = null;
        if (progressTrack != null) Destroy(progressTrack.gameObject);
        if (ownedRingSprite != null) Destroy(ownedRingSprite);
        if (ownedRingTexture != null) Destroy(ownedRingTexture);
    }

    private void OnEnable()
    {
        PlayerStats.OnChanged += Refresh;
        RefreshImmediate();
    }

    private void OnDisable()
    {
        PlayerStats.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        // UFO ile AYNI kural: mevcut level'da gerçekten teslim edilecek stage (eşik altında 0).
        int globalLevel = CurrentLevel.Global;
        bool activeForLevel = StreakBoosterEvent.IsActiveForLevel(globalLevel);

        if (!activeForLevel)
        {
            targetStage = 0;
            // Açılış seviyesine kadar kilitli göster (EventLockOverlay, diğer etkinliklerle aynı).
            if (EventLockOverlay.Apply(visibilityRoot, StreakBoosterEvent.ThresholdLevel, stageLabel)) return;
            SetVisibility(false);
            return;
        }

        EventLockOverlay.Release(visibilityRoot);
        SetVisibility(true);
        targetStage = StreakBoosterEvent.EffectiveStage(globalLevel);
        if (stageLabel != null)
            stageLabel.text = $"{targetStage}/{StreakBoosterEvent.MaxStage}";
    }

    private void SetVisibility(bool visible)
    {
        if (visibilityRoot != null && visibilityRoot.activeSelf != visible)
            visibilityRoot.SetActive(visible);
    }

    private void RefreshImmediate()
    {
        Refresh();
        displayedFill = TargetFill;
        if (progressFill != null) progressFill.fillAmount = displayedFill;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ;
#endif
    }

    private float TargetFill =>
        StreakBoosterEvent.MaxStage > 0
            ? Mathf.Clamp01((float)targetStage / StreakBoosterEvent.MaxStage)
            : 0f;

    private void Update()
    {
        if (progressFill == null) return;

        float target = TargetFill;
        if (fillLerpDuration <= 0f)
        {
            displayedFill = target;
        }
        else if (!Mathf.Approximately(displayedFill, target))
        {
            displayedFill = Mathf.SmoothDamp(displayedFill, target, ref velFill, fillLerpDuration);
            if (Mathf.Abs(displayedFill - target) < 0.001f) displayedFill = target;
        }

        progressFill.fillAmount = displayedFill;
    }
}
