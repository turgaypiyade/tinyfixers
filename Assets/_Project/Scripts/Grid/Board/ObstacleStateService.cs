using System;
using System.Collections.Generic;
using UnityEngine;

public enum ObstacleHitContext
{
    NormalMatch = 0,
    SpecialActivation = 1,
    Booster = 2,
    Scripted = 3
}

public readonly struct ObstacleVisualChange
{
    public readonly int originIndex;
    public readonly ObstacleId obstacleId;
    public readonly bool cleared;
    public readonly int remainingHits;
    public readonly Sprite sprite;

    public ObstacleVisualChange(int originIndex, ObstacleId obstacleId, bool cleared, int remainingHits, Sprite sprite)
    {
        this.originIndex = originIndex;
        this.obstacleId = obstacleId;
        this.cleared = cleared;
        this.remainingHits = remainingHits;
        this.sprite = sprite;
    }
}

public readonly struct ObstacleStageSnapshot
{
    public readonly ObstacleBehaviorType behavior;
    public readonly bool blocksCells;
    public readonly bool allowDiagonal;
    public readonly ObstacleDamageSourceRule damageRule;
    public readonly Sprite sprite;

    public ObstacleStageSnapshot(ObstacleBehaviorType behavior, bool blocksCells, bool allowDiagonal, ObstacleDamageSourceRule damageRule, Sprite sprite)
    {
        this.behavior = behavior;
        this.blocksCells = blocksCells;
        this.allowDiagonal = allowDiagonal;
        this.damageRule = damageRule;
        this.sprite = sprite;
    }
}

public readonly struct ObstacleStageTransition
{
    public readonly bool hasTransition;
    public readonly int originIndex;
    public readonly ObstacleId obstacleId;
    public readonly int remainingHitsAfter;
    public readonly bool cleared;
    public readonly ObstacleStageSnapshot previousStage;
    public readonly ObstacleStageSnapshot currentStage;

    public ObstacleStageTransition(bool hasTransition, int originIndex, ObstacleId obstacleId, int remainingHitsAfter, bool cleared,
        ObstacleStageSnapshot previousStage, ObstacleStageSnapshot currentStage)
    {
        this.hasTransition = hasTransition;
        this.originIndex = originIndex;
        this.obstacleId = obstacleId;
        this.remainingHitsAfter = remainingHitsAfter;
        this.cleared = cleared;
        this.previousStage = previousStage;
        this.currentStage = currentStage;
    }
}

public class ObstacleStateService
{
    private LevelData level;
    private ObstacleLibrary library;

    // Sadece origin hücre indexi anlamlıdır. -1 => obstacle yok.
    private int[] remainingHitsByOrigin = Array.Empty<int>();

    public event Action<int, ObstacleStageSnapshot> OnObstacleStageChanged;
    public event Action<int, ObstacleId> OnObstacleDestroyed;
    public event Action<int> OnCellUnlocked;

    public readonly struct ObstacleHitResult
    {
        public readonly bool didHit;
        public readonly bool consumedHit;
        public readonly bool rejectedByRule;
        public readonly ObstacleVisualChange visualChange;
        public readonly ObstacleStageTransition stageTransition;
        public readonly int[] affectedCellIndices;

        public ObstacleHitResult(bool didHit, bool consumedHit, bool rejectedByRule, ObstacleVisualChange visualChange,
            ObstacleStageTransition stageTransition, int[] affectedCellIndices)
        {
            this.didHit = didHit;
            this.consumedHit = consumedHit;
            this.rejectedByRule = rejectedByRule;
            this.visualChange = visualChange;
            this.stageTransition = stageTransition;
            this.affectedCellIndices = affectedCellIndices;
        }
    }

    public ObstacleStateService()
    {
    }

    public ObstacleStateService(LevelData level)
    {
        Initialize(level);
    }

    public void Initialize(LevelData level)
    {
        this.level = level;
        library = level != null ? level.obstacleLibrary : null;

        int size = (level != null) ? level.width * level.height : 0;
        remainingHitsByOrigin = new int[size];
        for (int i = 0; i < size; i++) remainingHitsByOrigin[i] = -1;

        InitializeFromLevel();
    }

    public void InitializeFromLevel()
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null)
            return;

        int size = Mathf.Min(remainingHitsByOrigin.Length, Mathf.Min(level.obstacles.Length, level.obstacleOrigins.Length));

        for (int i = 0; i < size; i++)
            remainingHitsByOrigin[i] = -1;

        for (int idx = 0; idx < size; idx++)
        {
            var id = (ObstacleId)level.obstacles[idx];
            if (id == ObstacleId.None) continue;

            int origin = level.obstacleOrigins[idx];
            if (origin != idx) continue;
            if (origin < 0 || origin >= size) continue;

            var def = library != null ? library.Get(id) : null;
            int hits = Mathf.Max(1, def != null ? def.hits : 1);
            remainingHitsByOrigin[origin] = hits;
        }
    }

    public bool TryDamageAt(int x, int y, out ObstacleVisualChange change)
    {
        var result = TryDamageAt(x, y, ObstacleHitContext.NormalMatch);
        change = result.visualChange;
        return result.didHit;
    }

    public ObstacleHitResult TryDamageAt(int x, int y)
    {
        return TryDamageAt(x, y, ObstacleHitContext.NormalMatch);
    }

    public ObstacleHitResult TryDamageAt(int x, int y, ObstacleHitContext context)
    {
        return TryDamageAtInternal(x, y, context, ignoreDamageRule: false);
    }

    public ObstacleHitResult TryDamageAtIgnoringRule(int x, int y, ObstacleHitContext context)
    {
        return TryDamageAtInternal(x, y, context, ignoreDamageRule: true);
    }

    private ObstacleHitResult TryDamageAtInternal(int x, int y, ObstacleHitContext context, bool ignoreDamageRule)
    {
        ObstacleVisualChange change = default;

        if (level == null || level.obstacles == null || level.obstacleOrigins == null)
            return new ObstacleHitResult(false, false, false, default, default, Array.Empty<int>());

        if (!level.InBounds(x, y))
            return new ObstacleHitResult(false, false, false, default, default, Array.Empty<int>());

        int idx = level.Index(x, y);
        if (idx < 0 || idx >= level.obstacles.Length || idx >= level.obstacleOrigins.Length)
            return new ObstacleHitResult(false, false, false, default, default, Array.Empty<int>());

        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None)
            return new ObstacleHitResult(false, false, false, default, default, Array.Empty<int>());

        int origin = level.obstacleOrigins[idx];
        if (origin < 0 || origin >= remainingHitsByOrigin.Length)
            return new ObstacleHitResult(false, false, false, default, default, Array.Empty<int>());

        var def = library != null ? library.Get(id) : null;

        int remaining = remainingHitsByOrigin[origin];
        if (remaining < 0)
        {
            remaining = Mathf.Max(1, def != null ? def.hits : 1);
        }

        if (!ignoreDamageRule && !CanConsumeHit(def, remaining, context))
            return new ObstacleHitResult(false, false, true, default, default, Array.Empty<int>());

        int[] affectedCells = CollectCellsForOrigin(origin, id);
        var previousStage = CreateSnapshot(def, id, remaining);

        remaining--;
        remainingHitsByOrigin[origin] = remaining;

        if (remaining <= 0)
        {
            ClearObstacleFromLevel(origin, id);
            change = new ObstacleVisualChange(origin, id, true, 0, null);
            var transition = new ObstacleStageTransition(true, origin, id, 0, true, previousStage, default);
            return new ObstacleHitResult(true, true, false, change, transition, affectedCells);
        }

        var currentStage = CreateSnapshot(def, id, remaining);
        var sprite = ResolveStageSprite(def, id, remaining);
        change = new ObstacleVisualChange(origin, id, false, remaining, sprite);
        var stageTransition = new ObstacleStageTransition(true, origin, id, remaining, false, previousStage, currentStage);
        OnObstacleStageChanged?.Invoke(origin, currentStage);
        return new ObstacleHitResult(true, true, false, change, stageTransition, affectedCells);
    }

    public bool HasObstacleAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;
        int idx = level.Index(x, y);
        return (ObstacleId)level.obstacles[idx] != ObstacleId.None;
    }

    public ObstacleId GetObstacleIdAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return ObstacleId.None;
        int idx = level.Index(x, y);
        return (ObstacleId)level.obstacles[idx];
    }

    public bool IsCellBlocked(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;

        int idx = level.Index(x, y);
        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None) return false;

        var def = library != null ? library.Get(id) : null;
        int remaining = ResolveRemainingHitsForCell(idx, def);
        return def != null && def.GetBlocksCellsForRemainingHits(remaining);
    }

    public bool IsOverTileBlockerAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;

        int idx = level.Index(x, y);
        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None) return false;

        var def = library != null ? library.Get(id) : null;
        int remaining = ResolveRemainingHitsForCell(idx, def);
        return def != null && def.IsOverTileDamageBehaviorForRemainingHits(remaining);
    }

    public bool IsDiagonalAllowedAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;

        int idx = level.Index(x, y);
        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None) return false;

        var def = library != null ? library.Get(id) : null;
        int remaining = ResolveRemainingHitsForCell(idx, def);
        return def != null && def.GetAllowDiagonalForRemainingHits(remaining);
    }


    private int ResolveRemainingHitsForCell(int idx, ObstacleDef def)
    {
        if (level == null || level.obstacleOrigins == null || idx < 0 || idx >= level.obstacleOrigins.Length)
            return Mathf.Max(1, def != null ? def.hits : 1);

        int origin = level.obstacleOrigins[idx];
        if (origin < 0 || origin >= remainingHitsByOrigin.Length)
            return Mathf.Max(1, def != null ? def.hits : 1);

        int remaining = remainingHitsByOrigin[origin];
        if (remaining >= 0)
            return remaining;

        return Mathf.Max(1, def != null ? def.hits : 1);
    }

    private bool IsValidCell(int x, int y)
    {
        return level != null
               && level.obstacles != null
               && level.obstacleOrigins != null
               && level.InBounds(x, y);
    }

    private bool CanConsumeHit(ObstacleDef def, int remainingHits, ObstacleHitContext context)
    {
        if (def == null)
            return true;

        ObstacleDamageSourceRule rule = def.GetDamageRuleForRemainingHits(remainingHits);
        return DoesContextMatchRule(context, rule);
    }

    private bool DoesContextMatchRule(ObstacleHitContext context, ObstacleDamageSourceRule rule)
    {
        switch (rule)
        {
            case ObstacleDamageSourceRule.SpecialOnly:
                return context == ObstacleHitContext.SpecialActivation;
            case ObstacleDamageSourceRule.NormalOnly:
                return context == ObstacleHitContext.NormalMatch;
            case ObstacleDamageSourceRule.BoosterOnly:
                return context == ObstacleHitContext.Booster;
            case ObstacleDamageSourceRule.Any:
            default:
                return true;
        }
    }

    private Sprite ResolveStageSprite(ObstacleDef def, ObstacleId id, int remainingHits)
    {
        def ??= library != null ? library.Get(id) : null;
        if (def == null) return null;
        return def.GetSpriteForRemainingHits(remainingHits);
    }

    private ObstacleStageSnapshot CreateSnapshot(ObstacleDef def, ObstacleId id, int remainingHits)
    {
        def ??= library != null ? library.Get(id) : null;
        var stage = def != null ? def.GetStageRuleForRemainingHits(remainingHits) : null;
        if (stage == null)
            return default;

        return new ObstacleStageSnapshot(stage.behavior, stage.blocksCells, stage.allowDiagonal, stage.damageRule, stage.sprite);
    }

    private int[] CollectCellsForOrigin(int origin, ObstacleId originId)
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null)
            return Array.Empty<int>();

        List<int> cells = null;
        for (int i = 0; i < level.obstacles.Length; i++)
        {
            if ((ObstacleId)level.obstacles[i] != originId) continue;
            if (level.obstacleOrigins[i] != origin) continue;

            cells ??= new List<int>(4);
            cells.Add(i);
        }

        return cells != null ? cells.ToArray() : Array.Empty<int>();
    }

    private void ClearObstacleFromLevel(int origin, ObstacleId originId)
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null)
            return;

        if (origin < 0 || origin >= level.obstacleOrigins.Length)
            return;

        for (int i = 0; i < level.obstacles.Length; i++)
        {
            if ((ObstacleId)level.obstacles[i] != originId) continue;
            if (level.obstacleOrigins[i] != origin) continue;

            level.obstacles[i] = (int)ObstacleId.None;
            level.obstacleOrigins[i] = -1;

            OnCellUnlocked?.Invoke(i);
        }

        remainingHitsByOrigin[origin] = -1;
        OnObstacleDestroyed?.Invoke(origin, originId);
    }
}
