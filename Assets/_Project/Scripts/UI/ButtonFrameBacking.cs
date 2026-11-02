using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kodla kurulan popup'ların alttaki yeşil/mavi butonlarına sarı çerçeve (PreLevel'deki ButtonFrame ile aynı oran:
/// buton 500×200 ↔ çerçeve 538.5×234). Çerçeve butonun ESKİ boyunu aşmaz (popup dışına taşmasın): çerçeve
/// görselin eski yüksekliğini alır, buton aynı oranla içeride küçülür (ölçek 200/234).
/// Çerçeve butonun hemen ÖNÜNDEKİ kardeştir → butonun arkasında çizilir ve butonun nefes alma/basma ölçeğine
/// katılmaz (PreLevel'de de çerçeve sabit, buton içinde nefes alır).
/// ORAN (2026-11-02, market/kampanya butonları 300×94 ≈ 3.2): kenar payları butonun YÜKSEKLİĞİNE göre hesaplanır
/// (PreLevel: yanlarda 0.096·H, üst/altta 0.085·H) ve çerçeve 9-slice çizilir (köşe/kenar kalınlığı yüksekliğe
/// göre ölçeklenir) → buton geniş ya da dar olsun çerçeve gerilip basıklaşmaz, PreLevel'dekiyle aynı görünür.
/// </summary>
[DisallowMultipleComponent]
public sealed class ButtonFrameBacking : MonoBehaviour
{
    private const float HeightRatio = 234f / 200f;
    /// Buton ölçeği: çerçeve yüksekliği = butonun eski yüksekliği.
    public const float ButtonFit = 1f / HeightRatio;
    public Vector3 ButtonScale => new Vector3(ButtonFit * outerScale, ButtonFit * outerScale, 1f);
    // PreLevel 500×200 butonda yan pay 19.25 = 0.09625·H (H = buton yüksekliği).
    private const float SideMarginPerHeight = (538.5f - 500f) * 0.5f / 200f;

    private RectTransform rt;
    private RectTransform slot;          // butonun yuvasının kopyası (çapa/ofset)
    private AspectRatioFitter fit;       // yuvada görselin oturduğu dikdörtgen
    private Image frame;
    private RectTransform frameRt;
    private float outerScale = 1f;
    private Vector2 lastRectSize;

    /// outerScale: butonun kendi dış ölçeği (ör. market üçlü kartında sütuna sığdırma). Çerçeve de aynı ölçeği alır.
    public static void Attach(Button button, Sprite frameSprite, float outerScale = 1f)
    {
        if (button == null || frameSprite == null) return;
        if (!button.TryGetComponent(out ButtonFrameBacking backing))
            backing = button.gameObject.AddComponent<ButtonFrameBacking>();
        backing.outerScale = Mathf.Max(0.01f, outerScale);
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
            // Butonun ebeveyninde Layout Group varsa çerçeve yuvası düzene katılıp butonu kaydırmasın.
            slotGo.AddComponent<LayoutElement>().ignoreLayout = true;

            var fitGo = new GameObject("Fit", typeof(RectTransform), typeof(AspectRatioFitter));
            fitGo.layer = gameObject.layer;
            var fitRt = (RectTransform)fitGo.transform;
            fitRt.SetParent(slot, false);
            fit = fitGo.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            var imgGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imgGo.layer = gameObject.layer;
            frameRt = (RectTransform)imgGo.transform;
            frameRt.SetParent(fitRt, false);
            frameRt.offsetMin = frameRt.offsetMax = Vector2.zero;   // çapalar Sync'te butonun oranına göre
            frame = imgGo.GetComponent<Image>();
            frame.raycastTarget = false;
        }
        frame.sprite = frameSprite;
        bool sliced = frameSprite.border != Vector4.zero;
        frame.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        frame.fillCenter = true;
        slot.localScale = new Vector3(outerScale, outerScale, 1f);
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
            || slot.GetSiblingIndex() != rt.GetSiblingIndex() - 1 || rt.rect.size != lastRectSize || slot.pivot != rt.pivot)
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
        lastRectSize = rt.rect.size;
        LayoutFrame();
    }

    // Fit = butonun ESKİ görsel dikdörtgeni (W×H). Buton içeride ButtonFit'e küçülür; çerçeve yüksekliği = H, genişliği
    // = küçülmüş buton + iki yanda 0.096·(küçülmüş yükseklik). Yüksekliğe bağlı pay → her en/boy oranında aynı kalınlık.
    private void LayoutFrame()
    {
        if (frameRt == null || fit == null) return;
        float aspect = Mathf.Max(0.1f, fit.aspectRatio);   // W / H
        float widthFrac = ButtonFit * (1f + 2f * SideMarginPerHeight / aspect);
        float ex = (widthFrac - 1f) * 0.5f, ey = (HeightRatio * ButtonFit - 1f) * 0.5f;
        frameRt.anchorMin = new Vector2(-ex, -ey);
        frameRt.anchorMax = new Vector2(1f + ex, 1f + ey);
        frameRt.offsetMin = frameRt.offsetMax = Vector2.zero;
        // Buton PİVOTUNA göre küçülür: pivot merkezde değilse (market fiyat butonu sağ-alt) küçülen buton o köşeye kayar.
        // Çerçeveyi küçülmüş butonun merkezine taşı: kayma = (pivot − 0.5)·boyut·(1 − ButtonFit).
        Vector2 pivotOffset = rt.pivot - new Vector2(0.5f, 0.5f);
        frameRt.anchoredPosition = Vector2.Scale(pivotOffset, rt.rect.size) * (1f - ButtonFit);

        // 9-slice: köşe/kenar kalınlığı çerçeve yüksekliğiyle orantılı (Simple çizimde olacağı gibi), yalnız orta uzar.
        if (frame != null && frame.type == Image.Type.Sliced && frame.sprite != null)
        {
            Vector2 size = rt.rect.size;
            float fitH = Mathf.Min(size.y, size.x / aspect) * HeightRatio * ButtonFit;
            if (fitH > 1f)
            {
                float refPpu = frame.canvas != null ? frame.canvas.referencePixelsPerUnit : 100f;
                frame.pixelsPerUnitMultiplier = frame.sprite.rect.height / fitH * refPpu / frame.sprite.pixelsPerUnit;
            }
        }
    }
}
