using System;
using System.Collections;
using System.Drawing;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Platform;
using SDL;
using static SDL.SDL3;

internal static class MacPlatform
{
    internal static void OpenExternal(string target, string arguments = null)
    {
        if (string.IsNullOrWhiteSpace(target))
            return;
        var info = new System.Diagnostics.ProcessStartInfo("/usr/bin/open")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(target);
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            info.ArgumentList.Add("--args");
            foreach (var argument in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                info.ArgumentList.Add(argument);
        }
        System.Diagnostics.Process.Start(info);
    }
}

internal static unsafe class MacStableWindow
{
    [DllImport("SDL3", EntryPoint = "SDL_SyncWindow")]
    private static extern byte SDL_SyncWindowNative(SDL_Window* window);

    internal static SDL_Window* Window;
    internal static SDL_GLContextState* Context;
    internal static GraphicsContext OpenTkContext;
    internal static bool Closing;
    private static readonly bool[] keys = new bool[256];
    private static readonly bool[] pressedThisPump = new bool[256];
    private static readonly bool[] releaseNextPump = new bool[256];
    private static float mouseX = 640;
    private static float mouseY = 360;
    private static float wheelAccumulator;
    private static SDL_MouseButtonFlags mouseButtons;
    private static bool nativeWindowWasShown;
    private static bool fullscreenActive;

    internal static int TargetViewportWidth = 1280;
    internal static int TargetViewportHeight = 720;

    internal static event EventHandler Resized;
    internal static event Action<int, bool> MouseButtonChanged;
    internal static event Action<float, float> MousePositionChanged;
    internal static event Action<int> MouseWheelScrolled;
    internal static event Action<string> FileDropped;

    internal static void WindowLog(string message)
    {
        try { File.AppendAllText("/tmp/osustab-window.log", $"{DateTime.Now:O} {message}\n"); }
        catch { }
    }

    internal static void EnsureCreated()
    {
        if (Window != null)
            return;
        Console.Error.WriteLine("[mac-host] SDL init");
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_AUDIO | SDL_InitFlags.SDL_INIT_GAMEPAD))
            throw new InvalidOperationException($"SDL initialization failed: {SDL_GetError()}");
        Window = SDL_CreateWindow("osu!stable"u8, 1280, 720,
            SDL_WindowFlags.SDL_WINDOW_OPENGL |
            SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
        if (Window == null)
            throw new InvalidOperationException($"SDL window creation failed: {SDL_GetError()}");
        Context = SDL_GL_CreateContext(Window);
        if (Context == null)
            throw new InvalidOperationException($"SDL OpenGL context creation failed: {SDL_GetError()}");
        SDL_GL_MakeCurrent(Window, Context);

        var initialResize = SDL_SetWindowSize(Window, 1280, 720);
        SDL_SetWindowPosition(Window, 0x2FFF0000, 0x2FFF0000); // SDL_WINDOWPOS_CENTERED
        var initialShow = SDL_ShowWindow(Window);
        if (!initialShow)
            throw new InvalidOperationException($"SDL window show failed: {SDL_GetError()}");
        nint cocoaWindow;
        fixed (byte* cocoaWindowProperty = SDL_PROP_WINDOW_COCOA_WINDOW_POINTER)
            cocoaWindow = SDL_GetPointerProperty(
                SDL_GetWindowProperties(Window), cocoaWindowProperty, 0);
        int diagnosticWidth;
        int diagnosticHeight;
        SDL_GetWindowSize(Window, &diagnosticWidth, &diagnosticHeight);
        WindowLog($"create sdl=0x{(nint)Window:x} cocoa=0x{cocoaWindow:x} resize={initialResize} show={initialShow} size={diagnosticWidth}x{diagnosticHeight} error={SDL_GetError()}");
        MacApplicationActivation.ShowNativeWindow(cocoaWindow, 1280, 720);
        nativeWindowWasShown = true;
        MacApplicationActivation.Activate();
        SDL_RaiseWindow(Window);
    }

    internal static void Run(Action updateFrame, Action renderFrame)
    {
        EnsureCreated();
        while (!Closing)
        {
            PumpEvents();
            updateFrame?.Invoke();
            renderFrame?.Invoke();
            SDL_GL_SwapWindow(Window);
        }
    }

    internal static void PumpEvents()
    {
        EnsureCreated();
        Array.Clear(pressedThisPump);
        for (int i = 0; i < releaseNextPump.Length; i++)
        {
            if (releaseNextPump[i])
            {
                keys[i] = false;
                releaseNextPump[i] = false;
            }
        }

        SDL_Event e;
        while (SDL_PollEvent(&e))
        {
            if (e.Type == SDL_EventType.SDL_EVENT_QUIT)
            {
                Closing = true;
                return;
            }
            if (e.Type == SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED)
            {
                Closing = true;
                return;
            }
            if (e.Type == SDL_EventType.SDL_EVENT_KEY_DOWN || e.Type == SDL_EventType.SDL_EVENT_KEY_UP)
            {
                int vk = ToVirtualKey(e.key.scancode);
                if (vk >= 0 && vk < keys.Length)
                {
                    bool isDown = e.Type == SDL_EventType.SDL_EVENT_KEY_DOWN;
                    if (isDown)
                    {
                        keys[vk] = true;
                        pressedThisPump[vk] = true;
                    }
                    else
                    {
                        if (pressedThisPump[vk])
                            releaseNextPump[vk] = true;
                        else
                            keys[vk] = false;
                    }
                }
            }
            else if (e.Type == SDL_EventType.SDL_EVENT_MOUSE_MOTION)
            {
                mouseX = e.motion.x;
                mouseY = e.motion.y;
                mouseButtons = e.motion.state;
            }
            else if (e.Type == SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN || e.Type == SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP)
            {
                mouseX = e.button.x;
                mouseY = e.button.y;
                bool isDown = e.Type == SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN;
                var mask = e.button.button switch
                {
                    1 => SDL_MouseButtonFlags.SDL_BUTTON_LMASK,
                    2 => SDL_MouseButtonFlags.SDL_BUTTON_MMASK,
                    3 => SDL_MouseButtonFlags.SDL_BUTTON_RMASK,
                    4 => SDL_MouseButtonFlags.SDL_BUTTON_X1MASK,
                    5 => SDL_MouseButtonFlags.SDL_BUTTON_X2MASK,
                    _ => (SDL_MouseButtonFlags)0
                };
                if (isDown)
                    mouseButtons |= mask;
                else
                    mouseButtons &= ~mask;

                MouseButtonChanged?.Invoke(e.button.button, isDown);
            }
            else if (e.Type == SDL_EventType.SDL_EVENT_MOUSE_WHEEL)
            {
                float contribution = e.wheel.integer_y != 0
                    ? MathF.Sign(e.wheel.integer_y)
                    : e.wheel.y * 0.2f;
                wheelAccumulator = Math.Clamp(wheelAccumulator + contribution, -2f, 2f);
            }
            else if (e.Type == SDL_EventType.SDL_EVENT_DROP_FILE)
            {
                var droppedPath = e.drop.GetData();
                if (!string.IsNullOrWhiteSpace(droppedPath))
                {
                    Console.Error.WriteLine("[mac-drop] importing " + droppedPath);
                    FileDropped?.Invoke(droppedPath);
                }
            }
            else if (e.Type == SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST)
            {
                Array.Clear(keys);
                Array.Clear(pressedThisPump);
                Array.Clear(releaseNextPump);
                for (int b = 1; b <= 5; b++)
                    MouseButtonChanged?.Invoke(b, false);
            }
            else if (e.Type == SDL_EventType.SDL_EVENT_WINDOW_RESIZED || e.Type == SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED)
                Resized?.Invoke(null, EventArgs.Empty);
        }

        const float wheelNotch = 1f;
        if (MathF.Abs(wheelAccumulator) >= wheelNotch)
        {
            int delta = wheelAccumulator > 0 ? 120 : -120;
            wheelAccumulator -= MathF.Sign(wheelAccumulator) * wheelNotch;
            MouseWheelScrolled?.Invoke(delta);
        }

        int visibleWidth;
        int visibleHeight;
        SDL_GetWindowSize(Window, &visibleWidth, &visibleHeight);
        if (!nativeWindowWasShown || visibleWidth <= 0 || visibleHeight <= 0)
        {
            if (visibleWidth <= 0 || visibleHeight <= 0)
                SDL_SetWindowSize(Window, 1280, 720);
            void* nativeWindow;
            fixed (byte* cocoaWindowProperty = SDL_PROP_WINDOW_COCOA_WINDOW_POINTER)
                nativeWindow = (void*)SDL_GetPointerProperty(
                    SDL_GetWindowProperties(Window), cocoaWindowProperty, 0);
            MacApplicationActivation.ShowNativeWindow((IntPtr)nativeWindow, 1280, 720);
            nativeWindowWasShown = true;
            SDL_ShowWindow(Window);
            SDL_RaiseWindow(Window);
        }

        var stableMouse = StableMousePosition();
        System.Windows.Forms.Cursor.Position = new Point((int)MathF.Round(stableMouse.X), (int)MathF.Round(stableMouse.Y));
        MousePositionChanged?.Invoke(stableMouse.X, stableMouse.Y);
    }

    internal static bool[] GetKeys() => (bool[])keys.Clone();

    internal static void WarpMouse(Point position)
    {
        EnsureCreated();
        int windowWidth;
        int windowHeight;
        int pixelWidth;
        int pixelHeight;
        SDL_GetWindowSize(Window, &windowWidth, &windowHeight);
        SDL_GetWindowSizeInPixels(Window, &pixelWidth, &pixelHeight);
        float x = position.X;
        float y = position.Y;
        int stableWidth = TargetViewportWidth > 0 ? TargetViewportWidth : pixelWidth;
        int stableHeight = TargetViewportHeight > 0 ? TargetViewportHeight : pixelHeight;
        if (stableWidth > 0 && stableHeight > 0)
        {
            x *= (float)windowWidth / stableWidth;
            y *= (float)windowHeight / stableHeight;
        }
        SDL_WarpMouseInWindow(Window, x, y);
        mouseX = x;
        mouseY = y;
    }

    private static PointF StableMousePosition()
    {
        int windowWidth;
        int windowHeight;
        SDL_GetWindowSize(Window, &windowWidth, &windowHeight);
        int stableWidth = TargetViewportWidth > 0 ? TargetViewportWidth : windowWidth;
        int stableHeight = TargetViewportHeight > 0 ? TargetViewportHeight : windowHeight;
        if (windowWidth <= 0 || windowHeight <= 0 || stableWidth <= 0 || stableHeight <= 0)
            return new PointF(mouseX, mouseY);

        // SDL reports Cocoa pointer positions in window points. The platform
        // input manager expects coordinates in its selected rendering
        // resolution. Keep the visible system pointer and hit testing
        // on the same point for every window/fullscreen resolution.
        return new PointF(
            mouseX * stableWidth / windowWidth,
            mouseY * stableHeight / windowHeight);
    }

    internal static void SetClientSize(Size size)
    {
        EnsureCreated();
        WindowLog($"set-client-size {size.Width}x{size.Height} fullscreen={fullscreenActive}");
        if (!fullscreenActive && size.Width > 0 && size.Height > 0)
            SDL_SetWindowSize(Window, size.Width, size.Height);
    }

    internal static void SetFullscreen(bool fullscreen)
    {
        EnsureCreated();
        var changed = SDL_SetWindowFullscreen(Window, fullscreen);
        if (changed)
            fullscreenActive = fullscreen;
        var synced = SDL_SyncWindowNative(Window) != 0;
        WindowLog($"set-fullscreen value={fullscreen} changed={changed} synced={synced} error={SDL_GetError()}");
    }

    internal static void SetBordered(bool bordered)
    {
        EnsureCreated();
        var changed = SDL_SetWindowBordered(Window, bordered);
        var synced = SDL_SyncWindowNative(Window) != 0;
        WindowLog($"set-bordered value={bordered} changed={changed} synced={synced} error={SDL_GetError()}");
    }

    internal static void SetAlwaysOnTop(bool alwaysOnTop)
    {
        EnsureCreated();
        SDL_SetWindowAlwaysOnTop(Window, alwaysOnTop);
    }

    internal static void SetCursorClip(Rectangle rectangle)
    {
        EnsureCreated();
        SDL_SetWindowMouseGrab(Window, !rectangle.IsEmpty);
    }

    internal static void SetCursorVisible(bool visible)
    {
        if (visible)
            SDL_ShowCursor();
        else
            SDL_HideCursor();
    }

    internal static void SetWindowState(System.Windows.Forms.FormWindowState state)
    {
        EnsureCreated();
        switch (state)
        {
            case System.Windows.Forms.FormWindowState.Minimized: SDL_MinimizeWindow(Window); break;
            case System.Windows.Forms.FormWindowState.Maximized: SDL_MaximizeWindow(Window); break;
            default: SDL_RestoreWindow(Window); break;
        }
    }

    internal static List<Size> DisplayModes()
    {
        EnsureCreated();
        var result = new List<Size>();
        int count = 0;
        var modes = SDL_GetFullscreenDisplayModes(SDL_GetDisplayForWindow(Window), &count);
        if (modes != null)
        {
            for (int i = 0; i < count; i++)
            {
                var mode = modes[i];
                if (mode != null && mode->w >= 640 && mode->h >= 480)
                {
                    var size = new Size(mode->w, mode->h);
                    if (!result.Contains(size))
                        result.Add(size);
                }
            }
            SDL_free((IntPtr)modes);
        }
        result.Sort((left, right) => left.Width != right.Width
            ? left.Width.CompareTo(right.Width)
            : left.Height.CompareTo(right.Height));
        if (result.Count == 0)
            result.Add(ClientSize);
        var desktop = DesktopSize();
        if (desktop.Width >= 640 && desktop.Height >= 480 && !result.Contains(desktop))
            result.Add(desktop);
        result.Sort((left, right) => left.Width != right.Width
            ? left.Width.CompareTo(right.Width)
            : left.Height.CompareTo(right.Height));
        return result;
    }

    internal static Size DesktopSize()
    {
        EnsureCreated();
        var mode = SDL_GetDesktopDisplayMode(SDL_GetDisplayForWindow(Window));
        return mode == null ? ClientSize : new Size(mode->w, mode->h);
    }

    internal static int RefreshRate()
    {
        EnsureCreated();
        var mode = SDL_GetCurrentDisplayMode(SDL_GetDisplayForWindow(Window));
        return mode == null ? 60 : Math.Max(1, (int)MathF.Round(mode->refresh_rate));
    }

    private static int ToVirtualKey(int scancode)
    {
        if (scancode >= 4 && scancode <= 29) return 'A' + scancode - 4;
        if (scancode >= 30 && scancode <= 38) return '1' + scancode - 30;
        if (scancode == 39) return '0';
        if (scancode >= 58 && scancode <= 69) return 112 + scancode - 58;
        return scancode switch
        {
            40 => 13, 41 => 27, 42 => 8, 43 => 9, 44 => 32,
            45 => 189, 46 => 187, 47 => 219, 48 => 221, 49 => 220,
            51 => 186, 52 => 222, 53 => 192, 54 => 188, 55 => 190, 56 => 191,
            57 => 20, 70 => 44, 71 => 145, 72 => 19,
            73 => 45, 74 => 36, 75 => 33, 76 => 46, 77 => 35, 78 => 34,
            79 => 39, 80 => 37, 81 => 40, 82 => 38,
            83 => 144, 84 => 111, 85 => 106, 86 => 109, 87 => 107,
            88 => 13, 89 => 97, 90 => 98, 91 => 99, 92 => 100, 93 => 101,
            94 => 102, 95 => 103, 96 => 104, 97 => 105, 98 => 96, 99 => 110,
            224 => 162, 225 => 160, 226 => 164, 227 => 91,
            228 => 163, 229 => 161, 230 => 165, 231 => 92,
            _ => 0
        };
    }

    internal static Size ClientSize
    {
        get
        {
            EnsureCreated();
            int width;
            int height;
            SDL_GetWindowSizeInPixels(Window, &width, &height);
            if (width <= 0 || height <= 0)
                return new Size(1280, 720);
            return new Size(width, height);
        }
    }

    internal static IntPtr Handle => (IntPtr)Window;
}
