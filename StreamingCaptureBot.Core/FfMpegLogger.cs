using FFmpeg.AutoGen.Abstractions;
using Microsoft.Extensions.Options;

namespace StreamingCaptureBot.Core;

public class FfMpegLogger(
    ILogger<FfMpegLogger> logger,
    IOptions<LoggerFilterOptions> loggerOptions) : IHostedLifecycleService
{
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private av_log_set_callback_callback? _logCallback;

    private const int LineSize = 1024;

    public void ConfigureFfMpegLogger()
    {
        // 设置日志
        var level = loggerOptions.Value.MinLevel switch
        {
            LogLevel.Trace => ffmpeg.AV_LOG_TRACE,
            LogLevel.Debug => ffmpeg.AV_LOG_DEBUG,
            LogLevel.Information => ffmpeg.AV_LOG_INFO,
            LogLevel.Warning => ffmpeg.AV_LOG_WARNING,
            LogLevel.Error => ffmpeg.AV_LOG_ERROR,
            LogLevel.Critical => ffmpeg.AV_LOG_PANIC,
            LogLevel.None => ffmpeg.AV_LOG_QUIET,
            _ => ffmpeg.AV_LOG_INFO
        };

        unsafe
        {
            _logCallback = FfMpegLogInvoke;
            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_TRACE);
            ffmpeg.av_log_set_callback(_logCallback);
        }
    }

    private unsafe void FfMpegLogInvoke(void* p0, int level, string format, byte* vl)
    {
        if (level > ffmpeg.av_log_get_level()) return;

        var buffer = stackalloc byte[LineSize];
        var printPrefix = ffmpeg.AV_LOG_SKIP_REPEATED | ffmpeg.AV_LOG_PRINT_LEVEL;
        var formatSpan = format.AsSpan();
        var trimmedFormat = formatSpan[..^1].ToString();

        ffmpeg.av_log_format_line(p0, level, trimmedFormat, vl, buffer, LineSize, &printPrefix);

        // count string
        var textBufferSize = 0;
        while (buffer[textBufferSize++] != 0) { }

        // empty string
        if (textBufferSize == 1) return;

        var textBuffer = new char[textBufferSize];
        var textBufferSpan = new Span<char>(textBuffer);

        for (var i = 0; i < textBufferSize; i++)
        {
            textBufferSpan[i] = (char)buffer[i];
        }

        var text = new string(textBufferSpan);

        using (logger.BeginScope(nameof(ffmpeg)))
        {
#pragma warning disable CA2254
            Action<string> logInvoke = level switch
            {
                ffmpeg.AV_LOG_PANIC => msg => logger.LogCritical(msg),
                ffmpeg.AV_LOG_FATAL => msg => logger.LogCritical(msg),
                ffmpeg.AV_LOG_ERROR => msg => logger.LogError(msg),
                ffmpeg.AV_LOG_WARNING => msg => logger.LogWarning(msg),
                ffmpeg.AV_LOG_INFO => msg => logger.LogInformation(msg),
                ffmpeg.AV_LOG_VERBOSE => msg => logger.LogDebug(msg),
                ffmpeg.AV_LOG_DEBUG => msg => logger.LogDebug(msg),
                ffmpeg.AV_LOG_TRACE => msg => logger.LogTrace(msg),
                _ => LogUnknown,
            };
#pragma warning restore CA2254

            logInvoke.Invoke(text);
        }

        return;

        void LogUnknown(string message)
        {
            logger.LogWarning("Log unknown level:{level}, msg: {msg}", level, message);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StartingAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
