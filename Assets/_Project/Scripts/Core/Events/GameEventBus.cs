using System;

/// Merkezi static event bus. Match kodu bu sınıfa emit eder;
/// progress, analitik veya başka sistemler subscribe olur.
/// SpecialBehaviorEvents ile aynı static-hub desenini izler.
public static class GameEventBus
{
    public static event Action<TileType, int> OnTileCleared;

    public static void EmitTileCleared(TileType type, int count)
    {
        if (count > 0) OnTileCleared?.Invoke(type, count);
    }
}
