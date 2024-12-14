using System.Numerics;
using FFmpeg.AutoGen.Abstractions;
using StreamingCaptureBot.Core.FfMpeg.Net.Utils;

namespace StreamingCaptureBot.Core.FfMpeg.Net.DataStructs;

public class AvPacketWrapper : WrapperBase<AVPacket>
{
    public unsafe AvPacketWrapper() : this(ffmpeg.av_packet_alloc()) { }

    public unsafe AvPacketWrapper(AVPacket* pointer) : base(pointer) { }

    public int StreamIndex
    {
        get
        {
            unsafe { return UnmanagedPointer->stream_index; }
        }
        set
        {
            unsafe { UnmanagedPointer->stream_index = value; }
        }
    }

    public int Size
    {
        get
        {
            unsafe { return UnmanagedPointer->size; }
        }
    }

    public long PresentationTimeStamp
    {
        get
        {
            unsafe { return UnmanagedPointer->pts; }
        }
    }

    public TimeSpan GetPresentationTimeSpan(AVRational timebase)
        => TimeSpanUtil.FromFfmpeg(PresentationTimeStamp, timebase);

    public long DecodingTimeStamp
    {
        get
        {
            unsafe { return UnmanagedPointer->dts; }
        }
    }

    public TimeSpan GetDecodingTimeSpan(AVRational timebase)
        => TimeSpanUtil.FromFfmpeg(DecodingTimeStamp, timebase);

    public void Reset()
    {
        unsafe
        {
            ffmpeg.av_packet_unref(UnmanagedPointer);
        }
    }

    public override string ToString()
    {
        unsafe
        {
            return $"Packet@0x{UnmanagedPointer->buf->GetHashCode():x16}";
        }
    }

    public override void Dispose()
    {
        unsafe
        {
            var packet = UnmanagedPointer;
            ffmpeg.av_packet_free(&packet);
        }

        base.Dispose();

        GC.SuppressFinalize(this);
    }
}
