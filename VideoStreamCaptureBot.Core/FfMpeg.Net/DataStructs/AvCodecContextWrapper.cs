using FFmpeg.AutoGen.Abstractions;
using VideoStreamCaptureBot.Core.FfMpeg.Net.Extensions;

namespace VideoStreamCaptureBot.Core.FfMpeg.Net.DataStructs;

public class AvCodecContextWrapper : WrapperBase<AVCodecContext>
{
    public unsafe AvCodecContextWrapper(AVCodec* codec)
        : base(ffmpeg.avcodec_alloc_context3(codec)) { }
    public unsafe AvCodecContextWrapper(AVCodecContext* ctx)
        : base(ctx) { }

    #region 字段

    public AVCodecID CodecId
    {
        get
        {
            AVCodecID id;
            unsafe { id = UnmanagedPointer->codec_id; }
            return id;
        }

        set
        {
            unsafe { UnmanagedPointer->codec_id = value; }
        }
    }

    public AVRational TimeBase
    {
        get
        {
            AVRational timebase;
            unsafe { timebase = UnmanagedPointer->time_base; }
            return timebase;
        }

        set
        {
            unsafe { UnmanagedPointer->time_base = value; }
        }
    }

    public AVRational FrameRate
    {
        get
        {
            AVRational rate;
            unsafe { rate = UnmanagedPointer->framerate; }
            return rate;
        }

        set
        {
            unsafe { UnmanagedPointer->framerate = value; }
        }
    }

    public AVPixelFormat PixelFormat
    {
        get
        {
            AVPixelFormat fmt;
            unsafe { fmt = UnmanagedPointer->pix_fmt; }

            return fmt;
        }

        set
        {
            unsafe { UnmanagedPointer->pix_fmt = value; }
        }
    }

    public long FrameNumber
    {
        get
        {
            unsafe
            {
                return UnmanagedPointer->frame_num;
            }
        }
    }

    #endregion

    #region 方法

    public unsafe void Open(AVCodec* codec, AVDictionary** options)
        => TryOpen(codec, options).ThrowExceptionIfError();

    public unsafe int TryOpen(AVCodec* codec, AVDictionary** options)
        => ffmpeg.avcodec_open2(UnmanagedPointer, codec, null);

    #endregion

    public override string ToString()
    {
        unsafe
        {
            return ffmpeg.avcodec_get_name(CodecId)
                       + $"@0x{(IntPtr)UnmanagedPointer:x16}";
        }
    }

    public override void Dispose()
    {
        unsafe
        {
            var ctx = UnmanagedPointer;
            ffmpeg.avcodec_free_context(&ctx);
        }

        base.Dispose();

        GC.SuppressFinalize(this);
    }
}
