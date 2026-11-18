namespace CopperDisk.FileSystem;

internal sealed class TrackedRead(Stream inner, Action close) : Stream
{
    private bool disposed;
    public override bool CanRead => !disposed;
    public override bool CanWrite => false;
    public override bool CanSeek => false;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => inner.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => inner.ReadAsync(buffer, ct);
    protected override void Dispose(bool disposing)
    {
        if (!disposed) { disposed = true; try { inner.Dispose(); } finally { close(); } }
        base.Dispose(disposing);
    }
    public override void Flush() { }
    public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
    public override void SetLength(long v) => throw new NotSupportedException();
    public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
}
