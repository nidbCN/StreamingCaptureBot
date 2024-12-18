using StreamingCaptureBot.Core;
using StreamingCaptureBot.Core.Bots.LagrangeBot.Extensions.DependencyInjection;
using StreamingCaptureBot.Core.Configs;
using StreamingCaptureBot.Core.Controllers;
using StreamingCaptureBot.Core.FfMpeg.Extensions.DependencyInjection;
using StreamingCaptureBot.Core.Services;
using StreamingCaptureBot.Core.Utils;
using StreamingCaptureBot.Impl.Tencent.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(s =>
{
    s.ServiceName = "Live stream capture bot";
});

var botOption = builder.Configuration
    .GetSection(nameof(BotOption));

builder.Services.Configure<BotOption>(botOption);

// ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
switch ((botOption.Get<BotOption>() ?? new()).BotImplement)
{
    case BotOption.Implement.Lagrange:
        builder.Services.Configure<LagrangeImplOption>(
            builder.Configuration.GetSection(nameof(LagrangeImplOption)));
        builder.Services.AddLagrangeBots();
        break;
    case BotOption.Implement.Tencent:
        builder.UseTencentBots();
        break;
}

builder.Services.Configure<StreamOption>(
    builder.Configuration.GetRequiredSection(nameof(StreamOption)));

builder.Services.AddCodecs();
builder.Services.AddLogging();

builder.Services.AddTransient<BinarySizeFormatter>();

builder.Services.AddSingleton<CaptureService>();
builder.Services.AddSingleton<BotController>();

builder.Services.AddHostedService<HeartBeatWorker>();

builder.Build().Run();