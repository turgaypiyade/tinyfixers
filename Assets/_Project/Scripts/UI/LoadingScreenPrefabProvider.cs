using UnityEngine;

/// <summary>
/// Scene-side provider for loading screen prefabs.
/// Put this on a MainMenu scene object and drag the prefab from Assets/_Project/Prefabs/UI.
/// LoadingScreenManager reads this before falling back to Resources/UI/LoadingHintView.
/// </summary>
public class LoadingScreenPrefabProvider : MonoBehaviour
{
    private static LoadingScreenPrefabProvider current;

    [SerializeField] private LoadingHintView loadingHintViewPrefab;

    public static LoadingHintView LoadingHintViewPrefab
    {
        get
        {
            if (current != null && current.loadingHintViewPrefab != null)
                return current.loadingHintViewPrefab;

            var provider = FindFirstObjectByType<LoadingScreenPrefabProvider>(FindObjectsInactive.Include);
            if (provider != null)
            {
                current = provider;
                return provider.loadingHintViewPrefab;
            }

            return null;
        }
    }

    private void Awake()
    {
        current = this;
    }

    private void OnEnable()
    {
        current = this;
    }

    private void OnDisable()
    {
        if (current == this)
            current = null;
    }

    private void OnDestroy()
    {
        if (current == this)
            current = null;
    }
}
