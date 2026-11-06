// Pure counters. Reservations prevent simultaneous broods eating the same last half twice.
public sealed class FlyFruitProgress
{
    public const int FliesPerEgg = 3, BitesPerFruit = 6;
    public int Remaining { get; private set; } = BitesPerFruit;
    public int Reserved { get; private set; }
    public bool TryReserveBrood()
    {
        if (Remaining - Reserved < FliesPerEgg) return false;
        Reserved += FliesPerEgg; return true;
    }
    public bool Arrive()
    {
        if (Reserved <= 0 || Remaining <= 0) return false;
        Reserved--; Remaining--; return Remaining == 0;
    }
    public void Cancel(int count) { Reserved -= count; if (Reserved < 0) Reserved = 0; }
}

public sealed class FlyEggBoxProgress
{
    private int available = 15, reserved;
    public int Placed { get; private set; }
    public bool Complete => Placed == 4 && reserved == 0;
    public int VisibleMask => available;
    public int Available
    {
        get { int n = 0; for (int i = 0; i < 4; i++) if ((available & (1 << i)) != 0) n++; return n; }
    }
    public bool TryReserve(out int slot)
    {
        for (slot = 0; slot < 4; slot++)
            if ((available & (1 << slot)) != 0)
            { available &= ~(1 << slot); reserved |= 1 << slot; return true; }
        slot = -1; return false;
    }
    public bool Finish(int slot, bool placed)
    {
        if (slot < 0 || slot >= 4 || (reserved & (1 << slot)) == 0) return false;
        reserved &= ~(1 << slot);
        if (placed) Placed++; else available |= 1 << slot;
        return true;
    }
}
