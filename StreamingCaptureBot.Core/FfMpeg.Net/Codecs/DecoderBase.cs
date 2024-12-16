using FFmpeg.AutoGen.Abstractions;
using StreamingCaptureBot.Core.FfMpeg.Net.DataStructs;
using StreamingCaptureBot.Core.FfMpeg.Net.Extensions;

namespace StreamingCaptureBot.Core.FfMpeg.Net.Codecs;

public abstract class DecoderBase(ILogger logger, DecoderContext ctx) : IDisposable
{
    public DecoderContext Context => ctx;
    public bool Opened { get; private set; }

    protected DecoderBase(ILogger logger, string name)
        : this(logger, new DecoderContext(name)) { }

    protected DecoderBase(ILogger logger, AVCodecID decoderId)
        : this(logger, new DecoderContext(decoderId)) { }

    protected unsafe DecoderBase(ILogger logger, AVCodec* decoder)
        : this(logger, new DecoderContext(decoder)) { }

    protected unsafe DecoderBase(ILogger logger, AVCodecContext* unmanagedCtx)
        : this(logger, new DecoderContext(unmanagedCtx)) { }

    public bool ConfigureAndOpen(Action<DecoderContext> config, IDictionary<string, string>? options = null)
    {
        if (Opened)
            return false;

        config.Invoke(Context);

        unsafe
        {
            AVDictionary* openOptions = null;

            if (options is null)
                return Context.TryOpen(Context.UnmanagedPointer->codec
                    , &openOptions) == 0;

            foreach (var (key, value) in options)
                ffmpeg.av_dict_set(&openOptions, key, value, 0x0);

            Opened =
                Context.TryOpen(Context.UnmanagedPointer->codec, &openOptions) == 0;

            return Opened;
        }
    }

    public void Decode(AvPacketWrapper packet, ref AvFrameWrapper frame)
    {
        using (logger.BeginScope("{name}.{function}",
                   Context.ToString(), nameof(Decode)))
        {
            int decodeResult;
            // 尝试发送

            logger.LogDebug("Try send packet to decoder.");

            var sendResult = ctx.TrySendPacket(packet);
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
                logger.LogInformation("Success sent packet to decoder.");

                // 获取解码结果
                decodeResult = ctx.TryReceivedFrame(ref frame);
            }
            else
            {
                var error = new ApplicationException(FfMpegExtension.av_strerror(sendResult));

                // 无法处理的发送失败
                logger.LogError(error, "Send packet to decoder failed.\n");

                throw error;
            }

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
                frame.PictureType.ToString(),
                frame.GetPresentationTimeSpan(ctx.TimeBase)?.ToString("c") ?? "NO PTS");
        }
    }

    public void Dispose()
    {
        Context.Dispose();
        GC.SuppressFinalize(this);
    }
}
