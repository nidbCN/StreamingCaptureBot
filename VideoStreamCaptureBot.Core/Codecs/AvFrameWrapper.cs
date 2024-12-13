using FFmpeg.AutoGen.Abstractions;

namespace VideoStreamCaptureBot.Core.Codecs;

public unsafe class AvFrameWrapper(AVFrame* frame):IDisposable
{
    public AVFrame* FramePointer { get; } = frame;

    public void Dispose()
    {
        var frame = FramePointer;
        ffmpeg.av_frame_unref(frame);
        ffmpeg.av_frame_free(&frame);

        GC.SuppressFinalize(this);
    }
}