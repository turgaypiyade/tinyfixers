public class TileData
{
    public int X { get; private set; }
    public int Y { get; private set; }

    public TileType Type { get; private set; }
    public TileSpecial Special { get; private set; }
    
    public TileType OverrideBaseType { get; private set; }
    public bool HasOverrideBaseType { get; private set; }

    public TileData(int x, int y, TileType type)
    {
        X = x;
        Y = y;
        Type = type;
        Special = TileSpecial.None;
        HasOverrideBaseType = false;
    }

    public void SetCoords(int x, int y)
    {
        X = x;
        Y = y;
    }

    public void SetType(TileType newType)
    {
        Type = newType;
    }

    public void SetSpecial(TileSpecial newSpecial)
    {
        Special = newSpecial;
        if (Special != TileSpecial.SystemOverride)
            HasOverrideBaseType = false;
    }

    public void SetOverrideBaseType(TileType type)
    {
        OverrideBaseType = type;
        HasOverrideBaseType = true;
    }

    public bool TryGetOverrideBaseType(out TileType type)
    {
        type = OverrideBaseType;
        return HasOverrideBaseType;
    }

    public void ClearSpecial()
    {
        Special = TileSpecial.None;
        HasOverrideBaseType = false;
    }

    public string ToDebugString()
    {
        if (Special == TileSpecial.SystemOverride) return "S";
        
        char baseChar = Type.ToString()[0];
        if (Special == TileSpecial.LineH) return $"{baseChar}-";
        if (Special == TileSpecial.LineV) return $"{baseChar}|";
        if (Special == TileSpecial.PatchBot) return $"{baseChar}B";
        if (Special == TileSpecial.PulseCore) return $"{baseChar}*";

        return baseChar.ToString();
    }

    public bool IsSpecial => Special != TileSpecial.None;
    public bool IsLine => Special == TileSpecial.LineH || Special == TileSpecial.LineV;
}
