using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLinked;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Core.Configs;
using System.Runtime.InteropServices;
using System.Text;
using FfMpegLib.Net.DataStructs;
using FfMpegLib.Net.Extensions;
using FfMpegLib.Net.Utils;
using StreamingCaptureBot.Core.FfMpeg.Codecs;
using StreamingCaptureBot.Core.Utils;

namespace StreamingCaptureBot.Core.FfMpeg.Extensions.DependencyInjection;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddCodecs(this IServiceCollection services)
    {
        // logger
        services.AddSingleton(sp =>
        {
            if (!_hasConfigured)
                ConfigureFfMpeg(sp);

            var logger = sp.GetRequiredService<ILogger<FfMpegLogger>>();
            var loggerOptions = sp.GetRequiredService<IOptions<LoggerFilterOptions>>();

            return ConfigureFfMpegLogger(logger, loggerOptions);
        });

        // libewbp
        services.AddSingleton<FfmpegLibWebpEncoder>(sp =>
        {
            if (!_hasConfigured)
                ConfigureFfMpeg(sp);

            var logger = sp.GetRequiredService<ILogger<FfmpegLibWebpEncoder>>();
            var formatter = sp.GetRequiredService<BinarySizeFormatter>();
            return new(logger, formatter);
        });

        // auto-detected decoder
        services.AddSingleton(sp =>
        {
            if (!_hasConfigured)
                ConfigureFfMpeg(sp);

            var streamOption = sp.GetRequiredService<IOptions<StreamOption>>();
            var logger = sp.GetRequiredService<ILogger<GenericDecoder>>();
            unsafe
            {
                var url = streamOption.Value.Url.AbsoluteUri;
                AVDictionary* openOptions = null;

                // 设置超时
                if (streamOption.Value.ConnectTimeout > 0)
                    ffmpeg.av_dict_set(&openOptions, "timeout", streamOption.Value.ConnectTimeout.ToString(), 0);

                logger.LogDebug("Open Input {url}.", url);

                var formatCtx = ffmpeg.avformat_alloc_context();

                // 打开流
                ffmpeg.avformat_open_input(&formatCtx, url, null, &openOptions)
                    .ThrowExceptionIfError();

                ffmpeg.avformat_find_stream_info(formatCtx, null)
                    .ThrowExceptionIfError();

                // 匹配解码器信息
                AVCodec* decoder = null;

                var streamIndex = ffmpeg
                    .av_find_best_stream(formatCtx, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0)
                    .ThrowExceptionIfError();
                if (streamOption.Value.StreamIndex < 0)
                    streamOption.Value.StreamIndex = streamIndex;

                var stream = formatCtx->streams[streamOption.Value.StreamIndex];
                var dec = new GenericDecoder(logger, decoder);

                ffmpeg.avcodec_parameters_to_context(
                        dec.Context.UnmanagedPointer, stream->codecpar)
                    .ThrowExceptionIfError();
                dec.Context.TimeBase = stream->time_base;

                logger.LogDebug("Close Input.");

                ffmpeg.avformat_close_input(&formatCtx);
                ffmpeg.avformat_free_context(formatCtx);

                return dec;
            }
        });
        return services;
    }

    private static bool _hasConfigured;

    private static FfMpegLogger ConfigureFfMpegLogger(ILogger<FfMpegLogger> logger, IOptions<LoggerFilterOptions> loggerOptions)
    {
        var fastForwardMovingPictureExpertsGroupLogger =
            new FfMpegLogger(logger, loggerOptions);
        fastForwardMovingPictureExpertsGroupLogger.ConfigureFfMpegLogger();
        return fastForwardMovingPictureExpertsGroupLogger;
    }

    private static void ConfigureFfMpeg(IServiceProvider provider)
    {
        var logger = provider.GetRequiredService<ILogger<FfMpegLogger>>();
        var streamOptions = provider.GetRequiredService<IOptions<StreamOption>>();

        // use linked
        if (streamOptions.Value.FfMpegLibrariesPath is null)
        {
            logger.LogInformation("ffmpeg library path not set, use {bind}.", nameof(FFmpeg.AutoGen.Bindings.DynamicallyLinked));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var bindingAssembly = typeof(DynamicallyLinkedBindings).Assembly;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                {
                    logger.LogInformation("Linux/FreeBSD platform, use resolver {name} for assembly `{asm}`.", nameof(LibraryUtil.LinuxFfMpegDllImportResolver), bindingAssembly.GetName());
                    NativeLibrary.SetDllImportResolver(bindingAssembly, LibraryUtil.LinuxFfMpegDllImportResolver);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    logger.LogInformation("OSX platform, use resolver {name} for assembly `{asm}`.", nameof(LibraryUtil.LinuxFfMpegDllImportResolver), bindingAssembly.GetName());
                    NativeLibrary.SetDllImportResolver(bindingAssembly, LibraryUtil.MacOsFfMpegDllImportResolver);
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }
            }

            DynamicallyLinkedBindings.Initialize();
        }
        else
        {
            DynamicallyLoadedBindings.LibrariesPath = streamOptions.Value.FfMpegLibrariesPath;

            if (streamOptions.Value.FfMpegLibrariesPath == string.Empty)
            {
                logger.LogInformation("ffmpeg library path set to system default search path, use {bind}.",
                    nameof(FFmpeg.AutoGen.Bindings.DynamicallyLoaded));
            }
            else
            {
                logger.LogInformation("ffmpeg library path set to `{path}`, use {bind}.",
                    DynamicallyLoadedBindings.LibrariesPath = streamOptions.Value.FfMpegLibrariesPath,
                    nameof(FFmpeg.AutoGen.Bindings.DynamicallyLoaded));
            }

            DynamicallyLoadedBindings.ThrowErrorIfFunctionNotFound = true;
            DynamicallyLoadedBindings.Initialize();
        }

        // test ffmpeg load
        try
        {
            var version = ffmpeg.av_version_info();
            var libraryVersion = new StringBuilder(48 * DynamicallyLoadedBindings.LibraryVersionMap.Count);

            foreach (var (library, requiredVersion) in DynamicallyLoadedBindings.LibraryVersionMap)
            {
                var versionFunc = typeof(ffmpeg).GetMethod($"{library}_version");
                var versionInfo = new VersionInfo((uint)(versionFunc?.Invoke(null, null) ?? 0u));

                libraryVersion.AppendLine($"\tLibrary: {library}, require `{requiredVersion}`, load `{versionInfo}`");
            }

            libraryVersion.Remove(libraryVersion.Length - 1, 1);    // remove '\n'

            logger.LogInformation("Load ffmpeg, version `{v}`", version);
            logger.LogInformation("Load ffmpeg, libraries:\n{libInfo}", libraryVersion);
        }
        catch (NotSupportedException e)
        {
            logger.LogCritical(e, "Failed to load ffmpeg, exit.");
            var appLifeTime = provider.GetRequiredService<IHostApplicationLifetime>();
            appLifeTime.StopApplication();
        }

        _hasConfigured = true;
    }
}