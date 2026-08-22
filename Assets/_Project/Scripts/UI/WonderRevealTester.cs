using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WonderRevealView prototip test paneli. Play modunda:
///  - "Yıldız Harca (+1 Kademe)" butonu: animasyonlu bir kademe açar
///  - "Sıfırla" butonu: baştan başlatır
///  - Slider: ham _Reveal önizleme (animasyonsuz)
/// Gerçek entegrasyonda bu bileşen yerine yıldız-harcama akışı AdvanceOneStage()'i çağırır.
/// </summary>
public class WonderRevealTester : MonoBehaviour
{
    public WonderRevealView view;
    public Button advanceButton;
    public Button resetButton;
    public Slider revealSlider;

    void Awake()
    {
        if (advanceButton != null) advanceButton.onClick.AddListener(() => view.AdvanceOneStage());
        if (resetButton != null) resetButton.onClick.AddListener(() => view.SetStage(0, animated: true));
        if (revealSlider != null)
            revealSlider.onValueChanged.AddListener(v =>
            {
                view.previewReveal = v;
                view.PreviewRevealValue(v);
            });
    }
}
