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
    public readonly ChestColorMask removedColor;

    public ObstacleVisualChange(int originIndex, ObstacleId obstacleId, bool cleared, int remainingHits, Sprite sprite, ChestColorMask removedColor = ChestColorMask.None)
    {
        this.originIndex = originIndex;
        this.obstacleId = obstacleId;
        this.cleared = cleared;
        this.remainingHits = remainingHits;
        this.sprite = sprite;
        this.removedColor = removedColor;
    }
}

public readonly struct ObstacleStageSnapshot
{
    public readonly ObstacleBehaviorType behavior;
    public readonly bool blocksCells;
    public readonly bool allowDiagonal;
    public readonly ObstacleDamageSourceRule damageRule;
    public readonly Sprite sprite;

    public ObstacleStageSnapshot(
        ObstacleBehaviorType behavior,
        bool blocksCells,
        bool allowDiagonal,
        ObstacleDamageSourceRule damageRule,
        Sprite sprite)
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

    public ObstacleStageTransition(
        bool hasTransition,
        int originIndex,
        ObstacleId obstacleId,
        int remainingHitsAfter,
        bool cleared,
        ObstacleStageSnapshot previousStage,
        ObstacleStageSnapshot currentStage)
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

public class ObstacleStateService : ISimObstacleQuery
{
    private LevelData level;
    private ObstacleLibrary library;

    // Sadece origin hücre indexi anlamlıdır. -1 => obstacle yok.
    private int[] remainingHitsByOrigin = Array.Empty<int>();

    // ColorChest: origin index → kalan renk maskesi
    private readonly Dictionary<int, ChestColorMask> _chestColorStates = new();

    // BatteryBox: origin index → int[4] kalan hit (Gear/Core/Bolt/Plate sırasıyla)
    private readonly Dictionary<int, int[]> _batteryHitsByOrigin = new();

    // Wardrobe: origin index → kalan item sayısı (kapı açıldıktan sonra)
    private readonly Dictionary<int, int> _wardrobeItemCounts = new();

    public event Action<int, ObstacleStageSnapshot> OnObstacleStageChanged;
    public event Action<int, ObstacleId> OnObstacleDestroyed;
    public event Action<int> OnCellUnlocked;

    /// Set by TubeObstacleService. Called with the tube's ORIGIN cell index when any tube cell is hit.
    public Action<int> TubeHitInterceptor;
    /// Set by EnergyContainerService. Called with the ORIGIN cell index when an active container is hit.
    /// The container state is NOT decremented; exhaustion is handled globally by EnergyContainerService.
    public Action<int> EnergyContainerHitInterceptor;
    // ColorChest açılış ve renk kırılması bildirimleri
    public event Action<int> OnChestOpened;
    public event Action<int, ChestColorMask> OnChestColorRemoved;
    // BatteryBox: origin, hangi renk pilin kaç hiti kaldı
    public event Action<int, ChestColorMask, int> OnBatteryHit;
    // Wardrobe: kapı açılması ve item kırılması
    public event Action<int> OnWardrobeOpened;
    public event Action<int, int> OnWardrobeItemRemoved;  // (origin, itemsRemaining)

    public readonly struct ObstacleHitResult
    {
        public readonly bool didHit;
        public readonly bool consumedHit;
        public readonly bool rejectedByRule;
        public readonly ObstacleVisualChange visualChange;
        public readonly ObstacleStageTransition stageTransition;
        public readonly int[] affectedCellIndices;

        public ObstacleHitResult(
            bool didHit,
            bool consumedHit,
            bool rejectedByRule,
            ObstacleVisualChange visualChange,
            ObstacleStageTransition stageTransition,
            int[] affectedCellIndices)
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
        for (int i = 0; i < size; i++)
            remainingHitsByOrigin[i] = -1;

        InitializeFromLevel();
    }

    public void InitializeFromLevel()
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null)
            return;

        int size = Mathf.Min(
            remainingHitsByOrigin.Length,
            Mathf.Min(level.obstacles.Length, level.obstacleOrigins.Length));

        for (int i = 0; i < size; i++)
            remainingHitsByOrigin[i] = -1;

        _chestColorStates.Clear();
        _batteryHitsByOrigin.Clear();
        _wardrobeItemCounts.Clear();

        for (int idx = 0; idx < size; idx++)
        {
            var id = (ObstacleId)level.obstacles[idx];
            if (id == ObstacleId.None) continue;

            int origin = level.obstacleOrigins[idx];
            if (origin != idx) continue;
            if (origin < 0 || origin >= size) continue;

            var def = library != null ? library.Get(id) : null;
            int hits;
            if (id == ObstacleId.EnergyContainer)
            {
                // hits = energyPerContainer (active) + 1 (exhausted/FullyDisabled stage).
                int perContainer = level != null ? Mathf.Max(1, level.energyPerContainer) : 1;
                hits = perContainer + 1;
            }
            else if (id == ObstacleId.BatteryBox)
            {
                // def.hits = hit sayısı (per battery). 4 renk × def.hits = toplam.
                int hitsPerBattery = Mathf.Max(1, def != null ? def.hits : 1);
                hits = hitsPerBattery * 4;
                _batteryHitsByOrigin[origin] = new int[] { hitsPerBattery, hitsPerBattery, hitsPerBattery, hitsPerBattery };
            }
            else if (id == ObstacleId.Wardrobe)
            {
                // Wardrobe her zaman 2 hit: stage 0 = kapalı, stage 1 = açık.
                // İçindeki item'lar ayrı dictionary'de tutulur; tümü bitince obstacle yıkılır.
                hits = 2;
                int itemCount = def != null ? def.wardrobeItemSprites.Count : 0;
                _wardrobeItemCounts[origin] = Mathf.Max(0, itemCount);
            }
            else
            {
                hits = Mathf.Max(1, def != null ? def.hits : 1);
            }
            remainingHitsByOrigin[origin] = hits;

            if (id == ObstacleId.ColorChest)
                _chestColorStates[origin] = ChestColorMask.All;
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
        return TryDamageAt(x, y, context, null);
    }

    public ObstacleHitResult TryDamageAt(ObstacleDamageRequest request)
    {
        return TryDamageAt(
            request.cell.x,
            request.cell.y,
            request.context,
            request.normalMatchTileType);
    }

    public ObstacleHitResult TryDamageAt(int x, int y, ObstacleHitContext context, TileType? sourceTileType)
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

        // Tube cells are handled entirely by TubeObstacleService.
        // originIndex=-1 so BoardBreakFxService skips the particle FX (TubeView owns its visuals).
        if (id == ObstacleId.Tube)
        {
            TubeHitInterceptor?.Invoke(origin);
            var tubeChange = new ObstacleVisualChange(-1, id, false, 0, null);
            return new ObstacleHitResult(true, true, false, tubeChange, default, Array.Empty<int>());
        }

        // EnergyContainer: hits are fully managed by EnergyContainerService via the interceptor.
        // Active (interceptor set)   → fire interceptor, no decrement, container stays on board.
        // Exhausted (interceptor null) → block all hits; container is permanent and invincible.
        if (id == ObstacleId.EnergyContainer)
        {
            if (EnergyContainerHitInterceptor != null)
            {
                EnergyContainerHitInterceptor.Invoke(origin);
                var ecChange = new ObstacleVisualChange(origin, id, false, remainingHitsByOrigin[origin], null);
                return new ObstacleHitResult(true, true, false, ecChange, default, Array.Empty<int>());
            }
            // Interceptor null = exhausted → invincible, block hit without clearing
            return new ObstacleHitResult(false, false, true, default, default, Array.Empty<int>());
        }

        var def = library != null ? library.Get(id) : null;

        int remaining = remainingHitsByOrigin[origin];
        if (remaining < 0)
            remaining = Mathf.Max(1, def != null ? def.hits : 1);

        if (!CanConsumeHit(def, remaining, context, sourceTileType))
            return new ObstacleHitResult(false, false, true, default, default, Array.Empty<int>());

        // ColorChest renk validasyonu
        ChestColorMask removedColor = ChestColorMask.None;
        if (id == ObstacleId.ColorChest)
        {
            bool isOpen = def != null && remaining < def.hits;

            if (context == ObstacleHitContext.NormalMatch)
            {
                if (!isOpen)
                    return new ObstacleHitResult(false, false, true, default, default, Array.Empty<int>());

                // Açık dolap: sadece içinde kalan renk hasar verir
                var colorFlag = sourceTileType.ToChestColor();
                if (colorFlag == ChestColorMask.None ||
                    !_chestColorStates.TryGetValue(origin, out var chestMask) ||
                    (chestMask & colorFlag) == 0)
                    return new ObstacleHitResult(false, false, true, default, default, Array.Empty<int>());
                removedColor = colorFlag;
            }
            else if (context == ObstacleHitContext.SpecialActivation && isOpen)
            {
                // Açık dolap + special: kalan renklerden rastgele biri kırılır
                if (!_chestColorStates.TryGetValue(origin, out var chestMask) || chestMask == ChestColorMask.None)
                    return new ObstacleHitResult(false, false, true, default, default, Array.Empty<int>());
                removedColor = PickRandomChestColor(chestMask);
            }
            // isOpen=false + SpecialActivation: kapalı dolabı açma işlemi, removedColor = None
        }

        // BatteryBox renk-pil validasyonu
        ChestColorMask batteryHitColor = ChestColorMask.None;
        int batteryColorIndex = -1;
        if (id == ObstacleId.BatteryBox)
        {
            if (!_batteryHitsByOrigin.TryGetValue(origin, out var batteryHits))
                return new ObstacleHitResult(false, false, true, default, default, Array.Empty<int>());

            if (context == ObstacleHitContext.NormalMatch)
            {
                var colorFlag = sourceTileType.ToChestColor();
                batteryColorIndex = BatteryColorToIndex(colorFlag);
                if (batteryColorIndex < 0 || batteryHits[batteryColorIndex] <= 0)
                    return new ObstacleHitResult(false, false, true, default, default, Array.Empty<int>());
                batteryHitColor = colorFlag;
            }
            else
            {
                // Special / Booster: kalan hit'i olan rastgele bir pili vur
                batteryHitColor = PickRandomBatteryColor(batteryHits);
                batteryColorIndex = BatteryColorToIndex(batteryHitColor);
                if (batteryColorIndex < 0)
                    return new ObstacleHitResult(false, false, true, default, default, Array.Empty<int>());
            }
        }

        int[] affectedCells = CollectCellsForOrigin(origin, id);
        var previousStage = CreateSnapshot(def, id, remaining);
        bool wasChestClosed = id == ObstacleId.ColorChest
                              && context == ObstacleHitContext.SpecialActivation
                              && (def == null || remaining == def.hits);

        // Wardrobe açık durum (remaining==1): her vuruş bir item kırar; remaining değişmez.
        // Tüm itemler bittiğinde remaining-- çalışır → remaining=0 → obstacle yıkılır.
        if (id == ObstacleId.Wardrobe && remaining == 1)
        {
            if (_wardrobeItemCounts.TryGetValue(origin, out int itemsLeft) && itemsLeft > 0)
            {
                int newCount = itemsLeft - 1;
                _wardrobeItemCounts[origin] = newCount;
                OnWardrobeItemRemoved?.Invoke(origin, newCount);

                if (newCount > 0)
                {
                    // Hâlâ item var: remaining sabit, erken dön
                    change = new ObstacleVisualChange(origin, id, false, remaining, null);
                    return new ObstacleHitResult(true, true, false, change, default, affectedCells);
                }
                // newCount == 0: düş → remaining-- → obstacle yıkılır
            }
        }

        remaining--;
        remainingHitsByOrigin[origin] = remaining;

        // Renk maskesini güncelle ve bildirimi gönder
        if (removedColor != ChestColorMask.None)
        {
            if (_chestColorStates.TryGetValue(origin, out var currentMask))
                _chestColorStates[origin] = currentMask & ~removedColor;
            OnChestColorRemoved?.Invoke(origin, removedColor);
        }

        // BatteryBox pil hit'ini güncelle ve bildir
        if (batteryHitColor != ChestColorMask.None && batteryColorIndex >= 0
            && _batteryHitsByOrigin.TryGetValue(origin, out var hits))
        {
            hits[batteryColorIndex]--;
            OnBatteryHit?.Invoke(origin, batteryHitColor, hits[batteryColorIndex]);
        }

        if (remaining <= 0)
        {
            ClearObstacleFromLevel(origin, id);
            change = new ObstacleVisualChange(origin, id, true, 0, null);
            var transition = new ObstacleStageTransition(true, origin, id, 0, true, previousStage, default);
            return new ObstacleHitResult(true, true, false, change, transition, affectedCells);
        }

        if (wasChestClosed)
            OnChestOpened?.Invoke(origin);

        // Wardrobe: kapı açıldı (remaining 2→1)
        if (id == ObstacleId.Wardrobe && remaining == 1)
            OnWardrobeOpened?.Invoke(origin);

        var currentStage = CreateSnapshot(def, id, remaining);
        var sprite = ResolveStageSprite(def, id, remaining);
        change = new ObstacleVisualChange(origin, id, false, remaining, sprite, removedColor);

        var stageTransition = new ObstacleStageTransition(
            true,
            origin,
            id,
            remaining,
            false,
            previousStage,
            currentStage);

        OnObstacleStageChanged?.Invoke(origin, currentStage);
        return new ObstacleHitResult(true, true, false, change, stageTransition, affectedCells);
    }

    public bool HasObstacleAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;
        int idx = level.Index(x, y);
        return (ObstacleId)level.obstacles[idx] != ObstacleId.None;
    }

    public int GetRemainingHitsAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return 0;

        int idx = level.Index(x, y);
        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None) return 0;

        int origin = level.obstacleOrigins[idx];
        if (origin < 0 || origin >= remainingHitsByOrigin.Length) return 0;

        int remaining = remainingHitsByOrigin[origin];
        if (remaining >= 0)
            return remaining;

        var def = library != null ? library.Get(id) : null;
        return Mathf.Max(1, def != null ? def.hits : 1);
    }

    public ObstacleId GetObstacleIdAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return ObstacleId.None;
        int idx = level.Index(x, y);
        return (ObstacleId)level.obstacles[idx];
    }

    public int GetObstacleOriginAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return -1;

        int idx = level.Index(x, y);
        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None) return -1;

        int origin = level.obstacleOrigins[idx];
        return (origin >= 0 && origin < remainingHitsByOrigin.Length) ? origin : -1;
    }

    public bool IsCellBlocked(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;

        int idx = level.Index(x, y);
        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None) return false;
        if (id == ObstacleId.Tube) return true;

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
        if (id == ObstacleId.Tube) return true;
        // EnergyContainer always blocks regardless of stage — interceptor handles active vs exhausted.
        if (id == ObstacleId.EnergyContainer) return true;

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

    // CascadeLogic ile uyum için alias
    public bool GetAllowDiagonalAt(int x, int y)
    {
        return IsDiagonalAllowedAt(x, y);
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

    public bool IsInteractionLockedAt(int x, int y)
    {
        if (!IsValidCell(x, y))
            return false;

        // Yalnızca sabit OverTileBlocker (Stone vb.) altındaki tile'ı kilitler.
        // MovableObstacle (Plastic vb.) swap edilebilir — interaction kilitlenmez.
        if (IsOverTileBlockerAt(x, y) && !IsMovableObstacleAt(x, y))
            return true;

        int idx = level.Index(x, y);
        var obsId = (ObstacleId)level.obstacles[idx];
        if (obsId == ObstacleId.None)
            return false;

        var def = library?.Get(obsId);
        if (def == null)
            return false;

        int remaining = ResolveRemainingHitsForCell(idx, def);
        var stage = def.GetStageRuleForRemainingHits(remaining);

        return stage != null && stage.locksInteraction;
    }
    private bool CanConsumeHit(
        ObstacleDef def,
        int remainingHits,
        ObstacleHitContext context,
        TileType? sourceTileType)
    {
        if (def == null)
            return true;

        ObstacleDamageSourceRule rule = def.GetDamageRuleForRemainingHits(remainingHits);

        if (!DoesContextMatchRule(context, rule))
            return false;

        if (context == ObstacleHitContext.NormalMatch && def.restrictNormalMatchTileType)
        {
            return sourceTileType.HasValue &&
                   sourceTileType.Value == def.requiredNormalMatchTileType;
        }

        return true;
    }

    private bool DoesContextMatchRule(ObstacleHitContext context, ObstacleDamageSourceRule rule)
    {
        switch (rule)
        {
            case ObstacleDamageSourceRule.FullyDisabled:
            case ObstacleDamageSourceRule.Disabled:
                // Boosters (jokers) are direct tools that must always deal a hit to every
                // obstacle type, including exhausted/fully-disabled stages.
                return context == ObstacleHitContext.Booster;

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

        return new ObstacleStageSnapshot(
            stage.behavior,
            stage.blocksCells,
            stage.allowDiagonal,
            stage.damageRule,
            stage.sprite);
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

        if (originId == ObstacleId.ColorChest)
            _chestColorStates.Remove(origin);

        if (originId == ObstacleId.BatteryBox)
            _batteryHitsByOrigin.Remove(origin);

        if (originId == ObstacleId.Wardrobe)
            _wardrobeItemCounts.Remove(origin);

        OnObstacleDestroyed?.Invoke(origin, originId);
    }

    public bool IsMovableObstacleAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;

        int idx = level.Index(x, y);
        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None) return false;

        var def = library != null ? library.Get(id) : null;
        if (def == null) return false;

        int remaining = ResolveRemainingHitsForCell(idx, def);
        return def.IsMovableObstacleForRemainingHits(remaining);
    }

    public void MoveObstacle(int fromX, int fromY, int toX, int toY)
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null)
            return;

        if (!level.InBounds(fromX, fromY) || !level.InBounds(toX, toY))
            return;

        int fromIdx = level.Index(fromX, fromY);
        int toIdx = level.Index(toX, toY);

        if (fromIdx < 0 || fromIdx >= level.obstacles.Length) return;
        if (toIdx < 0 || toIdx >= level.obstacles.Length) return;

        var id = (ObstacleId)level.obstacles[fromIdx];
        if (id == ObstacleId.None) return;

        int origin = level.obstacleOrigins[fromIdx];

        level.obstacles[toIdx] = level.obstacles[fromIdx];
        level.obstacleOrigins[toIdx] = toIdx; // yeni pozisyon yeni origin

        level.obstacles[fromIdx] = (int)ObstacleId.None;
        level.obstacleOrigins[fromIdx] = -1;

        if (origin >= 0 && origin < remainingHitsByOrigin.Length)
        {
            int remaining = remainingHitsByOrigin[origin];
            remainingHitsByOrigin[origin] = -1;

            if (toIdx < remainingHitsByOrigin.Length)
                remainingHitsByOrigin[toIdx] = remaining;
        }
    }

    public int CountAliveOrigins(ObstacleId obstacleId)
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null)
            return 0;

        int count = 0;
        int size = Mathf.Min(level.obstacles.Length, level.obstacleOrigins.Length);

        for (int i = 0; i < size; i++)
        {
            if ((ObstacleId)level.obstacles[i] != obstacleId) continue;
            if (level.obstacleOrigins[i] != i) continue;
            count++;
        }

        return count;
    }

    public bool TrySpawnSingleCellObstacleAt(int x, int y, ObstacleId obstacleId)
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null)
            return false;
        if (!level.InBounds(x, y))
            return false;

        int idx = level.Index(x, y);
        if ((ObstacleId)level.obstacles[idx] != ObstacleId.None)
            return false;

        var def = library != null ? library.Get(obstacleId) : null;
        if (def == null)
            return false;

        if (def.size.x != 1 || def.size.y != 1)
            return false;

        int initialHits = Mathf.Max(1, def.hits);

        level.obstacles[idx] = (int)obstacleId;
        level.obstacleOrigins[idx] = idx;

        if (idx >= 0 && idx < remainingHitsByOrigin.Length)
            remainingHitsByOrigin[idx] = initialHits;

        return true;
    }

    // ── Oil helpers ──────────────────────────────────────────────────────────

    public ChestColorMask GetChestColorsAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return ChestColorMask.None;
        int idx = level.Index(x, y);
        if ((ObstacleId)level.obstacles[idx] != ObstacleId.ColorChest) return ChestColorMask.None;
        int origin = level.obstacleOrigins[idx];
        if (origin < 0 || !_chestColorStates.TryGetValue(origin, out var mask)) return ChestColorMask.None;
        return mask;
    }

    private static ChestColorMask PickRandomChestColor(ChestColorMask mask)
    {
        ChestColorMask[] all = { ChestColorMask.Gear, ChestColorMask.Core, ChestColorMask.Bolt, ChestColorMask.Plate };
        int start = UnityEngine.Random.Range(0, all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            var candidate = all[(start + i) % all.Length];
            if ((mask & candidate) != 0)
                return candidate;
        }
        return ChestColorMask.None;
    }

    // int[4] → [Gear, Core, Bolt, Plate] index mapping
    private static int BatteryColorToIndex(ChestColorMask color) => color switch
    {
        ChestColorMask.Gear  => 0,
        ChestColorMask.Core  => 1,
        ChestColorMask.Bolt  => 2,
        ChestColorMask.Plate => 3,
        _                    => -1
    };

    private static ChestColorMask PickRandomBatteryColor(int[] batteryHits)
    {
        ChestColorMask[] all = { ChestColorMask.Gear, ChestColorMask.Core, ChestColorMask.Bolt, ChestColorMask.Plate };
        int start = UnityEngine.Random.Range(0, all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            int idx = (start + i) % all.Length;
            if (batteryHits[idx] > 0)
                return all[idx];
        }
        return ChestColorMask.None;
    }

    public bool IsFullyDisabledAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;
        int idx = level.Index(x, y);
        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None) return false;
        var def = library?.Get(id);
        if (def == null) return false;
        int remaining = ResolveRemainingHitsForCell(idx, def);
        return def.GetDamageRuleForRemainingHits(remaining) == ObstacleDamageSourceRule.FullyDisabled;
    }

    /// <summary>
    /// Kaç kez daha anlamlı hit alabilir? FullyDisabled stage'ler sayılmaz.
    /// PatchBot koordinatörü bu değeri kullanarak birden fazla botu boşa yollamaz.
    /// </summary>
    public int GetActiveMeaningfulHitsAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return 0;

        int idx = level.Index(x, y);
        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None) return 0;

        if (id == ObstacleId.Tube) return 1;

        var def = library?.Get(id);
        if (def == null) return 0;

        int current = ResolveRemainingHitsForCell(idx, def);
        int count = 0;

        for (int r = current; r > 0; r--)
        {
            var stage = def.GetStageRuleForRemainingHits(r);
            if (stage == null || stage.damageRule == ObstacleDamageSourceRule.FullyDisabled)
                break;
            count++;
        }

        return count;
    }

    public bool IsUnderTileObstacleAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;
        int idx = level.Index(x, y);
        var id = (ObstacleId)level.obstacles[idx];
        if (id == ObstacleId.None) return false;
        var def = library?.Get(id);
        if (def == null) return false;
        int remaining = ResolveRemainingHitsForCell(idx, def);
        var stage = def.GetStageRuleForRemainingHits(remaining);
        return stage != null && stage.behavior == ObstacleBehaviorType.UnderTileLayered;
    }

    /// Fires OnObstacleDestroyed for a fully-destroyed tube. Called by TubeObstacleService
    /// when the last cell of a tube is freed. Triggers goal counters etc.
    public void NotifyTubeFullyDestroyed(int originIndex)
    {
        OnObstacleDestroyed?.Invoke(originIndex, ObstacleId.Tube);
    }

    /// Removes all EnergyContainer cells from the obstacle layer and fires OnCellUnlocked
    /// for each, triggering gravity. Called only when a booster directly destroys an
    /// exhausted container (remaining goes to 0 via normal TryDamageAt flow).
    public void ForceRemoveAllEnergyContainers()
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null)
            return;

        int size = Mathf.Min(level.obstacles.Length, level.obstacleOrigins.Length);
        var origins = new List<int>();

        for (int i = 0; i < size; i++)
        {
            if ((ObstacleId)level.obstacles[i] != ObstacleId.EnergyContainer) continue;
            if (level.obstacleOrigins[i] != i) continue;
            origins.Add(i);
        }

        foreach (int origin in origins)
            ClearObstacleFromLevel(origin, ObstacleId.EnergyContainer);
    }

    /// Sets all alive EnergyContainers to remaining=1 (FullyDisabled stage) WITHOUT
    /// removing them from the obstacle layer. The cell stays blocked; only a booster
    /// can clear the container. Called by EnergyContainerService when the group energy
    /// quota is depleted.
    public void SetAllEnergyContainersExhausted()
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null)
            return;

        int size = Mathf.Min(level.obstacles.Length, level.obstacleOrigins.Length);
        for (int i = 0; i < size; i++)
        {
            if ((ObstacleId)level.obstacles[i] != ObstacleId.EnergyContainer) continue;
            if (level.obstacleOrigins[i] != i) continue;
            if (remainingHitsByOrigin[i] > 1)
                remainingHitsByOrigin[i] = 1; // FullyDisabled stage — cell stays blocked
        }
    }

    /// Frees a single tube cell from the obstacle layer and fires OnCellUnlocked.
    /// Called by TubeObstacleService when the tube shrinks.
    public void FreeTubeCell(int cellIndex)
    {
        if (level == null || level.obstacles == null || level.obstacleOrigins == null) return;
        if (cellIndex < 0 || cellIndex >= level.obstacles.Length) return;

        int origin = level.obstacleOrigins[cellIndex];
        level.obstacles[cellIndex]      = (int)ObstacleId.None;
        level.obstacleOrigins[cellIndex] = -1;

        if (origin >= 0 && origin < remainingHitsByOrigin.Length)
            remainingHitsByOrigin[origin] = -1;

        OnCellUnlocked?.Invoke(cellIndex);
    }

    public bool IsOilAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;
        int idx = level.Index(x, y);
        return (ObstacleId)level.obstacles[idx] == ObstacleId.Oil;
    }

    // Mud sadece görsel overlay; swap/interaction'ı bloklamaz.
    // BoardController IsUnderTileObstacleAt check'inde Mud'u istisna yapmak için kullanılır.
    public bool IsMudAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;
        int idx = level.Index(x, y);
        return (ObstacleId)level.obstacles[idx] == ObstacleId.Mud;
    }

    // Oil (veya herhangi bir holdsTile=true obstacle) bu hücredeki taşı tutuyorsa true.
    // allowDiagonal=true ise çapraz akış yine izinli — bu method sadece dikey blok için.
    public bool HoldsTileAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;
        int idx = level.Index(x, y);
        var obsId = (ObstacleId)level.obstacles[idx];
        if (obsId == ObstacleId.None) return false;
        var def = library?.Get(obsId);
        if (def == null) return false;
        var stage = def.GetStageRuleForRemainingHits(remainingHitsByOrigin[idx]);
        return stage != null && stage.holdsTile;
    }

    // HoldsTile + allowDiagonal => çapraz akış hâlâ izinli mi?
    public bool AllowsDiagonalAt(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;
        int idx = level.Index(x, y);
        var obsId = (ObstacleId)level.obstacles[idx];
        if (obsId == ObstacleId.None) return false;
        var def = library?.Get(obsId);
        if (def == null) return false;
        var stage = def.GetStageRuleForRemainingHits(remainingHitsByOrigin[idx]);
        return stage != null && stage.allowDiagonal;
    }


    public bool TryAddOilAt(int x, int y)
    {
        bool added = TrySpawnSingleCellObstacleAt(x, y, ObstacleId.Oil);
        if (added)
            Debug.Log($"[Oil] Added at ({x},{y}). Total oil: {CountAliveOrigins(ObstacleId.Oil)}");
        return added;
    }

    public bool TryRemoveOilAt(int x, int y)
    {
        if (!IsOilAt(x, y)) return false;
        int idx = level.Index(x, y);
        ClearObstacleFromLevel(idx, ObstacleId.Oil);
        Debug.Log($"[Oil] Removed at ({x},{y}). Total oil: {CountAliveOrigins(ObstacleId.Oil)}");
        return true;
    }

    public List<Vector2Int> GetAllOilCells()
    {
        var result = new List<Vector2Int>();
        if (level == null || level.obstacles == null) return result;

        for (int i = 0; i < level.obstacles.Length; i++)
        {
            if ((ObstacleId)level.obstacles[i] != ObstacleId.Oil) continue;
            int x = i % level.width;
            int y = i / level.width;
            result.Add(new Vector2Int(x, y));
        }
        return result;
    }

    public bool CanOilSpreadTo(int x, int y)
    {
        if (!IsValidCell(x, y)) return false;
        int idx = level.Index(x, y);
        return (ObstacleId)level.obstacles[idx] == ObstacleId.None;
    }
}
