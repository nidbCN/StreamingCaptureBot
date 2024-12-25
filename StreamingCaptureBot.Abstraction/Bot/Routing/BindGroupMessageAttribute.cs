namespace StreamingCaptureBot.Abstraction.Bot.Routing;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class BindGroupMessageAttribute : Attribute
{
    /// <summary>
    /// 绑定的群组
    /// </summary>
    public IList<uint>? BindGroups { get; set; }
}
