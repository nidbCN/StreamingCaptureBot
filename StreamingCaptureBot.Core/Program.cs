using StreamingCaptureBot.Core;
using StreamingCaptureBot.Core.Configs;
using StreamingCaptureBot.Core.Controllers;
using StreamingCaptureBot.Core.Extensions.DependencyInjection;
using StreamingCaptureBot.Core.FfMpeg.Extensions.DependencyInjection;
using StreamingCaptureBot.Core.Services;
using StreamingCaptureBot.Core.Utils;
using StreamingCaptureBot.Impl.Tencent.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

var botOption = builder.Configuration
    .GetSection(nameof(BotOption));

switch ((botOption.Get<BotOption>() ?? new()).BotImplement)
{
    case BotOption.Implement.Lagrange:
        builder.UseLagrangeBots();
        break;
    case BotOption.Implement.Tencent:
        builder.UseTencentBots();
        break;
    default:
        throw new ArgumentOutOfRangeException(nameof(BotOption.BotImplement));
}

builder.Services.AddWindowsService(s =>
{
    s.ServiceName = "Live stream capture bot";
});

builder.Services.Configure<BotOption>(botOption);
builder.Services.Configure<StreamOption>(
    builder.Configuration.GetRequiredSection(nameof(StreamOption)));

builder.Services.AddCodecs();
builder.Services.AddLogging();

builder.Services.AddTransient<BinarySizeFormatter>();

builder.Services.AddSingleton<CaptureService>();
builder.Services.AddSingleton<BotController>();

builder.Services.AddHostedService<HeartBeatWorker>();

builder.Build().Run();