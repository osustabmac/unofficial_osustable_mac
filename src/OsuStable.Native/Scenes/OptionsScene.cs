using SDL;
using static SDL.SDL3;
using static SDL.SDL3_image;

namespace OsuStable.Native.Scenes;

internal sealed unsafe class OptionsScene : IDisposable
{
    private static readonly string[] labels =
    [
        "FULLSCREEN", "FRAME SYNC", "MASTER VOLUME", "MUSIC VOLUME",
        "EFFECT VOLUME", "UNIVERSAL OFFSET", "MANIA SPEED", "BACKGROUND DIM"
    ];

    private readonly SDL_Renderer* renderer;
    private readonly StableConfiguration config;
    private readonly nint background;
    private readonly nint optionsTab;
    private readonly nint back;
    private readonly nint backHover;
    private int selected;
    private bool backHovered;

    public OptionsScene(SDL_Renderer* renderer, string resourceRoot, StableConfiguration config)
    {
        this.renderer = renderer;
        this.config = config;
        var ui = Path.Combine(resourceRoot, "ui");
        background = load(Path.Combine(ui, "menu-background-2@2x.jpg"));
        optionsTab = load(Path.Combine(ui, "overlay-options@2x.png"));
        back = load(Path.Combine(ui, "menu-button-back@2x.png"));
        backHover = load(Path.Combine(ui, "menu-button-back-over@2x.png"));
    }

    public void MoveSelection(int delta) => selected = (selected + delta + labels.Length) % labels.Length;

    public bool Adjust(int direction)
    {
        switch (selected)
        {
            case 0: config.Fullscreen = !config.Fullscreen; break;
            case 1: config.FrameSync = !config.FrameSync; break;
            case 2: config.VolumeUniversal = step(config.VolumeUniversal, direction, 0, 100); break;
            case 3: config.VolumeMusic = step(config.VolumeMusic, direction, 0, 100); break;
            case 4: config.VolumeEffect = step(config.VolumeEffect, direction, 0, 100); break;
            case 5: config.Offset = step(config.Offset, direction, -500, 500, 1); break;
            case 6: config.ManiaSpeed = step(config.ManiaSpeed, direction, 1, 40, 1); break;
            case 7: config.DimLevel = step(config.DimLevel, direction, 0, 100); break;
        }
        return selected is 0 or 1;
    }

    public void PointerMoved(float x, float y, int outputWidth, int outputHeight) =>
        backHovered = contains(backRect(outputWidth, outputHeight), x, y);

    public bool ActivateBack() => backHovered;

    public void Render(int outputWidth, int outputHeight)
    {
        renderCover(background, outputWidth, outputHeight);
        SDL_SetRenderDrawBlendMode(renderer, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        SDL_SetRenderDrawColor(renderer, 0, 0, 0, 185);
        var panel = new SDL_FRect { x = outputWidth * .16f, y = outputHeight * .08f, w = outputWidth * .70f, h = outputHeight * .78f };
        SDL_RenderFillRect(renderer, &panel);
        render(optionsTab, new SDL_FRect { x = 0, y = outputHeight * .12f, w = 54, h = 225 });

        var scale = MathF.Max(1, outputHeight / 720f);
        for (var i = 0; i < labels.Length; i++)
        {
            var y = panel.y + 45 * scale + i * 55 * scale;
            if (i == selected)
            {
                SDL_SetRenderDrawColor(renderer, 255, 102, 171, 150);
                var highlight = new SDL_FRect { x = panel.x + 18, y = y - 10, w = panel.w - 36, h = 34 * scale };
                SDL_RenderFillRect(renderer, &highlight);
            }
            SDL_SetRenderScale(renderer, scale, scale);
            SDL_RenderDebugText(renderer, (panel.x + 32) / scale, y / scale, labels[i]);
            SDL_RenderDebugText(renderer, (panel.x + panel.w - 180 * scale) / scale, y / scale, value(i));
            SDL_SetRenderScale(renderer, 1, 1);
        }
        var backArea = backRect(outputWidth, outputHeight);
        render(backHovered ? backHover : back, backArea);
    }

    private string value(int index) => index switch
    {
        0 => config.Fullscreen ? "ON" : "OFF",
        1 => config.FrameSync ? "ON" : "OFF",
        2 => $"{config.VolumeUniversal}%",
        3 => $"{config.VolumeMusic}%",
        4 => $"{config.VolumeEffect}%",
        5 => $"{config.Offset:+0;-0;0} ms",
        6 => config.ManiaSpeed.ToString(),
        _ => $"{config.DimLevel}%"
    };

    private static int step(int value, int direction, int minimum, int maximum, int amount = 5) =>
        Math.Clamp(value + Math.Sign(direction) * amount, minimum, maximum);

    private SDL_FRect backRect(int width, int height)
    {
        var buttonWidth = MathF.Min(width * .30f, height * .62f);
        return new SDL_FRect { x = -buttonWidth * .04f, y = height * .82f, w = buttonWidth, h = buttonWidth * 178f / 1166f };
    }

    private void renderCover(nint handle, int width, int height)
    {
        var texture = (SDL_Texture*)handle;
        float textureWidth;
        float textureHeight;
        if (texture == null || !SDL_GetTextureSize(texture, &textureWidth, &textureHeight)) return;
        var scale = MathF.Max(width / textureWidth, height / textureHeight);
        var rect = new SDL_FRect { x = (width - textureWidth * scale) / 2, y = (height - textureHeight * scale) / 2, w = textureWidth * scale, h = textureHeight * scale };
        SDL_RenderTexture(renderer, texture, null, &rect);
    }

    private void render(nint handle, SDL_FRect rect)
    {
        var texture = (SDL_Texture*)handle;
        if (texture != null) SDL_RenderTexture(renderer, texture, null, &rect);
    }

    private nint load(string path) => (nint)IMG_LoadTexture(renderer, path);
    private static bool contains(SDL_FRect rect, float x, float y) => x >= rect.x && x <= rect.x + rect.w && y >= rect.y && y <= rect.y + rect.h;
    private static void destroy(nint handle) { var texture = (SDL_Texture*)handle; if (texture != null) SDL_DestroyTexture(texture); }

    public void Dispose() { destroy(background); destroy(optionsTab); destroy(back); destroy(backHover); }
}
