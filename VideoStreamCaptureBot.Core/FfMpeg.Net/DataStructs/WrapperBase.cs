namespace VideoStreamCaptureBot.Core.FfMpeg.Net.DataStructs;

public class WrapperBase<T> : IDisposable where T : unmanaged
{
    public unsafe T* UnmanagedPointer { get; }

    public WrapperBase()
    {

    }

    public unsafe WrapperBase(T* pointer)
    {
        UnmanagedPointer = pointer;
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
