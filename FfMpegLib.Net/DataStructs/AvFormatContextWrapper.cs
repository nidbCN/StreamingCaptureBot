using FFmpeg.AutoGen.Abstractions;
using FfMpeg.AutoGen.Wrapper.Extensions;

namespace FfMpeg.AutoGen.Wrapper.DataStructs;

public class AvFormatContextWrapper : WrapperBase<AVFormatContext>
{
    public unsafe AvFormatContextWrapper()
        : base(ffmpeg.avformat_alloc_context()) { }

    public unsafe AvFormatContextWrapper(AVFormatContext* context)
        : base(context) { }

    public bool IsOpen { get; set; }

    public unsafe void Open(string url, AVInputFormat* format, AVDictionary* option)
        => OpenCore(url, format, option).ThrowExceptionIfError();

    public unsafe bool TryOpen(string url, AVInputFormat* format, AVDictionary* option)
        => OpenCore(url, format, option) == 0;

    private unsafe int OpenCore(string url, AVInputFormat* format, AVDictionary* option)
    {
        var formatCtx = UnmanagedPointer;

        var result = ffmpeg.avformat_open_input(&formatCtx, url, format, &option);
        IsOpen = result == 0;
        return result;
    }

    public void Close()
    {
        if (IsOpen)
        {
            unsafe
            {
                var formatCtx = UnmanagedPointer;
                ffmpeg.avformat_close_input(&formatCtx);
            }
        }
    }

    public unsafe bool TryFindStreamInfo(AVDictionary* option = null)
        => FindStreamInfoCore(option) >= 0;

    public unsafe void FindStreamInfo(AVDictionary* option = null)
        => FindStreamInfoCore(option).ThrowExceptionIfError();

    private unsafe int FindStreamInfoCore(AVDictionary* option)
    {
        ffmpeg.avformat_find_stream_info(UnmanagedPointer, &option)
            .ThrowExceptionIfError();
    }

    public override void Dispose()
    {
        unsafe
        {
            if (!IsOpen)
            {
                var formatCtx = UnmanagedPointer;
                ffmpeg.avformat_close_input(&formatCtx);
            }

            ffmpeg.avformat_free_context(UnmanagedPointer);
        }

        GC.SuppressFinalize(this);
    }
}
