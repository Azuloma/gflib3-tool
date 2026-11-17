// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Reflection;
using System.Runtime.InteropServices;

namespace Nxa.GFlib3.Core.Compression;

/// <summary>Managed entry point for the vendored Oodle Kraken decoder.</summary>
public static class OodleKraken
{
    private const string LibraryName = "Nxa.GFlib3.Native";
    private const int SafeSpace = 64;

    static OodleKraken()
    {
        NativeLibrary.SetDllImportResolver(typeof(OodleKraken).Assembly, ResolveLibrary);
    }

    /// <summary>Decompresses a raw Oodle Kraken stream.</summary>
    /// <exception cref="DllNotFoundException">The native shim could not be located.</exception>
    /// <exception cref="InvalidDataException">The stream did not decode to the expected size.</exception>
    public static byte[] Decompress(ReadOnlySpan<byte> source, int decompressedLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decompressedLength);

        if (decompressedLength == 0)
        {
            return Array.Empty<byte>();
        }

        var compressed = source.ToArray();
        var output = new byte[decompressedLength + SafeSpace];

        int written = DecompressNative(compressed, compressed.Length, output, decompressedLength);
        if (written != decompressedLength)
        {
            throw new InvalidDataException(
                $"Oodle Kraken decompression failed: expected {decompressedLength} bytes, got {written}.");
        }

        var result = new byte[decompressedLength];
        Buffer.BlockCopy(output, 0, result, 0, decompressedLength);
        return result;
    }

    private static IntPtr ResolveLibrary(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(name, LibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        foreach (string candidate in EnumerateCandidates())
        {
            if (File.Exists(candidate))
            {
                return NativeLibrary.Load(candidate);
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        string? configured = Environment.GetEnvironmentVariable("NXA_GF3_NATIVE");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            yield return Path.GetFullPath(configured);
        }

        foreach (string fileName in LibraryFileNames)
        {
            yield return Path.Combine(AppContext.BaseDirectory, fileName);
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            foreach (string fileName in LibraryFileNames)
            {
                yield return Path.Combine(directory.FullName, "artifacts", "native", fileName);
            }

            directory = directory.Parent;
        }
    }

    private static string[] LibraryFileNames =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["Nxa.GFlib3.Native.dll"]
            : ["libNxa.GFlib3.Native.so", "libNxa.GFlib3.Native.dylib"];

    [DllImport(LibraryName, EntryPoint = "nxa_oodle_decompress", CallingConvention = CallingConvention.Cdecl)]
    private static extern int DecompressNative(byte[] src, int srcLength, byte[] dst, int dstLength);
}
