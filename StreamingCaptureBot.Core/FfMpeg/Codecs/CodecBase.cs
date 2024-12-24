﻿using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using FfMpeg.AutoGen.Wrapper.DataStructs;
using FfMpeg.AutoGen.Wrapper.Extensions;
using StreamingCaptureBot.Hosting.Utils;

namespace StreamingCaptureBot.Hosting.FfMpeg.Codecs;

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
            logger.LogDebug("Try send frame to encoder.");

            var ret = ffmpeg.avcodec_send_frame(CodecCtx, frame);
            if (ret < 0)
            {
                // handle send exceptions.
                var exception = new ApplicationException(FfMpegExtension.ErrorCodeToString(ret));
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

            logger.LogInformation("Success sent frame to encoder.");
            logger.LogDebug("If there's no another usage, this frame can be release now.");

            #endregion

            #region 接收
            logger.LogDebug("Try receive packet from encoder.");

            IDisposable? scope = null;
            for (ret = ReceivePacket(); ret == 0 && packet->size > 0; ret = ReceivePacket())
            {
                scope = logger.BeginScope($"packet@0x{packet->buf->GetHashCode():x8}");
                logger.LogInformation("Received packet from encoder, size:{size}.",
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
                    FfMpegExtension.ErrorCodeToString(ret));
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

                scope?.Dispose();
                throw exception;
            }

            scope?.Dispose();
            #endregion

            return linkedBuffer;
        }
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