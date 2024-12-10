using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VideoStreamCaptureBot.Impl.Tencent.Options;
using VideoStreamCaptureBot.Impl.Tencent.Utils.Sign;

namespace VideoStreamCaptureBot.Impl.Tencent.Extensions.DependencyInjection;
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
