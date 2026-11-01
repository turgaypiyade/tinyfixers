using System;

/// <summary>One shuffled, persistent prize layout per board; never rolls again on a shot or reopen.</summary>
public static class GiftFestivalRewards
{
    public static bool IsEmpty(DailySlotReward reward) => reward == null || reward.type == DailySlotRewardType.Empty;

    public static int EmptyCount(int size, float fraction)
    {
        if (size < 2 || size > 4) return 0;
        if (float.IsNaN(fraction)) fraction = 0.25f;
        int count = size * size;
        return Math.Max(0, Math.Min(count - 1, (int)Math.Round(count * Math.Max(0, Math.Min(1, fraction)), MidpointRounding.AwayFromZero)));
    }

    /// Coins/stars are multiplied by the lap scale (first lap pays less); item counts stay as authored.
    public static DailySlotReward Scale(DailySlotReward source, float scale)
    {
        if (source == null) return null;
        int amount = Math.Max(1, source.amount);
        if (source.type == DailySlotRewardType.Coins || source.type == DailySlotRewardType.Stars)
            amount = Math.Max(1, (int)Math.Round(amount * (double)scale, MidpointRounding.AwayFromZero));
        return new DailySlotReward { type = source.type, amount = amount, durationMinutes = source.durationMinutes };
    }

    public static DailySlotReward[] Create(GiftFestivalRound round, float fraction, int seed, int board, int legacyOpenedMask, float scale = 1f)
    {
        int count = round.Size * round.Size;
        var prizes = new DailySlotReward[count];
        var order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;
        uint random = unchecked((uint)seed ^ ((uint)board + 1u) * 2654435761u);
        if (random == 0) random = 0xA341316Cu;
        for (int i = count - 1; i > 0; i--)
        {
            int j = (int)(Next(ref random) % (uint)(i + 1));
            int temp = order[i]; order[i] = order[j]; order[j] = temp;
        }
        int empty = EmptyCount(round.Size, fraction), rewardIndex = 0;
        int offset = (int)(Next(ref random) & 0x7fffffffu);
        for (int i = 0; i < count; i++)
        {
            int cell = order[i];
            if ((legacyOpenedMask & (1 << cell)) != 0)
            {
                // An already-paid coin box from the old save must not be relabelled as another prize.
                prizes[cell] = new DailySlotReward { type = DailySlotRewardType.Coins, amount = Math.Max(1, round.boxCoins) };
                continue;
            }
            if (empty > 0)
            {
                prizes[cell] = new DailySlotReward { type = DailySlotRewardType.Empty, amount = 0 };
                empty--; continue;
            }
            var deck = round.boxRewards;
            var source = deck != null && deck.Length > 0 ? deck[(int)(((long)offset + rewardIndex++) % deck.Length)] : null;
            prizes[cell] = Scale(source ?? new DailySlotReward { type = DailySlotRewardType.Coins, amount = Math.Max(1, round.boxCoins) }, scale);
        }
        return prizes;
    }

    private static uint Next(ref uint state)
    {
        state ^= state << 13; state ^= state >> 17; state ^= state << 5;
        return state;
    }
}
