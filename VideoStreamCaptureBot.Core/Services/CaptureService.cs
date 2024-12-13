using FFmpeg.AutoGen.Abstractions;
using Microsoft.Extensions.Options;
using VideoStreamCaptureBot.Core.Configs;
using VideoStreamCaptureBot.Core.FfMpeg.Net.Codecs;
using VideoStreamCaptureBot.Core.FfMpeg.Net.DataStructs;
using VideoStreamCaptureBot.Core.FfMpeg.Net.Extensions;
using VideoStreamCaptureBot.Core.Utils;

namespace VideoStreamCaptureBot.Core.Services;

public sealed class CaptureService : IDisposable
{
    private readonly ILogger<CaptureService> _logger;
    private readonly BinarySizeFormatter _formatter;
    private readonly StreamOption _streamOption;
    private readonly FfmpegLibWebpEncoder _encoder;

    private readonly DecoderContext _decoderCtx;

    private unsafe AVFormatContext* _inputFormatCtx;
    private readonly unsafe AVDictionary* _openOptions = null;

    private readonly AvFrameWrapper _frame = new();
    private readonly AvFrameWrapper _webpOutputFrame = new();
    private readonly AvPacketWrapper _packet = new();

    private readonly int _streamIndex;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public byte[]? LastCapturedImage { get; private set; }
    public DateTime LastCaptureTime { get; private set; }

    public string StreamDecoderName { get; }
    public AVPixelFormat StreamPixelFormat { get; }
    public int StreamHeight { get; private set; }
    public int StreamWidth { get; private set; }

    public unsafe CaptureService(ILogger<CaptureService> logger, IOptions<StreamOption> option, FfmpegLibWebpEncoder encoder, BinarySizeFormatter formatter)
    {
        _logger = logger;
        _streamOption = option.Value;
        _encoder = encoder;
        _formatter = formatter;

        if (_streamOption.Url is null)
            throw new ArgumentNullException(nameof(option), "StreamOption.Url can not be null.");

        // 设置超时
        var openOptions = _openOptions;
        ffmpeg.av_dict_set(&openOptions, "timeout", _streamOption.ConnectTimeout.ToString(), 0);

        #region 初始化视频流解码器
        OpenInput();

        ffmpeg.avformat_find_stream_info(_inputFormatCtx, null)
            .ThrowExceptionIfError();

        // 匹配解码器信息
        AVCodec* decoder = null;
        _streamIndex = ffmpeg
            .av_find_best_stream(_inputFormatCtx, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0)
            .ThrowExceptionIfError();

        _decoderCtx = DecoderContext.Create(decoder);

        ffmpeg.avcodec_parameters_to_context(_decoderCtx.UnmanagedPointer, _inputFormatCtx->streams[_streamIndex]->codecpar)
            .ThrowExceptionIfError();

        _decoderCtx.UnmanagedPointer->thread_count = (int)_streamOption.CodecThreads;
        _decoderCtx.UnmanagedPointer->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;
        _decoderCtx.UnmanagedPointer->skip_frame = AVDiscard.AVDISCARD_NONKEY;

        _decoderCtx.Open(decoder, null);

        var pixFormat = _decoderCtx.PixelFormat switch
        {
            AVPixelFormat.AV_PIX_FMT_YUVJ420P => AVPixelFormat.AV_PIX_FMT_YUV420P,
            AVPixelFormat.AV_PIX_FMT_YUVJ422P => AVPixelFormat.AV_PIX_FMT_YUV422P,
            AVPixelFormat.AV_PIX_FMT_YUVJ444P => AVPixelFormat.AV_PIX_FMT_YUV444P,
            AVPixelFormat.AV_PIX_FMT_YUVJ440P => AVPixelFormat.AV_PIX_FMT_YUV440P,
            _ => _decoderCtx.PixelFormat,
        };

        // 设置输入流信息
        StreamDecoderName = ffmpeg.avcodec_get_name(decoder->id);
        StreamPixelFormat = pixFormat;
        StreamWidth = _decoderCtx.UnmanagedPointer->width;
        StreamHeight = _decoderCtx.UnmanagedPointer->height;

        CloseInput();
        #endregion
    }

    private unsafe void OpenInput()
    {
        _logger.LogDebug("Open Input {url}.", _streamOption.Url.AbsoluteUri);

        _inputFormatCtx = ffmpeg.avformat_alloc_context();
        var formatCtx = _inputFormatCtx;

        // 设置超时
        var openOptions = _openOptions;

        // 打开流
        ffmpeg.avformat_open_input(&formatCtx, _streamOption.Url.AbsoluteUri, null, &openOptions)
            .ThrowExceptionIfError();
    }

    private unsafe void CloseInput()
    {
        _logger.LogDebug("Close Input.");

        var formatCtx = _inputFormatCtx;
        ffmpeg.avformat_close_input(&formatCtx);
        ffmpeg.avformat_free_context(formatCtx);
    }

    // 会引发异常，待排查
    public void Dispose()
    {
        _frame.Dispose();
        _webpOutputFrame.Dispose();
        _packet.Dispose();
        _decoderCtx.Dispose();
    }

    /// <summary>
    /// 解码下一关键帧（非线程安全）
    /// </summary>
    /// <returns></returns>
    public AvFrameWrapper DecodeNextFrameUnsafe()
    {
        var frame = _frame;

        using (_logger.BeginScope(_decoderCtx.ToString()))
        {
            IDisposable? scope = null;
            var decodeResult = -1;
            var timeoutTokenSource = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(_streamOption.CodecTimeout));

            while (!timeoutTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    do
                    {
                        _packet.Reset();

                        int readResult;

                        unsafe
                        {
                            readResult = ffmpeg.av_read_frame(_inputFormatCtx, _packet.UnmanagedPointer);
                        }

                        // EOF
                        if (readResult == ffmpeg.AVERROR_EOF)
                        {
                            var message = FfMpegExtension.av_strerror(readResult);
                            var error = new ApplicationException(message);

                            _logger.LogError(error, message);
                            throw new EndOfStreamException(message, error);
                        }

                        readResult.ThrowExceptionIfError();
                    } while (_packet.StreamIndex != _streamIndex);

                    scope?.Dispose();
                    scope = _logger.BeginScope(_packet.ToString());

                    // 取到了 stream 中的包
                    unsafe
                    {
                        _logger.LogInformation(
                            "Find packet in stream {index}, size:{size}, pts(display):{pts}, dts(decode):{dts}, key frame flag:{containsKey}",
                            _packet.StreamIndex,
                            string.Format(_formatter, "{0}", _packet.Size),
                            _packet.GetPresentationTimeSpan(_decoderCtx.TimeBase).ToString("c"),
                            _packet.GetDecodingTimeSpan(_decoderCtx.TimeBase).ToString("c"),
                            (_packet.UnmanagedPointer->flags & ffmpeg.AV_PKT_FLAG_KEY) == 1
                        );
                    }

                    // 空包
                    if (_packet.Size <= 0)
                    {
                        _logger.LogWarning("PacketBuffer with invalid size {size}, ignore.",
                            string.Format(_formatter, "{0}", _packet.Size));
                    }

                    unsafe
                    {
                        // 校验关键帧
                        if ((_packet.UnmanagedPointer->flags & ffmpeg.AV_PKT_FLAG_KEY) == 0x00)
                        {
                            _logger.LogInformation("PacketBuffer not contains KEY frame, drop.");
                            continue;
                        }
                    }

                    // 校验 PTS
                    if (_packet.PresentationTimeStamp < 0)
                    {
                        _logger.LogWarning("PacketBuffer pts={pts} < 0, drop.",
                            _packet.GetPresentationTimeSpan(_decoderCtx.TimeBase));
                        continue;
                    }

                    // 尝试发送
                    _logger.LogDebug("Try send packet to decoder.");
                    var sendResult = _decoderCtx.TrySendPacket(_packet);

                    if (sendResult == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                    {
                        // reference:
                        // * tree/release/6.1/fftools/ffmpeg_dec.c:567
                        // 理论上不会出现 EAGAIN

                        _logger.LogWarning(
                            "Receive {error} after sent, this could be cause by ffmpeg bug or some reason, ignored this message.",
                            nameof(ffmpeg.EAGAIN));
                        sendResult = 0;
                    }

                    if (sendResult == 0 || sendResult == ffmpeg.AVERROR_EOF)
                    {
                        // 发送成功
                        _logger.LogDebug("PacketBuffer sent success, try get decoded frame.");
                        // 获取解码结果
                        decodeResult = _decoderCtx.TryReceivedFrame(ref frame);
                    }
                    else
                    {
                        var error = new ApplicationException(FfMpegExtension.av_strerror(sendResult));

                        // 无法处理的发送失败
                        _logger.LogError(error, "Send packet to decoder failed.\n");

                        throw error;
                    }

                    scope?.Dispose();
                    scope = _logger.BeginScope(frame.ToString());

                    if (decodeResult < 0)
                    {
                        // 错误处理
                        ApplicationException error;
                        var message = FfMpegExtension.av_strerror(decodeResult);

                        if (decodeResult == ffmpeg.AVERROR_EOF)
                        {
                            // reference:
                            // * https://ffmpeg.org/doxygen/6.1/group__lavc__decoding.html#ga11e6542c4e66d3028668788a1a74217c
                            // > the codec has been fully flushed, and there will be no more output frames
                            // 理论上不会出现 EOF
                            message =
                                "the codec has been fully flushed, and there will be no more output frames.";

                            error = new(message);

                            _logger.LogError(error, "Received EOF from decoder.\n");
                        }
                        else if (decodeResult == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                        {
                            // reference:
                            // * tree/release/6.1/fftools/ffmpeg_dec.c:596
                            // * https://ffmpeg.org/doxygen/6.1/group__lavc__decoding.html#ga11e6542c4e66d3028668788a1a74217c
                            // > output is not available in this state - user must try to send new input
                            // 理论上不会出现 EAGAIN
                            // message =
                            //    "output is not available in this state - user must try to send new input";

                            //if (_streamOption.KeyFrameOnly)
                            //{
                            //    // 抛出异常，仅关键帧模式中，该错误不可能通过发送更多需要的包来解决
                            //    error = new(message);

                            //    _logger.LogError(error, "Received EAGAIN from decoder.\n");
                            //    throw error;
                            //}

                            // 忽略错误，发送下一个包进行编码，可能足够的包进入解码器可以解决
                            _logger.LogWarning("Receive EAGAIN from decoder, retry.");
                            continue;
                        }
                        else
                        {
                            error = new(message);
                            _logger.LogError(error, "Uncaught error occured during decoding.\n");
                            throw error;
                        }
                    }

                    // 解码正常
                    _logger.LogInformation("Decode frame success. type {type}, pts {pts}.",
                        frame.PictureType.ToString(),
                        frame.GetPresentationTimeSpan(_decoderCtx.TimeBase).ToString("c"));

                    scope?.Dispose();

                    break;
                }
                finally
                {
                    _packet.Reset();
                }
            }

            if (decodeResult != 0)
            {
                // 解码失败
                var error = new TaskCanceledException("Decode timeout.");
                _logger.LogError(error, "Failed to decode.\n");
                throw error;
            }

            unsafe
            {
                if (_decoderCtx.UnmanagedPointer->hw_device_ctx is not null)
                {
                    _logger.LogError("Hardware decode is unsupported, skip.");
                    // 硬件解码数据转换
                    // ffmpeg.av_hwframe_transfer_data(frame, frame, 0).ThrowExceptionIfError();
                }
            }

            return frame;
        }
    }

    /// <summary>
    /// 丢弃解码器结果中所有的帧（异步）
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task FlushDecoderBufferAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            await Task.Run(FlushDecoderBufferUnsafe, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 丢弃解码器结果中所有的帧
    /// </summary>
    private void FlushDecoderBufferUnsafe()
    {
        var cnt = 0;
        var frame = _frame;

        while (true)
        {
            unsafe
            {
                var result = _decoderCtx.TryReceivedFrame(ref frame);
                if (result == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                    break;

                if (result < 0)
                    _logger.LogError("An error occured during drop frame. {msg}", FfMpegExtension.av_strerror(result));

                _logger.LogDebug("Drop frame[{seq}] in decoder queue[{num}] in decoder buffer.", cnt, _decoderCtx.UnmanagedPointer->frame_num);
            }

            cnt++;
        }

        _frame.Reset();

        _logger.LogInformation("Clean decoder buffer completed, drop {num} frames.", cnt);
    }

    /// <summary>
    /// 截取图片（异步）
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>是否成功，图片字节码</returns>
    public async Task<(bool, byte[]?)> CaptureImageAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        // Check image cache.
        try
        {
            var captureTimeSpan = DateTime.Now - LastCaptureTime;
            if (LastCapturedImage != null && captureTimeSpan <= TimeSpan.FromSeconds(5))
            {
                _logger.LogInformation("Return image cached {time} ago.", captureTimeSpan);
                return (true, LastCapturedImage);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        // Capture new image and process it.
        var result = await Task.Run(async () =>
        {
            await _semaphore.WaitAsync(cancellationToken);

            _logger.LogInformation("Cache image expired, capture new.");
            try
            {
                OpenInput();

                var decodedFrame = DecodeNextFrameUnsafe();

                CloseInput();

                var queue = _encoder.Encode(decodedFrame);
                if (queue.Count > 1)
                {
                    var bufferSize = queue.Sum(buf => buf.Length);
                    var buffer = new byte[bufferSize];

                    var copied = 0;
                    foreach (var bufferBlock in queue)
                    {
                        Buffer.BlockCopy(bufferBlock, 0, buffer, copied, bufferBlock.Length);
                        copied += bufferBlock.Length;
                    }

                    LastCapturedImage = buffer;
                }
                else
                {
                    LastCapturedImage = queue.Dequeue();
                }

                LastCaptureTime = DateTime.Now;
                return (true, LastCapturedImage);
            }
            finally
            {
                _semaphore.Release();
            }
        }, cancellationToken);
        return result;
    }
}