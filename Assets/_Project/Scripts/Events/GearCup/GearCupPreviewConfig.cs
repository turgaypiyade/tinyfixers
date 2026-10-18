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

    [Header("Screen frame: team list panel + tilted title plate (1080x1920 design units)")]
    [Tooltip("Gold-rimmed navy 9-slice; used for both the team list frame and the title plate.")]
    public Sprite listPanel;
    [Tooltip("Image.pixelsPerUnitMultiplier — higher = thinner gold rim.")]
    [Min(0.1f)] public float listPanelBorderScale = 4.4f;
    [Tooltip("Vertical band the team list is centred in.")]
    public float listPanelTop = -999f;
    public float listPanelBottom = -1736f;
    [Tooltip("Gap between the rows and the panel's left/right edges (panel hugs the rows).")]
    [Min(0f)] public float listPanelPadding = 24f;
    [Tooltip("Gap above the first and below the last row; larger than the sides because the top/bottom rim is thicker.")]
    [Min(0f)] public float listPanelPaddingY = 40f;
    [Tooltip("Team rows are re-spaced evenly around the panel centre.")]
    [Min(0f)] public float teamRowStep = 136f;
    [Tooltip("Uniform row scale (width + height + contents) so rows and the own-team outline sit inside the gold rim.")]
    [Range(0.8f, 1f)] public float teamRowScale = 0.96f;
    [Min(0.1f)] public float titlePlateBorderScale = 5.5f;
    public Vector2 titlePlateSize = new Vector2(580f, 104f);
    public Vector2 titleTextSize = new Vector2(500f, 76f);
    public float titleY = -85f;
    public float timerY = -170f;
    [Range(-15f, 15f)] public float titleTilt = 5f;
    [Tooltip("Tall screens: header (title, timer, info, close) rises into the spare safe-area space above the design.")]
    [Min(0f)] public float maxHeaderLift = 260f;
    [Tooltip("Hero art bounds (ear tips / platform bottom). When the header has no spare room (16:9, iPad) the hero shrinks with its platform bottom fixed.")]
    public float heroTopY = -110f;
    public float heroBottomY = -706f;
    [Tooltip("'Tap to continue' sits this far above the safe-area bottom on tall screens; never higher than noticeMaxY.")]
    public float noticeBottomInset = 70f;
    public float noticeMaxY = -1908f;
    [Min(10f)] public float noticeFontSize = 50f;

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
    public bool autoShowJoinPopup = true;
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
