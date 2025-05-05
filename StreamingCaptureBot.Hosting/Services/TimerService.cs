using StreamingCaptureBot.Abstraction;

namespace StreamingCaptureBot.Hosting.Services;

public class TimerService() : ITimerService
{
    private DateTime _upTime;

    public void StartTimer()
    {
        _upTime = DateTime.Now;
    }

    public void StopTimer()
    {
        _upTime = DateTime.MaxValue;
    }

    public TimeSpan GetUpTime()
        => DateTime.Now - _upTime;
}