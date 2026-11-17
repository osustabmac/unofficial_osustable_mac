using System.IO.Compression;

namespace OsuStable.Core.Beatmaps;

public static class BeatmapImporter
{
    public static string ImportOsz(string archivePath, string songsDirectory)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Beatmap archive was not found.", archivePath);
        Directory.CreateDirectory(songsDirectory);

        var baseName = sanitize(Path.GetFileNameWithoutExtension(archivePath));
        var destination = uniqueDirectory(Path.Combine(songsDirectory, baseName));
        Directory.CreateDirectory(destination);
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            if (!archive.Entries.Any(entry => entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Archive contains no .osu beatmap files.");

            var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            foreach (var entry in archive.Entries)
            {
                var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                if (!target.StartsWith(root, StringComparison.Ordinal))
                    throw new InvalidDataException("Beatmap archive contains an unsafe path.");
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target);
            }
            return destination;
        }
        catch
        {
            Directory.Delete(destination, recursive: true);
            throw;
        }
    }

    private static string uniqueDirectory(string candidate)
    {
        if (!Directory.Exists(candidate))
            return candidate;
        for (var suffix = 2; ; suffix++)
        {
            var numbered = $"{candidate} ({suffix})";
            if (!Directory.Exists(numbered))
                return numbered;
        }
    }

    private static string sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var value = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrEmpty(value) ? "Imported beatmap" : value;
    }
}
