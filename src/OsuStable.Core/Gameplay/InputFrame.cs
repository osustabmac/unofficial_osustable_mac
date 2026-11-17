namespace OsuStable.Core.Gameplay;

[Flags]
public enum InputButtons
{
    None = 0,
    MouseLeft = 1,
    MouseRight = 2,
    Key1 = 4,
    Key2 = 8
}

public readonly record struct InputFrame(double Time, double X, double Y, InputButtons Buttons);

