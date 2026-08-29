using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// JokerGrid'deki booster slotunun kilit / free-oyun / hak sayacı görselleri.
/// PreLevelSpecialSlotView ile aynı üç-durum kuralı (durumlar birbirini dışlar):
///   kilitli → lockOverlay (ikon açık kalır) · free oyun → freeBadge · normal → numberBG+count.
/// Kural verisi BoosterAccessService'ten gelir; bu component yalnızca görseldir.
/// </summary>
public class BoosterSlotView : MonoBehaviour
{
    [Tooltip("-1 = aynı objedeki JokerBoosterSlotMapping.BoosterIndex kullanılır. " +
             "(0=Hammer, 1=Row, 2=Column, 3=Shuffle)")]
    [SerializeField] private int boosterIndex = -1;

    [Header("UI")]
    [Tooltip("Hak sayacının arka planı.")]
    [SerializeField] private Image numberBG;
    [Tooltip("Hak sayacı metni.")]
    [SerializeField] private TMP_Text countText;
    [Tooltip("Kilitliyken numberBG/count YERİNE gösterilecek görsel (lock image).")]
    [SerializeField] private GameObject lockOverlay;
    [Tooltip("Free oyunda numberBG/count YERİNE gösterilecek rozet. Altındaki TMP text " +
             "'prelevel_special_free' anahtarından lokalize edilir.")]
    [SerializeField] private GameObject freeBadge;
    [Tooltip("Booster ikonu — kilitliyken grileşir. Boşsa adında 'icon' geçen child aranır.")]
    [SerializeField] private Image iconImage;
    [Tooltip("Kilitliyken ikona uygulanan gri tint (açılınca beyaza döner).")]
    [SerializeField] private Color lockedIconTint = new Color(0.55f, 0.55f, 0.55f, 1f);

    private TMP_Text freeBadgeLabel;   // freeBadge altındaki text — lazily bulunur
    private int resolvedIndex = -1;

    /// <summary>Bu slotun booster index'i (0=Hammer, 1=Row, 2=Column, 3=Shuffle). -1 = çözülemedi.</summary>
    public int ResolvedIndex => resolvedIndex;

    /// <summary>Slotun ikon Image'inin RectTransform'u (uçuş efektleri başlangıç noktası için).</summary>
    public RectTransform IconRect => iconImage != null ? iconImage.rectTransform : null;

    private void Awake()
    {
        resolvedIndex = ResolveIndex();
        if (resolvedIndex < 0)
            Debug.LogError($"[BoosterSlotView] '{name}': booster index çözülemedi! Inspector'dan " +
                           "boosterIndex (0-3) ya da JokerBoosterSlotMapping.BoosterIndex ayarla.", this);

        AutoFindMissingRefs();
        ApplyBoosterIcon();
    }

    // Booster ikonu tek kaynaktan: TileIconLibrary.Shared. Library'de sprite yoksa
    // sahnedeki elle atanmış ikon korunur (regresyon yok).
    private void ApplyBoosterIcon()
    {
        if (iconImage == null || resolvedIndex < 0)
            return;

        var lib = TileIconLibrary.Shared;
        if (lib == null)
            return;

        var sprite = lib.GetBoosterIcon(BoosterAccessService.ToMode(resolvedIndex));
        if (sprite != null)
            iconImage.sprite = sprite;
    }

    // Index çözüm sırası: serialized alan → mapping (self/parent) → obje adındaki sayı (Slot1→0).
    private int ResolveIndex()
    {
        if (boosterIndex >= 0)
            return boosterIndex;

        var mapping = GetComponentInParent<JokerBoosterSlotMapping>();
        if (mapping != null && mapping.IsBoosterSlot && mapping.BoosterIndex >= 0)
            return mapping.BoosterIndex;

        // "Slot1".."Slot4" gibi isimlerden: sondaki sayı - 1.
        string n = gameObject.name;
        int end = -1;
        for (int i = n.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(n[i])) { end = i; break; }
        }
        if (end >= 0)
        {
            int start = end;
            while (start > 0 && char.IsDigit(n[start - 1])) start--;
            if (int.TryParse(n.Substring(start, end - start + 1), out int num) && num >= 1 && num <= 4)
                return num - 1;
        }

        return -1;
    }

    // Inspector'da bağlanmamış referansları çocuklardan isimle bul (bağlıysa dokunma).
    private void AutoFindMissingRefs()
    {
        if (numberBG == null) numberBG = FindChild<Image>("numberbg");
        if (countText == null) countText = FindChild<TMP_Text>("counttxt", "counttext", "count");
        if (lockOverlay == null) lockOverlay = FindChildObject("lockimage", "lockoverlay", "lock");
        if (freeBadge == null) freeBadge = FindChildObject("freebadge", "freebage", "free");
        // İkon child'ı sahnede "Joker_1"/"Slot..." gibi adlanabiliyor → sadece "icon" adına güvenme.
        if (iconImage == null) iconImage = FindChild<Image>("icon", "joker", "special", "booster");
        if (iconImage == null) iconImage = FindIconFallback();
    }

    // Bilinen alt-parçalar (numberbg/lock/free/count/frame/glow) DIŞINDAKİ ilk child Image = ikon.
    // İkon objesinin adı ne olursa olsun (ör. "Joker_1") bulur.
    private Image FindIconFallback()
    {
        foreach (var c in GetComponentsInChildren<Image>(true))
        {
            if (c == null || c.transform == transform) continue;
            if (numberBG != null && c == numberBG) continue;

            string cn = c.gameObject.name.ToLowerInvariant();
            if (cn.Contains("number") || cn.Contains("count") || cn.Contains("lock") ||
                cn.Contains("free") || cn.Contains("badge") || cn.Contains("frame") ||
                cn.Contains("glow") || cn.Contains("bg"))
                continue;

            return c;
        }
        return null;
    }

    private T FindChild<T>(params string[] nameKeys) where T : Component
    {
        foreach (var c in GetComponentsInChildren<T>(true))
        {
            if (c == null || c.transform == transform) continue;
            string cn = c.gameObject.name.ToLowerInvariant();
            foreach (var key in nameKeys)
                if (cn.Contains(key))
                    return c;
        }
        return null;
    }

    private GameObject FindChildObject(params string[] nameKeys)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == transform) continue;
            string tn = t.gameObject.name.ToLowerInvariant();
            foreach (var key in nameKeys)
                if (tn.Contains(key))
                    return t.gameObject;
        }
        return null;
    }

    private void OnEnable()
    {
        BoosterInventory.OnCountChanged += HandleCountChanged;
        BoosterAccessService.OnFreeConsumed += HandleFreeConsumed;
        GameLocalization.OnLanguageChanged += RefreshVisuals;
        RefreshVisuals();
    }

    private void OnDisable()
    {
        BoosterInventory.OnCountChanged -= HandleCountChanged;
        BoosterAccessService.OnFreeConsumed -= HandleFreeConsumed;
        GameLocalization.OnLanguageChanged -= RefreshVisuals;
    }

    private void HandleFreeConsumed(int index)
    {
        if (index == resolvedIndex)
            RefreshVisuals();
    }

    private void HandleCountChanged(BoardController.BoosterMode mode, int count)
    {
        if (BoosterAccessService.ToIndex(mode) == resolvedIndex)
            RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        // Index çözülemediyse sahnedeki editor-durumunu (hepsi açık) bırakma —
        // güvenli taraf: kilitli görünüm. (Awake'te error loglandı.)
        if (resolvedIndex < 0)
        {
            if (lockOverlay != null) lockOverlay.SetActive(true);
            if (freeBadge != null) freeBadge.SetActive(false);
            if (numberBG != null) numberBG.gameObject.SetActive(false);
            if (countText != null) countText.gameObject.SetActive(false);
            return;
        }

        ApplyBoosterIcon();

        bool unlocked = BoosterAccessService.IsUnlocked(resolvedIndex);
        bool free = unlocked && BoosterAccessService.IsFreeThisGame(resolvedIndex);
        bool showCount = unlocked && !free;

        if (lockOverlay != null)
            lockOverlay.SetActive(!unlocked);

        // Kilitliyken ikon grileşir.
        if (iconImage != null)
            iconImage.color = unlocked ? Color.white : lockedIconTint;

        if (freeBadge != null)
            freeBadge.SetActive(free);

        if (free)
            RefreshFreeBadgeLabel();

        if (numberBG != null)
            numberBG.gameObject.SetActive(showCount);

        if (countText != null)
        {
            countText.gameObject.SetActive(showCount);
            if (showCount)
                countText.text = BoosterInventory.GetCount(BoosterAccessService.ToMode(resolvedIndex)).ToString();
        }
    }

    private void RefreshFreeBadgeLabel()
    {
        if (freeBadge == null)
            return;

        if (freeBadgeLabel == null)
            freeBadgeLabel = freeBadge.GetComponentInChildren<TMP_Text>(true);

        if (freeBadgeLabel != null)
            freeBadgeLabel.text = GameLocalization.Get("prelevel_special_free");
    }
}
