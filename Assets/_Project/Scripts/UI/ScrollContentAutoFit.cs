using UnityEngine;

/// <summary>
/// ScrollRect Content'ini, içindeki tüm çocukları (tile'lar, region'lar) kapsayacak şekilde
/// otomatik boyutlandırır ve ortalar. Böylece ScrollRect her yöne kaydırabilir.
///
/// Kullanım: Content objesine ekle → sağ tık component → "Fit Content To Children".
/// Tile'lar SABİT anchor (stretch DEĞİL) olmalı; yoksa Content büyüyünce şişerler.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class ScrollContentAutoFit : MonoBehaviour
{
    [Tooltip("Kenar boşluğu (her yöne, piksel). Etrafta biraz bulut/boşluk payı.")]
    [SerializeField] private float padding = 300f;
    [Tooltip("Çocukları ortalamak için kaydır (önerilir).")]
    [SerializeField] private bool recenterChildren = true;

    /// <summary>
    /// Tüm çocukları (stretch dahil) GÖRÜNÜMÜ BOZMADAN sabit-anchor (merkez) yapar.
    /// Stretch tile'lar Content boyutunu takip ettiği için, Fit'ten ÖNCE bir kez çalıştır.
    /// </summary>
    [ContextMenu("1) Bake Children To Fixed Anchors")]
    public void BakeChildrenToFixedAnchors()
    {
        var content = (RectTransform)transform;
        for (int i = 0; i < content.childCount; i++)
        {
            var c = content.GetChild(i) as RectTransform;
            if (c == null) continue;

            Vector2 size = c.rect.size;     // o anki görünen boyut
            Vector3 worldPos = c.position;  // o anki dünya konumu

            c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f);
            c.sizeDelta = size;
            c.position = worldPos;          // yerini koru
        }
        Debug.Log($"[ScrollContentAutoFit] {content.childCount} çocuk sabit anchor'a çevrildi.", this);
    }

    [ContextMenu("2) Fit Content To Children")]
    public void Fit()
    {
        var content = (RectTransform)transform;
        if (content.childCount == 0) return;

        // Tüm çocukların kapsayan kutusunu Content'in local uzayında hesapla.
        Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(content);

        Vector2 size = new Vector2(b.size.x, b.size.y) + Vector2.one * (padding * 2f);
        content.sizeDelta = size;

        if (recenterChildren)
        {
            // Çocukları, kutu merkezi Content merkezine gelecek şekilde topluca kaydır
            // (göreli yerleşim korunur).
            Vector2 shift = new Vector2(b.center.x, b.center.y);
            for (int i = 0; i < content.childCount; i++)
            {
                var c = content.GetChild(i) as RectTransform;
                if (c != null) c.anchoredPosition -= shift;
            }
        }

        Debug.Log($"[ScrollContentAutoFit] Content boyutu ayarlandı: {size} (çocuk kutusu {b.size}).", this);
    }
}
