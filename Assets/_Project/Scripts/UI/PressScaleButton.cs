using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PressScaleButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Range(0.7f, 1f)] private float pressedScale = 0.92f;
    [SerializeField, Min(0.03f)] private float animDuration = 0.08f;

    private Button button;
    private Coroutine anim;
    private Vector3 normalScale;

    private void Awake()
    {
        button = GetComponent<Button>();
        normalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData _)
    {
        if (!button.interactable) return;
        ScaleTo(normalScale * pressedScale);
    }

    public void OnPointerUp(PointerEventData _)
    {
        ScaleTo(normalScale);
    }

    public void OnPointerExit(PointerEventData _)
    {
        ScaleTo(normalScale);
    }

    private void ScaleTo(Vector3 target)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(LerpScale(target));
    }

    private IEnumerator LerpScale(Vector3 target)
    {
        Vector3 from = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animDuration;
            transform.localScale = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.localScale = target;
    }
}
