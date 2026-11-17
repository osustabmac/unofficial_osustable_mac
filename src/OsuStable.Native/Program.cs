using OsuStable.Core.Beatmaps;
using OsuStable.Core.Gameplay;
using OsuStable.Native;
using OsuStable.Native.Platform;

var headlessImport = args.Contains("--headless-import", StringComparer.OrdinalIgnoreCase);
var verifyLibrary = args.Contains("--verify-library", StringComparer.OrdinalIgnoreCase);
var verifyMania = args.Contains("--verify-mania", StringComparer.OrdinalIgnoreCase);
var inputs = args.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray();
foreach (var archive in inputs.Where(path => path.EndsWith(".osz", StringComparison.OrdinalIgnoreCase)))
{
    var imported = BeatmapImporter.ImportOsz(archive, ApplicationPaths.SongsDirectory);
    Console.WriteLine($"Imported beatmap to {imported}");
}
if (verifyLibrary)
{
    var library = BeatmapLibrary.Scan(ApplicationPaths.SongsDirectory);
    foreach (var map in library.Beatmaps)
    {
        var playable = ManiaBeatmapConverter.Convert(map);
        Console.WriteLine($"mode={map.General.Mode} maniaObjects={playable.HitObjects.Count} {map.Metadata.Artist} - {map.Metadata.Title} [{map.Metadata.Version}]");
    }
    foreach (var failure in library.Failures)
        Console.Error.WriteLine($"Failed: {failure.Path}: {failure.Error}");
    return library.Failures.Count == 0 && library.Beatmaps.Count > 0 ? 0 : 1;
}
if (verifyMania)
{
    var library = BeatmapLibrary.Scan(ApplicationPaths.SongsDirectory);
    var failed = false;
    foreach (var map in library.Beatmaps)
    {
        var result = ManiaAutoplayVerifier.Verify(map);
        var passed = result.Complete && result.Misses == 0 && result.Judged == result.Objects;
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")} 4K objects={result.Objects} judged={result.Judged} misses={result.Misses} score={result.Score} accuracy={result.Accuracy:P2} {map.Metadata.Artist} - {map.Metadata.Title} [{map.Metadata.Version}]");
        failed |= !passed;
    }
    return !failed && library.Beatmaps.Count > 0 ? 0 : 1;
}
if (headlessImport)
    return 0;

var directBeatmap = inputs.FirstOrDefault(path => path.EndsWith(".osu", StringComparison.OrdinalIgnoreCase));
using var host = new SdlPlatformHost(directBeatmap);
return host.Run();

