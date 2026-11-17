using System.Globalization;

namespace OsuStable.Core.Beatmaps;

public static class BeatmapParser
{
    public static Beatmap ParseFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream, Path.GetFullPath(path));
    }

    public static Beatmap Parse(Stream stream, string sourcePath = "")
    {
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var general = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var difficulty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var timingPoints = new List<TimingPoint>();
        var hitObjects = new List<HitObject>();
        var section = string.Empty;
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1];
                continue;
            }

            switch (section)
            {
                case "General":
                    parseKeyValue(line, general);
                    break;
                case "Metadata":
                    parseKeyValue(line, metadata);
                    break;
                case "Difficulty":
                    parseKeyValue(line, difficulty);
                    break;
                case "TimingPoints":
                    timingPoints.Add(parseTimingPoint(line));
                    break;
                case "HitObjects":
                    hitObjects.Add(parseHitObject(line));
                    break;
            }
        }

        if (hitObjects.Count == 0)
            throw new InvalidDataException("Beatmap contains no hit objects.");

        var od = number(difficulty, "OverallDifficulty", 5);
        return new Beatmap(
            new GeneralSettings(
                text(general, "AudioFilename"),
                integer(general, "AudioLeadIn"),
                integer(general, "PreviewTime", -1),
                boolean(general, "Countdown", true),
                text(general, "SampleSet", "Normal"),
                integer(general, "Mode")),
            new BeatmapMetadata(
                text(metadata, "Title"),
                text(metadata, "TitleUnicode"),
                text(metadata, "Artist"),
                text(metadata, "ArtistUnicode"),
                text(metadata, "Creator"),
                text(metadata, "Version"),
                text(metadata, "Source"),
                text(metadata, "Tags").Split(' ', StringSplitOptions.RemoveEmptyEntries),
                integer(metadata, "BeatmapID", -1),
                integer(metadata, "BeatmapSetID", -1)),
            new DifficultySettings(
                number(difficulty, "HPDrainRate", 5),
                number(difficulty, "CircleSize", 5),
                od,
                number(difficulty, "ApproachRate", od),
                number(difficulty, "SliderMultiplier", 1.4),
                number(difficulty, "SliderTickRate", 1)),
            timingPoints.OrderBy(point => point.Time).ToArray(),
            hitObjects.OrderBy(hitObject => hitObject.StartTime).ToArray(),
            sourcePath);
    }

    private static void parseKeyValue(string line, IDictionary<string, string> target)
    {
        var separator = line.IndexOf(':');
        if (separator <= 0)
            return;
        target[line[..separator].Trim()] = line[(separator + 1)..].Trim();
    }

    private static TimingPoint parseTimingPoint(string line)
    {
        var values = line.Split(',');
        if (values.Length < 2)
            throw new InvalidDataException($"Invalid timing point: {line}");

        return new TimingPoint(
            parseDouble(values[0]),
            parseDouble(values[1]),
            parseInt(values, 2, 4),
            parseInt(values, 3),
            parseInt(values, 4),
            parseInt(values, 5, 100),
            parseInt(values, 6, 1) == 1,
            parseInt(values, 7));
    }

    private static HitObject parseHitObject(string line)
    {
        var values = line.Split(',');
        if (values.Length < 5)
            throw new InvalidDataException($"Invalid hit object: {line}");

        var type = parseInt(values[3]);
        var kind = (type & 1) != 0 ? HitObjectKind.Circle
            : (type & 2) != 0 ? HitObjectKind.Slider
            : (type & 8) != 0 ? HitObjectKind.Spinner
            : (type & 128) != 0 ? HitObjectKind.Hold
            : throw new InvalidDataException($"Unsupported hit object type {type}: {line}");

        var endTime = parseInt(values[2]);
        SliderData? slider = null;
        var hitSampleIndex = 5;

        if (kind == HitObjectKind.Slider)
        {
            if (values.Length < 8)
                throw new InvalidDataException($"Invalid slider: {line}");
            slider = parseSlider(values);
            hitSampleIndex = 10;
        }
        else if (kind == HitObjectKind.Spinner)
        {
            endTime = parseInt(values, 5, endTime);
            hitSampleIndex = 6;
        }
        else if (kind == HitObjectKind.Hold && values.Length > 5)
        {
            var hold = values[5].Split(':', 2);
            endTime = parseInt(hold[0]);
            hitSampleIndex = -1;
        }

        return new HitObject(
            parseInt(values[0]),
            parseInt(values[1]),
            parseInt(values[2]),
            kind,
            (type & 4) != 0,
            (type >> 4) & 7,
            parseInt(values[4]),
            endTime,
            slider,
            hitSampleIndex >= 0 && hitSampleIndex < values.Length ? values[hitSampleIndex] : string.Empty);
    }

    private static SliderData parseSlider(string[] values)
    {
        var curve = values[5].Split('|');
        var curveType = curve[0].Length > 0 ? curve[0][0] : 'B';
        var points = new List<PlayfieldPoint>();
        foreach (var point in curve.Skip(1))
        {
            var coordinates = point.Split(':');
            if (coordinates.Length == 2)
                points.Add(new PlayfieldPoint(parseInt(coordinates[0]), parseInt(coordinates[1])));
        }

        return new SliderData(
            curveType,
            points,
            Math.Max(1, parseInt(values, 6, 1)),
            parseDouble(values[7]),
            values.Length > 8 ? values[8].Split('|').Select(parseInt).ToArray() : [],
            values.Length > 9 ? values[9].Split('|') : []);
    }

    private static string text(IReadOnlyDictionary<string, string> values, string key, string fallback = "") =>
        values.TryGetValue(key, out var value) ? value : fallback;

    private static int integer(IReadOnlyDictionary<string, string> values, string key, int fallback = 0) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : fallback;

    private static double number(IReadOnlyDictionary<string, string> values, string key, double fallback) =>
        values.TryGetValue(key, out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : fallback;

    private static bool boolean(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var value) ? value != "0" : fallback;

    private static int parseInt(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static int parseInt(string[] values, int index, int fallback = 0) =>
        index < values.Length && int.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : fallback;
    private static double parseDouble(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
}

