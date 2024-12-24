﻿using FFmpeg.AutoGen.Abstractions;
using FfMpeg.AutoGen.Wrapper.DataStructs;

namespace StreamingCaptureBot.Core.FfMpeg.Codecs;

public class GenericDecoder : DecoderBase
{
    public GenericDecoder(ILogger logger, string name) : base(logger, name)
    {
    }

    public GenericDecoder(ILogger logger, AVCodecID decoderId) : base(logger, decoderId)
    {
    }

    public unsafe GenericDecoder(ILogger logger, AVCodec* decoder) : base(logger, decoder)
    {
    }

    public unsafe GenericDecoder(ILogger logger, AVCodecContext* unmanagedCtx) : base(logger, unmanagedCtx)
    {
    }

    public GenericDecoder(ILogger logger, DecoderContext ctx) : base(logger, ctx)
    {
    }
}
