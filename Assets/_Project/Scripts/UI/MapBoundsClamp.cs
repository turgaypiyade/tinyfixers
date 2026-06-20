using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Content'i her yöne serbest gezdirir ama viewport'un içerik (background) DIŞINA çıkmasını engeller —
/// yani üst/alt/sağ/sol boşluk asla görünmez. ScrollRect Movement Type = Unrestricted ile kullan.
/// Pan'i ScrollRect, zoom'u MapPinchZoom yapar; bu sadece sınırı zorlar.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public sealed class MapBoundsClamp : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;

    private readonly Vector3[] cc = new Vector3[4];   // content corners
    private readonly Vector3[] vc = new Vector3[4];   // viewport corners

    private void Awake()
    {
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
    }

    private void LateUpdate() => ClampNow();

    /// <summary>İçeriği, viewport tamamen içerik sınırları içinde kalacak şekilde kaydırır.</summary>
    public void ClampNow()
    {
        if (scrollRect == null || scrollRect.content == null) return;
        var content = scrollRect.content;
        var viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        content.GetWorldCorners(cc);   // 0=BL, 1=TL, 2=TR, 3=BR
        viewport.GetWorldCorners(vc);

        float contentLeft = cc[0].x, contentRight = cc[2].x;
        float contentBottom = cc[0].y, contentTop = cc[1].y;
        float vpLeft = vc[0].x, vpRight = vc[2].x;
        float vpBottom = vc[0].y, vpTop = vc[1].y;

        float dx = 0f, dy = 0f;

        // Yatay: içerik viewport'tan genişse kenarlardan birine yapışana kadar izin ver.
        if (contentRight - contentLeft >= vpRight - vpLeft)
        {
            if (contentLeft > vpLeft) dx = vpLeft - contentLeft;        // çok sağda → sola çek
            else if (contentRight < vpRight) dx = vpRight - contentRight; // çok solda → sağa çek
        }
        else
        {
            dx = (vpLeft + vpRight) * 0.5f - (contentLeft + contentRight) * 0.5f; // dar → ortala
        }

        // Dikey
        if (contentTop - contentBottom >= vpTop - vpBottom)
        {
            if (contentBottom > vpBottom) dy = vpBottom - contentBottom;
            else if (contentTop < vpTop) dy = vpTop - contentTop;
        }
        else
        {
            dy = (vpBottom + vpTop) * 0.5f - (contentBottom + contentTop) * 0.5f;
        }

        if (Mathf.Abs(dx) > 0.001f || Mathf.Abs(dy) > 0.001f)
        {
            content.position += new Vector3(dx, dy, 0f);
            scrollRect.velocity = Vector2.zero;
        }
    }
}
