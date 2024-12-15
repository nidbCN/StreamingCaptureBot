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

    public static IntPtr FfMpegDllImportResolverCore(string libraryName, Assembly assembly, DllImportSearchPath? searchPath, string extension)
    {
        var libraryNameSpan = libraryName.AsSpan();
        var partedIndex = libraryNameSpan.IndexOf('-');

        if (libraryNameSpan.LastIndexOf('-') != partedIndex)
        {
            // format not ffmpeg library
            return NativeLibrary.Load(libraryName, assembly, searchPath);
        }

        var pureName = libraryNameSpan[..partedIndex];

        if (!DynamicallyLoadedBindings.LibraryVersionMap.ContainsKey(pureName.ToString()))
        {
            // not ffmpeg library
            return NativeLibrary.Load(libraryName, assembly, searchPath);
        }

        var versionName = libraryNameSpan[(partedIndex + 1)..];

        var styledName = $"{pureName}.{extension}.{versionName}";
        return NativeLibrary.Load(styledName, assembly, searchPath);
    }
}
