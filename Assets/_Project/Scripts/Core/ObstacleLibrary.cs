using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum ObstacleBehaviorType
{
    UnderTileLayered = 0,
    OverTileBlocker = 1,
    RevealOnBreak = 2
}

public enum ObstacleDamageSourceRule
{
    Any = 0,
    SpecialOnly = 1,
    NormalOnly = 2,
    BoosterOnly = 3
}

[Serializable]
public class StageRule
{
    public Sprite sprite;
    public ObstacleDamageSourceRule damageRule = ObstacleDamageSourceRule.Any;
    public bool blocksCells = true;
    public ObstacleBehaviorType behavior = ObstacleBehaviorType.OverTileBlocker;
    public bool allowDiagonal = false;
}

[Serializable]
public class ObstacleDef
{
    public ObstacleId id = ObstacleId.Stone;
    [Tooltip("Her stage için tek satır kural seti: sprite + damage rule + bloklama + davranış + diagonal izni.")]
    public List<StageRule> stages = new();
    [Tooltip("Açık ise kural reddinde alternatif context ile tekrar denemeye izin verir. Varsayılan kapalıdır.")]
    public bool allowCrossContextFallback = false;
    public Vector2Int size = Vector2Int.one;   // örn 4x4, 1x2
    [Min(1)]
    public int hits = 1;                       // ileride: 1 vuruş, 2 vuruş
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
               || stage.behavior == ObstacleBehaviorType.RevealOnBreak;
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
