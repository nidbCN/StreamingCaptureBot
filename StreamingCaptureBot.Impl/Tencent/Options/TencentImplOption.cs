using System.Net;
using System.Text.Json.Serialization;
using StreamingCaptureBot.Impl.Tencent.Json.Serialization;

namespace StreamingCaptureBot.Impl.Tencent.Options;
public record TencentImplOption
{
    public string ListenIpAddress { get; set; } = IPAddress.Loopback.ToString();

    public uint ListenPort { get; set; } = 5033;
    
    public string Route { get; set; } = "/";

    public required uint AppId { get; set; }
    
    public required string AppSecret { get; set; }
    
    public required string Token { get; set; }
}
