using SDL;
using OsuStable.Core.Beatmaps;
using OsuStable.Native.Scenes;
using static SDL.SDL3;

namespace OsuStable.Native.Platform;

internal sealed unsafe class SdlPlatformHost : IPlatformHost
{
    private readonly IReadOnlyList<Beatmap> beatmaps;
    private readonly StableConfiguration configuration;
    private int selectedBeatmap;
    private SDL_Window* window;
    private SDL_Renderer* renderer;
    private StableMenuScene? menuScene;
    private SongSelectScene? songSelectScene;
    private OptionsScene? optionsScene;
    private PauseScene? pauseScene;
    private ManiaGameplayScene? maniaGameplayScene;
    private AppScreen screen;
    private bool running;

    public SdlPlatformHost(string? beatmapPath)
    {
        beatmaps = loadBeatmaps(beatmapPath);
        configuration = StableConfiguration.Load(ApplicationPaths.ConfigurationFile);
    }

    public int Run()
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_AUDIO | SDL_InitFlags.SDL_INIT_GAMEPAD))
        {
            Console.Error.WriteLine($"SDL initialization failed: {SDL_GetError()}");
            return 1;
        }

        window = SDL_CreateWindow(
            "osu!stable native port"u8,
            1280,
            720,
            SDL_WindowFlags.SDL_WINDOW_RESIZABLE |
            SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY);

        if (window == null)
        {
            Console.Error.WriteLine($"Window creation failed: {SDL_GetError()}");
            return 2;
        }

        renderer = SDL_CreateRenderer(window, (Utf8String)null);
        if (renderer == null)
        {
            Console.Error.WriteLine($"Renderer creation failed: {SDL_GetError()}");
            return 3;
        }

        applyDisplaySettings();
        menuScene = new StableMenuScene(renderer, stableResourceRoot());
        songSelectScene = new SongSelectScene(renderer, stableResourceRoot());
        songSelectScene.SetLibrary(beatmaps, selectedBeatmap);
        optionsScene = new OptionsScene(renderer, stableResourceRoot(), configuration);
        pauseScene = new PauseScene(renderer, stableResourceRoot());
        screen = AppScreen.MainMenu;
        running = true;
        var startedAt = SDL_GetTicksNS();

        while (running)
        {
            pumpEvents();

            SDL_SetRenderDrawColor(renderer, 20, 20, 24, 255);
            SDL_RenderClear(renderer);

            if (tryGetOutputSize(out var width, out var height))
            {
                switch (screen)
                {
                    case AppScreen.MainMenu:
                        menuScene.Render(width, height, (SDL_GetTicksNS() - startedAt) / 1_000_000_000.0);
                        break;
                    case AppScreen.SongSelect:
                        songSelectScene.Render(width, height);
                        break;
                    case AppScreen.Options:
                        optionsScene.Render(width, height);
                        break;
                    case AppScreen.Gameplay:
                        maniaGameplayScene?.Render(width, height);
                        break;
                    case AppScreen.Pause:
                        maniaGameplayScene?.Render(width, height);
                        pauseScene?.Render(width, height);
                        break;
                }
            }
            SDL_RenderPresent(renderer);
        }

        return 0;
    }

    private static string stableResourceRoot() => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "Resources",
            "stable"));

    private void pumpEvents()
    {
        SDL_Event e;
        while (SDL_PollEvent(&e))
        {
            SDL_ConvertEventToRenderCoordinates(renderer, &e);
            switch (e.Type)
            {
                case SDL_EventType.SDL_EVENT_QUIT:
                    running = false;
                    break;

                case SDL_EventType.SDL_EVENT_KEY_DOWN:
                    if (e.key.key == SDL_Keycode.SDLK_ESCAPE)
                    {
                        if (screen == AppScreen.MainMenu)
                            running = false;
                        else if (screen == AppScreen.Gameplay)
                            pauseGameplay();
                        else if (screen == AppScreen.Pause)
                            resumeGameplay();
                        else
                            showMainMenu();
                    }
                    else if (screen == AppScreen.MainMenu && e.key.key == SDL_Keycode.SDLK_RETURN)
                    {
                        handleMenuAction(menuScene?.ActivateHovered() ?? MenuAction.Play);
                    }
                    else if (screen == AppScreen.MainMenu && e.key.key == SDL_Keycode.SDLK_DOWN)
                    {
                        menuScene?.MoveSelection(1);
                    }
                    else if (screen == AppScreen.MainMenu && e.key.key == SDL_Keycode.SDLK_UP)
                    {
                        menuScene?.MoveSelection(-1);
                    }
                    else if (screen == AppScreen.SongSelect && e.key.key == SDL_Keycode.SDLK_RETURN)
                    {
                        startGameplay();
                    }
                    else if (screen == AppScreen.SongSelect && e.key.key == SDL_Keycode.SDLK_DOWN)
                    {
                        moveBeatmap(1);
                    }
                    else if (screen == AppScreen.SongSelect && e.key.key == SDL_Keycode.SDLK_UP)
                    {
                        moveBeatmap(-1);
                    }
                    else if (screen == AppScreen.Options && e.key.key == SDL_Keycode.SDLK_DOWN)
                    {
                        optionsScene?.MoveSelection(1);
                    }
                    else if (screen == AppScreen.Options && e.key.key == SDL_Keycode.SDLK_UP)
                    {
                        optionsScene?.MoveSelection(-1);
                    }
                    else if (screen == AppScreen.Options && (e.key.key == SDL_Keycode.SDLK_LEFT || e.key.key == SDL_Keycode.SDLK_RIGHT || e.key.key == SDL_Keycode.SDLK_RETURN))
                    {
                        var displayChanged = optionsScene?.Adjust(e.key.key == SDL_Keycode.SDLK_LEFT ? -1 : 1) == true;
                        configuration.Save(ApplicationPaths.ConfigurationFile);
                        if (displayChanged) applyDisplaySettings();
                    }
                    else if (screen == AppScreen.Gameplay && !e.key.repeat)
                    {
                        if (e.key.key == SDL_Keycode.SDLK_RETURN && maniaGameplayScene?.Finished == true)
                            showSongSelect();
                        else
                            setManiaKey(e.key.key, true);
                    }
                    else if (screen == AppScreen.Pause && e.key.key == SDL_Keycode.SDLK_DOWN)
                    {
                        pauseScene?.Move(1);
                    }
                    else if (screen == AppScreen.Pause && e.key.key == SDL_Keycode.SDLK_UP)
                    {
                        pauseScene?.Move(-1);
                    }
                    else if (screen == AppScreen.Pause && e.key.key == SDL_Keycode.SDLK_RETURN)
                    {
                        handlePauseAction(pauseScene?.Activate() ?? PauseAction.Continue);
                    }
                    break;

                case SDL_EventType.SDL_EVENT_KEY_UP:
                    if (screen == AppScreen.Gameplay)
                        setManiaKey(e.key.key, false);
                    break;

                case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                    if (tryGetOutputSize(out var width, out var height))
                    {
                        if (screen == AppScreen.MainMenu)
                            menuScene?.PointerMoved(e.motion.x, e.motion.y, width, height);
                        else if (screen == AppScreen.SongSelect)
                            songSelectScene?.PointerMoved(e.motion.x, e.motion.y, width, height);
                        else if (screen == AppScreen.Options)
                            optionsScene?.PointerMoved(e.motion.x, e.motion.y, width, height);
                    }
                    break;

                case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                    if (screen == AppScreen.MainMenu)
                    {
                        handleMenuAction(menuScene?.ActivateHovered() ?? MenuAction.None);
                    }
                    else if (screen == AppScreen.SongSelect && songSelectScene?.ActivateBack() == true)
                    {
                        showMainMenu();
                    }
                    else if (screen == AppScreen.SongSelect && songSelectScene?.ActivateSelection() is int selected)
                    {
                        selectedBeatmap = selected;
                        updateSongSelectTitle();
                        startGameplay();
                    }
                    else if (screen == AppScreen.Options && optionsScene?.ActivateBack() == true)
                    {
                        showMainMenu();
                    }
                    break;
            }
        }
    }

    private bool tryGetOutputSize(out int width, out int height)
    {
        int currentWidth;
        int currentHeight;
        var result = SDL_GetCurrentRenderOutputSize(renderer, &currentWidth, &currentHeight);
        width = currentWidth;
        height = currentHeight;
        return result;
    }

    private void handleMenuAction(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.Play:
                screen = AppScreen.SongSelect;
                updateSongSelectTitle();
                break;

            case MenuAction.Multiplayer:
                SDL_SetWindowTitle(window, "osu!stable native port — multiplayer offline"u8);
                break;

            case MenuAction.Options:
                screen = AppScreen.Options;
                SDL_SetWindowTitle(window, "osu!stable native port — options"u8);
                break;

            case MenuAction.Exit:
                running = false;
                break;
        }
    }

    private void showMainMenu()
    {
        screen = AppScreen.MainMenu;
        SDL_SetWindowTitle(window, "osu!stable native port"u8);
    }

    private void showSongSelect()
    {
        maniaGameplayScene?.Dispose();
        maniaGameplayScene = null;
        screen = AppScreen.SongSelect;
        songSelectScene?.SetLibrary(beatmaps, selectedBeatmap);
        updateSongSelectTitle();
    }

    private void startGameplay()
    {
        if (beatmaps.Count == 0)
            return;

        maniaGameplayScene?.Dispose();
        maniaGameplayScene = new ManiaGameplayScene(renderer, stableResourceRoot(), beatmaps[selectedBeatmap], configuration);
        maniaGameplayScene.Start();
        screen = AppScreen.Gameplay;
        SDL_SetWindowTitle(window, "osu!stable native port — osu!mania"u8);
    }

    private void pauseGameplay()
    {
        maniaGameplayScene?.Pause();
        screen = AppScreen.Pause;
    }

    private void resumeGameplay()
    {
        maniaGameplayScene?.Resume();
        screen = AppScreen.Gameplay;
    }

    private void handlePauseAction(PauseAction action)
    {
        switch (action)
        {
            case PauseAction.Continue: resumeGameplay(); break;
            case PauseAction.Retry: startGameplay(); break;
            case PauseAction.Back: showSongSelect(); break;
        }
    }

    private static IReadOnlyList<Beatmap> loadBeatmaps(string? beatmapPath)
    {
        if (!string.IsNullOrWhiteSpace(beatmapPath) && File.Exists(beatmapPath))
            return [BeatmapParser.ParseFile(beatmapPath)];

        return BeatmapLibrary.Scan(ApplicationPaths.SongsDirectory).Beatmaps;
    }

    private void moveBeatmap(int delta)
    {
        if (beatmaps.Count == 0)
            return;
        selectedBeatmap = (selectedBeatmap + delta + beatmaps.Count) % beatmaps.Count;
        songSelectScene?.SetLibrary(beatmaps, selectedBeatmap);
        updateSongSelectTitle();
    }

    private void updateSongSelectTitle()
    {
        if (beatmaps.Count == 0)
        {
            SDL_SetWindowTitle(window, "osu!stable native port — no beatmaps"u8);
            return;
        }
        var map = beatmaps[selectedBeatmap];
        SDL_SetWindowTitle(window, $"osu!mania — {map.Metadata.Artist} - {map.Metadata.Title} [{map.Metadata.Version}]");
    }

    private void setManiaKey(SDL_Keycode key, bool pressed)
    {
        if (maniaGameplayScene == null)
            return;
        var layout = keyLayout(maniaGameplayScene.KeyCount);
        var column = Array.IndexOf(layout, key);
        if (column >= 0)
            maniaGameplayScene.SetKey(column, pressed);
    }

    private static SDL_Keycode[] keyLayout(int keys) => keys switch
    {
        1 => [SDL_Keycode.SDLK_SPACE],
        2 => [SDL_Keycode.SDLK_F, SDL_Keycode.SDLK_J],
        3 => [SDL_Keycode.SDLK_F, SDL_Keycode.SDLK_SPACE, SDL_Keycode.SDLK_J],
        4 => [SDL_Keycode.SDLK_D, SDL_Keycode.SDLK_F, SDL_Keycode.SDLK_J, SDL_Keycode.SDLK_K],
        5 => [SDL_Keycode.SDLK_D, SDL_Keycode.SDLK_F, SDL_Keycode.SDLK_SPACE, SDL_Keycode.SDLK_J, SDL_Keycode.SDLK_K],
        6 => [SDL_Keycode.SDLK_S, SDL_Keycode.SDLK_D, SDL_Keycode.SDLK_F, SDL_Keycode.SDLK_J, SDL_Keycode.SDLK_K, SDL_Keycode.SDLK_L],
        7 => [SDL_Keycode.SDLK_S, SDL_Keycode.SDLK_D, SDL_Keycode.SDLK_F, SDL_Keycode.SDLK_SPACE, SDL_Keycode.SDLK_J, SDL_Keycode.SDLK_K, SDL_Keycode.SDLK_L],
        8 => [SDL_Keycode.SDLK_A, SDL_Keycode.SDLK_S, SDL_Keycode.SDLK_D, SDL_Keycode.SDLK_F, SDL_Keycode.SDLK_J, SDL_Keycode.SDLK_K, SDL_Keycode.SDLK_L, SDL_Keycode.SDLK_SEMICOLON],
        _ => [SDL_Keycode.SDLK_A, SDL_Keycode.SDLK_S, SDL_Keycode.SDLK_D, SDL_Keycode.SDLK_F, SDL_Keycode.SDLK_SPACE, SDL_Keycode.SDLK_J, SDL_Keycode.SDLK_K, SDL_Keycode.SDLK_L, SDL_Keycode.SDLK_SEMICOLON]
    };

    private void applyDisplaySettings()
    {
        SDL_SetRenderVSync(renderer, configuration.FrameSync ? 1 : 0);
        SDL_SetWindowFullscreen(window, configuration.Fullscreen);
    }

    public void Dispose()
    {
        menuScene?.Dispose();
        menuScene = null;
        songSelectScene?.Dispose();
        songSelectScene = null;
        optionsScene?.Dispose();
        optionsScene = null;
        pauseScene?.Dispose();
        pauseScene = null;
        maniaGameplayScene?.Dispose();
        maniaGameplayScene = null;

        if (renderer != null)
        {
            SDL_DestroyRenderer(renderer);
            renderer = null;
        }

        if (window != null)
        {
            SDL_DestroyWindow(window);
            window = null;
        }

        SDL_Quit();
    }

    private enum AppScreen
    {
        MainMenu,
        SongSelect,
        Options,
        Gameplay,
        Pause
    }
}
