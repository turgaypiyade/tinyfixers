using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoardController))]
public class BoardBorderThemeApplier : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private BorderColorId borderColor = BorderColorId.Orange;
    [SerializeField] private BorderSpriteLibrary borderSpriteLibrary;

    [Header("Target")]
    [SerializeField] private DynamicBoardBorder borderDrawer;

    public BorderColorId BorderColor
    {
        get => borderColor;
        set
        {
            if (borderColor == value)
                return;

            borderColor = value;
            ApplyTheme();
        }
    }

    public BorderSpriteLibrary BorderSpriteLibrary
    {
        get => borderSpriteLibrary;
        set
        {
            borderSpriteLibrary = value;
            ApplyTheme();
        }
    }

    public DynamicBoardBorder BorderDrawer
    {
        get => borderDrawer;
        set
        {
            borderDrawer = value;
            ApplyTheme();
        }
    }

    private void Awake()
    {
        ResolveBorderDrawerIfNeeded();
        ApplyTheme();
    }

    private void OnEnable()
    {
        ResolveBorderDrawerIfNeeded();
        ApplyTheme();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveBorderDrawerIfNeeded();
        ApplyTheme();
    }
#endif

    public void ApplyTheme()
    {
        ResolveBorderDrawerIfNeeded();

        if (borderDrawer == null || borderSpriteLibrary == null)
            return;

        BorderSpriteSet set = borderSpriteLibrary.Get(borderColor);
        if (set == null)
        {
            Debug.LogWarning($"BoardBorderThemeApplier: Border sprite set not found for {borderColor}.", this);
            return;
        }

        borderDrawer.ApplySpriteSet(set);
    }

    public bool TryGetSelectedSet(out BorderSpriteSet set)
    {
        set = borderSpriteLibrary != null ? borderSpriteLibrary.Get(borderColor) : null;
        return set != null;
    }

    private void ResolveBorderDrawerIfNeeded()
    {
        if (borderDrawer != null)
            return;

        borderDrawer = GetComponent<DynamicBoardBorder>()
                    ?? GetComponentInChildren<DynamicBoardBorder>(true)
                    ?? GetComponentInParent<DynamicBoardBorder>(true);
    }
}
