using OsuStable.Core.Beatmaps;

namespace OsuStable.Core.Gameplay;

public static class ManiaBeatmapConverter
{
    public static Beatmap Convert(Beatmap source, int keyCount = 4)
    {
        if (source.General.Mode == 3)
            return source;
        if (source.General.Mode != 0)
            throw new NotSupportedException("Only osu! and osu!mania maps can currently be played as mania.");

        keyCount = Math.Clamp(keyCount, 1, 9);
        var timing = new TimingResolver(source);
        var random = new StableFastRandom(conversionSeed(source));
        var converted = new List<HitObject>();
        foreach (var hitObject in source.HitObjects)
        {
            switch (hitObject.Kind)
            {
                case HitObjectKind.Circle:
                    converted.Add(asNote(hitObject, keyCount));
                    break;
                case HitObjectKind.Slider:
                    converted.Add(asHold(hitObject, (int)Math.Round(timing.EndTime(hitObject)), keyCount));
                    break;
                case HitObjectKind.Spinner:
                    // Stable's spinner conversion produces exactly one object:
                    // a note for sub-100ms spinners, otherwise a long note.
                    // 8K reserves column zero for the special key unless a
                    // short Finish spinner explicitly targets it.
                    var duration = hitObject.EndTime - hitObject.StartTime;
                    var lowerColumn = keyCount == 8 ? 1 : 0;
                    var finish = (hitObject.HitSound & 4) != 0;
                    var column = keyCount == 8 && finish && duration < 1000
                        ? 0
                        : random.Next(lowerColumn, keyCount);
                    var spinnerEnd = duration < 100 ? hitObject.StartTime : hitObject.EndTime;
                    converted.Add(new HitObject(
                        columnCenterFromColumn(column, keyCount), 192, hitObject.StartTime,
                        spinnerEnd == hitObject.StartTime ? HitObjectKind.Circle : HitObjectKind.Hold,
                        hitObject.NewCombo, hitObject.ComboOffset, hitObject.HitSound,
                        spinnerEnd, null, hitObject.HitSample));
                    break;
            }
        }

        return source with
        {
            General = source.General with { Mode = 3 },
            Difficulty = source.Difficulty with { CircleSize = keyCount },
            HitObjects = converted.OrderBy(hitObject => hitObject.StartTime).ToArray()
        };
    }

    private static HitObject asNote(HitObject source, int keyCount)
    {
        var x = columnCenter(source.X, keyCount);
        return source with { X = x, Kind = HitObjectKind.Circle, EndTime = source.StartTime, Slider = null };
    }

    private static HitObject asHold(HitObject source, int endTime, int keyCount)
    {
        var x = columnCenter(source.X, keyCount);
        return source with { X = x, Kind = HitObjectKind.Hold, EndTime = Math.Max(source.StartTime + 1, endTime), Slider = null };
    }

    private static int columnCenter(int x, int keyCount)
    {
        var column = ManiaSession.ColumnFor(x, keyCount);
        return columnCenterFromColumn(column, keyCount);
    }

    private static int columnCenterFromColumn(int column, int keyCount) =>
        (int)((column + 0.5) * 512 / keyCount);

    // Direct port of HitObjectManagerMania's conversion RNG seed.
    private static int conversionSeed(Beatmap beatmap) =>
        (int)Math.Round(beatmap.Difficulty.HpDrainRate + beatmap.Difficulty.CircleSize) * 20
        + (int)(beatmap.Difficulty.OverallDifficulty * 41.2)
        + (int)Math.Round(beatmap.Difficulty.ApproachRate);
}
