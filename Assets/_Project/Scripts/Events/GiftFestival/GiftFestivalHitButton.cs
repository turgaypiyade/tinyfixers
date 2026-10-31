using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Persistent normal/pressed layers; the image is swapped without creating objects/materials.</summary>
public sealed class GiftFestivalHitButton : Button
{
    private Image face;
    private RectTransform textRect;
    private TMP_Text label;
    private Sprite normal, pressed;
    private Material disabledMaterial;
    private Vector2 restPosition;
    public System.Action Strike;
    public bool IsStriking { get; private set; }

    public void ShowStrike(bool value)
    {
        if (IsStriking == value) return;
        IsStriking = value;
        DoStateTransition(currentSelectionState, true);
    }

    public void Configure(Image surface, TMP_Text text, GiftFestivalConfig cfg)
    {
        face = surface; label = text; textRect = text.rectTransform;
        restPosition = textRect.anchoredPosition;
        normal = cfg.Art.hitNormal; pressed = cfg.Art.hitPressed;
        disabledMaterial = cfg.Art.disabledButtonMaterial;
        targetGraphic = surface; transition = Transition.None;
        navigation = new Navigation { mode = Navigation.Mode.None };
        DoStateTransition(currentSelectionState, true);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        bool fire = eventData.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable();
        base.OnPointerDown(eventData);
        if (fire) Strike?.Invoke();
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (IsActive() && IsInteractable()) Strike?.Invoke();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        if (face == null) return;
        bool down = state == SelectionState.Pressed || IsStriking;
        bool disabled = state == SelectionState.Disabled && !IsStriking;
        face.sprite = down && pressed != null ? pressed : normal;
        // The disabled surface is deliberately subdued; the separate text keeps its contrast.
        face.material = disabled ? disabledMaterial : null;
        face.color = disabled ? new Color(1f, 1f, 1f, 0.78f) : Color.white;
        textRect.anchoredPosition = restPosition + (down ? new Vector2(0, -5) : Vector2.zero);
        label.color = disabled ? new Color(0.34f, 0.29f, 0.23f, 0.8f) : GiftFestivalUI.Ink;
    }
}
