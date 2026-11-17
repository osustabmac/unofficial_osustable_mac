using System.Globalization;

namespace OsuStable.Native;

// Stable uses a simple `name = value` cfg. Keeping the same representation
// makes configuration human-readable and lets later ports add original keys
// without a format migration.
internal sealed class StableConfiguration
{
    public bool Fullscreen { get; set; }
    public bool FrameSync { get; set; }
    public int VolumeUniversal { get; set; } = 100;
    public int VolumeMusic { get; set; } = 80;
    public int VolumeEffect { get; set; } = 80;
    public int Offset { get; set; }
    public int ManiaSpeed { get; set; } = 20;
    public int DimLevel { get; set; } = 30;

    public static StableConfiguration Load(string path)
    {
        var config = new StableConfiguration();
        if (!File.Exists(path))
            return config;

        foreach (var line in File.ReadLines(path))
        {
            var separator = line.IndexOf('=');
            if (separator < 0)
                continue;
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            switch (key)
            {
                case "Fullscreen": config.Fullscreen = boolean(value, config.Fullscreen); break;
                case "FrameSync": config.FrameSync = boolean(value, config.FrameSync); break;
                case "VolumeUniversal": config.VolumeUniversal = percent(value, config.VolumeUniversal); break;
                case "VolumeMusic": config.VolumeMusic = percent(value, config.VolumeMusic); break;
                case "VolumeEffect": config.VolumeEffect = percent(value, config.VolumeEffect); break;
                case "Offset": config.Offset = integer(value, config.Offset, -500, 500); break;
                case "ManiaSpeed": config.ManiaSpeed = integer(value, config.ManiaSpeed, 1, 40); break;
                case "DimLevel": config.DimLevel = percent(value, config.DimLevel); break;
            }
        }
        return config;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".new";
        File.WriteAllLines(temporary,
        [
            $"Fullscreen = {Fullscreen}",
            $"FrameSync = {FrameSync}",
            $"VolumeUniversal = {VolumeUniversal}",
            $"VolumeMusic = {VolumeMusic}",
            $"VolumeEffect = {VolumeEffect}",
            $"Offset = {Offset}",
            $"ManiaSpeed = {ManiaSpeed}",
            $"DimLevel = {DimLevel}"
        ]);
        File.Move(temporary, path, true);
    }

    public float MusicGain => VolumeUniversal / 100f * VolumeMusic / 100f;

    private static bool boolean(string value, bool fallback) => bool.TryParse(value, out var parsed) ? parsed : fallback;
    private static int percent(string value, int fallback) => integer(value, fallback, 0, 100);
    private static int integer(string value, int fallback, int minimum, int maximum) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;
}
