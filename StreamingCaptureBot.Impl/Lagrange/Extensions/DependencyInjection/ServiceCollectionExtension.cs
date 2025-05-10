using Lagrange.Core.Common.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Impl.Lagrange.Options;
using StreamingCaptureBot.Impl.Lagrange.Services;

namespace StreamingCaptureBot.Impl.Lagrange.Extensions.DependencyInjection;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddLagrangeBots(this IServiceCollection s, IConfiguration config)
        => s.AddSingleton<StoreService>()
            .Configure<LagrangeImplOption>(config)
            .AddSingleton(provider =>
            {
                // use configured options
                var implOption = provider.GetRequiredService<IOptions<LagrangeImplOption>>().Value;

                var storeService = provider.GetRequiredService<StoreService>();
                var deviceInfo = storeService.ReadDeviceInfo();
                var keyStore = storeService.ReadKeyStore();

                return BotFactory.Create(implOption.LagrangeConfig, deviceInfo, keyStore);
            })
            .AddHostedService<LagrangeHost>();
}
