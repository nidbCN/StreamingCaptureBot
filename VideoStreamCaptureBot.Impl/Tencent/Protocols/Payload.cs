using System.Text.Json;
using System.Text.Json.Serialization;
using VideoStreamCaptureBot.Impl.Tencent.Protocols.EventContents;

namespace VideoStreamCaptureBot.Impl.Tencent.Protocols;

public record Payload
{
    [JsonPropertyName("id")]
    public string EventId { get; set; }

    [JsonPropertyName("op")]
    public OperationCode OperationCode { get; set; }

    [JsonPropertyName("d")]
    public JsonElement JsonEventContent { get; set; }

    [JsonPropertyName("s")]
    public uint Sequence { get; set; }

    [JsonPropertyName("t")]
    public string EventType { get; set; }

    public T? GetEventContent<T>() where T : IEventContent
    => JsonEventContent.Deserialize<T>();
}

public enum OperationCode
{
    /// <summary>
    /// 服务端进行消息推送
    /// </summary>
    Dispatch = 0,

    /// <summary>
    /// 仅用于 http 回调模式的回包，代表机器人收到了平台推送的数据
    /// </summary>
    HttpCallbackAck = 12,

    /// <summary>
    /// 开放平台对机器人服务端进行验证
    /// </summary>
    HttpCallbackVerify = 13,
}
