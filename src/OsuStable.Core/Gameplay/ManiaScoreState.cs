namespace OsuStable.Core.Gameplay;

// Direct semantic port of stable's osu!mania ScoreV1 branch. The health deltas
// remain isolated because stable's drain processor is a separate subsystem.
public sealed class ManiaScoreState
{
    private const int BaseScorePortion = 500_000;
    private const int MaximumHitValue = 320;
    private const int MaximumBonus = 100;
    private const int BonusResetInterval = 384;
    private readonly double scoreUnit;
    private readonly double scoreIncreaseMultiplier;
    private int score;
    private double bonus = MaximumBonus;

    public ManiaScoreState(
        int totalObjects,
        double scoreUnitMultiplier = 1,
        double scoreIncreaseMultiplier = 1)
    {
        scoreUnit = (double)BaseScorePortion / Math.Max(1, totalObjects) * scoreUnitMultiplier;
        this.scoreIncreaseMultiplier = Math.Max(double.Epsilon, scoreIncreaseMultiplier);
    }

    public int Score => Math.Clamp(score, 0, 1_000_000);
    public double Bonus => bonus;
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }
    public double Health { get; private set; } = 1;
    public int PerfectCount { get; private set; }
    public int GreatCount { get; private set; }
    public int GoodCount { get; private set; }
    public int OkCount { get; private set; }
    public int MehCount { get; private set; }
    public int MissCount { get; private set; }
    public int TotalJudged => PerfectCount + GreatCount + GoodCount + OkCount + MehCount + MissCount;
    public bool Failed => Health <= 0;
    public double Accuracy => TotalJudged == 0 ? 1 :
        (PerfectCount * 300 + GreatCount * 300 + GoodCount * 200 + OkCount * 100 + MehCount * 50) /
        (TotalJudged * 300.0);

    public void Apply(ManiaHitResult result, double hpDrainRate)
    {
        var (hitValue, bonusValue) = result switch
        {
            ManiaHitResult.Perfect => (320, 32),
            ManiaHitResult.Great => (300, 32),
            ManiaHitResult.Good => (200, 16),
            ManiaHitResult.Ok => (100, 8),
            ManiaHitResult.Meh => (50, 4),
            ManiaHitResult.Miss => (0, 0),
            _ => (0, 0)
        };
        if (result == ManiaHitResult.None)
            return;

        switch (result)
        {
            case ManiaHitResult.Perfect: PerfectCount++; break;
            case ManiaHitResult.Great: GreatCount++; break;
            case ManiaHitResult.Good: GoodCount++; break;
            case ManiaHitResult.Ok: OkCount++; break;
            case ManiaHitResult.Meh: MehCount++; break;
            case ManiaHitResult.Miss: MissCount++; break;
        }

        bonus = result switch
        {
            ManiaHitResult.Perfect => bonus + 2,
            ManiaHitResult.Great => bonus + 1,
            ManiaHitResult.Good => bonus - 8 / scoreIncreaseMultiplier,
            ManiaHitResult.Ok => bonus - 24 / scoreIncreaseMultiplier,
            ManiaHitResult.Meh => bonus - 44 / scoreIncreaseMultiplier,
            ManiaHitResult.Miss => -56 / scoreIncreaseMultiplier,
            _ => bonus
        };
        if (TotalJudged != 0 && TotalJudged % BonusResetInterval == 0)
            bonus = MaximumBonus;
        bonus = Math.Clamp(bonus, 0, MaximumBonus);

        var bonusScore = Math.Sqrt(bonus) * bonusValue * scoreUnit / MaximumHitValue;
        var baseScore = hitValue * scoreUnit / MaximumHitValue;
        score += (int)(baseScore + bonusScore);

        if (result == ManiaHitResult.Miss)
            Combo = 0;
        else
            MaxCombo = Math.Max(MaxCombo, ++Combo);

        applyHealth(result, hpDrainRate);
    }

    private void applyHealth(ManiaHitResult result, double hpDrainRate)
    {
        var hp = Math.Clamp(hpDrainRate, 0, 10);
        Health = Math.Clamp(Health + (result switch
        {
            ManiaHitResult.Perfect => (10 - hp * 0.3) / 100,
            ManiaHitResult.Great => (8 - hp * 0.3) / 100,
            ManiaHitResult.Good => (4 - hp * 0.3) / 100,
            ManiaHitResult.Ok => 0,
            ManiaHitResult.Meh => -(2 + hp * 0.3) / 100,
            ManiaHitResult.Miss => -(6 + hp * 1.4) / 100,
            _ => 0
        }), 0, 1);
    }
}
