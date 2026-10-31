using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tek bir butonun tık sesini elle belirler (UiClickSounds otomatik seçimini ezer).
/// None = bu buton ses çalmaz (kendi özel sesi olan butonlar için).
/// </summary>
[DisallowMultipleComponent]
public sealed class UiClickSound : MonoBehaviour, IPointerDownHandler
{
    public enum Mode { Auto, Tap, Confirm, Back, None }

    public Mode mode = Mode.Auto;
    [Tooltip("Parmak değdiği anda çal. Merkezi bırakma sesi bu buton için atlanır.")]
    public bool playOnPress;
    [Tooltip("Basışa özel klip. Boşsa Mode üzerinden merkezi UI klibi kullanılır.")]
    public AudioClip clipOverride;

    private Selectable target;

    private void Awake() => target = GetComponent<Selectable>();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left
            || !playOnPress || mode == Mode.None || !isActiveAndEnabled) return;
        if (target == null) target = GetComponent<Selectable>();
        if (target == null || !target.IsInteractable()) return;
        if (clipOverride != null) GameEventSfx.PlayUiClip(clipOverride);
        else
        {
            var sound = UiClickSounds.Classify(target, includePressOwned: true);
            if (sound.HasValue) GameEventSfx.PlayUi(sound.Value);
        }
    }
}
