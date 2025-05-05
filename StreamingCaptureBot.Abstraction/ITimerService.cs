namespace StreamingCaptureBot.Abstraction;

public interface ITimerService
{
    public void StartTimer();

    public void StopTimer();

    public TimeSpan GetUpTime();
}
