namespace StreamingCaptureBot.Core.Configs;

public record StreamOption
{
    public string? FfMpegLibrariesPath { get; set; } = null;
    public required Uri Url { get; set; }
    public TimeSpan CacheTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public uint ConnectTimeout { get; set; } = 0;
    public TimeSpan CodecTimeout { get; set; } = TimeSpan.FromSeconds(12);
    public uint CodecThreads { get; set; } = (uint)Environment.ProcessorCount;
}
