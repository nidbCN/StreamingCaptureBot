using Lagrange.Core.Common.Interface;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Core.Configs;
using StreamingCaptureBot.Core.Services;

namespace StreamingCaptureBot.Core.Bots.LagrangeBot.Extensions.DependencyInjection;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddLagrangeBots(this IServiceCollection s)
        => s.AddSingleton<StoreService>()
            .AddSingleton(provider =>
            {
                var configSection = provider.GetRequiredService<IConfiguration>()
                    .GetSection(nameof(LagrangeImplOption));
                s.Configure<LagrangeImplOption>(configSection);

                var config = configSection.Get<LagrangeImplOption>();

                var storeService = provider.GetRequiredService<StoreService>();
                var deviceInfo = storeService.ReadDeviceInfo();
                var keyStore = storeService.ReadKeyStore();

                return BotFactory.Create(config?.LagrangeConfig ?? new(), deviceInfo, keyStore);
            })
            .AddHostedService<LagrangeHost>();
}
