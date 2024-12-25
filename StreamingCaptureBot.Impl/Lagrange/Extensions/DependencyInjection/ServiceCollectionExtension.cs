using Lagrange.Core.Common.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Impl.Lagrange.Options;
using StreamingCaptureBot.Impl.Lagrange.Services;

namespace StreamingCaptureBot.Impl.Lagrange.Extensions.DependencyInjection;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddLagrangeBots(this IServiceCollection s, Action<LagrangeImplOption>? config = null)
        => s.AddSingleton<StoreService>()
            .AddSingleton(provider =>
            {
                // use configured options
                var implOption = provider.GetService<IOptions<LagrangeImplOption>>()?.Value;
                
                // use config file
                if (implOption is null)
                {
                    var configSection = provider.GetRequiredService<IConfiguration>()
                        .GetSection(nameof(LagrangeImplOption));
                    implOption = configSection.Get<LagrangeImplOption>() ?? new();
                }

                // use user-defined config
                config?.Invoke(implOption);

                var storeService = provider.GetRequiredService<StoreService>();
                var deviceInfo = storeService.ReadDeviceInfo();
                var keyStore = storeService.ReadKeyStore();

                return BotFactory.Create(implOption.LagrangeConfig, deviceInfo, keyStore);
            })
            .AddHostedService<LagrangeHost>();
}
