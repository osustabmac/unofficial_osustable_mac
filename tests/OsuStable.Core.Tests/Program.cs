using System.Text;
using OsuStable.Core.Beatmaps;
using OsuStable.Core.Gameplay;

const string beatmapText = """
osu file format v14

[General]
AudioFilename: audio.mp3
PreviewTime: 12000
Mode: 0

[Metadata]
Title:Parser Test
Artist:Codex
Creator:ppy
Version:Insane
Tags:test native mac
BeatmapID:42
BeatmapSetID:7

[Difficulty]
HPDrainRate:6
CircleSize:4
OverallDifficulty:8
ApproachRate:9
SliderMultiplier:1.6
SliderTickRate:2

[TimingPoints]
0,500,4,2,1,80,1,0
10000,-50,4,2,1,70,0,1

[HitObjects]
256,192,1000,1,0,0:0:0:0:
100,200,2000,6,2,B|200:200|300:100,2,240,0|2|0,0:0|0:0|0:0,0:0:0:0:
256,192,5000,12,0,6500,0:0:0:0:
64,192,7000,128,0,7500:0:0:0:0:
""";

using var stream = new MemoryStream(Encoding.UTF8.GetBytes(beatmapText));
var beatmap = BeatmapParser.Parse(stream, "fixture.osu");

assert(beatmap.General.AudioFilename == "audio.mp3", "audio filename");
assert(beatmap.Metadata.Title == "Parser Test", "title");
assert(beatmap.Metadata.Tags.Count == 3, "tags");
assert(beatmap.Difficulty.ApproachRate == 9, "approach rate");
assert(beatmap.TimingPoints.Count == 2 && !beatmap.TimingPoints[1].Uninherited, "timing points");
assert(beatmap.HitObjects.Count == 4, "hit object count");
assert(beatmap.HitObjects[0].Kind == HitObjectKind.Circle, "circle");
assert(beatmap.HitObjects[1].Kind == HitObjectKind.Slider, "slider");
assert(beatmap.HitObjects[1].NewCombo, "new combo flag");
assert(beatmap.HitObjects[1].Slider?.ControlPoints.Count == 2, "slider points");
assert(beatmap.HitObjects[2].EndTime == 6500, "spinner end");
assert(beatmap.HitObjects[3].Kind == HitObjectKind.Hold && beatmap.HitObjects[3].EndTime == 7500, "hold");
var timing = new TimingResolver(beatmap);
assert(Math.Abs(timing.At(2000).BeatLength - 500) < 0.001, "base beat length");
assert(Math.Abs(timing.SliderDuration(beatmap.HitObjects[1]) - 1500) < 0.001, "slider duration");
assert(Math.Abs(timing.At(11000).SliderVelocity - 2) < 0.001, "inherited slider velocity");
var sliderPath = new SliderPath(beatmap.HitObjects[1]);
assert(Math.Abs(sliderPath.Length - 240) < 0.01, "slider path length");
assert(PathPoint.Distance(sliderPath.PositionAt(0), new PathPoint(100, 200)) < 0.01, "slider path start");
assert(PathPoint.Distance(sliderPath.PositionAt(1), sliderPath.Points[^1]) < 0.01, "slider path end");
assert(Math.Abs(StableDifficulty.HitWindow300(8) - 32) < 0.001, "OD hit window");
assert(Math.Abs(StableDifficulty.ApproachPreempt(9) - 600) < 0.001, "AR preempt");
assert(Math.Abs(StableDifficulty.CircleRadius(4) - 36.48) < 0.001, "CS radius");

var windows = OsuStable.Core.Gameplay.HitWindows.FromOverallDifficulty(8);
assert(windows.ResultFor(31) == OsuStable.Core.Gameplay.HitResult.Great, "great window");
assert(windows.ResultFor(50) == OsuStable.Core.Gameplay.HitResult.Ok, "ok window");
assert(windows.ResultFor(100) == OsuStable.Core.Gameplay.HitResult.Meh, "meh window");

var session = new OsuStable.Core.Gameplay.GameplaySession(beatmap);
assert(session.TryHit(256, 192, 1010) == OsuStable.Core.Gameplay.HitResult.Great, "circle hit");
assert(session.Combo == 1 && session.MaxCombo == 1, "combo increment");
assert(session.Score.Score == 300 && Math.Abs(session.Score.Accuracy - 1) < 0.001, "score and accuracy");
session.Update(10000);
assert(session.MissCount == 0, "non-circle objects are not falsely missed");

using var replayStream = new MemoryStream(Encoding.UTF8.GetBytes(beatmapText));
var replayBeatmap = BeatmapParser.Parse(replayStream, "fixture.osu");
var replaySession = new GameplaySession(replayBeatmap);
assert(replaySession.Process(new InputFrame(990, 256, 192, InputButtons.Key1)) == HitResult.Great, "input edge hit");
assert(replaySession.Process(new InputFrame(995, 256, 192, InputButtons.Key1)) == HitResult.None, "held key does not retrigger");

const string maniaText = """
osu file format v14

[General]
AudioFilename: audio.mp3
Mode: 3

[Metadata]
Title:Mania Test
Artist:Codex
Creator:Codex
Version:4K

[Difficulty]
HPDrainRate:5
CircleSize:4
OverallDifficulty:8
ApproachRate:5
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
0,500,4,2,1,80,1,0

[HitObjects]
64,192,1000,1,0,0:0:0:0:
448,192,2000,128,0,3000:0:0:0:0:
""";

using var maniaStream = new MemoryStream(Encoding.UTF8.GetBytes(maniaText));
var maniaMap = BeatmapParser.Parse(maniaStream, "mania.osu");
var maniaWindows = ManiaHitWindows.FromOverallDifficulty(8);
assert(maniaWindows.Perfect == 16 && maniaWindows.Great == 40 && maniaWindows.Good == 73, "mania hit windows");
var convertedWindows = ManiaHitWindows.FromOverallDifficulty(8, nativeManiaMap: false);
assert(convertedWindows == new ManiaHitWindows(16, 34, 67, 97, 121, 158), "converted mania high-OD windows");
var lowOdConvertedWindows = ManiaHitWindows.FromOverallDifficulty(4, nativeManiaMap: false);
assert(lowOdConvertedWindows == new ManiaHitWindows(16, 47, 77, 97, 121, 158), "converted mania low-OD windows");
var hardRockWindows = ManiaHitWindows.FromOverallDifficulty(8, mods: StableManiaMods.HardRock);
assert(hardRockWindows.Great == 28 && hardRockWindows.Miss == 117, "stable mania mod window truncation");
var stableSkin = new StableManiaSkinSettings(4);
assert(stableSkin.ColumnWidth.SequenceEqual(new[] { 30d, 30d, 30d, 30d }), "stable mania per-column widths");
assert(stableSkin.ColumnLineWidth.Length == 5 && stableSkin.PlayfieldWidth() == 120, "stable mania stage defaults");
stableSkin.ColumnWidth[1] = 2;
stableSkin.ColumnSpacing[0] = -100;
stableSkin.HitPosition = 999;
stableSkin.LightFramesPerSecond = 0;
stableSkin.Normalise();
assert(stableSkin.ColumnWidth[1] == 5 && stableSkin.ColumnSpacing[0] == -5, "stable mania skin clamps");
assert(stableSkin.HitPosition == 480 && stableSkin.LightFramesPerSecond == 24, "stable mania position/fps clamps");
var stableRandomA = new StableFastRandom(1337);
var stableRandomB = new StableFastRandom(1337);
assert(stableRandomA.X == 1337 && stableRandomA.Y == 842502087 && stableRandomA.Z == 3579807591 && stableRandomA.W == 273326509, "stable random seed state");
assert(stableRandomA.NextUInt() == 274941776 && stableRandomA.NextUInt() == 2661595948, "stable random reference vector");
stableRandomA.Reinitialise(1337);
for (var i = 0; i < 128; i++)
    assert(stableRandomA.NextUInt() == stableRandomB.NextUInt(), "stable random deterministic sequence");
stableRandomA.Reinitialise(1337);
var stableBytes = new byte[7];
stableRandomA.NextBytes(stableBytes);
assert(stableBytes.Any(value => value != 0), "stable random byte generation");
assert(maniaWindows.ResultForPress(-120) == ManiaHitResult.Meh, "early mania meh");
assert(maniaWindows.ResultForPress(-150) == ManiaHitResult.Miss, "early mania miss");
assert(maniaWindows.ResultForPress(120) == ManiaHitResult.None, "late mania meh is impossible");
var mania = new ManiaSession(maniaMap);
assert(mania.KeyCount == 4 && mania.Objects[0].Column == 0 && mania.Objects[1].Column == 3, "mania columns");
assert(mania.SetKey(0, true, 1000) == ManiaHitResult.Perfect, "mania note perfect");
assert(mania.SetKey(0, false, 1001) == ManiaHitResult.None, "mania note release");
assert(mania.SetKey(3, true, 2005) == ManiaHitResult.None, "mania hold head has no separate judgement");
assert(mania.SetKey(3, false, 2995) == ManiaHitResult.Perfect, "mania hold tail");
assert(mania.Score.PerfectCount == 2 && mania.Score.Combo == 2, "mania score counts");
assert(mania.Score.Score == 1_000_000 && Math.Abs(mania.Score.Accuracy - 1) < 0.001, "mania perfect score");
assert(mania.Complete, "mania completion");

using var brokenHoldStream = new MemoryStream(Encoding.UTF8.GetBytes(maniaText));
var brokenHold = new ManiaSession(BeatmapParser.Parse(brokenHoldStream, "broken-hold.osu"));
brokenHold.Advance(1200);
assert(brokenHold.Score.MissCount == 1, "mania automatic miss");
assert(brokenHold.SetKey(3, true, 2000) == ManiaHitResult.None, "broken hold head");
assert(brokenHold.SetKey(3, false, 2200) == ManiaHitResult.None, "early hold release");
assert(brokenHold.SetKey(3, true, 2500) == ManiaHitResult.None, "hold regrab");
assert(brokenHold.SetKey(3, false, 3000) == ManiaHitResult.Good, "broken hold capped at good");
assert(brokenHold.Score.GoodCount == 1 && brokenHold.Score.Combo == 1, "broken hold scoring");

var stableBonus = new ManiaScoreState(101);
stableBonus.Apply(ManiaHitResult.Miss, 5);
assert(stableBonus.Bonus == 0, "stable mania miss clears bonus through clamp");
for (var i = 1; i < 384; i++) stableBonus.Apply(ManiaHitResult.Miss, 5);
assert(stableBonus.Bonus == 100, "stable mania bonus resets every 384 judgements");

using var convertStream = new MemoryStream(Encoding.UTF8.GetBytes(beatmapText));
var converted = ManiaBeatmapConverter.Convert(BeatmapParser.Parse(convertStream, "standard.osu"));
assert(converted.General.Mode == 3 && converted.Difficulty.CircleSize == 4, "standard to mania conversion");
assert(converted.HitObjects.Any(hitObject => hitObject.Kind == HitObjectKind.Hold), "slider to mania hold conversion");
assert(converted.HitObjects.Any(hitObject => hitObject.Kind == HitObjectKind.Hold && hitObject.StartTime == 5000 && hitObject.EndTime == 6500), "stable spinner to single hold conversion");
var maniaVerification = ManiaAutoplayVerifier.Verify(BeatmapParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(beatmapText)), "autoplay.osu"));
assert(maniaVerification.Complete && maniaVerification.Misses == 0, "4K autoplay verification completion");
assert(maniaVerification.Judged == maniaVerification.Objects && maniaVerification.Accuracy == 1, "4K autoplay perfect judgement");

var temporaryRoot = Path.Combine(Path.GetTempPath(), $"osustabwin-core-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
try
{
    var validPath = Path.Combine(temporaryRoot, "valid.osu");
    var invalidPath = Path.Combine(temporaryRoot, "invalid.osu");
    File.WriteAllText(validPath, beatmapText);
    File.WriteAllText(invalidPath, "not a beatmap");
    var library = BeatmapLibrary.Scan(temporaryRoot);
    assert(library.Beatmaps.Count == 1, "library valid map count");
    assert(library.Failures.Count == 1, "library failure count");
    assert(BeatmapLibrary.ResolveAudioPath(library.Beatmaps[0]) == Path.Combine(temporaryRoot, "audio.mp3"), "audio path");

    var escapingText = beatmapText.Replace("AudioFilename: audio.mp3", "AudioFilename: ../escape.mp3");
    using var escapingStream = new MemoryStream(Encoding.UTF8.GetBytes(escapingText));
    var escapingMap = BeatmapParser.Parse(escapingStream, Path.Combine(temporaryRoot, "escape.osu"));
    var escaped = false;
    try
    {
        BeatmapLibrary.ResolveAudioPath(escapingMap);
    }
    catch (InvalidDataException)
    {
        escaped = true;
    }
    assert(escaped, "audio traversal rejection");

    var archivePath = Path.Combine(temporaryRoot, "import.osz");
    using (var archive = System.IO.Compression.ZipFile.Open(archivePath, System.IO.Compression.ZipArchiveMode.Create))
    {
        var entry = archive.CreateEntry("Map/test.osu");
        using var writer = new StreamWriter(entry.Open());
        writer.Write(maniaText);
    }
    var importedDirectory = BeatmapImporter.ImportOsz(archivePath, Path.Combine(temporaryRoot, "Songs"));
    assert(File.Exists(Path.Combine(importedDirectory, "Map", "test.osu")), "osz import");
}
finally
{
    Directory.Delete(temporaryRoot, recursive: true);
}

Console.WriteLine("OsuStable.Core tests passed");
return 0;

static void assert(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException($"Assertion failed: {name}");
}
