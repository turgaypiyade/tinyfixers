using UnityEngine;


[CreateAssetMenu(menuName = "CoreCollapse/Tile Icon Library")]
public class TileIconLibrary : ScriptableObject
{
    public Sprite gear;
    public Sprite core;
    public Sprite bolt;
    public Sprite plate;
    
    [Header("Special Tile Icons")]
    public Sprite lineH;
    public Sprite lineV;
    public Sprite patchBot;
    public Sprite pulseCore;

    [Header("Override Icons (color-keyed)")]
    public Sprite overrideRed;
    public Sprite overrideYellow;
    public Sprite overrideBlue;
    public Sprite overrideGreen;


    public Sprite GetSpecialIcon(TileSpecial special)
    {
        switch (special)
        {
            case TileSpecial.LineH:
                return lineH;

            case TileSpecial.LineV:
                return lineV;

            case TileSpecial.PatchBot:
                return patchBot;

            case TileSpecial.PulseCore:
                return pulseCore;

            case TileSpecial.SystemOverride:
                return overrideRed; // default fallback — callers should use GetOverrideIcon for color-keyed

            default:
                return null;
        }
    }

    /// <summary>
    /// Returns the color-specific Override icon based on the base TileType
    /// that formed the 5-match.
    /// </summary>
    public Sprite GetOverrideIcon(TileType baseType)
    {
        switch (baseType)
        {
            case TileType.Gear:   return overrideRed;
            case TileType.Core:   return overrideBlue;
            case TileType.Bolt:   return overrideYellow;
            case TileType.Plate:  return overrideGreen;
            default:              return overrideRed;
        }
    }

    public Sprite Get(TileType type)
    {
        return type switch
        {
            TileType.Gear => gear,
            TileType.Core => core,
            TileType.Bolt => bolt,
            TileType.Plate => plate,
            _ => null
        };
    }
}
