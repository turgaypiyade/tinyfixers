using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>
/// ObstacleLibrary'nin Unity'den bağımsız, salt-okunur anlık görüntüsü.
/// TEK SEFER ana thread'de kurulur; simülasyonun sıcak yolu bir daha ScriptableObject'e
/// dokunmaz — paralel (çok thread'li) çalıştırmayı legal kılan şey budur.
/// </summary>
public sealed class SimRules
{
    public readonly struct Stage
    {
        public readonly ObstacleBehaviorType Behavior;
        public readonly ObstacleDamageSourceRule DamageRule;
        public readonly bool BlocksCells;
        public readonly bool LocksInteraction;
        public readonly bool HoldsTile;
        public readonly bool AllowDiagonal;

        public Stage(StageRule src)
        {
            Behavior         = src != null ? src.behavior : ObstacleBehaviorType.OverTileBlocker;
            DamageRule       = src != null ? src.damageRule : ObstacleDamageSourceRule.Any;
            BlocksCells      = src != null && src.blocksCells;
            LocksInteraction = src != null && src.locksInteraction;
            HoldsTile        = src != null && src.holdsTile;
            AllowDiagonal    = src != null && src.allowDiagonal;
        }

        public bool IsMovable      => Behavior == ObstacleBehaviorType.MovableObstacle;
        public bool IsUnderTile    => Behavior == ObstacleBehaviorType.UnderTileLayered;
        public bool IsOverlay      => Behavior == ObstacleBehaviorType.CellAnchoredOverlay;
        public bool IsOverTileDamage => Behavior == ObstacleBehaviorType.OverTileBlocker
                                     || Behavior == ObstacleBehaviorType.RevealOnBreak
                                     || Behavior == ObstacleBehaviorType.MovableObstacle;
    }

    /// <summary>Tek bir ObstacleId'nin tüm gameplay kuralları — sprite/ses yok.</summary>
    public sealed class Rule
    {
        public ObstacleId Id;
        public int Hits = 1;
        public Stage[] Stages = System.Array.Empty<Stage>();
        public bool RestrictNormalMatchTileType;
        public TileType RequiredNormalMatchTileType;
        public bool ExitAtBottom;
        public bool FillsShadowBeneath;
        public int SizeX = 1, SizeY = 1;
        /// <summary>Def bulunamadı → varsayılan (1 vuruşluk over-tile blocker) ile yürüdük.</summary>
        public bool IsFallback;

        // ObstacleDef.ResolveStageIndex ile birebir aynı.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int StageIndex(int remainingHits)
        {
            if (Stages.Length == 0) return -1;
            int maxHits = Hits < 1 ? 1 : Hits;
            int hits    = remainingHits < 0 ? 0 : (remainingHits > maxHits ? maxHits : remainingHits);
            if (hits <= 0) return -1;                       // kırıldı → stage yok
            int damageTaken = maxHits - hits;
            return damageTaken >= Stages.Length ? Stages.Length - 1 : damageTaken;
        }

        public Stage StageFor(int remainingHits)
        {
            int i = StageIndex(remainingHits);
            return i < 0 ? default : Stages[i];
        }

        public bool IsMovable(int rem)          => Has(rem) && StageFor(rem).IsMovable;
        public bool BlocksCells(int rem)        => Has(rem) && StageFor(rem).BlocksCells;
        public bool LocksInteraction(int rem)   => Has(rem) && StageFor(rem).LocksInteraction;
        public bool HoldsTile(int rem)          => Has(rem) && StageFor(rem).HoldsTile;
        public bool IsUnderTile(int rem)        => Has(rem) && StageFor(rem).IsUnderTile;
        public bool IsOverlay(int rem)          => Has(rem) && StageFor(rem).IsOverlay;
        public bool IsOverTileDamage(int rem)   => Has(rem) && StageFor(rem).IsOverTileDamage;

        public ObstacleDamageSourceRule Damage(int rem)
            => Has(rem) ? StageFor(rem).DamageRule : ObstacleDamageSourceRule.Any;

        private bool Has(int rem) => StageIndex(rem) >= 0;
    }

    private static readonly Dictionary<ObstacleLibrary, SimRules> _cache = new();
    private static readonly Rule _fallback = new()
    {
        Id = ObstacleId.None,
        Hits = 1,
        IsFallback = true,
        Stages = new[] { new Stage(new StageRule { behavior = ObstacleBehaviorType.OverTileBlocker }) }
    };

    private readonly Dictionary<ObstacleId, Rule> _map = new();

    // Paralel oyunlardan yazılır → kilit şart (HashSet thread-safe değil).
    private readonly HashSet<ObstacleId> _missingDefs = new();

    /// <summary>Kütüphanede tanımı OLMAYAN, levelda kullanılan obstacle id'leri (rapor uyarısı için).</summary>
    public ObstacleId[] MissingDefs
    {
        get { lock (_missingDefs) { var a = new ObstacleId[_missingDefs.Count]; _missingDefs.CopyTo(a); return a; } }
    }

    private SimRules() { }

    /// <summary>ANA THREAD'den çağır. Kütüphane başına sonuç cache'lenir.</summary>
    public static SimRules From(ObstacleLibrary lib)
    {
        if (lib == null) return new SimRules();

        if (_cache.TryGetValue(lib, out var cached)) return cached;

        var rules = new SimRules();
        foreach (var def in lib.obstacles)
        {
            if (def == null || rules._map.ContainsKey(def.id)) continue;
            rules._map[def.id] = Snapshot(def);
        }

        _cache[lib] = rules;
        return rules;
    }

    /// <summary>Editörde asset değişince elle temizlemek için.</summary>
    public static void ClearCache() => _cache.Clear();

    public Rule Get(ObstacleId id)
    {
        if (id == ObstacleId.None) return null;
        if (_map.TryGetValue(id, out var r)) return r;
        lock (_missingDefs) _missingDefs.Add(id);
        return _fallback;
    }

    public bool HasDef(ObstacleId id) => _map.ContainsKey(id);

    private static Rule Snapshot(ObstacleDef def)
    {
        def.EnsureStageSlots();

        var stages = new Stage[def.stages.Count];
        for (int i = 0; i < stages.Length; i++)
            stages[i] = new Stage(def.stages[i]);

        return new Rule
        {
            Id                          = def.id,
            Hits                        = def.hits < 1 ? 1 : def.hits,
            Stages                      = stages,
            RestrictNormalMatchTileType = def.restrictNormalMatchTileType,
            RequiredNormalMatchTileType = def.requiredNormalMatchTileType,
            ExitAtBottom                = def.exitAtBottom,
            FillsShadowBeneath          = def.fillsShadowBeneath,
            SizeX                       = def.size.x < 1 ? 1 : def.size.x,
            SizeY                       = def.size.y < 1 ? 1 : def.size.y,
        };
    }
}
