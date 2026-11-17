using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;

internal static class MacLegacyResources
{
    private static readonly ConcurrentDictionary<(Assembly Assembly, string Name), byte[]> images = new();
    private static readonly ConcurrentDictionary<(Assembly Assembly, string Name), byte[]> byteArrays = new();
    private static readonly byte[] pngStart = { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
    private static readonly byte[] pngEnd = { 0x49, 0x45, 0x4e, 0x44, 0xae, 0x42, 0x60, 0x82 };
    private static readonly byte[] jpegStart = { 0xff, 0xd8, 0xff };
    private static readonly byte[] jpegEnd = { 0xff, 0xd9 };

    internal static Bitmap GetBitmap(Assembly assembly, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var payload = images.GetOrAdd((assembly, name), key => ReadImage(key.Assembly, key.Name) ?? Array.Empty<byte>());
        if (payload.Length == 0)
            return null;

        using var stream = new MemoryStream(payload, writable: false);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    internal static byte[] GetBytes(Assembly assembly, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        var payload = byteArrays.GetOrAdd((assembly, name), key => ReadBytes(key.Assembly, key.Name) ?? Array.Empty<byte>());
        return payload.Length == 0 ? null : payload;
    }

    private static byte[] ReadImage(Assembly assembly, string name)
    {
        foreach (var manifestName in assembly.GetManifestResourceNames().Where(value => value.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(manifestName);
            if (stream == null)
                continue;
            using var reader = new ResourceReader(stream);
            var enumerator = reader.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!string.Equals((string)enumerator.Key, name, StringComparison.Ordinal))
                    continue;
                reader.GetResourceData(name, out _, out var rawData);
                return Extract(rawData, pngStart, pngEnd) ?? Extract(rawData, jpegStart, jpegEnd);
            }
        }
        return null;
    }

    private static byte[] ReadBytes(Assembly assembly, string name)
    {
        foreach (var manifestName in assembly.GetManifestResourceNames().Where(value => value.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(manifestName);
            if (stream == null)
                continue;
            using var reader = new ResourceReader(stream);
            var enumerator = reader.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!string.Equals((string)enumerator.Key, name, StringComparison.Ordinal))
                    continue;
                reader.GetResourceData(name, out var typeName, out var rawData);
                if ((typeName == "ResourceTypeCode.ByteArray" || typeName == "ResourceTypeCode.Stream") && rawData.Length >= 4)
                {
                    int length = BitConverter.ToInt32(rawData, 0);
                    if (length >= 0 && length <= rawData.Length - 4)
                        return rawData.AsSpan(4, length).ToArray();
                }
                return rawData;
            }
        }
        return null;
    }

    private static byte[] Extract(byte[] source, byte[] startMarker, byte[] endMarker)
    {
        var start = source.AsSpan().IndexOf(startMarker);
        if (start < 0)
            return null;
        var relativeEnd = source.AsSpan(start + startMarker.Length).IndexOf(endMarker);
        if (relativeEnd < 0)
            return null;
        var length = startMarker.Length + relativeEnd + endMarker.Length;
        return source.AsSpan(start, length).ToArray();
    }
}
