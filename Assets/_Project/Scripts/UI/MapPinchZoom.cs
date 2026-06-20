using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Harita için kullanıcı zoom'u: mobilde iki parmak pinch, editör/mouse'ta tekerlek.
/// İmleç/pinch merkezine doğru yakınlaşır. Pan'i ScrollRect zaten yapıyor.
/// ScrollRect Content'ini ölçekler (localScale). Movement Type = Unrestricted önerilir.
/// </summary>
public sealed class MapPinchZoom : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField, Min(0.05f)] private float minZoom = 0.5f;
    [SerializeField, Min(0.1f)]  private float maxZoom = 2f;
    [Tooltip("Mouse tekerleği zoom hızı.")]
    [SerializeField] private float wheelSpeed = 0.1f;
    [Tooltip("Pinch zoom hızı (parmak mesafesi oranına çarpan).")]
    [SerializeField] private float pinchSpeed = 1f;

    private Canvas canvas;
    private Camera cam;

    private void Awake()
    {
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
        canvas = GetComponentInParent<Canvas>();
        cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
    }

    private void Update()
    {
        if (scrollRect == null || scrollRect.content == null) return;

#if ENABLE_INPUT_SYSTEM
        // ── Dokunmatik pinch ──
        var ts = Touchscreen.current;
        if (ts != null)
        {
            int active = 0; Vector2 p0 = default, p1 = default;
            foreach (var t in ts.touches)
            {
                if (t.press.isPressed)
                {
                    if (active == 0) p0 = t.position.ReadValue();
                    else if (active == 1) p1 = t.position.ReadValue();
                    active++;
                }
            }
            if (active >= 2)
            {
                float dist = Vector2.Distance(p0, p1);
                if (hadTwo && prevDist > 0.01f)
                {
                    float factor = Mathf.Lerp(1f, dist / prevDist, pinchSpeed);
                    ZoomAt((p0 + p1) * 0.5f, factor);
                }
                prevDist = dist; hadTwo = true;
                return;   // pinch sırasında tekerlek okuma
            }
            hadTwo = false;
        }

        // ── Mouse tekerleği ──
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                ZoomAt(mouse.position.ReadValue(), 1f + Mathf.Sign(scroll) * wheelSpeed);
        }
#else
        if (Input.touchCount >= 2)
        {
            var t0 = Input.GetTouch(0); var t1 = Input.GetTouch(1);
            float dist = Vector2.Distance(t0.position, t1.position);
            if (hadTwo && prevDist > 0.01f)
            {
                float factor = Mathf.Lerp(1f, dist / prevDist, pinchSpeed);
                ZoomAt((t0.position + t1.position) * 0.5f, factor);
            }
            prevDist = dist; hadTwo = true;
            return;
        }
        hadTwo = false;

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f)
            ZoomAt(Input.mousePosition, 1f + Mathf.Sign(wheel) * wheelSpeed);
#endif
    }

    private float prevDist;
    private bool hadTwo;

    /// <summary>İçeriğin viewport'u TAM kaplaması için gereken min ölçek (altına inilirse boşluk görünür).</summary>
    private float CoverScale()
    {
        var content = scrollRect.content;
        var viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
        float cw = content.rect.width, ch = content.rect.height;
        if (cw <= 0f || ch <= 0f) return minZoom;
        return Mathf.Max(viewport.rect.width / cw, viewport.rect.height / ch);
    }

    private void ZoomAt(Vector2 screenFocal, float factor)
    {
        var content = scrollRect.content;
        float current = content.localScale.x;
        // Alt sınır: hem kullanıcı minZoom'u hem "içerik ekranı kaplasın" sınırı (boşluk gösterme).
        float floor = Mathf.Max(minZoom, CoverScale());
        float target = Mathf.Clamp(current * factor, floor, maxZoom);
        if (Mathf.Approximately(target, current)) return;

        // Odak noktasını sabit tut: ölçekten önce/sonra content-local konumu karşılaştır.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenFocal, cam, out Vector2 before);
        content.localScale = Vector3.one * target;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenFocal, cam, out Vector2 after);

        Vector3 worldDelta = content.TransformVector(after - before);
        content.position += worldDelta;
        scrollRect.velocity = Vector2.zero;
    }
}
