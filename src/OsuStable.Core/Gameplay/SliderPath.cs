using OsuStable.Core.Beatmaps;

namespace OsuStable.Core.Gameplay;

public sealed class SliderPath
{
    private readonly PathPoint[] points;
    private readonly double[] cumulativeLength;

    public SliderPath(HitObject hitObject)
    {
        if (hitObject.Kind != HitObjectKind.Slider || hitObject.Slider == null)
            throw new ArgumentException("Hit object is not a slider.", nameof(hitObject));

        var controlPoints = new List<PathPoint> { new(hitObject.X, hitObject.Y) };
        controlPoints.AddRange(hitObject.Slider.ControlPoints.Select(point => new PathPoint(point.X, point.Y)));
        var raw = hitObject.Slider.CurveType switch
        {
            'L' => controlPoints,
            'C' => catmull(controlPoints),
            'P' when controlPoints.Count == 3 => perfectCircle(controlPoints),
            _ => bezier(controlPoints)
        };

        points = normalizeLength(raw, hitObject.Slider.PixelLength).ToArray();
        cumulativeLength = new double[points.Length];
        for (var i = 1; i < points.Length; i++)
            cumulativeLength[i] = cumulativeLength[i - 1] + PathPoint.Distance(points[i - 1], points[i]);
    }

    public IReadOnlyList<PathPoint> Points => points;
    public double Length => cumulativeLength.Length == 0 ? 0 : cumulativeLength[^1];

    public PathPoint PositionAt(double progress)
    {
        if (points.Length == 0)
            return default;
        if (points.Length == 1)
            return points[0];

        var distance = Math.Clamp(progress, 0, 1) * Length;
        var index = Array.BinarySearch(cumulativeLength, distance);
        if (index >= 0)
            return points[index];
        index = ~index;
        if (index <= 0)
            return points[0];
        if (index >= points.Length)
            return points[^1];

        var startDistance = cumulativeLength[index - 1];
        var segmentLength = cumulativeLength[index] - startDistance;
        var t = segmentLength <= 0 ? 0 : (distance - startDistance) / segmentLength;
        return PathPoint.Lerp(points[index - 1], points[index], t);
    }

    private static List<PathPoint> bezier(IReadOnlyList<PathPoint> source)
    {
        var output = new List<PathPoint>();
        var segment = new List<PathPoint>();
        for (var i = 0; i < source.Count; i++)
        {
            segment.Add(source[i]);
            var split = i < source.Count - 1 && source[i] == source[i + 1];
            if (split || i == source.Count - 1)
            {
                appendBezier(output, segment);
                segment.Clear();
            }
        }
        return output;
    }

    private static void appendBezier(List<PathPoint> output, IReadOnlyList<PathPoint> controlPoints)
    {
        if (controlPoints.Count == 0)
            return;
        var subdivisions = Math.Max(25, controlPoints.Count * 25);
        for (var step = output.Count == 0 ? 0 : 1; step <= subdivisions; step++)
        {
            var working = controlPoints.ToArray();
            var t = step / (double)subdivisions;
            for (var level = working.Length - 1; level > 0; level--)
                for (var i = 0; i < level; i++)
                    working[i] = PathPoint.Lerp(working[i], working[i + 1], t);
            output.Add(working[0]);
        }
    }

    private static List<PathPoint> catmull(IReadOnlyList<PathPoint> source)
    {
        if (source.Count < 2)
            return source.ToList();
        var output = new List<PathPoint> { source[0] };
        for (var i = 0; i < source.Count - 1; i++)
        {
            var p0 = source[Math.Max(0, i - 1)];
            var p1 = source[i];
            var p2 = source[i + 1];
            var p3 = source[Math.Min(source.Count - 1, i + 2)];
            for (var step = 1; step <= 50; step++)
            {
                var t = step / 50.0;
                var t2 = t * t;
                var t3 = t2 * t;
                output.Add(new PathPoint(
                    0.5 * ((2 * p1.X) + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3),
                    0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3)));
            }
        }
        return output;
    }

    private static List<PathPoint> perfectCircle(IReadOnlyList<PathPoint> source)
    {
        var a = source[0];
        var b = source[1];
        var c = source[2];
        var determinant = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        if (Math.Abs(determinant) < 0.001)
            return source.ToList();

        var a2 = a.X * a.X + a.Y * a.Y;
        var b2 = b.X * b.X + b.Y * b.Y;
        var c2 = c.X * c.X + c.Y * c.Y;
        var center = new PathPoint(
            (a2 * (b.Y - c.Y) + b2 * (c.Y - a.Y) + c2 * (a.Y - b.Y)) / determinant,
            (a2 * (c.X - b.X) + b2 * (a.X - c.X) + c2 * (b.X - a.X)) / determinant);
        var start = Math.Atan2(a.Y - center.Y, a.X - center.X);
        var middle = Math.Atan2(b.Y - center.Y, b.X - center.X);
        var end = Math.Atan2(c.Y - center.Y, c.X - center.X);
        var clockwise = !angleContains(start, end, middle);
        var sweep = normalizedSweep(start, end, clockwise);
        var radius = PathPoint.Distance(center, a);
        var subdivisions = Math.Max(25, (int)(Math.Abs(sweep) * radius / 4));
        return Enumerable.Range(0, subdivisions + 1)
            .Select(i =>
            {
                var angle = start + sweep * i / subdivisions;
                return new PathPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
            })
            .ToList();
    }

    private static bool angleContains(double start, double end, double value)
    {
        var total = positiveAngle(end - start);
        var partial = positiveAngle(value - start);
        return partial <= total;
    }

    private static double normalizedSweep(double start, double end, bool clockwise)
    {
        var positive = positiveAngle(end - start);
        return clockwise ? positive - Math.PI * 2 : positive;
    }

    private static double positiveAngle(double angle)
    {
        var result = angle % (Math.PI * 2);
        return result < 0 ? result + Math.PI * 2 : result;
    }

    private static List<PathPoint> normalizeLength(IReadOnlyList<PathPoint> source, double targetLength)
    {
        if (source.Count == 0)
            return [];
        var output = new List<PathPoint> { source[0] };
        var remaining = Math.Max(0, targetLength);
        for (var i = 1; i < source.Count && remaining > 0; i++)
        {
            var segmentLength = PathPoint.Distance(source[i - 1], source[i]);
            if (segmentLength <= remaining)
            {
                output.Add(source[i]);
                remaining -= segmentLength;
            }
            else
            {
                output.Add(PathPoint.Lerp(source[i - 1], source[i], remaining / segmentLength));
                remaining = 0;
            }
        }

        if (remaining > 0 && output.Count >= 2)
        {
            var last = output[^1];
            var previous = output[^2];
            var length = PathPoint.Distance(previous, last);
            if (length > 0)
                output.Add(new PathPoint(last.X + (last.X - previous.X) / length * remaining, last.Y + (last.Y - previous.Y) / length * remaining));
        }
        return output;
    }
}

public readonly record struct PathPoint(double X, double Y)
{
    public static double Distance(PathPoint a, PathPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static PathPoint Lerp(PathPoint a, PathPoint b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
