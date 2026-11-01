using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GiftFestivalConfig", menuName = "TinyFixers/Events/Gift Festival Config")]
public sealed class GiftFestivalConfig : ScriptableObject
{
    public const string ResourcePath = "Events/GiftFestivalConfig";
    private static GiftFestivalConfig shared;
    public static GiftFestivalConfig Shared => shared != null ? shared : (shared = Resources.Load<GiftFestivalConfig>(ResourcePath));

    [Header("Unlock and player-specific UTC cycle")]
    [Min(1)] public int unlockAfterLevel = 100;
    [Min(1)] public int activeHours = 48;
    [Min(1)] public int restHours = 120;
    [Tooltip("Editor only. Never bypasses the production gate in a player build.")]
    public bool debugForceAvailable;

    [Header("Timing (same boundaries are drawn and evaluated)")]
    [Range(0.01f, 0.45f)] public float redWidth = 0.15f;
    [Range(0.02f, 0.8f)] public float greenWidth = 0.20f;
    [Tooltip("Seconds to travel from one end of the meter to the other.")]
    [Range(0.35f, 3f)] public float sweepSeconds = 0.90f;
    [Tooltip("Throw, swing and ball-flight playback multiplier on the first board.")]
    [Range(0.5f, 3f)] public float shotSpeed = 1.35f;
    [Tooltip("Compounding speed increase per completed event board. 0.2 = 20% faster per board.")]
    [Range(0f, 0.5f)] public float speedIncreasePerRound = 0.2f;
    [Header("Prizes")]
    [Tooltip("Hidden empty boxes. Rounded to whole boxes: 1/4, 3/9, 5/16 at 30%.")]
    [Range(0f, 0.75f)] public float emptyBoxFraction = 0.30f;
    public GiftFestivalRound[] rounds = GiftFestivalRound.Defaults();

    [Header("Laps (Safari-like replay inside one active window)")]
    [Tooltip("All boards done → rest, then the boards restart with a fresh prize deck. Off = the event ends after the last board.")]
    public bool lapsEnabled = true;
    [Tooltip("Hours between finishing the last board and the next lap. A lap that could not start before the event ends is skipped.")]
    [Range(0f, 24f)] public float lapRestHours = 3f;
    [Tooltip("Coin/star amount multiplier per lap (1st, 2nd, ...). The last value repeats. Joker/booster counts are unchanged.")]
    public float[] lapRewardScales = { 0.5f, 0.75f, 1f };

    [Header("Main menu invitation")]
    public bool autoShowJoinPopup = true;
    [Range(1f, 5f)] public float introSeconds = 2.6f;
    // Load the texture references only after the event is unlocked, never for early-level win hooks.
    public GiftFestivalArt Art => GiftFestivalArt.Shared;

    public int RoundCount => rounds != null ? rounds.Length : 0;
    public GiftFestivalRound GetRound(int index) => rounds != null && index >= 0 && index < rounds.Length ? rounds[index] : null;
    public float LapRewardScale(int lap) => lapRewardScales == null || lapRewardScales.Length == 0 ? 1f
        : Mathf.Clamp(lapRewardScales[Mathf.Clamp(lap, 0, lapRewardScales.Length - 1)], 0.1f, 5f);
    public float SafeRedWidth => GiftFestivalRules.RedWidth(redWidth);
    public float SafeGreenWidth => GiftFestivalRules.GreenWidth(redWidth, greenWidth);

    private float RoundSpeed(int roundIndex) => Mathf.Pow(1f + Mathf.Clamp(speedIncreasePerRound, 0f, 0.5f),
        Mathf.Clamp(roundIndex, 0, Mathf.Max(0, RoundCount - 1)));
    public float SweepSeconds(int roundIndex) => Mathf.Max(0.35f, sweepSeconds / RoundSpeed(roundIndex));
    public float ShotSpeed(int roundIndex) => Mathf.Clamp(shotSpeed, 0.5f, 3f) * RoundSpeed(roundIndex);
}

[Serializable]
public sealed class GiftFestivalRound
{
    [Range(2, 4)] public int size = 2;
    [HideInInspector] public int boxCoins = 25; // Legacy opened-box display / old configs without a deck.
    [HideInInspector] public int completionCoins;
    [Tooltip("Balanced prize deck, shuffled across non-empty boxes and snapshotted for this board.")]
    public DailySlotReward[] boxRewards = Array.Empty<DailySlotReward>();
    [Tooltip("Optional extra rewards granted together with the board completion bonus.")]
    public DailySlotReward[] completionRewards = Array.Empty<DailySlotReward>();
    public int Size => Mathf.Clamp(size, 2, 4);

    private static DailySlotReward R(DailySlotRewardType type, int amount = 1) => new DailySlotReward { type = type, amount = amount };
    public static GiftFestivalRound[] Defaults() => new[]
    {
        new GiftFestivalRound { size = 2, boxCoins = 25,
            boxRewards = new[] { R(DailySlotRewardType.Coins, 25), R(DailySlotRewardType.Joker_Line), R(DailySlotRewardType.Stars) },
            completionRewards = new[] { R(DailySlotRewardType.Booster_Hammer) } },
        new GiftFestivalRound { size = 3, boxCoins = 40,
            boxRewards = new[] { R(DailySlotRewardType.Coins, 40), R(DailySlotRewardType.Joker_Line), R(DailySlotRewardType.Booster_Shuffle),
                R(DailySlotRewardType.Stars), R(DailySlotRewardType.Coins, 60), R(DailySlotRewardType.Joker_PulseCore), R(DailySlotRewardType.Stars) },
            completionRewards = new[] { R(DailySlotRewardType.Coins, 100), R(DailySlotRewardType.Joker_PulseCore) } },
        new GiftFestivalRound { size = 4, boxCoins = 60,
            boxRewards = new[] { R(DailySlotRewardType.Coins, 50), R(DailySlotRewardType.Coins, 75), R(DailySlotRewardType.Coins, 100),
                R(DailySlotRewardType.Joker_Line), R(DailySlotRewardType.Joker_PulseCore), R(DailySlotRewardType.Joker_SystemOverride),
                R(DailySlotRewardType.Booster_Hammer), R(DailySlotRewardType.Booster_Shuffle), R(DailySlotRewardType.Booster_PaintBucket),
                R(DailySlotRewardType.Stars), R(DailySlotRewardType.Stars), R(DailySlotRewardType.Stars, 2) },
            completionRewards = new[] { R(DailySlotRewardType.Joker_SystemOverride), R(DailySlotRewardType.Booster_PaintBucket), R(DailySlotRewardType.Stars, 2) } },
    };
}
