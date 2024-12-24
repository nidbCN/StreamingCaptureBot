using System.Text.Json;
using FFmpeg.AutoGen.Abstractions;
using FfMpeg.AutoGen.Wrapper.DataStructs;
using FfMpeg.AutoGen.Wrapper.Extensions;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Hosting.Configs;

namespace StreamingCaptureBot.Hosting.Services;

public class FaceMosaicProcessService
    : IImageProcessService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FaceMosaicProcessService> _logger;
    private readonly StreamOption _streamOption;

    private unsafe AVFrame* _rawFrame;
    private unsafe AVFrame* _processFrame;
    private readonly EncoderContext _bmpEncoderCtx;
    //private readonly unsafe AVPacket* _packet;

    public FaceMosaicProcessService(HttpClient httpClient,
        ILogger<FaceMosaicProcessService> logger, IOptions<StreamOption> streamOptions)
    {
        _httpClient = httpClient;
        _logger = logger;
        _streamOption = streamOptions.Value;

        #region 初始化图片编码器
        unsafe
        {
            _bmpEncoderCtx = EncoderContext.Create(AVCodecID.AV_CODEC_ID_WEBP);

            _bmpEncoderCtx.PixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P;
            _bmpEncoderCtx.UnmanagedPointer->gop_size = 1;
            _bmpEncoderCtx.UnmanagedPointer->thread_count = (int)_streamOption.CodecThreads;
            _bmpEncoderCtx.TimeBase = new() { den = 1, num = 1000 };
            _bmpEncoderCtx.UnmanagedPointer->flags |= ffmpeg.AV_CODEC_FLAG_COPY_OPAQUE;
            _bmpEncoderCtx.UnmanagedPointer->width = 1920;
            _bmpEncoderCtx.UnmanagedPointer->height = 1080;

            ffmpeg.av_opt_set(_bmpEncoderCtx.UnmanagedPointer->priv_data, "preset", "photo", ffmpeg.AV_OPT_SEARCH_CHILDREN)
                .ThrowExceptionIfError();

            _bmpEncoderCtx.Open(_bmpEncoderCtx.UnmanagedPointer->codec, null);
        }

        #endregion
    }

    public unsafe bool TryProcessImage(AVFrame* inputFrame, out AVFrame* outputFrame)
    {
        _rawFrame = inputFrame;
        _processFrame = ConvertToPixelFormatUnsafe(_rawFrame, AVPixelFormat.AV_PIX_FMT_RGB24);
        // 转换到 RGB24

        // 转换到 -1:640
        // 编码 BMP

        // 推理
        // 打码
        throw new NotImplementedException();
    }

    /// <summary>
    /// 转换像素格式
    /// </summary>
    /// <param name="inputFrame">输入帧（原格式）</param>
    /// <param name="targetPixelFormat">目标像素格式</param>
    /// <returns>返回新格式的帧（与输入帧不是同一引用）</returns>
    /// <exception cref="NotImplementedException"></exception>
    public unsafe AVFrame* ConvertToPixelFormatUnsafe(AVFrame* inputFrame, AVPixelFormat targetPixelFormat)
    {
        throw new NotImplementedException();
    }


    public unsafe AVFrame* ScaleFrameUnsafe(AVFrame* inputFrame, int targetWidth, int targetHeight)
    {

        throw new NotImplementedException();
    }

    public async Task<IList<FaceBox>> PredictFaces(Stream stream)
        => await JsonSerializer.DeserializeAsync<IList<FaceBox>>(
            await (await _httpClient.PostAsync(
                    new Uri("http://localhost:8080"), new StreamContent(stream))
                ).Content.ReadAsStreamAsync())
           ?? [];

    public unsafe AVFrame* MosaicFrameUnsafe(AVFrame* rawFrame, IList<FaceBox> mosaics)
    {
        throw new NotImplementedException();
    }
}

public struct FaceBox
{
    public uint X { get; set; }
    public uint Y { get; set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
}
