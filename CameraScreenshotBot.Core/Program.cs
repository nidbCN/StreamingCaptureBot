using CameraScreenshotBot.Core;
using CameraScreenshotBot.Core.Configs;
using CameraScreenshotBot.Core.Extensions.DependencyInjection;
using CameraScreenshotBot.Core.Services;
using FFmpeg.AutoGen;

DynamicallyLoadedBindings.Initialize();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(s =>
{
    s.ServiceName = "Live stream capture bot";
});

builder.Services.Configure<StreamOption>(
    builder.Configuration.GetSection(nameof(StreamOption)));
builder.Services.Configure<BotOption>(
    builder.Configuration.GetSection(nameof(BotOption)));

var streamConfig = builder.Configuration.GetSection(nameof(StreamOption))
    .Get<StreamOption>();

ffmpeg.RootPath =
    streamConfig?.FfmpegRoot;

builder.Services.AddSingleton<CaptureService>();
builder.Services.AddIsoStorage();
builder.Services.AddBot(() => builder.Configuration
    .GetSection(nameof(BotOption))
    .Get<BotOption>() ?? new BotOption());
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var env = host.Services.GetRequiredService<IHostEnvironment>();

// set current directory
logger.LogDebug("Content Root: {r}, current {c}, base {b}",
    builder.Environment.ContentRootPath,
    Directory.GetCurrentDirectory(),
    AppContext.BaseDirectory
);
Directory.SetCurrentDirectory(env.ContentRootPath);

// test ffmpeg load
//try
//{
//    var version = ffmpeg.av_version_info();
//    logger.LogInformation("Load ffmpeg version {v}", version ?? "unknown");
//}
//catch (NotSupportedException e)
//{
//    logger.LogCritical(e, "Failed to load ffmpeg, exit.");
//    return;
//}

host.Run();