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
    internal static event EventHandler Resized;

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

        // SDL3 creates Cocoa windows hidden.  A terminal launch happened to
        // expose the window through process activation, while LaunchServices
        // kept its native NSWindow at 0x0.  Apply the stable client size and
        // explicitly show it before asking AppKit to activate the application.
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
        ConfigureFormsBridge();
        Console.Error.WriteLine("[mac-host] SDL window created and shown");
        Console.Error.WriteLine("[mac-host] SDL GL context current");
        OpenTkContext = AttachOpenTkToCurrentSdlContext();
        Console.Error.WriteLine("[mac-host] OpenTK entry points loaded");
    }

    private static void ConfigureFormsBridge()
    {
        var bridge = typeof(System.Windows.Forms.Form).Assembly.GetType("System.Windows.Forms.MacFormsBridge", true)!;
        bridge.GetField("SetClientSize")!.SetValue(null, (Action<Size>)SetClientSize);
        bridge.GetField("SetWindowState")!.SetValue(null, (Action<System.Windows.Forms.FormWindowState>)SetWindowState);
        bridge.GetField("SetFormBorderStyle")!.SetValue(null, (Action<System.Windows.Forms.FormBorderStyle>)(style =>
        {
            bool windowed = style != System.Windows.Forms.FormBorderStyle.None;
            SetFullscreen(!windowed);
            SetBordered(windowed);
        }));
        bridge.GetField("SetTopMost")!.SetValue(null, (Action<bool>)SetAlwaysOnTop);
        bridge.GetField("SetCursorClip")!.SetValue(null, (Action<Rectangle>)SetCursorClip);
        bridge.GetField("SetCursorVisible")!.SetValue(null, (Action<bool>)SetCursorVisible);
        bridge.GetField("GetScreenBounds")!.SetValue(null, (Func<Rectangle>)(() =>
        {
            var size = DesktopSize();
            return new Rectangle(0, 0, size.Width, size.Height);
        }));
    }

    private static GraphicsContext AttachOpenTkToCurrentSdlContext()
    {
        // OpenTK 1.1's normal macOS bootstrap uses its retired Carbon/Cocoa
        // window backend. Stable only needs its GL binding table, so attach the
        // existing SDL context directly without constructing that old backend.
        var handle = new ContextHandle((IntPtr)Context);
        GraphicsContext.GetAddressDelegate getAddress = name => SDL_GL_GetProcAddress(name);
        GraphicsContext.GetCurrentContextDelegate getCurrent = () =>
            new ContextHandle((IntPtr)SDL_GL_GetCurrentContext());

        var dummyType = typeof(GraphicsContext).Assembly.GetType("OpenTK.Platform.Dummy.DummyGLContext", throwOnError: true)!;
        var dummy = (IGraphicsContext)Activator.CreateInstance(
            dummyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { handle, getAddress },
            culture: null)!;

        var wrapper = (GraphicsContext)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsContext));
        typeof(GraphicsContext).GetField("implementation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wrapper, dummy);
        typeof(GraphicsContext).GetField("GetCurrentContext", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, getCurrent);

        var contexts = (IDictionary)typeof(GraphicsContext)
            .GetField("available_contexts", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
        contexts.Add(handle, wrapper);
        dummy.LoadAll();
        return wrapper;
    }

    internal static void PumpEvents()
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (releaseNextPump[i])
            {
                keys[i] = false;
                releaseNextPump[i] = false;
            }
            pressedThisPump[i] = false;
        }

        SDL_Event e;
        while (SDL_PollEvent(&e))
        {
            if (e.Type == SDL_EventType.SDL_EVENT_QUIT || e.Type == SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED)
                Closing = true;
            else if (e.Type == SDL_EventType.SDL_EVENT_KEY_DOWN || e.Type == SDL_EventType.SDL_EVENT_KEY_UP)
            {
                int virtualKey = ToVirtualKey((int)e.key.scancode);
                if ((uint)virtualKey < keys.Length)
                {
                    if (e.Type == SDL_EventType.SDL_EVENT_KEY_DOWN)
                    {
                        keys[virtualKey] = true;
                        pressedThisPump[virtualKey] = true;
                    }
                    else if (pressedThisPump[virtualKey])
                    {
                        // A complete tap can arrive between two stable update
                        // frames (especially while typing into search). Keep it
                        // visible for one snapshot, then release next frame.
                        releaseNextPump[virtualKey] = true;
                    }
                    else
                    {
                        keys[virtualKey] = false;
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
                var state = e.Type == SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN
                    ? global::Microsoft.Xna.Framework.Input.ButtonState.Pressed
                    : global::Microsoft.Xna.Framework.Input.ButtonState.Released;
                var mask = e.button.button switch
                {
                    1 => SDL_MouseButtonFlags.SDL_BUTTON_LMASK,
                    2 => SDL_MouseButtonFlags.SDL_BUTTON_MMASK,
                    3 => SDL_MouseButtonFlags.SDL_BUTTON_RMASK,
                    4 => SDL_MouseButtonFlags.SDL_BUTTON_X1MASK,
                    5 => SDL_MouseButtonFlags.SDL_BUTTON_X2MASK,
                    _ => (SDL_MouseButtonFlags)0
                };
                if (e.Type == SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN)
                    mouseButtons |= mask;
                else
                    mouseButtons &= ~mask;
                switch (e.button.button)
                {
                    case 1: Type_020008BA.Method_9EC203E165595DF1(state); break;
                    case 2: Type_020008BA.Method_E53C61968C9E9EFE(state); break;
                    case 3: Type_020008BA.Method_4346DA7402963C0B(state); break;
                    case 4: Type_020008BA.Method_453CA1626B5CAF2A(state); break;
                    case 5: Type_020008BA.Method_0AA9369E9D0EE9B7(state); break;
                }
            }
            else if (e.Type == SDL_EventType.SDL_EVENT_MOUSE_WHEEL)
            {
                // Cocoa trackpads emit many precise wheel fragments. Feeding
                // every fragment as a complete WinForms notch makes stable's
                // lists jump several sections at once. Accumulate fragments,
                // cap momentum and emit at most one notch per stable frame.
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
                    Type_020003F1.Method_70F8C66B9F100C67(new[] { droppedPath });
                }
            }
            else if (e.Type == SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST)
            {
                Array.Clear(keys);
                Array.Clear(pressedThisPump);
                Array.Clear(releaseNextPump);
                Type_020008BA.Method_9EC203E165595DF1(global::Microsoft.Xna.Framework.Input.ButtonState.Released);
                Type_020008BA.Method_4346DA7402963C0B(global::Microsoft.Xna.Framework.Input.ButtonState.Released);
                Type_020008BA.Method_E53C61968C9E9EFE(global::Microsoft.Xna.Framework.Input.ButtonState.Released);
                Type_020008BA.Method_453CA1626B5CAF2A(global::Microsoft.Xna.Framework.Input.ButtonState.Released);
                Type_020008BA.Method_0AA9369E9D0EE9B7(global::Microsoft.Xna.Framework.Input.ButtonState.Released);
            }
            else if (e.Type == SDL_EventType.SDL_EVENT_WINDOW_RESIZED || e.Type == SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED)
                Resized?.Invoke(null, EventArgs.Empty);
        }

        const float wheelNotch = 1f;
        if (MathF.Abs(wheelAccumulator) >= wheelNotch)
        {
            int delta = wheelAccumulator > 0 ? 120 : -120;
            wheelAccumulator -= MathF.Sign(wheelAccumulator) * wheelNotch;
            Type_020008BA.Method_6DA68EAD2C5AE515(
                null,
                new System.Windows.Forms.MouseEventArgs(
                    System.Windows.Forms.MouseButtons.None,
                    0,
                    (int)mouseX,
                    (int)mouseY,
                    delta));
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

        // The original primary mouse device reads WinForms Cursor.Position on
        // every update. Keep the compatibility property synchronised with SDL;
        // feeding only Type_020008BA is insufficient because that device runs
        // later. Window-relative SDL events avoid mixing Cocoa screen points
        // with OpenGL drawable pixels.
        var stableMouse = StableMousePosition();
        System.Windows.Forms.Cursor.Position = new Point((int)MathF.Round(stableMouse.X), (int)MathF.Round(stableMouse.Y));
        Type_020008BA.Method_1F3BC0EC5236C7B5(stableMouse);
        Type_020008BA.Method_9EC203E165595DF1((mouseButtons & SDL_MouseButtonFlags.SDL_BUTTON_LMASK) != 0
            ? global::Microsoft.Xna.Framework.Input.ButtonState.Pressed
            : global::Microsoft.Xna.Framework.Input.ButtonState.Released);
        Type_020008BA.Method_4346DA7402963C0B((mouseButtons & SDL_MouseButtonFlags.SDL_BUTTON_RMASK) != 0
            ? global::Microsoft.Xna.Framework.Input.ButtonState.Pressed
            : global::Microsoft.Xna.Framework.Input.ButtonState.Released);
        Type_020008BA.Method_E53C61968C9E9EFE((mouseButtons & SDL_MouseButtonFlags.SDL_BUTTON_MMASK) != 0
            ? global::Microsoft.Xna.Framework.Input.ButtonState.Pressed
            : global::Microsoft.Xna.Framework.Input.ButtonState.Released);
        Type_020008BA.Method_453CA1626B5CAF2A((mouseButtons & SDL_MouseButtonFlags.SDL_BUTTON_X1MASK) != 0
            ? global::Microsoft.Xna.Framework.Input.ButtonState.Pressed
            : global::Microsoft.Xna.Framework.Input.ButtonState.Released);
        Type_020008BA.Method_0AA9369E9D0EE9B7((mouseButtons & SDL_MouseButtonFlags.SDL_BUTTON_X2MASK) != 0
            ? global::Microsoft.Xna.Framework.Input.ButtonState.Pressed
            : global::Microsoft.Xna.Framework.Input.ButtonState.Released);
    }

    internal static Type_020009B4 KeyboardState()
    {
        Type_020009B4 state = default;
        for (int i = 0; i < keys.Length; i++)
            if (keys[i])
                state.Method_CFDE64C50BC5F53D(i);
        return state;
    }

    internal static void WarpMouse(global::Microsoft.Xna.Framework.Point position)
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
        int stableWidth = Type_020003F1.Field_743F03B7E62C2534?.Field_07893BA38B4E055D ?? pixelWidth;
        int stableHeight = Type_020003F1.Field_743F03B7E62C2534?.Field_133E6DE927CCDCDB ?? pixelHeight;
        if (stableWidth > 0 && stableHeight > 0)
        {
            x *= (float)windowWidth / stableWidth;
            y *= (float)windowHeight / stableHeight;
        }
        SDL_WarpMouseInWindow(Window, x, y);
        mouseX = x;
        mouseY = y;
    }

    private static global::Microsoft.Xna.Framework.Vector2 StableMousePosition()
    {
        int windowWidth;
        int windowHeight;
        SDL_GetWindowSize(Window, &windowWidth, &windowHeight);
        int stableWidth = Type_020003F1.Field_743F03B7E62C2534?.Field_07893BA38B4E055D ?? windowWidth;
        int stableHeight = Type_020003F1.Field_743F03B7E62C2534?.Field_133E6DE927CCDCDB ?? windowHeight;
        if (windowWidth <= 0 || windowHeight <= 0 || stableWidth <= 0 || stableHeight <= 0)
            return new global::Microsoft.Xna.Framework.Vector2(mouseX, mouseY);

        // SDL reports Cocoa pointer positions in window points. The recovered
        // stable input manager expects coordinates in its selected rendering
        // resolution. Keep the visible system pointer and osu!'s hit testing
        // on the same point for every window/fullscreen resolution.
        return new global::Microsoft.Xna.Framework.Vector2(
            mouseX * stableWidth / windowWidth,
            mouseY * stableHeight / windowHeight);
    }

    internal static void SetClientSize(Size size)
    {
        EnsureCreated();
        WindowLog($"set-client-size {size.Width}x{size.Height} fullscreen={fullscreenActive}");
        // In exclusive fullscreen the selected SDL display mode already owns
        // the drawable size. Cocoa applies the transition asynchronously; a
        // subsequent SDL_SetWindowSize cancels the initial fullscreen layout
        // until the user minimises/restores the app.
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

public unsafe class Type_02000155 : System.Windows.Forms.Form
{
    private readonly GraphicsMode graphicsMode;
    private readonly GraphicsContextFlags flags;
    private bool swapIntervalEnabled;

    public Type_02000155() : this(GraphicsMode.Default) { }
    public Type_02000155(GraphicsMode arg0) : this(arg0, 1, 0, (GraphicsContextFlags)0) { }
    public Type_02000155(GraphicsMode arg0, int arg1, int arg2, GraphicsContextFlags arg3)
    {
        graphicsMode = arg0;
        flags = arg3;
        MacStableWindow.EnsureCreated();
        OnHandleCreated(EventArgs.Empty);
    }

    public float Property_C064D6ED49A4E91C => (float)MacStableWindow.ClientSize.Width / MacStableWindow.ClientSize.Height;
    public bool Property_B733472457CE233F
    {
        get => swapIntervalEnabled;
        set
        {
            swapIntervalEnabled = value;
            SDL_GL_SetSwapInterval(value ? 1 : 0);
        }
    }

    public static bool Method_AC4455A81A77DB7B() => false;
    public static bool Method_15ADBDC8AF6C704F() => false;
    public static bool Method_C7581C449412EA87() => false;
    public static bool Method_E856356208970006() => false;
    protected override void OnHandleCreated(EventArgs arg0) { }
    protected override void OnHandleDestroyed(EventArgs arg0) { }
    protected override void OnResize(EventArgs arg0) { }
    protected override void OnParentChanged(EventArgs arg0) { }
    public void Method_3705DFBFFC823149() { }
    public virtual void Method_705729788A32FF5A() => SDL_GL_SwapWindow(MacStableWindow.Window);
    public void Method_D83AD56CF4AFC3E5() => SDL_GL_MakeCurrent(MacStableWindow.Window, MacStableWindow.Context);
    public bool Method_A8BD44AC685B6566() => true;
    public IGraphicsContext Method_DF881945E3BAD5E6() => MacStableWindow.OpenTkContext;
    public GraphicsMode Method_0BF515666BFDA876() => graphicsMode;
    public GraphicsContextFlags Method_A71A81BEB96B2443() => flags;
    public IWindowInfo Method_A0925BFFD2835E23() => null;
    public Bitmap Method_C8FC5275A7574849() => null;
    public static void Method_875FAA57BA448069() { }
    protected override void Dispose(bool arg0) { base.Dispose(arg0); }
}

public class Type_0200071C : Type_02000943
{
    public Type_0200071C(GraphicsContextFlags arg0) : base(GraphicsMode.Default, 2, 0, arg0) { }
}

public class Type_0200017F : Type_0200071C
{
    private event EventHandler activated;
    private event EventHandler deactivated;
    private event EventHandler screenChanged;
    private event EventHandler painted;

    public Type_0200017F(GraphicsContextFlags arg0) : base(arg0)
    {
        MacStableWindow.Resized += (_, _) => screenChanged?.Invoke(this, EventArgs.Empty);
    }

    internal global::Microsoft.Xna.Framework.Rectangle Method_06C0E081EAE4E419()
    {
        var size = MacStableWindow.ClientSize;
        return new global::Microsoft.Xna.Framework.Rectangle(0, 0, size.Width, size.Height);
    }
    public bool Method_B90CE2E2501785A5() => MacStableWindow.ClientSize.Width == 0 || MacStableWindow.ClientSize.Height == 0;
    internal void Method_A683FE04576DCDE9(EventHandler arg0) => activated += arg0;
    internal void Method_F8A0E194030D5536(EventHandler arg0) => activated -= arg0;
    internal void Method_0DB079E9AEC4C593(EventHandler arg0) => deactivated += arg0;
    internal void Method_D583A863BFF4D852(EventHandler arg0) => deactivated -= arg0;
    internal void Method_C2C8BEAEFD0C040B(EventHandler arg0) => screenChanged += arg0;
    internal void Method_142D985F27686BAA(EventHandler arg0) => screenChanged -= arg0;
    internal void Method_F807D0386520A06D(EventHandler arg0) => painted += arg0;
    internal void Method_3E787CF36870A9A4(EventHandler arg0) => painted -= arg0;
    public void Method_0D8644759F5F257D() { }
    internal void RaisePaint() => painted?.Invoke(this, EventArgs.Empty);
}

public class Type_020001DE : Type_020007D5
{
    private readonly Type_0200017F window;
    public Type_020001DE(GraphicsContextFlags arg0) { window = new Type_0200017F(arg0); }
    public Type_0200017F Method_80271D52C5F752A0() => window;
    public override global::Microsoft.Xna.Framework.Rectangle Method_06C0E081EAE4E419() => window.Method_06C0E081EAE4E419();
    public override bool Method_B90CE2E2501785A5() => window.Method_B90CE2E2501785A5();
    public override IntPtr Method_B4467C7077FA7B4F() => MacStableWindow.Handle;
    internal void Method_551CD80A97C82C55() => MacStableWindow.Closing = true;
    protected override void Method_0B4F69E2545A1AEF(string arg0) { }
}

public class Type_02000A00 : Type_0200039A
{
    private bool closing;
    public Type_02000A00(Type_020000DC arg0, GraphicsContextFlags arg1)
    {
        Method_968C7402F2198F0A(new Type_020001DE(arg1));
        Field_A98DF7014218BEB2 = new global::Type_020004D1<bool>(() => true, 50, arg2: false);
    }
    public override Type_02000943 Method_9EFDEC5F5F24CF16() => ((Type_020001DE)Method_8067FEC2680DE865()).Method_80271D52C5F752A0();
    internal override void Method_BFFCF4B0DFFCAD28() => closing = true;
    internal override void Method_9DF70D9B8A3C67D9()
    {
        while (!closing && !MacStableWindow.Closing)
        {
            MacStableWindow.PumpEvents();
            Method_13573B0425C56863(this, EventArgs.Empty);
        }
        Method_342F23E0F48533D5(this, EventArgs.Empty);
    }
}

public static unsafe class Type_02000830
{
    public static bool Method_DF2537D474E338A6(int arg0, int arg1, int? arg2, bool arg3)
    {
        MacStableWindow.EnsureCreated();
        if (arg0 <= 0 || arg1 <= 0)
        {
            arg0 = 1280;
            arg1 = 720;
        }
        var display = SDL_GetDisplayForWindow(MacStableWindow.Window);
        SDL_DisplayMode closest = default;
        float refresh = arg2.HasValue ? arg2.Value : 0;
        if (!SDL_GetClosestFullscreenDisplayMode(display, arg0, arg1, refresh, true, &closest))
            return false;
        if (arg3)
            return true;
        var changed = SDL_SetWindowFullscreenMode(MacStableWindow.Window, &closest);
        MacStableWindow.WindowLog($"set-display-mode requested={arg0}x{arg1} closest={closest.w}x{closest.h} changed={changed} error={SDL_GetError()}");
        return changed;
    }

    public static bool Method_B9628CDFB4BBE5EA() => true;

    internal static List<Size> Method_52E32ACBEB604D60()
    {
        var current = MacStableWindow.ClientSize;
        return MacStableWindow.DisplayModes();
    }

    internal static Size Method_04F45C8F8C057BDA() => MacStableWindow.DesktopSize();
    internal static int Method_D827D50AAC670BEF() => MacStableWindow.RefreshRate();
    internal static Size Method_989CCECD1BA1FE45() => MacStableWindow.DesktopSize();
    internal static Point Method_462F2FEA162A1F6A() => Point.Empty;
}

// Windows stable sampled a 256-entry Win32 virtual-key bitmap. SDL event to
// virtual-key translation is supplied by the macOS input adapter; returning an
// empty snapshot during early construction also replaces GetKeyboardState.
public static class Type_020008E9
{
    public static Type_020009B4 Method_D5D138D768DC6EE2() => MacStableWindow.KeyboardState();
}

public static class Type_020008F2
{
    internal static float Method_CAF231F827B68346(System.Windows.Forms.Control arg0, bool arg1) => 96f;
}
