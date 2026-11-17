namespace OsuStable.Native;

internal static class ApplicationPaths
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "osustabwin");

    public static string SongsDirectory => Path.Combine(DataDirectory, "Songs");
    public static string ConfigurationFile => Path.Combine(DataDirectory, "osu!.cfg");
}
