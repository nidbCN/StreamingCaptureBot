namespace StreamingCaptureBot.Core.FfMpeg.Extensions.DependencyInjection;

public interface IFfMpegServices
{
    public void Initialize();
    public void Initialized();
    public void EnsureInitialized();
}
