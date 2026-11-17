namespace OsuStable.Core.Beatmaps;

public static class BeatmapLibrary
{
    public static BeatmapLibraryResult Scan(string songsDirectory)
    {
        if (!Directory.Exists(songsDirectory))
            return new BeatmapLibraryResult([], []);

        var beatmaps = new List<Beatmap>();
        var failures = new List<BeatmapLoadFailure>();

        foreach (var path in Directory.EnumerateFiles(songsDirectory, "*.osu", SearchOption.AllDirectories))
        {
            try
            {
                beatmaps.Add(BeatmapParser.ParseFile(path));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
            {
                failures.Add(new BeatmapLoadFailure(path, exception.Message));
            }
        }

        return new BeatmapLibraryResult(
            beatmaps
                .OrderBy(beatmap => beatmap.Metadata.Artist, StringComparer.OrdinalIgnoreCase)
                .ThenBy(beatmap => beatmap.Metadata.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(beatmap => beatmap.Metadata.Version, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            failures);
    }

    public static string ResolveAudioPath(Beatmap beatmap)
    {
        if (string.IsNullOrWhiteSpace(beatmap.SourcePath))
            throw new InvalidOperationException("Beatmap has no source path.");
        if (string.IsNullOrWhiteSpace(beatmap.General.AudioFilename))
            throw new InvalidDataException("Beatmap has no audio filename.");

        var directory = Path.GetDirectoryName(beatmap.SourcePath)
            ?? throw new InvalidOperationException("Beatmap source has no directory.");
        var directoryPrefix = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(directory, beatmap.General.AudioFilename));
        if (!candidate.StartsWith(directoryPrefix, StringComparison.Ordinal))
            throw new InvalidDataException("Beatmap audio path escapes its beatmap directory.");
        return candidate;
    }
}

