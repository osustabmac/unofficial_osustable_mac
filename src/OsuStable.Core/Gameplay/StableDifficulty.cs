namespace OsuStable.Core.Gameplay;

public static class StableDifficulty
{
    public static double HitWindow300(double overallDifficulty) => 80 - 6 * clamp(overallDifficulty);
    public static double HitWindow100(double overallDifficulty) => 140 - 8 * clamp(overallDifficulty);
    public static double HitWindow50(double overallDifficulty) => 200 - 10 * clamp(overallDifficulty);

    public static double ApproachPreempt(double approachRate)
    {
        var ar = clamp(approachRate);
        return ar < 5 ? 1800 - 120 * ar : 1200 - 150 * (ar - 5);
    }

    public static double ApproachFadeIn(double approachRate)
    {
        var ar = clamp(approachRate);
        return ar < 5 ? 1200 - 80 * ar : 800 - 100 * (ar - 5);
    }

    public static double CircleRadius(double circleSize) => 54.4 - 4.48 * clamp(circleSize);

    private static double clamp(double value) => Math.Clamp(value, 0, 10);
}

