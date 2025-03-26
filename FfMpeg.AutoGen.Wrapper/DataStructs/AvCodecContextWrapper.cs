using FFmpeg.AutoGen.Abstractions;
using FfMpeg.AutoGen.Wrapper.Extensions;

namespace FfMpeg.AutoGen.Wrapper.DataStructs;

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
            unsafe { return UnmanagedPointer->codec_id; }
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

    public AVRational SampleAspectRatio
    {
        get
        {
            unsafe { return UnmanagedPointer->sample_aspect_ratio; }
        }
    }

    public int Width
    {
        get
        {
            unsafe { return UnmanagedPointer->width; }
        }

        set
        {
            unsafe { UnmanagedPointer->height = value; }
        }
    }

    public int Height
    {
        get
        {
            unsafe { return UnmanagedPointer->height; }
        }

        set
        {
            unsafe { UnmanagedPointer->height = value; }
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
            unsafe { return UnmanagedPointer->pix_fmt; }
        }

        set
        {
            unsafe { UnmanagedPointer->pix_fmt = value; }
        }
    }

    public int ThreadCount
    {
        get
        {
            unsafe { return UnmanagedPointer->thread_count; }
        }

        set
        {
            if (value <= 0)
                unsafe { UnmanagedPointer->thread_count = Environment.ProcessorCount; }
            else
                unsafe { UnmanagedPointer->thread_count = value; }
        }
    }

    public AVDiscard SkipFrame
    {
        get
        {
            unsafe { return UnmanagedPointer->skip_frame; }
        }

        set
        {
            unsafe { UnmanagedPointer->skip_frame = value; }
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

    public CodecFlag Flags
    {
        get
        {
            unsafe { return (CodecFlag)UnmanagedPointer->flags; }
        }

        set
        {
            unsafe { UnmanagedPointer->flags = (int)value; }
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

    [Flags]
    public enum CodecFlag
    {
        AcPred = ffmpeg.AV_CODEC_FLAG_AC_PRED,
        BitExact = ffmpeg.AV_CODEC_FLAG_BITEXACT,
        //ClosedGop = ffmpeg.AV_CODEC_FLAG_CLOSED_GOP, // a 64bit flag
        CopyOpaques = ffmpeg.AV_CODEC_FLAG_COPY_OPAQUE,
        DropChanged = ffmpeg.AV_CODEC_FLAG_DROPCHANGED,
        FrameDuration = ffmpeg.AV_CODEC_FLAG_FRAME_DURATION,
        GlobalHeader = ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER,
        Gray = ffmpeg.AV_CODEC_FLAG_GRAY,
        InterlacedDct = ffmpeg.AV_CODEC_FLAG_INTERLACED_DCT,
        InterlacedMe = ffmpeg.AV_CODEC_FLAG_INTERLACED_ME,
        LoopFilter = ffmpeg.AV_CODEC_FLAG_LOOP_FILTER,
        LowDelay = ffmpeg.AV_CODEC_FLAG_LOW_DELAY,
        M = ffmpeg.AV_CODEC_FLAG_OUTPUT_CORRUPT,
        Pass1 = ffmpeg.AV_CODEC_FLAG_PASS1,
        Pass2 = ffmpeg.AV_CODEC_FLAG_PASS2,
        Psnr = ffmpeg.AV_CODEC_FLAG_PSNR,
        Qpel = ffmpeg.AV_CODEC_FLAG_QPEL,
        QScale = ffmpeg.AV_CODEC_FLAG_QSCALE,
        ReconFrame = ffmpeg.AV_CODEC_FLAG_RECON_FRAME,
        UnAligned = ffmpeg.AV_CODEC_FLAG_UNALIGNED
    }
}
