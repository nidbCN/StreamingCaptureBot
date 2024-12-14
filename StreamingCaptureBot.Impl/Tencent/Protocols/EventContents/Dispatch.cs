using System.Text.Json.Serialization;

namespace StreamingCaptureBot.Impl.Tencent.Protocols.EventContents;

public record Dispatch : IEventContent
{
    [JsonPropertyName("id")]
    public required string BotId { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime TimeStamp { get; set; }
}

public record Author
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("user_openid")]
    public required string UserOpenId { get; set; }

    [JsonPropertyName("union_openid")]
    public required string UnionOpenId { get; set; }
}