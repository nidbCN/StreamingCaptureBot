using Lagrange.Core;
using Lagrange.Core.Common.Interface.Api;
using Lagrange.Core.Message;
using Microsoft.Extensions.Options;
using VideoStreamCaptureBot.Core.Configs;

namespace VideoStreamCaptureBot.Core;

public class HeartBeatWorker(ILogger<HeartBeatWorker> logger,
    BotContext botCtx,
    IOptions<BotOption> botOptions) : BackgroundService
{
    private readonly HttpClient _httpClient = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested
               && (botOptions.Value.NotificationConfig.NotifyWebhookOnHeartbeat
               || botOptions.Value.NotificationConfig.NotifyAdminOnHeartbeat))
        {
            var assemblyName = GetType().Assembly.GetName();
            var msg = $"Bot {botCtx.BotName}@{botCtx.BotUin} running. Time: `{DateTime.Now:F}`, Program: {assemblyName.Name} {assemblyName.Version}.";

            // delay before heartbeat. at the beginning of first loop bot maybe not logged in.
            await Task.Delay(TimeSpan.FromHours(botOptions.Value.NotificationConfig.HeartbeatIntervalHour), stoppingToken);

            if (botOptions.Value.NotificationConfig is
                { NotifyWebhookOnHeartbeat: true, WebhookUrl: not null })
                await ProcessWebhookHeartbeatAsync(msg, stoppingToken);

            if (botOptions.Value.NotificationConfig.NotifyAdminOnHeartbeat)
                await ProcessBotHeartbeatAsync(msg, stoppingToken);
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
