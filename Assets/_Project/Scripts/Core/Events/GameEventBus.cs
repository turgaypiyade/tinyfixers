using System;

/// Merkezi static event bus. Match kodu bu sınıfa emit eder;
/// progress, analitik veya başka sistemler subscribe olur.
/// SpecialBehaviorEvents ile aynı static-hub desenini izler.
public static class GameEventBus
{
    public static event Action<TileType, int> OnTileCleared;

    /// Tek taş kırılırken DÜNYA pozisyonuyla yayınlanır (FX için — sayım OnTileCleared'da).
    /// ProgressEventFxDriver bunları buffer'layıp "+1"i taşın yanında doğurur.
    public static event Action<TileType, UnityEngine.Vector3> OnTileClearedAt;

    public static void EmitTileCleared(TileType type, int count)
    {
        if (count > 0) OnTileCleared?.Invoke(type, count);
    }

    public static void EmitTileClearedAt(TileType type, UnityEngine.Vector3 worldPos)
    {
        OnTileClearedAt?.Invoke(type, worldPos);
    }
}
