#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Editor/dev-build slow-motion toggle for inspecting tile-fall feel.
/// Sahnedeki herhangi bir GameObject'e ekle. Play Mode'da:
///   - [S]    : ağır çekimi aç/kapat (slowScale ↔ 1)
///   - [,]    : daha da yavaşlat
///   - [.]    : hızlandır (1'e kadar)
///   - [0]    : duraklat (timeScale = 0)
/// Yalnızca Editor ve Development Build'de derlenir; release build'i etkilemez.
/// </summary>
public class SlowMoDebug : MonoBehaviour
{
    [Tooltip("[S] ile geçilecek ağır çekim ölçeği. 0.2 = %20 hız.")]
    [SerializeField, Range(0.02f, 1f)] private float slowScale = 0.2f;

    [Tooltip("[,] / [.] tuşlarının her basışta uyguladığı çarpan adımı.")]
    [SerializeField, Range(1.05f, 2f)] private float stepFactor = 1.25f;

    private float lastNonZeroScale = 1f;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.sKey.wasPressedThisFrame)
            SetScale(Mathf.Approximately(Time.timeScale, 1f) ? slowScale : 1f);

        if (kb.commaKey.wasPressedThisFrame)
            SetScale(Time.timeScale / stepFactor);

        if (kb.periodKey.wasPressedThisFrame)
            SetScale(Time.timeScale * stepFactor);

        if (kb.digit0Key.wasPressedThisFrame)
            SetScale(Time.timeScale > 0f ? 0f : lastNonZeroScale);
    }

    private void SetScale(float scale)
    {
        scale = Mathf.Clamp(scale, 0f, 1f);
        Time.timeScale = scale;
        if (scale > 0f)
            lastNonZeroScale = scale;
        Debug.Log($"[SlowMoDebug] Time.timeScale = {scale:0.000}");
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }
}
#endif
