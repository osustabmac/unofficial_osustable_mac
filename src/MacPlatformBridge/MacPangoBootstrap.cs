using System;
using System.IO;
using System.Runtime.InteropServices;

internal static class MacPangoBootstrap
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateFontMap(int fontType);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetDefaultFontMap(IntPtr fontMap);

    private static IntPtr library;
    private static IntPtr fontMap;

    internal static void Initialize(string engineDirectory)
    {
        library = NativeLibrary.Load(Path.Combine(engineDirectory, "libpangocairo-1.0.0.dylib"));
        var create = Marshal.GetDelegateForFunctionPointer<CreateFontMap>(
            NativeLibrary.GetExport(library, "pango_cairo_font_map_new_for_font_type"));
        var setDefault = Marshal.GetDelegateForFunctionPointer<SetDefaultFontMap>(
            NativeLibrary.GetExport(library, "pango_cairo_font_map_set_default"));

        // Do not use Cairo's Quartz/CoreText font map here.  libgdiplus/Pango
        // hands its descriptor objects to AppKit's menu font cache, where they
        // are not valid NSFontDescriptor instances and crash activation with
        // fontDescriptorWithSize:.  FreeType/fontconfig is isolated from
        // AppKit and is the correct backend for System.Drawing rasterisation.
        fontMap = create(1);
        if (fontMap == IntPtr.Zero)
            throw new InvalidOperationException("Pango could not create a macOS font map.");
        setDefault(fontMap);
        Console.Error.WriteLine($"[mac-fonts] Pango font map ready: 0x{fontMap:x}");
    }
}
