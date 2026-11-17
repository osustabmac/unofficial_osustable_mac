using SDL;
using static SDL.SDL3;
using static SDL.SDL3_image;

namespace OsuStable.Native.Scenes;

internal enum PauseAction { Continue, Retry, Back }

internal sealed unsafe class PauseScene : IDisposable
{
    private readonly SDL_Renderer* renderer;
    private readonly nint[] textures;
    private int selected;

    public PauseScene(SDL_Renderer* renderer, string resourceRoot)
    {
        this.renderer = renderer;
        var ui = Path.Combine(resourceRoot, "ui");
        textures =
        [
            (nint)IMG_LoadTexture(renderer, Path.Combine(ui, "pause-continue@2x.png")),
            (nint)IMG_LoadTexture(renderer, Path.Combine(ui, "pause-retry@2x.png")),
            (nint)IMG_LoadTexture(renderer, Path.Combine(ui, "pause-back@2x.png"))
        ];
    }

    public void Move(int delta) => selected = (selected + delta + textures.Length) % textures.Length;
    public PauseAction Activate() => (PauseAction)selected;

    public void Render(int width, int height)
    {
        SDL_SetRenderDrawBlendMode(renderer, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        SDL_SetRenderDrawColor(renderer, 0, 0, 0, 205);
        var overlay = new SDL_FRect { x = 0, y = 0, w = width, h = height };
        SDL_RenderFillRect(renderer, &overlay);
        var buttonWidth = MathF.Min(width * .46f, height * .82f);
        var buttonHeight = buttonWidth * .24f;
        var total = buttonHeight * 3;
        for (var i = 0; i < textures.Length; i++)
        {
            var rect = new SDL_FRect
            {
                x = (width - buttonWidth) / 2 - (i == selected ? 12 : 0),
                y = (height - total) / 2 + i * buttonHeight,
                w = buttonWidth + (i == selected ? 24 : 0),
                h = buttonHeight
            };
            var texture = (SDL_Texture*)textures[i];
            if (texture != null) SDL_RenderTexture(renderer, texture, null, &rect);
        }
    }

    public void Dispose()
    {
        foreach (var handle in textures)
        {
            var texture = (SDL_Texture*)handle;
            if (texture != null) SDL_DestroyTexture(texture);
        }
    }
}
