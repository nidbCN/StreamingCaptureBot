using System.Reflection;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;

namespace StreamingCaptureBot.Core.FfMpeg.Net.Utils;

public static class LibraryUtil
{
    public static IntPtr LinuxFfMpegDllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        => FfMpegDllImportResolverCore(libraryName, assembly, searchPath, "so");

    public static IntPtr MacOsFfMpegDllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        => FfMpegDllImportResolverCore(libraryName, assembly, searchPath, "dylib");

    public static IntPtr FfMpegDllImportResolverCore(ReadOnlySpan<char> loadNameSpan, Assembly assembly, DllImportSearchPath? searchPath, string extension)
    {
        var partedIndex = loadNameSpan.IndexOf('-');

        // contains 0 or mult '-', format not ffmpeg library
        if (partedIndex == -1 || loadNameSpan.LastIndexOf('-') != partedIndex)
            return NativeLibrary.Load(loadNameSpan.ToString(), assembly, searchPath);

        var libraryName = loadNameSpan[..partedIndex];

        // library not in map table, not ffmpeg library
        if (!DynamicallyLoadedBindings.LibraryVersionMap.ContainsKey(libraryName.ToString()))
            return NativeLibrary.Load(loadNameSpan.ToString(), assembly, searchPath);

        var versionName = loadNameSpan[(partedIndex + 1)..];

        var styledName = $"{libraryName}.{extension}.{versionName}";
        return NativeLibrary.Load(styledName, assembly, searchPath);
    }
}
