using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using FfMpeg.AutoGen.Wrapper.Extensions;

namespace FfMpeg.AutoGen.Wrapper.DataStructs;

public class AvDictionaryWrapper : WrapperBase<AVDictionary>
{
    public string this[string key]
    {
        get => GetValue(key);
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        set => SetValue(key, value ?? string.Empty);
    }

    public string GetValue(string key, AvDictionaryFlag flag = DefaultFlag)
    {
        var val = GetValueCore(key, flag);
        if (val is null)
            throw new ArgumentOutOfRangeException(nameof(key));

        return val;
    }

    public bool TryGetValue(string key, out string? val, AvDictionaryFlag flag = DefaultFlag)
    {
        val = GetValueCore(key, flag);
        return val is not null;
    }

    private string? GetValueCore(string key, AvDictionaryFlag flag = DefaultFlag)
    {
        unsafe
        {
            var entry = ffmpeg.av_dict_get(UnmanagedPointer, key, null, (int)flag);
            return entry is null
                ? null
                : Marshal.PtrToStringAuto((IntPtr)entry->value);
        }
    }

    public void SetValue(string key, string value, AvDictionaryFlag flag = DefaultFlag)
        => SetValueCore(key, value, flag).ThrowExceptionIfError();

    public bool TrySetValue(string key, string value, AvDictionaryFlag flag = DefaultFlag)
        => SetValueCore(key, value, flag) >= 0;

    private int SetValueCore(string key, string? value, AvDictionaryFlag flag = DefaultFlag)
    {
        unsafe
        {
            var ptr = UnmanagedPointer;
            return ffmpeg.av_dict_set(&ptr, key, value, (int)flag);
        }
    }

    public void Remove(string key, AvDictionaryFlag flag = DefaultFlag)
        => SetValueCore(key, null, flag)
            .ThrowExceptionIfError();

    public bool TryRemove(string key, AvDictionaryFlag flag = DefaultFlag)
        => SetValueCore(key, null, flag) >= 0;

    private const AvDictionaryFlag DefaultFlag = AvDictionaryFlag.MatchCase;
}

[Flags]
public enum AvDictionaryFlag
{
    None = 0x00,
    MatchCase = ffmpeg.AV_DICT_MATCH_CASE,
    IgnoreSuffix = ffmpeg.AV_DICT_IGNORE_SUFFIX,
    DoNotStrDupKey = ffmpeg.AV_DICT_DONT_STRDUP_KEY,
    DoNotStrDupVal = ffmpeg.AV_DICT_DONT_STRDUP_VAL,
    DoNotOverwrite = ffmpeg.AV_DICT_DONT_OVERWRITE,
    Append = ffmpeg.AV_DICT_APPEND,
    MultiKey = ffmpeg.AV_DICT_MULTIKEY
}
