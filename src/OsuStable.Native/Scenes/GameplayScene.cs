using OsuStable.Core.Beatmaps;
using OsuStable.Core.Gameplay;
using OsuStable.Native.Audio;
using SDL;
using static SDL.SDL3;
using static SDL.SDL3_image;

namespace OsuStable.Native.Scenes;

internal sealed unsafe class GameplayScene : IDisposable
{
    private readonly SDL_Renderer* renderer;
    private readonly Beatmap beatmap;
    private readonly GameplaySession session;
    private readonly TimingResolver timingResolver;
    private readonly Dictionary<HitObject, SliderPath> sliderPaths;
    private readonly nint hitCircle;
    private readonly nint approachCircle;
    private readonly BeatmapAudioPlayer? audioPlayer;
    private ulong startedAt;
    private double pointerX = 256;
    private double pointerY = 192;

    public GameplayScene(SDL_Renderer* renderer, string resourceRoot, Beatmap beatmap)
    {
        this.renderer = renderer;
        this.beatmap = beatmap;
        session = new GameplaySession(beatmap);
        timingResolver = new TimingResolver(beatmap);
        sliderPaths = beatmap.HitObjects
            .Where(hitObject => hitObject.Kind == HitObjectKind.Slider && hitObject.Slider != null)
            .ToDictionary(hitObject => hitObject, hitObject => new SliderPath(hitObject));
        var gameplay = Path.Combine(resourceRoot, "gameplay");
        hitCircle = load(Path.Combine(gameplay, "hitcircle@2x.png"));
        approachCircle = load(Path.Combine(gameplay, "approachcircle@2x.png"));

        try
        {
            audioPlayer = new BeatmapAudioPlayer(BeatmapLibrary.ResolveAudioPath(beatmap));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Audio disabled: {exception.Message}");
        }
    }

    public void Start()
    {
        startedAt = SDL_GetTicksNS();
        audioPlayer?.Start();
    }

    public void PointerMoved(float windowX, float windowY, int outputWidth, int outputHeight)
    {
        var playfield = getPlayfield(outputWidth, outputHeight);
        pointerX = Math.Clamp((windowX - playfield.x) / playfield.w * 512, 0, 512);
        pointerY = Math.Clamp((windowY - playfield.y) / playfield.h * 384, 0, 384);
    }

    public void Hit() => session.TryHit(pointerX, pointerY, elapsedMilliseconds());

    public void Render(int outputWidth, int outputHeight)
    {
        var time = elapsedMilliseconds();
        session.Update(time);

        SDL_SetRenderDrawColor(renderer, 12, 12, 18, 255);
        SDL_RenderClear(renderer);

        var playfield = getPlayfield(outputWidth, outputHeight);
        SDL_SetRenderDrawColor(renderer, 25, 25, 34, 255);
        SDL_RenderFillRect(renderer, &playfield);

        var radius = (float)StableDifficulty.CircleRadius(beatmap.Difficulty.CircleSize);
        var preempt = StableDifficulty.ApproachPreempt(beatmap.Difficulty.ApproachRate);

        foreach (var state in session.VisibleObjects.Reverse())
        {
            if (state.HitObject.Kind == HitObjectKind.Slider && sliderPaths.TryGetValue(state.HitObject, out var path))
                renderSliderTrack(path, playfield);
        }

        foreach (var state in session.VisibleObjects.Reverse())
        {
            var hitObject = state.HitObject;
            if (hitObject.Kind is not (HitObjectKind.Circle or HitObjectKind.Slider))
                continue;

            var centerX = playfield.x + hitObject.X / 512f * playfield.w;
            var centerY = playfield.y + hitObject.Y / 384f * playfield.h;
            var diameter = radius * 2 * playfield.w / 512f;
            var remaining = Math.Clamp((hitObject.StartTime - time) / preempt, 0, 1);
            var approachDiameter = diameter * (float)(1 + remaining * 3);

            var approachRect = centered(centerX, centerY, approachDiameter);
            var circleRect = centered(centerX, centerY, diameter);
            render(approachCircle, approachRect);
            render(hitCircle, circleRect);

            if (hitObject.Kind == HitObjectKind.Slider &&
                sliderPaths.TryGetValue(hitObject, out var path) &&
                time >= hitObject.StartTime)
            {
                var duration = timingResolver.SliderDuration(hitObject);
                var repeats = Math.Max(1, hitObject.Slider!.RepeatCount);
                var overallProgress = duration <= 0 ? 1 : Math.Clamp((time - hitObject.StartTime) / duration, 0, 1);
                var spanProgress = overallProgress * repeats;
                var span = Math.Min(repeats - 1, (int)spanProgress);
                var progress = spanProgress - span;
                if ((span & 1) != 0)
                    progress = 1 - progress;
                var ball = path.PositionAt(progress);
                var ballRect = centered(
                    playfield.x + (float)ball.X / 512 * playfield.w,
                    playfield.y + (float)ball.Y / 384 * playfield.h,
                    diameter);
                render(hitCircle, ballRect);
            }
        }

        var cursorDiameter = MathF.Max(8, playfield.w / 512f * 10);
        SDL_SetRenderDrawColor(renderer, 255, 102, 171, 220);
        var cursor = centered(
            playfield.x + (float)pointerX / 512 * playfield.w,
            playfield.y + (float)pointerY / 384 * playfield.h,
            cursorDiameter);
        SDL_RenderFillRect(renderer, &cursor);
    }

    private double elapsedMilliseconds() =>
        audioPlayer?.PositionMilliseconds ?? (SDL_GetTicksNS() - startedAt) / 1_000_000.0;

    private static SDL_FRect getPlayfield(int outputWidth, int outputHeight)
    {
        var width = MathF.Min(outputWidth * 0.92f, outputHeight * 0.92f * 4 / 3);
        var height = width * 3 / 4;
        return new SDL_FRect
        {
            x = (outputWidth - width) / 2,
            y = (outputHeight - height) / 2,
            w = width,
            h = height
        };
    }

    private static SDL_FRect centered(float x, float y, float size) => new()
    {
        x = x - size / 2,
        y = y - size / 2,
        w = size,
        h = size
    };

    private void render(nint handle, SDL_FRect destination)
    {
        var texture = (SDL_Texture*)handle;
        if (texture != null)
            SDL_RenderTexture(renderer, texture, null, &destination);
    }

    private void renderSliderTrack(SliderPath path, SDL_FRect playfield)
    {
        if (path.Points.Count < 2)
            return;

        var scale = playfield.w / 512f;
        var thickness = Math.Max(2, (int)MathF.Round(scale * 12));
        for (var offset = -thickness / 2; offset <= thickness / 2; offset++)
        {
            var edge = Math.Abs(offset) >= thickness / 2;
            SDL_SetRenderDrawColor(renderer,
                edge ? (byte)238 : (byte)78,
                edge ? (byte)238 : (byte)78,
                edge ? (byte)245 : (byte)92,
                210);
            for (var i = 1; i < path.Points.Count; i++)
            {
                var a = path.Points[i - 1];
                var b = path.Points[i];
                SDL_RenderLine(renderer,
                    playfield.x + (float)a.X / 512 * playfield.w,
                    playfield.y + (float)a.Y / 384 * playfield.h + offset,
                    playfield.x + (float)b.X / 512 * playfield.w,
                    playfield.y + (float)b.Y / 384 * playfield.h + offset);
            }
        }
    }

    private nint load(string path)
    {
        var texture = IMG_LoadTexture(renderer, path);
        if (texture == null)
            Console.Error.WriteLine($"Failed to load gameplay texture '{path}': {SDL_GetError()}");
        return (nint)texture;
    }

    public void Dispose()
    {
        destroy(hitCircle);
        destroy(approachCircle);
        audioPlayer?.Dispose();
    }

    private static void destroy(nint handle)
    {
        var texture = (SDL_Texture*)handle;
        if (texture != null)
            SDL_DestroyTexture(texture);
    }
}
