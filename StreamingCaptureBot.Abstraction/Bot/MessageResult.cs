namespace StreamingCaptureBot.Abstraction.Bot;

public class MessageResult
{
    // public IList<IMessagePart> Type { get; set; }
}

public interface IMessagePart<out R>
{
    R GetPart();
}