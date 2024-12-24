using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;

namespace FfMpeg.AutoGen.Wrapper.Extensions;

public static class FfMpegExtension
{
    public static string? ErrorCodeToString(int error)
    {
        const int bufferSize = 1024;
        unsafe
        {
            var buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(error, buffer, bufferSize);
            return Marshal.PtrToStringAnsi((nint)buffer);
        }
    }

    public static int ThrowExceptionIfError(this int error)
        => error < 0
            ? throw new ApplicationException(ErrorCodeToString(error))
            : error;
}
