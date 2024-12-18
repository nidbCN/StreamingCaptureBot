using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;

namespace FfMpegLib.Net.Extensions;

public static class FfMpegExtension
{
    public static unsafe string? av_strerror(int error)
    {
        const int bufferSize = 1024;
        var buffer = stackalloc byte[bufferSize];
        ffmpeg.av_strerror(error, buffer, bufferSize);
        var message = Marshal.PtrToStringAnsi((nint)buffer);
        return message;
    }

    public static int ThrowExceptionIfError(this int error)
    {
        if (error < 0) throw new ApplicationException(av_strerror(error));
        return error;
    }
}
