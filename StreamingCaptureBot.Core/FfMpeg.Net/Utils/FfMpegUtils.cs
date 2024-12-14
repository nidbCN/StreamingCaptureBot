using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using StreamingCaptureBot.Core.FfMpeg.Net.DataStructs;
using StreamingCaptureBot.Core.FfMpeg.Net.Extensions;
using StreamingCaptureBot.Core.Services;

namespace StreamingCaptureBot.Core.FfMpeg.Net.Utils;

public static class FfMpegUtils
{
    public static unsafe void WriteToStream(Stream stream, AVPacket* packet)
    {
        var buffer = new byte[packet->size];
        Marshal.Copy((nint)packet->data, buffer, 0, packet->size);
        stream.Write(buffer, 0, packet->size);
    }
}