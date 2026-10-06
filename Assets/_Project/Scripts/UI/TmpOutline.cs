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

    public static void Apply(TMP_Text text, float width, Color color)
    {
        if (text == null) return;
        if (!text.TryGetComponent(out TmpOutline o)) o = text.gameObject.AddComponent<TmpOutline>();
        o.width = width;
        o.color = color;
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
        pending = false;
    }

    private System.Collections.IEnumerator RetryNextFrame()
    {
        yield return null;
        TryApply();
    }
}
