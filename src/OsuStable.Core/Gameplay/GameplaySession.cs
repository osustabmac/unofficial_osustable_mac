using OsuStable.Core.Beatmaps;

namespace OsuStable.Core.Gameplay;

public sealed class GameplaySession
{
    private readonly Beatmap beatmap;
    private readonly HitWindows hitWindows;
    private readonly TimingResolver timingResolver;
    private readonly ObjectState[] states;
    private InputButtons previousButtons;

    public GameplaySession(Beatmap beatmap)
    {
        this.beatmap = beatmap;
        hitWindows = HitWindows.FromOverallDifficulty(beatmap.Difficulty.OverallDifficulty);
        timingResolver = new TimingResolver(beatmap);
        states = beatmap.HitObjects.Select(hitObject => new ObjectState(hitObject)).ToArray();
    }

    public double CurrentTime { get; private set; }
    public ScoreState Score { get; } = new();
    public int Combo => Score.Combo;
    public int MaxCombo => Score.MaxCombo;
    public int GreatCount => Score.GreatCount;
    public int OkCount => Score.OkCount;
    public int MehCount => Score.MehCount;
    public int MissCount => Score.MissCount;
    public IReadOnlyList<ObjectState> Objects => states;

    public IEnumerable<ObjectState> VisibleObjects
    {
        get
        {
            var preempt = StableDifficulty.ApproachPreempt(beatmap.Difficulty.ApproachRate);
            return states.Where(state =>
                state.Result == HitResult.None &&
                state.HitObject.StartTime - CurrentTime <= preempt &&
                CurrentTime - timingResolver.EndTime(state.HitObject) <= hitWindows.Meh);
        }
    }

    public void Update(double time)
    {
        if (time < CurrentTime)
            throw new ArgumentOutOfRangeException(nameof(time), "Gameplay time must be monotonic.");
        CurrentTime = time;

        foreach (var state in states)
        {
            if (state.Result != HitResult.None)
                continue;
            if (state.HitObject.Kind != HitObjectKind.Circle)
                continue;
            if (CurrentTime - state.HitObject.StartTime > hitWindows.Meh)
                applyResult(state, HitResult.Miss, CurrentTime - state.HitObject.StartTime);
        }
    }

    public HitResult TryHit(double playfieldX, double playfieldY, double time)
    {
        Update(time);
        var radius = StableDifficulty.CircleRadius(beatmap.Difficulty.CircleSize);

        var candidate = states
            .Where(state => state.Result == HitResult.None && state.HitObject.Kind == HitObjectKind.Circle)
            .Where(state => Math.Abs(time - state.HitObject.StartTime) <= hitWindows.Meh)
            .Where(state => distance(state.HitObject.X, state.HitObject.Y, playfieldX, playfieldY) <= radius)
            .OrderBy(state => state.HitObject.StartTime)
            .FirstOrDefault();

        if (candidate == null)
            return HitResult.None;

        var error = time - candidate.HitObject.StartTime;
        var result = hitWindows.ResultFor(Math.Abs(error));
        applyResult(candidate, result, error);
        return result;
    }

    public HitResult Process(InputFrame frame)
    {
        var pressed = frame.Buttons & ~previousButtons;
        previousButtons = frame.Buttons;
        Update(frame.Time);
        if ((pressed & (InputButtons.MouseLeft | InputButtons.MouseRight | InputButtons.Key1 | InputButtons.Key2)) == 0)
            return HitResult.None;
        return TryHit(frame.X, frame.Y, frame.Time);
    }

    private void applyResult(ObjectState state, HitResult result, double error)
    {
        state.SetResult(result, error);
        Score.Apply(result, beatmap.Difficulty.HpDrainRate);
    }

    private static double distance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public sealed class ObjectState
{
    internal ObjectState(HitObject hitObject) => HitObject = hitObject;

    public HitObject HitObject { get; }
    public HitResult Result { get; private set; }
    public double? HitError { get; private set; }

    internal void SetResult(HitResult result, double error)
    {
        Result = result;
        HitError = error;
    }
}
