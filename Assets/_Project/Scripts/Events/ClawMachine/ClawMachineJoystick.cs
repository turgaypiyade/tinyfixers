using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pençe Makinesi joystick'i (kullanıcı 2026-11-28): topuz parmağı izler, Value −1..1 (yön kısıtını ekran uygular:
/// yalnız sağa ve geriye). Topuzun ortasına dokunup kaydırmadan bırakmak = İNDİR (Tapped).
/// Kod ile kurulur (ClawMachineScreen); topuz görseli dışarıdan verilir.
/// </summary>
public sealed class ClawMachineJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float TapSlop = 16f, TapSeconds = 0.35f, CenterRatio = 0.5f;

    public RectTransform knob;
    public float radius = 70f;
    public Action Tapped;

    public Vector2 Value { get; private set; }
    public bool Held { get; private set; }

    private Vector2 pressPos;
    private float pressTime;
    private bool moved, pressedCenter;

    public void OnPointerDown(PointerEventData e)
    {
        if (!ToLocal(e, out var p)) return;
        Held = true;
        moved = false;
        pressPos = p;
        pressTime = Time.unscaledTime;
        pressedCenter = p.magnitude <= radius * CenterRatio;
        // Ortaya basış İNDİR adayıdır: kaydırılana dek pençeyi oynatmaz.
        if (!pressedCenter) Apply(p);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!Held || !ToLocal(e, out var p)) return;
        if (!moved && (p - pressPos).magnitude > TapSlop) moved = true;
        if (moved || !pressedCenter) Apply(p);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!Held) return;
        bool tap = pressedCenter && !moved && Time.unscaledTime - pressTime <= TapSeconds;
        Release();
        if (tap) Tapped?.Invoke();
    }

    private void OnDisable() => Release();

    private void Release()
    {
        Held = false;
        Value = Vector2.zero;
        if (knob != null) knob.anchoredPosition = Vector2.zero;
    }

    private void Apply(Vector2 p)
    {
        var v = Vector2.ClampMagnitude(p, radius);
        if (knob != null) knob.anchoredPosition = v;
        Value = v / Mathf.Max(1f, radius);
    }

    private bool ToLocal(PointerEventData e, out Vector2 local)
        => RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out local);
}
