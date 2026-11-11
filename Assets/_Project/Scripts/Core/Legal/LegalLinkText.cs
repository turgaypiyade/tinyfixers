using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Kompakt kabul kutusundaki metin içi linkler (&lt;link=terms&gt; / &lt;link=privacy&gt;): dokunulan link belgesini
/// oyun içi görüntüleyicide açar (LegalDocumentView, kutunun üstünde). Kutu açık kalır.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class LegalLinkText : MonoBehaviour, IPointerClickHandler
{
    private TMP_Text text;

    private void Awake() => text = GetComponent<TMP_Text>();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (text == null) return;
        var canvas = text.canvas;
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        int index = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, cam);
        if (index < 0 || index >= text.textInfo.linkCount) return;
        string slug = text.textInfo.linkInfo[index].GetLinkID();
        if (slug == "terms" || slug == "privacy") LegalDocumentView.Show(slug);
    }
}
