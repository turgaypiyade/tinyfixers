using System.Collections.Generic;

/// <summary>
/// Levelın hedeflerini ve ilerlemesini tutar. Eski GoalTracker'dan farkı:
///  • Hedef başına ilerleme oranı verir (bot skorlaması + "hangi hedef tıkadı" raporu için).
///  • Simüle EDİLEMEYEN hedefleri (EnergyOrb gibi) işaretler — sessizce %0 win üretmek yerine
///    rapor "bu level tam simüle edilmiyor" diye uyarır.
/// </summary>
public sealed class SimGoalSet
{
    public struct Entry
    {
        public LevelGoalTargetType Kind;
        public TileType Tile;
        public ObstacleId Obstacle;
        public CollectibleId Collectible;
        public int Needed;
        public int Done;
        public SimGoalFidelity Fidelity;

        public bool Met => Done >= Needed;
        public float Ratio => Needed <= 0 ? 1f : (Done >= Needed ? 1f : (float)Done / Needed);

        public string Label => Kind switch
        {
            LevelGoalTargetType.Tile        => Tile.ToString(),
            LevelGoalTargetType.Obstacle    => Obstacle.ToString(),
            LevelGoalTargetType.Collectible => Collectible.ToString(),
            _                               => "?"
        };
    }

    private Entry[] _entries;
    private int[] _tileCleared = new int[TileTypeCount];
    private readonly int _bossDamagePerTile;

    private const int TileTypeCount = 16;

    public IReadOnlyList<Entry> Entries => _entries;
    public bool HasAnyGoal => _entries.Length > 0;

    /// <summary>Bu levelın hedeflerinin en kötü simülasyon güvenilirliği.</summary>
    public SimGoalFidelity Fidelity { get; private set; }

    public SimGoalSet(LevelData level) : this(new SimLevel(level)) { }

    public SimGoalSet(SimLevel level)
    {
        _bossDamagePerTile = level.damagePerClearedTile > 0 ? level.damagePerClearedTile : 10;

        var list = new List<Entry>();
        if (level.goals != null)
        {
            foreach (var g in level.goals)
            {
                var e = new Entry
                {
                    Kind        = g.targetType,
                    Tile        = g.tileType,
                    Obstacle    = g.obstacleId,
                    Collectible = g.collectibleId,
                    Needed      = g.amount < 1 ? 1 : g.amount,
                    Fidelity    = ResolveFidelity(g)
                };
                list.Add(e);
                if (e.Fidelity > Fidelity) Fidelity = e.Fidelity;
            }
        }

        _entries = list.ToArray();
    }

    private SimGoalSet(SimGoalSet src)
    {
        _bossDamagePerTile = src._bossDamagePerTile;
        _entries = new Entry[src._entries.Length];
        Fidelity = src.Fidelity;
        CopyFrom(src);
    }

    public SimGoalSet CreateScratch() => new(this);

    public void CopyFrom(SimGoalSet src)
    {
        System.Array.Copy(src._entries, _entries, _entries.Length);
        System.Array.Copy(src._tileCleared, _tileCleared, _tileCleared.Length);
        Fidelity = src.Fidelity;
    }

    private static SimGoalFidelity ResolveFidelity(LevelGoalDefinition g)
    {
        if (g.targetType == LevelGoalTargetType.Tile) return SimGoalFidelity.Exact;

        if (g.targetType == LevelGoalTargetType.Collectible)
            return g.collectibleId == CollectibleId.BossDamage
                ? SimGoalFidelity.Approximate      // taş başına sabit hasar; boss saldırıları yok
                : SimGoalFidelity.NotSimulated;    // EnergyOrb: konteyner/launcher sistemi yok

        return g.obstacleId switch
        {
            ObstacleId.EnergyContainer => SimGoalFidelity.NotSimulated,
            ObstacleId.HatLauncher     => SimGoalFidelity.NotSimulated,
            ObstacleId.Oil             => SimGoalFidelity.Approximate,  // yayılma modellenmiyor
            ObstacleId.Barrel          => SimGoalFidelity.Approximate,  // mud saçılması yok
            ObstacleId.Wardrobe        => SimGoalFidelity.Approximate,
            ObstacleId.Magnet          => SimGoalFidelity.Approximate,  // uçlara yaklaşma yok
            ObstacleId.Safe            => SimGoalFidelity.Approximate,  // 3 kilit tek sayaç
            ObstacleId.KeyGenerator    => SimGoalFidelity.NotSimulated,
            ObstacleId.RocketBasket    => SimGoalFidelity.NotSimulated,
            ObstacleId.SpreadingGel    => SimGoalFidelity.Approximate,  // yayılma modellenmiyor
            ObstacleId.Grass           => SimGoalFidelity.Approximate,
            ObstacleId.Tube            => SimGoalFidelity.Approximate,  // küçülme adımları yok
            _                          => SimGoalFidelity.Exact
        };
    }

    // ── İlerleme ─────────────────────────────────────────────────────────────

    public void RecordTileCleared(TileType t)
    {
        int i = (int)t;
        if (i >= 0 && i < _tileCleared.Length) _tileCleared[i]++;

        for (int e = 0; e < _entries.Length; e++)
        {
            ref var entry = ref _entries[e];
            if (entry.Kind == LevelGoalTargetType.Tile && entry.Tile == t)
                entry.Done++;
            else if (entry.Kind == LevelGoalTargetType.Collectible &&
                     entry.Collectible == CollectibleId.BossDamage)
                entry.Done += _bossDamagePerTile;
        }
    }

    /// <summary>Cascade adımından sonra obstacle sayaçlarını katmandan çeker.</summary>
    public void SyncObstacles(SimObstacleLayer obs)
    {
        if (obs == null) return;
        for (int e = 0; e < _entries.Length; e++)
        {
            ref var entry = ref _entries[e];
            if (entry.Kind != LevelGoalTargetType.Obstacle) continue;
            entry.Done = obs.GetClearedCount(entry.Obstacle);
        }
    }

    public bool AllMet
    {
        get
        {
            if (_entries.Length == 0) return false;
            for (int i = 0; i < _entries.Length; i++)
                if (!_entries[i].Met) return false;
            return true;
        }
    }

    /// <summary>0..1 — hedeflerin ortalama tamamlanma oranı (bot skorlamasının ana terimi).</summary>
    public float Completion
    {
        get
        {
            if (_entries.Length == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _entries.Length; i++) sum += _entries[i].Ratio;
            return sum / _entries.Length;
        }
    }

    /// <summary>Hedefe kalan toplam birim — küçüldükçe iyi.</summary>
    public int RemainingUnits
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                var e = _entries[i];
                if (!e.Met) sum += e.Needed - e.Done;
            }
            return sum;
        }
    }

    /// <summary>Bu hedef türü hâlâ eksik mi? Bot "gereksiz" temizliği ödüllendirmesin diye.</summary>
    public bool NeedsTile(TileType t)
    {
        for (int i = 0; i < _entries.Length; i++)
        {
            var e = _entries[i];
            if (e.Kind == LevelGoalTargetType.Tile && e.Tile == t && !e.Met) return true;
            if (e.Kind == LevelGoalTargetType.Collectible && e.Collectible == CollectibleId.BossDamage && !e.Met)
                return true;
        }
        return false;
    }

    public bool NeedsObstacle(ObstacleId id)
    {
        for (int i = 0; i < _entries.Length; i++)
        {
            var e = _entries[i];
            if (e.Kind == LevelGoalTargetType.Obstacle && e.Obstacle == id && !e.Met) return true;
        }
        return false;
    }

    /// <summary>Bu obstacle hedefinde kalan miktar (yoksa 0). Movable respawn kuralı bunu kullanır.</summary>
    public int RemainingFor(ObstacleId id)
    {
        int remaining = 0;
        for (int i = 0; i < _entries.Length; i++)
        {
            var e = _entries[i];
            if (e.Kind == LevelGoalTargetType.Obstacle && e.Obstacle == id && !e.Met)
                remaining += e.Needed - e.Done;
        }
        return remaining;
    }

    public int GoalCount => _entries.Length;
    public Entry GetEntry(int i) => _entries[i];
}

/// <summary>Bir hedefin simülasyonda ne kadar doğru modellendiği.</summary>
public enum SimGoalFidelity
{
    Exact = 0,          // birebir
    Approximate = 1,    // yaklaşık (yayılma/çok-kilit gibi ayrıntılar yok)
    NotSimulated = 2    // hiç modellenmiyor → win% anlamsız
}
