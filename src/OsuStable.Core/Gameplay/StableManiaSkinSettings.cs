namespace OsuStable.Core.Gameplay;

// Direct semantic port of stable's SkinMania constructor defaults and value
// normalisation. Coordinates use stable's fixed 640x480 playfield.
public sealed class StableManiaSkinSettings
{
    public StableManiaSkinSettings(int columns)
    {
        Columns = Math.Max(1, columns);
        ColumnLineWidth = repeated(Columns + 1, 2);
        ColumnSpacing = repeated(Math.Max(0, Columns - 1), 0);
        ColumnWidth = repeated(Columns, 30);
        LightingNWidth = repeated(Columns, 0);
        LightingLWidth = repeated(Columns, 0);
    }

    public int Columns { get; }
    public double[] ColumnLineWidth { get; }
    public double[] ColumnSpacing { get; }
    public double[] ColumnWidth { get; }
    public double[] LightingNWidth { get; }
    public double[] LightingLWidth { get; }
    public double BarlineHeight { get; set; } = 1.2;
    public bool JudgementLine { get; set; } = true;
    public bool UpsideDown { get; set; }
    public bool SeparateScore { get; set; } = true;
    public bool KeysUnderNotes { get; set; }
    public bool? SplitStagesFromSkin { get; set; }
    public double ColumnStart { get; set; } = 136;
    public double ColumnRight { get; set; } = 19;
    public int HitPosition { get; set; } = 402;
    public int LightPosition { get; set; } = 413;
    public int ComboPosition { get; set; } = 111;
    public int ScorePosition { get; set; } = 325;
    public double StageSeparation { get; set; } = 40;
    public double WidthForNoteHeightScale { get; set; }
    public int LightFramesPerSecond { get; set; } = 60;

    public void Normalise()
    {
        for (var i = 0; i < ColumnLineWidth.Length; i++)
            if (ColumnLineWidth[i] > 0 && ColumnLineWidth[i] < 2)
                ColumnLineWidth[i] = 2;
        for (var i = 0; i < ColumnWidth.Length; i++)
            ColumnWidth[i] = Math.Clamp(ColumnWidth[i], 5, 100);
        for (var i = 0; i < ColumnSpacing.Length; i++)
            ColumnSpacing[i] = Math.Max(ColumnSpacing[i], -ColumnWidth[i + 1]);
        StageSeparation = Math.Max(StageSeparation, 5);
        HitPosition = Math.Clamp(HitPosition, 240, 480);
        if (LightFramesPerSecond <= 0)
            LightFramesPerSecond = 24;
    }

    public double ColumnX(int column)
    {
        var x = ColumnStart;
        for (var i = 0; i < column; i++)
            x += ColumnWidth[i] + ColumnSpacing[i];
        return x;
    }

    public double PlayfieldWidth() => ColumnWidth.Sum() + ColumnSpacing.Sum();

    private static double[] repeated(int count, double value) => Enumerable.Repeat(value, count).ToArray();
}
