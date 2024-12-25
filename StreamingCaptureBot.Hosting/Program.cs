using StreamingCaptureBot.Abstraction.Controllers;
using StreamingCaptureBot.Abstraction.Options;
using StreamingCaptureBot.Hosting;
using StreamingCaptureBot.Hosting.Configs;
using StreamingCaptureBot.Hosting.Controllers;
using StreamingCaptureBot.Hosting.FfMpeg.Extensions.DependencyInjection;
using StreamingCaptureBot.Hosting.Services;
using StreamingCaptureBot.Hosting.Utils;
using StreamingCaptureBot.Impl.Lagrange.Extensions.DependencyInjection;
using StreamingCaptureBot.Impl.Tencent.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

var botOption = builder.Configuration
    .GetSection(nameof(BotOption));

builder.Services.Configure<BotOption>(botOption);

// ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
switch ((botOption.Get<BotOption>() ?? new()).BotImplement)
{
    case BotOption.Implement.Lagrange:
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
builder.Services.AddSingleton<ITempBotController, TempBotController>();

builder.Services.AddHostedService<HeartBeatWorker>();

builder.Build().Run();