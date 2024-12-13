using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using VideoStreamCaptureBot.Core.FfMpeg.Net.DataStructs;
using VideoStreamCaptureBot.Core.FfMpeg.Net.Extensions;
using VideoStreamCaptureBot.Core.Services;

namespace VideoStreamCaptureBot.Core.FfMpeg.Net.Utils;

public static class FfMpegUtils
{
    public static unsafe void WriteToStream(Stream stream, AVPacket* packet)
    {
        var buffer = new byte[packet->size];
        Marshal.Copy((nint)packet->data, buffer, 0, packet->size);
        stream.Write(buffer, 0, packet->size);
    }
}