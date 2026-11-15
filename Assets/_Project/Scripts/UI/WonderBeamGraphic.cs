using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// İnşaat vinci kirişi: sprite'ı dört serbest köşeli bir dörtgene çizer (perspektifli yamuk — uzak uç ince,
/// yakın uç kalın). Köşeler değişince yalnız mesh yeniden kurulur; tahsis yok. [[project_wonder_build_pilot]]
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class WonderBeamGraphic : MaskableGraphic
{
    Sprite _sprite;
    // Yerel koordinatta: sol-alt, sol-üst, sağ-üst, sağ-alt.
    Vector2 _bl, _tl, _tr, _br;
    const int Segments = 12;

    public override Texture mainTexture => _sprite != null ? _sprite.texture : s_WhiteTexture;

    public Sprite Sprite
    {
        get => _sprite;
        set { _sprite = value; SetMaterialDirty(); SetVerticesDirty(); }
    }

    public void SetQuad(Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight)
    {
        _bl = bottomLeft; _tl = topLeft; _tr = topRight; _br = bottomRight;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_sprite == null) return;
        Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(_sprite);
        Color32 c = color;
        // Boyuna dilimlere bölünür: yamukta iki üçgenin afin doku kırığı (çapraz dikiş) görünmesin.
        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            float u = Mathf.Lerp(uv.x, uv.z, t);
            vh.AddVert(Vector2.Lerp(_bl, _br, t), c, new Vector2(u, uv.y));
            vh.AddVert(Vector2.Lerp(_tl, _tr, t), c, new Vector2(u, uv.w));
            if (i == 0) continue;
            int b = i * 2;
            vh.AddTriangle(b - 2, b - 1, b + 1);
            vh.AddTriangle(b + 1, b, b - 2);
        }
    }
}
