using VideoStreamCaptureBot.Core;
using VideoStreamCaptureBot.Core.Configs;
using VideoStreamCaptureBot.Core.Controllers;
using VideoStreamCaptureBot.Core.Extensions.DependencyInjection;
using VideoStreamCaptureBot.Core.FfMpeg.Net.Codecs;
using VideoStreamCaptureBot.Core.Services;
using VideoStreamCaptureBot.Core.Utils;
using VideoStreamCaptureBot.Impl.Tencent.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

var botOption = builder.Configuration
    .GetSection(nameof(BotOption))
    .Get<BotOption>() ?? new();

switch (botOption.BotImplement)
{
    case BotOption.Implement.Lagrange:
        builder.UseLagrangeBots();
        break;
    case BotOption.Implement.Tencent:
        builder.UseTencentBots();
        break;
    default:
        throw new ArgumentOutOfRangeException(nameof(botOption.BotImplement));
}

builder.Services.AddWindowsService(s =>
{
    s.ServiceName = "Live stream capture bot";
});

builder.Services.Configure<StreamOption>(
    builder.Configuration.GetRequiredSection(nameof(StreamOption)));

builder.Services.AddTransient<BinarySizeFormatter>();

builder.Services.AddSingleton<FfmpegLibWebpEncoder>();
builder.Services.AddSingleton<CaptureService>();
builder.Services.AddSingleton<BotController>();

builder.Services.AddHostedService<FfMpegConfigureHost>();
builder.Services.AddHostedService<HeartBeatWorker>();

builder.Services.AddLogging();

builder.Build().Run();