using StreamingCaptureBot.Core.Controllers;
using StreamingCaptureBot.Impl.Tencent;

namespace StreamingCaptureBot.Core.Bots.TencentBot;

public class TencentHost(
    ILogger<TencentHost> logger,
    TencentWebhookWorker worker,
    BotController controller) : IHostedLifecycleService
{

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Configuring Tencent bot.");

        worker.CapturedCommandReceivedInvoke = () =>
        {
            var r = controller.HandleCaptureImageCommand(null!)
                .Result;
            return r.Image!;
        };

        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
