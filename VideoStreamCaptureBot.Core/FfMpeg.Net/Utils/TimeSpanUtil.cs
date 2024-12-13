using FFmpeg.AutoGen.Abstractions;

namespace VideoStreamCaptureBot.Core.FfMpeg.Net.Utils;

public static class TimeSpanUtil
{
    public static TimeSpan FromFfmpeg(long value, AVRational timebase)
    {
        if (timebase.den == 0)
        {
            timebase.num = 1;
            timebase.den = ffmpeg.AV_TIME_BASE;
        }

        var milliseconds = (double)(value * timebase.num) / ((long)timebase.den * 1000);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
