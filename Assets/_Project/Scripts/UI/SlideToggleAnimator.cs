using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// iOS-style slide toggle animatörü. Unity Toggle component'inin yanına eklenir.
/// Toggle.isOn değiştikçe knob soldan sağa kayar, yeşil overlay fade-in olur.
///
/// Hiyerarşi:
///   ToggleRoot (Toggle component)
///   ├── Background (kırmızı track)
///   ├── GreenOverlay (yeşil track, alpha 0 başlar — Toggle.graphic'e bağla)
///   └── Knob (kayan yuvarlak buton)
/// </summary>
[RequireComponent(typeof(Toggle))]
public class SlideToggleAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Soldan sağa kayacak knob (yuvarlak buton).")]
    [SerializeField] private RectTransform knob;
    [Tooltip("Yeşil overlay — isOn=true iken alpha 1, false iken alpha 0.")]
    [SerializeField] private Graphic greenOverlay;

    [Header("Knob Positions")]
    [Tooltip("Knob'un sola yaslı (kapalı) pozisyonu — anchoredPosition.x.")]
    [SerializeField] private float knobOffX = -40f;
    [Tooltip("Knob'un sağa yaslı (açık) pozisyonu — anchoredPosition.x.")]
    [SerializeField] private float knobOnX = 40f;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float slideDuration = 0.18f;

    private Toggle toggle;
    private float animTimer;
    private bool animating;
    private float fromX;
    private float toX;
    private float fromAlpha;
    private float toAlpha;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        if (toggle != null) toggle.onValueChanged.AddListener(OnToggleChanged);

        // Sahne başlangıcında durum görsel olarak doğru olsun (animasyonsuz)
        ApplyInstant(toggle != null && toggle.isOn);
    }

    private void OnDestroy()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnToggleChanged(bool isOn)
    {
        StartAnim(isOn);
    }

    private void ApplyInstant(bool isOn)
    {
        if (knob != null)
        {
            var p = knob.anchoredPosition;
            p.x = isOn ? knobOnX : knobOffX;
            knob.anchoredPosition = p;
        }
        if (greenOverlay != null)
        {
            var c = greenOverlay.color;
            c.a = isOn ? 1f : 0f;
            greenOverlay.color = c;
        }
        animating = false;
    }

    private void StartAnim(bool isOn)
    {
        if (knob == null && greenOverlay == null) return;

        fromX = knob != null ? knob.anchoredPosition.x : 0f;
        toX = isOn ? knobOnX : knobOffX;
        fromAlpha = greenOverlay != null ? greenOverlay.color.a : 0f;
        toAlpha = isOn ? 1f : 0f;
        animTimer = 0f;
        animating = true;
    }

    private void Update()
    {
        if (!animating) return;

        animTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(animTimer / slideDuration);
        float k = Mathf.SmoothStep(0f, 1f, t);

        if (knob != null)
        {
            var p = knob.anchoredPosition;
            p.x = Mathf.Lerp(fromX, toX, k);
            knob.anchoredPosition = p;
        }
        if (greenOverlay != null)
        {
            var c = greenOverlay.color;
            c.a = Mathf.Lerp(fromAlpha, toAlpha, k);
            greenOverlay.color = c;
        }

        if (t >= 1f) animating = false;
    }
}
