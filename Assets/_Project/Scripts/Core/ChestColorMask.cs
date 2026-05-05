using System;

[Flags]
public enum ChestColorMask
{
    None  = 0,
    Gear  = 1 << 0,
    Core  = 1 << 1,
    Bolt  = 1 << 2,
    Plate = 1 << 3,
    All   = Gear | Core | Bolt | Plate
}

public static class TileTypeChestExtensions
{
    public static ChestColorMask ToChestColor(this TileType? tileType) => tileType switch
    {
        TileType.Gear  => ChestColorMask.Gear,
        TileType.Core  => ChestColorMask.Core,
        TileType.Bolt  => ChestColorMask.Bolt,
        TileType.Plate => ChestColorMask.Plate,
        _              => ChestColorMask.None
    };

    public static ChestColorMask ToChestColor(this TileType tileType) =>
        ((TileType?)tileType).ToChestColor();
}
