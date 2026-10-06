using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ortak buton ailesi (Resources/UIButtons): çerçeveli parlak kapsüller + kare butonlar, 9-slice.
/// Her ekran aynı butonları kullansın diye tek yerden takılır; yazıya renk uyumlu koyu kontur verilir.
/// </summary>
public static class UiButtons
{
    public enum Kind { Orange, Green, Blue, Red, Purple, SquareRed, SquareGreen }

    private static Sprite Load(string name) => Resources.Load<Sprite>("UIButtons/" + name);

    public static Sprite SpriteOf(Kind kind) => kind switch
    {
        Kind.Orange      => Load("Btn_Orange"),
        Kind.Green       => Load("Btn_Green"),
        Kind.Blue        => Load("Btn_Blue"),
        Kind.Red         => Load("Btn_Red"),
        Kind.Purple      => Load("Btn_Purple"),
        Kind.SquareRed   => Load("BtnSq_Red"),
        Kind.SquareGreen => Load("BtnSq_Green"),
        _ => null,
    };

    // Yazı konturu: butonun koyu tonu (beyaz yazı her renkte okunur).
    private static Color OutlineOf(Kind kind) => kind switch
    {
        Kind.Orange      => new Color(0.55f, 0.22f, 0.02f),
        Kind.Green or Kind.SquareGreen => new Color(0.10f, 0.35f, 0.05f),
        Kind.Blue        => new Color(0.05f, 0.18f, 0.55f),
        Kind.Red or Kind.SquareRed     => new Color(0.50f, 0.04f, 0.08f),
        Kind.Purple      => new Color(0.30f, 0.08f, 0.50f),
        _ => Color.black,
    };

    /// <summary>Butonun arka planını verilen türe çevirir (+ yazı stili). Tekrar çağrılması güvenli.</summary>
    public static void Apply(Button button, Kind kind, bool styleLabel = true)
    {
        if (button == null) return;
        var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        Apply(image, kind);

        if (!styleLabel) return;
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            StyleLabel(label, kind);
    }

    /// <summary>
    /// Kapsül görseli yalnız yatay butonda doğru görünür (uçlar = yüksekliğin yarısı). Kareye yakın bir
    /// butonu en az minRatio oranına genişletir (merkezi sabit) ve yazıyı okunur boya çeker.
    /// </summary>
    public static void EnsureCapsuleAspect(Button button, float minRatio = 2.6f, float labelMaxSize = 32f)
    {
        if (button == null) return;
        var rt = (RectTransform)button.transform;
        float h = rt.rect.height > 1f ? rt.rect.height : rt.sizeDelta.y;
        if (h <= 1f) return;
        float w = rt.rect.width > 1f ? rt.rect.width : rt.sizeDelta.x;
        if (w < h * minRatio && Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x))
        {
            // SAĞ kenar sabit, sola doğru genişler (bu butonlar genelde kartın sağ kenarına yakın).
            float grow = h * minRatio - w;
            rt.sizeDelta = new Vector2(h * minRatio, rt.sizeDelta.y);
            rt.anchoredPosition -= new Vector2(grow * (1f - rt.pivot.x), 0f);
        }

        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = labelMaxSize;
            label.fontSizeMin = labelMaxSize * 0.6f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    /// <summary>Buton yazısı: beyaz + butonun koyu tonunda kontur.</summary>
    public static void StyleLabel(TMP_Text label, Kind kind)
    {
        if (label == null) return;
        label.color = Color.white;
        // Kapalı panel içindeki butonlarda TMP materyali yok → doğrudan outlineWidth NRE verir.
        TmpOutline.Apply(label, 0.2f, OutlineOf(kind));
    }

    /// <summary>Yalnız Image'ı (ör. sekme zemini) verilen buton görseline çevirir.</summary>
    public static void Apply(Image image, Kind kind)
    {
        if (image == null) return;
        var sprite = SpriteOf(kind);
        if (sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.preserveAspect = false;
        if (!image.TryGetComponent(out SlicedHeightScaler scaler))
            scaler = image.gameObject.AddComponent<SlicedHeightScaler>();
        scaler.Refresh();
    }
}
