using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using StreamingCaptureBot.Core.FfMpeg.Net.DataStructs;
using StreamingCaptureBot.Core.FfMpeg.Net.Extensions;
using StreamingCaptureBot.Core.FfMpeg.Net.Utils;
using StreamingCaptureBot.Core.Utils;

namespace StreamingCaptureBot.Core.FfMpeg.Net.Codecs;

public class CodecBase(ILogger logger, BinarySizeFormatter binarySizeFormat) : IDisposable
{
    protected unsafe AVCodecContext* CodecCtx;

    protected readonly AvPacketWrapper PacketBuffer = new();

    public unsafe Queue<byte[]> Encode(AvFrameWrapper rawFrame)
    {
        var frame = rawFrame.UnmanagedPointer;

        using (logger.BeginScope(
                   "{name}@0x{address:x16}.{func}",
                   ffmpeg.avcodec_get_name(CodecCtx->codec_id),
                   (nint)CodecCtx,
                   nameof(Encode)))
        {
            var linkedBuffer = new Queue<byte[]>(2);
            var packet = PacketBuffer.UnmanagedPointer;

            CodecCtx->width = frame->width;
            CodecCtx->height = frame->height;
            CodecCtx->sample_aspect_ratio = frame->sample_aspect_ratio;

            #region 发送
            var scope = logger.BeginScope($"frame@0x{frame->GetHashCode():x8}");

            logger.LogDebug("Try send frame to encoder.");

            var ret = ffmpeg.avcodec_send_frame(CodecCtx, frame);
            if (ret < 0)
            {
                // handle send exceptions.
                var exception = new ApplicationException(FfMpegExtension.av_strerror(ret));
                string? message = null;

                if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                {
                    message =
                        "input is not accepted in the current state - user must read output with avcodec_receive_packet()" +
                        "(once all output is read, the packet should be resent, and the call will not fail with EAGAIN).\n";
                }
                else if (ret == ffmpeg.AVERROR_EOF)
                {
                    message =
                        "the encoder has been flushed, and no new frames can be sent to it\n";
                }
                else if (ret == ffmpeg.AVERROR(ffmpeg.EINVAL))
                {
                    message = "codec not opened, it is a decoder, or requires flush\n";
                }
                else if (ret == ffmpeg.AVERROR(ffmpeg.ENOMEM))
                {
                    message = "failed to add packet to (ffmpeg-managed) internal queue, or similar\n";
                }

#pragma warning disable CA2254
                logger.LogError(exception, message);
#pragma warning restore CA2254
                throw exception;
            }

            logger.LogInformation("Success sent frame to decoder.");
            logger.LogDebug("If there's no another usage, this frame can be release now.");

            scope?.Dispose();
            #endregion

            #region 接收
            logger.LogDebug("Try receive packet from decoder.");

            for (ret = ReceivePacket(); ret == 0 && packet->size > 0; ret = ReceivePacket())
            {
                scope = logger.BeginScope($"packet@{packet->buf->GetHashCode()}");
                logger.LogInformation("Received packet from decoder, size:{size}.",
                    string.Format(binarySizeFormat, "{0}", packet->size));

                var buffer = new byte[packet->size];
                Marshal.Copy((nint)packet->data, buffer, 0, packet->size);
                linkedBuffer.Enqueue(buffer);
            }

            if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == 0)
            {
                logger.LogInformation("Received EAGAIN from encoder, Encode completed with {num} packets.", linkedBuffer.Count);
            }
            else
            {
                // references:
                // * https://ffmpeg.org/doxygen/6.1/group__lavc__decoding.html#ga5b8eff59cf259747cf0b31563e38ded6
                var exception = new ApplicationException(
                    FfMpegExtension.av_strerror(ret));
                var message = "Uncaught error occured during encoding.\n";

                if (ret == ffmpeg.AVERROR_EOF)
                {
                    // > the encoder has been fully flushed, and there will be no more output packets
                    // should not happen because nobody send flush frame.
                    message =
                        "The encoder has been fully flushed, and there will be no more output packets.\n";
                }
                else if (ret == ffmpeg.AVERROR(ffmpeg.EINVAL))
                {
                    // > codec not opened, or it is a decoder
                    // should not happen because codec has been opened correct in ctor.
                    message = "Codec not opened, or it is a decoder.\n";
                }

#pragma warning disable CA2254
                logger.LogError(exception, message);
#pragma warning restore CA2254

                throw exception;
            }

            scope?.Dispose();
            #endregion

            return linkedBuffer;
        }
    }

    public unsafe AvFrameWrapper Decode(AVPacket* packet)
    {
        throw new NotImplementedException();
        var decodeResult = 0;
        var frame = new AvFrameWrapper();

        // 尝试发送
        logger.LogDebug("Try send packet to decoder.");
        var sendResult = ffmpeg.avcodec_send_packet(CodecCtx, packet);

        if (sendResult == ffmpeg.AVERROR(ffmpeg.EAGAIN))
        {
            // reference:
            // * tree/release/6.1/fftools/ffmpeg_dec.c:567
            // 理论上不会出现 EAGAIN

            logger.LogWarning(
                "Receive {error} after sent, this could be cause by ffmpeg bug or some reason, ignored this message.",
                nameof(ffmpeg.EAGAIN));
            sendResult = 0;
        }

        if (sendResult == 0 || sendResult == ffmpeg.AVERROR_EOF)
        {
            // 发送成功
            logger.LogDebug("PacketBuffer sent success, try get decoded frame.");
            // 获取解码结果
            decodeResult = ffmpeg.avcodec_receive_frame(CodecCtx, frame.UnmanagedPointer);
        }
        else
        {
            var error = new ApplicationException(FfMpegExtension.av_strerror(sendResult));

            // 无法处理的发送失败
            logger.LogError(error, "Send packet to decoder failed.\n");

            throw error;
        }

        var scope = logger.BeginScope($"Frame@0x{frame.UnmanagedPointer->GetHashCode():x8}");

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

                logger.LogError(error, "Received EOF from decoder.\n");
            }
            else if (decodeResult == ffmpeg.AVERROR(ffmpeg.EAGAIN))
            {
                // reference:
                // * tree/release/6.1/fftools/ffmpeg_dec.c:596
                // * https://ffmpeg.org/doxygen/6.1/group__lavc__decoding.html#ga11e6542c4e66d3028668788a1a74217c
                // > output is not available in this state - user must try to send new input
                // 理论上不会出现 EAGAIN
                message =
                    "output is not available in this state - user must try to send new input";

                //if (_streamOption.KeyFrameOnly)
                //{
                //    // 抛出异常，仅关键帧模式中，该错误不可能通过发送更多需要的包来解决
                //    error = new(message);

                //    _logger.LogError(error, "Received EAGAIN from decoder.\n");
                //    throw error;
                //}

                // 忽略错误，发送下一个包进行编码，可能足够的包进入解码器可以解决
                logger.LogWarning("Receive EAGAIN from decoder, retry.");
                // continue;
            }
            else
            {
                error = new(message);
                logger.LogError(error, "Uncaught error occured during decoding.\n");
                throw error;
            }
        }

        // 解码正常
        logger.LogInformation("Decode frame success. type {type}, pts {pts}.",
            frame.UnmanagedPointer->pict_type.ToString(),
            TimeSpanUtil.FromFfmpeg(frame.UnmanagedPointer->pts, CodecCtx->time_base).ToString("c"));

    }

    public AvFrameWrapper Decode(Queue<byte[]> binaryQueue)
    {
        throw new NotImplementedException();
    }

    private unsafe int ReceivePacket()
    {
        PacketBuffer.Reset();
        return ffmpeg.avcodec_receive_packet(CodecCtx, PacketBuffer.UnmanagedPointer);
    }

    public virtual void Dispose()
    {
        PacketBuffer.Dispose();
        GC.SuppressFinalize(this);
    }
}