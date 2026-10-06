using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 9-slice kenarlarını Image yüksekliğine göre ölçekler: yuvarlak uçlar ve çerçeve, buton
/// kısa da olsa uzun da olsa aynı ORANDA kalır (kısa butonda kalın, uzunda ince görünmez).
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class SlicedHeightScaler : MonoBehaviour
{
    private Image image;
    private float lastHeight = -1f;

    private void Awake() => image = GetComponent<Image>();
    private void OnRectTransformDimensionsChange() => Refresh();

    public void Refresh()
    {
        if (image == null) image = GetComponent<Image>();
        if (image == null || image.sprite == null) return;
        float h = ((RectTransform)transform).rect.height;
        if (h <= 1f || Mathf.Approximately(h, lastHeight)) return;
        lastHeight = h;
        // Sprite tam yüksekliği butonun yüksekliğine eşlenir → kenarlar butonla orantılı.
        image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, image.sprite.rect.height / h);
    }
}
