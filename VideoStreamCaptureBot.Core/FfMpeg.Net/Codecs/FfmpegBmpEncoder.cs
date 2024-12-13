using FFmpeg.AutoGen.Abstractions;
using VideoStreamCaptureBot.Core.FfMpeg.Net.Extensions;
using VideoStreamCaptureBot.Core.Utils;

namespace VideoStreamCaptureBot.Core.FfMpeg.Net.Codecs;

public class FfmpegBmpEncoder : CodecBase
{
    public FfmpegBmpEncoder(ILogger<FfmpegBmpEncoder> logger, BinarySizeFormatter format) : base(logger, format)
    {
        unsafe
        {
            var codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_BMP);
            CodecCtx = ffmpeg.avcodec_alloc_context3(codec);

            CodecCtx->width = 1920;
            CodecCtx->height = 1080;

            CodecCtx->time_base = new() { num = 1, den = 25 }; // 设置时间基准
            CodecCtx->framerate = new() { num = 25, den = 1 };

            CodecCtx->pix_fmt = AVPixelFormat.AV_PIX_FMT_RGB24;

            CodecCtx->gop_size = 1;
            CodecCtx->time_base = new() { den = 1, num = 1000 };

            CodecCtx->flags |= ffmpeg.AV_CODEC_FLAG_COPY_OPAQUE;

            ffmpeg.avcodec_open2(CodecCtx, codec, null)
                .ThrowExceptionIfError();
        }
    }

    public unsafe void ConvertToRGB24(AVFrame* frame)
    {

    }
}