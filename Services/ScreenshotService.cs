﻿
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using CameraScreenshotBotService.Extensions;


namespace CameraScreenshotBotService.Services;

public sealed unsafe class ScreenshotService
{
    private readonly ILogger<ScreenshotService> _logger;
    private readonly IConfiguration _config;

    private readonly AVCodecContext* _decoderCtx;
    private readonly AVCodecContext* _encoderCtx;

    private readonly AVFormatContext* _inputFormatCtx;
    private readonly AVFrame* _inputFrame;
    private readonly AVPacket* _pPacket;
    private readonly AVFrame* _receivedFrame;
    private readonly int _streamIndex;

    public ScreenshotService(ILogger<ScreenshotService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;

        var url = config["Camera:Url"] ?? throw new ArgumentNullException("CameraUrl");

        // 初始化 ffmpeg 输入
        _inputFormatCtx = ffmpeg.avformat_alloc_context();
        _receivedFrame = ffmpeg.av_frame_alloc();
        var fotmatCtx = _inputFormatCtx;
        ffmpeg.avformat_open_input(&fotmatCtx, url, null, null).ThrowExceptionIfError();
        ffmpeg.avformat_find_stream_info(_inputFormatCtx, null).ThrowExceptionIfError();

        // 初始化解码器
        {
            AVCodec* decoder = null;
            _streamIndex = ffmpeg
                .av_find_best_stream(_inputFormatCtx, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0)
                .ThrowExceptionIfError();

            _decoderCtx = ffmpeg.avcodec_alloc_context3(decoder);
            ffmpeg.avcodec_parameters_to_context(_decoderCtx, _inputFormatCtx->streams[_streamIndex]->codecpar)
                .ThrowExceptionIfError();
            _decoderCtx->thread_count = ushort.Parse(config["DecodeThread"] ?? "4");
            ffmpeg.avcodec_open2(_decoderCtx, decoder, null).ThrowExceptionIfError();

            StreamCodecName = ffmpeg.avcodec_get_name(decoder->id);
            StreamPixelFormat = _decoderCtx->pix_fmt;
            StreamWidth = _decoderCtx->width;
            StreamHeight = _decoderCtx->height;
        }

        // 初始化 PNG 编码器
        {
            var encoder = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_PNG);
            _encoderCtx = ffmpeg.avcodec_alloc_context3(encoder);

            // 设置编码器参数
            _encoderCtx->width = _decoderCtx->width;
            _encoderCtx->height = _decoderCtx->height;
            _encoderCtx->time_base = new AVRational { num = 1, den = 25 }; // 设置时间基准
            _encoderCtx->framerate = new AVRational { num = 25, den = 1 };
            _encoderCtx->pix_fmt = AVPixelFormat.AV_PIX_FMT_RGB24;

            // 打开编码器
            ffmpeg.avcodec_open2(_encoderCtx, encoder, null).ThrowExceptionIfError();
        }

        _pPacket = ffmpeg.av_packet_alloc();
        _inputFrame = ffmpeg.av_frame_alloc();

        // 设置日志
        {
            ffmpeg.av_log_set_level(config["Camera:Log"]?.ToUpper() switch
            {
                "DEBUG" => ffmpeg.AV_LOG_DEBUG,
                "WARNING" => ffmpeg.AV_LOG_WARNING,
                "ERROR" => ffmpeg.AV_LOG_ERROR,
                _ => ffmpeg.AV_LOG_INFO
            });

            //// do not convert to local function
            av_log_set_callback_callback logCallback = (p0, level, format, vl) =>
            {
                if (level > ffmpeg.av_log_get_level()) return;

                var lineSize = 1024;
                var lineBuffer = stackalloc byte[lineSize];
                var printPrefix = 1;
                ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                var line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer);

                switch (level)
                {
                    case ffmpeg.AV_LOG_DEBUG:
                        _logger.LogDebug(line);
                        break;
                    case ffmpeg.AV_LOG_WARNING:
                        _logger.LogWarning(line);
                        break;
                    case ffmpeg.AV_LOG_INFO:
                        _logger.LogInformation(line);
                        break;
                    default:
                        _logger.LogInformation(line);
                        break;
                }
            };

            ffmpeg.av_log_set_callback(logCallback);
        }
    }

    public string StreamCodecName { get; }
    public AVPixelFormat StreamPixelFormat { get; }
    public int StreamHeight { get; }
    public int StreamWidth { get; }

    public void Dispose()
    {
        var pFrame = _inputFrame;
        ffmpeg.av_frame_free(&pFrame);

        var pPacket = _pPacket;
        ffmpeg.av_packet_free(&pPacket);

        ffmpeg.avcodec_close(_decoderCtx);
        var pFormatContext = _inputFormatCtx;
        ffmpeg.avformat_close_input(&pFormatContext);
    }

    public bool TryDecodeNextKeyFrame(out AVFrame* frame)
    {
        ffmpeg.av_frame_unref(_inputFrame);
        ffmpeg.av_frame_unref(_receivedFrame);
        int ret = 0;

        for (int cnt = 0; cnt < 120; cnt++)
        {
            do
            {
                try
                {
                    do
                    {
                        // 遍历流找到 bestStream
                        ffmpeg.av_packet_unref(_pPacket);
                        ret = ffmpeg.av_read_frame(_inputFormatCtx, _pPacket);

                        // 视频流已经结束
                        if (ret == ffmpeg.AVERROR_EOF)
                        {
                            frame = _inputFrame;
                            return false;
                        }

                        ret.ThrowExceptionIfError();
                    } while (_pPacket->stream_index != _streamIndex);

                    ffmpeg.avcodec_send_packet(_decoderCtx, _pPacket).ThrowExceptionIfError();
                }
                finally
                {
                    ffmpeg.av_packet_unref(_pPacket);
                }

                ret = ffmpeg.avcodec_receive_frame(_decoderCtx, _inputFrame);

                // -11 资源暂不可用时候重试
            } while (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            _logger.LogInformation("Frame counter: {cnt}", ++cnt);

            // 关键帧
            if (_inputFrame->pict_type == AVPictureType.AV_PICTURE_TYPE_I)
            {
                break;
            }
        }

        ret.ThrowExceptionIfError();

        if (_decoderCtx->hw_device_ctx != null)
        {
            ffmpeg.av_hwframe_transfer_data(_receivedFrame, _inputFrame, 0).ThrowExceptionIfError();
            frame = _receivedFrame;
        }
        else
            frame = _inputFrame;

        return true;
    }

    public bool TryCapturePngImage(out byte[]? image)
    {
        image = null;

        if (!TryDecodeNextKeyFrame(out var frame))
            return false;

        var width = frame->width;
        var height = frame->height;
        var sourcePixelFormat = _decoderCtx->pix_fmt;
        var destPixelFormat = AVPixelFormat.AV_PIX_FMT_RGB24;

        // 创建 AVFrame 用于编码
        AVFrame* imageFrame = ffmpeg.av_frame_alloc();
        imageFrame->width = frame->width;
        imageFrame->height = frame->height;
        imageFrame->format = (int)destPixelFormat;
        ffmpeg.av_frame_get_buffer(imageFrame, 32); // 分配内存

        // 将 AVFrame 数据复制到 pngFrame
        ffmpeg.av_frame_copy(imageFrame, frame);
        ffmpeg.av_frame_copy_props(imageFrame, frame);

        // 转换
        var scaleCxt = ffmpeg.sws_getContext(width, height, sourcePixelFormat,
                                  width, height, destPixelFormat,
                                  ffmpeg.SWS_BICUBIC, null, null, null);
        ffmpeg.sws_scale(scaleCxt, frame->data, frame->linesize, 0,
                  height, imageFrame->data, imageFrame->linesize);
        ffmpeg.sws_freeContext(scaleCxt);

        ffmpeg.avcodec_send_frame(_encoderCtx, imageFrame).ThrowExceptionIfError();

        using var memStream = new MemoryStream();


        int ret = 0;
        var offset = 0;

        do
        {
            ret = ffmpeg.avcodec_receive_packet(_encoderCtx, _pPacket);

            if (ret >= 0)
            {
                // 正常
                _logger.LogInformation("Receive packet with size {s}", _pPacket->size);

                var buffer = new byte[_pPacket->size];
                Marshal.Copy((IntPtr)_pPacket->data, buffer, 0, _pPacket->size);
                memStream.Write(buffer, offset, _pPacket->size);

                ffmpeg.av_packet_unref(_pPacket);
            }
            else if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
            {
                // -11
                _logger.LogWarning("Receive -11, send EOF and skip");
                // ret = ffmpeg.avcodec_send_packet(codecCtx, );
                ffmpeg.avcodec_send_frame(_encoderCtx, null).ThrowExceptionIfError();
                do
                {
                    ffmpeg.av_packet_unref(_pPacket);
                    ret = ffmpeg.avcodec_receive_packet(_encoderCtx, _pPacket);

                    if (ret >= 0)
                    {
                        byte[] buffer2 = new byte[_pPacket->size];
                        Marshal.Copy((IntPtr)_pPacket->data, buffer2, 0, _pPacket->size);
                        _logger.LogInformation("EOF send and recv, size {s}", _pPacket->size);
                        memStream.Write(buffer2, 0, _pPacket->size);
                    }
                    else
                    {
                        _logger.LogError("EOF send and recv err, msg {m}", FFMpegExtension.av_strerror(ret));
                    }

                    ffmpeg.av_packet_unref(_pPacket);
                } while (ret >= 0);

                continue;
            }
            else if (ret == ffmpeg.AVERROR_EOF)
            {
                break;
            }
            else
            {
                _logger.LogError("Error, msg {m}", FFMpegExtension.av_strerror(ret));
            }
        } while (ret >= 0);


        // 释放资源
        ffmpeg.av_frame_free(&imageFrame);
        // 取消引用
        ffmpeg.av_packet_unref(_pPacket);

        // result
        image = memStream.ToArray();
        return true;
    }

    public IReadOnlyDictionary<string, string?>? GetContextInfo()
    {
        AVDictionaryEntry* tag = null;

        var result = new Dictionary<string, string?>();

        while ((tag = ffmpeg.av_dict_get(_inputFormatCtx->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
        {
            var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
            var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);

            if (key is not null)
                result.Add(key, value);
        }

        return result;
    }
}