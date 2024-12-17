using Lagrange.Core;
using Lagrange.Core.Common.Interface.Api;
using Lagrange.Core.Message;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Core.Configs;
using StreamingCaptureBot.Core.Utils;

namespace StreamingCaptureBot.Core;

public class HeartBeatWorker(ILogger<HeartBeatWorker> logger,
    IOptions<BotOption> botOptions,
    BotContext botCtx,
    BinarySizeFormatter formatter) : BackgroundService
{
    private readonly HttpClient _httpClient = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (botOptions.Value.NotificationConfig.NotifyWebhookOnHeartbeat
            || botOptions.Value.NotificationConfig.NotifyAdminOnHeartbeat)
        {
            var assemblyName = GetType().Assembly.GetName();

            var message = "Time: `{0:F}`."
                          + $"Bot {botCtx.BotName}@{botCtx.BotUin} running."
                          + $"Bot app {assemblyName.Name} {assemblyName.Version} "
                          + $"running on {Environment.OSVersion.VersionString}(.NET {Environment.Version})."
                          + "Used memory: {1}.";

            while (!stoppingToken.IsCancellationRequested)
            {
                // delay before heartbeat. at the beginning of first loop bot maybe not logged in.
                await Task.Delay(botOptions.Value.NotificationConfig.HeartbeatInterval,
                    stoppingToken);

                message = string.Format(formatter, message, DateTime.Now, Environment.WorkingSet);

                if (botOptions.Value.NotificationConfig is
                    { NotifyWebhookOnHeartbeat: true, WebhookUrl: not null })
                    await ProcessWebhookHeartbeatAsync(message, stoppingToken);

                if (botOptions.Value.NotificationConfig.NotifyAdminOnHeartbeat)
                    await ProcessBotHeartbeatAsync(message, stoppingToken);
            }
        }
    }

    private async Task ProcessWebhookHeartbeatAsync(string message, CancellationToken stoppingToken)
    {
        var url = botOptions.Value.NotificationConfig.WebhookUrl!;

        var headers = botOptions.Value.NotificationConfig.WebhookHeaders;
        var resp = await _httpClient
            .PostAsync(url, new StringContent(message), stoppingToken);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogInformation("Webhook heartbeat invoked, {code} {msg}.", resp.StatusCode, resp.ReasonPhrase);
        }
        else
        {
            logger.LogWarning("Webhook heartbeat invoked failed, {code} {msg}.", resp.StatusCode, resp.ReasonPhrase);
        }
    }

    private async Task ProcessBotHeartbeatAsync(string message, CancellationToken stoppingToken)
    {
        try
        {
            await Parallel.ForEachAsync(botOptions.Value.AdminAccounts, stoppingToken,
                async (account, _) =>
                {
                    var botMessage = MessageBuilder
                        .Friend(account)
                        .Text(message)
                        .Build();
                    await botCtx.SendMessage(botMessage);
                    logger.LogInformation("Bot heartbeat invoked.");
                });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Bot heartbeat invoked failed, {msg}.", e.Message);
        }
    }
}
