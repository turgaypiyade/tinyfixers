using UnityEngine;

/// <summary>
/// Sıralı harika listesi + oyun ilk açıldığında (hiç tamamlanmamışken) gösterilecek
/// default arka plan. WonderProgress bu listeyi indeksler. [[project_wonder_reveal_background]]
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Wonder Catalog", fileName = "WonderCatalog")]
public class WonderCatalog : ScriptableObject
{
    [Tooltip("Hiç harika tamamlanmadan ana menüde duracak varsayılan arka plan")]
    public Sprite defaultBackground;

    [Tooltip("Sırayla açılacak harikalar")]
    public WonderDefinition[] wonders;

    public int Count => wonders != null ? wonders.Length : 0;

    public WonderDefinition Get(int i)
        => (wonders != null && i >= 0 && i < wonders.Length) ? wonders[i] : null;
}
