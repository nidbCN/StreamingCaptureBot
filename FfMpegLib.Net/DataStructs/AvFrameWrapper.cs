using FFmpeg.AutoGen.Abstractions;
using FfMpeg.AutoGen.Wrapper.Utils;

namespace FfMpeg.AutoGen.Wrapper.DataStructs;

public class AvFrameWrapper : WrapperBase<AVFrame>
{
    public unsafe AvFrameWrapper()
        : base(ffmpeg.av_frame_alloc()) { }

    public unsafe AvFrameWrapper(AVFrame* frame)
        : base(frame) { }

    public AVPictureType PictureType
    {
        get
        {
            unsafe
            {
                return UnmanagedPointer->pict_type;
            }
        }
    }

    public long PresentationTimeStamp
    {
        get
        {
            unsafe { return UnmanagedPointer->pts; }
        }
    }

    public TimeSpan? GetPresentationTimeSpan(AVRational timebase)
        => TimeSpanUtil.FromFfmpeg(PresentationTimeStamp, timebase);

    public long PacketDecodingTimeStamp
    {
        get
        {
            unsafe { return UnmanagedPointer->pkt_dts; }
        }
    }

    public TimeSpan? GetPacketDecodingTimeSpan(AVRational timebase)
        => TimeSpanUtil.FromFfmpeg(PacketDecodingTimeStamp, timebase);

    public void Reset()
    {
        unsafe
        {
            ffmpeg.av_frame_unref(UnmanagedPointer);
        }
    }

    public override string ToString()
    {
        unsafe
        {
            return $"Frame@0x{UnmanagedPointer->buf.ToArray().GetHashCode():x8}";
        }
    }

    public override void Dispose()
    {
        unsafe
        {
            var frame = UnmanagedPointer;
            ffmpeg.av_frame_free(&frame);
        }

        base.Dispose();

        GC.SuppressFinalize(this);
    }
}