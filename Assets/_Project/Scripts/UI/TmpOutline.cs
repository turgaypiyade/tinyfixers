using TMPro;
using UnityEngine;

/// <summary>
/// TMP yazısına kontur verir — GÜVENLİ. outlineWidth/outlineColor setter'ı yazının materyal
/// kopyasını oluşturur; yazı henüz hiç etkinleşmemişse (kapalı panel içi, ya da başka bir
/// bileşenin Awake'i TMP'nin Awake'inden önce koştuysa) materyal yoktur → NullReferenceException.
/// Burada yazı hazırsa hemen uygulanır, değilse ilk OnEnable'da (TMP kurulduktan sonra) uygulanır.
/// </summary>
[DisallowMultipleComponent]
public sealed class TmpOutline : MonoBehaviour
{
    private float width;
    private Color color;
    private bool pending;
    private bool shadow;
    private Color shadowColor;
    private Vector2 shadowOffset;

    public static void Apply(TMP_Text text, float width, Color color)
    {
        if (text == null) return;
        if (!text.TryGetComponent(out TmpOutline o)) o = text.gameObject.AddComponent<TmpOutline>();
        o.width = width;
        o.color = color;
        o.shadow = false;
        o.pending = true;
        o.TryApply();
    }

    /// <summary>Kontur + alt gölge (underlay): gölge konturlu harf biçiminde, offset yönünde kaymış
    /// (x+ sağa, y− aşağı; SDF birimi). Küçük sayılarda kontur tek başına zemine karışınca okunurluğu verir.</summary>
    public static void Apply(TMP_Text text, float width, Color color, Color shadowColor, Vector2 shadowOffset)
    {
        Apply(text, width, color);
        var o = text.GetComponent<TmpOutline>();
        o.shadow = true;
        o.shadowColor = shadowColor;
        o.shadowOffset = shadowOffset;
        o.pending = true;
        o.TryApply();
    }

    private void OnEnable() => TryApply();

    private void TryApply()
    {
        if (!pending || !isActiveAndEnabled) return;
        if (!TryGetComponent(out TMP_Text text) || text.font == null) return;
        // Yazı etkin olsa da kendi kurulumu bu kareye kalmış olabilir → materyal yoksa sonraki karede dene.
        if (text.fontSharedMaterial == null) { StartCoroutine(RetryNextFrame()); return; }

        text.outlineWidth = width;
        text.outlineColor = color;
        // Mobil SDF shader'ı konturu yalnız OUTLINE_ON açıkken çizer (koddan üretilen font asset'leri bu
        // shader'la gelir): anahtar kapalıysa genişlik verilse de kontur görünmez. Örnek materyalde aç.
        var mat = text.fontMaterial;   // yazıya özel örnek (outlineWidth setter'ı zaten oluşturdu)
        if (mat != null)
        {
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            if (shadow)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, shadowColor);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffset.x);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, shadowOffset.y);
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, width);   // gölge konturlu biçimle aynı kalınlıkta
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
                ShaderUtilities.UpdateShaderRatios(mat);
                text.UpdateMeshPadding();   // gölge ofseti mesh kenarında kesilmesin
            }
        }
        pending = false;
    }

    private System.Collections.IEnumerator RetryNextFrame()
    {
        yield return null;
        TryApply();
    }
}
