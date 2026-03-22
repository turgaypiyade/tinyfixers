using System.Collections.Generic;

public sealed class PendingCreationStore
{
    public readonly struct PendingCreation
    {
        public readonly int x;
        public readonly int y;
        public readonly TileSpecial special;

        public PendingCreation(int x, int y, TileSpecial special)
        {
            this.x = x;
            this.y = y;
            this.special = special;
        }
    }

    private readonly List<PendingCreation> items = new();

    public bool HasPending => items.Count > 0;
    public int Count => items.Count;

    public (int x, int y, TileSpecial special) LastCaptured { get; private set; }

    public void Clear()
    {
        items.Clear();
        LastCaptured = default;
    }

    public void Store(int x, int y, TileSpecial special)
    {
        if (special == TileSpecial.None)
            return;

        var item = new PendingCreation(x, y, special);
        items.Add(item);
        LastCaptured = (x, y, special);
    }

    public List<PendingCreation> Drain()
    {
        var copy = new List<PendingCreation>(items);
        items.Clear();
        LastCaptured = default;
        return copy;
    }

    public IReadOnlyList<PendingCreation> PeekAll()
    {
        return items;
    }
}