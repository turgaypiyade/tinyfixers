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

    /// Bir engel tahtadan TAMAMEN kalktı (origin başına bir kez). Progress "engel kır" hedefi sayar.
    public static event Action<ObstacleId> OnObstacleCleared;

    /// Eşleşmeden bir special doğdu. Progress "special oluştur" hedefi sayar.
    public static event Action<TileSpecial> OnSpecialCreated;

    public static void EmitObstacleCleared(ObstacleId id) => OnObstacleCleared?.Invoke(id);

    public static void EmitSpecialCreated(TileSpecial special)
    {
        if (special != TileSpecial.None) OnSpecialCreated?.Invoke(special);
    }

    public static void EmitTileCleared(TileType type, int count)
    {
        if (count > 0) OnTileCleared?.Invoke(type, count);
    }

    public static void EmitTileClearedAt(TileType type, UnityEngine.Vector3 worldPos)
    {
        OnTileClearedAt?.Invoke(type, worldPos);
    }
}
