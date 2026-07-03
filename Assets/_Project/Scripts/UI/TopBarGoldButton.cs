using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Üst bardaki "+" altın butonuna (ArtiButtonGold) eklenir. Tıklanınca marketi açar.
/// Button bileşenini kendisi garanti eder (Awake'te ekler) → sahnede ekstra kurulum gerekmez.
/// </summary>
[DisallowMultipleComponent]
public sealed class TopBarGoldButton : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        var img = GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        _button = GetComponent<Button>();
        if (_button == null)
            _button = gameObject.AddComponent<Button>();
    }

    private void OnEnable()
    {
        if (_button == null) return;
        _button.onClick.RemoveListener(OpenMarket);
        _button.onClick.AddListener(OpenMarket);
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OpenMarket);
    }

    private void OpenMarket() => MarketNavigator.OpenMarket();
}
