using Lagrange.Core;
using Lagrange.Core.Common.Interface.Api;
using Lagrange.Core.Message;
using Microsoft.Extensions.Options;
using StreamingCaptureBot.Abstraction.Options;
using StreamingCaptureBot.Abstraction.Services;
using StreamingCaptureBot.Hosting.Utils;

namespace StreamingCaptureBot.Hosting;

public class HeartBeatWorker(ILogger<HeartBeatWorker> logger,
    IOptions<BotOption> botOptions,
    BotContext botCtx,
    BinarySizeFormatter formatter,
    IUpTimerService timerService) : BackgroundService
{
    private readonly HttpClient _httpClient = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (botOptions.Value.NotificationConfig.NotifyWebhookOnHeartbeat
            || botOptions.Value.NotificationConfig.NotifyAdminOnHeartbeat)
        {
            var assemblyName = GetType().Assembly.GetName();

            var template = "Heartbeat time: {0:G}.\n"
                           + @"Online: {1:%d} days {1:hh\:mm\:ss}\n"
                          + $"Bot: {botCtx.BotName}@{botCtx.BotUin}\n"
                          + $"Bot app `{assemblyName.Name} v{assemblyName.Version}`, "
                          + $"running on {Environment.OSVersion.VersionString}(.NET {Environment.Version}) "
                          + "with {1} memory used.";

            while (!stoppingToken.IsCancellationRequested)
            {
                // delay before heartbeat. at the beginning of first loop bot maybe not logged in.
                await Task.Delay(botOptions.Value.NotificationConfig.HeartbeatInterval,
                    stoppingToken);

                var upTime = timerService.GetUpTime();
                var message = string.Format(formatter, template, DateTime.Now, upTime, upTime, Environment.WorkingSet);

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

        // var headers = botOptions.Value.NotificationConfig.WebhookHeaders;
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
                    logger.LogInformation("Bot heartbeat invoked, message: `{msg}`.", message);
                });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Bot heartbeat invoked failed, {msg}.", e.Message);
        }
    }
}
