using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum ObstacleBehaviorType
{
    UnderTileLayered = 0,
    OverTileBlocker = 1,
    RevealOnBreak = 2,
    MovableObstacle = 3,
    CellAnchoredOverlay = 4   // Oil: gravity geçer, swap+match kilitler, görsel dim
}

public enum ObstacleDamageSourceRule
{
    Any = 0,
    SpecialOnly = 1,
    NormalOnly = 2,
    BoosterOnly = 3,
    Disabled = 4,        // Only Booster can hit
    FullyDisabled = 5    // Only Booster can hit — used for exhausted EnergyContainer last stage
}

[Serializable]
public class StageRule
{
    public Sprite sprite;
    public ObstacleDamageSourceRule damageRule = ObstacleDamageSourceRule.Any;
    public bool blocksCells = true;
    public ObstacleBehaviorType behavior = ObstacleBehaviorType.OverTileBlocker;
    public bool allowDiagonal = false;
    [Tooltip("Swap ve match kilitleri (Oil). CellAnchoredOverlay behavior ile kullanılır.")]
    public bool locksInteraction = false;
    [Tooltip("Bu hücredeki taşın düşmesini engeller (Oil). allowDiagonal=true ise çapraz akış yine çalışır.")]
    public bool holdsTile = false;
    [Tooltip("Açık ise bu stage'de hit particle efekti tamamen kapatılır (sprite listesi yok sayılır).")]
    public bool suppressHitParticles = false;
    [Tooltip("Bu stage'e özgü hit particle sprite'ları. Boş ise ObstacleDef düzeyindeki hitParticleSprites kullanılır.")]
    public List<Sprite> hitParticleSprites = new();
}

[Serializable]
public class ObstacleDef
{
    public ObstacleId id = ObstacleId.Stone;
    [Tooltip("Her stage için tek satır kural seti: sprite + damage rule + bloklama + davranış + diagonal izni.")]
    public List<StageRule> stages = new();
    [Tooltip("Açık ise kural reddinde alternatif context ile tekrar denemeye izin verir. Varsayılan kapalıdır.")]
    public bool allowCrossContextFallback = false;
    [Tooltip("Açık ise normal match hasarı sadece requiredNormalMatchTileType ile gelir. Special/Booster etkilenmez.")]
    public bool restrictNormalMatchTileType = false;

    [Tooltip("restrictNormalMatchTileType açıkken, obstacle'a normal match ile hasar verebilecek taş tipi.")]
    public TileType requiredNormalMatchTileType = TileType.Gear;
    public Vector2Int size = Vector2Int.one;   // örn 4x4, 1x2
    [Min(1)]
    public int hits = 1;                       // ileride: 1 vuruş, 2 vuruş
    [Header("Particle Sprites")]
    [Tooltip("Açık ise tüm stage'lerde hit particle efekti kapatılır. Stage düzeyinde ayrıca override edilebilir.")]
    public bool suppressHitParticles = false;
    [Tooltip("Obstacle hasar aldığında kullanılacak particle sprite sheet parçaları (stage override yoksa).")]
    public List<Sprite> hitParticleSprites = new();

    [Tooltip("Obstacle tamamen kırıldığında kullanılacak particle sprite sheet parçaları.")]
    public List<Sprite> breakParticleSprites = new();

    [Header("Audio")]
    [Tooltip("Obstacle hasar aldığında (kırılmadan) çalınacak ses.")]
    public AudioClip hitSound;
    [Range(0f, 1f)] public float hitSoundVolume = 1f;

    [Tooltip("Obstacle tamamen kırıldığında çalınacak ses.")]
    public AudioClip breakSound;
    [Range(0f, 1f)] public float breakSoundVolume = 1f;
    [HideInInspector] public bool drawUnderTiles = false;        // legacy serialized flag

    [SerializeField, HideInInspector, FormerlySerializedAs("sprite")]
    private Sprite legacySprite;
    [SerializeField, HideInInspector, FormerlySerializedAs("stageSprites")]
    private List<Sprite> legacyStageSprites = new();
    [SerializeField, HideInInspector, FormerlySerializedAs("stageDamageRules")]
    private List<ObstacleDamageSourceRule> legacyStageDamageRules = new();
    [SerializeField, HideInInspector, FormerlySerializedAs("blocksCells")]
    private bool legacyBlocksCells = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("behavior")]
    private ObstacleBehaviorType legacyBehavior = ObstacleBehaviorType.UnderTileLayered;


    // Backward-compatible property aliases
    public bool BlocksCells
    {
        get => GetPrimaryStage().blocksCells;
        set => GetPrimaryStage().blocksCells = value;
    }

    public bool DrawUnderTiles
    {
        get => IsUnderTileBehavior;
        set
        {
            drawUnderTiles = value;
            GetPrimaryStage().behavior = value ? ObstacleBehaviorType.UnderTileLayered : ObstacleBehaviorType.OverTileBlocker;
        }
    }

    public bool IsUnderTileBehavior => GetPrimaryStage().behavior == ObstacleBehaviorType.UnderTileLayered;
    public bool IsOverTileDamageBehavior => IsOverTileDamageBehaviorForRemainingHits(hits);

    /// <summary>
    /// Bu obstacle hareket edebilir mi? (düşme, swap)
    /// </summary>
    public bool IsMovableObstacle
    {
        get
        {
            var stage = GetStageRuleForRemainingHits(hits);
            return stage != null && stage.behavior == ObstacleBehaviorType.MovableObstacle;
        }
    }

    /// <summary>
    /// Belirtilen kalan vuruş için obstacle hareket edebilir mi?
    /// Stage geçişlerinde behavior değişebilir.
    /// </summary>
    public bool IsMovableObstacleForRemainingHits(int remainingHits)
    {
        var stage = GetStageRuleForRemainingHits(remainingHits);
        return stage != null && stage.behavior == ObstacleBehaviorType.MovableObstacle;
    }

    // Hit particle stage indexing: Stage 0 = ilk vuruş, Stage 1 = ikinci vuruş, ...
    // Visual sprite indexing'den (damageTaken) farklı: burada damageTaken-1 kullanılır.
    private StageRule GetHitParticleStageForRemainingHits(int remainingHits)
    {
        EnsureStageSlots();
        if (stages == null || stages.Count == 0) return null;
        int normalizedMaxHits = Mathf.Max(1, hits);
        int normalizedHits    = Mathf.Clamp(remainingHits, 0, normalizedMaxHits);
        int damageTaken       = normalizedMaxHits - normalizedHits;
        int idx               = Mathf.Clamp(damageTaken - 1, 0, stages.Count - 1);
        return stages[idx] ?? stages[0];
    }

    public bool IsHitParticlesSuppressedForRemainingHits(int remainingHits)
    {
        var stage = GetHitParticleStageForRemainingHits(remainingHits);
        if (stage != null && stage.suppressHitParticles)
            return true;
        return suppressHitParticles;
    }

    public List<Sprite> GetHitParticleSpritesForRemainingHits(int remainingHits)
    {
        if (IsHitParticlesSuppressedForRemainingHits(remainingHits))
            return null;
        var stage = GetHitParticleStageForRemainingHits(remainingHits);
        if (stage != null && stage.hitParticleSprites != null && stage.hitParticleSprites.Count > 0)
            return stage.hitParticleSprites;
        return hitParticleSprites;
    }

    public void MigrateLegacyFieldsIfNeeded()
    {
        hits = Mathf.Max(1, hits);

        // Legacy migration: old assets did not have `behavior` serialized and default to
        // UnderTileLayered (0). In those assets `drawUnderTiles == false` actually means
        // the intended behavior is OverTileBlocker.
        if (!drawUnderTiles && legacyBehavior == ObstacleBehaviorType.UnderTileLayered)
            legacyBehavior = ObstacleBehaviorType.OverTileBlocker;

        EnsureStageSlots();
        MigrateLegacyStageDataIfNeeded();

        // behavior is source-of-truth, keep legacy bool synchronized only for compatibility.
        drawUnderTiles = GetPrimaryStage().behavior == ObstacleBehaviorType.UnderTileLayered;

        legacyBlocksCells = GetPrimaryStage().blocksCells;
        legacyBehavior = GetPrimaryStage().behavior;
    }

    public void EnsureStageSlots()
    {
        if (stages == null)
            stages = new List<StageRule>();

        int required = Mathf.Max(1, hits);

        while (stages.Count < required)
            stages.Add(new StageRule());

        while (stages.Count > required)
            stages.RemoveAt(stages.Count - 1);
    }

    public ObstacleDamageSourceRule GetDamageRuleForRemainingHits(int remainingHits)
    {
        var rule = GetStageRuleForRemainingHits(remainingHits);
        return rule != null ? rule.damageRule : ObstacleDamageSourceRule.Any;
    }

    public Sprite GetSpriteForRemainingHits(int remainingHits)
    {
        if (ResolveStageIndex(remainingHits, hits, stages != null ? stages.Count : 0) < 0)
            return null;

        var stageRule = GetStageRuleForRemainingHits(remainingHits);
        if (stageRule != null && stageRule.sprite != null)
            return stageRule.sprite;

        var preview = GetPreviewSprite();
        return preview != null ? preview : legacySprite;
    }

    public Sprite GetPreviewSprite()
    {
        EnsureStageSlots();
        if (stages.Count > 0 && stages[0] != null && stages[0].sprite != null)
            return stages[0].sprite;
        return legacySprite;
    }

    public bool GetBlocksCellsForRemainingHits(int remainingHits)
    {
        var stage = GetStageRuleForRemainingHits(remainingHits);
        return stage != null && stage.blocksCells;
    }

    public bool IsOverTileDamageBehaviorForRemainingHits(int remainingHits)
    {
        var stage = GetStageRuleForRemainingHits(remainingHits);
        if (stage == null)
            return false;

        return stage.behavior == ObstacleBehaviorType.OverTileBlocker
               || stage.behavior == ObstacleBehaviorType.RevealOnBreak
               || stage.behavior == ObstacleBehaviorType.MovableObstacle;
    }

    public bool GetAllowDiagonalForRemainingHits(int remainingHits)
    {
        var stage = GetStageRuleForRemainingHits(remainingHits);
        return stage != null && stage.allowDiagonal;
    }

    public StageRule GetStageRuleForRemainingHits(int remainingHits)
    {
        EnsureStageSlots();
        if (stages.Count == 0)
            return null;

        int stageIndex = ResolveStageIndex(remainingHits, hits, stages.Count);
        if (stageIndex < 0)
            return null;

        return stages[stageIndex] ?? stages[0];
    }

    public static int ResolveStageIndex(int currentHits, int maxHits, int stageCount)
    {
        if (stageCount <= 0)
            return -1;

        int normalizedMaxHits = Mathf.Max(1, maxHits);
        int normalizedHits = Mathf.Clamp(currentHits, 0, normalizedMaxHits);

        // Obstacle kırıldıktan sonra stage yoktur.
        if (normalizedHits <= 0)
            return -1;

        int damageTaken = normalizedMaxHits - normalizedHits;
        return Mathf.Clamp(damageTaken, 0, stageCount - 1);
    }

    private StageRule GetPrimaryStage()
    {
        EnsureStageSlots();
        if (stages.Count == 0)
            stages.Add(new StageRule());
        if (stages[0] == null)
            stages[0] = new StageRule();
        return stages[0];
    }

    private void MigrateLegacyStageDataIfNeeded()
    {
        var primary = GetPrimaryStage();
        bool hasLegacyStageSprites = legacyStageSprites != null && legacyStageSprites.Count > 0;
        bool hasLegacyStageDamageRules = legacyStageDamageRules != null && legacyStageDamageRules.Count > 0;
        bool hasAnyLegacyData = legacySprite != null || hasLegacyStageSprites || hasLegacyStageDamageRules;

        if (primary.sprite == null && legacySprite != null)
            primary.sprite = legacySprite;

        bool stagesLookUninitialized = true;
        for (int i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            if (stage == null)
                continue;

            bool isDefaultStage = stage.sprite == null
                                  && stage.damageRule == ObstacleDamageSourceRule.Any
                                  && stage.blocksCells
                                  && stage.behavior == ObstacleBehaviorType.OverTileBlocker
                                  && !stage.allowDiagonal;

            if (!isDefaultStage)
            {
                stagesLookUninitialized = false;
                break;
            }
        }

        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i] == null)
                stages[i] = new StageRule();

            if (hasLegacyStageSprites && i < legacyStageSprites.Count && stages[i].sprite == null)
                stages[i].sprite = legacyStageSprites[i];

            if (hasLegacyStageDamageRules && i < legacyStageDamageRules.Count)
                stages[i].damageRule = legacyStageDamageRules[i];

            if (hasAnyLegacyData && stagesLookUninitialized)
            {
                stages[i].blocksCells = legacyBlocksCells;
                stages[i].behavior = legacyBehavior;
            }
        }

        legacySprite = primary.sprite;
        legacyStageSprites = null;
        legacyStageDamageRules = null;
    }
}

[CreateAssetMenu(fileName = "ObstacleLibrary", menuName = "CoreCollapse/Obstacle Library", order = 2)]
public class ObstacleLibrary : ScriptableObject
{
    public List<ObstacleDef> obstacles = new();

    private Dictionary<ObstacleId, ObstacleDef> _map;

    public ObstacleDef Get(ObstacleId id)
    {
        if (_map == null) BuildMap();
        _map.TryGetValue(id, out var def);
        return def;
    }

    private void BuildMap()
    {
        _map = new Dictionary<ObstacleId, ObstacleDef>();
        foreach (var o in obstacles)
        {
            if (o == null) continue;

            if (_map.ContainsKey(o.id))
                continue;

            _map[o.id] = o;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (obstacles != null)
        {
            foreach (var obstacle in obstacles)
                obstacle?.MigrateLegacyFieldsIfNeeded();

            var seen = new HashSet<ObstacleId>();
            for (int i = 0; i < obstacles.Count; i++)
            {
                var obstacle = obstacles[i];
                if (obstacle == null) continue;

                if (!seen.Add(obstacle.id))
                {
                    Debug.LogWarning($"ObstacleLibrary '{name}': Duplicate ObstacleId '{obstacle.id}' at index {i}. First entry will be used.", this);
                }
            }
        }

        _map = null;
    }
#endif
}
