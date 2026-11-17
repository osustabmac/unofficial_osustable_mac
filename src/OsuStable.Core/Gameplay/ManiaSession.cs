using OsuStable.Core.Beatmaps;

namespace OsuStable.Core.Gameplay;

public sealed class ManiaSession
{
    private readonly Beatmap beatmap;
    private readonly ManiaHitWindows windows;
    private readonly ManiaObjectState[] states;
    private readonly bool[] keys;

    public ManiaSession(
        Beatmap beatmap,
        bool convertedFromStandard = false,
        StableManiaMods mods = StableManiaMods.None)
    {
        if (beatmap.General.Mode != 3)
            throw new ArgumentException("Beatmap is not an osu!mania beatmap.", nameof(beatmap));
        this.beatmap = beatmap;
        KeyCount = Math.Clamp((int)Math.Round(beatmap.Difficulty.CircleSize), 1, 9);
        windows = ManiaHitWindows.FromOverallDifficulty(
            beatmap.Difficulty.OverallDifficulty,
            nativeManiaMap: !convertedFromStandard,
            mods);
        states = beatmap.HitObjects
            .Where(hitObject => hitObject.Kind is HitObjectKind.Circle or HitObjectKind.Hold)
            .Select(hitObject => new ManiaObjectState(hitObject, ColumnFor(hitObject.X, KeyCount)))
            .ToArray();
        keys = new bool[KeyCount];
        Score = new ManiaScoreState(states.Length);
    }

    public int KeyCount { get; }
    public double CurrentTime { get; private set; }
    public ManiaScoreState Score { get; }
    public IReadOnlyList<ManiaObjectState> Objects => states;
    public IReadOnlyList<bool> Keys => keys;
    public bool Complete => states.Length > 0 && states.All(state => state.Result != ManiaHitResult.None);

    public IEnumerable<ManiaObjectState> VisibleObjects(double preempt = 1000) =>
        states.Where(state => state.Result == ManiaHitResult.None &&
            state.HitObject.StartTime - CurrentTime <= preempt &&
            state.EndTime >= CurrentTime - windows.Ok);

    public static int ColumnFor(int x, int keyCount) => Math.Clamp(x * keyCount / 512, 0, keyCount - 1);

    public void Advance(double time)
    {
        if (time < CurrentTime)
            throw new ArgumentOutOfRangeException(nameof(time), "Gameplay time must be monotonic.");
        CurrentTime = time;

        foreach (var state in states.Where(state => state.Result == ManiaHitResult.None))
        {
            if (!state.HeadHit && time - state.HitObject.StartTime > windows.Ok)
                judge(state, ManiaHitResult.Miss, time - state.HitObject.StartTime);
            else if (state.IsHold && state.HeadHit && time - state.EndTime > windows.Ok)
                judge(state, ManiaHitResult.Miss, time - state.EndTime);
        }
    }

    public ManiaHitResult SetKey(int column, bool pressed, double time)
    {
        Advance(time);
        if (column < 0 || column >= KeyCount || keys[column] == pressed)
            return ManiaHitResult.None;
        keys[column] = pressed;
        return pressed ? press(column, time) : release(column, time);
    }

    private ManiaHitResult press(int column, double time)
    {
        var activeHold = states.FirstOrDefault(state => state.Column == column && state.IsHold && state.HeadHit && state.Result == ManiaHitResult.None);
        if (activeHold != null)
            return ManiaHitResult.None;

        var state = states
            .Where(candidate => candidate.Column == column && candidate.Result == ManiaHitResult.None && !candidate.HeadHit)
            .OrderBy(candidate => candidate.HitObject.StartTime)
            .FirstOrDefault();
        if (state == null)
            return ManiaHitResult.None;

        var error = time - state.HitObject.StartTime;
        if (error < -windows.Miss)
            return ManiaHitResult.None;
        var result = windows.ResultForPress(error);
        if (result == ManiaHitResult.None)
            return ManiaHitResult.None;

        if (state.IsHold)
        {
            state.BeginHold(error);
            // Stable records the head time but only emits one judgement for a
            // long note after the tail state resolves.
            return ManiaHitResult.None;
        }

        judge(state, result, error);
        return result;
    }

    private ManiaHitResult release(int column, double time)
    {
        var state = states.FirstOrDefault(candidate =>
            candidate.Column == column && candidate.IsHold && candidate.HeadHit && candidate.Result == ManiaHitResult.None);
        if (state == null)
            return ManiaHitResult.None;

        var tailError = time - state.EndTime;
        if (tailError < -windows.Meh)
        {
            state.MarkBroken();
            return ManiaHitResult.None;
        }
        if (tailError > windows.Ok)
        {
            judge(state, ManiaHitResult.Miss, tailError);
            return ManiaHitResult.Miss;
        }

        var head = Math.Abs(state.HeadError!.Value);
        var tail = Math.Abs(tailError);
        var combined = head + tail;
        var result = head <= windows.Perfect * 1.2 && combined <= windows.Perfect * 2.4 ? ManiaHitResult.Perfect
            : head <= windows.Great * 1.1 && combined <= windows.Great * 2.2 ? ManiaHitResult.Great
            : head <= windows.Good && combined <= windows.Good * 2 ? ManiaHitResult.Good
            : head <= windows.Ok && combined <= windows.Ok * 2 ? ManiaHitResult.Ok
            : ManiaHitResult.Meh;
        // Stable permits a released-and-regrabbed hold to recover, but caps an
        // otherwise Great/Perfect judgement at Good.
        if (state.Broken && result > ManiaHitResult.Good)
            result = ManiaHitResult.Good;
        judge(state, result, tailError);
        return result;
    }

    private void judge(ManiaObjectState state, ManiaHitResult result, double error)
    {
        state.SetResult(result, error);
        Score.Apply(result, beatmap.Difficulty.HpDrainRate);
    }
}

public sealed class ManiaObjectState
{
    internal ManiaObjectState(HitObject hitObject, int column)
    {
        HitObject = hitObject;
        Column = column;
    }

    public HitObject HitObject { get; }
    public int Column { get; }
    public bool IsHold => HitObject.Kind == HitObjectKind.Hold;
    public double EndTime => IsHold ? HitObject.EndTime : HitObject.StartTime;
    public bool HeadHit { get; private set; }
    public bool Broken { get; private set; }
    public double? HeadError { get; private set; }
    public double? TailError { get; private set; }
    public ManiaHitResult Result { get; private set; }

    internal void BeginHold(double error)
    {
        HeadHit = true;
        HeadError = error;
    }

    internal void MarkBroken() => Broken = true;

    internal void SetResult(ManiaHitResult result, double error)
    {
        Result = result;
        if (IsHold)
            TailError = error;
        else
            HeadError = error;
    }
}
