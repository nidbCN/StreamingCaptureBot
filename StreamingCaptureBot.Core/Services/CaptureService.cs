﻿using FFmpeg.AutoGen.Abstractions;
using FfMpeg.AutoGen.Wrapper.DataStructs;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Core.Bots.LagrangeBot.Extensions;
using StreamingCaptureBot.Core.Configs;
using StreamingCaptureBot.Core.FfMpeg.Codecs;
using StreamingCaptureBot.Core.Utils;

namespace StreamingCaptureBot.Core.Services;

public sealed class CaptureService : IDisposable
{
    private readonly ILogger<CaptureService> _logger;
    private readonly IOptions<StreamOption> _streamOption;

    private bool _streamIsOpen;

    private readonly BinarySizeFormatter _formatter;

    private readonly FfmpegLibWebpEncoder _encoder;
    private readonly GenericDecoder _decoder;

    private unsafe AVFormatContext* _inputFormatCtx;
    private readonly unsafe AVDictionary* _openOptions = null;

    private readonly AvFrameWrapper _frame = new();
    private readonly AvFrameWrapper _webpOutputFrame = new();
    private readonly AvPacketWrapper _packet = new();

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public byte[]? LastCapturedImage { get; private set; }
    public DateTime LastCaptureTime { get; private set; }

    public string StreamDecoderName { get; }
    public AVPixelFormat StreamPixelFormat { get; }
    public int StreamHeight { get; private set; }
    public int StreamWidth { get; private set; }

    public unsafe CaptureService(
        ILogger<CaptureService> logger,
        IOptions<StreamOption> option,
        FfmpegLibWebpEncoder encoder,
        BinarySizeFormatter formatter,
        GenericDecoder decoder)
    {
        _logger = logger;
        _streamOption = option;
        _encoder = encoder;
        _formatter = formatter;

        if (_streamOption.Value.Url is null)
            throw new ArgumentNullException(nameof(option), "StreamOption.Url can not be null.");

        _decoder = decoder;

        _decoder.ConfigureAndOpen(ctx =>
        {
            ctx.ThreadCount = (int)_streamOption.Value.CodecThreads;
            ctx.Flags |= AvCodecContextWrapper.CodecFlag.LowDelay;
            ctx.SkipFrame = AVDiscard.AVDISCARD_NONKEY;
        });

        // 设置输入流信息
        StreamPixelFormat = _decoder.Context.PixelFormat switch
        {
            AVPixelFormat.AV_PIX_FMT_YUVJ420P => AVPixelFormat.AV_PIX_FMT_YUV420P,
            AVPixelFormat.AV_PIX_FMT_YUVJ422P => AVPixelFormat.AV_PIX_FMT_YUV422P,
            AVPixelFormat.AV_PIX_FMT_YUVJ444P => AVPixelFormat.AV_PIX_FMT_YUV444P,
            AVPixelFormat.AV_PIX_FMT_YUVJ440P => AVPixelFormat.AV_PIX_FMT_YUV440P,
            _ => _decoder.Context.PixelFormat,
        };
        StreamDecoderName = ffmpeg.avcodec_get_name(_decoder.Context.UnmanagedPointer->codec_id);
        StreamWidth = _decoder.Context.Width;
        StreamHeight = _decoder.Context.Height;
    }

    private unsafe void OpenInput()
    {
        var url = _streamOption.Value.Url.AbsoluteUri;
        _logger.LogDebug("Open Input {url}.", url);

        var formatCtx = ffmpeg.avformat_alloc_context();

        if (formatCtx is null)
        {
            _streamIsOpen = false;
            return;
        }

        // 设置超时
        var openOptions = _openOptions;

        // 打开流
        if (ffmpeg.avformat_open_input(&formatCtx, url, null, &openOptions) != 0)
            return;
        _streamIsOpen = true;
        _inputFormatCtx = formatCtx;
    }

    private unsafe void CloseInput()
    {
        var formatCtx = _inputFormatCtx;

        if (!_streamIsOpen)
            return;

        _logger.LogDebug("Close Input.");

        ffmpeg.avformat_close_input(&formatCtx);
        ffmpeg.avformat_free_context(formatCtx);

        _streamIsOpen = false;
    }

    public void Dispose()
    {
        _frame.Dispose();
        _webpOutputFrame.Dispose();
        _packet.Dispose();
        _decoder.Dispose();
    }

    /// <summary>
    /// 解码下一关键帧（非线程安全）
    /// </summary>
    /// <returns></returns>
    public void DecodeNextFrameUnsafe()
    {
        var timeoutTokenSource = new CancellationTokenSource(_streamOption.Value.CodecTimeout);
        do
        {
            IDisposable? scope = null;

            try
            {
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

                scope = _logger.BeginScope(_packet.ToString());

                // drop stream mis-matched packet
                if (_packet.StreamIndex != _streamOption.Value.StreamIndex)
                {
                    _logger.LogInformation("Received packet from stream[{id}], require stream[{req id}], drop.",
                        _packet.StreamIndex, _streamOption.Value.StreamIndex);

                    continue;
                }

                _logger.LogInformation(
                    "Received packet from stream[{index}] with size:{size}, pts(display):{pts:c}, dts(decode):{dts:c}.",
                    _packet.StreamIndex,
                    string.Format(_formatter, "{0}", _packet.Size),
                    _packet.GetPresentationTimeSpan(_decoder.Context.TimeBase),
                    _packet.GetDecodingTimeSpan(_decoder.Context.TimeBase)
                );

                // drop packet without key frame
                unsafe
                {
                    if ((_packet.UnmanagedPointer->flags & ffmpeg.AV_PKT_FLAG_KEY) == 0x00)
                    {
                        _logger.LogInformation("Packet flag 0x{flag:x8} not contains KEY frame, drop.",
                            _packet.UnmanagedPointer->flags);

                        continue;
                    }
                }

                // drop packet with invalid size
                if (_packet.Size <= 0)
                {
                    _logger.LogWarning("Packet with invalid size {size}, drop.",
                        string.Format(_formatter, "{0}", _packet.Size));

                    continue;
                }

                // drop packet with invalid pts
                if (_packet.PresentationTimeStamp < 0)
                {
                    _logger.LogWarning("Packet pts={pts:c} < 0, drop.",
                        _packet.GetPresentationTimeSpan(_decoder.Context.TimeBase));

                    continue;
                }

                // decode
                var frame = _frame;
                _decoder.Decode(_packet, ref frame);

                scope?.Dispose();

                // drop frame without key frame
                if (frame.PictureType != AVPictureType.AV_PICTURE_TYPE_I)
                {
                    using (_logger.BeginScope(frame.ToString()))
                    {
                        _logger.LogWarning("Frame type {type}, not key frame, drop.", frame.PictureType.ToString());
                    }

                    continue;
                }

                // success
                break;
            }
            finally
            {
                scope?.Dispose();
                _packet.Reset();
            }
        } while (!timeoutTokenSource.Token.IsCancellationRequested);

        if (timeoutTokenSource.Token.IsCancellationRequested)
        {
            // 解码失败
            var error = new TaskCanceledException("Decode timeout.");
            _logger.LogError(error, "Failed to decode.");

            throw error;
        }

        unsafe
        {
            if (_decoder.Context.UnmanagedPointer->hw_device_ctx is not null)
            {
                _logger.LogError("Hardware decode is unsupported, skip.");
                // 硬件解码数据转换
                // ffmpeg.av_hwframe_transfer_data(frame, frame, 0).ThrowExceptionIfError();
            }
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
            await Task.Run(() =>
                FlushDecoderBufferUnsafe(cancellationToken), cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 丢弃解码器结果中所有的帧
    /// </summary>
    private void FlushDecoderBufferUnsafe(CancellationToken cancellationToken)
    {
        var cnt = 0;
        var frame = _frame;

        while (!cancellationToken.IsCancellationRequested)
        {
            unsafe
            {
                var result = _decoder.Context.TryReceivedFrame(ref frame);
                if (result == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                    break;

                if (result < 0)
                    _logger.LogError("An error occured during drop frame. {msg}", FfMpegExtension.av_strerror(result));

                _logger.LogDebug("Drop frame[{seq}] in decoder queue[{num}] in decoder buffer.", cnt, _decoder.Context.UnmanagedPointer->frame_num);
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
        => await Task.Run(async () =>
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                var captureTimeSpan = DateTime.Now - LastCaptureTime;
                if (LastCapturedImage != null && captureTimeSpan < _streamOption.Value.CacheTimeout)
                {
                    _logger.LogInformation("Return image cached {time:c} ago.", captureTimeSpan);
                    return (true, LastCapturedImage);
                }

                _logger.LogInformation("Image cached {time:c} ago has expired, capture new.", captureTimeSpan);

                OpenInput();

                DecodeNextFrameUnsafe();

                using (_logger.BeginScope(_frame.ToString()))
                {
                    var queue = _encoder.Encode(_frame);

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
                }
                LastCaptureTime = DateTime.Now;
                return (true, LastCapturedImage);
            }
            finally
            {
                CloseInput();
                _frame.Reset();
                _semaphore.Release();
            }
        }, cancellationToken);
}