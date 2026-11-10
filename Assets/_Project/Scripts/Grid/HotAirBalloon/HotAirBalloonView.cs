using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sıcak Hava Balonu görünümü (ObstacleId.HotAirBalloon, 2x2, 3 vuruş — yalnız special/joker; kullanıcı 2026-11-09).
/// GridSpawner'ın çizdiği engel Image'ına takılır (Rodos/Lazer deseni). Taban Image saydam kalır (tıklama vekili için
/// yerinde durur); çocuklar: önü açık ahşap dolap (2x2'yi doldurur) ve içinde balon.
/// Kademe: kalan 3 sönük · 2 yarı · 1 neredeyse dolu (hâlâ dolaba sığar) · 0 tam şişik → dolap ikiye yarılır, balon uçar.
/// Vuruşta YALNIZ balon görseli değişir; sepet/boyut sabit (kullanıcı: "sepeti genişletme"). Update yok.
/// Yarılma + uçuşu HotAirBalloonFlight yönetir (görseli devralır). Sprite'lar Resources/HotAirBalloon/
/// (Balloon_S1..S4 aynı tuval + sepet hizası; Cabinet + tırtıklı çizgiyle kesilmiş Cabinet_L/Cabinet_R aynı tuval).
/// </summary>
public sealed class HotAirBalloonView : MonoBehaviour
{
    private const string Dir = "HotAirBalloon/";
    // 2x2 alan oranları (sol-alt köşeden). Balon genişliği dolabın iç genişliğine yakın; sepet dolap tabanında.
    // Ölçüler önizlemeyle onaylandı (Wondi Paylaşım/SicakHavaBalonu/Balon_Dolap_onizleme.gif).
    private const float BalloonSize = 0.88f, BalloonBottom = 0.05f;

    private static Sprite transparent, cabinetSprite, cabinetLeftSprite, cabinetRightSprite;
    private static readonly Sprite[] Stages = new Sprite[4];
    private static readonly Dictionary<int, Image> pendingFlights = new();

    private RectTransform root, balloon, cabinet;
    private Image balloonImage, cabinetImage;
    private int stage = -1;

    internal RectTransform BalloonRect => balloon;
    /// Sepet tabanının 2x2 alt kenarından yüksekliği (alan yüksekliğine oranla) — uçuş satır vuruşu hizası.
    internal static float FullBasketLift => BalloonBottom;

    /// Kalan vuruş → kademe (0 sönük … 3 tam şişik).
    public static int StageForRemaining(int remainingHits) => Mathf.Clamp(3 - remainingHits, 0, 3);

    public static HotAirBalloonView Ensure(Image image, int remainingHits)
    {
        if (image == null) return null;
        if (!image.TryGetComponent<HotAirBalloonView>(out var view))
            view = image.gameObject.AddComponent<HotAirBalloonView>();
        view.Build(image);
        view.SetStage(StageForRemaining(remainingHits));
        return view;
    }

    // ── Uçuş devri (GelLauncherFx kalıbı): GridSpawner yok etmek yerine bırakır, HotAirBalloonFlight devralır ──
    internal static void HandOver(int originIndex, Image view)
    {
        if (view == null) return;
        if (pendingFlights.TryGetValue(originIndex, out var old) && old != null && old != view) Destroy(old.gameObject);
        pendingFlights[originIndex] = view;
    }

    internal static Image Take(int originIndex)
    {
        if (!pendingFlights.TryGetValue(originIndex, out var view)) return null;
        pendingFlights.Remove(originIndex);
        return view;
    }

    internal static bool IsPending(int originIndex, Image view)
        => pendingFlights.TryGetValue(originIndex, out var v) && v == view;

    private void Build(Image baseImage)
    {
        if (root != null) return;
        root = baseImage.rectTransform;
        baseImage.sprite = Transparent();     // taban yerinde (tıklama vekili), görünmez
        baseImage.preserveAspect = false;
        cabinetSprite ??= Resources.Load<Sprite>(Dir + "Cabinet");
        cabinetLeftSprite ??= Resources.Load<Sprite>(Dir + "Cabinet_L");
        cabinetRightSprite ??= Resources.Load<Sprite>(Dir + "Cabinet_R");

        cabinet = Child("Cabinet", cabinetSprite, out cabinetImage, new Vector2(0.5f, 0f));
        balloon = Child("Balloon", null, out balloonImage, new Vector2(0.5f, 0f));   // dolabın önünde, tabana oturur
        cabinetImage.preserveAspect = balloonImage.preserveAspect = true;
    }

    private RectTransform Child(string name, Sprite sprite, out Image img, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;   // runtime UI: layer 0'da doğarsa kamera çizmez
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = pivot;
        img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;     // tıklama taban Image'ına (ObstacleClickProxy) gitsin
        return rt;
    }

    private void SetStage(int s)
    {
        stage = Mathf.Clamp(s, 0, 3);
        balloonImage.sprite = StageSprite(stage);
        Layout();
    }

    /// Vuruş: yalnız balon görseli bir kademe değişir (boyut/sepet sabit). Ses merkezi yoldan (BoardBreakFxService).
    public void OnHit(int remainingHits, BoardController board, ObstacleDef def)
    {
        int s = StageForRemaining(remainingHits);
        if (s != stage) SetStage(s);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (balloon != null) Layout();
    }

    /// Kalkış: tam şişik görsel.
    internal void ShowFull() => SetStage(3);

    /// Dolabı iki yarıya böler: sağlam dolap gizlenir, yarılar (sol: dış sol-alt köşe pivotlu, sağ: dış sağ-alt) aynı
    /// yerde açılır ve <paramref name="fxParent"/>'a taşınır (balonla yükselmesin). Devrilmeyi çağıran sürer.
    internal void SplitCabinet(RectTransform fxParent, out RectTransform left, out RectTransform right)
    {
        left = right = null;
        if (cabinet == null || root == null) return;
        Vector2 area = root.rect.size;
        left = Child("CabinetL", cabinetLeftSprite, out var li, Vector2.zero);
        right = Child("CabinetR", cabinetRightSprite, out var ri, Vector2.right);
        li.preserveAspect = ri.preserveAspect = true;
        left.sizeDelta = right.sizeDelta = area;
        left.anchoredPosition = Vector2.zero;
        right.anchoredPosition = new Vector2(area.x, 0f);
        cabinet.gameObject.SetActive(false);
        if (fxParent != null)
        {
            left.SetParent(fxParent, true);
            right.SetParent(fxParent, true);
        }
        balloon.SetAsLastSibling();
    }

    private void Layout()
    {
        if (root == null) return;
        Vector2 area = root.rect.size;
        if (area.x <= 1f || area.y <= 1f) return;
        cabinet.sizeDelta = area;
        cabinet.anchoredPosition = new Vector2(area.x * 0.5f, 0f);
        float size = BalloonSize * area.y;
        balloon.sizeDelta = new Vector2(size, size);
        balloon.anchoredPosition = new Vector2(area.x * 0.5f, BalloonBottom * area.y);
    }

    internal void Tremble(float offset)
    {
        if (balloon == null) return;
        Layout();
        balloon.anchoredPosition += new Vector2(offset, 0f);
    }

    private static Sprite StageSprite(int s)
    {
        s = Mathf.Clamp(s, 0, 3);
        return Stages[s] != null ? Stages[s] : Stages[s] = Resources.Load<Sprite>(Dir + "Balloon_S" + (s + 1));
    }

    private static Sprite Transparent()
    {
        if (transparent != null) return transparent;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        tex.SetPixels32(new Color32[4]);
        tex.Apply(false, true);
        transparent = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        transparent.hideFlags = HideFlags.HideAndDontSave;
        return transparent;
    }
}
