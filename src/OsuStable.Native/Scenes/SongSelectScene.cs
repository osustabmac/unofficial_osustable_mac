using SDL;
using OsuStable.Core.Beatmaps;
using static SDL.SDL3;
using static SDL.SDL3_image;

namespace OsuStable.Native.Scenes;

internal sealed unsafe class SongSelectScene : IDisposable
{
    private readonly SDL_Renderer* renderer;
    private readonly nint background;
    private readonly nint header;
    private readonly nint back;
    private readonly nint backHover;
    private readonly nint mode;
    private readonly nint options;
    private readonly nint random;
    private bool backHovered;
    private IReadOnlyList<Beatmap> beatmaps = [];
    private int selected;
    private int hoveredIndex = -1;

    public SongSelectScene(SDL_Renderer* renderer, string resourceRoot)
    {
        this.renderer = renderer;
        var ui = Path.Combine(resourceRoot, "ui");
        background = load(Path.Combine(ui, "menu-background-3@2x.jpg"));
        header = load(Path.Combine(ui, "menu-button-freeplay@2x.png"));
        back = load(Path.Combine(ui, "menu-button-back@2x.png"));
        backHover = load(Path.Combine(ui, "menu-button-back-over@2x.png"));
        mode = load(Path.Combine(ui, "selection-mode@2x.png"));
        options = load(Path.Combine(ui, "selection-options@2x.png"));
        random = load(Path.Combine(ui, "selection-random@2x.png"));
    }

    public void PointerMoved(float x, float y, int outputWidth, int outputHeight)
    {
        backHovered = contains(getBackRect(outputWidth, outputHeight), x, y);
        hoveredIndex = -1;
        if (x < outputWidth * .38f || x > outputWidth * .96f)
            return;
        var rowHeight = MathF.Max(42, outputHeight * .072f);
        var row = (int)((y - outputHeight * .19f) / rowHeight);
        var first = Math.Clamp(selected - 4, 0, Math.Max(0, beatmaps.Count - 9));
        if (row >= 0 && row < Math.Min(9, beatmaps.Count))
            hoveredIndex = first + row;
    }

    public bool ActivateBack() => backHovered;

    public int? ActivateSelection() => hoveredIndex >= 0 ? hoveredIndex : null;

    public void SetLibrary(IReadOnlyList<Beatmap> maps, int selectedIndex)
    {
        beatmaps = maps;
        selected = selectedIndex;
    }

    public void Render(int outputWidth, int outputHeight)
    {
        renderCover(background, outputWidth, outputHeight);

        var headerWidth = MathF.Min(outputWidth * 0.62f, outputHeight * 1.10f);
        SDL_FRect headerRect = new()
        {
            x = (outputWidth - headerWidth) / 2,
            y = outputHeight * 0.10f,
            w = headerWidth,
            h = headerWidth * 178f / 1166f
        };
        render(header, headerRect);

        var rowHeight = MathF.Max(42, outputHeight * .072f);
        var listX = outputWidth * .38f;
        var listWidth = outputWidth * .58f;
        var listY = outputHeight * .19f;
        var first = Math.Clamp(selected - 4, 0, Math.Max(0, beatmaps.Count - 9));
        for (var visible = 0; visible < Math.Min(9, beatmaps.Count); visible++)
        {
            var index = first + visible;
            var rowY = listY + visible * rowHeight;
            var active = index == selected;
            SDL_SetRenderDrawBlendMode(renderer, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL_SetRenderDrawColor(renderer, active ? (byte)242 : (byte)20, active ? (byte)92 : (byte)20, active ? (byte)160 : (byte)28, active ? (byte)220 : (byte)190);
            var row = new SDL_FRect { x = listX - (active ? 20 : 0), y = rowY, w = listWidth + (active ? 20 : 0), h = rowHeight - 3 };
            SDL_RenderFillRect(renderer, &row);
            var map = beatmaps[index];
            var scale = MathF.Max(1, outputHeight / 900f);
            SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
            SDL_SetRenderScale(renderer, scale, scale);
            SDL_RenderDebugText(renderer, (row.x + 16) / scale, (row.y + 9) / scale, $"{map.Metadata.Artist} - {map.Metadata.Title}");
            SDL_RenderDebugText(renderer, (row.x + 28) / scale, (row.y + 25) / scale, $"[{map.Metadata.Version}]  {(map.General.Mode == 3 ? "osu!mania" : "converted to 4K")}");
            if (active)
                SDL_RenderDebugText(renderer, (row.x + row.w - 150 * scale) / scale, (row.y + 17) / scale, "ENTER TO PLAY");
            SDL_SetRenderScale(renderer, 1, 1);
        }

        if (beatmaps.Count == 0)
        {
            SDL_SetRenderScale(renderer, 2, 2);
            SDL_RenderDebugText(renderer, outputWidth * .24f, outputHeight * .24f, "NO BEATMAPS IN SONGS FOLDER");
            SDL_SetRenderScale(renderer, 1, 1);
        }

        var backRect = getBackRect(outputWidth, outputHeight);
        render(backHovered ? backHover : back, backRect);

        var controlSize = MathF.Min(outputHeight * 0.17f, outputWidth * 0.12f);
        var gap = controlSize * 0.10f;
        var controlsWidth = controlSize * 3 + gap * 2;
        var startX = (outputWidth - controlsWidth) / 2;
        var y = outputHeight - controlSize - outputHeight * 0.045f;
        render(mode, new SDL_FRect { x = startX, y = y, w = controlSize, h = controlSize });
        render(options, new SDL_FRect { x = startX + controlSize + gap, y = y, w = controlSize, h = controlSize });
        render(random, new SDL_FRect { x = startX + (controlSize + gap) * 2, y = y, w = controlSize, h = controlSize });
    }

    private SDL_FRect getBackRect(int outputWidth, int outputHeight)
    {
        var width = MathF.Min(outputWidth * 0.30f, outputHeight * 0.62f);
        return new SDL_FRect
        {
            x = -width * 0.04f,
            y = outputHeight * 0.78f,
            w = width,
            h = width * 178f / 1166f
        };
    }

    private void renderCover(nint handle, int outputWidth, int outputHeight)
    {
        var texture = (SDL_Texture*)handle;
        float width;
        float height;
        if (texture == null || !SDL_GetTextureSize(texture, &width, &height))
            return;

        var scale = MathF.Max(outputWidth / width, outputHeight / height);
        SDL_FRect rect = new()
        {
            x = (outputWidth - width * scale) / 2,
            y = (outputHeight - height * scale) / 2,
            w = width * scale,
            h = height * scale
        };
        SDL_RenderTexture(renderer, texture, null, &rect);
    }

    private void render(nint handle, SDL_FRect rect)
    {
        var texture = (SDL_Texture*)handle;
        if (texture != null)
            SDL_RenderTexture(renderer, texture, null, &rect);
    }

    private nint load(string path)
    {
        var texture = IMG_LoadTexture(renderer, path);
        if (texture == null)
            Console.Error.WriteLine($"Failed to load stable texture '{path}': {SDL_GetError()}");
        return (nint)texture;
    }

    private static bool contains(SDL_FRect rect, float x, float y) =>
        x >= rect.x && x <= rect.x + rect.w && y >= rect.y && y <= rect.y + rect.h;

    public void Dispose()
    {
        destroy(background);
        destroy(header);
        destroy(back);
        destroy(backHover);
        destroy(mode);
        destroy(options);
        destroy(random);
    }

    private static void destroy(nint handle)
    {
        var texture = (SDL_Texture*)handle;
        if (texture != null)
            SDL_DestroyTexture(texture);
    }
}
