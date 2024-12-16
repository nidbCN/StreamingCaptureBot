using FFmpeg.AutoGen.Abstractions;

namespace StreamingCaptureBot.Core.FfMpeg.Net.Utils;

public static class TimeSpanUtil
{
    public static TimeSpan? FromFfmpeg(long value, AVRational timebase)
    {
        if (timebase.den <= 0 || timebase.num < 0 || value == ffmpeg.AV_NOPTS_VALUE)
            return null;

        var microSec = (long)(
            (Int128)(value * timebase.num) * ffmpeg.AV_TIME_BASE
            / timebase.den);

        if (microSec > TimeSpan.MaxValue.TotalSeconds)
            return TimeSpan.MaxValue;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (microSec < TimeSpan.MinValue.TotalSeconds)
            return TimeSpan.MinValue;

        return TimeSpan.FromMicroseconds(microSec);
    }
}
