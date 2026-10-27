namespace Aion2PetTracker.Core.Pets;

public sealed record PetProgress(int Level, int Current, int Goal, long Total)
{
    public static PetProgress From(long total)
    {
        if (total < 0)
            throw new ArgumentOutOfRangeException(nameof(total));
        return total >= 105 ? new(3, 0, 0, total) : total >= 30 ? new(2, (int)total - 30, 75, total) :
            total >= 5 ? new(1, (int)total - 5, 25, total) : new(0, (int)total, 5, total);
    }
    public string Label => Level == 3 ? "Lvl 3 · MAX" : $"Lvl {Level} · {Current} / {Goal}";
}
public sealed record SoulGain(int PetId, int Amount);
