namespace StreamingCaptureBot.Abstraction.Services;

public interface IUpTimerService
{
    public void StartTimer();

    public void StopTimer();

    public TimeSpan GetUpTime();
}
