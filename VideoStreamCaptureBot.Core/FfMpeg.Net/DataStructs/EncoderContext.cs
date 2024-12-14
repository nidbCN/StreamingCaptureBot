using FFmpeg.AutoGen.Abstractions;

namespace VideoStreamCaptureBot.Core.FfMpeg.Net.DataStructs;

public class EncoderContext : AvCodecContextWrapper
{
    public unsafe EncoderContext(AVCodec* codec) : base(codec)
    {
    }

    public unsafe EncoderContext(AVCodecContext* ctx) : base(ctx)
    {
    }

    public new AVRational SampleAspectRatio
    {
        get => base.SampleAspectRatio;

        set
        {
            unsafe { UnmanagedPointer->sample_aspect_ratio = value; }
        }
    }

    public static EncoderContext Create(string name)
    {
        unsafe
        {
            var enc = ffmpeg.avcodec_find_decoder_by_name(name);
            return Create(enc);
        }
    }

    public static EncoderContext Create(AVCodecID id)
    {
        unsafe
        {
            var enc = ffmpeg.avcodec_find_encoder(id);
            return Create(enc);
        }
    }

    public static unsafe EncoderContext Create(AVCodec* enc)
    {
        if (enc is null)
            throw new ArgumentNullException(nameof(enc));

        var ctx = new EncoderContext(enc);

        ctx.TimeBase = new() { num = 1, den = 25 }; // 设置时间基准
        ctx.FrameRate = new() { num = 25, den = 1 };

        return new(enc);
    }
}
