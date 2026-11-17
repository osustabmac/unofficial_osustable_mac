namespace OsuStable.Core.Gameplay;

public sealed class ScoreState
{
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }
    public int GreatCount { get; private set; }
    public int OkCount { get; private set; }
    public int MehCount { get; private set; }
    public int MissCount { get; private set; }
    public double Health { get; private set; } = 1;

    public int TotalHits => GreatCount + OkCount + MehCount + MissCount;
    public double Accuracy => TotalHits == 0
        ? 1
        : (GreatCount * 300 + OkCount * 100 + MehCount * 50) / (TotalHits * 300.0);

    public void Apply(HitResult result, double hpDrainRate)
    {
        var hp = Math.Clamp(hpDrainRate, 0, 10);
        switch (result)
        {
            case HitResult.Great:
                GreatCount++;
                Score += 300;
                Combo++;
                Health += (10.2 - hp) / 100;
                break;
            case HitResult.Ok:
                OkCount++;
                Score += 100;
                Combo++;
                Health += (4 - hp * 0.4) / 100;
                break;
            case HitResult.Meh:
                MehCount++;
                Score += 50;
                Combo++;
                break;
            case HitResult.Miss:
                MissCount++;
                Combo = 0;
                Health -= (2 + hp * 2) / 100;
                break;
            case HitResult.None:
                return;
        }

        Health = Math.Clamp(Health, 0, 1);
        MaxCombo = Math.Max(MaxCombo, Combo);
    }
}

