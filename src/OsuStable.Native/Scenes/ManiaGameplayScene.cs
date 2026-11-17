using OsuStable.Core.Beatmaps;
using OsuStable.Core.Gameplay;
using OsuStable.Native.Audio;
using SDL;
using static SDL.SDL3;
using static SDL.SDL3_image;

namespace OsuStable.Native.Scenes;

internal sealed unsafe class ManiaGameplayScene : IDisposable
{
    private readonly SDL_Renderer* renderer;
    private readonly ManiaSession session;
    private readonly StableManiaSkinSettings skin;
    private readonly BeatmapAudioPlayer? audioPlayer;
    private readonly nint note1;
    private readonly nint note2;
    private readonly nint noteSpecial;
    private readonly nint noteHold;
    private readonly nint stageLeft;
    private readonly nint stageRight;
    private readonly nint stageLight;
    private readonly nint rankingPanel;
    private readonly nint rankingTitle;
    private readonly Dictionary<ManiaHitResult, nint> judgements;
    private readonly nint[] digits;
    private readonly nint scoreDot;
    private readonly nint scorePercent;
    private readonly nint scoreX;
    private ulong startedAt;
    private ManiaHitResult lastResult;
    private double resultAt;
    private readonly int universalOffset;

    public ManiaGameplayScene(SDL_Renderer* renderer, string resourceRoot, Beatmap source, StableConfiguration config)
    {
        this.renderer = renderer;
        universalOffset = config.Offset;
        var convertedFromStandard = source.General.Mode != 3;
        var beatmap = ManiaBeatmapConverter.Convert(source);
        session = new ManiaSession(beatmap, convertedFromStandard);
        skin = new StableManiaSkinSettings(session.KeyCount);
        var gameplay = Path.Combine(resourceRoot, "gameplay");
        note1 = load(Path.Combine(gameplay, "mania-note1@2x.png"));
        note2 = load(Path.Combine(gameplay, "mania-note2@2x.png"));
        noteSpecial = load(Path.Combine(gameplay, "mania-noteS@2x.png"));
        noteHold = load(Path.Combine(gameplay, "mania-note1L-0@2x.png"));
        stageLeft = load(Path.Combine(gameplay, "mania-stage-left@2x.png"));
        stageRight = load(Path.Combine(gameplay, "mania-stage-right@2x.png"));
        stageLight = load(Path.Combine(gameplay, "mania-stage-light@2x.png"));
        rankingPanel = load(Path.Combine(gameplay, "ranking-panel@2x.png"));
        rankingTitle = load(Path.Combine(gameplay, "ranking-title@2x.png"));
        judgements = new Dictionary<ManiaHitResult, nint>
        {
            [ManiaHitResult.Perfect] = load(Path.Combine(gameplay, "mania-hit300g-0@2x.png")),
            [ManiaHitResult.Great] = load(Path.Combine(gameplay, "mania-hit300@2x.png")),
            [ManiaHitResult.Good] = load(Path.Combine(gameplay, "mania-hit200@2x.png")),
            [ManiaHitResult.Ok] = load(Path.Combine(gameplay, "mania-hit100@2x.png")),
            [ManiaHitResult.Meh] = load(Path.Combine(gameplay, "mania-hit50@2x.png")),
            [ManiaHitResult.Miss] = load(Path.Combine(gameplay, "mania-hit0@2x.png"))
        };
        digits = Enumerable.Range(0, 10)
            .Select(number => load(Path.Combine(gameplay, $"score-{number}@2x.png")))
            .ToArray();
        scoreDot = load(Path.Combine(gameplay, "score-dot@2x.png"));
        scorePercent = load(Path.Combine(gameplay, "score-percent@2x.png"));
        scoreX = load(Path.Combine(gameplay, "score-x@2x.png"));

        try
        {
            audioPlayer = new BeatmapAudioPlayer(BeatmapLibrary.ResolveAudioPath(beatmap), config.MusicGain);
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

    public void Pause() => audioPlayer?.Pause();
    public void Resume() => audioPlayer?.Resume();

    public void SetKey(int column, bool pressed)
    {
        var result = session.SetKey(column, pressed, elapsedMilliseconds());
        if (result != ManiaHitResult.None)
        {
            lastResult = result;
            resultAt = session.CurrentTime;
        }
    }

    public int KeyCount => session.KeyCount;
    public bool Finished => session.Complete || session.Score.Failed;

    public void Render(int outputWidth, int outputHeight)
    {
        var time = elapsedMilliseconds();
        session.Advance(time);
        SDL_SetRenderDrawColor(renderer, 8, 8, 14, 255);
        SDL_RenderClear(renderer);

        // stable lays mania out in a 640x480 coordinate space. Preserve those
        // coordinates and uniformly scale/centre them for the native window.
        var playfieldScale = MathF.Min(outputWidth / 640f, outputHeight / 480f);
        var viewportX = (outputWidth - 640f * playfieldScale) / 2;
        var viewportY = (outputHeight - 480f * playfieldScale) / 2;
        var stageX = viewportX + (float)skin.ColumnStart * playfieldScale;
        var stageY = viewportY;
        var stageHeight = 480f * playfieldScale;
        var stageWidth = (float)skin.PlayfieldWidth() * playfieldScale;
        var hitY = viewportY + skin.HitPosition * playfieldScale;
        var columnWidth = (float)skin.ColumnWidth[0] * playfieldScale;

        SDL_SetRenderDrawColor(renderer, 15, 15, 24, 255);
        var stage = new SDL_FRect { x = stageX, y = stageY, w = stageWidth, h = stageHeight };
        SDL_RenderFillRect(renderer, &stage);

        for (var column = 0; column < session.KeyCount; column++)
        {
            if (session.Keys[column])
            {
                SDL_SetRenderDrawColor(renderer, 255, 105, 180, 70);
                var light = new SDL_FRect { x = viewportX + (float)skin.ColumnX(column) * playfieldScale, y = stageY, w = (float)skin.ColumnWidth[column] * playfieldScale, h = hitY - stageY };
                SDL_RenderFillRect(renderer, &light);
            }
            SDL_SetRenderDrawColor(renderer, 70, 70, 88, 210);
            var lineX = viewportX + (float)skin.ColumnX(column) * playfieldScale;
            for (var line = 0; line < Math.Max(1, (int)(skin.ColumnLineWidth[column] * playfieldScale)); line++)
                SDL_RenderLine(renderer, lineX + line, stageY, lineX + line, stageY + stageHeight);
        }
        SDL_RenderLine(renderer, stageX + stageWidth, stageY, stageX + stageWidth, stageY + stageHeight);

        var pixelsPerMillisecond = 0.365f * playfieldScale;
        foreach (var state in session.VisibleObjects().Reverse())
        {
            var x = viewportX + (float)skin.ColumnX(state.Column) * playfieldScale + 2;
            var noteWidth = (float)skin.ColumnWidth[state.Column] * playfieldScale - 4;
            var headY = hitY - (float)(state.HitObject.StartTime - time) * pixelsPerMillisecond;
            if (state.IsHold)
            {
                var tailY = hitY - (float)(state.EndTime - time) * pixelsPerMillisecond;
                var bodyTop = MathF.Max(stageY, MathF.Min(headY, tailY));
                var bodyBottom = MathF.Min(hitY, MathF.Max(headY, tailY));
                if (bodyBottom > bodyTop)
                {
                    var body = new SDL_FRect { x = x + noteWidth * 0.22f, y = bodyTop, w = noteWidth * 0.56f, h = bodyBottom - bodyTop };
                    render(noteHold, body);
                }
            }
            if (!state.HeadHit && headY >= stageY - columnWidth && headY <= hitY + columnWidth)
            {
                var note = new SDL_FRect { x = x, y = headY - columnWidth * 0.22f, w = noteWidth, h = columnWidth * 0.44f };
                render(noteTexture(state.Column), note);
            }
        }

        SDL_SetRenderDrawColor(renderer, 255, 255, 255, 235);
        if (skin.JudgementLine)
        {
            var receptor = new SDL_FRect { x = stageX, y = hitY, w = stageWidth, h = MathF.Max(1, (float)(skin.BarlineHeight * playfieldScale)) };
            SDL_RenderFillRect(renderer, &receptor);
        }
        render(stageLeft, new SDL_FRect { x = stageX - 20, y = stageY, w = 20, h = stageHeight });
        render(stageRight, new SDL_FRect { x = stageX + stageWidth, y = stageY, w = 20, h = stageHeight });

        var hpX = viewportX + 12 * playfieldScale;
        var hpY = viewportY + 12 * playfieldScale;
        var hpWidth = 180 * playfieldScale;
        var hpHeight = MathF.Max(8, 8 * playfieldScale);
        SDL_SetRenderDrawColor(renderer, 55, 55, 65, 255);
        var hpBack = new SDL_FRect { x = hpX, y = hpY, w = hpWidth, h = hpHeight };
        SDL_RenderFillRect(renderer, &hpBack);
        SDL_SetRenderDrawColor(renderer, 255, 93, 170, 255);
        var hp = new SDL_FRect { x = hpX, y = hpY, w = hpWidth * (float)session.Score.Health, h = hpHeight };
        SDL_RenderFillRect(renderer, &hp);
        SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
        SDL_SetRenderScale(renderer, playfieldScale, playfieldScale);
        SDL_RenderDebugText(renderer, hpX / playfieldScale, (hpY + hpHeight + 4) / playfieldScale, "HP");
        SDL_SetRenderScale(renderer, 1, 1);
        var hudDigitHeight = 24 * playfieldScale;
        renderNumber(session.Score.Score, viewportX + 628 * playfieldScale, viewportY + 10 * playfieldScale, hudDigitHeight, rightAligned: true);
        renderScoreText($"{session.Score.Accuracy * 100:00.00}%", viewportX + 628 * playfieldScale, viewportY + 38 * playfieldScale, 16 * playfieldScale, rightAligned: true);
        renderNumber(session.Score.Combo, stageX + stageWidth / 2, viewportY + skin.ComboPosition * playfieldScale, 22 * playfieldScale, rightAligned: false);
        renderScoreText($"{session.Score.Combo}x", stageX + stageWidth / 2, viewportY + (skin.ComboPosition + 24) * playfieldScale, 13 * playfieldScale, rightAligned: false);

        if (lastResult != ManiaHitResult.None && time - resultAt < 550 && judgements.TryGetValue(lastResult, out var texture))
        {
            var width = 188 * playfieldScale;
            var height = 91 * playfieldScale;
            render(texture, new SDL_FRect
            {
                x = stageX + stageWidth / 2 - width / 2,
                y = viewportY + skin.ScorePosition * playfieldScale - height / 2,
                w = width,
                h = height
            });
        }

        if (Finished)
            renderResults(outputWidth, outputHeight);
    }

    private nint noteTexture(int column) => session.KeyCount % 2 == 1 && column == session.KeyCount / 2
        ? noteSpecial
        : (column & 1) == 0 ? note1 : note2;

    private void renderResults(int outputWidth, int outputHeight)
    {
        SDL_SetRenderDrawColor(renderer, 4, 4, 8, 215);
        var overlay = new SDL_FRect { x = 0, y = 0, w = outputWidth, h = outputHeight };
        SDL_RenderFillRect(renderer, &overlay);
        var panelWidth = MathF.Min(outputWidth * 0.72f, outputHeight * 1.05f);
        var panel = new SDL_FRect
        {
            x = (outputWidth - panelWidth) / 2,
            y = outputHeight * 0.20f,
            w = panelWidth,
            h = outputHeight * 0.60f
        };
        render(rankingPanel, panel);
        render(rankingTitle, new SDL_FRect
        {
            x = panel.x + panel.w * 0.12f,
            y = outputHeight * 0.10f,
            w = panel.w * 0.76f,
            h = outputHeight * 0.10f
        });
        renderNumber(session.Score.Score, outputWidth / 2f, outputHeight * 0.37f, 48, rightAligned: false);
        renderNumber((int)Math.Round(session.Score.Accuracy * 10000), outputWidth / 2f, outputHeight * 0.52f, 28, rightAligned: false);
        renderNumber(session.Score.MaxCombo, outputWidth / 2f, outputHeight * 0.64f, 28, rightAligned: false);
    }

    private void renderNumber(int value, float anchorX, float y, float height, bool rightAligned)
    {
        var text = Math.Max(0, value).ToString();
        var width = height * 0.62f;
        var x = rightAligned ? anchorX - text.Length * width : anchorX - text.Length * width / 2;
        foreach (var character in text)
        {
            render(digits[character - '0'], new SDL_FRect { x = x, y = y, w = width, h = height });
            x += width;
        }
    }

    private void renderScoreText(string text, float anchorX, float y, float height, bool rightAligned)
    {
        var width = height * 0.62f;
        var x = rightAligned ? anchorX - text.Length * width : anchorX - text.Length * width / 2;
        foreach (var character in text)
        {
            var handle = character switch
            {
                >= '0' and <= '9' => digits[character - '0'],
                '.' => scoreDot,
                '%' => scorePercent,
                'x' or 'X' => scoreX,
                _ => 0
            };
            if (handle != 0)
                render(handle, new SDL_FRect { x = x, y = y, w = width, h = height });
            x += width;
        }
    }

    private double elapsedMilliseconds() =>
        (audioPlayer?.PositionMilliseconds ?? (SDL_GetTicksNS() - startedAt) / 1_000_000.0) + universalOffset;

    private void render(nint handle, SDL_FRect destination)
    {
        var texture = (SDL_Texture*)handle;
        if (texture != null)
            SDL_RenderTexture(renderer, texture, null, &destination);
    }

    private nint load(string path)
    {
        var texture = IMG_LoadTexture(renderer, path);
        if (texture == null)
            Console.Error.WriteLine($"Failed to load mania texture '{path}': {SDL_GetError()}");
        else
            SDL_SetTextureBlendMode(texture, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        return (nint)texture;
    }

    public void Dispose()
    {
        audioPlayer?.Dispose();
        destroy(note1); destroy(note2); destroy(noteSpecial); destroy(noteHold);
        destroy(stageLeft); destroy(stageRight); destroy(stageLight); destroy(rankingPanel); destroy(rankingTitle);
        foreach (var texture in judgements.Values) destroy(texture);
        foreach (var digit in digits) destroy(digit);
        destroy(scoreDot); destroy(scorePercent); destroy(scoreX);
    }

    private static void destroy(nint handle)
    {
        var texture = (SDL_Texture*)handle;
        if (texture != null)
            SDL_DestroyTexture(texture);
    }
}
