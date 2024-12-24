using FFmpeg.AutoGen.Abstractions;

namespace FfMpeg.AutoGen.Wrapper.Utils;

public static class TimeSpanUtil
{
    private static readonly AVRational FfMpegTimeBaseRational = new()
    {
        num = 1, 
        den = ffmpeg.AV_TIME_BASE
    };

    public static TimeSpan? FromFfmpeg(long value, AVRational timebase)
    {
        if (timebase.den <= 0 || timebase.num < 0 || value == ffmpeg.AV_NOPTS_VALUE)
            return null;

        var microSec = ffmpeg.av_rescale_q(value, timebase, FfMpegTimeBaseRational);

        if (microSec > TimeSpan.MaxValue.TotalMicroseconds)
            return TimeSpan.MaxValue;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (microSec < TimeSpan.MinValue.TotalMicroseconds)
            return TimeSpan.MinValue;

        return TimeSpan.FromMicroseconds(microSec);
    }
}
