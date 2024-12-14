using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StreamingCaptureBot.Impl.Tencent.Options;
using StreamingCaptureBot.Impl.Tencent.Utils.Sign;

namespace StreamingCaptureBot.Impl.Tencent.Extensions.DependencyInjection;
public static class HostApplicationBuilderExtension
{
    public static IHostApplicationBuilder UseTencentBots(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions();

        builder.Services.Configure<TencentImplOption>(
            builder.Configuration
                .GetSection(nameof(TencentImplOption))
            );

        builder.Services.AddTransient<ISignProvider, SodiumSignProvider>();
        builder.Services.AddHostedService<TencentWebhookWorker>();

        return builder;
    }
}
