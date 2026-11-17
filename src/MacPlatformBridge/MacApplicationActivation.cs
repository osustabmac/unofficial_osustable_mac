using System;
using System.Runtime.InteropServices;

internal static class MacApplicationActivation
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        internal double Width;
        internal double Height;

        internal NativeSize(double width, double height)
        {
            Width = width;
            Height = height;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal double X;
        internal double Y;
        internal double Width;
        internal double Height;

        internal NativeRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr object_getClass(IntPtr value);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr class_getName(IntPtr value);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern bool SendInteger(IntPtr receiver, IntPtr selector, nint value);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SendBoolean(IntPtr receiver, IntPtr selector, bool value);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SendSize(IntPtr receiver, IntPtr selector, NativeSize value);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SendObject(IntPtr receiver, IntPtr selector, IntPtr value);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SendRectBoolean(IntPtr receiver, IntPtr selector, NativeRect value, bool display);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern NativeRect GetRect(IntPtr receiver, IntPtr selector);

    [return: MarshalAs(UnmanagedType.I1)]
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern bool GetBoolean(IntPtr receiver, IntPtr selector);

    internal static void Activate()
    {
        var applicationClass = objc_getClass("NSApplication");
        var application = Send(applicationClass, sel_registerName("sharedApplication"));
        // The standalone .NET host entry point runs independently of SDL_main. Complete AppKit's
        // launch lifecycle so ordered NSWindows receive a real window number
        // and are registered with WindowServer.
        Send(application, sel_registerName("finishLaunching"));
        // NSApplicationActivationPolicyRegular makes a bundle started through
        // Finder own a foreground window rather than behaving like a CLI tool.
        SendInteger(application, sel_registerName("setActivationPolicy:"), 0);
        SendBoolean(application, sel_registerName("activateIgnoringOtherApps:"), true);
    }

    internal static void ShowNativeWindow(IntPtr nativeWindow, int width, int height)
    {
        if (nativeWindow == IntPtr.Zero)
            return;

        // SDL's Cocoa window can remain 0x0 when a self-contained .NET binary
        // is started by LaunchServices.  Set the actual NSWindow, rather than
        // relying solely on SDL's deferred window-state update.
        try
        {
            var classNamePointer = class_getName(object_getClass(nativeWindow));
            var className = Marshal.PtrToStringUTF8(classNamePointer) ?? "?";
            System.IO.File.AppendAllText("/tmp/osustab-window.log", $"native object=0x{nativeWindow:x} class={className} requested={width}x{height}\n");
        }
        catch { }
        var size = new NativeSize(width, height);
        SendRectBoolean(nativeWindow, sel_registerName("setFrame:display:"),
            new NativeRect(100, 100, width, height + 28), true);
        SendSize(nativeWindow, sel_registerName("setContentSize:"), size);
        var contentView = Send(nativeWindow, sel_registerName("contentView"));
        if (contentView != IntPtr.Zero)
            SendSize(contentView, sel_registerName("setFrameSize:"), size);
        Send(nativeWindow, sel_registerName("center"));
        Send(nativeWindow, sel_registerName("display"));
        Send(nativeWindow, sel_registerName("orderFrontRegardless"));
        SendObject(nativeWindow, sel_registerName("makeKeyAndOrderFront:"), IntPtr.Zero);
        try
        {
            var frame = GetRect(nativeWindow, sel_registerName("frame"));
            var visible = GetBoolean(nativeWindow, sel_registerName("isVisible"));
            System.IO.File.AppendAllText("/tmp/osustab-window-state.log",
                $"frame={frame.X},{frame.Y} {frame.Width}x{frame.Height} visible={visible}\n");
        }
        catch { }
    }
}
