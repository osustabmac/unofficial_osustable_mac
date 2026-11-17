namespace OsuStable.Core.Beatmaps;

public sealed record Beatmap(
    GeneralSettings General,
    BeatmapMetadata Metadata,
    DifficultySettings Difficulty,
    IReadOnlyList<TimingPoint> TimingPoints,
    IReadOnlyList<HitObject> HitObjects,
    string SourcePath);

public sealed record GeneralSettings(
    string AudioFilename,
    int AudioLeadIn,
    int PreviewTime,
    bool Countdown,
    string SampleSet,
    int Mode);

public sealed record BeatmapMetadata(
    string Title,
    string TitleUnicode,
    string Artist,
    string ArtistUnicode,
    string Creator,
    string Version,
    string Source,
    IReadOnlyList<string> Tags,
    int BeatmapId,
    int BeatmapSetId);

public sealed record DifficultySettings(
    double HpDrainRate,
    double CircleSize,
    double OverallDifficulty,
    double ApproachRate,
    double SliderMultiplier,
    double SliderTickRate);

public sealed record TimingPoint(
    double Time,
    double BeatLength,
    int Meter,
    int SampleSet,
    int SampleIndex,
    int Volume,
    bool Uninherited,
    int Effects);

public enum HitObjectKind
{
    Circle,
    Slider,
    Spinner,
    Hold
}

public sealed record HitObject(
    int X,
    int Y,
    int StartTime,
    HitObjectKind Kind,
    bool NewCombo,
    int ComboOffset,
    int HitSound,
    int EndTime,
    SliderData? Slider,
    string HitSample);

public sealed record SliderData(
    char CurveType,
    IReadOnlyList<PlayfieldPoint> ControlPoints,
    int RepeatCount,
    double PixelLength,
    IReadOnlyList<int> EdgeSounds,
    IReadOnlyList<string> EdgeSets);

public readonly record struct PlayfieldPoint(int X, int Y);

public sealed record BeatmapLoadFailure(string Path, string Error);

public sealed record BeatmapLibraryResult(
    IReadOnlyList<Beatmap> Beatmaps,
    IReadOnlyList<BeatmapLoadFailure> Failures);

