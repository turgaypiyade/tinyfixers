using UnityEngine;

/// <summary>
/// ScreenCrackFx görselleri (Resources/ScreenCrack/ScreenCrackConfig).
/// crackSprite boşsa çatlak prosedürel çizilir.
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/VFX/Screen Crack Config", fileName = "ScreenCrackConfig")]
public class ScreenCrackConfig : ScriptableObject
{
    [Tooltip("Opsiyonel hazır çatlak görseli (şeffaf PNG). Boşsa prosedürel örümcek ağı çizilir.")]
    public Sprite crackSprite;

    [Header("Tamirci maymun (Bridge event kareleri)")]
    public Sprite[] monkeyWalk;
    public Sprite[] monkeyBend;

    [Tooltip("Kaynak sırasında GameEventSfx welding loop'u çalsın.")]
    public bool useWeldingSfx = true;
}
