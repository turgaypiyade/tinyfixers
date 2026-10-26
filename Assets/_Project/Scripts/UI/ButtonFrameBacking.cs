using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kodla kurulan popup'ların alttaki yeşil/mavi butonlarına sarı çerçeve (PreLevel'deki ButtonFrame ile aynı oran:
/// buton 500×200 ↔ çerçeve 538.5×234). Çerçeve butonun ESKİ boyunu aşmaz (popup dışına taşmasın): çerçeve
/// görselin eski yüksekliğini alır, buton aynı oranla içeride küçülür (ölçek 200/234).
/// Çerçeve butonun hemen ÖNÜNDEKİ kardeştir → butonun arkasında çizilir ve butonun nefes alma/basma ölçeğine
/// katılmaz (PreLevel'de de çerçeve sabit, buton içinde nefes alır).
/// </summary>
[DisallowMultipleComponent]
public sealed class ButtonFrameBacking : MonoBehaviour
{
    private const float WidthRatio = 538.5f / 500f;
    private const float HeightRatio = 234f / 200f;
    /// Buton ölçeği: çerçeve yüksekliği = butonun eski yüksekliği.
    public const float ButtonFit = 1f / HeightRatio;
    public Vector3 ButtonScale => new Vector3(ButtonFit, ButtonFit, 1f);

    private RectTransform rt;
    private RectTransform slot;          // butonun yuvasının kopyası (çapa/ofset)
    private AspectRatioFitter fit;       // yuvada görselin oturduğu dikdörtgen
    private Image frame;

    public static void Attach(Button button, Sprite frameSprite)
    {
        if (button == null || frameSprite == null) return;
        if (!button.TryGetComponent(out ButtonFrameBacking backing))
            backing = button.gameObject.AddComponent<ButtonFrameBacking>();
        backing.Build(frameSprite);
    }

    private void Build(Sprite frameSprite)
    {
        rt = (RectTransform)transform;
        if (slot == null)
        {
            var slotGo = new GameObject(name + "_Frame", typeof(RectTransform));
            slotGo.layer = gameObject.layer;   // runtime UI: kamera görsün
            slot = (RectTransform)slotGo.transform;

            var fitGo = new GameObject("Fit", typeof(RectTransform), typeof(AspectRatioFitter));
            fitGo.layer = gameObject.layer;
            var fitRt = (RectTransform)fitGo.transform;
            fitRt.SetParent(slot, false);
            fit = fitGo.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            var imgGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imgGo.layer = gameObject.layer;
            var imgRt = (RectTransform)imgGo.transform;
            imgRt.SetParent(fitRt, false);
            // Fit = butonun ESKİ görsel dikdörtgeni; çerçeve onun içinde (yükseklik tam, genişlik oranla biraz dar).
            float ex = (WidthRatio * ButtonFit - 1f) * 0.5f, ey = (HeightRatio * ButtonFit - 1f) * 0.5f;
            imgRt.anchorMin = new Vector2(-ex, -ey);
            imgRt.anchorMax = new Vector2(1f + ex, 1f + ey);
            imgRt.offsetMin = imgRt.offsetMax = Vector2.zero;
            frame = imgGo.GetComponent<Image>();
            frame.raycastTarget = false;
        }
        frame.sprite = frameSprite;
        rt.localScale = ButtonScale;   // buton çerçevenin içine küçülür
        if (TryGetComponent(out ButtonShine shine)) shine.SetBaseScale(rt.localScale);   // nefes alma bu ölçekten
        Sync();
        slot.gameObject.SetActive(isActiveAndEnabled);
    }

    private void OnEnable()
    {
        if (slot == null) return;
        Sync();
        slot.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (slot != null) slot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (slot != null) Destroy(slot.gameObject);
    }

    // Yuva kodla sonradan yerleştirilir (Region/ActionRegion): değişince kopyala. Birkaç Vector2 karşılaştırması.
    private void LateUpdate()
    {
        if (slot == null) return;
        if (slot.parent != rt.parent || slot.anchorMin != rt.anchorMin || slot.anchorMax != rt.anchorMax
            || slot.anchoredPosition != rt.anchoredPosition || slot.sizeDelta != rt.sizeDelta
            || slot.GetSiblingIndex() != rt.GetSiblingIndex() - 1)
            Sync();
    }

    private void Sync()
    {
        if (slot.parent != rt.parent) slot.SetParent(rt.parent, false);
        slot.anchorMin = rt.anchorMin;
        slot.anchorMax = rt.anchorMax;
        slot.pivot = rt.pivot;
        slot.anchoredPosition = rt.anchoredPosition;
        slot.sizeDelta = rt.sizeDelta;
        int index = rt.GetSiblingIndex();
        if (slot.GetSiblingIndex() != index - 1)
            slot.SetSiblingIndex(slot.GetSiblingIndex() < index ? index - 1 : index);

        var art = GetComponent<Image>();
        if (art != null && art.sprite != null && art.sprite.rect.height > 0f)
            fit.aspectRatio = art.sprite.rect.width / art.sprite.rect.height;
    }
}
