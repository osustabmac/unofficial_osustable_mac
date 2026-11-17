namespace OsuStable.Core.Gameplay;

public readonly record struct HitWindows(double Great, double Ok, double Meh)
{
    public static HitWindows FromOverallDifficulty(double overallDifficulty)
    {
        var od = Math.Clamp(overallDifficulty, 0, 10);
        return new HitWindows(
            80 - 6 * od,
            140 - 8 * od,
            200 - 10 * od);
    }

    public HitResult ResultFor(double absoluteError)
    {
        if (absoluteError <= Great)
            return HitResult.Great;
        if (absoluteError <= Ok)
            return HitResult.Ok;
        if (absoluteError <= Meh)
            return HitResult.Meh;
        return HitResult.Miss;
    }
}

public enum HitResult
{
    None,
    Great,
    Ok,
    Meh,
    Miss
}

