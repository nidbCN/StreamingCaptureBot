namespace StreamingCaptureBot.Abstraction.Bot.Entities;

public record BotRequest
{
    public required CallerInfo Caller { get; init; }
    public IList<byte[]> ImagePart { get; init; } = new List<byte[]>();
    public string OriginalText { get; init; } = string.Empty;
    public IList<string> TextPart { get; init; } = new List<string>();
}

public record CallerInfo
{
    public CallerType CallerType { get; set; }

    public uint? CallerUin { get; set; }

}

public enum CallerType
{
    Group, Friend, Platform
}
