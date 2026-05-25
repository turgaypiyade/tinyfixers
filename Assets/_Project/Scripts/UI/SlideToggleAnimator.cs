using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class SlideToggleAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Soldan sağa kayacak container (KnobContainer RectTransform).")]
    [SerializeField] private RectTransform knob;
    [Tooltip("ON durumunda aktif olacak GameObject (GreenKnob). SetActive ile text dahil tüm child'lar gizlenir.")]
    [SerializeField] private GameObject onObject;
    [Tooltip("OFF durumunda aktif olacak GameObject (RedKnob). SetActive ile text dahil tüm child'lar gizlenir.")]
    [SerializeField] private GameObject offObject;

    [Header("Knob Positions")]
    [SerializeField] private float knobOffX = -40f;
    [SerializeField] private float knobOnX  =  40f;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float slideDuration = 0.18f;

    private Toggle toggle;
    private float animTimer;
    private bool  animating;
    private float fromX, toX;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        if (toggle != null) toggle.onValueChanged.AddListener(OnToggleChanged);
        ApplyInstant(toggle != null && toggle.isOn);
    }

    private void OnDestroy()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnToggleChanged(bool isOn) => StartAnim(isOn);

    private void ApplyInstant(bool isOn)
    {
        if (knob != null)
        {
            var p = knob.anchoredPosition;
            p.x = isOn ? knobOnX : knobOffX;
            knob.anchoredPosition = p;
        }
        if (onObject  != null) onObject.SetActive(isOn);
        if (offObject != null) offObject.SetActive(!isOn);
        animating = false;
    }

    private void StartAnim(bool isOn)
    {
        fromX = knob != null ? knob.anchoredPosition.x : 0f;
        toX   = isOn ? knobOnX : knobOffX;
        // Knob görünürlüğünü hemen değiştir — kayma animasyonunu engellemiyor
        if (onObject  != null) onObject.SetActive(isOn);
        if (offObject != null) offObject.SetActive(!isOn);
        animTimer = 0f;
        animating = true;
    }

    private void Update()
    {
        if (!animating) return;

        animTimer += Time.unscaledDeltaTime;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(animTimer / slideDuration));

        if (knob != null)
        {
            var p = knob.anchoredPosition;
            p.x = Mathf.Lerp(fromX, toX, k);
            knob.anchoredPosition = p;
        }

        if (animTimer >= slideDuration) animating = false;
    }
}
