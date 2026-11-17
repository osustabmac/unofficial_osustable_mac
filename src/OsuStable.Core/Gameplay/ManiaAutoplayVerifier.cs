using OsuStable.Core.Beatmaps;

namespace OsuStable.Core.Gameplay;

public sealed record ManiaVerificationResult(
    int Objects,
    int Judged,
    int Misses,
    int Score,
    double Accuracy,
    bool Complete);

public static class ManiaAutoplayVerifier
{
    public static ManiaVerificationResult Verify(Beatmap source)
    {
        var converted = ManiaBeatmapConverter.Convert(source, 4);
        var session = new ManiaSession(converted, convertedFromStandard: source.General.Mode != 3);
        var events = new List<(double Time, int Column, bool Pressed)>();
        foreach (var state in session.Objects)
        {
            events.Add((state.HitObject.StartTime, state.Column, true));
            events.Add((state.IsHold ? state.EndTime : state.HitObject.StartTime + 1, state.Column, false));
        }

        // A tail and the next head at the same timestamp must release first.
        foreach (var input in events.OrderBy(item => item.Time).ThenBy(item => item.Pressed))
            session.SetKey(input.Column, input.Pressed, input.Time);

        if (events.Count > 0)
            session.Advance(events.Max(item => item.Time) + 1000);
        return new ManiaVerificationResult(
            session.Objects.Count,
            session.Score.TotalJudged,
            session.Score.MissCount,
            session.Score.Score,
            session.Score.Accuracy,
            session.Complete);
    }
}
