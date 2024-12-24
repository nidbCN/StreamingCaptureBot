namespace StreamingCaptureBot.Abstraction.Controllers;

public interface ITempBotController
{
    public Task<ActionResult> HandleCaptureImageCommand(BotRequest request);
}

public record ActionResult
{
    public string? Message { get; set; }
    public byte[]? Image { get; set; }
}

public record BotRequest
{
    public uint? GroupUin { get; set; }
    public uint FriendUin { get; set; }
    public string? MessageText { get; set; }
}