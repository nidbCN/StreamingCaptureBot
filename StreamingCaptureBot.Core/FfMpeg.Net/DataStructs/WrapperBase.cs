namespace StreamingCaptureBot.Core.FfMpeg.Net.DataStructs;

public abstract class WrapperBase<T> : IDisposable where T : unmanaged
{
    public unsafe T* UnmanagedPointer { get; }

    protected WrapperBase()
    {

    }

    protected unsafe WrapperBase(T* pointer)
    {
        UnmanagedPointer = pointer;
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
