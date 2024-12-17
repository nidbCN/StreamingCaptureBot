namespace StreamingCaptureBot.Core.Utils;

public sealed class BinarySizeFormatter : IFormatProvider, ICustomFormatter
{
    private const int _1k = 1024;
    private const int _1m = _1k * _1k;
    private const int _1g = _1k * _1m;
    private const long _1t = (long)_1k * _1g;

    public object? GetFormat(Type? formatType)
        => formatType == typeof(ICustomFormatter) ? this : null;

    public string Format(string? format, object? arg, IFormatProvider? formatProvider)
     => arg switch
     {
         uint usize => FormatSize(usize),
         int size => size < 0 ? '-' + FormatSize((uint)-size) : FormatSize((uint)size),
         ulong uLongSize => FormatSize(uLongSize),
         long longSize => longSize < 0 ? '-' + FormatSize((ulong)-longSize) : FormatSize((ulong)longSize),
         _ => string.Format(formatProvider, format!, arg)
     };

    private static string FormatSize(ulong size)
        => size switch
        {
            < _1k => $"{size}B",
            < _1m => $"{size / (double)_1k:F2}KB",
            < _1g => $"{size / (double)_1m:F2}MB",
            < _1t => $"{size / (double)_1g:F2}GB",
            _ => $"{size / (double)_1t:F2}TB"
        };

    private static string FormatSize(uint size)
        => size switch
        {
            < _1k => $"{size}B",
            < _1m => $"{size / (double)_1k:F2}KB",
            < _1g => $"{size / (double)_1m:F2}MB",
            _ => $"{size / (double)_1g:F2}GB"
        };
}