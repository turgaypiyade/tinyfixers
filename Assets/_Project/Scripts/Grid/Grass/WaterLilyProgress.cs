/// <summary>Persistent flower damage state; grass takes over only after the fourth petal falls.</summary>
public sealed class WaterLilyProgress
{
    public int RemainingPetals { get; private set; } = 4;
    public bool HasReleased { get; private set; }
    public bool IsDead { get; private set; }
    public bool CanSwallow => !IsDead && RemainingPetals == 4;

    // True means release-only. False means a petal fell (or only grass remains).
    public bool Hit(bool hasStoredSpecial)
    {
        if (RemainingPetals == 0) return false;
        if (hasStoredSpecial && !HasReleased)
        {
            HasReleased = true;
            return true;
        }
        IsDead = true;
        RemainingPetals--;
        return false;
    }
}
