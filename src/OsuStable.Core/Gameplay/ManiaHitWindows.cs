namespace OsuStable.Core.Gameplay;

[Flags]
public enum StableManiaMods
{
    None = 0,
    Easy = 1 << 0,
    HardRock = 1 << 1,
    DoubleTime = 1 << 2,
    HalfTime = 1 << 3,
    ScoreV2 = 1 << 4
}

// Direct semantic port of stable's mania hit-window initialisation from the
// decompiled HitObjectManagerMania implementation.
public readonly record struct ManiaHitWindows(
    int Perfect,
    int Great,
    int Good,
    int Ok,
    int Meh,
    int Miss)
{
    public static ManiaHitWindows FromOverallDifficulty(
        double overallDifficulty,
        bool nativeManiaMap = true,
        StableManiaMods mods = StableManiaMods.None)
    {
        var od = Math.Clamp(overallDifficulty, 0, 10);
        double perfect;
        double great;
        double good;
        double ok;
        double meh;
        double miss;

        if ((mods & StableManiaMods.ScoreV2) != 0)
        {
            perfect = difficultyRange(od, 22.4, 19.4, 13.9);
            great = difficultyRange(od, 64, 49, 34);
            good = difficultyRange(od, 97, 82, 67);
            ok = difficultyRange(od, 127, 112, 97);
            meh = difficultyRange(od, 151, 136, 121);
            miss = difficultyRange(od, 188, 173, 158);
        }
        else if (nativeManiaMap)
        {
            var inverseOd = Math.Clamp(10 - od, 0, 10);
            perfect = 16;
            great = 34 + 3 * inverseOd;
            good = 67 + 3 * inverseOd;
            ok = 97 + 3 * inverseOd;
            meh = 121 + 3 * inverseOd;
            miss = 158 + 3 * inverseOd;
        }
        else if (Math.Round(od) > 4)
        {
            perfect = 16; great = 34; good = 67; ok = 97; meh = 121; miss = 158;
        }
        else
        {
            perfect = 16; great = 47; good = 77; ok = 97; meh = 121; miss = 158;
        }

        return new ManiaHitWindows(
            applyMods(perfect, mods),
            applyMods(great, mods),
            applyMods(good, mods),
            applyMods(ok, mods),
            applyMods(meh, mods),
            applyMods(miss, mods));
    }

    public ManiaHitResult ResultForPress(double error)
    {
        var absolute = Math.Abs(error);
        if (absolute <= Perfect) return ManiaHitResult.Perfect;
        if (absolute <= Great) return ManiaHitResult.Great;
        if (absolute <= Good) return ManiaHitResult.Good;
        if (absolute <= Ok) return ManiaHitResult.Ok;
        if (error < 0 && absolute <= Meh) return ManiaHitResult.Meh;
        // Stable starts considering a lane at -Miss, before the visible Meh
        // window. An excessively early key therefore consumes the object as a
        // miss; on the late side the manager has already auto-missed at +Ok.
        if (error < 0 && absolute <= Miss) return ManiaHitResult.Miss;
        return ManiaHitResult.None;
    }

    private static int applyMods(double value, StableManiaMods mods)
    {
        if ((mods & StableManiaMods.HardRock) != 0)
            value /= 1.4;
        else if ((mods & StableManiaMods.Easy) != 0)
            value *= 1.4;

        if ((mods & StableManiaMods.DoubleTime) != 0)
            value *= 1.5;
        else if ((mods & StableManiaMods.HalfTime) != 0)
            value *= 0.75;
        return (int)value;
    }

    private static double difficultyRange(double difficulty, double minimum, double middle, double maximum) =>
        difficulty > 5
            ? middle + (maximum - middle) * (difficulty - 5) / 5
            : difficulty < 5
                ? middle - (middle - minimum) * (5 - difficulty) / 5
                : middle;
}

public enum ManiaHitResult
{
    None,
    Miss,
    Meh,
    Ok,
    Good,
    Great,
    Perfect
}
