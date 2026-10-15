using System;
using TMPro;
using UnityEngine;

/// <summary>Gear Cup artwork and rules. Historical class name retained for existing scene references.</summary>
[CreateAssetMenu(menuName = "TinyFixers/Events/Gear Cup Preview", fileName = "GearCupPreviewConfig")]
public sealed class GearCupPreviewConfig : ScriptableObject
{
    [Header("Artwork")]
    public Sprite background;
    public Sprite platform;
    public Sprite platformRing;
    public Sprite gear;
    public Sprite eventBadge;
    public Sprite rankShield;
    public Sprite rewardHeader;
    public Sprite rewardCardGold;
    public Material rewardCardSilverMaterial;
    public Material rewardCardBronzeMaterial;
    public Material teamRowWhiteMaterial;
    public Sprite rowBackground;
    public Sprite darkPanel;
    public Sprite infoIcon;
    public Sprite closeIcon;
    public Sprite inspectIcon;
    public Sprite buttonBackground;
    public Sprite closeBackground;
    public Sprite joinPopupBackground;
    public Material joinPopupMaterial;
    public Sprite[] teamEmblems;
    public TMP_FontAsset headingFont;
    public TMP_FontAsset bodyFont;

    [Header("Rabbit: idle, half blink, closed, prepare, reach, push, stretch")]
    public Sprite[] rabbitFrames = new Sprite[7];
    [Tooltip("Source-space foot-bottom Y, measured from the top of the 1536px canvas.")]
    public float[] rabbitFootY = { 1508f, 1508f, 1508f, 1509f, 1509f, 1508f, 1479f };
    [Min(3f)] public float animationCycleSeconds = 4.333333f;

    [Header("Pig: idle, prepare, fists at chest, celebrate (no blink)")]
    public Sprite[] pigFrames = new Sprite[4];
    [Tooltip("Source-space foot-bottom Y, measured from the top of the 1536px canvas.")]
    public float[] pigFootY = { 1497f, 1492f, 1486f, 1494f };

    [Header("Live event rules")]
    public bool eventEnabled = true;
    [Min(1)] public int minLevelGate = 51;
    [Min(1)] public int seasonHours = 72;
    [Range(0, 60)] public int settlementMinutes = 10;
    public bool onlyYellowTiles;
    [Min(1)] public int gearsPerTile = 1;
    [Min(1)] public int maxGearsPerLevel = 10000;
    public Material gainLabelMaterial;

    private static GearCupPreviewConfig shared;
    public static GearCupPreviewConfig Shared => shared != null ? shared
        : (shared = Resources.Load<GearCupPreviewConfig>("Events/GearCupConfig"));

    [Header("Reward packages and fallback team artwork")]
    public RewardSample[] rewards = new RewardSample[3];

    [Serializable]
    public sealed class RewardSample
    {
        [Min(0)] public int coins;
        public DailySlotRewardType item = DailySlotRewardType.Joker_PulseCore;
        [Min(0)] public int itemAmount = 1;
    }
}
