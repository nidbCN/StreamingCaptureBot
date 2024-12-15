using FFmpeg.AutoGen.Abstractions;
using StreamingCaptureBot.Core.FfMpeg.Net.Extensions;
using StreamingCaptureBot.Core.Utils;

namespace StreamingCaptureBot.Core.FfMpeg.Net.Codecs;

public class FfmpegLibWebpEncoder : CodecBase
{
    public FfmpegLibWebpEncoder(ILogger<FfmpegLibWebpEncoder> logger, BinarySizeFormatter format) : base(logger, format)
    {
        unsafe
        {
            var codec = ffmpeg.avcodec_find_encoder_by_name("libwebp");
            CodecCtx = ffmpeg.avcodec_alloc_context3(codec);

            CodecCtx->width = 1920;
            CodecCtx->height = 1080;

            CodecCtx->time_base = new() { num = 1, den = 25 }; // 设置时间基准
            CodecCtx->framerate = new() { num = 25, den = 1 };

            CodecCtx->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

            CodecCtx->gop_size = 1;
            CodecCtx->time_base = new() { den = 1, num = 1000 };

            CodecCtx->flags |= ffmpeg.AV_CODEC_FLAG_COPY_OPAQUE;

            AVDictionary* openOptions = null;
            ffmpeg.av_dict_set(&openOptions, "preset", "photo", 0);

            //ffmpeg.av_opt_set(_codecCtx->priv_data,
            //        "preset", "photo", ffmpeg.AV_OPT_SEARCH_CHILDREN)
            //    .ThrowExceptionIfError();

            ffmpeg.avcodec_open2(CodecCtx, codec, &openOptions)
                .ThrowExceptionIfError();
        }
    }

    public override void Dispose()
    {
        unsafe
        {
            var encoderCtx = CodecCtx;
            ffmpeg.avcodec_free_context(&encoderCtx);
        }

        GC.SuppressFinalize(this);
        base.Dispose();
    }
}