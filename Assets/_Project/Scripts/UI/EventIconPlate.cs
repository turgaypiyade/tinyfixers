using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menü etkinlik ikonunun alt yazı plakası (kullanıcı 2026-11-26, tek şablon ikonlar): ikon görselinde şerit yok;
/// süre/durum yazısı ikonun altına, halkaya hafif binen ALTIN ÇERÇEVELİ renkli hap plakaya taşınır.
/// Plaka = Resources/UI/EventPlate_Fill (beyaz, renk Image.color ile) + EventPlate_Rim (altın çerçeve). Yazı kuralı:
/// beyaz yüz + plakanın koyusu kontur (TextSurfaceStyle). Yazı boşken plaka gizlenir (TMP metin olayı + CanvasRenderer alfası; Update yok).
/// Kullanım: ikon butonunun Awake/OnEnable'ında <c>EventIconPlate.Attach(labelText, renk)</c> (tekrar çağrı zararsız).
/// </summary>
public sealed class EventIconPlate : MonoBehaviour
{
    private const string PlateName = "EventPlate";
    // İkon kutusuna göre plaka alanı (150 px ikonda ~126×39): ikon kutusunun altında, halkanın alt kısmına biner.
    private static readonly Vector2 AnchorMin = new Vector2(0.08f, 0f);
    private static readonly Vector2 AnchorMax = new Vector2(0.92f, 0.26f);

    private static Sprite fillSprite, rimSprite;

    private TMP_Text label;
    private Image fill, rim;

    public static void Attach(TMP_Text label, Color color)
    {
        if (label == null) return;
        var host = label.transform.parent as RectTransform;
        if (host == null) return;
        if (label.transform.parent.name == PlateName) return;   // zaten taşındı

        if (fillSprite == null) fillSprite = Resources.Load<Sprite>("UI/EventPlate_Fill");
        if (rimSprite == null) rimSprite = Resources.Load<Sprite>("UI/EventPlate_Rim");
        if (fillSprite == null || rimSprite == null) return;   // görsel yoksa eski yerleşim kalır

        var go = new GameObject(PlateName, typeof(RectTransform));
        go.layer = host.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(host, false);
        rt.SetSiblingIndex(label.transform.GetSiblingIndex());   // yazının yerini alır: ikon görselinin üstünde, rozetlerin altında
        rt.anchorMin = AnchorMin;
        rt.anchorMax = AnchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var plate = go.AddComponent<EventIconPlate>();
        plate.label = label;
        plate.fill = AddImage(go, fillSprite, color);
        var rimGo = new GameObject("Rim", typeof(RectTransform));
        rimGo.layer = go.layer;
        var rimRt = (RectTransform)rimGo.transform;
        rimRt.SetParent(rt, false);
        rimRt.anchorMin = Vector2.zero; rimRt.anchorMax = Vector2.one;
        rimRt.offsetMin = rimRt.offsetMax = Vector2.zero;
        plate.rim = AddImage(rimGo, rimSprite, Color.white);

        // Hap uçları yuvarlak kalsın: sprite yüksekliği plaka yüksekliğine ölçeklenir (köşe = yarı yükseklik).
        float plateHeight = host.rect.height > 1f ? host.rect.height * (AnchorMax.y - AnchorMin.y) : 40f;
        float ppu = fillSprite.rect.height / Mathf.Max(1f, plateHeight);
        plate.fill.pixelsPerUnitMultiplier = ppu;
        plate.rim.pixelsPerUnitMultiplier = ppu;

        var lrt = label.rectTransform;
        lrt.SetParent(rt, false);
        lrt.SetAsLastSibling();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.offsetMin = new Vector2(10f, 3f);
        lrt.offsetMax = new Vector2(-10f, -3f);
        lrt.localScale = Vector3.one;
        label.alignment = TextAlignmentOptions.Center;
        TextSurfaceStyle.Apply(label, color);

        plate.RefreshVisible();
    }

    private static Image AddImage(GameObject go, Sprite sprite, Color color)
    {
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;   // dokunuş ikonun butonuna gider
        return img;
    }

    private void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        RefreshVisible();
    }

    private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

    private void OnTextChanged(Object changed)
    {
        if (changed == label) RefreshVisible();
    }

    // TMP metin olayı grafik yeniden çizim döngüsünün İÇİNDEN gelir: Image.enabled değiştirmek orada yeni rebuild
    // kaydı açar (Unity hatası). CanvasRenderer alfası rebuild istemez → döngü içinde güvenli.
    private void RefreshVisible()
    {
        float alpha = label != null && !string.IsNullOrEmpty(label.text) ? 1f : 0f;
        if (fill != null) fill.canvasRenderer.SetAlpha(alpha);
        if (rim != null) rim.canvasRenderer.SetAlpha(alpha);
    }
}
