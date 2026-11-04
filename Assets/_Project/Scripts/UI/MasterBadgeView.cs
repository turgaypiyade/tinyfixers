using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Usta Ligi rozeti çipi: bir hedef görselin (ör. profil avatarı) sağ alt köşesine en değerli rozeti ve 2+ ise "×N"
/// sayısını koyar. Rozet yoksa gizli. Kurulum bir kez (Attach); Refresh yalnız açılışta/ödül alınınca çağrılır.
/// Takım/sıralama satırlarında da aynı bileşen kullanılacak (başka oyuncunun rozeti Firestore alanı gelince).
/// </summary>
public sealed class MasterBadgeView : MonoBehaviour
{
    private const float SizeOfTarget = 0.42f;   // hedefin kısa kenarına oranla rozet boyu

    private Image icon;
    private TMP_Text count;

    public static MasterBadgeView Attach(RectTransform target)
    {
        if (target == null) return null;
        var existing = target.GetComponentInChildren<MasterBadgeView>(true);
        if (existing != null) return existing;

        var go = new GameObject("MasterBadge", typeof(RectTransform));
        go.layer = target.gameObject.layer;   // runtime UI: kamera görsün
        go.transform.SetParent(target, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.75f, 0.25f);
        rt.anchoredPosition = Vector2.zero;
        float side = Mathf.Min(target.rect.width, target.rect.height);
        if (side <= 1f) side = 200f;
        rt.sizeDelta = Vector2.one * side * SizeOfTarget;
        return go.AddComponent<MasterBadgeView>().Build();
    }

    private MasterBadgeView Build()
    {
        icon = gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var label = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.layer = gameObject.layer;
        label.transform.SetParent(transform, false);
        var lrt = (RectTransform)label.transform;
        lrt.anchorMin = new Vector2(0.45f, -0.05f);
        lrt.anchorMax = new Vector2(1.25f, 0.35f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        count = label.GetComponent<TextMeshProUGUI>();
        count.alignment = TextAlignmentOptions.Right;
        count.enableAutoSizing = true;
        count.fontSizeMax = 44f;
        count.fontSizeMin = 16f;
        count.raycastTarget = false;
        TextSurfaceStyle.Apply(count, TextSurface.Blue);   // beyaz yüz + lacivert kontur (rozet kurdelesi mavi)
        Refresh();
        return this;
    }

    public void Refresh()
    {
        var best = MasterLeagueRewards.Best;
        bool show = best != MasterBadge.None;
        icon.enabled = show;
        count.enabled = show && MasterLeagueRewards.Count(best) > 1;
        if (!show) return;
        icon.sprite = MasterLeagueRewards.SpriteOf(best);
        if (count.enabled) count.text = "×" + MasterLeagueRewards.Count(best);
    }
}
