namespace CopperScreen;

// UI dispatch may wait for a dispatcher lock or platform signal. The PCM owner
// only signals this reusable, coalesced work item; it never performs that wait.
internal sealed class CopperScreenFrameNotification(Action post) : IThreadPoolWorkItem, IDisposable
{
    private int _pending, _scheduled, _disposed;
    internal void Signal()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        Volatile.Write(ref _pending, 1);
        QueueIfNeeded();
    }
    private void QueueIfNeeded()
    {
        if (Volatile.Read(ref _disposed) == 0 && Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0)
            ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: false);
    }
    void IThreadPoolWorkItem.Execute()
    {
        Interlocked.Exchange(ref _pending, 0);
        try { if (Volatile.Read(ref _disposed) == 0) post(); }
        catch (InvalidOperationException) when (Volatile.Read(ref _disposed) != 0) { }
        finally
        {
            Volatile.Write(ref _scheduled, 0);
            if (Volatile.Read(ref _pending) != 0) QueueIfNeeded();
        }
    }
    public void Dispose() { Volatile.Write(ref _disposed, 1); Interlocked.Exchange(ref _pending, 0); }
}
