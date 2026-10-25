using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TilesRoot'un stencil maske şekli: her sütunda o sütunun en üst oynanabilir hücresinin üst kenarından aşağısı açık,
/// ızgara dışında (sol/sağ) ızgara üst kenarından aşağısı açık. Üstte sıraya dizilen yeni taşlar yalnız sütunun
/// girişinden içeri kayarak görünür; HUD/arka plan üstünde görünmez.
///
/// Neden RectMask2D değil: her taş ikonunda iç içe Canvas var (TileView.creationSortingCanvas). RectMask2D'nin
/// dikdörtgen kırpması iç Canvas'larda yanlış uzayda uygulanıyor → kesme çizgisi kayıyor (yarım taş / hiç taş).
/// Stencil iç Canvas'tan etkilenmez. overrideSorting açık iç Canvas (oluşum/aktivasyon anı) maskeden kaçar — istenen bu.
///
/// Yerel uzay: TilesRoot sol-üst pivotlu, y = 0 ızgara üst kenarı, satırlar aşağı (−y). Çizilmez (Mask renk yazmaz).
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
public sealed class TilesTopClipShape : Graphic
{
    // Ekranı her yönde aşan uzaklık (TilesRoot yerel birimi). Stencil yalnız ekrandaki pikselleri yazar.
    private const float Far = 6000f;

    private float[] columnTops = System.Array.Empty<float>();
    private float cellWidth;

    /// tops[x] = x sütununun açık bölgesinin üst kenarı (yerel y, ≤ 0 aşağı).
    public void SetShape(float[] tops, float cellW)
    {
        columnTops = tops ?? System.Array.Empty<float>();
        cellWidth = cellW;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int n = columnTops.Length;
        float gridRight = n * cellWidth;
        AddRect(vh, -Far, 0f, 0f);                    // ızgaranın solu
        AddRect(vh, gridRight, Far, 0f);              // ızgaranın sağı
        for (int x = 0; x < n; x++)
            AddRect(vh, x * cellWidth, (x + 1) * cellWidth, columnTops[x]);
    }

    private void AddRect(VertexHelper vh, float xMin, float xMax, float top)
    {
        if (xMax <= xMin) return;
        int i = vh.currentVertCount;
        Color32 c = color;
        vh.AddVert(new Vector3(xMin, -Far), c, Vector2.zero);
        vh.AddVert(new Vector3(xMin, top), c, Vector2.zero);
        vh.AddVert(new Vector3(xMax, top), c, Vector2.zero);
        vh.AddVert(new Vector3(xMax, -Far), c, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }
}
