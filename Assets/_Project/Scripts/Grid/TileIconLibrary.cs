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
    public Sprite systemOverride;


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
                return systemOverride;

            default:
                return null;
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
