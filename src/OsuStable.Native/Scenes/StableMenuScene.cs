using SDL;
using static SDL.SDL3;
using static SDL.SDL3_image;

namespace OsuStable.Native.Scenes;

internal enum MenuAction
{
    None,
    Play,
    Multiplayer,
    Options,
    Exit
}

internal sealed unsafe class StableMenuScene : IDisposable
{
    private readonly SDL_Renderer* renderer;
    private readonly nint background;
    private readonly nint logo;
    private readonly MenuButton[] buttons;
    private int hoveredButton;

    public StableMenuScene(SDL_Renderer* renderer, string resourceRoot)
    {
        this.renderer = renderer;
        var ui = Path.Combine(resourceRoot, "ui");
        background = load(Path.Combine(ui, "menu-background@2x.jpg"));
        logo = load(Path.Combine(ui, "menu-osu@2x.png"));
        buttons =
        [
            createButton(ui, "play", MenuAction.Play),
            createButton(ui, "multiplayer", MenuAction.Multiplayer),
            createButton(ui, "options", MenuAction.Options),
            createButton(ui, "exit", MenuAction.Exit)
        ];
    }

    public void PointerMoved(float x, float y, int outputWidth, int outputHeight)
    {
        hoveredButton = -1;

        for (var i = 0; i < buttons.Length; i++)
        {
            if (contains(getButtonRect(i, outputWidth, outputHeight), x, y))
            {
                hoveredButton = i;
                break;
            }
        }
    }

    public MenuAction ActivateHovered() =>
        buttons[hoveredButton >= 0 ? hoveredButton : 0].Action;

    public void MoveSelection(int delta)
    {
        var current = hoveredButton >= 0 ? hoveredButton : 0;
        hoveredButton = (current + delta + buttons.Length) % buttons.Length;
    }

    public void Render(int outputWidth, int outputHeight, double timeSeconds)
    {
        renderCover(background, outputWidth, outputHeight);

        var logoSide = MathF.Min(outputHeight * 0.56f, outputWidth * 0.38f);
        var pulse = 1 + MathF.Sin((float)timeSeconds * 2.2f) * 0.012f;
        logoSide *= pulse;
        SDL_FRect logoRect = new()
        {
            x = outputWidth * 0.35f - logoSide / 2,
            y = outputHeight * 0.50f - logoSide / 2,
            w = logoSide,
            h = logoSide
        };
        render(logo, logoRect);

        for (var i = 0; i < buttons.Length; i++)
        {
            var texture = i == hoveredButton ? buttons[i].Hover : buttons[i].Normal;
            var rect = getButtonRect(i, outputWidth, outputHeight);
            if (i == hoveredButton)
            {
                rect.x -= 8;
                rect.w += 8;
            }
            render(texture, rect);
        }
    }

    private MenuButton createButton(string directory, string name, MenuAction action) => new(
        load(Path.Combine(directory, $"menu-button-{name}@2x.png")),
        load(Path.Combine(directory, $"menu-button-{name}-over@2x.png")),
        action);

    private SDL_FRect getButtonRect(int index, int outputWidth, int outputHeight)
    {
        var width = MathF.Min(outputWidth * 0.50f, outputHeight * 0.92f);
        var height = width * 178f / 1166f;
        var gap = height * 0.06f;
        var totalHeight = height * buttons.Length + gap * (buttons.Length - 1);
        return new SDL_FRect
        {
            x = outputWidth * 0.46f,
            y = (outputHeight - totalHeight) / 2 + index * (height + gap),
            w = width,
            h = height
        };
    }

    private void renderCover(nint textureHandle, int outputWidth, int outputHeight)
    {
        var texture = (SDL_Texture*)textureHandle;
        float width;
        float height;
        if (texture == null || !SDL_GetTextureSize(texture, &width, &height))
            return;

        var scale = MathF.Max(outputWidth / width, outputHeight / height);
        SDL_FRect destination = new()
        {
            x = (outputWidth - width * scale) / 2,
            y = (outputHeight - height * scale) / 2,
            w = width * scale,
            h = height * scale
        };
        SDL_RenderTexture(renderer, texture, null, &destination);
    }

    private void render(nint textureHandle, SDL_FRect destination)
    {
        var texture = (SDL_Texture*)textureHandle;
        if (texture != null)
            SDL_RenderTexture(renderer, texture, null, &destination);
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
        destroy(logo);
        foreach (var button in buttons)
        {
            destroy(button.Normal);
            destroy(button.Hover);
        }
    }

    private static void destroy(nint textureHandle)
    {
        var texture = (SDL_Texture*)textureHandle;
        if (texture != null)
            SDL_DestroyTexture(texture);
    }

    private readonly record struct MenuButton(nint Normal, nint Hover, MenuAction Action);
}
