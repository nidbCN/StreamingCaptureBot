using Lagrange.Core.Common.Interface;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Core.Configs;
using StreamingCaptureBot.Core.Services;

namespace StreamingCaptureBot.Core.Bots.LagrangeBot.Extensions.DependencyInjection;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddLagrangeBots(this IServiceCollection services)
    {
        services.AddSingleton<StoreService>();
        services.AddSingleton(provider =>
        {
            var storeService = provider.GetRequiredService<StoreService>();
            var implOption = provider.GetRequiredService<IOptions<LagrangeImplOption>>();

            var deviceInfo = storeService.ReadDeviceInfo();
            var keyStore = storeService.ReadKeyStore();

            return BotFactory.Create(implOption.Value.LagrangeConfig, deviceInfo, keyStore);
        });
        services.AddHostedService<LagrangeHost>();

        return services;
    }
}
