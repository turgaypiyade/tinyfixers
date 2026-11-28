using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pençe Makinesi yön tuşu (kullanıcı 2026-11-28, gerçek makine gibi): basılı tutuldukça pençe o yöne gider.
/// Joystick'in "ortasına dokun" İNDİR'i anlaşılmadı → yön tuşları (sağa, geriye) + ayrı İNDİR butonu.
/// Parmak tuşun dışına kayınca ya da bırakınca durur; ekran kapanınca sıfırlanır.
/// </summary>
public sealed class ClawMachineHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool Held { get; private set; }

    public void OnPointerDown(PointerEventData e) => Held = true;
    public void OnPointerUp(PointerEventData e) => Held = false;
    public void OnPointerExit(PointerEventData e) => Held = false;
    private void OnDisable() => Held = false;
}
