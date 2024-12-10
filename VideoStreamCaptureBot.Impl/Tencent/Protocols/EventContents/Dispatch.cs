using System.Text.Json.Serialization;

namespace VideoStreamCaptureBot.Impl.Tencent.Protocols.EventContents;

public record Dispatch : IEventContent
{
    [JsonPropertyName("id")]
    public string BotId { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime TimeStamp { get; set; }
}

public record Author
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("user_openid")]
    public string UserOpenId { get; set; }

    [JsonPropertyName("union_openid")]
    public string UnionOpenId { get; set; }
}