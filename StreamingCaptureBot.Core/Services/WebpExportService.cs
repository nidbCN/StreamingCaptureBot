using FFmpeg.AutoGen.Abstractions;
using FfMpegLib.Net.DataStructs;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Core.Bots.LagrangeBot.Extensions;
using StreamingCaptureBot.Core.Configs;

namespace StreamingCaptureBot.Core.Services;

public class WebpExportService : IDisposable
{
    private readonly ILogger<WebpExportService> _logger;
    private readonly StreamOption _streamOption;

    private readonly EncoderContext _webpEncoderCtx;

    public WebpExportService(ILogger<WebpExportService> logger, IOptions<StreamOption> streamOptions)
    {
        _logger = logger;
        _streamOption = streamOptions.Value;
        #region 初始化图片编码器

        unsafe
        {
            _webpEncoderCtx = EncoderContext.Create(AVCodecID.AV_CODEC_ID_WEBP);

            _webpEncoderCtx.PixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P;
            _webpEncoderCtx.UnmanagedPointer->gop_size = 1;
            _webpEncoderCtx.UnmanagedPointer->thread_count = (int)_streamOption.CodecThreads;
            _webpEncoderCtx.TimeBase = new() { den = 1, num = 1000 };
            _webpEncoderCtx.UnmanagedPointer->flags |= ffmpeg.AV_CODEC_FLAG_COPY_OPAQUE;
            //config.Value->width = StreamWidth;
            //config.Value->height = StreamHeight;

            //ffmpeg.av_opt_set(config.Value->priv_data, "lossless", "0", ffmpeg.AV_OPT_SEARCH_CHILDREN)
            //  .ThrowExceptionIfError();
            //ffmpeg.av_opt_set(config.Value->priv_data, "compression_level", "4", ffmpeg.AV_OPT_SEARCH_CHILDREN)
            //  .ThrowExceptionIfError();
            ffmpeg.av_opt_set(_webpEncoderCtx.UnmanagedPointer->priv_data, "quality", "80", ffmpeg.AV_OPT_SEARCH_CHILDREN)
                .ThrowExceptionIfError();
            ffmpeg.av_opt_set(_webpEncoderCtx.UnmanagedPointer->priv_data, "preset", "photo", ffmpeg.AV_OPT_SEARCH_CHILDREN)
                .ThrowExceptionIfError();

            _webpEncoderCtx.Open(_webpEncoderCtx.UnmanagedPointer->codec, null);
        }

        #endregion
    }

    public unsafe MemoryStream OpenWebpStreamUnsafe(AVFrame* inputFrame)
    {
        throw new NotImplementedException();
    }


    public void Dispose()
    {
        _webpEncoderCtx.Dispose();
    }
}
