namespace VideoStreamCaptureBot.Core.Configs;

public record BotOption
{
    public enum Implement
    {
        Lagrange,
        Tencent
    }

    public IList<uint>? AllowedGroups { get; set; } = null;
    public IList<uint>? AllowedFriends { get; set; } = null;
    public IList<uint> AdminAccounts { get; set; } = [];

    public NotificationConfig NotificationConfig { get; set; } = new();

    public Implement BotImplement { get; set; } = Implement.Lagrange;
}

public record NotificationConfig
{
    public bool NotifyAdminOnException { get; set; } = true;
    public bool NotifyWebhookOnException { get; set; } = false;
    public bool NotifyAdminOnHeartbeat { get; set; } = false;
    public bool NotifyWebhookOnHeartbeat { get; set; } = false;
    public uint HeartbeatIntervalHour { get; set; } = 6;
    public Uri? WebhookUrl { get; set; }
    public IDictionary<string, string?>? WebhookHeaders { get; set; }
}
