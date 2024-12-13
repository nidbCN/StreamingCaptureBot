namespace VideoStreamCaptureBot.Core.FfMpeg.Net.DependencyInjection;

public interface IFfMpegServices
{
    public void Initialize();
    public void Initialized();
    public void EnsureInitialized();
}
