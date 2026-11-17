namespace OsuStable.Core.Beatmaps;

public sealed class TimingResolver
{
    private readonly Beatmap beatmap;

    public TimingResolver(Beatmap beatmap) => this.beatmap = beatmap;

    public TimingState At(double time)
    {
        var active = beatmap.TimingPoints
            .Where(point => point.Time <= time)
            .ToArray();

        var basePoint = active.LastOrDefault(point => point.Uninherited)
            ?? beatmap.TimingPoints.FirstOrDefault(point => point.Uninherited)
            ?? new TimingPoint(0, 500, 4, 0, 0, 100, true, 0);
        var latest = active.LastOrDefault() ?? basePoint;
        var sliderVelocity = latest.Uninherited || latest.BeatLength >= 0
            ? 1
            : Math.Clamp(-100 / latest.BeatLength, 0.1, 10);

        return new TimingState(
            basePoint.BeatLength,
            sliderVelocity,
            latest.Meter,
            latest.SampleSet,
            latest.SampleIndex,
            latest.Volume,
            (latest.Effects & 1) != 0);
    }

    public double SliderDuration(HitObject hitObject)
    {
        if (hitObject.Kind != HitObjectKind.Slider || hitObject.Slider == null)
            throw new ArgumentException("Hit object is not a slider.", nameof(hitObject));

        var timing = At(hitObject.StartTime);
        var scoringDistance = 100 * beatmap.Difficulty.SliderMultiplier * timing.SliderVelocity;
        if (scoringDistance <= 0)
            throw new InvalidDataException("Slider scoring distance is not positive.");

        var spanDuration = hitObject.Slider.PixelLength / scoringDistance * timing.BeatLength;
        return Math.Max(0, spanDuration * hitObject.Slider.RepeatCount);
    }

    public double EndTime(HitObject hitObject) => hitObject.Kind switch
    {
        HitObjectKind.Circle => hitObject.StartTime,
        HitObjectKind.Slider => hitObject.StartTime + SliderDuration(hitObject),
        _ => hitObject.EndTime
    };
}

public readonly record struct TimingState(
    double BeatLength,
    double SliderVelocity,
    int Meter,
    int SampleSet,
    int SampleIndex,
    int Volume,
    bool Kiai);

