using System.Collections;
using TMPro;
using UnityEngine;

/// Oyun sahnesine eklenir. Event aktifken ilgili taş toplanınca
/// spawnAnchor üzerinde "+N" metni çıkar, yukarı kayar ve söner.
public class ProgressEventFxDriver : MonoBehaviour
{
    [SerializeField] private RectTransform spawnAnchor;  // HUD'daki event göstergesi yakını
    [SerializeField] private Canvas        canvas;

    [SerializeField] private Color  textColor     = new Color(1f, 0.9f, 0.2f);
    [SerializeField] private float  floatDistance = 55f;
    [SerializeField] private float  duration      = 0.75f;
    [SerializeField] private int    fontSize      = 30;

    private void Awake()   => ProgressEventService.OnProgressGained += OnGained;
    private void OnDestroy() => ProgressEventService.OnProgressGained -= OnGained;

    private void OnGained(int amount)
    {
        if (amount <= 0 || canvas == null) return;
        StartCoroutine(CoFloat($"+{amount}"));
    }

    private IEnumerator CoFloat(string text)
    {
        var go  = new GameObject("ProgressFx+1", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 50f);
        rt.anchoredPosition = spawnAnchor != null
            ? spawnAnchor.anchoredPosition
            : Vector2.zero;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.color         = textColor;
        tmp.fontSize      = fontSize;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        Vector2 from = rt.anchoredPosition;
        Vector2 to   = from + Vector2.up * floatDistance;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            rt.anchoredPosition = Vector2.Lerp(from, to, t);
            var c = tmp.color; c.a = 1f - t; tmp.color = c;
            yield return null;
        }

        Destroy(go);
    }
}
