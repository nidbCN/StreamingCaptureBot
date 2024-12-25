using Lagrange.Core.Common;

namespace StreamingCaptureBot.Impl.Lagrange.Options;

public record LagrangeImplOption
{
    public string KeyStoreFile { get; set; } = "keystore.json";
    public string DeviceInfoFile { get; set; } = "deviceInfo.json";

    public IDictionary<uint, PasswordInfo>? AccountPasswords { get; set; }

    public record PasswordInfo
    {
        public bool Hashed { get; set; } = false;
        public required string Password { get; set; }
    }

    public BotConfig LagrangeConfig { get; set; } = new()
    {
        AutoReconnect = true,
        AutoReLogin = true,
        GetOptimumServer = true,
        Protocol = Protocols.Linux,
        UseIPv6Network = true,
    };
}
