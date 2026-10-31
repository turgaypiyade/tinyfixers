using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tüm UI butonlarına merkezi tık sesi (2026-10-29). Unity'de "herhangi bir buton basıldı" olayı olmadığı için
/// parmak kalkınca altındaki buton bulunur ve türüne göre ses çalar (GameEventSfx → SFX mixer: ses ayarına uyar).
/// Kapat/geri → Back, ana eylem (Oyna/Devam/Satın al/Topla) → Confirm, diğerleri → Tap, pasif buton → Denied.
/// Sürükleme/kaydırma ses çalmaz. Yalnız kendi sesi olan butonlar atlanır.
/// Tek butonda değiştirmek için butona <see cref="UiClickSound"/> ekle.
/// Raycast yalnız basma/bırakma karesinde; liste ve olay nesnesi yeniden kullanılır.
/// </summary>
// EventSystem.Update (0) butonu kapatmadan / sahneyi değiştirmeden önce sesi yakala.
[DefaultExecutionOrder(-1000)]
public sealed class UiClickSounds : MonoBehaviour
{
    private const float MaxTapMovePixels = 30f;

    private static readonly List<RaycastResult> Hits = new(8);
    private PointerEventData _eventData;
    private EventSystem _eventSystem;
    private Pointer _pointer;
    private Selectable _pressed;
    private Vector2 _pressPos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (RuntimeSimulationSession.IsActive) return;
        var go = new GameObject("[UiClickSounds]");
        DontDestroyOnLoad(go);
        go.AddComponent<UiClickSounds>();
    }

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer != _pointer)
        {
            _pressed = null;
            _pointer = pointer;
        }
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            _pressPos = pointer.position.ReadValue();
            _pressed = FindSelectable(_pressPos);
        }

        if (!pointer.press.wasReleasedThisFrame || _pressed == null) return;
        var pressed = _pressed;
        _pressed = null;

        Vector2 pos = pointer.position.ReadValue();
        if ((pos - _pressPos).sqrMagnitude > MaxTapMovePixels * MaxTapMovePixels) return;   // sürükleme / kaydırma
        if (FindSelectable(pos) != pressed) return;                                       // parmak butondan çıktı

        var sound = Classify(pressed);
        if (sound.HasValue) GameEventSfx.PlayUi(sound.Value);
    }

    private void OnDisable() => ClearPress();

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ClearPress();
    }

    private void ClearPress()
    {
        _pressed = null;
        _pointer = null;
        _eventData = null;
        _eventSystem = null;
        Hits.Clear();
    }

    private Selectable FindSelectable(Vector2 screenPos)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return null;
        if (_eventData == null || _eventSystem != eventSystem)
        {
            _eventSystem = eventSystem;
            _eventData = new PointerEventData(eventSystem);
        }
        _eventData.Reset();
        _eventData.position = screenPos;
        Hits.Clear();
        eventSystem.RaycastAll(_eventData, Hits);
        if (Hits.Count == 0) return null;
        var top = Hits[0].gameObject;
        if (top == null) return null;
        var selectable = top.GetComponentInParent<Selectable>();
        return selectable is Button || selectable is Toggle ? selectable : null;
    }

    internal static GameEventSfx.UiSound? Classify(Selectable selectable, bool includePressOwned = false)
    {
        if (selectable.TryGetComponent(out UiClickSound custom))
        {
            if (custom.playOnPress && !includePressOwned) return null;
            return custom.mode switch
            {
                UiClickSound.Mode.None => null,
                UiClickSound.Mode.Tap => GameEventSfx.UiSound.Tap,
                UiClickSound.Mode.Confirm => GameEventSfx.UiSound.Confirm,
                UiClickSound.Mode.Back => GameEventSfx.UiSound.Back,
                _ => AutoClassify(selectable),
            };
        }

        // PreLevel'in boş ses alanları merkezi UI seslerini engellememeli.
        var preLevel = selectable.GetComponentInParent<PreLevelSpecialPopupController>();
        if (preLevel != null && preLevel.HasCustomClickSound(selectable))
            return null;

        if (selectable.GetComponentInParent<BoosterSlotView>() != null)
            return null;

        return AutoClassify(selectable);
    }

    private static readonly string[] BackWords = { "close", "back", "cancel", "exit", "kapat", "geri", "iptal", "quit" };
    private static readonly string[] ConfirmWords = { "continue", "play", "confirm", "buy", "purchase", "claim", "collect", "start", "join", "retry", "done", "accept", "devam", "oyna", "satin", "topla", "basla", "katil", "tamam", "onay" };
    private static readonly string[] OkNames = { "ok", "btnok", "btn_ok", "okbutton", "buttonok" };

    private static GameEventSfx.UiSound AutoClassify(Selectable selectable)
    {
        if (!selectable.IsInteractable()) return GameEventSfx.UiSound.Denied;
        string name = selectable.gameObject.name;
        foreach (var w in BackWords) if (name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return GameEventSfx.UiSound.Back;
        foreach (var w in ConfirmWords) if (name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return GameEventSfx.UiSound.Confirm;
        foreach (var w in OkNames) if (string.Equals(name, w, StringComparison.OrdinalIgnoreCase)) return GameEventSfx.UiSound.Confirm;
        return GameEventSfx.UiSound.Tap;
    }
}
