using FFmpeg.AutoGen.Abstractions;

namespace VideoStreamCaptureBot.Core.FfMpeg.Net.DataStructs;

public class DecoderContext : AvCodecContextWrapper
{
    public unsafe DecoderContext(AVCodec* codec) : base(codec)
    {
    }

    public unsafe DecoderContext(AVCodecContext* ctx) : base(ctx)
    {
    }

    public int TrySendPacket(AvPacketWrapper packet)
    {
        unsafe
        {
            return ffmpeg.avcodec_send_packet(UnmanagedPointer, packet.UnmanagedPointer);
        }
    }

    public int TryReceivedFrame(ref AvFrameWrapper frame)
    {
        unsafe
        {
            return ffmpeg.avcodec_receive_frame(UnmanagedPointer, frame.UnmanagedPointer);
        }
    }

    public static DecoderContext Create(string name)
    {
        unsafe
        {
            var enc = ffmpeg.avcodec_find_decoder_by_name(name);
            return Create(enc);
        }
    }

    public static DecoderContext Create(AVCodecID id)
    {
        unsafe
        {
            var enc = ffmpeg.avcodec_find_decoder(id);
            return Create(enc);
        }
    }

    public static unsafe DecoderContext Create(AVCodec* enc)
    {
        if (enc is null)
            throw new ArgumentNullException(nameof(enc));

        var ctx = new DecoderContext(enc);

        ctx.TimeBase = new() { num = 1, den = 25 }; // 设置时间基准
        ctx.FrameRate = new() { num = 25, den = 1 };

        return new(enc);
    }

}
