using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Drawing;

internal static unsafe class MacFontRegistry
{
    private const uint ProcessScope = 1;

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFURLCreateFromFileSystemRepresentation(
        IntPtr allocator, byte* buffer, nint bufferLength, bool isDirectory);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr value);

    [DllImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    private static extern bool CTFontManagerRegisterFontsForURL(IntPtr fontUrl, uint scope, IntPtr error);

    internal static void Register(string resourceName, byte[] fontData)
    {
        if (fontData == null || fontData.Length == 0)
            return;

        var fontDirectory = Path.Combine(AppContext.BaseDirectory, "Fonts");
        Directory.CreateDirectory(fontDirectory);
        var safeName = string.Concat(resourceName.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(fontDirectory, safeName + ".ttf");
        if (!File.Exists(path) || new FileInfo(path).Length != fontData.Length)
            File.WriteAllBytes(path, fontData);

        var utf8Path = Encoding.UTF8.GetBytes(path);
        fixed (byte* pathPointer = utf8Path)
        {
            var url = CFURLCreateFromFileSystemRepresentation(IntPtr.Zero, pathPointer, utf8Path.Length, false);
            if (url == IntPtr.Zero)
                throw new InvalidOperationException($"CoreText could not create a URL for {path}.");
            try
            {
                // Returning false also means that this process registered the
                // same face earlier, which is harmless during a skin reload.
                CTFontManagerRegisterFontsForURL(url, ProcessScope, IntPtr.Zero);
            }
            finally
            {
                CFRelease(url);
            }
        }
    }

    internal static FontFamily ResolveFamily(string resourceName)
    {
        // Resource keys include the face/style while fontconfig exposes the
        // actual family stored in the TTF name table.
        var familyName = resourceName switch
        {
            "FontAwesome" => "FontAwesome",
            var name when name.StartsWith("Aller", StringComparison.Ordinal) => "Aller",
            var name when name.StartsWith("Exo2.0", StringComparison.Ordinal) => "Exo 2.0",
            _ => resourceName
        };

        try
        {
            return new FontFamily(familyName);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[font-family] {resourceName} -> {familyName} failed: {exception.Message}");
            return null;
        }
    }
}
