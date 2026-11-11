using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menü etkinlik butonları için kilitli görünüm (kullanıcı 2026-11-10, Knock Out referansı): açılış seviyesine
/// gelmeden buton gizlenmez; gri tonlanır, köşede altın kilit görünür ve butonun KENDİ süre yazısına (alttaki yazı
/// kutusu) "SEVİYE N" yazılır. Kök aktif kalır (panel düzeninde yerini tutar). Dokunuş etkinliği açmaz, kilidi sallar.
/// Seviye gelince kilit kaldırılır, sonrası etkinliğin kendi takvimidir.
/// Kullanım: butonun gizleme noktasında <c>if (EventLockOverlay.Apply(root, gate, timeLabel)) return;</c>.
/// Kilitliyken LateUpdate yazıyı korur (buton kendi Update'inde süre yazmaya çalışsa da); sallanma Update'te.
/// </summary>
public sealed class EventLockOverlay : MonoBehaviour
{
    private const float ShakeSeconds = 0.45f;
    private const float FallbackLabelSizeRatio = 22f / 150f;   // etkinlik ikonlarının süre yazısı (22pt / 150px)
    private static readonly Color LabelSurface = new Color(0.22f, 0.22f, 0.26f, 1f);

    private static Material grayMaterial;
    private static Sprite lockSprite;

    private readonly List<Graphic> grayed = new();
    private readonly List<Material> originalMaterials = new();
    private readonly List<GameObject> hiddenTexts = new();
    private RectTransform layer, lockIcon;
    private TMP_Text label;          // butonun süre yazısı; yoksa kendi yedek etiketimiz
    private LabelStyle hostStyle;    // butonun yazısının kilit öncesi görünümü (TinyFixers Pop'tan geri dönüş)
    private bool hostStyled;
    private TmpOutline disabledOutline;
    private bool keepTexts;
    private readonly List<TMP_Text> tintedTexts = new();
    private readonly List<Color> tintedColors = new();
    private static readonly Color TextGray = new Color(0.78f, 0.78f, 0.78f, 1f);

    private struct LabelStyle
    {
        public TMP_FontAsset font;
        public Material material;
        public float size, sizeMin, sizeMax;
        public bool autoSize, gradient, extraPadding;
        public FontStyles style;
        public Color color;
    }
    private string lockText;
    private int level = -1;
    private float shakeT = -1f;

    public static bool IsLocked(int unlockLevel) => CurrentLevel.Global < unlockLevel;

    /// Seviye kapısı kapalıysa kökü kilitli gösterir ve true döner (çağıran gizleme/gösterme yapmasın).
    /// Kapı açıksa varsa kilidi kaldırır ve false döner (çağıran normal akışına devam eder).
    /// <paramref name="timeLabel"/>: butonun alttaki süre yazısı; "SEVİYE N" oraya yazılır (yoksa yedek etiket kurulur).
    /// <paramref name="keepTexts"/>: diğer yazılar gizlenmez, griye boyanır (ör. ilerleme çubuğunun hedef/ödül sayıları).
    public static bool Apply(GameObject root, int unlockLevel, TMP_Text timeLabel = null, bool keepTexts = false)
    {
        if (root == null) return false;
        root.TryGetComponent(out EventLockOverlay overlay);
        if (!IsLocked(unlockLevel))
        {
            if (overlay != null) overlay.Clear();
            return false;
        }
        if (!root.activeSelf) root.SetActive(true);
        if (overlay == null) overlay = root.AddComponent<EventLockOverlay>();
        overlay.keepTexts = keepTexts;
        overlay.Lock(unlockLevel, timeLabel);
        return true;
    }

    /// Kökte kilit varsa kaldırır (etkinlik gerçekten görünür olduğunda, ör. editör debug zorlaması).
    public static void Release(GameObject root)
    {
        if (root != null && root.TryGetComponent(out EventLockOverlay overlay)) overlay.Clear();
    }

    private void Lock(int unlockLevel, TMP_Text timeLabel)
    {
        if (layer != null && level == unlockLevel) return;
        level = unlockLevel;
        if (layer == null) Build(timeLabel);
        GrayOut();
        lockText = GameLocalization.GetFormat("event_locked_level", unlockLevel);
        if (label != null) label.text = lockText;
        layer.SetAsLastSibling();
        enabled = true;   // LateUpdate yazıyı korur
    }

    private void Build(TMP_Text timeLabel)
    {
        if (grayMaterial == null) grayMaterial = Resources.Load<Material>("UIEffects/UIGrayscale");
        if (lockSprite == null) lockSprite = Resources.Load<Sprite>("UIEffects/EventLock");
        int uiLayer = gameObject.layer;   // runtime UI: ebeveyn katmanı (kamera culling)

        layer = NewRect("LockLayer", transform, uiLayer);
        layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one;
        layer.offsetMin = layer.offsetMax = Vector2.zero;
        var catcher = layer.gameObject.AddComponent<Image>();
        catcher.color = new Color(1f, 1f, 1f, 0f);   // görünmez: alttaki butona dokunuş gitmesin
        var tap = layer.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(Shake);

        Vector2 size = ((RectTransform)transform).rect.size;
        float side = Mathf.Max(24f, Mathf.Min(size.x, size.y));

        lockIcon = NewRect("Lock", layer, uiLayer);
        lockIcon.anchorMin = lockIcon.anchorMax = new Vector2(0.86f, 0.84f);
        lockIcon.sizeDelta = Vector2.one * side * 0.42f;
        var lockImage = lockIcon.gameObject.AddComponent<Image>();
        lockImage.sprite = lockSprite;
        lockImage.preserveAspect = true;
        lockImage.raycastTarget = false;

        if (timeLabel != null && timeLabel.transform.IsChildOf(transform))
        {
            label = timeLabel;   // butonun kendi yazı kutusu: "SEVİYE N" oraya, TinyFixers Pop ile sığar
            StyleHostLabel();
            return;
        }

        var labelRect = NewRect("Level", layer, uiLayer);
        // Diğer etkinlik ikonlarının alt yazı kutusuyla aynı ölçü (150'lik ikonda %16–84 genişlik, 22 punto).
        labelRect.anchorMin = new Vector2(0.16f, 0.10f);
        labelRect.anchorMax = new Vector2(0.84f, 0.26f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var own = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        own.alignment = TextAlignmentOptions.Center;
        own.textWrappingMode = TextWrappingModes.NoWrap;
        own.raycastTarget = false;
        ApplyPop(own, side * FallbackLabelSizeRatio);   // süre kutusu olmayan rozet (Seri): diğerleriyle aynı TinyFixers Pop stili
        label = own;
    }

    // Kilitliyken: TinyFixers Pop + koyu gri kontur (gri zemin), kutuya sığsın diye otomatik boyut (üst sınır eski punto).
    private void StyleHostLabel()
    {
        hostStyle = new LabelStyle
        {
            font = label.font, material = label.fontSharedMaterial,
            size = label.fontSize, sizeMin = label.fontSizeMin, sizeMax = label.fontSizeMax,
            autoSize = label.enableAutoSizing, gradient = label.enableVertexGradient, extraPadding = label.extraPadding,
            style = label.fontStyle, color = label.color,
        };
        hostStyled = true;
        // TextSurfaceStyle.Apply eski TmpOutline'ı siler; geçici değişimde silmek yerine kapatılır, kilit açılınca döner.
        if (label.TryGetComponent(out TmpOutline outline) && outline.enabled) { outline.enabled = false; disabledOutline = outline; }
        ApplyPop(label, hostStyle.size > 0f ? hostStyle.size : label.fontSizeMax);
    }

    // Tek stil yolu (süre kutusu da yedek etiket de): TinyFixers Pop, beyaz yüz, koyu gri kontur, kutuya sığan otomatik boyut.
    private static void ApplyPop(TMP_Text text, float maxSize)
    {
        var pop = CommonPopupSkin.Shared != null ? CommonPopupSkin.Shared.titleFont : null;
        if (pop == null || pop.material == null) return;
        text.font = pop;
        text.fontSharedMaterial = TextSurfaceStyle.RuntimeMaterial(pop, LabelSurface);
        text.fontStyle &= ~FontStyles.Bold;   // Pop zaten kalın
        text.enableVertexGradient = false;
        text.color = TextSurfaceStyle.Face;
        text.extraPadding = true;
        text.enableAutoSizing = true;
        text.fontSizeMax = maxSize;
        text.fontSizeMin = maxSize * 0.5f;
    }

    private void RestoreHostLabel()
    {
        if (!hostStyled || label == null) return;
        hostStyled = false;
        label.font = hostStyle.font;
        label.fontSharedMaterial = hostStyle.material;
        label.enableAutoSizing = hostStyle.autoSize;
        label.fontSize = hostStyle.size;
        label.fontSizeMin = hostStyle.sizeMin;
        label.fontSizeMax = hostStyle.sizeMax;
        label.enableVertexGradient = hostStyle.gradient;
        label.extraPadding = hostStyle.extraPadding;
        label.fontStyle = hostStyle.style;
        label.color = hostStyle.color;
        if (disabledOutline != null) disabledOutline.enabled = true;
        disabledOutline = null;
    }

    // Kökteki görseller gri malzemeye alınır; süre yazısı dışındaki yazılar (sayaç/rozet) gizlenir. Katman hariç.
    private void GrayOut()
    {
        RestoreVisuals();
        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null || graphic.transform.IsChildOf(layer)) continue;
            if (graphic is TMP_Text text)
            {
                if (text == label)
                {
                    if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
                    continue;
                }
                if (keepTexts)
                {
                    tintedTexts.Add(text);
                    tintedColors.Add(text.color);
                    text.color = TextGray;
                    continue;
                }
                if (text.gameObject.activeSelf) { text.gameObject.SetActive(false); hiddenTexts.Add(text.gameObject); }
                continue;
            }
            if (grayMaterial == null) continue;
            grayed.Add(graphic);
            originalMaterials.Add(graphic.material == graphic.defaultMaterial ? null : graphic.material);
            graphic.material = grayMaterial;
        }
    }

    private void RestoreVisuals()
    {
        for (int i = 0; i < grayed.Count; i++)
            if (grayed[i] != null) grayed[i].material = originalMaterials[i];
        grayed.Clear();
        originalMaterials.Clear();
        foreach (var go in hiddenTexts) if (go != null) go.SetActive(true);
        hiddenTexts.Clear();
        for (int i = 0; i < tintedTexts.Count; i++)
            if (tintedTexts[i] != null) tintedTexts[i].color = tintedColors[i];
        tintedTexts.Clear();
        tintedColors.Clear();
    }

    private void Clear()
    {
        RestoreVisuals();
        bool ownLabel = label != null && layer != null && label.transform.IsChildOf(layer);
        RestoreHostLabel();
        if (label != null && !ownLabel) label.text = "";   // buton kendi süresini yeniden yazar
        if (layer != null) Destroy(layer.gameObject);
        layer = null;
        Destroy(this);
    }

    private void Shake() => shakeT = 0f;

    private void Update()
    {
        if (shakeT < 0f || lockIcon == null) return;
        shakeT += Time.unscaledDeltaTime / ShakeSeconds;
        float t = Mathf.Clamp01(shakeT);
        lockIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 16f * (1f - t));
        if (t >= 1f) { lockIcon.localRotation = Quaternion.identity; shakeT = -1f; }
    }

    // Buton kendi Update'inde süre yazsa bile kilitliyken "SEVİYE N" kalır.
    private void LateUpdate()
    {
        if (label != null && lockText != null && label.text != lockText) label.text = lockText;
    }

    private static RectTransform NewRect(string name, Transform parent, int uiLayer)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = uiLayer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }
}
