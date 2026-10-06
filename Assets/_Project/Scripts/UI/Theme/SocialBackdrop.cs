using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Alt-menü ekranlarının (Liderlik / Takım) gövdesine düşük opaklıkta döşenen desen
/// (pati, anahtar, dişli, yıldız — Resources/UIPatterns/SocialBgPattern). Düz lacivert
/// zemini canlandırır; satırların ARKASINDA kalır, tıklamayı kesmez.
/// </summary>
[RequireComponent(typeof(RawImage))]
public sealed class SocialBackdrop : MonoBehaviour
{
    private const string TexturePath = "UIPatterns/SocialBgPattern";
    private const float TileSize = 300f;     // bir desen karesinin ekrandaki boyu (1080 referans)
    private const float Opacity = 0.07f;

    private RawImage image;
    private RectTransform rect;
    private Vector2 lastSize;

    /// <summary>Ekran kökünün altındaki "Body"nin en arkasına deseni ekler (bir kez).</summary>
    public static void ApplyToScreen(Transform screenRoot)
    {
        if (screenRoot == null) return;
        var body = screenRoot.Find("Body") as RectTransform;
        if (body == null || body.Find("BgPattern") != null) return;

        var tex = Resources.Load<Texture2D>(TexturePath);
        if (tex == null) return;
        tex.wrapMode = TextureWrapMode.Repeat;

        var go = new GameObject("BgPattern", typeof(RectTransform));
        go.layer = body.gameObject.layer;   // Screen Space Camera culling guard
        var rt = (RectTransform)go.transform;
        rt.SetParent(body, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var raw = go.AddComponent<RawImage>();
        raw.texture = tex;
        raw.color = new Color(1f, 1f, 1f, Opacity);
        raw.raycastTarget = false;
        go.AddComponent<LayoutElement>().ignoreLayout = true;   // gövdeye layout eklenirse dizilime girmesin
        go.AddComponent<SocialBackdrop>();
    }

    private void Awake()
    {
        image = GetComponent<RawImage>();
        rect = (RectTransform)transform;
    }

    // Desen ölçeği sabit kalsın: alan büyüdükçe esnemez, daha çok tekrarlanır.
    private void LateUpdate()
    {
        var size = rect.rect.size;
        if (size == lastSize) return;
        lastSize = size;
        image.uvRect = new Rect(0f, 0f, size.x / TileSize, size.y / TileSize);
    }
}
