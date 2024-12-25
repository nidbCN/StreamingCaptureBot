namespace StreamingCaptureBot.Abstraction.Bot.Routing;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class BindFriendMessageAttribute : Attribute
{
    /// <summary>
    /// 绑定的好友
    /// </summary>
    public IList<uint>? BindFriends { get; set; }

    public string TextPartStart { get; set; } = string.Empty;

    public string TextPartContains { get; set; } = string.Empty;
}
